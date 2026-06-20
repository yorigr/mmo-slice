// WorldState.cs
// Mantém o estado do mundo recebido do servidor e gerencia jogadores remotos.
//
// Responsabilidade única:
//   Este script APENAS armazena e atualiza dados — não cria GameObjects.
//   A spawning de objetos visuais para jogadores remotos é responsabilidade
//   do GameManager, que ouve o evento OnWorldUpdated.
//
// Por que Dictionary<string, RemotePlayer> e não List<>?
//   O servidor identifica jogadores por ID string (ex: socket.id).
//   Dictionary permite O(1) lookup/update por ID — essencial pois recebemos
//   world:update 20 vezes por segundo com potencialmente dezenas de jogadores.
//
// Por que JsonUtility e não Newtonsoft.Json?
//   JsonUtility é built-in, zero dependências, zero GC alocações com [Serializable].
//   Para nosso protocolo simples, é suficiente. Newtonsoft seria necessário apenas
//   para tipos avançados (Dictionary, polimorfismo, etc.).

using System;
using System.Collections.Generic;
using UnityEngine;

namespace MMORPG.World
{
    // ─── Estruturas de dados ──────────────────────────────────────────────────────

    /// <summary>
    /// Dados de um jogador remoto, como recebidos do servidor.
    /// Coordenadas em espaço Unity (já convertidas de pixels).
    /// </summary>
    [Serializable]
    public struct RemotePlayer
    {
        public string id;
        public string name;
        public float  x;        // Unidades Unity (= serverX / 50)
        public float  z;        // Unidades Unity (= serverY / 50)
        public int    hp;
        public int    maxHp;
        public int    mana;
        public int    maxMana;
        public int    stamina;
        public int    maxStamina;
        public bool   dead;
        public string className; // "warrior", "mage", etc — para escolher prefab/material
        // Progressão
        public int    level;
        public int    xp;
        public int    xpMax;
        public int    gold;

        // Calculado localmente, não vem do servidor
        [NonSerialized] public float lastUpdateTime; // Time.time da última atualização

        public bool IsAlive => !dead && hp > 0;
        public float HpPercent  => maxHp   > 0 ? (float)hp   / maxHp   : 0f;
        public float ManaPercent => maxMana > 0 ? (float)mana / maxMana : 0f;
        public float XpPercent   => xpMax   > 0 ? (float)xp   / xpMax   : 0f;
    }

    /// <summary>
    /// Dados de um monstro, recebidos em world:update.
    /// Coordenadas em espaço Unity (já convertidas de pixels).
    /// </summary>
    [Serializable]
    public struct RemoteMonster
    {
        public string id;
        public string type;   // "wolf", "goblin", etc
        public float  x;      // Unidades Unity (= serverX / 50)
        public float  z;      // Unidades Unity (= serverY / 50)
        public int    hp;
        public int    maxHp;
    }

    /// <summary>
    /// Item no chão do mundo, recebido em world:update.
    /// Coordenadas em espaço Unity.
    /// </summary>
    [Serializable]
    public struct WorldItem
    {
        public string id;
        public string type; // ex: "potion_small", "sword_rusty"
        public float  x;   // Unidades Unity (= serverX / 50)
        public float  z;   // Unidades Unity (= serverY / 50)
    }

    // ─── MonoBehaviour ────────────────────────────────────────────────────────────

    public class WorldState : MonoBehaviour
    {
        // ─── Estruturas de parsing JSON (espelham o protocolo do servidor) ────────────

        // Formato do payload world:update:
        // { "players": [...], "monsters": [...], "items": [...], "t": 12345 }
        [Serializable]
        private class WorldUpdatePayload
        {
            public PlayerData[]   players;
            public MonsterData[]  monsters;
            public ItemData[]     items;
            public long t; // servidor envia "t" (não "timestamp")
        }

        // Formato de cada item no array:
        // { "id":"item_uuid", "type":"potion_small", "x":1200, "y":900 }
        [Serializable]
        private class ItemData
        {
            public string id;
            public string type;
            public float  x;  // Pixels no servidor
            public float  y;  // Pixels no servidor
        }

        // Formato de cada jogador no array (world:update do mmo-v1):
        // { "id":"abc", "name":"Yuri", "x":1200, "y":900, "hp":100, "maxHp":100,
        //   "mana":80, "maxMana":100, "stamina":100, "maxStamina":100,
        //   "dead":false, "playerClass":"warrior", "level":1, "xp":0, "xpMax":100, "gold":0 }
        [Serializable]
        private class PlayerData
        {
            public string id;
            public string name;
            public float  x;           // Pixels no servidor
            public float  y;           // Pixels no servidor
            public int    hp;
            public int    maxHp;
            public int    mana;
            public int    maxMana;
            public int    stamina;
            public int    maxStamina;
            public bool   dead;
            public string playerClass; // Servidor envia "playerClass" (alias de "class") para compatibilidade C#
            public int    level;
            public int    xp;
            public int    xpMax;
            public int    gold;
        }

