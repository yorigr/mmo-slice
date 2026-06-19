// ItemWorldController.cs
// Renderiza itens no chão do mundo e permite ao jogador coletá-los.
//
// Funcionamento:
//   - A cada Update, sincroniza com WorldState.Items (atualizado pelo world:update a 20Hz).
//   - Cada item vira uma pequena esfera colorida com label de nome flutuante.
//   - Tecla E coleta o item mais próximo dentro do raio de coleta.
//   - Emite item:pickup → servidor remove o item e adiciona ao inventário.
//
// Integração na cena:
//   Adicione ao mesmo GameObject que o GameManager.
//   Chame SetLocalPlayer(transform) após spawnar o jogador local.
//
// Raio de coleta:
//   O servidor valida distância de 60px = 1.2 unidades Unity.
//   pickupRange no Inspector deve ser <= 1.5 para não confundir o servidor.

using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG
{
    public class ItemWorldController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Visual")]
        [SerializeField] private float itemRadius  = 0.15f; // Raio da esfera visual
        [SerializeField] private float labelHeight = 0.55f; // Altura do label acima do item

        [Header("Coleta")]
        [Tooltip("Distância máxima (unidades Unity) para coletar. Servidor limita 60px = 1.2u.")]
        [SerializeField] private float pickupRange = 1.4f;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private NetworkManager _net;
        private WorldState     _world;
        private Transform      _localPlayer;
        private Camera         _cam;

        // Mapa itemId → objetos visuais
        private readonly Dictionary<string, ItemEntry> _items = new();

        private class ItemEntry
        {
            public GameObject Root;
            public Transform  LabelRoot; // pai do texto (precisa girar para câmera)
        }

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _net   = NetworkManager.Instance;
            _world = WorldState.Instance;
            _cam   = Camera.main;

            if (_world == null)
                Debug.LogError("[ItemWorldController] WorldState não encontrado.");
        }

        private void Update()
        {
            if (_world == null) return;

            if (_cam == null) _cam = Camera.main;

            SyncItems();
            BillboardLabels();

            if (Input.GetKeyDown(KeyCode.E))
                TryPickupNearest();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Define o transform do jogador local para cálculo de distância de coleta.</summary>
        public void SetLocalPlayer(Transform playerTransform)
        {
            _localPlayer = playerTransform;
        }

        // ─── Sync ─────────────────────────────────────────────────────────────────
        private void SyncItems()
        {
            var serverItems = _world.Items;

            foreach (var kvp in serverItems)
            {
                if (!_items.TryGetValue(kvp.Key, out var entry))
                    entry = SpawnItem(kvp.Key, kvp.Value);

                if (entry?.Root == null) continue;

                // Posiciona no terreno (Y via GroundSampler + elevação visual)
                Vector3 pos = GroundSampler.Snap(new Vector3(kvp.Value.x, 0f, kvp.Value.z));
                pos.y += itemRadius;
                entry.Root.transform.position = pos;
            }

            // Despawna itens que saíram do servidor (coletados / expirados)
            var toRemove = new List<string>();
            foreach (string id in _items.Keys)
                if (!serverItems.ContainsKey(id))
                    toRemove.Add(id);

            foreach (string id in toRemove)
                DespawnItem(id);
        }

        private ItemEntry SpawnItem(string id, WorldItem data)
        {
            // Esfera visual
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = $"WorldItem_{data.type}";
            root.transform.localScale = Vector3.one * itemRadius * 2f;

            // Remove collider (pickup por distância, não por física)
            var col = root.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Material com cor por tipo
            var rend = root.GetComponent<Renderer>();
            if (rend != null)
                rend.material.color = ItemColor(data.type);

            // Label flutuante (world space canvas)
            var labelRoot = CreateWorldLabel(root.transform, data.type, labelHeight);

            var entry = new ItemEntry { Root = root, LabelRoot = labelRoot };
            _items[id] = entry;
            return entry;
        }

        private void DespawnItem(string id)
        {
            if (!_items.TryGetValue(id, out var entry)) return;
            if (entry.Root != null) Destroy(entry.Root);
            _items.Remove(id);
        }

        // ─── Coleta ───────────────────────────────────────────────────────────────
        private void TryPickupNearest()
        {
            if (_localPlayer == null)
            {
                Debug.LogWarning("[ItemWorldController] LocalPlayer não definido. Chame SetLocalPlayer().");
                return;
            }
            if (_net == null || !_net.IsConnected) return;

            string nearestId   = null;
            float  nearestDist = float.MaxValue;

            foreach (var kvp in _items)
            {
                if (kvp.Value?.Root == null) continue;
                float dist = Vector3.Distance(_localPlayer.position, kvp.Value.Root.transform.position);
                if (dist < nearestDist && dist <= pickupRange)
                {
                    nearestDist = dist;
                    nearestId   = kvp.Key;
                }
            }

            if (nearestId == null)
            {
                Debug.Log("[ItemWorldController] Nenhum item próximo para coletar (E).");
                return;
            }

            _net.Emit("item:pickup", $"{{\"itemId\":\"{nearestId}\"}}");
            Debug.Log($"[ItemWorldController] item:pickup → {nearestId}");
        }

        // ─── Labels ───────────────────────────────────────────────────────────────

        /// <summary>Cria um texto flutuante World Space acima do item.</summary>
        private static Transform CreateWorldLabel(Transform parent, string text, float height)
        {
            // Canvas world space (escala pequena)
            var canvasGO = new GameObject("LabelCanvas");
            canvasGO.transform.SetParent(parent, false);
            canvasGO.transform.localPosition = new Vector3(0f, height, 0f);
            canvasGO.transform.localScale    = Vector3.one * 0.01f; // 0.01 para escala mundo

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var cr = canvasGO.AddComponent<CanvasScaler>();
            cr.dynamicPixelsPerUnit = 10f;

            // Texto
            var textGO = new GameObject("Text");
            textGO.transform.SetParent(canvasGO.transform, false);

            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = FriendlyName(text);
            tmp.fontSize  = 24f; // em unidades do canvas (escalado para 0.01 = 0.24u visível)
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;

            var rect = textGO.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(200f, 40f);
            rect.anchoredPosition = Vector2.zero;

            return canvasGO.transform;
        }

        private void BillboardLabels()
        {
            if (_cam == null) return;

            foreach (var entry in _items.Values)
            {
                if (entry.LabelRoot == null) continue;
                // Aponta para a câmera (billboard simples)
                entry.LabelRoot.rotation = Quaternion.LookRotation(
                    entry.LabelRoot.position - _cam.transform.position
                );
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private static Color ItemColor(string type)
        {
            if (type == null) return Color.white;
            if (type.Contains("potion") && !type.Contains("mana")) return new Color(1f, 0.25f, 0.25f);
            if (type.Contains("mana"))                              return new Color(0.2f, 0.4f, 1f);
            if (type.Contains("sword") || type.Contains("axe") ||
                type.Contains("bow")   || type.Contains("club"))   return new Color(0.9f, 0.85f, 0.2f);
            if (type.Contains("armor"))                             return new Color(0.5f, 0.55f, 0.8f);
            return new Color(0.7f, 0.6f, 0.5f);
        }

        private static string FriendlyName(string id)
        {
            if (id == null) return "?";
            return System.Globalization.CultureInfo.CurrentCulture
                .TextInfo.ToTitleCase(id.Replace('_', ' '));
        }
    }
}
