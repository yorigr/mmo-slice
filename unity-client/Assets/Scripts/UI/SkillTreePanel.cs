// SkillTreePanel.cs  (tecla K)
// Permite escolher qual skill fica ativa em cada slot de gear (Q/W/E da arma,
// R do peito, D do elmo, F das botas).
//
// As opções vêm de WorldState.Local.gearOptions (preenchido a partir de
// player:joined.gearOptions). A skill atualmente selecionada tem borda dourada.
// Slots de peças não equipadas aparecem cinza/desabilitados.
//
// Clicar numa opção → emite skill:select { slotKey, skillId }. O servidor confirma
// via skill:select_result (tratado no WorldState), o que dispara o Refresh.
//
// Hover nos botões de skill → TooltipPopup exibe cooldown, mana, dano, descrição
// usando dados do SkillCatalog (espelho estático de skills.json).

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.World;

namespace MMORPG.UI
{
    public class SkillTreePanel : UIPanelBase
    {
        protected override string  Title     => "Habilidades";
        protected override Vector2 PanelSize => new Vector2(560f, 640f);

        // Cada slotKey tem uma linha com N botões de opção. Guardamos os botões para repintar.
        private static readonly (string slotKey, string label)[] ROWS =
        {
            ("weapon_Q", "Q"),
            ("weapon_W", "W"),
            ("weapon_E", "E"),
            ("chest_R",  "R"),
            ("head_D",   "D"),
            ("boots_F",  "F"),
        };

        // slotKey → lista de (button, outline, skillId)
        private readonly Dictionary<string, List<OptionWidget>> _options = new();
        private readonly Dictionary<string, TextMeshProUGUI>    _sectionLabels = new();
        private VerticalLayoutGroup _layout;
        private RectTransform _container;

        private class OptionWidget
        {
            public string  skillId;
            public Button   button;
            public Outline  outline;
        }

        protected override void BuildContent(RectTransform body)
        {
            _container = body;
            _layout = body.gameObject.AddComponent<VerticalLayoutGroup>();
            _layout.spacing = 8f;
            _layout.childForceExpandWidth  = true;
            _layout.childForceExpandHeight = false;
            _layout.childControlHeight     = true;
            _layout.childControlWidth      = true;

            // Cabeçalhos de seção + linhas de opções (recriadas em Refresh quando o gear muda,
            // mas a estrutura base é estável: 6 slotKeys fixos).
        }

        protected override void Refresh()
        {
            if (World == null) return;
            var s = World.Local;
            if (s == null) return;

            // Reconstrói as linhas de opção do zero (o conjunto de opções muda com o gear).
            RebuildRows(s);
        }

        // ─── Reconstrução das linhas ───────────────────────────────────────────────
        private void RebuildRows(LocalFullState s)
        {
            // Limpa filhos existentes
            foreach (Transform child in _container)
                Destroy(child.gameObject);
            _options.Clear();
            _sectionLabels.Clear();

            string lastSection = null;

            foreach (var (slotKey, label) in ROWS)
            {
                // Insere um cabeçalho de seção quando muda a peça (arma/peito/elmo/botas)
                string section = SectionFor(slotKey, s);
                if (section != lastSection)
                {
                    AddSectionHeader(section);
                    lastSection = section;
                }

                AddOptionRow(slotKey, label, s);
            }
        }

        private void AddSectionHeader(string text)
        {
            var txt = MakeText(_container, text, 15, Accent, TextAlignmentOptions.Left);
            txt.fontStyle = FontStyles.Bold;
            var le = txt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 22f;
        }

        private void AddOptionRow(string slotKey, string label, LocalFullState s)
        {
            string gearId = GearForSlot(slotKey, s);
            bool   equipped = !string.IsNullOrEmpty(gearId);
            string[] opts   = equipped ? s.OptionsForSlot(gearId, slotKey) : new string[0];
            string selected = s.GetSkill(slotKey);

            var row = NewRect($"Row_{slotKey}", _container);
            var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 6f;
            rowLayout.childForceExpandWidth  = false;
            rowLayout.childForceExpandHeight = true;
            rowLayout.childControlWidth      = true;
            rowLayout.childControlHeight     = true;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            var rle = row.AddComponent<LayoutElement>();
            rle.minHeight = 38f;

            // Prefixo com a tecla (Q/W/E/R/D/F)
            var keyTxt = MakeText(row.transform, label + ":", 15, TextMain, TextAlignmentOptions.Left);
            keyTxt.fontStyle = FontStyles.Bold;
            var kle = keyTxt.gameObject.AddComponent<LayoutElement>();
            kle.minWidth = 28f; kle.preferredWidth = 28f;

            var widgets = new List<OptionWidget>();

            if (!equipped)
            {
                var none = MakeText(row.transform, "(peça não equipada)", 13, TextDim, TextAlignmentOptions.Left);
                none.fontStyle = FontStyles.Italic;
            }
            else if (opts.Length == 0)
            {
                var none = MakeText(row.transform, "(sem opções)", 13, TextDim, TextAlignmentOptions.Left);
            }
            else
            {
                foreach (var skillId in opts)
                {
                    var w = MakeOptionButton(row.transform, slotKey, skillId, skillId == selected);
                    widgets.Add(w);
                }
            }

            _options[slotKey] = widgets;
        }