        // Formato de cada monstro no array (world:update do mmo-v1):
        // { "id":"m_001", "type":"wolf", "x":1200, "y":900, "hp":80, "maxHp":80 }
        [Serializable]
        private class MonsterData
        {
            public string id;
            public string type;
            public float  x;
            public float  y;
            public int    hp;
            public int    maxHp;
        }

        // ─── Singleton ────────────────────────────────────────────────────────────
        public static WorldState Instance { get; private set; }

        // ─── Estado ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Todos os jogadores ativos, indexados por socket ID.
        /// Inclui o jogador local? Depende do servidor — geralmente sim.
        /// </summary>
        public Dictionary<string, RemotePlayer>  Players  { get; } = new();

        /// <summary>Todos os monstros ativos, indexados por ID.</summary>
        public Dictionary<string, RemoteMonster> Monsters { get; } = new();

        /// <summary>Itens no chão do mundo, indexados por ID.</summary>
        public Dictionary<string, WorldItem> Items { get; } = new();

        /// <summary>Socket ID do jogador local (definido pelo GameManager após join).</summary>
        public string LocalPlayerId { get; set; }

        /// <summary>Timestamp da última atualização recebida (epoch ms do servidor).</summary>
        public long LastServerTimestamp { get; private set; }

        // ─── Eventos ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Disparado após cada world:update processado.
        /// Parâmetro: conjunto de IDs que mudaram (adicionados ou atualizados).
        /// </summary>
        public event Action<HashSet<string>> OnWorldUpdated;

        /// <summary>Disparado quando um jogador remoto entra no mundo.</summary>
        public event Action<string> OnPlayerJoined;

        /// <summary>Disparado quando um jogador remoto sai do mundo.</summary>
        public event Action<string> OnPlayerLeft;

        /// <summary>Disparado quando um jogador morre.</summary>
        public event Action<string> OnPlayerDied;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Processa o payload JSON do evento "world:update" do servidor.
        /// Atualiza o dicionário de jogadores e dispara OnWorldUpdated.
        /// </summary>
        public void UpdateFromServer(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                Debug.LogWarning("[WorldState] world:update com payload vazio.");
                return;
            }

            WorldUpdatePayload payload;
            try
            {
                payload = JsonUtility.FromJson<WorldUpdatePayload>(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WorldState] Falha ao parsear world:update: {ex.Message}\nJSON: {json}");
                return;
            }

            if (payload?.players == null) return;

            LastServerTimestamp = payload.t; // servidor envia "t", não "timestamp"

            // Rastreia IDs presentes nesta atualização para detectar jogadores que saíram
            var updatedIds = new HashSet<string>(payload.players.Length);

            foreach (var data in payload.players)
            {
                if (string.IsNullOrEmpty(data?.id)) continue;

                updatedIds.Add(data.id);
                bool isNew = !Players.ContainsKey(data.id);

                // Converte coordenadas do servidor (pixels) para Unity (unidades)
                var player = new RemotePlayer
                {
                    id             = data.id,
                    name           = data.name ?? "Unknown",
                    x              = data.x / 50f,
                    z              = data.y / 50f,  // servidor Y → Unity Z
                    hp             = data.hp,
                    maxHp          = data.maxHp > 0 ? data.maxHp : 100,
                    mana           = data.mana,
                    maxMana        = data.maxMana > 0 ? data.maxMana : 100,
                    stamina        = data.stamina,
                    maxStamina     = data.maxStamina > 0 ? data.maxStamina : 100,
                    dead           = data.dead,
                    className      = data.playerClass ?? "warrior",
                    level          = data.level > 0 ? data.level : 1,
                    xp             = data.xp,
                    xpMax          = data.xpMax > 0 ? data.xpMax : 100,
                    gold           = data.gold,
                    lastUpdateTime = Time.time
                };

                bool wasDead = Players.TryGetValue(data.id, out RemotePlayer prev) && prev.dead;

                Players[data.id] = player;

                if (isNew)
                    OnPlayerJoined?.Invoke(data.id);

                if (!wasDead && player.dead)
                    OnPlayerDied?.Invoke(data.id);
            }

            // Detecta jogadores que estavam no estado mas não vieram na atualização = saíram
            var toRemove = new List<string>();
            foreach (string existingId in Players.Keys)
            {
                if (!updatedIds.Contains(existingId))
                    toRemove.Add(existingId);
            }

            foreach (string id in toRemove)
            {
                Players.Remove(id);
                OnPlayerLeft?.Invoke(id);
            }

            OnWorldUpdated?.Invoke(updatedIds);

