// ChatUI.cs
// Chat global e de zona. Canto inferior esquerdo da tela.
//
// Controles:
//   Enter       → abre input / envia mensagem
//   Escape      → fecha input sem enviar
//   Tab         → alterna canal [Z]ona ↔ [G]lobal
//
// Eventos de rede:
//   client → chat:send  { channel:"global"|"zone", message:"..." }
//   server → chat:message { channel, from, message, ts }
//
// Visual:
//   - Log de mensagens com fade automático após FADE_START segundos de inatividade
//   - Fundo semitransparente aparece apenas quando há mensagens recentes ou input aberto
//   - Scroll automático para a mensagem mais recente
//
// Não requer prefab — UI construída proceduralmente em Start().

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.Network;

namespace MMORPG.UI
{
    public class ChatUI : MonoBehaviour
    {
        // ─── Config ───────────────────────────────────────────────────────────────
        private const int   MAX_MESSAGES   = 50;   // histórico máximo
        private const float MSG_HEIGHT     = 22f;  // altura de cada linha em pixels
        private const float FADE_START     = 8f;   // segundos até começar a desvanecer
        private const float FADE_DURATION  = 2.5f; // duração do fade-out

        // ─── UI refs ──────────────────────────────────────────────────────────────
        private ScrollRect      _scroll;
        private RectTransform   _content;
        private TMP_InputField  _input;
        private CanvasGroup     _logGroup;
        private TextMeshProUGUI _placeholder;

        // ─── Estado ───────────────────────────────────────────────────────────────
        private readonly Queue<(string text, float spawnTime, TextMeshProUGUI tmp)> _messages = new();
        private bool   _inputOpen;
        private string _channel = "zone"; // canal atual (zone ou global)

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            BuildUI();
            // Registro via GameManager.Start() (que chama Register após Connect)
            // Mas se NetworkManager já existe quando Start() é chamado, auto-registra
            if (NetworkManager.Instance != null)
                Register(NetworkManager.Instance);
        }

        private void Update()
        {
            HandleKeyboardInput();
            FadeOldMessages();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Registra o evento chat:message no NetworkManager.</summary>
        public void Register(NetworkManager net)
        {
            net.OnEvent["chat:message"] = OnChatMessage;
        }

        /// <summary>Adiciona uma mensagem ao log do chat.</summary>
        public void AddMessage(string from, string text, string channel)
        {
            // Cor do nome por canal
            Color nameColor = channel == "global"
                ? new Color(1f, 0.82f, 0.4f)     // dourado para global
                : new Color(0.6f, 0.9f, 1f);      // azul claro para zona

            string hexColor = ColorUtility.ToHtmlStringRGB(nameColor);
            string prefix   = channel == "global" ? "[G] " : "";
            string display  = $"{prefix}<color=#{hexColor}>{EscapeRichText(from)}</color>: {EscapeRichText(text)}";

            // Cria linha de texto
            var go = new GameObject($"Msg_{_messages.Count % MAX_MESSAGES}");
            go.transform.SetParent(_content, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, MSG_HEIGHT);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text              = display;
            tmp.fontSize          = 13f;
            tmp.color             = Color.white;
            tmp.richText          = true;
            tmp.enableWordWrapping = true;
            tmp.overflowMode      = TextOverflowModes.Ellipsis;

            _messages.Enqueue((display, Time.time, tmp));

            // Descarta mensagens mais antigas que MAX_MESSAGES
            while (_messages.Count > MAX_MESSAGES)
            {
                var old = _messages.Dequeue();
                if (old.tmp != null) Destroy(old.tmp.gameObject);
            }

            RepositionMessages();

            // Auto-scroll para o final
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;

            // Força o log visível imediatamente
            if (_logGroup != null) _logGroup.alpha = 1f;
        }

        // ─── Input do teclado ─────────────────────────────────────────────────────
        private void HandleKeyboardInput()
        {
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);

            if (!_inputOpen)
            {
                if (enter) OpenInput();
                return;
            }

            // Input aberto
            if (enter)
            {
                TrySendMessage();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseInput();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                // Tab capturado antes de o TMP_InputField processar
                _channel = _channel == "zone" ? "global" : "zone";
                UpdatePlaceholder();
            }
        }