        private OptionWidget MakeOptionButton(Transform parent, string slotKey, string skillId, bool isSelected)
        {
            var btn = MakeButton(parent, GearNames.Skill(skillId), null);
            var le  = btn.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 130f; le.preferredWidth = 150f; le.minHeight = 34f;

            // Borda dourada na skill ativa
            var outline = btn.gameObject.AddComponent<Outline>();
            outline.effectColor    = Gold;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.enabled        = isSelected;

            // Realce de fundo na selecionada
            var img = btn.targetGraphic as Image;
            if (img != null && isSelected)
                img.color = new Color(0.32f, 0.30f, 0.16f, 1f);

            string capturedSlot  = slotKey;
            string capturedSkill = skillId;
            btn.onClick.AddListener(() => OnOptionClicked(capturedSlot, capturedSkill));

            // Hover → tooltip com dados da skill (cooldown, mana, dano, descrição)
            AddTooltipHover(btn.gameObject, skillId);

            return new OptionWidget { skillId = skillId, button = btn, outline = outline };
        }

        // ─── Tooltip ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Registra handlers de PointerEnter/Exit no GO para exibir TooltipPopup.
        /// Usa EventTrigger para não precisar de MonoBehaviour próprio por botão.
        /// </summary>
        private static void AddTooltipHover(GameObject go, string skillId)
        {
            var et = go.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            // PointerEnter → mostra tooltip
            var enterEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerEnter
            };
            enterEntry.callback.AddListener(_ =>
            {
                var entry = SkillCatalog.Get(skillId);
                string text = entry != null
                    ? entry.ToTooltipText()
                    : $"<b>{GearNames.Skill(skillId)}</b>";
                TooltipPopup.Show(text, UnityEngine.Input.mousePosition);
            });
            et.triggers.Add(enterEntry);

            // PointerExit → esconde tooltip
            var exitEntry = new UnityEngine.EventSystems.EventTrigger.Entry
            {
                eventID = UnityEngine.EventSystems.EventTriggerType.PointerExit
            };
            exitEntry.callback.AddListener(_ => TooltipPopup.Hide());
            et.triggers.Add(exitEntry);
        }

        // ─── Ação ──────────────────────────────────────────────────────────────────
        private void OnOptionClicked(string slotKey, string skillId)
        {
            Emit("skill:select", $"{{\"slotKey\":\"{slotKey}\",\"skillId\":\"{skillId}\"}}");
            // O Refresh virá quando o servidor confirmar (skill:select_result).
        }

        // ─── Helpers ───────────────────────────────────────────────────────────────
        private static string GearForSlot(string slotKey, LocalFullState s)
        {
            if (s?.equipment == null) return null;
            if (slotKey.StartsWith("weapon_")) return s.equipment.weapon;
            return slotKey switch
            {
                "chest_R" => s.equipment.chest,
                "head_D"  => s.equipment.head,
                "boots_F" => s.equipment.boots,
                _         => null
            };
        }

        /// <summary>Retorna o nome de exibição do gear que corresponde ao slot de skill.</summary>
        private static string SectionFor(string slotKey, LocalFullState s)
        {
            if (s?.equipment == null) return slotKey;
            if (slotKey.StartsWith("weapon")) return string.IsNullOrEmpty(s.equipment.weapon) ? "Arma"    : GearNames.Display(s.equipment.weapon);
            if (slotKey.StartsWith("chest"))  return string.IsNullOrEmpty(s.equipment.chest)  ? "Coura\u00e7a" : GearNames.Display(s.equipment.chest);
            if (slotKey.StartsWith("head"))   return string.IsNullOrEmpty(s.equipment.head)   ? "Elmo"    : GearNames.Display(s.equipment.head);
            if (slotKey.StartsWith("boots"))  return string.IsNullOrEmpty(s.equipment.boots)  ? "Botas"   : GearNames.Display(s.equipment.boots);
            return slotKey;
        }

        private static string SelectedSkillFor(string slotKey, LocalFullState s)
        {
            if (s?.selectedSkills == null) return null;
            // SelectedSkills tem campos nomeados por slot — mapeia diretamente.
            return slotKey switch
            {
                "weapon_Q" => s.selectedSkills.weapon_Q,
                "weapon_W" => s.selectedSkills.weapon_W,
                "weapon_E" => s.selectedSkills.weapon_E,
                "chest_R"  => s.selectedSkills.chest_R,
                "head_D"   => s.selectedSkills.head_D,
                "boots_F"  => s.selectedSkills.boots_F,
                _          => null,
            };
        }
    }
}
