// GameManager.cs
// Orquestrador principal do jogo. Coordena a sequência de inicialização e mantém
// referências cruzadas entre os sistemas.
//
// Fluxo de inicialização:
//   Awake → Connect → [OnConnected] → SendJoin → [player:joined] → SpawnPlayer → StartGame
//
// Por que um GameManager separado e não colocar lógica no NetworkManager?
//   Single Responsibility Principle: NetworkManager sabe COMO falar com o servidor,
//   GameManager sabe O QUE fazer com as respostas. Separar facilita testar e modificar
//   cada sistema de forma independente.
//
// Sobre spawning de jogadores remotos:
//   Quando outros jogadores entram (OnPlayerJoined), GameManager cria seus GameObjects.
//   Quando saem (OnPlayerLeft), GameManager os destrói.
//   WorldState apenas mantém os dados — não cria objetos visuais.

using System.Collections.Generic;
using UnityEngine;
using MMORPG.Network;
using MMORPG.Player;
using MMORPG.World;
using MMORPG.UI;
// GroundSampler está em MMORPG.World — já importado acima
// StickManBuilder e ItemWorldController estão em MMORPG

namespace MMORPG
{
    public class GameManager : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Prefabs")]
        [Tooltip("Prefab do jogador local. Deve ter PlayerController e Rigidbody.")]
        [SerializeField] private GameObject playerPrefab;

        [Tooltip("Prefab para jogadores remotos (sem PlayerController — controlados pelo servidor).")]
        [SerializeField] private GameObject remotePlayerPrefab;

        [Header("Configuração do jogador")]
        [SerializeField] private string playerName  = "Hero";
        [SerializeField] private string playerClass = "warrior";

        [Header("Referências de cena")]
        [SerializeField] private CameraController      cameraController;
        [SerializeField] private HUD                   hud;
        [SerializeField] private SkillBar              skillBar;          // Opcional: auto-cria se nulo
        [SerializeField] private ItemWorldController   itemController;    // Opcional: auto-cria se nulo
        [SerializeField] private RespawnPanel          respawnPanel;      // Opcional: auto-cria se nulo
        [SerializeField] private ChatUI                chatUI;            // Opcional: auto-cria se nulo
        [SerializeField] private UIManager             uiManager;         // Opcional: auto-cria se nulo (painéis P/C/K/I)

        // ─── Estado interno ───────────────────────────────────────────────────────
        private NetworkManager _net;
        private WorldState     _world;

        // Instância do GameObject do jogador local
        private GameObject _localPlayerGO;
        private PlayerController _localPlayerCtrl;

        // GameObjects dos jogadores remotos, indexados por ID
        private readonly Dictionary<string, GameObject> _remotePlayerObjects = new();

        // Estado do jogo
        private bool _gameStarted;

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

