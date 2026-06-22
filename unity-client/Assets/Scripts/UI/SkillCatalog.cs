// SkillCatalog.cs
// Catálogo estático de dados de skills, espelhando server/src/config/skills.json.
//
// Por que manter uma cópia no cliente?
//   O servidor envia em player:joined apenas as skills equipadas (abilities[]).
//   Para tooltips no SkillTreePanel (hover sobre opções de skill),
//   precisamos de cooldown, mana, stamina, dano e descrição de QUALQUER skill —
//   mesmo as não equipadas. Manter a cópia evita uma round-trip de rede por hover.
//
// Manutenção:
//   Ao adicionar uma skill no servidor (skills.json), espelhe-a aqui.
//   O formato é intencionalmente idêntico ao JSON do servidor para facilitar sync.
//
// Uso:
//   var data = SkillCatalog.Get("skill_slash");
//   if (data != null) TooltipPopup.Show(data.ToTooltipText(), mousePos);

namespace MMORPG.UI
{
    /// <summary>Dados completos de uma skill (espelho de skills.json).</summary>
    public class SkillCatalogEntry
    {
        public string id;
        public string name;
        public string type;
        public int    castTime;    // ms; 0 = instantâneo
        public int    cooldown;    // ms
        public int    mana;
        public int    stamina;
        public int    range;       // px (servidor)
        public int    damage;
        public string description;
        public string statusEffect;   // opcional: "bleed", "stun", "slow", etc.
        public int    statusDuration; // ms
        public int    dotDamage;
        public int    dotTicks;
        public bool   isHeal;

        /// <summary>Gera texto formatado para exibição no tooltip.</summary>
        public string ToTooltipText()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>{name}</b>");
            sb.AppendLine();
            if (!string.IsNullOrEmpty(description))
                sb.AppendLine(description);
            sb.AppendLine();

            // Linha de custo
            var costs = new System.Collections.Generic.List<string>();
            if (mana    > 0) costs.Add($"{mana} Mana");
            if (stamina > 0) costs.Add($"{stamina} Estamina");
            if (costs.Count > 0)
                sb.AppendLine($"Custo: {string.Join(", ", costs)}");

            // Linha de stats
            if (damage > 0)
                sb.AppendLine($"Dano: {damage}");
            if (isHeal && damage > 0)
                sb.AppendLine($"Cura: {damage}");

            // Cooldown e cast time
            if (castTime > 0)
                sb.AppendLine($"Tempo de cast: {castTime / 1000f:F1}s");
            sb.AppendLine($"Cooldown: {cooldown / 1000f:F1}s");

            // Range
            if (range > 0)
                sb.AppendLine($"Alcance: {range} u");

            // Efeito de status
            if (!string.IsNullOrEmpty(statusEffect))
            {
                string effLabel = statusEffect switch
                {
                    "bleed"  => "Sangramento",
                    "stun"   => "Atordoamento",
                    "slow"   => "Lentidão",
                    "poison" => "Veneno",
                    _        => statusEffect
                };
                sb.AppendLine($"Efeito: {effLabel} ({statusDuration / 1000f:F1}s)");
                if (dotDamage > 0 && dotTicks > 0)
                    sb.AppendLine($"  {dotDamage} dano × {dotTicks} ticks");
            }

