// StatusPanel.cs  (tecla P)
// Mostra o estado completo do personagem: recursos, combate, gathering/crafting
// e maestria ativa das peças equipadas.
//
// Fonte de dados: WorldState.Instance.Local (atualizado via player:joined e eventos).
// Repinta sob demanda quando OnLocalStateUpdated dispara (ver UIPanelBase).

using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.World;

namespace MMORPG.UI
{
    public class StatusPanel : UIPanelBase
    {
        protected override string  Title     => "Status";
        protected override Vector2 PanelSize => new Vector2(540f, 680f);

        // Referências de texto/barras atualizadas em Refresh().
        private TextMeshProUGUI _header;
        private Image  _hpFill,   _manaFill, _stamFill, _xpFill;
        private TextMeshProUGUI _hpTxt, _manaTxt, _stamTxt, _xpTxt, _goldTxt;
        private TextMeshProUGUI _combatTxt;
        private TextMeshProUGUI _gatherTxt, _craftTxt;
        private TextMeshProUGUI _masteryTxt;
        private Button _convertFameBtn;

        protected override void BuildContent(RectTransform body)
        {
            // Layout vertical simples de cima para baixo.
            var layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6f;
            layout.childForceExpandWidth  = true;
            layout.childForceExpandHeight = false;
            layout.childControlHeight     = true;
            layout.childControlWidth      = true;

            // Cabeçalho: nome + level
            _header = AddRow(body, "", 20, TextMain, FontStyles.Bold);

            AddDivider(body);

            // Barras de recursos
            (_hpFill,   _hpTxt)   = AddBarRow(body, "HP",    new Color(0.80f, 0.22f, 0.20f));
            (_manaFill, _manaTxt) = AddBarRow(body, "Mana",  new Color(0.25f, 0.45f, 0.85f));
            (_stamFill, _stamTxt) = AddBarRow(body, "Stam",  new Color(0.30f, 0.70f, 0.35f));
            (_xpFill,   _xpTxt)   = AddBarRow(body, "XP",    new Color(0.75f, 0.65f, 0.20f));
            _goldTxt = AddRow(body, "Gold: 0", 15, Gold, FontStyles.Bold);

            AddDivider(body);

            AddRow(body, "COMBATE", 14, Accent, FontStyles.Bold);
            _combatTxt = AddRow(body, "", 14, TextMain, FontStyles.Normal);

            AddDivider(body);

            AddRow(body, "GATHERING / CRAFTING", 14, Accent, FontStyles.Bold);
            // Duas colunas lado a lado
            var cols = NewRect("Cols", body);
            var colsLayout = cols.AddComponent<HorizontalLayoutGroup>();
            colsLayout.spacing = 10f;
            colsLayout.childForceExpandWidth = true;
            colsLayout.childControlWidth = true;
            var le = cols.AddComponent<LayoutElement>();
            le.minHeight = 110f;
            _gatherTxt = AddCol(cols.transform);
            _craftTxt  = AddCol(cols.transform);

            AddDivider(body);

            AddRow(body, "MAESTRIA ATIVA", 14, Accent, FontStyles.Bold);
            _masteryTxt = AddRow(body, "", 13, TextMain, FontStyles.Normal);
            _masteryTxt.textWrappingMode = TMPro.TextWrappingModes.Normal;

            // Botão "Converter Fama Amarela" — habilitado só perto do Instrutor Magnus
            _convertFameBtn = MakeButton(body, "Converter Fama Amarela", OnConvertFameClicked);
            var btnLe = _convertFameBtn.gameObject.AddComponent<LayoutElement>();
            btnLe.minHeight = 34f;
            btnLe.preferredHeight = 34f;
        }