        private void OpenInput()
        {
            _inputOpen = true;
            _input.gameObject.SetActive(true);
            _input.text = "";
            _input.Select();
            _input.ActivateInputField();
            UpdatePlaceholder();
        }

        private void CloseInput()
        {
            _inputOpen = false;
            _input.DeactivateInputField();
            _input.gameObject.SetActive(false);
        }

        private void TrySendMessage()
        {
            string msg = _input?.text?.Trim() ?? "";
            CloseInput();

            if (string.IsNullOrEmpty(msg)) return;

            // Sanitiza para JSON (remove aspas e barras inversas)
            msg = msg.Replace("\\", "").Replace("\"", "'");

            string json = $"{{\"channel\":\"{_channel}\",\"message\":\"{msg}\"}}";
            NetworkManager.Instance?.Emit("chat:send", json);
        }

        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            string ch = _channel == "global" ? "[G]" : "[Z]";
            _placeholder.text = $"{ch} Mensagem... (Tab=canal, Esc=cancelar)";
        }

        // ─── Evento de rede ───────────────────────────────────────────────────────
        private void OnChatMessage(string json)
        {
            var data = JsonUtility.FromJson<ChatMessageData>(json);
            if (data == null || string.IsNullOrEmpty(data.message)) return;
            AddMessage(data.from ?? "?", data.message, data.channel ?? "zone");
        }

        // ─── Fade de mensagens antigas ────────────────────────────────────────────
        private void FadeOldMessages()
        {
            // Enquanto o input estiver aberto, o log fica completamente visível
            if (_logGroup != null) _logGroup.alpha = _inputOpen ? 1f : _logGroup.alpha;
            if (_inputOpen) return;

            float now      = Time.time;
            float maxAlpha = 0f;

            foreach (var (_, ts, tmp) in _messages)
            {
                if (tmp == null) continue;
                float age   = now - ts;
                float alpha = age < FADE_START
                    ? 1f
                    : 1f - Mathf.Clamp01((age - FADE_START) / FADE_DURATION);

                Color c = tmp.color;
                tmp.color = new Color(c.r, c.g, c.b, alpha);
                if (alpha > maxAlpha) maxAlpha = alpha;
            }

            if (_logGroup != null)
                _logGroup.alpha = maxAlpha;
        }

        // ─── Layout ───────────────────────────────────────────────────────────────
        private void RepositionMessages()
        {
            // Empilha de baixo para cima (mensagem mais recente = índice mais alto)
            int i = 0;
            foreach (var (_, _, tmp) in _messages)
            {
                if (tmp == null) { i++; continue; }
                var rt = tmp.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(4f, i * MSG_HEIGHT + 4f);
                i++;
            }

            // Expande o container de conteúdo
            if (_content != null)
                _content.sizeDelta = new Vector2(0f, _messages.Count * MSG_HEIGHT + 8f);
        }

        // ─── Construção procedural da UI ──────────────────────────────────────────
        private void BuildUI()
        {
            // Canvas principal — sortingOrder abaixo do RespawnPanel (100 < 200)
            var canvasGO = new GameObject("ChatCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // ── Painel de log (canto inferior esquerdo) ──────────────────────────
            var logPanel = CreateRect(canvasGO, "ChatLog",
                new Vector2(0.005f, 0.055f), new Vector2(0.32f, 0.28f));

            // CanvasGroup para fade global do log
            _logGroup = logPanel.AddComponent<CanvasGroup>();
            _logGroup.alpha          = 0f;
            _logGroup.interactable   = false;
            _logGroup.blocksRaycasts = false;

            // Fundo semitransparente do log
            var logBg = logPanel.AddComponent<Image>();
            logBg.color = new Color(0f, 0f, 0f, 0.35f);

            // ScrollRect dentro do log panel
            var scrollGO = CreateRect(logPanel, "ScrollRect", Vector2.zero, Vector2.one);
            _scroll = scrollGO.AddComponent<ScrollRect>();
            _scroll.horizontal        = false;
            _scroll.vertical          = true;
            _scroll.scrollSensitivity = 15f;
            _scroll.movementType      = ScrollRect.MovementType.Clamped;
            _scroll.inertia           = false;

            // Viewport com máscara
            var viewportGO = CreateRect(scrollGO, "Viewport", Vector2.zero, Vector2.one);
            viewportGO.AddComponent<Image>().color = Color.clear;
            var mask = viewportGO.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            _scroll.viewport = viewportGO.GetComponent<RectTransform>();

            // Content (cresce para cima conforme mensagens chegam)
            var contentGO = new GameObject("Content");
            contentGO.transform.SetParent(viewportGO.transform, false);
            _content = contentGO.AddComponent<RectTransform>();
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 0f);
            _content.pivot     = new Vector2(0.5f, 0f);
            _content.sizeDelta = Vector2.zero;
            _scroll.content = _content;

            // ── Input de chat (escondido por padrão) ─────────────────────────────
            var inputGO = CreateRect(canvasGO, "ChatInput",
                new Vector2(0.005f, 0.01f), new Vector2(0.32f, 0.05f));

            var inputBg = inputGO.AddComponent<Image>();
            inputBg.color = new Color(0f, 0f, 0f, 0.75f);

            _input = inputGO.AddComponent<TMP_InputField>();
            _input.characterLimit      = 200;
            _input.lineType            = TMP_InputField.LineType.SingleLine;
            _input.contentType         = TMP_InputField.ContentType.Standard;
            _input.onSubmit.AddListener(_ => { /* Enter é capturado em Update() */ });

            // Text area com máscara para não vazar texto
            var textAreaGO = CreateRect(inputGO, "TextArea",
                new Vector2(0.01f, 0.08f), new Vector2(0.99f, 0.92f));
            textAreaGO.AddComponent<RectMask2D>();

            // Placeholder
            var placeholderGO = CreateRect(textAreaGO, "Placeholder", Vector2.zero, Vector2.one);
            _placeholder = placeholderGO.AddComponent<TextMeshProUGUI>();
            _placeholder.text      = "[Z] Mensagem... (Tab=canal, Esc=cancelar)";
            _placeholder.fontSize  = 13f;
            _placeholder.color     = new Color(0.6f, 0.6f, 0.6f, 0.9f);
            _placeholder.fontStyle = FontStyles.Italic;
            _placeholder.enableWordWrapping = false;
            _input.placeholder = _placeholder;

            // Texto digitado
            var textGO = CreateRect(textAreaGO, "Text", Vector2.zero, Vector2.one);
            var textTmp = textGO.AddComponent<TextMeshProUGUI>();
            textTmp.fontSize = 13f;
            textTmp.color    = Color.white;
            textTmp.enableWordWrapping = false;
            _input.textComponent = textTmp;
            _input.textViewport  = textAreaGO.GetComponent<RectTransform>();

            // Cursor piscante
            _input.caretWidth     = 2;
            _input.caretBlinkRate = 0.85f;

            inputGO.SetActive(false); // começa escondido
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private static string EscapeRichText(string s)
        {
            // Evita que nomes/mensagens quebrem o rich text do TMP
            return s?.Replace("<", "‹").Replace(">", "›") ?? "";
        }

        private static GameObject CreateRect(GameObject parent, string name,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go;
        }

        // ─── Estruturas de dados ──────────────────────────────────────────────────
        [System.Serializable]
        private class ChatMessageData
        {
            public string channel;
            public string from;
            public string message;
            public long   ts;
        }
    }
}
