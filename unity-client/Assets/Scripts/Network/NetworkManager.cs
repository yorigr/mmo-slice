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

        // Garante que apenas um SendAsync esteja ativo por vez no mesmo WebSocket.
        // ClientWebSocket NÃO permite envios concorrentes — uma segunda chamada a
        // SendAsync enquanto outra está pendente lança InvalidOperationException,
        // o que faz o pong ser perdido e o servidor desconectar por pingTimeout.
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        // Filas thread-safe para despachar para o main thread
        private readonly ConcurrentQueue<string> _messageQueue   = new();
        private readonly ConcurrentQueue<Action> _mainThreadQueue = new();

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            // Unity sobrescreve o operador == para MonoBehaviour: `Instance == null` retorna true
            // mesmo quando a referência C# ainda existe mas o objeto foi destruído.
            // Isso cobre tanto domain reload ativo quanto desabilitado (Enter Play Mode Settings).
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
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
            // Cancela e descarta conexão anterior.
            // IMPORTANTE: capturamos _cts e _ws ANTES de substituí-los para que
            // o ReceiveLoop antigo (que usa os mesmos snapshots) saiba que foi
            // cancelado intencionalmente e não inicie um novo ciclo de reconexão.
            var oldCts = _cts;
            var oldWs  = _ws;

            // Sinaliza cancelamento ANTES de abortar o socket, para que o ReceiveLoop
            // que está em ReceiveAsync possa capturar OperationCanceledException.
            oldCts?.Cancel();

            // Aguarda um frame para dar chance ao ReceiveLoop de processar o cancelamento
            // antes do Abort() mudar o estado do socket de forma abrupta.
            await Task.Delay(10);

            if (oldWs != null)
            {
                try { oldWs.Abort(); } catch { /* ok */ }
                oldWs.Dispose();
            }

            _cts = new CancellationTokenSource();
            _ws  = new ClientWebSocket();

            // Snapshot local: cada chamada a OpenWebSocket captura suas próprias referências,
            // impedindo que um ReceiveLoop stale acesse o ws ou cts desta conexão.
            var thisCts = _cts;
            var thisWs  = _ws;

            Debug.Log($"[NetworkManager] Conectando em {serverUrl}...");

            try
            {
                // Timeout de 8s para não ficar pendurado indefinidamente se o servidor
                // aceitar a conexão TCP mas não completar o handshake WebSocket.
                using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    thisCts.Token, connectTimeout.Token);

                await thisWs.ConnectAsync(new Uri(serverUrl), linkedCts.Token);
                _mainThreadQueue.Enqueue(HandleOpen);
                _ = ReceiveLoop(thisWs, thisCts);
            }
            catch (OperationCanceledException)
            {
                if (!thisCts.IsCancellationRequested)
                    // Timeout esgotado — tratar como falha, não como shutdown intencional
                    Debug.LogWarning("[NetworkManager] ConnectAsync timeout (8s) — servidor não respondeu.");
                else
                    Debug.Log("[NetworkManager] Conexão cancelada intencionalmente.");
                if (!thisCts.IsCancellationRequested)
                    _mainThreadQueue.Enqueue(HandleConnectFailed);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Falha ao conectar: {ex.Message}");
                _mainThreadQueue.Enqueue(HandleConnectFailed);
            }
        }

        /// <summary>
        /// Loop de recepção WebSocket. Recebe ws e cts como parâmetros para que
        /// cada chamada opere sobre suas próprias referências — evita que um loop
        /// stale de reconexão anterior acesse o socket/token da nova conexão.
        /// </summary>
        private async Task ReceiveLoop(ClientWebSocket ws, CancellationTokenSource cts)
        {
            var buffer = new byte[8192];
            var sb     = new StringBuilder();
            var token  = cts.Token;

            try
            {
                while (ws.State == WebSocketState.Open && !token.IsCancellationRequested)
                {
                    sb.Clear();
                    WebSocketReceiveResult result;

                    do
                    {
                        result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), token);

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
            catch (OperationCanceledException) { /* shutdown intencional — não reconectar */ }
            catch (Exception ex)
            {
                // Só notifica como erro se o token NÃO foi cancelado intencionalmente.
                // Isso evita que o Abort() no OpenWebSocket dispare um ciclo extra de reconexão.
                if (!token.IsCancellationRequested)
                {
                    Debug.LogError($"[NetworkManager] ReceiveLoop erro: {ex.Message}");
                    _mainThreadQueue.Enqueue(HandleCloseFromServer);
                }
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
                case SocketIOMessageType.EngineOpen:
                    // "0{...}" — Engine.IO handshake recebido. Cliente DEVE responder com "40"
                    // para solicitar conexão ao namespace Socket.IO padrão "/".
                    SendRaw(SocketIOParser.NamespaceConnectMessage);
                    break;

                case SocketIOMessageType.Connect:
                    // "40" — Socket.IO confirmou a sessão. Seguro emitir agora.
                    _reconnectAttempt = 0;
                    StopReconnect(); // Para o coroutine de reconexão — conexão confirmada
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

            // Captura snapshots ANTES de qualquer await para que referências não mudem
            // se uma reconexão iniciar enquanto esta chamada está pendente.
            var ws  = _ws;
            var cts = _cts;
            if (ws == null || cts == null) return;

            // Serializa envios: ClientWebSocket só permite um SendAsync ativo por vez.
            // Sem esse lock, pong ("3") e player:move competem e um deles lança
            // InvalidOperationException — o pong perdido causa "forced close" no servidor.
            try
            {
                await _sendLock.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                return; // Conexão cancelada enquanto aguardava vez de enviar
            }

            try
            {
                // Re-valida: outro envio pode ter fechado o socket enquanto esperávamos.
                if (ws.State != WebSocketState.Open || cts.IsCancellationRequested) return;

                var bytes = Encoding.UTF8.GetBytes(message);
                await ws.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    cancellationToken: cts.Token
                );
            }
            catch (OperationCanceledException) { /* shutdown intencional — não logar */ }
            catch (Exception ex)
            {
                Debug.LogWarning($"[NetworkManager] SendRaw erro: {ex.Message}");
            }
            finally
            {
                _sendLock.Release();
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

                if (_intentionalDisconnect) yield break;

                _ = OpenWebSocket();

                // Aguarda até 12s pela confirmação Socket.IO ("40") antes de tentar de novo.
                // HandleMessage(Connect) chama StopReconnect() ao confirmar conexão —
                // esse coroutine será interrompido antes de iterar se a conexão funcionar.
                float waited = 0f;
                while (!IsConnected && !_intentionalDisconnect && waited < 12f)
                {
                    yield return new WaitForSeconds(0.5f);
                    waited += 0.5f;
                }

                if (IsConnected || _intentionalDisconnect) yield break;
                // Se não conectou em 12s, o ConnectAsync timeout (8s) já chamou
                // HandleConnectFailed → StartReconnect, que criou um novo coroutine.
                // Este aqui pode sair com segurança.
                yield break;
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