        protected override void Refresh()
        {
            if (World == null) return;
            var s = World.Local;
            if (s == null) return;

            _header.text = $"{(string.IsNullOrEmpty(s.name) ? "---" : s.name)}      Lv {Mathf.Max(1, s.level)}";

            SetBar(_hpFill,   _hpTxt,   s.hp,      s.maxHp);
            SetBar(_manaFill, _manaTxt, s.mana,    s.maxMana);
            SetBar(_stamFill, _stamTxt, s.stamina, s.maxStamina);
            SetBar(_xpFill,   _xpTxt,   s.xp,      s.xpMax);
            _goldTxt.text = $"Gold: {s.gold}";

            _combatTxt.text =
                $"Velocidade:   {s.speed}\n" +
                $"Dodge:        {s.dodgeChance * 100f:0.0}%\n" +
                $"Red. de dano: {s.damageReduction * 100f:0.0}%";

            _gatherTxt.text = BuildSkillColumn("GATHERING", s.gatheringSkills);
            _craftTxt.text  = BuildSkillColumn("CRAFTING",  s.craftingSkills);

            _masteryTxt.text = BuildMastery(s);

            // Converter Fama Amarela: só habilitado perto do Instrutor (TRAINER_RANGE = 120px)
            // e se houver fama pendente em pelo menos uma peça equipada.
            bool hasPendingFame = HasAnyPendingFame(s);
            bool nearTrainer = GameManager.Instance != null
                && GameManager.Instance.DistanceToNpc("trainer") <= 120f;
            _convertFameBtn.interactable = hasPendingFame && nearTrainer;
            _convertFameBtn.GetComponentInChildren<TMPro.TMP_Text>().text = nearTrainer
                ? "Converter Fama Amarela"
                : "Converter Fama (longe do Instrutor)";
        }

        // ─── Helpers de montagem ───────────────────────────────────────────────────
        private TextMeshProUGUI AddRow(Transform parent, string text, float size, Color color, FontStyles style)
        {
            var txt = MakeText(parent, text, size, color, TextAlignmentOptions.Left);
            txt.fontStyle = style;
            txt.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            var le = txt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = size + 6f;
            return txt;
        }

        private TextMeshProUGUI AddCol(Transform parent)
        {
            var txt = MakeText(parent, "", 13, TextMain, TextAlignmentOptions.TopLeft);
            txt.textWrappingMode = TMPro.TextWrappingModes.Normal;
            return txt;
        }

        private (Image fill, TextMeshProUGUI label) AddBarRow(Transform parent, string name, Color color)
        {
            var row = NewRect($"Row_{name}", parent);
            var le  = row.AddComponent<LayoutElement>();
            le.minHeight = 22f;

            // label à esquerda
            var lbl = MakeText(row.transform, name, 13, TextDim, TextAlignmentOptions.Left);
            var lblRect = lbl.rectTransform;
            lblRect.anchorMin = new Vector2(0f, 0f); lblRect.anchorMax = new Vector2(0f, 1f);
            lblRect.pivot = new Vector2(0f, 0.5f);
            lblRect.sizeDelta = new Vector2(60f, 0f);
            lblRect.anchoredPosition = Vector2.zero;

            // barra
            var fill = MakeBar(row.transform, color);
            var barRect = fill.transform.parent.GetComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0f, 0.15f); barRect.anchorMax = new Vector2(1f, 0.85f);
            barRect.offsetMin = new Vector2(64f, 0f);   barRect.offsetMax = new Vector2(-90f, 0f);

            // valor numérico à direita
            var val = MakeText(row.transform, "", 12, TextMain, TextAlignmentOptions.Right);
            var valRect = val.rectTransform;
            valRect.anchorMin = new Vector2(1f, 0f); valRect.anchorMax = new Vector2(1f, 1f);
            valRect.pivot = new Vector2(1f, 0.5f);
            valRect.sizeDelta = new Vector2(86f, 0f);
            valRect.anchoredPosition = Vector2.zero;

            return (fill, val);
        }

