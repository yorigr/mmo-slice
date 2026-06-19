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

            // Registra callbacks do WorldState para spawning de remotos
            if (_world != null)
            {
                _world.OnPlayerJoined += SpawnRemotePlayer;
                _world.OnPlayerLeft   += DespawnRemotePlayer;
            }

            // Cria SkillBar e ItemWorldController se não atribuídos no Inspector
            if (skillBar == null)
                skillBar = gameObject.AddComponent<SkillBar>();

            if (itemController == null)
                itemController = gameObject.AddComponent<ItemWorldController>();

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
            // Payload: { id, sessionToken, world, abilities:[...], state:{x,y,name,hp,...} }
            _world?.HandlePlayerJoined(json);

            var idPayload = JsonUtility.FromJson<IdExtract>(json);
            if (idPayload == null || string.IsNullOrEmpty(idPayload.id)) return;

            // Se ainda não spawnamos o jogador local, este evento é nosso
            if (string.IsNullOrEmpty(_world?.LocalPlayerId))
            {
                AssignLocalPlayer(idPayload.id, json);
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

            // TODO Phase 3: Tocar animação de morte no remote player / tela de respawn no local
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
            // O world:update vai refletir o novo estado automaticamente
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

        // ─── Sincronização de remotos ─────────────────────────────────────────────
        private void SyncRemotePlayers()
        {
            if (_world == null) return;

            // Move cada remote player em direção à posição do WorldState.
            // WorldState é atualizado 20Hz; entre updates, fazemos lerp para suavizar.
            foreach (var kvp in _remotePlayerObjects)
            {
                if (kvp.Value == null) continue;
                if (!_world.Players.TryGetValue(kvp.Key, out var data)) continue;

                // Alvo XZ vem do servidor; Y vem do terreno na posição alvo
                Vector3 targetPos = GroundSampler.Snap(new Vector3(data.x, 0f, data.z));

                // Lerp suave entre atualizaç