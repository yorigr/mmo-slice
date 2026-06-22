// InventoryPanel.cs  (tecla I)
// Grid 5×6 (30 slots) do inventário. Clicar num item o seleciona e revela os botões
// de ação: Equipar (gear:equip), Usar (item:use) e Dropar (item:drop).
//
// O slot de equipamento é inferido do tipo do item (GearNames.SlotOf). Se o item não
// for equipável (ex: material), o botão Equipar fica desabilitado.
//
// Hover num slot com item → TooltipPopup exibe nome, descrição, efeitos e valor
// usando dados do ItemCatalog (espelho de items.json).
//
// Atualiza via inventory:updated (e via player:joined inicial) — ambos tratados no
// WorldState, que dispara OnLocalStateUpdated.

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.World;

namespace MMORPG.UI
{
    public class InventoryPanel : UIPanelBase
    {
        protected override string  Title     => "Inventário";
        protected override Vector2 PanelSize => new Vector2(520f, 560f);

        private const int COLS = 5;
        private const int ROWS = 6;
        private const int MAX_INVENTORY_SLOTS = COLS * ROWS; // 30

        private readonly Image[]           _slotBg   = new Image[MAX_INVENTORY_SLOTS];
        private readonly TextMeshProUGUI[] _slotLbls = new TextMeshProUGUI[MAX_INVENTORY_SLOTS];
        private readonly Outline[]         _slotOutl = new Outline[MAX_INVENTORY_SLOTS];

        private TextMeshProUGUI _titleCount;
        private TextMeshProUGUI _selectedLbl;
        private Button          _equipBtn, _useBtn, _dropBtn;

        private int    _selectedIndex = -1;
        private string _selectedItemId;
        private string _selectedItemType;

        protected override void BuildContent(RectTransform body)
        {
            // Contador (X/30) no topo da área de conteúdo
            _titleCount = MakeText(body, "(0/30)", 14, TextDim, TextAlignmentOptions.Left);
            var tcRect = _titleCount.rectTransform;
            tcRect.anchorMin = new Vector2(0f, 1f); tcRect.anchorMax = new Vector2(1f, 1f);
            tcRect.pivot = new Vector2(0.5f, 1f);
            tcRect.sizeDelta = new Vector2(0f, 20f);
            tcRect.anchoredPosition = Vector2.zero;

            // Grid de slots
            var grid     = NewRect("Grid", body);
            var gridRect = grid.GetComponent<RectTransform>();
            gridRect.anchorMin = new Vector2(0f, 0.28f);
            gridRect.anchorMax = new Vector2(1f, 1f);
            gridRect.offsetMin = new Vector2(0f, 0f);
            gridRect.offsetMax = new Vector2(0f, -26f);

            var gl = grid.AddComponent<GridLayoutGroup>();
            gl.cellSize       = new Vector2(86f, 64f);
            gl.spacing        = new Vector2(6f, 6f);
            gl.constraint     = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = COLS;
            gl.childAlignment  = TextAnchor.UpperCenter;

            for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
                MakeSlot(grid.transform, i);

            // Painel de ação (parte de baixo)
            _selectedLbl = MakeText(body, "(nenhum item selecionado)", 15, TextMain, TextAlignmentOptions.Left);
            var slRect = _selectedLbl.rectTransform;
            slRect.anchorMin = new Vector2(0f, 0.14f); slRect.anchorMax = new Vector2(1f, 0.22f);
            slRect.offsetMin = Vector2.zero; slRect.offsetMax = Vector2.zero;

            _equipBtn = MakeButton(body, "Equipar", OnEquip);
            _useBtn   = MakeButton(body, "Usar",    OnUse);
            _dropBtn  = MakeButton(body, "Dropar",  OnDrop);
            PlaceButton(_equipBtn, 0f,    0.34f);
            PlaceButton(_useBtn,   0.34f, 0.67f);
            PlaceButton(_dropBtn,  0.67f, 1f);
        }

        protected override void Refresh()
        {
            if (World == null) return;
            var s = World.Local;
            if (s == null) return;
            if (_slotBg[0] == null) return; // BuildContent ainda não terminou

            var inv = s.inventory ?? new InventoryItem[0];
            _titleCount.text = $"({inv.Length}/{MAX_INVENTORY_SLOTS})";

            for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
            {
                bool filled = i < inv.Length && inv[i] != null;
                _slotBg[i].color  = filled ? SlotBg : SlotEmpty;
                _slotLbls[i].text = filled ? GearNames.Display(inv[i].type) : "";
                _slotOutl[i].enabled = (i == _selectedIndex && filled);
            }

            // Se o item selecionado saiu do inventário, limpa a seleção.
            if (_selectedIndex >= 0 && (_selectedIndex >= inv.Length || inv[_selectedIndex] == null))
                ClearSelection();
            else
                RefreshActionButtons();
        }