        private void AddDivider(Transform parent)
        {
            var go = NewRect("Divider", parent);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.35f, 0.6f);
            var le = go.AddComponent<LayoutElement>();
            le.minHeight = 2f;
        }

        private void SetBar(Image fill, TextMeshProUGUI label, int cur, int max)
        {
            if (fill != null)  fill.fillAmount = max > 0 ? Mathf.Clamp01((float)cur / max) : 0f;
            if (label != null) label.text = $"{cur}/{max}";
        }

        // ─── Builders de texto ─────────────────────────────────────────────────────
        private string BuildSkillColumn(string header, GatheringSkills g)
        {
            if (g == null) return header + "\n-";
            var sb = new StringBuilder();
            AppendSkill(sb, "Mineração", g.mining);
            AppendSkill(sb, "Lenhador",  g.woodcutting);
            AppendSkill(sb, "Herborista",g.herbalism);
            AppendSkill(sb, "Caça",      g.hunting);
            AppendSkill(sb, "Pesca",     g.fishing);
            return sb.ToString().TrimEnd();
        }

        private string BuildSkillColumn(string header, CraftingSkills c)
        {
            if (c == null) return header + "\n-";
            var sb = new StringBuilder();
            AppendSkill(sb, "Ferraria",   c.smithing);
            AppendSkill(sb, "Couraria",   c.leatherwork);
            AppendSkill(sb, "Alquimia",   c.alchemy);
            AppendSkill(sb, "Arquearia",  c.fletching);
            AppendSkill(sb, "Runas",      c.runecrafting);
            return sb.ToString().TrimEnd();
        }

        private void AppendSkill(StringBuilder sb, string name, SkillProgress p)
        {
            int lv = p != null ? Mathf.Max(1, p.level) : 1;
            sb.AppendLine($"{name}: Lv {lv}");
        }

        private string BuildMastery(LocalFullState s)
        {
            if (s.equipment == null) return "-";
            var sb = new StringBuilder();
            AppendMastery(sb, s, s.equipment.weapon);
            AppendMastery(sb, s, s.equipment.chest);
            AppendMastery(sb, s, s.equipment.head);
            AppendMastery(sb, s, s.equipment.boots);
            string r = sb.ToString().TrimEnd();
            return string.IsNullOrEmpty(r) ? "(nenhuma peça equipada)" : r;
        }

        private void AppendMastery(StringBuilder sb, LocalFullState s, string gearId)
        {
            if (string.IsNullOrEmpty(gearId)) return;
            var m = s.GetMastery(gearId);
            int lv = m != null ? m.level : 1;
            float ratio = (m != null && m.xpMax > 0) ? (float)m.xp / m.xpMax : 0f;
            string bar = ProgressBar(ratio, 9);
            string yf = (m != null && m.yfPending > 0) ? $"  (FA: {m.yfPending})" : "";
            sb.AppendLine($"{GearName(gearId)}: Lv {lv} {bar}{yf}");
        }

        private static string ProgressBar(float ratio, int width)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(ratio) * width);
            return new string('#', filled) + new string('-', width - filled);
        }

        private static string GearName(string gearId) => GearNames.Display(gearId);

        /// <summary>Verifica se alguma peça equipada tem Fama Amarela pendente.</summary>
        private static bool HasAnyPendingFame(LocalFullState s)
        {
            if (s?.equipment == null || s.masteryEntries == null) return false;
            string[] gears = { s.equipment.weapon, s.equipment.chest, s.equipment.head, s.equipment.boots };
            foreach (var gearId in gears)
            {
                if (string.IsNullOrEmpty(gearId)) continue;
                var m = s.GetMastery(gearId);
                if (m != null && m.yfPending > 0) return true;
            }
            return false;
        }

        /// <summary>
        /// Emite mastery:convert para converter Fama Amarela em XP de maestria.
        /// O servidor valida a proximidade — mesmo que o botão esteja habilitado,
        /// o servidor pode rejeitar se o jogador se afastou. Resultado: mastery:convert_result.
        /// </summary>
        private void OnConvertFameClicked()
        {
            Emit("mastery:convert", "{}");
        }
    }
}
