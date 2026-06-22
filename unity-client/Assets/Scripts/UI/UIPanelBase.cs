// UIPanelBase.cs
// Classe base para os 4 painéis de UI procedurais (Status/PaperDoll/SkillTree/Inventory).
//
// Por que uma base compartilhada?
//   Os 4 painéis repetem a mesma estrutura: uma Canvas própria, um painel central
//   com fundo escuro, título, botão [X] e o hábito de redesenhar quando o estado
//   local muda. Centralizar esse boilerplate aqui mantém cada painel focado só no
//   seu layout específico e evita divergência de estilo entre eles.
//
// Padrão de uso (subclasses):
//   - Implementam BuildContent(RectTransform body) para montar seus widgets.
//   - Implementam Refresh() para repintar a partir de WorldState.Instance.Local.
//   - A base cuida de Canvas, fundo, título, [X], show/hide e do hook de eventos.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG.UI
{
    public abstract class UIPanelBase : MonoBehaviour
    {
        // ─── Paleta compartilhada ─────────────────────────────────────────────────
        protected static readonly Color PanelBg   = new Color(0.08f, 0.08f, 0.10f, 0.95f);
        protected static readonly Color SlotBg    = new Color(0.16f, 0.16f, 0.20f, 1f);
        protected static readonly Color SlotEmpty = new Color(0.12f, 0.12f, 0.14f, 1f);
        protected static readonly Color Gold      = new Color(0.85f, 0.72f, 0.25f, 1f);
        protected static readonly Color BarBack   = new Color(0.20f, 0.20f, 0.24f, 1f);
        protected static readonly Color Accent    = new Color(0.30f, 0.55f, 0.85f, 1f);
        protected static readonly Color TextMain  = Color.white;
        protected static readonly Color TextDim   = new Color(0.65f, 0.65f, 0.70f, 1f);

        // ─── Estado ───────────────────────────────────────────────────────────────
        protected NetworkManager Net   { get; private set; }
        protected WorldState     World { get; private set; }
        private   GameObject     _root;       // Canvas raiz deste painel
        protected RectTransform  Body { get; private set; } // área de conteúdo
        private   bool           _built;

        /// <summary>Título exibido no topo do painel.</summary>
        protected abstract string Title { get; }
        /// <summary>Tamanho do painel (largura, altura).</summary>
        protected virtual Vector2 PanelSize => new Vector2(520f, 600f);

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        protected virtual void Start()
        {
            Net   = NetworkManager.Instance;
            World = WorldState.Instance;

            BuildShell();
            BuildContent(Body);
            _built = true;

            if (World != null)
                World.OnLocalStateUpdated += SafeRefresh;

            Refresh();
            SetVisible(false);
        }

        protected virtual void OnDestroy()
        {
            if (World != null)
                World.OnLocalStateUpdated -= SafeRefresh;
        }

        /// <summary>
        /// Wrapper seguro: só chama Refresh() se BuildContent() já terminou.
        /// Evita NullReferenceException quando WorldState dispara eventos durante
        /// a inicialização dos painéis.
        /// </summary>
        private void SafeRefresh() { if (_built) Refresh(); }

        // ─── API ──────────────────────────────────────────────────────────────────

        /// <summary>Mostra/esconde o painel. Repinta ao mostrar.</summary>
        public void SetVisible(bool visible)
        {
            if (_root == null) return;
            _root.SetActive(visible);
            if (visible && _built) Refresh();
        }

        public bool IsVisible => _root != null && _root.activeSelf;

        // ─── Contrato das subclasses ───────────────────────────────────────────────
        protected abstract void BuildContent(RectTransform body);
        protected abstract void Refresh();

        // ─── Construção do "shell" (canvas + fundo + título + [X]) ──────────────────
        private void BuildShell()
        {
            _root = new GameObject($"{Title}Canvas");
            _root.transform.SetParent(transform, false);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // acima do HUD/SkillBar (10)

            var scaler = _root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            _root.AddComponent<GraphicRaycaster>();

            // Painel central
            var panel     = NewRect("Panel", _root.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot     = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = PanelSize;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = PanelBg;

            // Barra de título
            var titleBar     = NewRect("TitleBar", panel.transform);
            var titleRect    = titleBar.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot     = new Vector2(0.5f, 1f);
            titleRect.sizeDelta = new Vector2(0f, 40f);
            titleRect.anchoredPosition = Vector2.zero;
            var titleImg = titleBar.AddComponent<Image>();
            titleImg.color = new Color(0.15f, 0.15f, 0.20f, 1f);

            var titleTxt = MakeText(titleBar.transform, Title, 20, TextMain, TextAlignmentOptions.Left);
            var ttRect   = titleTxt.rectTransform;
            ttRect.anchorMin = Vector2.zero; ttRect.anchorMax = Vector2.one;
            ttRect.offsetMin = new Vector2(14f, 0f); ttRect.offsetMax = new Vector2(-44f, 0f);
            titleTxt.fontStyle = FontStyles.Bold;

            // Botão fechar [X]
            var closeBtn = MakeButton(titleBar.transform, "X", () => UIManager.Instance?.CloseAll());
            var cbRect   = closeBtn.GetComponent<RectTransform>();
            cbRect.anchorMin = new Vector2(1f, 0.5f);
            cbRect.anchorMax = new Vector2(1f, 0.5f);
            cbRect.pivot     = new Vector2(1f, 0.5f);
            cbRect.anchoredPosition = new Vector2(-6f, 0f);
            cbRect.sizeDelta = new Vector2(30f, 30f);

            // Área de conteúdo (abaixo do título)
            var body     = NewRect("Body", panel.transform);
            var bodyRect  = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 1f);
            bodyRect.offsetMin = new Vector2(14f, 14f);
            bodyRect.offsetMax = new Vector2(-14f, -46f);
            Body = bodyRect;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // UTILITÁRIOS DE UI PROCEDURAL (reutilizados pelas subclasses)
        // ═══════════════════════════════════════════════════════════════════════════

        protected static GameObject NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Cria um TMP_Text já parentado e configurado.</summary>
        protected static TextMeshProUGUI MakeText(Transform parent, string text, float size,
                                                  Color color, TextAlignmentOptions align)
        {
            var go  = NewRect("Text", parent);
            var txt = go.AddComponent<TextMeshProUGUI>();
            txt.text      = text;
            txt.fontSize  = size;
            txt.color     = color;
            txt.alignment = align;
            txt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            txt.raycastTarget = false;
            return txt;
        }

        /// <summary>Cria um botão simples com label e callback.</summary>
        protected static Button MakeButton(Transform parent, string label, Action onClick)
        {
            var go  = NewRect($"Btn_{label}", parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.25f, 0.28f, 0.34f, 1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = new Color(0.34f, 0.40f, 0.50f, 1f);
            colors.pressedColor     = new Color(0.18f, 0.20f, 0.26f, 1f);
            colors.disabledColor    = new Color(0.16f, 0.16f, 0.18f, 0.6f);
            btn.colors = colors;
            btn.targetGraphic = img;

            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = MakeText(go.transform, label, 14, TextMain, TextAlignmentOptions.Center);
            var r = txt.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

            return btn;
        }

        /// <summary>
        /// Cria uma barra de progresso simples (fundo + preenchimento ancorado à esquerda).
        /// Retorna a Image de preenchimento para ajustar o fillAmount/cor depois.
        /// </summary>
        protected static Image MakeBar(Transform parent, Color fillColor)
        {
            var back     = NewRect("Bar", parent);
            var backImg  = back.AddComponent<Image>();
            backImg.color = BarBack;
            backImg.raycastTarget = false;

            var fillGo   = NewRect("Fill", back.transform);
            var fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = fillColor;
            fillImg.type  = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 1f;
            fillImg.raycastTarget = false;
            return fillImg;
        }

        /// <summary>Emite um evento para o servidor com payload JSON (atalho seguro).</summary>
        protected void Emit(string ev, string json)
        {
            Net?.Emit(ev, json);
        }
    }
}