            // ── Processa monstros ─────────────────────────────────────────────────
            if (payload.monsters != null)
            {
                var monsterIds = new HashSet<string>(payload.monsters.Length);

                foreach (var m in payload.monsters)
                {
                    if (string.IsNullOrEmpty(m?.id)) continue;
                    monsterIds.Add(m.id);
                    Monsters[m.id] = new RemoteMonster
                    {
                        id    = m.id,
                        type  = m.type ?? "unknown",
                        x     = m.x / 50f,
                        z     = m.y / 50f,
                        hp    = m.hp,
                        maxHp = m.maxHp > 0 ? m.maxHp : 1,
                    };
                }

                // Remove monstros que sumiram do servidor
                var deadMonsters = new List<string>();
                foreach (string mid in Monsters.Keys)
                    if (!monsterIds.Contains(mid)) deadMonsters.Add(mid);
                foreach (string mid in deadMonsters)
                    Monsters.Remove(mid);
            }

            // ── Processa itens no chão ────────────────────────────────────────────
            if (payload.items != null)
            {
                var itemIds = new HashSet<string>(payload.items.Length);

                foreach (var it in payload.items)
                {
                    if (string.IsNullOrEmpty(it?.id)) continue;
                    itemIds.Add(it.id);
                    Items[it.id] = new WorldItem
                    {
                        id   = it.id,
                        type = it.type ?? "unknown",
                        x    = it.x / 50f,
                        z    = it.y / 50f,
                    };
                }

                // Remove itens que foram coletados ou desapareceram
                var pickedUp = new List<string>();
                foreach (string iid in Items.Keys)
                    if (!itemIds.Contains(iid)) pickedUp.Add(iid);
                foreach (string iid in pickedUp)
                    Items.Remove(iid);
            }
        }

        /// <summary>
        /// Processa "player:joined" — jogador entrou no mundo.
        /// O payload do mmo-v1 tem estrutura: {id, world, abilities, state:{id,name,x,y,...}}
        /// Os dados do jogador estão dentro do objeto "state".
        /// </summary>
        public void HandlePlayerJoined(string json)
        {
            // Parseia o envelope externo para extrair o objeto "state" aninhado
            PlayerJoinedEnvelope envelope;
            try { envelope = JsonUtility.FromJson<PlayerJoinedEnvelope>(json); }
            catch { return; }

            if (envelope == null || string.IsNullOrEmpty(envelope.id)) return;

            // Os dados reais estão em state — fallback para valores do envelope se state não parseou
            var s = envelope.state;
            bool hasState = s != null && (s.x != 0 || s.y != 0);

            var player = new RemotePlayer
            {
                id             = envelope.id,
                name           = hasState ? (s.name ?? "Unknown") : "Unknown",
                x              = hasState ? s.x / 50f : 0f,
                z              = hasState ? s.y / 50f : 0f,
                hp             = hasState ? s.hp : 100,
                maxHp          = hasState && s.maxHp > 0 ? s.maxHp : 100,
                mana           = hasState ? s.mana : 100,
                maxMana        = hasState && s.maxMana > 0 ? s.maxMana : 100,
                stamina        = hasState ? s.stamina : 100,
                maxStamina     = hasState && s.maxStamina > 0 ? s.maxStamina : 100,
                dead           = false,
                className      = hasState ? (s.playerClass ?? "warrior") : "warrior",
                level          = hasState && s.level > 0 ? s.level : 1,
                xp             = hasState ? s.xp : 0,
                xpMax          = hasState && s.xpMax > 0 ? s.xpMax : 100,
                gold           = hasState ? s.gold : 0,
                lastUpdateTime = Time.time
            };

            Players[envelope.id] = player;
            OnPlayerJoined?.Invoke(envelope.id);
        }

        /// <summary>
        /// Processa "player:left" — remove jogador do estado.
        /// json formato: {"id":"abc"}
        /// </summary>
        public void HandlePlayerLeft(string json)
        {
            try
            {
                var data = JsonUtility.FromJson<IdPayload>(json);
                if (data == null || string.IsNullOrEmpty(data.id)) return;

                Players.Remove(data.id);
                OnPlayerLeft?.Invoke(data.id);
            }
            catch { /* payload malformado — ignora */ }
        }

        /// <summary>
        /// Tenta obter os dados do jogador local (posição, HP, recursos, progressão).
        /// Retorna false se LocalPlayerId ainda não foi definido ou não está no estado.
        /// </summary>
        public bool TryGetLocalPlayer(out RemotePlayer player)
        {
            player = default;
            if (string.IsNullOrEmpty(LocalPlayerId)) return false;
            return Players.TryGetValue(LocalPlayerId, out player);
        }

