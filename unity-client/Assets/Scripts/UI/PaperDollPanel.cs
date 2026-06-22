// PaperDollPanel.cs  (tecla C)
// Mostra os 4 slots de equipamento (weapon/chest/head/boots) num layout de "paper doll".
// Clicar num slot equipado revela detalhes: nome, maestria, durabilidade e os botões
// "Desequipar" (gear:unequip) e "Reparar" (repair:item).
//
// O reparo exige proximidade ao Ferreiro — quem valida isso é o servidor; o cliente
// apenas emite o pedido e reage ao repair:result (tratado no WorldState).

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.World;

namespace MMORPG.UI
{
    public class PaperDollPanel : UIPanelBase
    {
        protected override string  Title     => "Equipamentos";
        protected override Vector2 PanelSize => new Vector2(480f, 560f);

        // Slots clicáveis: index 0=weapon,1=chest,2=head,3=boots
        private static readonly string[] SLOTS = { "weapon", "chest", "head", "boots" };
        private readonly Button[]          _slotBtns  = new Button[4];
        private readonly Image[]           _slotBg    = new Image[4];
        private readonly TextMeshProUGUI[] _slotLbls  = new TextMeshProUGUI[4];

        // Detalhes do slot selecionado
        private string          _selectedSlot;
        private TextMeshProUGUI _detailName, _detailMastery, _detailDur;
        private Image           _detailMasteryFill;
        private Button          _unequipBtn, _repairBtn;
        private GameObject      _detailPanel;

        protected override void BuildContent(RectTransform body)
        {
            // ── Área dos slots (topo) ──────────────────────────────────────────────
            // Layout em formato de boneco: HEAD em cima, WEAPON/CHEST/BOOTS na linha de baixo.
            // head (2) topo-centro
            _slotBtns[2] = MakeSlot(body, 2, new Vector2(0.5f, 0.82f));
            // weapon (0), chest (1), boots (3) na linha do meio
            _slotBtns[0] = MakeSlot(body, 0, new Vector2(0.22f, 0.60f));
            _slotBtns[1] = MakeSlot(body, 1, new Vector2(0.50f, 0.60f));
            _slotBtns[3] = MakeSlot(body, 3, new Vector2(0.78f, 0.60f));

            // ── Painel de detalhes (parte inferior) ────────────────────────────────
            _detailPanel = NewRect("Detail", body);
            var dp = _detailPanel.GetComponent<RectTransform>();
            dp.anchorMin = new Vector2(0f, 0f);
            dp.anchorMax = new Vector2(1f, 0.42f);
            dp.offsetMin = Vector2.zero; dp.offsetMax = Vector2.zero;
            var dpImg = _detailPanel.AddComponent<Image>();
            dpImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            _detailName = MakeText(_detailPanel.transform, "", 18, TextMain, TextAlignmentOptions.TopLeft);
            Place(_detailName.rectTransform, 0f, 1f, 1f, 1f, new Vector2(12f, -10f), new Vector2(-12f, -34f));
            _detailName.fontStyle = FontStyles.Bold;

            _detailMastery = MakeText(_detailPanel.transform, "", 13, TextDim, TextAlignmentOptions.TopLeft);
            Place(_detailMastery.rectTransform, 0f, 1f, 1f, 1f, new Vector2(12f, -40f), new Vector2(-12f, -60f));

            _detailMasteryFill = MakeBar(_detailPanel.transform, new Color(0.75f, 0.65f, 0.20f));
            var mbRect = _detailMasteryFill.transform.parent.GetComponent<RectTransform>();
            Place(mbRect, 0f, 1f, 1f, 1f, new Vector2(12f, -64f), new Vector2(-12f, -76f));

            _detailDur = MakeText(_detailPanel.transform, "", 13, TextMain, TextAlignmentOptions.TopLeft);
            Place(_detailDur.rectTransform, 0f, 1f, 1f, 1f, new Vector2(12f, -82f), new Vector2(-12f, -102f));

            // Botões
            _unequipBtn = MakeButton(_detailPanel.transform, "Desequipar", OnUnequipClicked);
            var ub = _unequipBtn.GetComponent<RectTransform>();
            Place(ub, 0f, 0f, 0.5f, 0f, new Vector2(12f, 10f), new Vector2(-6f, 38f));

            _repairBtn = MakeButton(_detailPanel.transform, "Reparar", OnRepairClicked);
            var rb = _repairBtn.GetComponent<RectTransform>();
            Place(rb, 0.5f, 0f, 1f, 0f, new Vector2(6f, 10f), new Vector2(-12f, 38f));
        }

        protected override void Refresh()
        {
            if (World == null) return;
            var s = World.Local;
            if (s?.equipment == null) return;

            for (int i = 0; i < 4; i++)
            {
                string gearId = s.GetEquip(SLOTS[i]);
                bool   has    = !string.IsNullOrEmpty(gearId);
                _slotBg[i].color  = has ? SlotBg : SlotEmpty;
                _slotLbls[i].text = has ? GearNames.Display(gearId) : SlotPlaceholder(SLOTS[i]);
                _slotLbls[i].color = has ? TextMain : TextDim;
            }

            RefreshDetails();
        }

