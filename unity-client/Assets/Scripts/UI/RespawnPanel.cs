// RespawnPanel.cs
// Painel de morte: aparece quando o jogador local morre.
//
// Comportamento:
//   - Aparece ao receber player:died com o ID do jogador local
//   - Mostra "VOCÊ MORREU" e um countdown de RESPAWN_TIME segundos
//   - Botão "Ressuscitar" emite skill:use com skillId "revive" (healer class)
//   - Desaparece ao receber player:revived (qualquer fonte) ou ao fim do countdown
//
// Uso (GameManager):
//   RespawnPanel.Instance.Show();   // HandlePlayerDied (local)
//   RespawnPanel.Instance.Hide();   // HandlePlayerRevived
//
// Não requer prefab — UI construída proceduralmente em Awake().

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MMORPG.UI
{
    public class RespawnPanel : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────
        public static RespawnPanel Instance { get; private set; }

        // ─── Config ───────────────────────────────────────────────────────────────
        private const float RESPAWN_TIME = 8f;    // segundos de countdown automático
        private const float PANEL_ALPHA  = 0.88f; // opacidade do fundo

        // ─── UI refs ──────────────────────────────────────────────────────────────
        private GameObject      _root;
        private TextMeshProUGUI _countdownText;
        private Button          _respawnBtn;

        // ─── Estado ───────────────────────────────────────────────────────────────
        private bool  _active;
        private float _elapsed;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            BuildUI();
            SetVisible(false);
        }

        private void Update()
        {
            if (!_active) return;

            _elapsed += Time.deltaTime;
            float remaining = Mathf.Max(0f, RESPAWN_TIME - _elapsed);

            if (_countdownText != null)
                _countdownText.text = remaining > 0.5f
                    ? $"Ressuscitando em {Mathf.CeilToInt(remaining)}s..."
                    : "Ressuscitando...";

            // Servidor envia player:revived; se não chegar, fechamos mesmo assim
            if (_elapsed >= RESPAWN_TIME + 1f)
                Hide();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Exibe o painel de morte e inicia o countdown.</summary>
        public void Show()
        {
            if (_active) return;
            _active  = true;
            _elapsed = 0f;
            SetVisible(true);
        }

        /// <summary>Esconde o painel (chamado quando player:revived chega).</summary>
        public void Hide()
        {
            _active = false;
            SetVisible(false);
        }

        // ─── Construção procedural da UI ──────────────────────────────────────────
        private void BuildUI()
        {
            // Canvas Screen-Space Overlay — acima de tudo
            var canvasGO = new GameObject("RespawnCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;
            canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
                UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGO.AddComponent<GraphicRaycaster>();

            // Fundo escuro semitransparente centralizado
            _root = CreateRect(canvasGO, "BG",
                new Vector2(0.3f, 0.28f), new Vector2(0.7f, 0.72f));
            var bg = _root.AddComponent<Image>();
            bg.color = new Color(0.04f, 0f, 0f, PANEL_ALPHA);

            // "VOCÊ MORREU"
            var titleGO = CreateRect(_root, "Title",
                new Vector2(0.05f, 0.68f), new Vector2(0.95f, 0.92f));
            var title = titleGO.AddComponent<TextMeshProUGUI>();
            title.text      = "VOCÊ MORREU";
            title.fontSize  = 38;
            title.color     = new Color(0.9f, 0.08f, 0.08f);
            title.alignment = TextAlignmentOptions.Center;
            title.fontStyle = FontStyles.Bold;

            // Separador (linha horizontal)
            var sepGO = CreateRect(_root, "Separator",
                new Vector2(0.05f, 0.645f), new Vector2(0.95f, 0.655f));
            var sep = sepGO.AddComponent<Image>();
            sep.color = new Color(0.6f, 0.1f, 0.1f, 0.8f);

            // Countdown
            var cdGO = CreateRect(_root, "Countdown",
                new Vector2(0.05f, 0.42f), new Vector2(0.95f, 0.63f));
            _countdownText = cdGO.AddComponent<TextMeshProUGUI>();
            _countdownText.text      = $"Ressuscitando em {(int)RESPAWN_TIME}s...";
            _countdownText.fontSize  = 18;
            _countdownText.color     = new Color(0.85f, 0.85f, 0.85f);
            _countdownText.alignment = TextAlignmentOptions.Center;

            // Dica de revive (healer)
            var hintGO = CreateRect(_root, "Hint",
                new Vector2(0.05f, 0.28f), new Vector2(0.95f, 0.41f));
            var hint = hintGO.AddComponent<TextMeshProUGUI>();
            hint.text      = "Um healer pode te ressuscitar antes disso.";
            hint.fontSize  = 13;
            hint.color     = new Color(0.6f, 0.6f, 0.6f);
            hint.alignment = TextAlignmentOptions.Center;
            hint.fontStyle = FontStyles.Italic;

            // Botão "Ressuscitar" (auto-revive ou healer self-cast)
            var btnGO = CreateRect(_root, "RespawnButton",
                new Vector2(0.2f, 0.07f), new Vector2(0.8f, 0.25f));
            var btnImg = btnGO.AddComponent<Image>();
            btnImg.color = new Color(0.28f, 0.06f, 0.06f);
            _respawnBtn = btnGO.AddComponent<Button>();

            var colors = _respawnBtn.colors;
            colors.normalColor      = new Color(0.28f, 0.06f, 0.06f);
            colors.highlightedColor = new Color(0.48f, 0.12f, 0.12f);
            colors.pressedColor     = new Color(0.65f, 0.18f, 0.18f);
            _respawnBtn.colors = colors;
            _respawnBtn.onClick.AddListener(OnRespawnClicked);

            var btnLblGO = CreateRect(btnGO, "Label", Vector2.zero, Vector2.one);
            var btnLbl = btnLblGO.AddComponent<TextMeshProUGUI>();
            btnLbl.text      = "Ressuscitar";
            btnLbl.fontSize  = 16;
            btnLbl.color     = Color.white;
            btnLbl.alignment = TextAlignmentOptions.Center;
            btnLbl.fontStyle = FontStyles.Bold;
        }

        private void OnRespawnClicked()
        {
            // Healer pode usar revive em si mesmo; classes sem revive não terão cooldown
            // O servidor vai emitir skill:result → se rejeitado, aguardamos o countdown
            var net = Network.NetworkManager.Instance;
            if (net != null)
            {
                net.Emit("skill:use", "{\"skillId\":\"revive\",\"tx\":0,\"ty\":0}");
                Debug.Log("[RespawnPanel] Tentando auto-revive via skill:use...");
            }
        }

        private void SetVisible(bool visible)
        {
            if (_root != null)
                _root.SetActive(visible);
        }

        // Helper: cria RectTransform com anchors relativas ao parent
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
    }
}