        /// <summary>
        /// Limpa todo o estado do mundo. Chamado ao desconectar — será repopulado
        /// no próximo player:joined / world:update.
        /// </summary>
        public void Clear()
        {
            Players.Clear();
            Monsters.Clear();
            Items.Clear();
            LocalPlayerId = null;
        }

        // ══════════════════════════════════════════════════════════════════════════
        // ESTADO LOCAL COMPLETO (Part 2 — dados que as UIs precisam)
        //
        // Por que separado de RemotePlayer?
        //   RemotePlayer carrega só o que vem em world:update (20Hz, todos os players).
        //   Equipment, skills, durabilidade, inventário e maestria só interessam ao
        //   jogador LOCAL e chegam via player:joined + eventos específicos (gear:equipped,
        //   skill:select_result, mastery:*, repair:result). Mantê-los aqui evita inflar
        //   o payload de world:update com dados irrelevantes para os demais players.
        // ══════════════════════════════════════════════════════════════════════════

        /// <summary>Estado completo do jogador local — alimenta os 4 painéis de UI.</summary>
        public LocalFullState Local { get; } = new LocalFullState();

        /// <summary>
        /// Disparado sempre que o estado local completo muda (gear, skill, durabilidade,
        /// inventário, maestria, recursos). As UIs ouvem isto para se redesenhar.
        /// </summary>
        public event Action OnLocalStateUpdated;

        /// <summary>Notifica as UIs de que o estado local mudou.</summary>
        public void RaiseLocalStateUpdated() => OnLocalStateUpdated?.Invoke();

