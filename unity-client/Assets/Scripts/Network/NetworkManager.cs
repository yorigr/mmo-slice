// NetworkManager.cs
// Gerencia a conexão WebSocket com o servidor Node.js + Socket.IO.
//
// Arquitetura:
//   - Singleton (DontDestroyOnLoad) — uma única instância vive durante toda a sessão
//   - NativeWebSocket cuida do WebSocket puro; SocketIOParser trata o envelope Socket.IO v4
//   - Reconexão automática com backoff exponencial (3s → 6s → 12s → 30s max)
//   - Ping/pong a cada 5s para medir RTT e manter a conexão viva em proxies
//   - Despacha eventos via System.Action — simples, sem overhead de UnityEvent
//
// Como usar em outros scripts:
//   NetworkManager.Instance.OnEvent["world:update"] += OnWorldUpdate;
//   NetworkManager.Instance.Emit("player:move", JsonUtility.ToJson(moveData));

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NativeWebSocket;

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
        [SerializeField] private float reconnectBaseDelay = 3f;   // Primeiro retry: 3s
        [SerializeField] private float reconnectMaxDelay  = 30f;  // Teto do backoff

        [Header("Keepalive")]
        [SerializeField] private float pingIntervalSeconds = 5f;  // Envia ping a cada 5s

        // ─── Estado público ───────────────────────────────────────────────────────
        public bool IsConnected => _ws != null && _ws.State == WebSocketState.Open;

        /// <summary>RTT medido pelo último ping/pong em milissegundos.</summary>
        public float LatencyMs { get; private set; }

        // ─── Eventos ──────────────────────────────────────────────────────────────
        /// <summary>
        /// Chamado quando a conexão é estabelecida E o Socket.IO retorna "40" (connect).
        /// Só aqui é seguro fazer Emit — o socket já está pronto.
        /// </summary>
        public event Action OnConnected;

        /// <summary>Chamado quando a conexão é perdida (erro ou fechamento).</summary>
        public event Action OnDisconnected;

        /// <summary>
        /// Dicionário de callbacks por nome de evento Socket.IO.
        /// Uso: NetworkManager.Instance.OnEvent["world:update"] += handler;
        /// </summary>
        public Dictionary<string, Action<string>> OnEvent { get; } = new();

        // ─── Internos ─────────────────────────────────────────────────────────────
        private WebSocket _ws;
        private Coroutine _reconnectCoroutine;
        private Coroutine _pingCoroutine;
        private int       _reconnectAttempt = 0;
        private bool      _intentionalDisconnect = false; // Evita reconectar após Disconnect() manual
        private float     _pingSentTime;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            // Singleton clássico com DontDestroyOnLoad
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
            // NativeWebSocket requer dispatch manual no thread principal no WebGL.
            // No standalone também é necessário para processar callbacks da fila.
#if !UNITY_WEBGL || UNITY_EDITOR
            _ws?.DispatchMessageQueue();
