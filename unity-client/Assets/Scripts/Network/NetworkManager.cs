// NetworkManager.cs
// Gerencia a conexão WebSocket com o servidor Node.js + Socket.IO.
//
// SEM DEPENDÊNCIAS EXTERNAS — usa System.Net.WebSockets (built-in Unity 2021+).
//
// Arquitetura:
//   - Singleton (DontDestroyOnLoad) — uma única instância vive durante toda a sessão
//   - Receive loop em background Task; mensagens despachadas para o main thread via ConcurrentQueue
//   - SocketIOParser trata o envelope Socket.IO v4 (40/2/3/42["event",{...}])
//   - Reconexão automática com backoff exponencial (3s → 6s → 12s → 30s max)
//   - Ping/pong a cada 5s para medir RTT e manter a conexão viva em proxies

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MMORPG.Network
{
    public class NetworkManager : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────
        public static NetworkManager Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Conexão")]
        [Tooltip("URL do servidor. EIO=4 força Engine.IO v4 (Socket.IO v4). transport=websocket evita polling.")]
        [SerializeField] private string serverUrl = "ws://localhost:3000/socket.io/?EIO=4&transport=websocket";

        [Header("Reconexão")]
        [SerializeField] private float reconnectBaseDelay = 3f;
        [SerializeField] private float reconnectMaxDelay  = 30f;

        [Header("Keepalive")]
        [SerializeField] private float pingIntervalSeconds = 5f;

        // ─── Estado público ───────────────────────────────────────────────────────
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        /// <summary>RTT medido pelo último ping/pong em milissegundos.</summary>
        public float LatencyMs { get; private set; }

        // ─── Eventos ──────────────────────────────────────────────────────────────
        /// <summary>Chamado quando Socket.IO confirma sessão ("40"). Seguro emitir após este evento.</summary>
        public event Action OnConnected;

        /// <summary>Chamado quando a conexão é perdida (erro ou fechamento).</summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Callbacks por nome de evento Socket.IO.
        /// Uso: NetworkManager.Instance.OnEvent["world:update"] += handler;
        /// </summary>
        public Dictionary<string, Action<string>> OnEvent { get; } = new();

        // ─── Internos ─────────────────────────────────────────────────────────────
        private ClientWebSocket          _ws;
        private CancellationTokenSource  _cts;
        private Coroutine _reconnectCoroutine;
        private Coroutine _pingCoroutine;
        private int       _reconnectAttempt;
        private bool      _intentionalDisconnect;
        private float     _pingSentTime;

        // Filas thread-safe para despachar para o main thread
        private readonly ConcurrentQueue<string> _messageQueue   = new();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            // Executa ações de estado (open/close/error) no main thread
            while (_mainThreadQueue.TryDequeue(out var action))
                action?.Invoke();

            // Processa mensagens Socket.IO recebidas
            while (_messageQueue.TryDequeue(out var raw))
                HandleMessage(raw);
        }

        private void OnDestroy()
        {
            _intentionalDisconnect = true;
            _cts?.Cancel();
            _ws?.Abort();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Inicia a conexão com o servidor.
        /// Se já conectado, não faz nada.
        /// </summary>
        public void Connect()
        {
            if (IsConnected) return;
            _intentionalDisconnect = false;
            _reconnectAttempt = 0;
            _ = OpenWebSocket();
        }

        /// <summary>
        /// Fecha a conexão de forma intencional (sem reconexão automática).
        /// </summary>
        public async void Disconnect()
        {
            _intentionalDisconnect = true;
            StopReconnect();
            StopPing();
            _cts?.Cancel();

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try { await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "disconnect", CancellationToken.None); }
                catch { /* ignora erros no close */ }
            }
        }

        /// <summary>
        /// Envia um evento Socket.IO para o servidor.
        /// jsonPayload: objeto serializado com JsonUtility.ToJson() ou string JSON manual.
        /// </summary>
        public void Emit(string eventName, string jsonPayload = null)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[NetworkManager] Tentou emitir '{eventName}' sem conexão.");
                return;
            }
            string message = SocketIOParser.BuildEmit(eventName, jsonPayload);
            SendRaw(message);
        }

        // ─── Conexão ──────────────────────────────────────────────────────────────
        private async Task OpenWebSocket()
        {
            // Cancela e descarta conexão anterior
            _cts?.Cancel();
            if (_ws != null)
            {
                try { _ws.Abort(); } catch { /* ok */ }
                _ws.Dispose();
            }

            _cts = new CancellationTokenSource();
            _ws  = new ClientWebSocket();

            Debug.Log($"[NetworkManager] Conectando em {serverUrl}...");

            try
            {
                await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);
                _mainThreadQueue.Enqueue(HandleOpen);
                _ = ReceiveLoop();
            }
            catch (OperationCanceledException) { /* shutdown intencional */ }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Falha ao conectar: {ex.Message}");
                _mainThreadQueue.Enqueue(HandleConnectFailed);
            }
        }

        private async Task ReceiveLoop()
        {
            var buffer = new byte[8192];
            var sb     = new StringBuilder();

            try
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            _mainThreadQueue.Enqueue(HandleCloseFromServer);
                            return;
                        }

                        sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));
                    }
                    while (!result.EndOfMessage);

                    string raw = sb.ToString();
                    if (!string.IsNullOrEmpty(raw))
                        _messageQueue.Enqueue(raw);
                }
            }
            catch (OperationCanceledException) { /* shutdown intencional */ }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] ReceiveLoop erro: {ex.Message}");
                _mainThreadQueue.Enqueue(HandleCloseFromServer);
            }
        }

        // ─── Handlers (main thread) ───────────────────────────────────────────────
        private void HandleOpen()
        {
            // TCP aberto — aguarda o "40" do Socket.IO antes de notificar OnConnected
            Debug.Log("[NetworkManager] WebSocket aberto. Aguardando handshake Socket.IO...");
        }

        private void HandleConnectFailed()
        {
            StopPing();
            OnDisconnected?.Invoke();
            if (!_intentionalDisconnect)
                StartReconnect();
        }

        private void HandleCloseFromServer()
        {
            Debug.Log("[NetworkManager] Conexão fechada pelo servidor.");
            StopPing();
            OnDisconnected?.Invoke();
            if (!_intentionalDisconnect)
                StartReconnect();
        }

        private void HandleMessage(string raw)
        {
            SocketIOMessage msg = SocketIOParser.Parse(raw);

            switch (msg.Type)
            {
                case SocketIOMessageType.Connect:
                    // "40" — Socket.IO confirmou a sessão. Seguro emitir agora.
                    _reconnectAttempt = 0;
                    StartPing();
                    Debug.Log("[NetworkManager] Socket.IO conectado.");
                    OnConnected?.Invoke();
                    break;

                case SocketIOMessageType.Ping:
                    // "2" — servidor testando se estamos vivos. Responda imediatamente.
                    SendRaw(SocketIOParser.PongMessage);
                    break;

                case SocketIOMessageType.Disconnect:
                    Debug.Log("[NetworkManager] Servidor pediu desconexão.");
                    break;

                case SocketIOMessageType.Event:
                    DispatchEvent(msg.EventName, msg.JsonData);
                    break;
            }
        }

        private async void SendRaw(string message)
        {
            if (!IsConnected) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: _cts?.Token ?? CancellationToken.None
                );
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkManager] SendRaw erro: {ex.Message}");
            }
        }

        private void DispatchEvent(string eventName, string jsonData)
        {
            if (OnEvent.TryGetValue(eventName, out Action<string> handler))
                handler?.Invoke(jsonData);
        }

        // ─── Reconexão com backoff exponencial ───────────────────────────────────
        private void StartReconnect()
        {
            StopReconnect();
            _reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }

        private void StopReconnect()
        {
            if (_reconnectCoroutine != null) { StopCoroutine(_reconnectCoroutine); _reconnectCoroutine = null; }
        }

        private IEnumerator ReconnectCoroutine()
        {
            while (!_intentionalDisconnect)
            {
                float delay = Mathf.Min(reconnectBaseDelay * Mathf.Pow(2, _reconnectAttempt), reconnectMaxDelay);
                _reconnectAttempt++;

                Debug.Log($"[NetworkManager] Reconectando em {delay:F0}s (tentativa {_reconnectAttempt})...");
                yield return new WaitForSeconds(delay);

                if (!_intentionalDisconnect)
                    _ = OpenWebSocket();

                if (IsConnected) yield break;
            }
        }

        // ─── Ping / keepalive ─────────────────────────────────────────────────────
        private void StartPing()
        {
            StopPing();
            _pingCoroutine = StartCoroutine(PingCoroutine());
        }

        private void StopPing()
        {
            if (_pingCoroutine != null) { StopCoroutine(_pingCoroutine); _pingCoroutine = null; }
        }

        private IEnumerator PingCoroutine()
        {
            while (IsConnected)
            {
                yield return new WaitForSeconds(pingIntervalSeconds);
                if (!IsConnected) yield break;
                _pingSentTime = Time.realtimeSinceStartup;
                // Servidor escuta "ping_rtt" e responde com "pong_rtt"
                Emit("ping_rtt", $"{{\"t\":{Mathf.FloorToInt(_pingSentTime * 1000)}}}");
            }
        }

        /// <summary>
        /// Chamado pelo GameManager quando o servidor responde ao ping customizado.
        /// Atualiza a latência exibida no HUD.
        /// </summary>
        public void RegisterPong()
        {
            float rtt = (Time.realtimeSinceStartup - _pingSentTime) * 1000f;
            LatencyMs = Mathf.Lerp(LatencyMs, rtt, 0.3f);
        }
    }
}
