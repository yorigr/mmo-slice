// GearNames.cs
// Tabela de nomes amigáveis (PT-BR) para gearIds e skillIds.
//
// Por que hardcoded e não ler gear.json/skills.json em runtime?
//   O cliente Unity não embarca os JSONs do servidor. Os nomes mudam raramente e
//   uma tabela estática evita uma dependência de I/O e parsing no cliente. Se um id
//   for desconhecido, caímos num fallback legível (o próprio id formatado).
//
// Mantida em sincronia manual com server/src/config/gear.json e skills.json.

using System.Collections.Generic;

namespace MMORPG.UI
{
    public static class GearNames
    {
        private static readonly Dictionary<string, string> Gear = new()
        {
            // Armas
            { "sword",        "Espada" },
            { "greataxe",     "Machado Grande" },
            { "daggers",      "Adagas" },
            { "mace",         "Maça" },
            { "hammer",       "Martelo" },
            { "bow",          "Arco" },
            { "fire_staff",   "Cajado de Fogo" },
            { "frost_staff",  "Cajado de Gelo" },
            { "arcane_staff", "Cajado Arcano" },
            { "holy_staff",   "Cajado Sagrado" },
            // Cabeça
            { "cloth_hood",   "Capuz de Pano" },
            { "leather_cap",  "Chapéu de Couro" },
            { "plate_helm",   "Elmo de Placa" },
            // Peito
            { "cloth_chest",   "Vestes de Pano" },
            { "leather_chest", "Armadura de Couro" },
            { "plate_chest",   "Peitoral de Placa" },
            // Botas
            { "cloth_boots",   "Sandálias de Pano" },
            { "leather_boots", "Botas de Couro" },
            { "plate_boots",   "Sapatões de Placa" },
        };

        private static readonly Dictionary<string, string> Skills = new()
        {
            { "skill_slash", "Golpe" }, { "skill_rend", "Rasgar" },
            { "skill_heavy_blow", "Golpe Pesado" }, { "skill_shield_bash", "Golpe de Escudo" },
            { "skill_execute", "Executar" }, { "skill_charge", "Investida" },
            { "skill_cleave", "Golpe Amplo" }, { "skill_whirlwind", "Redemoinho" },
            { "skill_decimate", "Dizimar" }, { "skill_ground_slam", "Golpe Sísmico" },
            { "skill_stab", "Estocada" }, { "skill_lacerate", "Laceração" },
            { "skill_shadow_dash", "Dash das Sombras" }, { "skill_expose", "Expor" },
            { "skill_pummel", "Espancar" }, { "skill_shatter", "Fragmentar" },
            { "skill_body_slam", "Voadora" }, { "skill_fortify", "Fortalecer" },
            { "skill_sunder_armor", "Despedaçar Armadura" }, { "skill_smash", "Esmagar" },
            { "skill_earth_shatter", "Quebrar Terra" }, { "skill_tremor", "Tremor" },
            { "skill_thunderclap", "Trovão" }, { "skill_seismic_stomp", "Pisão Sísmico" },
            { "skill_quick_shot", "Tiro Rápido" }, { "skill_barbed_arrow", "Flecha Farpada" },
            { "skill_aimed_shot", "Tiro Mirando" }, { "skill_multi_shot", "Múltiplos Tiros" },
            { "skill_bear_trap", "Armadilha" }, { "skill_fire_bolt", "Projétil de Fogo" },
            { "skill_arcane_pulse", "Pulso Arcano" }, { "skill_fireball", "Bola de Fogo" },
            { "skill_fire_wall", "Parede de Fogo" }, { "skill_mana_surge", "Surto de Mana" },
            { "skill_frost_bolt", "Parafuso de Gelo" }, { "skill_ice_shard", "Estilhaço de Gelo" },
            { "skill_blizzard", "Nevasca" }, { "skill_frost_nova", "Nova de Gelo" },
            { "skill_ice_armor", "Armadura de Gelo" }, { "skill_arcane_bolt", "Parafuso Arcano" },
            { "skill_mana_burn", "Queimar Mana" }, { "skill_life_drain", "Drenar Vida" },
            { "skill_arcane_pull", "Puxão Arcano" }, { "skill_purge", "Purificar" },
            { "skill_holy_bolt", "Raio Sagrado" }, { "skill_smite", "Castigo" },
            { "skill_heal", "Curar" }, { "skill_group_heal", "Cura em Grupo" },
            { "skill_resurrection", "Ressurreição" }, { "skill_arcane_shield", "Escudo Arcano" },
            { "skill_mana_amp", "Amplificar Mana" }, { "skill_cc_immune", "Imunidade a CC" },
            { "skill_dodge_passive", "Esquivar" }, { "skill_iron_will", "Vontade de Ferro" },
            { "skill_taunting_cry", "Grito de Provocação" }, { "skill_damage_amp", "Amplificar Dano" },
            { "skill_mana_recovery", "Recuperação de Mana" }, { "skill_evasion", "Evasão" },
            { "skill_relentless", "Implacável" }, { "skill_tough_skin", "Pele Grossa" },
            { "skill_fortified", "Fortalecido" }, { "skill_blink", "Blink" },
            { "skill_self_cleanse", "Purificar-se" }, { "skill_sprint", "Correr" },
            { "skill_rolling_dodge", "Esquiva Rolante" }, { "skill_heavy_step", "Passo Pesado" },
            { "skill_basic_punch", "Soco" },
        };

        /// <summary>Nome amigável de um gearId, ou um fallback legível do próprio id.</summary>
        public static string Display(string gearId)
        {
            if (string.IsNullOrEmpty(gearId)) return "(vazio)";
            return Gear.TryGetValue(gearId, out var n) ? n : Prettify(gearId);
        }

        /// <summary>Nome amigável de um skillId, ou um fallback legível do próprio id.</summary>
        public static string Skill(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return "-";
            return Skills.TryGetValue(skillId, out var n) ? n : Prettify(skillId);
        }

        /// <summary>Infere o slot de equipamento ('weapon'|'chest'|'head'|'boots') de um gearId.</summary>
        public static string SlotOf(string gearId)
        {
            if (string.IsNullOrEmpty(gearId)) return null;
            if (Weapons.Contains(gearId)) return "weapon";
            if (gearId.EndsWith("_chest")) return "chest";
            if (gearId.EndsWith("_hood") || gearId.EndsWith("_cap") || gearId.EndsWith("_helm")) return "head";
            if (gearId.EndsWith("_boots")) return "boots";
            return null;
        }

        private static readonly HashSet<string> Weapons = new()
        {
            "sword", "greataxe", "daggers", "mace", "hammer",
            "bow", "fire_staff", "frost_staff", "arcane_staff", "holy_staff",
        };

        private static string Prettify(string id)
        {
            string s = id.StartsWith("skill_") ? id.Substring(6) : id;
            s = s.Replace('_', ' ');
            return s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
        }
    }
}