        private void Start()
        {
            _net   = NetworkManager.Instance;
            _world = WorldState.Instance;

            if (_net == null)
            {
                Debug.LogError("[GameManager] NetworkManager não encontrado! Adicione à cena.");
                return;
            }

            // Registra callbacks de rede
            _net.OnConnected    += HandleConnected;
            _net.OnDisconnected += HandleDisconnected;

            // Registra eventos do servidor
            _net.OnEvent["player:joined"]    = HandlePlayerJoined;
            _net.OnEvent["player:left"]      = HandlePlayerLeft;
            _net.OnEvent["world:update"]     = HandleWorldUpdate;
            _net.OnEvent["pong_rtt"]         = HandlePong;
            _net.OnEvent["player:died"]      = HandlePlayerDied;
            _net.OnEvent["player:xp"]        = HandlePlayerXp;
            _net.OnEvent["player:levelup"]   = HandlePlayerLevelUp;
            _net.OnEvent["player:revived"]   = HandlePlayerRevived;
            _net.OnEvent["skill:result"]     = HandleSkillResult;
            _net.OnEvent["item:picked"]      = HandleItemPicked;
            // Servidor envia combate como arrays por tick (evita N eventos por frame)
            _net.OnEvent["combat:hits"]      = HandleCombatHits;
            _net.OnEvent["combat:deaths"]    = HandleCombatDeaths;

            // Eventos de gear / skills / inventário / maestria — alimentam os 4 painéis de UI.
            // Mantêm WorldState.Local sincronizado e disparam OnLocalStateUpdated.
            _net.OnEvent["gear:equipped"]        = HandleGearEquipped;
            _net.OnEvent["gear:unequipped"]      = HandleGearUnequipped;
            _net.OnEvent["skill:select_result"]  = HandleSkillSelectResult;
            _net.OnEvent["inventory:updated"]    = HandleInventoryUpdated;
            _net.OnEvent["repair:result"]        = HandleRepairResult;
            _net.OnEvent["mastery:xp"]           = HandleMasteryXp;
            _net.OnEvent["mastery:levelup"]      = HandleMasteryLevelUp;
            _net.OnEvent["mastery:yellow_fame"]  = HandleMasteryYellowFame;
            _net.OnEvent["mastery:convert_result"] = HandleMasteryConvertResult;

            // Registra callbacks do WorldState para spawning de remotos
            if (_world != null)
            {
                _world.OnPlayerJoined += SpawnRemotePlayer;
                _world.OnPlayerLeft   += DespawnRemotePlayer;
            }

            // Cria componentes de UI se não atribuídos no Inspector
            if (skillBar == null)
                skillBar = gameObject.AddComponent<SkillBar>();

            if (itemController == null)
                itemController = gameObject.AddComponent<ItemWorldController>();

            if (respawnPanel == null)
                respawnPanel = gameObject.AddComponent<RespawnPanel>();

            if (chatUI == null)
                chatUI = gameObject.AddComponent<ChatUI>();

            // UIManager: orquestra os 4 painéis (Status/PaperDoll/SkillTree/Inventory).
            // Auto-criado como GameObject persistente — zero configuração no Editor.
            if (uiManager == null)
            {
                var uiGo = new GameObject("UIManager");
                uiManager = uiGo.AddComponent<UIManager>();
            }

            // ChatUI precisa registrar o evento chat:message assim que NetworkManager conectar.
            // Registramos aqui (antes de Connect()) porque o NetworkManager pode já estar pronto.
            if (chatUI != null && _net != null)
                chatUI.Register(_net);

            // Inicia conexão
            _net.Connect();
        }

        private void Update()
        {
            if (!_gameStarted) return;

            // Sincroniza posição de jogadores remotos com o WorldState
            // Fazemos isso em Update para suavizar o movimento entre os world:updates (20Hz)
            SyncRemotePlayers();
        }

        private void OnDestroy()
        {
            // Remove listeners para evitar chamadas após destruição
            if (_net != null)
            {
                _net.OnConnected    -= HandleConnected;
                _net.OnDisconnected -= HandleDisconnected;
            }

            if (_world != null)
            {
                _world.OnPlayerJoined -= SpawnRemotePlayer;
                _world.OnPlayerLeft   -= DespawnRemotePlayer;
            }
        }

        private void OnApplicationQuit()
        {
            // Fecha a conexão limpa antes de encerrar o processo
            // Sem isso, o servidor pode levar até o timeout (30s+) para detectar a saída
            _net?.Disconnect();
        }

        // ─── Handlers de conexão ──────────────────────────────────────────────────
        private void HandleConnected()
        {
            Debug.Log("[GameManager] Conectado ao servidor. Enviando player:join...");

            // Servidor lê "playerClass" (não "class" — palavra reservada no protocolo v1)
            string json = $"{{\"name\":\"{playerName}\",\"playerClass\":\"{playerClass}\"}}";
            _net.Emit("player:join", json);
        }

        private void HandleDisconnected()
        {
            Debug.Log("[GameManager] Desconectado do servidor.");
            _gameStarted = false;

            // Limpa o estado do mundo — será repopulado ao reconectar
            _world?.Clear();

            // Destrói todos os remotos (o jogador local permanece, pois vai reconectar)
            foreach (var go in _remotePlayerObjects.Values)
                if (go != null) Destroy(go);
            _remotePlayerObjects.Clear();
        }

