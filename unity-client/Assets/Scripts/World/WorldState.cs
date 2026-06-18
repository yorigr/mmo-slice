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
        public bool   dead;
        public string className; // "warrior", "mage", etc — para escolher prefab/material

        // Calculado localmente, não vem do servidor
        [NonSerialized] public float lastUpdateTime; // Time.time da última atualização

        public bool IsAlive => !dead && hp > 0;
        public float HpPercent => maxHp > 0 ? (float)hp / maxHp : 0f;
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

    // ─── MonoBehaviour ────────────────────────────────────────────────────────────

    public class WorldState : MonoBehaviour
    {
        // ─── Estruturas de parsing JSON (espelham o protocolo do servidor) ────────────

        // Formato do payload world:update:
        // { "players": [...], "t": 12345 }
        [Serializable]
        private class WorldUpdatePayload
        {
            public PlayerData[]   players;
            public MonsterData[]  monsters;
            public long t; // servidor envia "t" (não "timestamp")
        }

        // Formato de cada jogador no array (world:update do mmo-v1):
        // { "id":"abc", "name":"Yuri", "x":1200, "y":900, "hp":100, "maxHp":100,
        //   "dead":false, "class":"warrior", "playerClass":"warrior" }
        // Nota: o servidor envia AMBOS "class" e "playerClass" — usamos "playerClass"
        // porque "class" é palavra reservada em C# e JsonUtility não aceita o [FormerlySerializedAs]
        // para esse caso (esse atributo funciona apenas com serialização de assets Unity, não JSON).
        [Serializable]
        private class PlayerData
        {
            public string id;
            public string name;
            public float  x;           // Pixels no servidor
            public float  y;           // Pixels no servidor
            public int    hp;
            public int    maxHp;
            public bool   dead;
            public string playerClass; // Servidor envia "playerClass" (alias de "class") para compatibilidade C#
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
                    dead           = data.dead,
                    className      = data.playerClass ?? "warrior",
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
                dead           = false,
                className      = hasState ? (s.playerClass ?? "warrior") : "warrior",
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
            catch (Exception ex)
            {
                Debug.LogWarning($"[WorldState] Erro ao processar player:left: {ex.Message}");
            }
        }

        /// <summary>Limpa todo o estado (ex: ao desconectar).</summary>
        public void Clear()
        {
            Players.Clear();
            LocalPlayerId = null;
            LastServerTimestamp = 0;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>Retorna dados do jogador local, se disponível.</summary>
        public bool TryGetLocalPlayer(out RemotePlayer player)
        {
            if (!string.IsNullOrEmpty(LocalPlayerId) && Players.TryGetValue(LocalPlayerId, out player))
                return true;

            player = default;
            return false;
        }

        // Estrutura auxiliar para parsear payloads com apenas "id"
        [Serializable]
        private class IdPayload { public string id; }

        // Envelope do evento "player:joined" do mmo-v1:
        // { "id":"...", "world":{...}, "abilities":{...}, "state":{id,name,x,y,hp,maxHp,...} }
        [Serializable]
        private class PlayerJoinedEnvelope
        {
            public string id;
            public PlayerJoinedState state;
        }

        // Dados do jogador dentro do envelope "player:joined"
        [Serializable]
        private class PlayerJoinedState
        {
            public string id;
            public string name;
            public float  x;
            public float  y;
            public int    hp;
            public int    maxHp;
            public string playerClass; // servidor envia "playerClass" como alias de "class"
        }
    }
}
