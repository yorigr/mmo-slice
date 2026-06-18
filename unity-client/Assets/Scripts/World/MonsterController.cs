// MonsterController.cs
// Sincroniza o estado dos monstros recebidos em world:update com GameObjects na cena.
//
// Responsabilidade:
//   GameManager chama SyncMonsters() a cada frame (ou a cada world:update).
//   MonsterController mantém um Dictionary<string, GameObject> para spawn/despawn/lerp.
//
// HP Bar:
//   Cada monstro tem uma World Space Canvas com Slider que se vira para a câmera.
//   Cor: verde > 50%, amarelo 25-50%, vermelho < 25%.
//
// Integração:
//   Adicione MonsterController ao mesmo GameObject que o GameManager.
//   Atribua monsterPrefab e hpBarPrefab no Inspector.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MMORPG.World;

namespace MMORPG
{
    public class MonsterController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Prefabs")]
        [Tooltip("Prefab visual do monstro. Pode ser um cubo temporário.")]
        [SerializeField] private GameObject monsterPrefab;

        [Tooltip("Prefab da barra de HP no World Space. Deve ter um Slider chamado 'HPSlider'.")]
        [SerializeField] private GameObject hpBarPrefab;

        [Header("Configuração")]
        [Tooltip("Velocidade de interpolação visual entre atualizações do servidor (20Hz).")]
        [SerializeField] private float lerpSpeed = 8f;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private WorldState _world;

        // GameObject de cada monstro, indexado por ID do servidor
        private readonly Dictionary<string, MonsterEntry> _monsters = new();

        // ─── Estrutura interna ────────────────────────────────────────────────────
        private class MonsterEntry
        {
            public GameObject Root;     // GameObject raiz (modelo)
            public Transform  HpBar;    // Transform do HP bar canvas
            public Slider     Slider;   // Slider de HP
            public Image      Fill;     // Imagem de fill (para colorir)
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _world = WorldState.Instance;

            if (_world == null)
                Debug.LogError("[MonsterController] WorldState não encontrado! Certifique-se que existe na cena.");
        }

        private void Update()
        {
            if (_world == null) return;
            SyncMonsters();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Sincroniza GameObjects com os dados de monstros do WorldState.
        /// Chamado em Update — não precisa ser chamado manualmente.
        /// </summary>
        public void SyncMonsters()
        {
            var serverMonsters = _world.Monsters;
            var camera = Camera.main;

            // Atualiza/spawna monstros existentes no servidor
            foreach (var kvp in serverMonsters)
            {
                string id = kvp.Key;
                var data  = kvp.Value;

                if (!_monsters.TryGetValue(id, out var entry))
                    entry = SpawnMonster(id, data);

                if (entry == null) continue;

                // Interpolação suave de posição (XZ vem do servidor, Y do terreno)
                Vector3 targetPos = GroundSampler.Snap(new Vector3(data.x, 0f, data.z));
                entry.Root.transform.position = Vector3.Lerp(
                    entry.Root.transform.position,
                    targetPos,
                    Time.deltaTime * lerpSpeed
                );

                // Atualiza HP bar
                UpdateHpBar(entry, data.hp, data.maxHp, camera);
            }

            // Despawna monstros que o servidor removeu
            var toRemove = new List<string>();
            foreach (string id in _monsters.Keys)
                if (!serverMonsters.ContainsKey(id))
                    toRemove.Add(id);

            foreach (string id in toRemove)
                DespawnMonster(id);
        }

        // ─── Spawn / Despawn ──────────────────────────────────────────────────────
        private MonsterEntry SpawnMonster(string id, RemoteMonster data)
        {
            if (monsterPrefab == null)
            {
                Debug.LogWarning("[MonsterController] monsterPrefab não atribuído.");
                return null;
            }

            Vector3 spawnPos = GroundSampler.Snap(new Vector3(data.x, 0f, data.z));
            var root = Instantiate(monsterPrefab, spawnPos, Quaternion.identity);
            root.name = $"Monster_{data.type}_{id.Substring(0, Mathf.Min(6, id.Length))}";

            // Coloração baseada no tipo de monstro (pode evoluir para prefabs distintos)
            var renderer = root.GetComponentInChildren<Renderer>();
            if (renderer != null)
                renderer.material.color = MonsterColor(data.type);

            MonsterEntry entry = new() { Root = root };

            // Cria HP bar se o prefab existir
            if (hpBarPrefab != null)
            {
                var barGO = Instantiate(hpBarPrefab, root.transform);
                barGO.transform.localPosition = new Vector3(0f, 1.8f, 0f); // acima do monstro
                entry.HpBar   = barGO.transform;
                entry.Slider  = barGO.GetComponentInChildren<Slider>();
                entry.Fill    = barGO.GetComponentInChildren<Image>();
            }

            _monsters[id] = entry;
            return entry;
        }

        private void DespawnMonster(string id)
        {
            if (!_monsters.TryGetValue(id, out var entry)) return;
            if (entry.Root != null) Destroy(entry.Root);
            _monsters.Remove(id);
        }

        // ─── HP Bar ───────────────────────────────────────────────────────────────
        private void UpdateHpBar(MonsterEntry entry, int hp, int maxHp, Camera cam)
        {
            if (entry.Slider == null) return;

            float pct = maxHp > 0 ? (float)hp / maxHp : 0f;
            entry.Slider.value = pct;

            // Cor dinâmica
            if (entry.Fill != null)
                entry.Fill.color = HpColor(pct);

            // Vira para a câmera (billboard)
            if (entry.HpBar != null && cam != null)
                entry.HpBar.rotation = Quaternion.LookRotation(
                    entry.HpBar.position - cam.transform.position
                );
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private static Color MonsterColor(string type)
        {
            return type switch
            {
                "wolf"    => new Color(0.55f, 0.45f, 0.35f),
                "goblin"  => new Color(0.3f,  0.65f, 0.2f),
                "orc"     => new Color(0.4f,  0.6f,  0.3f),
                "troll"   => new Color(0.3f,  0.5f,  0.4f),
                "dragon"  => new Color(0.8f,  0.2f,  0.1f),
                _         => new Color(0.6f,  0.2f,  0.2f), // vermelho padrão
            };
        }

        private static Color HpColor(float pct) => pct switch
        {
            > 0.5f => Color.green,
            > 0.25f => Color.yellow,
            _ => Color.red,
        };
    }
}