#endif
        }

        private void OnDestroy()
        {
            // Garante que a conexão seja fechada limpa ao destruir o objeto
            _ = _ws?.Close();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Inicia a conexão com o servidor.
        /// Pode ser chamado a qualquer momento — se já conectado, não faz nada.
        /// </summary>
        public async void Connect()
        {
            if (IsConnected) return;

            _intentionalDisconnect = false;
            _reconnectAttempt = 0;
            await OpenWebSocket();
        }

        /// <summary>
        /// Fecha a conexão de forma intencional (sem reconexão automática).
        /// Use no logout ou ao encerrar o jogo.
        /// </summary>
        public async void Disconnect()
        {
            _intentionalDisconnect = true;
            StopReconnect();
            StopPing();

            if (_ws != null)
                await _ws.Close();
        }

        /// <summary>
        /// Envia um evento Socket.IO para o servidor.
        /// jsonPayload: objeto serializado com JsonUtility.ToJson() ou string JSON manual.
        /// </summary>
        public async void Emit(string eventName, string jsonPayload = null)
        {
            if (!IsConnected)
            {
                Debug.LogWarning($"[NetworkManager] Tentou emitir '{eventName}' sem conexão.");
                return;
            }

            string message = SocketIOParser.BuildEmit(eventName, jsonPayload);
            await _ws.SendText(message);
        }

        // ─── Conexão ──────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task OpenWebSocket()
        {
            // Fecha a conexão anterior se existir
            if (_ws != null)
            {
                _ws.OnOpen    -= HandleOpen;
                _ws.OnMessage -= HandleMessage;
                _ws.OnError   -= HandleError;
                _ws.OnClose   -= HandleClose;
                await _ws.Close();
            }

            _ws = new WebSocket(serverUrl);
            _ws.OnOpen    += HandleOpen;
            _ws.OnMessage += HandleMessage;
            _ws.OnError   += HandleError;
            _ws.OnClose   += HandleClose;

            Debug.Log($"[NetworkManager] Conectando em {serverUrl}...");
            await _ws.Connect();
        }

        // ─── Handlers WebSocket ───────────────────────────────────────────────────
        private void HandleOpen()
        {
            // HandleOpen dispara quando o WebSocket TCP abre.
            // Ainda NÃO é seguro emitir — esperamos o "40" do Socket.IO (HandleMessage).
            Debug.Log("[NetworkManager] WebSocket aberto. Aguardando handshake Socket.IO...");
        }

        private void HandleMessage(byte[] bytes)
        {
            string raw = System.Text.Encoding.UTF8.GetString(bytes);
            SocketIOMessage msg = SocketIOParser.Parse(raw);

            switch (msg.Type)
            {
                case SocketIOMessageType.Connect:
                    // "40" — Socket.IO confirmou a sessão. Agora podemos emitir.
                    _reconnectAttempt = 0;
                    StartPing();
                    Debug.Log("[NetworkManager] Socket.IO conectado.");
                    OnConnected?.Invoke();
                    break;

                case SocketIOMessageType.Ping:
                    // "2" — servidor testando se estamos vivos. Responda imediatamente.
                    _ = _ws.SendText(SocketIOParser.PongMessage);
                    break;

                case SocketIOMessageType.Disconnect:
                    Debug.Log("[NetworkManager] Servidor pediu desconexão.");
                    break;

                case SocketIOMessageType.Event:
                    DispatchEvent(msg.EventName, msg.JsonData);
                    break;
            }
        }

        private void HandleError(string errorMsg)
        {
            Debug.LogError($"[NetworkManager] WebSocket erro: {errorMsg}");
        }

        private void HandleClose(WebSocketCloseCode closeCode)
        {
            Debug.Log($"[NetworkManager] Conexão fechada: {closeCode}");
            StopPing();
            OnDisconnected?.Invoke();

            // Reconecta automaticamente, a menos que o disconnect foi intencional
            if (!_intentionalDisconnect)
                StartReconnect();
        }

        // ─── Despacho de eventos ──────────────────────────────────────────────────
        private void DispatchEvent(string eventName, string jsonData)
        {
            if (OnEvent.TryGetValue(eventName, out Action<string> handler))
                handler?.Invoke(jsonData);
            // Eventos sem listener são silenciosamente ignorados (normal durante dev)
        }

        // ─── Reconexão com backoff exponencial ───────────────────────────────────
        private void StartReconnect()
        {
            StopReconnect();
            _reconnectCoroutine = StartCoroutine(ReconnectCoroutine());
        }

        private void StopReconnect()
        {
            if (_reconnectCoroutine != null)
            {
                StopCoroutine(_reconnectCoroutine);
                _reconnectCoroutine = null;
            }
        }

        private IEnumerator ReconnectCoroutine()
        {
            while (!_intentionalDisconnect)
            {
                // Backoff: 3s, 6s, 12s, 24s, 30s, 30s, ...
                float delay = Mathf.Min(reconnectBaseDelay * Mathf.Pow(2, _reconnectAttempt), reconnectMaxDelay);
                _reconnectAttempt++;

                Debug.Log($"[NetworkManager] Reconectando em {delay:F0}s (tentativa {_reconnectAttempt})...");
                yield return new WaitForSeconds(delay);

                if (!_intentionalDisconnect)
                    await OpenWebSocket();

                // Se conectou, para o loop. HandleMessage("40") vai limpar _reconnectAttempt.
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
            if (_pingCoroutine != null)
            {
                StopCoroutine(_pingCoroutine);
                _pingCoroutine = null;
            }
        }

        private IEnumerator PingCoroutine()
        {
            // Registra handler de pong para medir RTT
            // O servidor Socket.IO responde ao nosso ping com "3"
            // Nota: o servidor Socket.IO normalmente envia pings e espera pongs do cliente.
            // Aqui usamos o evento "pong" customizado para medir latência se o servidor suportar.
            // O keepalive real é feito pelo HandleMessage respondendo "2" → "3".

            while (IsConnected)
            {
                yield return new WaitForSeconds(pingIntervalSeconds);

                if (!IsConnected) yield break;

                // Registra tempo de envio para calcular RTT quando receber resposta
                _pingSentTime = Time.realtimeSinceStartup;

                // Emite evento customizado de ping ao servidor (se o servidor suportar)
                // Isso é opcional — o keepalive Socket.IO "2"/"3" já mantém a conexão
                Emit("ping");
            }
        }

        /// <summary>
        /// Chamado pelo GameManager quando o servidor responder ao nosso ping customizado.
        /// Atualiza a latência exibida no HUD.
        /// </summary>
        public void RegisterPong()
        {
            float rtt = (Time.realtimeSinceStartup - _pingSentTime) * 1000f;
            // Suaviza a leitura para evitar variações bruscas no display
            LatencyMs = Mathf.Lerp(LatencyMs, rtt, 0.3f);
        }
    }
}