        // ─── Handlers de eventos do servidor ─────────────────────────────────────
        private void HandlePlayerJoined(string json)
        {
            // Payload: { id, sessionToken, world, abilities:[...], gearOptions, state:{x,y,name,hp,...} }
            _world?.HandlePlayerJoined(json);

            var idPayload = JsonUtility.FromJson<IdExtract>(json);
            if (idPayload == null || string.IsNullOrEmpty(idPayload.id)) return;

            // Se ainda não spawnamos o jogador local, este evento é nosso
            if (string.IsNullOrEmpty(_world?.LocalPlayerId))
            {
                AssignLocalPlayer(idPayload.id, json);

                // Carrega o estado local COMPLETO (equipment, skills, durabilidade,
                // inventário, maestria, gearOptions) para alimentar os painéis de UI.
                _world?.LoadLocalFullState(json);
            }
        }

        private void HandlePlayerLeft(string json)
        {
            _world?.HandlePlayerLeft(json);
        }

        private void HandleWorldUpdate(string json)
        {
            _world?.UpdateFromServer(json);

            // Reconcilia posição e atualiza HUD com dados autoritativos do servidor
            if (_localPlayerCtrl != null && _world != null && _world.TryGetLocalPlayer(out var localData))
            {
                float serverX = localData.x * 50f;
                float serverY = localData.z * 50f;
                _localPlayerCtrl.ApplyServerPosition(serverX, serverY);

                // HUD atualizado com todos os campos de progressão
                hud?.SetHP(localData.hp, localData.maxHp);
                hud?.SetMana(localData.mana, localData.maxMana);
                hud?.SetXP(localData.xp, localData.xpMax);
                hud?.SetGold(localData.gold);
                hud?.SetLevel(localData.level > 0 ? localData.level : 1);
            }
        }

        private void HandlePong(string json)
        {
            // Resposta ao nosso ping customizado — atualiza medição de RTT
            _net?.RegisterPong();
        }

        private void HandlePlayerDied(string json)
        {
            var idPayload = JsonUtility.FromJson<IdExtract>(json);
            if (idPayload == null) return;

            Debug.Log($"[GameManager] Jogador morreu: {idPayload.id}");

            // Mostra tela de respawn apenas para o jogador local
            if (idPayload.id == _world?.LocalPlayerId)
                respawnPanel?.Show();
        }

        private void HandlePlayerXp(string json)
        {
            // Payload: { xp, gold, totalXp, totalGold, xpMax }
            var data = JsonUtility.FromJson<PlayerXpData>(json);
            if (data == null) return;

            Debug.Log($"[GameManager] +{data.xp} XP, +{data.gold} gold");

            hud?.SetXP(data.totalXp, data.xpMax);
            hud?.SetGold(data.totalGold);

            // Mostra XP e gold ganhos acima do jogador local
            if (_localPlayerGO != null)
            {
                Vector3 pos = _localPlayerGO.transform.position;
                if (data.xp > 0)
                    FloatingText.Spawn(pos + Vector3.up * 0.3f, $"+{data.xp} XP", Color.cyan);
                if (data.gold > 0)
                    FloatingText.Spawn(pos + Vector3.up * 0.6f, $"+{data.gold} G", new Color(1f, 0.85f, 0f));
            }
        }

        private void HandlePlayerLevelUp(string json)
        {
            // Payload: { level, maxHp, maxMana, speed, xp, xpMax }
            var data = JsonUtility.FromJson<PlayerLevelUpData>(json);
            if (data == null) return;

            Debug.Log($"[GameManager] LEVEL UP! Agora Lv{data.level} (HP:{data.maxHp}, Mana:{data.maxMana})");

            hud?.SetLevel(data.level);
            hud?.SetHP(data.maxHp, data.maxHp);   // full heal no level up
            hud?.SetMana(data.maxMana, data.maxMana);
            hud?.SetXP(data.xp, data.xpMax);

            // TODO Phase 3: Tocar efeito visual/sonoro de level up
        }