        /// <summary>
        /// Processa o player:joined COMPLETO para o jogador local: extrai equipment,
        /// skills selecionadas, durabilidade, inventário, maestria e gearOptions.
        /// Chamado pelo GameManager logo após receber player:joined.
        /// </summary>
        public void LoadLocalFullState(string json)
        {
            if (string.IsNullOrEmpty(json)) return;

            LocalJoinEnvelope env;
            try { env = JsonUtility.FromJson<LocalJoinEnvelope>(json); }
            catch { return; }
            if (env == null || env.state == null) return;

            var s = env.state;
            Local.id              = s.id;
            Local.name            = s.name;
            Local.level           = s.level;
            Local.xp              = s.xp;
            Local.xpMax           = s.xpMax;
            Local.gold            = s.gold;
            Local.hp              = s.hp;       Local.maxHp      = s.maxHp;
            Local.mana            = s.mana;     Local.maxMana    = s.maxMana;
            Local.stamina         = s.stamina;  Local.maxStamina = s.maxStamina;
            Local.speed           = s.speed;
            Local.dodgeChance     = s.dodgeChance + s.masteryDodgeBonus;
            Local.damageReduction = s.damageReduction;
            Local.equipment       = s.equipment      ?? new Equipment();
            Local.selectedSkills  = s.selectedSkills ?? new SelectedSkills();
            Local.durability      = s.durability     ?? new Durability();
            Local.inventory       = s.inventory      ?? Array.Empty<InventoryItem>();
            Local.gatheringSkills = s.gatheringSkills ?? new GatheringSkills();
            Local.craftingSkills  = s.craftingSkills  ?? new CraftingSkills();
            Local.masteryEntries  = MasteryParser.Parse(json);
            Local.gearOptions     = GearOptionsParser.Parse(json);

            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica gear:equipped — atualiza equipment/durabilidade do slot.</summary>
        public void HandleGearEquipped(string json)
        {
            var d = SafeParse<GearEquippedPayload>(json);
            if (d == null || string.IsNullOrEmpty(d.slot)) return;
            Local.SetEquip(d.slot, d.gearId);
            Local.SetDurability(d.slot, 100);
            // Skills do slot foram resetadas no servidor para a 1ª opção; redesenha.
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica gear:unequipped — limpa o slot e suas skills.</summary>
        public void HandleGearUnequipped(string json)
        {
            var d = SafeParse<GearEquippedPayload>(json);
            if (d == null || string.IsNullOrEmpty(d.slot)) return;
            Local.SetEquip(d.slot, null);
            Local.ClearSkills(d.slot);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica skill:select_result — confirma a skill ativa em um slotKey.</summary>
        public void HandleSkillSelectResult(string json)
        {
            var d = SafeParse<SkillSelectPayload>(json);
            if (d == null || string.IsNullOrEmpty(d.slotKey)) return;
            // Só aplica se o servidor confirmou (sem campo "error").
            if (!string.IsNullOrEmpty(d.error)) return;
            Local.SetSkill(d.slotKey, d.skillId);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica inventory:updated — substitui o inventário local.</summary>
        public void HandleInventoryUpdated(string json)
        {
            var d = SafeParse<InventoryUpdatedPayload>(json);
            if (d == null) return;
            Local.inventory = d.inventory ?? Array.Empty<InventoryItem>();
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica repair:result — durabilidade dos slots reparados volta a 100.</summary>
        public void HandleRepairResult(string json)
        {
            var d = SafeParse<RepairResultPayload>(json);
            if (d == null || !d.ok) return;
            Local.gold = d.gold;
            if (d.repairs != null)
                foreach (var r in d.repairs)
                    Local.SetDurability(r.slot, 100);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica mastery:xp — progresso de XP de maestria de uma peça.</summary>
        public void HandleMasteryXp(string json)
        {
            var d = SafeParse<MasteryXpPayload>(json);
            if (d == null || string.IsNullOrEmpty(d.gearId)) return;
            Local.UpdateMastery(d.gearId, d.level, d.xp, d.xpMax, null, null);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica mastery:levelup — sobe o nível de maestria de uma peça.</summary>
        public void HandleMasteryLevelUp(string json)
        {
            var d = SafeParse<MasteryXpPayload>(json);
            if (d == null || string.IsNullOrEmpty(d.gearId)) return;
            Local.UpdateMastery(d.gearId, d.level, d.xp, d.xpMax, null, null);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica mastery:yellow_fame — XP excedente vira Fama Amarela pendente.</summary>
        public void HandleMasteryYellowFame(string json)
        {
            var d = SafeParse<MasteryYellowFamePayload>(json);
            if (d == null || string.IsNullOrEmpty(d.gearId)) return;
            Local.UpdateMastery(d.gearId, null, null, null, d.pending, d.level);
            RaiseLocalStateUpdated();
        }

        /// <summary>Aplica mastery:convert_result — Fama Amarela convertida em nível.</summary>
        public void HandleMasteryConvertResult(string json)
        {
            var d = SafeParse<MasteryConvertPayload>(json);
            if (d == null || !d.ok || string.IsNullOrEmpty(d.gearId)) return;
            Local.gold = d.gold;
            Local.UpdateMastery(d.gearId, null, null, null, d.pending, d.yellowFameLevel);
            RaiseLocalStateUpdated();
        }

        private static T SafeParse<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch { return null; }
        }

        // ─── Estruturas de parsing aninhadas ──────────────────────────────────────

        [Serializable]
        private class PlayerJoinedEnvelope
        {
            public string id;
            public PlayerJoinedState state;
        }

        [Serializable]
        private class PlayerJoinedState
        {
            public string name;
            public float  x;
            public float  y;
            public int    hp;
            public int    maxHp;
            public int    mana;
            public int    maxMana;
            public int    stamina;
            public int    maxStamina;
            public string playerClass;
            public int    level;
            public int    xp;
            public int    xpMax;
            public int    gold;
        }

        [Serializable]
        private class IdPayload
        {
            public string id;
        }

        // Envelope para LoadLocalFullState — estado completo do jogador local.
        [Serializable]
        private class LocalJoinEnvelope
        {
            public LocalStateRaw state;
        }

        [Serializable]
        private class LocalStateRaw
        {
            public string id;
            public string name;
            public int    level;
            public int    xp;
            public int    xpMax;
            public int    gold;
            public int    hp;
            public int    maxHp;
            public int    mana;
            public int    maxMana;
            public int    stamina;
            public int    maxStamina;
            public int    speed;
            public float  dodgeChance;
            public float  masteryDodgeBonus;
            public float  damageReduction;
            public Equipment       equipment;
            public SelectedSkills  selectedSkills;
            public Durability      durability;
            public InventoryItem[] inventory;
            public GatheringSkills gatheringSkills;
            public CraftingSkills  craftingSkills;
        }

        // ─── Payloads de eventos específicos ──────────────────────────────────────
        [Serializable] private class GearEquippedPayload      { public string slot; public string gearId; }
        [Serializable] private class SkillSelectPayload       { public string slotKey; public string skillId; public string error; }
        [Serializable] private class InventoryUpdatedPayload  { public InventoryItem[] inventory; }
        [Serializable] private class RepairResultPayload      { public bool ok; public int totalCost; public int gold; public RepairEntry[] repairs; }
        [Serializable] private class RepairEntry              { public string slot; public string gearId; public int cost; }
        [Serializable] private class MasteryXpPayload         { public string gearId; public int level; public int xp; public int xpMax; }
        [Serializable] private class MasteryYellowFamePayload { public string gearId; public int pending; public int level; }
        [Serializable] private class MasteryConvertPayload    { public bool ok; public string gearId; public int yellowFameLevel; public int gold; public int pending; }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // ESTRUTURAS DE ESTADO LOCAL
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Equipamento do jogador, keyed por slot. "" = vazio.</summary>
    [Serializable]
    public class Equipment
    {
        public string weapon;
        public string chest;
        public string head;
        public string boots;
    }

    /// <summary>Skill selecionada em cada slot de gear. null/"" = vazio.</summary>
    [Serializable]
    public class SelectedSkills
    {
        public string weapon_Q;
        public string weapon_W;
        public string weapon_E;
        public string chest_R;
        public string head_D;
        public string boots_F;
    }

    /// <summary>Durabilidade (0–100) por slot de equipamento.</summary>
    [Serializable]
    public class Durability
    {
        public int weapon = 100;
        public int chest  = 100;
        public int head   = 100;
        public int boots  = 100;
    }

    /// <summary>Item de inventário (id único + tipo de gear/material).</summary>
    [Serializable]
    public class InventoryItem
    {
        public string id;
        public string type;
    }

    /// <summary>Níveis de gathering (cada um é um objeto { level, xp, xpMax }).</summary>
    [Serializable]
    public class GatheringSkills
    {
        public SkillProgress mining;
        public SkillProgress woodcutting;
        public SkillProgress herbalism;
        public SkillProgress hunting;
        public SkillProgress fishing;
    }

    /// <summary>Níveis de crafting (cada um é um objeto { level, xp, xpMax }).</summary>
    [Serializable]
    public class CraftingSkills
    {
        public SkillProgress smithing;
        public SkillProgress leatherwork;
        public SkillProgress alchemy;
        public SkillProgress fletching;
        public SkillProgress runecrafting;
    }

    [Serializable]
    public class SkillProgress
    {
        public int level = 1;
        public int xp;
        public int xpMax;
    }

    /// <summary>
    /// Entrada de maestria por gearId. JsonUtility não suporta Dictionary, então
    /// guardamos um array paralelo de structs e fazemos lookup manual por gearId.
    /// </summary>
    [Serializable]
    public class MasteryEntry
    {
        public string gearId;
        public int    level = 1;
        public int    xp;
        public int    xpMax;
        public int    yfPending; // Fama Amarela pendente
        public int    yfLevel;   // nível de Fama Amarela já convertido
    }

    /// <summary>
    /// Opções de skill por gearId (vem de player:joined.gearOptions). Cada peça
    /// expõe os slotKeys que controla e as skills disponíveis em cada um.
    /// </summary>
    [Serializable]
    public class GearOption
    {
        public string   gearId;
        public string   slotKey; // ex: "weapon_Q", "chest_R"
        public string[] options; // skillIds disponíveis nesse slot
    }

    /// <summary>Estado completo do jogador local. Mutado pelos handlers do WorldState.</summary>
    public class LocalFullState
    {
        public string id;
        public string name;
        public int    level = 1, xp, xpMax = 100, gold;
        public int    hp = 100, maxHp = 100;
        public int    mana = 100, maxMana = 100;
        public int    stamina = 100, maxStamina = 100;
        public int    speed;
        public float  dodgeChance;
        public float  damageReduction;

        public Equipment       equipment       = new Equipment();
        public SelectedSkills  selectedSkills  = new SelectedSkills();
        public Durability      durability       = new Durability();
        public InventoryItem[] inventory        = Array.Empty<InventoryItem>();
        public GatheringSkills gatheringSkills  = new GatheringSkills();
        public CraftingSkills  craftingSkills   = new CraftingSkills();
        public MasteryEntry[]  masteryEntries   = Array.Empty<MasteryEntry>();
        public GearOption[]    gearOptions      = Array.Empty<GearOption>();

        // ─── Helpers de mutação ──────────────────────────────────────────────────
        public string GetEquip(string slot) => slot switch
        {
            "weapon" => equipment.weapon,
            "chest"  => equipment.chest,
            "head"   => equipment.head,
            "boots"  => equipment.boots,
            _        => null,
        };

        public void SetEquip(string slot, string gearId)
        {
            switch (slot)
            {
                case "weapon": equipment.weapon = gearId; break;
                case "chest":  equipment.chest  = gearId; break;
                case "head":   equipment.head   = gearId; break;
                case "boots":  equipment.boots  = gearId; break;
            }
        }

        public int GetDurability(string slot) => slot switch
        {
            "weapon" => durability.weapon,
            "chest"  => durability.chest,
            "head"   => durability.head,
            "boots"  => durability.boots,
            _        => 100,
        };

        public void SetDurability(string slot, int value)
        {
            switch (slot)
            {
                case "weapon": durability.weapon = value; break;
                case "chest":  durability.chest  = value; break;
                case "head":   durability.head   = value; break;
                case "boots":  durability.boots  = value; break;
            }
        }

        public string GetSkill(string slotKey) => slotKey switch
        {
            "weapon_Q" => selectedSkills.weapon_Q,
            "weapon_W" => selectedSkills.weapon_W,
            "weapon_E" => selectedSkills.weapon_E,
            "chest_R"  => selectedSkills.chest_R,
            "head_D"   => selectedSkills.head_D,
            "boots_F"  => selectedSkills.boots_F,
            _          => null,
        };

        public void SetSkill(string slotKey, string skillId)
        {
            switch (slotKey)
            {
                case "weapon_Q": selectedSkills.weapon_Q = skillId; break;
                case "weapon_W": selectedSkills.weapon_W = skillId; break;
                case "weapon_E": selectedSkills.weapon_E = skillId; break;
                case "chest_R":  selectedSkills.chest_R  = skillId; break;
                case "head_D":   selectedSkills.head_D   = skillId; break;
                case "boots_F":  selectedSkills.boots_F  = skillId; break;
            }
        }

        /// <summary>Limpa as skills de um slot de equipamento (ao desequipar).</summary>
        public void ClearSkills(string slot)
        {
            if (slot == "weapon")
            {
                selectedSkills.weapon_Q = null;
                selectedSkills.weapon_W = null;
                selectedSkills.weapon_E = null;
            }
            else if (slot == "chest") selectedSkills.chest_R = null;
            else if (slot == "head")  selectedSkills.head_D  = null;
            else if (slot == "boots") selectedSkills.boots_F = null;
        }

        /// <summary>Lookup de maestria por gearId (null se não existir).</summary>
        public MasteryEntry GetMastery(string gearId)
        {
            if (masteryEntries == null) return null;
            foreach (var m in masteryEntries)
                if (m != null && m.gearId == gearId) return m;
            return null;
        }

        /// <summary>
        /// Atualiza (ou cria) a entrada de maestria de um gearId. Parâmetros null
        /// são ignorados (atualização parcial) — útil pois mastery:xp e
        /// mastery:yellow_fame trazem campos diferentes.
        /// </summary>
        public void UpdateMastery(string gearId, int? level, int? xp, int? xpMax, int? yfPending, int? yfLevel)
        {
            var m = GetMastery(gearId);
            if (m == null)
            {
                m = new MasteryEntry { gearId = gearId };
                var list = new System.Collections.Generic.List<MasteryEntry>(
                    masteryEntries ?? Array.Empty<MasteryEntry>());
                list.Add(m);
                masteryEntries = list.ToArray();
            }
            if (level.HasValue)     m.level     = level.Value;
            if (xp.HasValue)        m.xp        = xp.Value;
            if (xpMax.HasValue)     m.xpMax     = xpMax.Value;
            if (yfPending.HasValue) m.yfPending = yfPending.Value;
            if (yfLevel.HasValue)   m.yfLevel   = yfLevel.Value;
        }

        /// <summary>Lista os GearOptions de um slotKey específico (vazio se nenhum).</summary>
        public string[] OptionsForSlot(string gearId, string slotKey)
        {
            if (gearOptions == null) return Array.Empty<string>();
            foreach (var o in gearOptions)
                if (o != null && o.gearId == gearId && o.slotKey == slotKey)
                    return o.options ?? Array.Empty<string>();
            return Array.Empty<string>();
        }
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // PARSERS para tipos que JsonUtility não consegue mapear diretamente
    // (objetos com chaves dinâmicas — equipmentMastery e gearOptions são keyed por
    // gearId, então JsonUtility não os enxerga). Fazemos parsing leve por regex/string.
    // ══════════════════════════════════════════════════════════════════════════════

    /// <summary>Extrai equipmentMastery (keyed por gearId) em MasteryEntry[].</summary>
    internal static class MasteryParser
    {
        public static MasteryEntry[] Parse(string json)
        {
            var result = new System.Collections.Generic.List<MasteryEntry>();
            if (string.IsNullOrEmpty(json)) return result.ToArray();

            int key = json.IndexOf("\"equipmentMastery\"", StringComparison.Ordinal);
            if (key < 0) return result.ToArray();

            int braceStart = json.IndexOf('{', key);
            if (braceStart < 0) return result.ToArray();

            // Encontra o fim do objeto equipmentMastery balanceando chaves.
            int depth = 0, end = braceStart;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            string block = json.Substring(braceStart, end - braceStart + 1);

            // Itera entradas "gearId": { ... }
            int idx = 0;
            while (true)
            {
                int q1 = block.IndexOf('"', idx);
                if (q1 < 0) break;
                int q2 = block.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                string gearId = block.Substring(q1 + 1, q2 - q1 - 1);

                int objStart = block.IndexOf('{', q2);
                if (objStart < 0) break;
                int d = 0, objEnd = objStart;
                for (int i = objStart; i < block.Length; i++)
                {
                    if (block[i] == '{') d++;
                    else if (block[i] == '}') { d--; if (d == 0) { objEnd = i; break; } }
                }
                string obj = block.Substring(objStart, objEnd - objStart + 1);

                var e = new MasteryEntry { gearId = gearId };
                e.level     = ReadInt(obj, "level", 1);
                e.xp        = ReadInt(obj, "xp", 0);
                e.xpMax     = ReadInt(obj, "xpMax", 0);
                // yellowFame é um sub-objeto { pending, level }
                int yfKey = obj.IndexOf("\"yellowFame\"", StringComparison.Ordinal);
                if (yfKey >= 0)
                {
                    int yfStart = obj.IndexOf('{', yfKey);
                    int yd = 0, yfEnd = yfStart;
                    for (int i = yfStart; i < obj.Length && yfStart >= 0; i++)
                    {
                        if (obj[i] == '{') yd++;
                        else if (obj[i] == '}') { yd--; if (yd == 0) { yfEnd = i; break; } }
                    }
                    if (yfStart >= 0)
                    {
                        string yf = obj.Substring(yfStart, yfEnd - yfStart + 1);
                        e.yfPending = ReadInt(yf, "pending", 0);
                        e.yfLevel   = ReadInt(yf, "level", 0);
                    }
                }
                result.Add(e);
                idx = objEnd + 1;
            }
            return result.ToArray();
        }

        private static int ReadInt(string s, string field, int dflt)
        {
            int k = s.IndexOf("\"" + field + "\"", StringComparison.Ordinal);
            if (k < 0) return dflt;
            int colon = s.IndexOf(':', k);
            if (colon < 0) return dflt;
            int i = colon + 1;
            while (i < s.Length && (s[i] == ' ' || s[i] == '\t')) i++;
            int start = i;
            while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '-')) i++;
            if (i == start) return dflt;
            return int.TryParse(s.Substring(start, i - start), out int v) ? v : dflt;
        }
    }

    /// <summary>
    /// Extrai gearOptions (keyed por gearId) em GearOption[] achatado por slotKey.
    /// Formato origem: { "sword": { "Q":[...], "W":[...], "E":[...] }, "cloth_chest": { "R":[...] } }
    /// Convertemos as letras de slot (Q/W/E/R/D/F) em slotKeys completos (weapon_Q, chest_R, ...).
    /// </summary>
    internal static class GearOptionsParser
    {
        public static GearOption[] Parse(string json)
        {
            var result = new System.Collections.Generic.List<GearOption>();
            if (string.IsNullOrEmpty(json)) return result.ToArray();

            int key = json.IndexOf("\"gearOptions\"", StringComparison.Ordinal);
            if (key < 0) return result.ToArray();
            int braceStart = json.IndexOf('{', key);
            if (braceStart < 0) return result.ToArray();
            int depth = 0, end = braceStart;
            for (int i = braceStart; i < json.Length; i++)
            {
                if (json[i] == '{') depth++;
                else if (json[i] == '}') { depth--; if (depth == 0) { end = i; break; } }
            }
            string block = json.Substring(braceStart, end - braceStart + 1);

            int idx = 1; // pula o '{' inicial
            while (true)
            {
                int q1 = block.IndexOf('"', idx);
                if (q1 < 0 || q1 >= end) break;
                int q2 = block.IndexOf('"', q1 + 1);
                if (q2 < 0) break;
                string gearId = block.Substring(q1 + 1, q2 - q1 - 1);

                int objStart = block.IndexOf('{', q2);
                if (objStart < 0) break;
                int d = 0, objEnd = objStart;
                for (int i = objStart; i < block.Length; i++)
                {
                    if (block[i] == '{') d++;
                    else if (block[i] == '}') { d--; if (d == 0) { objEnd = i; break; } }
                }
                string obj = block.Substring(objStart, objEnd - objStart + 1);

                // Para cada letra de slot dentro deste gear, lê o array de opções.
                foreach (var letter in new[] { "Q", "W", "E", "R", "D", "F" })
                {
                    int lk = obj.IndexOf("\"" + letter + "\"", StringComparison.Ordinal);
                    if (lk < 0) continue;
                    int arrStart = obj.IndexOf('[', lk);
                    int arrEnd   = obj.IndexOf(']', arrStart < 0 ? lk : arrStart);
                    if (arrStart < 0 || arrEnd < 0) continue;
                    string arr = obj.Substring(arrStart + 1, arrEnd - arrStart - 1);

                    var opts = new System.Collections.Generic.List<string>();
                    foreach (var part in arr.Split(','))
                    {
                        string v = part.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(v)) opts.Add(v);
                    }

                    result.Add(new GearOption
                    {
                        gearId  = gearId,
                        slotKey = ToSlotKey(letter),
                        options = opts.ToArray(),
                    });
                }
                idx = objEnd + 1;
            }
            return result.ToArray();
        }

        private static string ToSlotKey(string letter) => letter switch
        {
            "Q" => "weapon_Q",
            "W" => "weapon_W",
            "E" => "weapon_E",
            "R" => "chest_R",
            "D" => "head_D",
            "F" => "boots_F",
            _   => letter,
        };
    }
}