        // ─── Detalhes do slot selecionado ──────────────────────────────────────────
        private void RefreshDetails()
        {
            var s = World?.Local;
            if (s == null) return;

            string gearId = _selectedSlot != null ? s.GetEquip(_selectedSlot) : null;
            bool has = !string.IsNullOrEmpty(gearId);

            if (!has)
            {
                _detailName.text = _selectedSlot == null
                    ? "(clique num slot para detalhes)"
                    : "(slot vazio)";
                _detailMastery.text = "";
                _detailDur.text = "";
                _detailMasteryFill.fillAmount = 0f;
                _unequipBtn.interactable = false;
                _repairBtn.interactable  = false;
                return;
            }

            var m = s.GetMastery(gearId);
            int lv = m != null ? m.level : 1;
            float ratio = (m != null && m.xpMax > 0) ? (float)m.xp / m.xpMax : 0f;
            int dur = s.GetDurability(_selectedSlot);

            _detailName.text    = $"{GearNames.Display(gearId)}";
            _detailMastery.text = $"Maestria: Lv {lv}" + (m != null && m.yfPending > 0 ? $"   (Fama Amarela: {m.yfPending})" : "");
            _detailMasteryFill.fillAmount = ratio;
            _detailDur.text     = $"Durabilidade: {dur}/100";
            _detailDur.color    = dur <= 0 ? new Color(0.9f, 0.3f, 0.2f) : (dur < 30 ? new Color(0.9f, 0.7f, 0.2f) : TextMain);

            _unequipBtn.interactable = true;

            // Reparar: exige durabilidade < 100 E proximidade do Ferreiro (BLACKSMITH_RANGE = 120px)
            bool nearBlacksmith = GameManager.Instance != null
                && GameManager.Instance.DistanceToNpc("blacksmith") <= 120f;
            _repairBtn.interactable = dur < 100 && nearBlacksmith;
            _repairBtn.GetComponentInChildren<TMPro.TMP_Text>().text = nearBlacksmith
                ? "Reparar"
                : "Reparar ⚒ (longe)";
        }

        // ─── Ações ─────────────────────────────────────────────────────────────────
        private void OnSlotClicked(int index)
        {
            _selectedSlot = SLOTS[index];
            // destaca o slot selecionado
            for (int i = 0; i < 4; i++)
            {
                var has = !string.IsNullOrEmpty(World?.Local?.GetEquip(SLOTS[i]));
                _slotBg[i].color = (i == index)
                    ? new Color(0.30f, 0.35f, 0.45f, 1f)
                    : (has ? SlotBg : SlotEmpty);
            }
            RefreshDetails();
        }

        private void OnUnequipClicked()
        {
            if (string.IsNullOrEmpty(_selectedSlot)) return;
            Emit("gear:unequip", $"{{\"slot\":\"{_selectedSlot}\"}}");
        }

        private void OnRepairClicked()
        {
            if (string.IsNullOrEmpty(_selectedSlot)) return;
            Emit("repair:item", $"{{\"slot\":\"{_selectedSlot}\"}}");
        }

        // ─── Helpers de montagem ───────────────────────────────────────────────────
        private Button MakeSlot(RectTransform body, int index, Vector2 anchor)
        {
            var go  = NewRect($"Slot_{SLOTS[index]}", body);
            var r   = go.GetComponent<RectTransform>();
            r.anchorMin = anchor; r.anchorMax = anchor; r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = new Vector2(96f, 96f);
            r.anchoredPosition = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = SlotEmpty;
            _slotBg[index] = img;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = index;
            btn.onClick.AddListener(() => OnSlotClicked(captured));

            // Tipo do slot (canto superior)
            var tag = MakeText(go.transform, SlotTag(SLOTS[index]), 11, Accent, TextAlignmentOptions.Top);
            Place(tag.rectTransform, 0f, 1f, 1f, 1f, new Vector2(2f, -2f), new Vector2(-2f, -18f));

            // Nome do gear (centro)
            var lbl = MakeText(go.transform, "", 12, TextDim, TextAlignmentOptions.Center);
            lbl.textWrappingMode = TMPro.TextWrappingModes.Normal;
            Place(lbl.rectTransform, 0f, 0f, 1f, 1f, new Vector2(4f, 4f), new Vector2(-4f, -18f));
            _slotLbls[index] = lbl;

            return btn;
        }

        /// <summary>Texto exibido quando o slot está vazio.</summary>
        private static string SlotPlaceholder(string slot) => slot switch
        {
            "weapon" => "(sem arma)",
            "chest"  => "(sem couraça)",
            "head"   => "(sem elmo)",
            "boots"  => "(sem botas)",
            _        => "(vazio)",
        };

        /// <summary>R\u00f3tulo de tipo exibido no canto do slot.</summary>
        private static string SlotTag(string slot) => slot switch
        {
            "weapon" => "ARMA",
            "chest"  => "COURA\u00c7A",
            "head"   => "ELMO",
            "boots"  => "BOTAS",
            _        => slot.ToUpper(),
        };

        private static void Place(RectTransform r, float axMin, float ayMin, float axMax, float ayMax,
                                  Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = new Vector2(axMin, ayMin);
            r.anchorMax = new Vector2(axMax, ayMax);
            r.offsetMin = offMin;
            r.offsetMax = offMax;
        }
    }
}
