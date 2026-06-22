// NpcController.cs
// Representa um NPC estático no mundo (Ferreiro Aldric, Instrutor Magnus).
//
// Como funciona:
//   GameManager recebe a lista `npcs` em player:joined e chama NpcController.Spawn()
//   para cada NPC. O controller cria um stick man colorido + tag de nome acima.
//
// Sem IA, sem movimento — NPCs são decoração interativa.
// A detecção de proximidade para repair/mastery é feita no servidor (BLACKSMITH_RANGE,
// TRAINER_RANGE). O cliente pode exibir um indicador visual (ver ProximityHint) mas
// a validação autoritativa sempre fica no servidor.
//
// Cores por tipo (provisório até assets reais):
//   blacksmith → laranja escuro (ferreiro)
//   trainer    → roxo (mago/instrutor)
//   default    → cinza
//
// Hierarquia criada:
//   NPC_<id> (NpcController)
//   ├── Body    (stick man via StickManBuilder)
//   └── NameTag (Canvas World Space → TMP_Text)

using UnityEngine;
using TMPro;
using MMORPG.World;

namespace MMORPG
{
    /// <summary>Dados de um NPC recebidos no payload player:joined.</summary>
    [System.Serializable]
    public class NpcData
    {
        public string id;
        public string type;
        public string name;
        public float  x;    // coordenadas do servidor (pixels)
        public float  y;    // coordenadas do servidor (pixels)
    }

    public class NpcController : MonoBehaviour
    {
        // ─── Constantes visuais ───────────────────────────────────────────────────

        // Escala Unity ← servidor: 1 unidade Unity = 50 px servidor
        private const float ServerToUnity = 1f / 50f;

        // Distância acima do corpo onde o nome aparece
        private const float NameTagHeight = 2.8f;

        // Cores por tipo de NPC
        private static readonly Color ColorBlacksmith = new Color(0.85f, 0.45f, 0.10f); // laranja queimado
        private static readonly Color ColorTrainer    = new Color(0.55f, 0.20f, 0.80f); // roxo
        private static readonly Color ColorDefault    = new Color(0.55f, 0.55f, 0.55f); // cinza

        // ─── Estado ───────────────────────────────────────────────────────────────
        private NpcData _data;
        private TMP_Text _nameText;

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Cria e inicializa um NpcController a partir dos dados recebidos do servidor.
        /// Chamado por GameManager durante o processamento de player:joined.
        /// </summary>
        public static NpcController Spawn(NpcData data)
        {
            if (data == null) return null;

            var go = new GameObject($"NPC_{data.id}");
            var ctrl = go.AddComponent<NpcController>();
            ctrl.Initialize(data);
            return ctrl;
        }

        // ─── Inicialização ────────────────────────────────────────────────────────

        private void Initialize(NpcData data)
        {
            _data = data;

            // Posiciona no mundo (servidor usa pixels; Unity usa unidades)
            transform.position = new Vector3(data.x * ServerToUnity, 0f, data.y * ServerToUnity);

            // Stick man colorido (reutiliza StickManBuilder existente)
            Color color = data.type switch
            {
                "blacksmith" => ColorBlacksmith,
                "trainer"    => ColorTrainer,
                _            => ColorDefault,
            };
            StickManBuilder.Build(gameObject, color);

            // Tag de nome flutuante
            CreateNameTag(data.name);

            // Indicador visual de interação (círculo no chão)
            CreateGroundRing(color);
        }

        /// <summary>Cria um Canvas World Space com o nome do NPC acima da cabeça.</summary>
        private void CreateNameTag(string npcName)
        {
            // Canvas World Space para o nome
            var canvasGo = new GameObject("NameTag");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, NameTagHeight, 0f);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingLayerName = "UI";

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(3f, 0.6f);

            // Texto do nome
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);

            _nameText = textGo.AddComponent<TextMeshProUGUI>();
            _nameText.text      = npcName;
            _nameText.fontSize  = 0.4f;
            _nameText.alignment = TextAlignmentOptions.Center;
            _nameText.color     = Color.white;

            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = textRt.offsetMax = Vector2.zero;

            // Faz o nome sempre olhar para a câmera (Billboard)
            canvasGo.AddComponent<NpcNameBillboard>();
        }

        /// <summary>
        /// Cria um anel no chão indicando a área de interação do NPC.
        /// Ajuda o jogador a saber onde parar para interagir.
        /// </summary>
        private void CreateGroundRing(Color color)
        {
            // LineRenderer formando um círculo no chão
            var ringGo = new GameObject("GroundRing");
            ringGo.transform.SetParent(transform, false);
            ringGo.transform.localPosition = new Vector3(0f, 0.05f, 0f); // ligeiramente acima do chão

            var lr = ringGo.AddComponent<LineRenderer>();
            lr.useWorldSpace      = false;
            lr.loop               = true;
            lr.widthMultiplier    = 0.05f;

            // Material simples sem textura
            lr.material = new Material(Shader.Find("Sprites/Default"));
            var ringColor = new Color(color.r, color.g, color.b, 0.4f);
            lr.startColor = ringColor;
            lr.endColor   = ringColor;

            // Círculo de raio 1.2 unidades (≈ 60px servidor = BLACKSMITH_RANGE / 2)
            const int segments = 32;
            const float radius = 1.2f;
            lr.positionCount = segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = 2f * Mathf.PI * i / segments;
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        // ─── Dados ────────────────────────────────────────────────────────────────

        /// <summary>ID do NPC (ex: "blacksmith_1").</summary>
        public string NpcId   => _data?.id;

        /// <summary>Tipo do NPC (ex: "blacksmith", "trainer").</summary>
        public string NpcType => _data?.type;

        /// <summary>Posição em coordenadas do servidor (pixels).</summary>
        public Vector2 ServerPosition => _data != null ? new Vector2(_data.x, _data.y) : Vector2.zero;
    }

    // ─── Billboard helper ─────────────────────────────────────────────────────────

    /// <summary>
    /// Faz o objeto sempre encarar a câmera principal (usado no nome do NPC).
    /// Separado em MonoBehaviour próprio para ser reutilizável.
    /// </summary>
    public class NpcNameBillboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            // Rotaciona para encarar a câmera mantendo o eixo Y
            transform.rotation = Quaternion.LookRotation(
                transform.position - cam.transform.position,
                cam.transform.up
            );
        }
    }
}
