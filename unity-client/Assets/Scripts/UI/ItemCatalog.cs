// ItemCatalog.cs
// Catálogo estático de itens, espelhando server/src/config/items.json.
//
// Mesmo padrão do SkillCatalog.cs: lookup por id, método ToTooltipText().
// Ao adicionar item no servidor, espelhe aqui.
//
// Uso:
//   var entry = ItemCatalog.Get("potion_small");
//   if (entry != null) TooltipPopup.Show(entry.ToTooltipText(), mousePos);

namespace MMORPG.UI
{
    public class ItemCatalogEntry
    {
        public string id;
        public string name;
        public string type;        // "consumable", "equipment", "material"
        public string rarity;      // "common", "uncommon", "rare", "epic"
        public string description;
        public string slot;        // para equipment: "weapon", "chest", "head", "boots"
        public int    value;       // gold
        public bool   stackable;
        // Efeito (consumíveis)
        public int    effectHp;
        public int    effectMana;
        // Stats (equipamentos)
        public int    statDamage;
        public int    statMaxHp;
        public int    statMaxMana;
        public int    statSpeed;   // positivo = bônus, negativo = penalidade
        public int    statRange;
        public float  statDamageReduction; // 0.05 = 5%

        /// <summary>Gera texto formatado para o TooltipPopup.</summary>
        public string ToTooltipText()
        {
            var sb = new System.Text.StringBuilder();

            // Título com raridade colorida
            string rarityColor = rarity switch
            {
                "uncommon" => "#4CAF50",
                "rare"     => "#2196F3",
                "epic"     => "#9C27B0",
                _          => "#FFFFFF"
            };
            sb.AppendLine($"<b><color={rarityColor}>{name}</color></b>");

            // Tipo legível
            string typeLabel = type switch
            {
                "consumable" => "Consumível",
                "equipment"  => "Equipamento",
                "material"   => "Material",
                _            => type
            };
            sb.AppendLine($"<size=90%><color=#AAAAAA>{typeLabel}</color></size>");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(description))
                sb.AppendLine(description);

            // Efeitos de consumível
            if (effectHp > 0)   sb.AppendLine($"<color=#4CAF50>+{effectHp} HP</color>");
            if (effectMana > 0) sb.AppendLine($"<color=#2196F3>+{effectMana} Mana</color>");

            // Stats de equipamento
            if (statDamage > 0)          sb.AppendLine($"Dano: +{statDamage}");
            if (statMaxHp > 0)           sb.AppendLine($"HP Máximo: +{statMaxHp}");
            if (statMaxMana > 0)         sb.AppendLine($"Mana Máxima: +{statMaxMana}");
            if (statSpeed != 0)          sb.AppendLine($"Velocidade: {(statSpeed > 0 ? "+" : "")}{statSpeed}");
            if (statRange > 0)           sb.AppendLine($"Alcance: +{statRange}");
            if (statDamageReduction > 0) sb.AppendLine($"Redução de dano: {statDamageReduction * 100f:F0}%");

            // Valor
            sb.AppendLine();
            sb.Append($"<color=#FFD700>Valor: {value} ouro</color>");
            if (stackable) sb.Append(" · Empilhável");

            return sb.ToString().TrimEnd();
        }
    }

    public static class ItemCatalog
    {
        private static System.Collections.Generic.Dictionary<string, ItemCatalogEntry> _entries;

        public static ItemCatalogEntry Get(string itemId)
        {
            EnsureInit();
            return _entries.TryGetValue(itemId, out var e) ? e : null;
        }

        private static void EnsureInit()
        {
            if (_entries != null) return;
            _entries = new System.Collections.Generic.Dictionary<string, ItemCatalogEntry>();
            Register();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Catálogo — espelho de server/src/config/items.json
        // ──────────────────────────────────────────────────────────────────────────
        private static void Register()
        {
            // Consumíveis
            Add(new ItemCatalogEntry {
                id="potion_small", name="Poção Pequena", type="consumable", rarity="common",
                description="Restaura 30 HP.", effectHp=30, value=10, stackable=true
            });
            Add(new ItemCatalogEntry {
                id="potion_large", name="Poção Grande", type="consumable", rarity="uncommon",
                description="Restaura 80 HP.", effectHp=80, value=30, stackable=true
            });
            Add(new ItemCatalogEntry {
                id="mana_potion", name="Poção de Mana", type="consumable", rarity="common",
                description="Restaura 40 de mana.", effectMana=40, value=15, stackable=true
            });

            // Armas
            Add(new ItemCatalogEntry {
                id="sword_rusty", name="Espada Enferrujada", type="equipment", slot="weapon",
                rarity="common", description="Uma espada velha. Melhor que os punhos.",
                statDamage=5, statSpeed=-5, value=20, stackable=false
            });
            Add(new ItemCatalogEntry {
                id="axe_iron", name="Machado de Ferro", type="equipment", slot="weapon",
                rarity="uncommon", description="Machado pesado. Alto dano, menos velocidade.",
                statDamage=18, statSpeed=-15, value=80, stackable=false
            });
            Add(new ItemCatalogEntry {
                id="bow_bone", name="Arco de Osso", type="equipment", slot="weapon",
                rarity="common", description="Arco frágil feito de ossos.",
                statDamage=8, statRange=30, value=25, stackable=false
            });
            Add(new ItemCatalogEntry {
                id="club_heavy", name="Clava Pesada", type="equipment", slot="weapon",
                rarity="uncommon", description="Clava de madeira reforçada.",
                statDamage=22, statSpeed=-20, value=90, stackable=false
            });

            // Armaduras
            Add(new ItemCatalogEntry {
                id="armor_leather", name="Armadura de Couro", type="equipment", slot="chest",
                rarity="common", description="+15 HP máximo. Proteção leve.",
                statMaxHp=15, statDamageReduction=0.05f, value=60, stackable=false
            });

            // Materiais
            Add(new ItemCatalogEntry {
                id="wolf_pelt", name="Pele de Lobo", type="material", rarity="common",
                description="Material de artesanato. Pode ser vendido.",
                value=18, stackable=true
            });
            Add(new ItemCatalogEntry {
                id="troll_hide", name="Couro de Troll", type="material", rarity="uncommon",
                description="Material raro. Vale bastante.",
                value=75, stackable=true
            });
        }

        private static void Add(ItemCatalogEntry e) => _entries[e.id] = e;
    }
}