        private void HandlePlayerRevived(string json)
        {
            var data = JsonUtility.FromJson<PlayerRevivedData>(json);
            Debug.Log($"[GameManager] Ressuscitado! HP: {(data != null ? data.hp : 0)}");
            // Esconde a tela de respawn — world:update vai refletir o novo HP automaticamente
            respawnPanel?.Hide();
        }

        // ─── Spawning ─────────────────────────────────────────────────────────────
        private void AssignLocalPlayer(string playerId, string joinJson)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[GameManager] playerPrefab não atribuído no Inspector!");
                return;
            }

            // Extrai posição inicial do payload de join.
            // O servidor envia {id, world, abilities, state:{x,y,...}} — os dados de posição
            // estão dentro de "state", não no nível raiz.
            var data = JsonUtility.FromJson<PlayerJoinData>(joinJson);
            var stateData = data?.state;

            // ServerToUnity usa GroundSampler internamente — Y = altura do terreno
            Vector3 startPos = stateData != null
                ? PlayerController.ServerToUnity(stateData.x, stateData.y)
                : GroundSampler.Snap(Vector3.zero);

            // Usa o nome do servidor se disponível; fallback para o nome configurado no Inspector
            string spawnName = !string.IsNullOrEmpty(stateData?.name) ? stateData.name : playerName;

            // Spawna o jogador local
            _localPlayerGO   = Instantiate(playerPrefab, startPos, Quaternion.identity);
            _localPlayerCtrl = _localPlayerGO.GetComponent<PlayerController>();

            if (_localPlayerCtrl == null)
                Debug.LogError("[GameManager] PlayerController não encontrado no playerPrefab!");

            // Constrói o visual stick man para o jogador local
            StickManBuilder.Build(_localPlayerGO, StickManBuilder.ClassColor(playerClass));

            // Nome tag acima da cabeça (branco = jogador local)
            string spawnNameForTag = !string.IsNullOrEmpty(stateData?.name) ? stateData.name : playerName;
            PlayerNameTag.Attach(_localPlayerGO, spawnNameForTag, Color.white);

            // Informa o ItemWorldController sobre o jogador local (para distância de pickup)
            itemController?.SetLocalPlayer(_localPlayerGO.transform);

            // Configura a câmera para seguir o jogador
            cameraController?.SetTarget(_localPlayerGO.transform);

            // Registra ID local no WorldState
            if (_world != null)
                _world.LocalPlayerId = playerId;

            // Inicializa HUD com dados reais do servidor
            hud?.SetPlayerName(spawnName);
            hud?.SetLevel(stateData?.level > 0 ? stateData.level : 1);
            hud?.SetHP(stateData?.hp ?? stateData?.maxHp ?? 100, stateData?.maxHp ?? 100);
            hud?.SetMana(stateData?.mana ?? stateData?.maxMana ?? 100, stateData?.maxMana ?? 100);
            hud?.SetXP(stateData?.xp ?? 0, stateData?.xpMax > 0 ? stateData.xpMax : 100);
            hud?.SetGold(stateData?.gold ?? 0);

            // Configura a barra de skills com as abilities recebidas do servidor
            if (data?.abilities != null && data.abilities.Length > 0)
            {
                var skillList = new System.Collections.Generic.List<SkillDef>(data.abilities);
                skillBar?.Configure(skillList);
                Debug.Log($"[GameManager] {skillList.Count} skills configuradas para {playerClass}.");
            }

            _gameStarted = true;
            Debug.Log($"[GameManager] Jogador local spawnado. ID: {playerId} em {startPos}");
        }

        private void SpawnRemotePlayer(string playerId)
        {
            // Não spawna o próprio jogador local como remoto
            if (playerId == _world?.LocalPlayerId) return;
            if (_remotePlayerObjects.ContainsKey(playerId)) return;

            if (remotePlayerPrefab == null)
            {
                Debug.LogWarning("[GameManager] remotePlayerPrefab não atribuído. Pulando spawn remoto.");
                return;
            }

            if (!_world.Players.TryGetValue(playerId, out var playerData)) return;

            // Snaupa ao terreno na posição inicial do remoto
            Vector3 pos = GroundSampler.Snap(new Vector3(playerData.x, 0f, playerData.z));
            var go = Instantiate(remotePlayerPrefab, pos, Quaternion.identity);
            go.name = $"RemotePlayer_{playerData.name}";

            // Constrói stick man com a cor da classe do remoto
            StickManBuilder.Build(go, StickManBuilder.ClassColor(playerData.className));

            // Nome tag na cor da classe (distingue remotos por classe)
            PlayerNameTag.Attach(go, playerData.name, StickManBuilder.ClassColor(playerData.className));

            _remotePlayerObjects[playerId] = go;
            Debug.Log($"[GameManager] Jogador remoto spawnado: {playerData.name} ({playerId})");
        }

        private void DespawnRemotePlayer(string playerId)
        {
            if (!_remotePlayerObjects.TryGetValue(playerId, out var go)) return;

            if (go != null) Destroy(go);
            _remotePlayerObjects.Remove(playerId);

            Debug.Log($"[GameManager] Jogador remoto removido: {playerId}");
        }

        // ─── Sincronização de remotos ──────────────────────────────────────────────
        // Interpola suavemente a posição dos GameObjects remotos em direção à
        // posição autoritativa do servidor (recebida a 20Hz no WorldState).
        private void SyncRemotePlayers()
        {
            if (_world == null) return;

            foreach (var kv in _remotePlayerObjects)
            {
                string id = kv.Key;
                GameObject go = kv.Value;
                if (go == null) continue;
                if (!_world.Players.TryGetValue(id, out var data)) continue;

                Vector3 target = GroundSampler.Snap(new Vector3(data.x, 0f, data.z));
                go.transform.position = Vector3.Lerp(go.transform.position, target, Time.deltaTime * 10f);
            }
        }

        // ─── Handler: skill:result ─────────────────────────────────────────────────
        private void HandleSkillResult(string json)
        {
            // Payload: { skillId, rejected?, resolved?, cooldown? }
            var data = JsonUtility.FromJson<SkillResultData>(json);
            if (data == null || string.IsNullOrEmpty(data.skillId)) return;

            bool rejected = !string.IsNullOrEmpty(data.rejected);
            skillBar?.OnSkillResult(data.skillId, rejected, data.cooldown);
        }

        // ─── Handler: item:picked ──────────────────────────────────────────────────
        private void HandleItemPicked(string json)
        {
            // Payload: { item: { id, type, x, y } }
            var data = JsonUtility.FromJson<ItemPickedData>(json);
            if (data?.item == null) return;

            Debug.Log($"[GameManager] Item coletado: {data.item.type}");

            // Feedback flutuante acima do jogador
            if (_localPlayerGO != null)
                FloatingText.Spawn(_localPlayerGO.transform.position + Vector3.up * 0.5f,
                                   $"+ {data.item.type}", Color.white);
        }

        // ─── Handler: combat:hits ──────────────────────────────────────────────────
        // Servidor agrega todos os acertos do tick num único array (evita N eventos/frame).
        private void HandleCombatHits(string json)
        {
            var wrapper = JsonUtility.FromJson<CombatHitsWrapper>($"{{\"items\":{json}}}");
            if (wrapper?.items == null) return;

            foreach (var hit in wrapper.items)
            {
                if (hit == null) continue;

                // Texto de dano flutuante na posição do alvo (player ou monstro)
                Vector3? pos = ResolveEntityPosition(hit.to);
                if (pos.HasValue)
                {
                    Color c = hit.crit ? new Color(1f, 0.5f, 0f) : Color.red;
                    FloatingText.Spawn(pos.Value + Vector3.up * 0.4f, hit.damage.ToString(), c,
                                       hit.crit ? 1.3f : 1f);
                }
            }
        }

        // ─── Handler: combat:deaths ────────────────────────────────────────────────
        private void HandleCombatDeaths(string json)
        {
            var wrapper = JsonUtility.FromJson<CombatDeathsWrapper>($"{{\"items\":{json}}}");
            if (wrapper?.items == null) return;

            foreach (var death in wrapper.items)
            {
                if (death == null || string.IsNullOrEmpty(death.id)) continue;

                // Morte de jogador local → tela de respawn
                if (death.id == _world?.LocalPlayerId)
                    respawnPanel?.Show();
            }
        }

        /// <summary>
        /// Resolve a posição Unity de uma entidade (jogador remoto, jogador local ou
        /// monstro) pelo ID, para posicionar texto de dano. Retorna null se desconhecida.
        /// </summary>
        private Vector3? ResolveEntityPosition(string entityId)
        {
            if (string.IsNullOrEmpty(entityId)) return null;

            if (entityId == _world?.LocalPlayerId && _localPlayerGO != null)
                return _localPlayerGO.transform.position;

            if (_remotePlayerObjects.TryGetValue(entityId, out var rgo) && rgo != null)
                return rgo.transform.position;

            if (_world != null && _world.Monsters.TryGetValue(entityId, out var m))
                return GroundSampler.Snap(new Vector3(m.x, 0f, m.z)) + Vector3.up * 0.5f;

            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════════
        // PARTE 2/4 — Estado local completo + painéis de UI
        // ═══════════════════════════════════════════════════════════════════════════

        // ─── Handlers de estado local (mantêm WorldState.Local atualizado) ─────────
        private void HandleGearEquipped(string json)       => _world?.HandleGearEquipped(json);
        private void HandleGearUnequipped(string json)     => _world?.HandleGearUnequipped(json);
        private void HandleSkillSelectResult(string json)  => _world?.HandleSkillSelectResult(json);
        private void HandleInventoryUpdated(string json)   => _world?.HandleInventoryUpdated(json);
        private void HandleRepairResult(string json)       => _world?.HandleRepairResult(json);
        private void HandleMasteryXp(string json)          => _world?.HandleMasteryXp(json);
        private void HandleMasteryLevelUp(string json)     => _world?.HandleMasteryLevelUp(json);
        private void HandleMasteryYellowFame(string json)  => _world?.HandleMasteryYellowFame(json);
        private void HandleMasteryConvertResult(string json) => _world?.HandleMasteryConvertResult(json);

        // ─── Estruturas de parsing JSON ────────────────────────────────────────────

        [System.Serializable]
        private class IdExtract { public string id; }

        [System.Serializable]
        private class PlayerJoinData
        {
            public StateData state;
            public SkillDef[] abilities;
        }

        [System.Serializable]
        private class StateData
        {
            public string name;
            public float  x;
            public float  y;
            public int    hp;
            public int    maxHp;
            public int    mana;
            public int    maxMana;
            public int    level;
            public int    xp;
            public int    xpMax;
            public int    gold;
        }

        [System.Serializable]
        private class PlayerXpData
        {
            public int xp;
            public int gold;
            public int totalXp;
            public int totalGold;
            public int xpMax;
        }

        [System.Serializable]
        private class PlayerLevelUpData
        {
            public int level;
            public int maxHp;
            public int maxMana;
            public int speed;
            public int xp;
            public int xpMax;
        }

        [System.Serializable]
        private class PlayerRevivedData
        {
            public int   hp;
            public float x;
            public float y;
        }

        [System.Serializable]
        private class SkillResultData
        {
            public string skillId;
            public string rejected; // nao-vazio => rejeitado
            public bool   resolved;
            public int    cooldown; // ms
        }

        [System.Serializable]
        private class ItemPickedData { public PickedItem item; }

        [System.Serializable]
        private class PickedItem
        {
            public string id;
            public string type;
            public float  x;
            public float  y;
        }

        [System.Serializable]
        private class CombatHitsWrapper { public CombatHit[] items; }

        [System.Serializable]
        private class CombatHit
        {
            public string from;
            public string to;
            public int    damage;
            public bool   crit;
            public int    hp;
            public bool   isMonster;
        }

        [System.Serializable]
        private class CombatDeathsWrapper { public CombatDeath[] items; }

        [System.Serializable]
        private class CombatDeath
        {
            public string id;
            public string killerId;
        }
    }
}