        // ─── Seleção / ações ───────────────────────────────────────────────────────
        private void OnSlotClicked(int index)
        {
            var inv = World?.Local?.inventory;
            if (inv == null || index >= inv.Length || inv[index] == null)
            {
                ClearSelection();
                return;
            }

            _selectedIndex    = index;
            _selectedItemId   = inv[index].id;
            _selectedItemType = inv[index].type;

            for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
                _slotOutl[i].enabled = (i == index);

            _selectedLbl.text = $"Selecionado: {GearNames.Display(_selectedItemType)}";
            RefreshActionButtons();
        }

        private void ClearSelection()
        {
            _selectedIndex = -1;
            _selectedItemId = null;
            _selectedItemType = null;
            _selectedLbl.text = "(nenhum item selecionado)";
            for (int i = 0; i < MAX_INVENTORY_SLOTS; i++)
                if (_slotOutl[i] != null) _slotOutl[i].enabled = false;
            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            bool hasSel = !string.IsNullOrEmpty(_selectedItemId);
            string slot = hasSel ? GearNames.SlotOf(_selectedItemType) : null;

            _equipBtn.interactable = hasSel && !string.IsNullOrEmpty(slot);
            _useBtn.interactable    = hasSel;
            _dropBtn.interactable   = hasSel;
        }

        private void OnEquip()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            string slot = GearNames.SlotOf(_selectedItemType);
            if (string.IsNullOrEmpty(slot)) return;
            // gear:equip espera o gearId (= type do item).
            Emit("gear:equip", $"{{\"slot\":\"{slot}\",\"gearId\":\"{_selectedItemType}\"}}");
        }

        private void OnUse()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            Emit("item:use", $"{{\"itemId\":\"{_selectedItemId}\"}}");
        }

        private void OnDrop()
        {
            if (string.IsNullOrEmpty(_selectedItemId)) return;
            Emit("item:drop", $"{{\"itemId\":\"{_selectedItemId}\"}}");
        }

        // ─── Montagem ──────────────────────────────────────────────────────────────
        private void MakeSlot(Transform parent, int index)
        {
            var go  = NewRect($"InvSlot_{index}", parent);
            var img = go.AddComponent<Image>();
            img.color = SlotEmpty;
            _slotBg[index] = img;

            var outline = go.AddComponent<Outline>();
            outline.effectColor    = Gold;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.enabled        = false;
            _slotOutl[index] = outline;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            int captured = index;
            btn.onClick.AddListener(() => OnSlotClicked(captured));

            // Hover → tooltip com dados do item
            AddItemTooltipHover(go, captured);

            var lbl = MakeText(go.transform, "", 11, TextMain, TextAlignmentOptions.Center);
            lbl.textWrappingMode = TMPro.TextWrappingModes.Normal;
            var r = lbl.rectTransform;
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(2f, 2f); r.offsetMax = new Vector2(-2f, -2f);
            _slotLbls[index] = lbl;
        }

        // ─── Tooltip ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Registra handlers de PointerEnter/Exit no GO do slot.
        /// Lê o item atual do inventário em runtime (suporta mudanças dinâmicas).
        /// </summary>
        private void AddItemTooltipHover(GameObject go, int index)
        {
            var et = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener(_ =>
            {
                var inv = World?.Local?.inventory;
                if (inv == null || index >= inv.Length || inv[index] == null) return;

                string itemType = inv[index].type;
                var entry = ItemCatalog.Get(itemType);
                string text = entry != null
                    ? entry.ToTooltipText()
                    : $"<b>{GearNames.Display(itemType)}</b>";
                TooltipPopup.Show(text, UnityEngine.Input.mousePosition);
            });
            et.triggers.Add(enterEntry);

            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener(_ => TooltipPopup.Hide());
            et.triggers.Add(exitEntry);
        }

        private void PlaceButton(Button btn, float anchorMinX, float anchorMaxX)
        {
            var r = btn.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(anchorMinX, 0f);
            r.anchorMax = new Vector2(anchorMaxX, 0.12f);
            r.offsetMin = new Vector2(anchorMinX == 0f ? 0f : 4f, 0f);
            r.offsetMax = new Vector2(anchorMaxX == 1f ? 0f : -4f, 0f);
        }
    }
}
