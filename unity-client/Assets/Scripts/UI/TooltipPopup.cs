// TooltipPopup.cs
// Popup de tooltip singleton que segue o ponteiro do mouse.
//
// Uso:
//   // Mostrar tooltip:
//   TooltipPopup.Show("texto", Input.mousePosition);
//   // Mostrar usando dados do catálogo de skills:
//   var entry = SkillCatalog.Get(skillId);
//   if (entry != null) TooltipPopup.Show(entry.ToTooltipText(), pos);
//   // Esconder:
//   TooltipPopup.Hide();
//
// Criação procedural: não requer nenhum prefab ou configuração no Editor.
// O singleton é criado automaticamente na primeira chamada a Show().
//
// Design:
//   Canvas (ScreenSpaceOverlay, sortOrder=20)
//   └── TooltipPanel (Image fundo semitransparente, VerticalLayout)
//       ├── TitleText (TMP, negrito, branco)
//       └── BodyText (TMP, menor, cinza claro)
//
// RichText e tags TMP (ex: <b>, <color=#...>) são suportados nos textos.

using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace MMORPG.UI
{
    public class TooltipPopup : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────
        private static TooltipPopup _instance;

        // Garante que existe uma instância, criando-a se necessário.
        private static TooltipPopup Instance
        {
            get
            {
                if (_instance == null) _instance = Create();
                return _instance;
            }
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Exibe o tooltip com o texto fornecido perto da posição de tela dada.
        /// Textos suportam rich text TMP (ex: "Título\n<size=90%>Descrição</size>").
        /// O popup é reposicionado automaticamente para não sair da tela.
        /// </summary>
        public static void Show(string text, Vector2 screenPos)
        {
            Instance._Show(text, screenPos);
        }

        /// <summary>Esconde o tooltip.</summary>
        public static void Hide()
        {
            if (_instance != null) _instance._Hide();
        }

        // ─── Constantes visuais ───────────────────────────────────────────────────
        private const float  PanelWidth   = 280f;
        private const float  PanelPadding = 12f;
        private const float  Offset       = 18f;  // deslocamento do cursor
        private const int    BodyFontSize = 13;
        private const float  BgAlpha      = 0.92f;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private RectTransform _panelRect;
        private TMP_Text      _bodyText;
        private Canvas        _canvas;

        // ─── Implementação ────────────────────────────────────────────────────────

        private void _Show(string text, Vector2 screenPos)
        {
            _bodyText.text = text;
            gameObject.SetActive(true);

            // Força rebuild do layout para calcular altura correta antes de reposicionar
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panelRect);
            Reposition(screenPos);
        }

        private void _Hide()
        {
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // Segue o mouse enquanto visível
            if (gameObject.activeSelf)
                Reposition(Input.mousePosition);
        }

        /// <summary>
        /// Calcula posição do painel para que ele não saia da tela.
        /// Tenta aparecer à direita/acima do cursor; espelha se não couber.
        /// </summary>
        private void Reposition(Vector2 mouseScreenPos)
        {
            if (_panelRect == null) return;

            float sw = Screen.width;
            float sh = Screen.height;

            Vector2 size = _panelRect.sizeDelta;
            float x = mouseScreenPos.x + Offset;
            float y = mouseScreenPos.y + Offset;

            // Espelha horizontalmente se sair da tela
            if (x + size.x > sw)
                x = mouseScreenPos.x - size.x - Offset;

            // Espelha verticalmente se sair da tela
            if (y + size.y > sh)
                y = mouseScreenPos.y - size.y - Offset;

            // Garante que não saia pela esquerda/baixo
            x = Mathf.Max(0f, x);
            y = Mathf.Max(0f, y);

            _panelRect.anchoredPosition = new Vector2(x, y);
        }

        // ─── Criação Procedural ───────────────────────────────────────────────────

        private static TooltipPopup Create()
        {
            // GO raiz persistente
            var go = new GameObject("TooltipPopup");
            DontDestroyOnLoad(go);

            var popup = go.AddComponent<TooltipPopup>();
            popup.BuildUI();
            go.SetActive(false); // começa escondido
            return popup;
        }

        private void BuildUI()
        {
            // ── Canvas ──────────────────────────────────────────────────────────
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // acima de qualquer painel de UI
            _canvas = canvas;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            gameObject.AddComponent<GraphicRaycaster>();

            // ── Painel de fundo ─────────────────────────────────────────────────
            var panelGO = new GameObject("TooltipPanel");
            panelGO.transform.SetParent(transform, false);

            _panelRect = panelGO.AddComponent<RectTransform>();
            // Ancora no canto inferior-esquerdo para que anchoredPosition = posição absoluta
            _panelRect.anchorMin = Vector2.zero;
            _panelRect.anchorMax = Vector2.zero;
            _panelRect.pivot     = Vector2.zero;
            _panelRect.sizeDelta = new Vector2(PanelWidth, 0f); // altura cresce com conteúdo

            // Fundo escuro semitransparente
            var bg = panelGO.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.08f, 0.10f, BgAlpha);

            // Layout vertical com padding
            var layout = panelGO.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(
                (int)PanelPadding, (int)PanelPadding,
                (int)PanelPadding, (int)PanelPadding
            );
            layout.spacing               = 4f;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth      = true;
            layout.childControlHeight     = true;

            // ContentSizeFitter para altura automática
            var csf = panelGO.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

            // ── Texto do corpo ──────────────────────────────────────────────────
            // Usamos um único TMP com rich text para título + corpo.
            // Ex: "<b>Golpe</b>\n\nAtaque básico instantâneo.\nCooldown: 0.6s"
            var bodyGO = new GameObject("BodyText");
            bodyGO.transform.SetParent(panelGO.transform, false);

            _bodyText = bodyGO.AddComponent<TextMeshProUGUI>();
            _bodyText.fontSize          = BodyFontSize;
            _bodyText.color             = new Color(0.90f, 0.90f, 0.88f);
            _bodyText.richText          = true;
            _bodyText.textWrappingMode = TMPro.TextWrappingModes.Normal;
            _bodyText.overflowMode      = TextOverflowModes.Overflow;

            // ContentSizeFitter no texto para crescer verticalmente
            var textCsf = bodyGO.AddComponent<ContentSizeFitter>();
            textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Borda sutil (outline via segundo Image atrás do painel)
            AddBorder(panelGO);
        }

        /// <summary>Adiciona uma borda fina colorida ao redor do painel.</summary>
        private static void AddBorder(GameObject panelGO)
        {
            var borderGO = new GameObject("Border");
            borderGO.transform.SetParent(panelGO.transform, false);
            borderGO.transform.SetAsFirstSibling(); // atrás do conteúdo

            var borderRect = borderGO.AddComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-1f, -1f);
            borderRect.offsetMax = new Vector2(1f, 1f);

            var borderImg = borderGO.AddComponent<Image>();
            borderImg.color = new Color(0.4f, 0.35f, 0.15f, 0.9f); // dourado escuro
        }

        // ─── Destruição ───────────────────────────────────────────────────────────
        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