            return sb.ToString().TrimEnd();
        }
    }

    /// <summary>
    /// Lookup estático: skillId → SkillCatalogEntry.
    /// Inicializado uma vez na primeira chamada (lazy init).
    /// </summary>
    public static class SkillCatalog
    {
        private static System.Collections.Generic.Dictionary<string, SkillCatalogEntry> _entries;

        public static SkillCatalogEntry Get(string skillId)
        {
            EnsureInit();
            return _entries.TryGetValue(skillId, out var e) ? e : null;
        }

        // Todos os IDs registrados (útil para debug)
        public static System.Collections.Generic.IEnumerable<string> AllIds
        {
            get { EnsureInit(); return _entries.Keys; }
        }

        private static void EnsureInit()
        {
            if (_entries != null) return;
            _entries = new System.Collections.Generic.Dictionary<string, SkillCatalogEntry>();
            Register();
        }

        // ──────────────────────────────────────────────────────────────────────────
        // Catálogo — espelho de server/src/config/skills.json
        // ──────────────────────────────────────────────────────────────────────────
        private static void Register()
        {
            Add(new SkillCatalogEntry {
                id="skill_basic_punch", name="Soco", type="melee",
                castTime=0, cooldown=1000, mana=0, stamina=5, range=50, damage=8,
                description="Ataque básico sem arma."
            });
            Add(new SkillCatalogEntry {
                id="skill_slash", name="Golpe", type="melee",
                castTime=0, cooldown=600, mana=0, stamina=10, range=65, damage=15,
                description="Ataque básico instantâneo."
            });
            Add(new SkillCatalogEntry {
                id="skill_rend", name="Rasgar", type="melee_dot",
                castTime=0, cooldown=5000, mana=0, stamina=15, range=65, damage=8,
                statusEffect="bleed", statusDuration=4000, dotDamage=4, dotTicks=4,
                description="8 dano + sangramento: 4 dano/s por 4s (incurável)."
            });
            Add(new SkillCatalogEntry {
                id="skill_heavy_blow", name="Golpe Pesado", type="melee",
                castTime=400, cooldown=1500, mana=0, stamina=25, range=65, damage=38,
                description="Golpe poderoso. Interrompível por dano."
            });
            Add(new SkillCatalogEntry {
                id="skill_shield_bash", name="Golpe de Escudo", type="melee",
                castTime=0, cooldown=8000, mana=0, stamina=20, range=65, damage=10,
                statusEffect="stun", statusDuration=1500,
                description="Atordoa o alvo por 1.5s."
            });
            Add(new SkillCatalogEntry {
                id="skill_execute", name="Executar", type="melee",
                castTime=0, cooldown=12000, mana=0, stamina=40, range=65, damage=70,
                description="Dano massivo. Mais eficaz contra alvos com <30% HP."
            });
            Add(new SkillCatalogEntry {
                id="skill_fireball", name="Bola de Fogo", type="magic",
                castTime=800, cooldown=2000, mana=30, stamina=0, range=200, damage=40,
                description="Projétil mágico de fogo. Interrompível."
            });
            Add(new SkillCatalogEntry {
                id="skill_frostbolt", name="Raio Gélido", type="magic",
                castTime=600, cooldown=1500, mana=20, stamina=0, range=200, damage=22,
                statusEffect="slow", statusDuration=2000,
                description="Projétil gélido. Reduz velocidade do alvo."
            });
            Add(new SkillCatalogEntry {
                id="skill_arcane_missile", name="Míssil Arcano", type="magic",
                castTime=0, cooldown=800, mana=15, stamina=0, range=200, damage=18,
                description="Projétil arcano instantâneo."
            });
            Add(new SkillCatalogEntry {
                id="skill_blizzard", name="Nevasca", type="magic_aoe",
                castTime=1200, cooldown=15000, mana=60, stamina=0, range=180, damage=25,
                statusEffect="slow", statusDuration=3000,
                description="AoE gélido. Dano em área + lentidão."
            });
            Add(new SkillCatalogEntry {
                id="skill_mana_shield", name="Escudo de Mana", type="buff",
                castTime=0, cooldown=20000, mana=40, stamina=0, range=0, damage=0,
                description="Absorve 50% do dano recebido usando mana por 6s."
            });
            Add(new SkillCatalogEntry {
                id="skill_teleport", name="Teletransporte", type="mobility",
                castTime=0, cooldown=10000, mana=35, stamina=0, range=250, damage=0,
                description="Teleporta para a posição do mouse."
            });
            Add(new SkillCatalogEntry {
                id="skill_shoot", name="Atirar", type="ranged",
                castTime=0, cooldown=700, mana=0, stamina=8, range=250, damage=14,
                description="Disparo básico de flecha."
            });
            Add(new SkillCatalogEntry {
                id="skill_multishot", name="Disparo Múltiplo", type="ranged_aoe",
                castTime=0, cooldown=5000, mana=0, stamina=20, range=200, damage=10,
                description="3 flechas em leque. Dano menor por projétil."
            });
            Add(new SkillCatalogEntry {
                id="skill_poison_arrow", name="Flecha Envenenada", type="ranged_dot",
                castTime=0, cooldown=8000, mana=0, stamina=15, range=250, damage=12,
                statusEffect="poison", statusDuration=5000, dotDamage=5, dotTicks=5,
                description="Veneno: 5 dano/s por 5s."
            });
            Add(new SkillCatalogEntry {
                id="skill_eagle_eye", name="Olho de Águia", type="ranged",
                castTime=0, cooldown=3000, mana=0, stamina=12, range=350, damage=30,
                description="Disparo preciso de longo alcance."
            });
            Add(new SkillCatalogEntry {
                id="skill_volley", name="Chuva de Flechas", type="ranged_aoe",
                castTime=1000, cooldown=18000, mana=0, stamina=30, range=200, damage=20,
                description="Chuva em área. Interrompível."
            });
            Add(new SkillCatalogEntry {
                id="skill_dodge", name="Esquivar", type="mobility",
                castTime=0, cooldown=6000, mana=0, stamina=25, range=0, damage=0,
                description="Rola na direção do movimento. Invulnerável por 0.4s."
            });
            Add(new SkillCatalogEntry {
                id="skill_battle_cry", name="Grito de Guerra", type="buff_aoe",
                castTime=0, cooldown=30000, mana=0, stamina=35, range=0, damage=0,
                description="Aumenta dano e velocidade de aliados próximos por 8s."
            });
            Add(new SkillCatalogEntry {
                id="skill_heal", name="Curar", type="heal",
                castTime=1000, cooldown=5000, mana=40, stamina=0, range=0, damage=50,
                isHeal=true,
                description="Restaura 50 HP. Interrompível."
            });
            Add(new SkillCatalogEntry {
                id="skill_holy_bolt", name="Raio Sagrado", type="magic",
                castTime=0, cooldown=1200, mana=25, stamina=0, range=200, damage=28,
                description="Projétil sagrado. Mais eficaz contra mortos-vivos."
            });
            Add(new SkillCatalogEntry {
                id="skill_divine_shield", name="Escudo Divino", type="buff",
                castTime=0, cooldown=60000, mana=80, stamina=0, range=0, damage=0,
                description="Imune a dano por 3s."
            });
            Add(new SkillCatalogEntry {
                id="skill_consecrate", name="Consagrar", type="magic_aoe",
                castTime=0, cooldown=20000, mana=50, stamina=0, range=100, damage=15,
                description="Área sagrada ao redor do jogador. Dano por segundo em inimigos próximos."
            });
            Add(new SkillCatalogEntry {
                id="skill_sprint", name="Correr", type="mobility",
                castTime=0, cooldown=12000, mana=0, stamina=30, range=0, damage=0,
                description="Aumenta velocidade em 50% por 4s."
            });
            Add(new SkillCatalogEntry {
                id="skill_stealth", name="Furtividade", type="buff",
                castTime=0, cooldown=15000, mana=0, stamina=20, range=0, damage=0,
                description="Torna-se invisível por 8s. Atacar quebra furtividade."
            });
            Add(new SkillCatalogEntry {
                id="skill_backstab", name="Apunhalada pelas Costas", type="melee",
                castTime=0, cooldown=6000, mana=0, stamina=20, range=65, damage=60,
                description="Dano massivo ao atacar pelas costas ou enquanto furtivo."
            });
        }

        private static void Add(SkillCatalogEntry e) => _entries[e.id] = e;
    }
}
