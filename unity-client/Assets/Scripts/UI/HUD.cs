// HUD.cs
// Interface principal do jogador — criada 100% proceduralmente em runtime.
// Não requer nenhuma configuração no Inspector nem hierarchy pré-montada.
//
// Layout (inspirado no Albion Online):
//
//   ┌──────────────────────────────────────┐
//   │ PlayerName                   [Ping]  │  ← TopBar (semi-transparente)
//   └──────────────────────────────────────┘
//
//   ┌────┐ ████████░░ HP  420/500
//   │ ⚔  │ ██████░░░░ MP  180/250
//   │ Lv │ ⬡ 1250
//   └────┘
//
//   ████████████████████░░░░░░░░░░░░░░   ← XPBar borda inferior
//
// Barras usam Image.fillAmount (mais performático que Slider para UIs high-freq).

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG.UI
{
    public class HUD : MonoBehaviour
    {
        // ─── Referências de UI criadas proceduralmente ────────────────────────────
        private Image    _hpFill;
        private Image    _manaFill;
        private Image    _xpBottomFill;
        private TMP_Text _hpText;
        private TMP_Text _manaText;
        private TMP_Text _levelText;
        private TMP_Text _playerNameTopText;
        private TMP_Text _pingText;
        private TMP_Text _goldText;
        private Image    _portraitBorder;

        // ─── Estado ───────────────────────────────────────────────────────────────
        private NetworkManager _net;
        private WorldState     _world;
        private float          _pingTimer;
        private const float    PING_INTERVAL = 1f;

        // ─── Cores do tema Albion ─────────────────────────────────────────────────
        private static readonly Color C_HP_HIGH  = new Color(0.82f, 0.18f, 0.12f);
        private static readonly Color C_HP_MED   = new Color(0.90f, 0.60f, 0.05f);
        private static readonly Color C_HP_LOW   = new Color(1.00f, 0.20f, 0.10f);
        private static readonly Color C_MANA     = new Color(0.15f, 0.45f, 0.92f);
        private static readonly Color C_XP       = new Color(0.72f, 0.58f, 0.08f);
        private static readonly Color C_PANEL_BG = new Color(0.06f, 0.06f, 0.06f, 0.80f);
        private static readonly Color C_BAR_BG   = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        private static readonly Color C_GOLD     = new Color(1.00f, 0.85f, 0.20f);
        private static readonly Color C_TEXT     = new Color(0.92f, 0.90f, 0.85f);
        private static readonly Color C_BORDER   = new Color(0.50f, 0.42f, 0.12f, 0.90f);

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _net   = NetworkManager.Instance;
            _world = WorldState.Instance;

            BuildUI();

            if (_world != null)
                _world.OnWorldUpdated += OnWorldUpdated;

            SetHP(0, 100);
            SetMana(0, 100);
            SetXP(0, 100);
            SetGold(0);
            SetLevel(1);
            SetPing(0);
        }

        private void OnDestroy()
        {
            if (_world != null)
                _world.OnWorldUpdated -= OnWorldUpdated;
        }

        private void Update()
        {
            _pingTimer += Time.deltaTime;
            if (_pingTimer >= PING_INTERVAL)
            {
                _pingTimer = 0f;
                if (_net != null) SetPing(Mathf.RoundToInt(_net.LatencyMs));
            }
        }

        // ─── Evento de mundo ──────────────────────────────────────────────────────
        private void OnWorldUpdated(System.Collections.Generic.HashSet<string> _)
        {
            if (_world == null || !_world.TryGetLocalPlayer(out var p)) return;
            SetHP(p.hp, p.maxHp);
            SetMana(p.mana, p.maxMana);
            SetXP(p.xp, p.xpMax);
            SetGold(p.gold);
            SetLevel(p.level > 0 ? p.level : 1);
        }

        // ─── Setters Públicos ─────────────────────────────────────────────────────

        public void SetHP(int hp, int maxHp)
        {
            float r = maxHp > 0 ? (float)hp / maxHp : 0f;
            if (_hpFill != null)
            {
                _hpFill.fillAmount = r;
                _hpFill.color = r > 0.6f ? C_HP_HIGH : r > 0.3f ? C_HP_MED : C_HP_LOW;
            }
            if (_hpText != null) _hpText.text = $"{hp} / {maxHp}";
        }

        public void SetMana(int mana, int maxMana)
        {
            float r = maxMana > 0 ? (float)mana / maxMana : 0f;
            if (_manaFill != null) _manaFill.fillAmount = r;
            if (_manaText != null) _manaText.text = $"{mana} / {maxMana}";
        }

        public void SetXP(int xp, int xpMax)
        {
            if (_xpBottomFill != null)
                _xpBottomFill.fillAmount = xpMax > 0 ? (float)xp / xpMax : 0f;
        }

        public void SetGold(int gold)
        {
            if (_goldText != null) _goldText.text = $"G {gold:N0}";
        }

        public void SetLevel(int level)
        {
            if (_levelText != null) _levelText.text = level.ToString();
        }

        public void SetPlayerName(string name)
        {
            if (_playerNameTopText != null) _playerNameTopText.text = name;
        }

        public void SetPing(int ms)
        {
            if (_pingText == null) return;
            _pingText.text  = $"{ms}ms";
            _pingText.color = ms < 80 ? Color.green : ms < 200 ? Color.yellow : Color.red;
        }

        public void SetClassColor(Color c)
        {
            if (_portraitBorder != null) _portraitBorder.color = c;
        }

        // ─── Criação procedural da UI ─────────────────────────────────────────────

        private void BuildUI()
        {
            var canvasGO = new GameObject("HUDCanvas");
            canvasGO.transform.SetParent(transform, false);
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            BuildTopBar(canvasGO.transform);
            BuildStatusPanel(canvasGO.transform);
            BuildXPBottomBar(canvasGO.transform);
        }

        private void BuildTopBar(Transform canvas)
        {
            var bar = R("TopBar", canvas);
            var rt  = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta        = new Vector2(0f, 32f);
            bar.AddComponent<Image>().color = C_PANEL_BG;

            // Nome do jogador
            var nameGO = R("PlayerName", bar.transform);
            CenterFull(nameGO);
            _playerNameTopText = nameGO.AddComponent<TextMeshProUGUI>();
            _playerNameTopText.text      = "---";
            _playerNameTopText.fontSize  = 15f;
            _playerNameTopText.color     = C_TEXT;
            _playerNameTopText.alignment = TextAlignmentOptions.Center;
            _playerNameTopText.fontStyle = FontStyles.Bold;

            // Ping
            var pingGO = R("Ping", bar.transform);
            var pingRT = pingGO.GetComponent<RectTransform>();
            pingRT.anchorMin = new Vector2(1f, 0f); pingRT.anchorMax = new Vector2(1f, 1f);
            pingRT.pivot     = new Vector2(1f, 0.5f);
            pingRT.anchoredPosition = new Vector2(-10f, 0f);
            pingRT.sizeDelta        = new Vector2(75f, 0f);
            _pingText = pingGO.AddComponent<TextMeshProUGUI>();
            _pingText.text      = "0ms";
            _pingText.fontSize  = 12f;
            _pingText.color     = Color.green;
            _pingText.alignment = TextAlignmentOptions.Right;
        }

        private void BuildStatusPanel(Transform canvas)
        {
            var panel = R("StatusPanel", canvas);
            var pRT   = panel.GetComponent<RectTransform>();
            pRT.anchorMin = Vector2.zero; pRT.anchorMax = Vector2.zero;
            pRT.pivot     = Vector2.zero;
            pRT.anchoredPosition = new Vector2(10f, 46f);
            pRT.sizeDelta        = new Vector2(300f, 108f);
            panel.AddComponent<Image>().color = C_PANEL_BG;

            // Portrait
            var portrait = R("Portrait", panel.transform);
            var portRT   = portrait.GetComponent<RectTransform>();
            portRT.anchorMin = new Vector2(0f, 0f); portRT.anchorMax = new Vector2(0f, 1f);
            portRT.pivot     = new Vector2(0f, 0.5f);
            portRT.anchoredPosition = new Vector2(6f, 0f);
            portRT.sizeDelta        = new Vector2(86f, 0f);
            portrait.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var border = R("Border", portrait.transform);
            _portraitBorder = border.AddComponent<Image>();
            _portraitBorder.color = C_BORDER;
            StretchFull(border, -3f, -3f);

            // Símbolo da classe
            var sym = R("ClassSym", portrait.transform);
            CenterFull(sym);
            var symTxt = sym.AddComponent<TextMeshProUGUI>();
            symTxt.text = "⚔"; symTxt.fontSize = 28f;
            symTxt.color = new Color(0.70f, 0.65f, 0.50f);
            symTxt.alignment = TextAlignmentOptions.Center;

            // Level badge
            var lvGO = R("Level", portrait.transform);
            var lvRT = lvGO.GetComponent<RectTransform>();
            lvRT.anchorMin = new Vector2(0f, 0f); lvRT.anchorMax = new Vector2(1f, 0f);
            lvRT.pivot     = new Vector2(0.5f, 0f);
            lvRT.anchoredPosition = new Vector2(0f, 4f);
            lvRT.sizeDelta        = new Vector2(0f, 20f);
            _levelText = lvGO.AddComponent<TextMeshProUGUI>();
            _levelText.text = "1"; _levelText.fontSize = 16f;
            _levelText.color = C_GOLD; _levelText.fontStyle = FontStyles.Bold;
            _levelText.alignment = TextAlignmentOptions.Center;

            // Barras HP + Mana
            var barsGO = R("Bars", panel.transform);
            var barsRT = barsGO.GetComponent<RectTransform>();
            barsRT.anchorMin = new Vector2(0f, 0f); barsRT.anchorMax = new Vector2(1f, 1f);
            barsRT.offsetMin = new Vector2(100f, 6f);
            barsRT.offsetMax = new Vector2(-6f, -6f);

            (_hpFill, _hpText)     = BuildBar(barsGO.transform, "HP",   0f, 30f, C_HP_HIGH, "HP");
            (_manaFill, _manaText) = BuildBar(barsGO.transform, "Mana", 40f, 70f, C_MANA,   "MP");

            // Gold
            var goldGO = R("Gold", barsGO.transform);
            var goldRT = goldGO.GetComponent<RectTransform>();
            goldRT.anchorMin = Vector2.zero; goldRT.anchorMax = new Vector2(1f, 0f);
            goldRT.pivot     = new Vector2(0f, 0f);
            goldRT.anchoredPosition = new Vector2(0f, 6f);
            goldRT.sizeDelta        = new Vector2(0f, 20f);
            _goldText = goldGO.AddComponent<TextMeshProUGUI>();
            _goldText.text = "G 0"; _goldText.fontSize = 13f;
            _goldText.color = C_GOLD;
        }

        private void BuildXPBottomBar(Transform canvas)
        {
            var bgGO = R("XPBarBG", canvas);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0f); bgRT.anchorMax = new Vector2(1f, 0f);
            bgRT.pivot     = new Vector2(0.5f, 0f);
            bgRT.anchoredPosition = Vector2.zero;
            bgRT.sizeDelta        = new Vector2(0f, 8f);
            bgGO.AddComponent<Image>().color = C_BAR_BG;

            var fillGO = R("XPFill", bgGO.transform);
            StretchFull(fillGO, 0f, 0f);
            var fillImg = fillGO.AddComponent<Image>();
            fillImg.color = C_XP;
            fillImg.type       = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;
            _xpBottomFill = fillImg;
        }

        /// <summary>Cria uma barra horizontal (HP ou Mana) dentro de um container.</summary>
        /// <param name="fromBottom">Y do topo da barra (a partir de baixo do container).</param>
        /// <param name="toBottom">Y da base da barra.</param>
        private (Image fill, TMP_Text value) BuildBar(Transform parent, string id,
                                                       float fromBottom, float toBottom,
                                                       Color color, string label)
        {
            // Container da barra
            var bgGO = R($"{id}BG", parent);
            var bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0f, 0f); bgRT.anchorMax = new Vector2(1f, 0f);
            bgRT.pivot     = new Vector2(0f, 0f);
            bgRT.anchoredPosition = new Vector2(0f, fromBottom);
            bgRT.sizeDelta        = new Vector2(0f, toBottom - fromBottom);
            bgGO.AddComponent<Image>().color = C_BAR_BG;

            // Fill
            var fillGO = R($"{id}Fill", bgGO.transform);
            StretchFull(fillGO, 0f, 0f);
            var fill = fillGO.AddComponent<Image>();
            fill.color      = color;
            fill.type       = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;

            // Glossy highlight (topo)
            var hlGO = R($"{id}HL", bgGO.transform);
            var hlRT = hlGO.GetComponent<RectTransform>();
            hlRT.anchorMin = new Vector2(0f, 0.6f); hlRT.anchorMax = Vector2.one;
            hlRT.offsetMin = hlRT.offsetMax = Vector2.zero;
            hlGO.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

            // Label
            var lblGO = R($"{id}Lbl", bgGO.transform);
            var lblRT = lblGO.GetComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0f); lblRT.anchorMax = new Vector2(0f, 1f);
            lblRT.pivot     = new Vector2(0f, 0.5f);
            lblRT.anchoredPosition = new Vector2(4f, 0f);
            lblRT.sizeDelta        = new Vector2(24f, 0f);
            var lbl = lblGO.AddComponent<TextMeshProUGUI>();
            lbl.text = label; lbl.fontSize = 10f;
            lbl.color = new Color(1f, 1f, 1f, 0.85f);
            lbl.fontStyle = FontStyles.Bold;

            // Valor (HP / MaxHP)
            var valGO = R($"{id}Val", bgGO.transform);
            var valRT = valGO.GetComponent<RectTransform>();
            valRT.anchorMin = new Vector2(0f, 0f); valRT.anchorMax = Vector2.one;
            valRT.offsetMin = new Vector2(28f, 0f);
            valRT.offsetMax = new Vector2(-4f, 0f);
            var val = valGO.AddComponent<TextMeshProUGUI>();
            val.text = "---"; val.fontSize = 10f;
            val.color = new Color(1f, 1f, 1f, 0.90f);
            val.fontStyle = FontStyles.Bold;
            val.alignment = TextAlignmentOptions.Right;

            return (fill, val);
        }

        // ─── UI Helpers ───────────────────────────────────────────────────────────

        private static GameObject R(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void CenterFull(GameObject go)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        private static void StretchFull(GameObject go, float pad, float pad2)
        {
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(pad2, pad2);
        }
    }
}
