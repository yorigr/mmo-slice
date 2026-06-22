// SkillEffectSystem.cs
// Sistema de efeitos visuais de skill no terreno:
//
//   1. AOE Preview — círculo animado que segue o mouse durante o "aim mode"
//      Ativado pelo SkillBar quando uma skill AOE está sendo mirada.
//      Cor e raio derivados do SkillDef (range em pixels → unidades Unity).
//
//   2. Impact VFX — anel expansivo que aparece quando a skill aterra.
//      Chamado via SkillEffectSystem.SpawnImpact(position, radius, color).
//
// Design: classe estática + MonoBehaviour interno para manter objetos de cena.
// Sem dependências de assets externos — usa primitivos Unity.

using UnityEngine;

namespace MMORPG.UI
{
    public static class SkillEffectSystem
    {
        // ─── Preview AOE ──────────────────────────────────────────────────────────
        private static AOEPreviewRenderer _preview;

        /// <summary>
        /// Exibe o preview de AOE na posição do mouse.
        /// radius em unidades Unity. Chame todo frame enquanto estiver em aim mode.
        /// </summary>
        public static void ShowAOEPreview(float radius, Color color)
        {
            if (_preview == null)
            {
                var go = new GameObject("AOEPreview");
                _preview = go.AddComponent<AOEPreviewRenderer>();
            }
            _preview.SetActive(true);
            _preview.SetParameters(radius, color);
            _preview.UpdatePosition();
        }

        /// <summary>Esconde e destrói o preview de AOE.</summary>
        public static void HideAOEPreview()
        {
            if (_preview != null)
                _preview.SetActive(false);
        }

        // ─── Impact VFX ──────────────────────────────────────────────────────────

        /// <summary>
        /// Spawna um anel expansivo de impacto no terreno.
        /// radius em unidades Unity. Chame no momento em que a skill é confirmada.
        /// </summary>
        public static void SpawnImpact(Vector3 worldPos, float radius, Color color)
        {
            var go = new GameObject("SkillImpact");
            go.AddComponent<ImpactRingVFX>().Init(worldPos, radius, color);
        }

        // ─── Helper: converte range de pixels para unidades Unity ────────────────
        public static float RangeToUnits(int rangePixels)
            => rangePixels > 0 ? rangePixels / 50f : 3f; // fallback 3u se range=0
    }

    // =========================================================================
    // AOEPreviewRenderer — anel circular animado no terreno, segue o mouse
    // =========================================================================
    [RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    internal class AOEPreviewRenderer : MonoBehaviour
    {
        // Número de segmentos do anel (mais = mais redondo, mas mais vértices)
        private const int   SEGMENTS     = 48;
        private const float RING_WIDTH   = 0.08f; // espessura do anel em u
        private const float PULSE_FREQ   = 2.5f;  // Hz da pulsação de brilho
        private const float FILL_ALPHA   = 0.08f; // preenchimento interior quase invisível
        private const float RING_ALPHA   = 0.75f; // anel visível

        // Objetos de cena
        private GameObject   _ring;       // anel de borda
        private GameObject   _fill;       // disco interior semi-transparente
        private MeshRenderer _ringRend;
        private MeshRenderer _fillRend;
        private Material     _ringMat;
        private Material     _fillMat;

        private float _radius;
        private Color _color;
        private float _phase;

        private void Awake()
        {
            // Shader com suporte a transparência
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");

            // ── Anel (borda) ──
            _ring = new GameObject("Ring");
            _ring.transform.SetParent(transform, false);
            _ringMat = new Material(shader);
            _ringMat.SetFloat("_Surface", 1f);          // URP: transparent
            _ringMat.SetFloat("_Blend", 0f);
            _ringMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            _ringMat.renderQueue = 3000;
            _ringMat.EnableKeyword("_EMISSION");
            var ringMF = _ring.AddComponent<MeshFilter>();
            _ringRend  = _ring.AddComponent<MeshRenderer>();
            _ringRend.material = _ringMat;
            _ringRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ringRend.receiveShadows    = false;
            ringMF.mesh = BuildRingMesh(3f, RING_WIDTH);

            // ── Preenchimento (disco) ──
            _fill = new GameObject("Fill");
            _fill.transform.SetParent(transform, false);
            _fillMat = new Material(shader);
            _fillMat.SetFloat("_Surface", 1f);
            _fillMat.SetFloat("_Blend", 0f);
            _fillMat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            _fillMat.renderQueue = 2999;
            var fillMF = _fill.AddComponent<MeshFilter>();
            _fillRend  = _fill.AddComponent<MeshRenderer>();
            _fillRend.material = _fillMat;
            _fillRend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _fillRend.receiveShadows    = false;
            fillMF.mesh = BuildDiscMesh(3f);
        }

        private void Update()
        {
            _phase += Time.deltaTime * PULSE_FREQ * Mathf.PI * 2f;

            // Pulso de brilho no anel
            float pulse = 0.6f + 0.4f * Mathf.Sin(_phase);

            Color ringColor = new Color(_color.r, _color.g, _color.b, RING_ALPHA * pulse);
            _ringMat.color = ringColor;
            _ringMat.SetColor("_EmissionColor", ringColor * pulse * 0.8f);

            Color fillColor = new Color(_color.r, _color.g, _color.b, FILL_ALPHA);
            _fillMat.color = fillColor;
        }

        public void SetParameters(float radius, Color color)
        {
            if (Mathf.Abs(radius - _radius) > 0.01f)
            {
                _radius = radius;
                // Reconstrói meshes com novo raio
                _ring.GetComponent<MeshFilter>().mesh = BuildRingMesh(radius, RING_WIDTH);
                _fill.GetComponent<MeshFilter>().mesh = BuildDiscMesh(radius);
            }
            _color = color;
        }

        /// <summary>Lança raycast ao chão e move o preview para o ponto do mouse.</summary>
        public void UpdatePosition()
        {
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            // Tenta acertar o terreno
            if (Physics.Raycast(ray, out RaycastHit hit, 300f))
            {
                transform.position = new Vector3(hit.point.x, hit.point.y + 0.05f, hit.point.z);
                return;
            }

            // Fallback: plano y=0
            if (Mathf.Abs(ray.direction.y) > 0.001f)
            {
                float t = -ray.origin.y / ray.direction.y;
                if (t > 0f)
                {
                    Vector3 p = ray.origin + ray.direction * t;
                    transform.position = new Vector3(p.x, 0.05f, p.z);
                }
            }
        }

        public void SetActive(bool active)
        {
            _ring?.SetActive(active);
            _fill?.SetActive(active);
        }

        // ── Geração de mesh procedural ────────────────────────────────────────────

        /// <summary>Anel (torus plano) entre innerRadius e outerRadius.</summary>
        private static Mesh BuildRingMesh(float outerRadius, float width)
        {
            float innerRadius = Mathf.Max(0.01f, outerRadius - width);
            int   segs        = SEGMENTS;

            var verts = new Vector3[segs * 2];
            var tris  = new int[segs * 6];
            var uvs   = new Vector2[segs * 2];

            for (int i = 0; i < segs; i++)
            {
                float angle = i / (float)segs * Mathf.PI * 2f;
                float cos   = Mathf.Cos(angle);
                float sin   = Mathf.Sin(angle);

                verts[i * 2]     = new Vector3(cos * innerRadius, 0f, sin * innerRadius);
                verts[i * 2 + 1] = new Vector3(cos * outerRadius, 0f, sin * outerRadius);
                uvs[i * 2]     = new Vector2(i / (float)segs, 0f);
                uvs[i * 2 + 1] = new Vector2(i / (float)segs, 1f);
            }

            for (int i = 0; i < segs; i++)
            {
                int next = (i + 1) % segs;
                int t    = i * 6;
                tris[t]   = i * 2;     tris[t+1] = next * 2;     tris[t+2] = i * 2 + 1;
                tris[t+3] = next * 2;  tris[t+4] = next * 2 + 1; tris[t+5] = i * 2 + 1;
            }

            var mesh = new Mesh { name = "AOERing" };
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.uv        = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>Disco sólido para o preenchimento interior.</summary>
        private static Mesh BuildDiscMesh(float radius)
        {
            int segs  = SEGMENTS;
            var verts = new Vector3[segs + 1];
            var tris  = new int[segs * 3];
            var uvs   = new Vector2[segs + 1];

            verts[0] = Vector3.zero;
            uvs[0]   = new Vector2(0.5f, 0.5f);

            for (int i = 0; i < segs; i++)
            {
                float angle = i / (float)segs * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                uvs[i + 1]   = new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f);
            }

            for (int i = 0; i < segs; i++)
            {
                int t = i * 3;
                tris[t]   = 0;
                tris[t+1] = i + 1;
                tris[t+2] = (i + 1) % segs + 1;
            }

            var mesh = new Mesh { name = "AOEDisc" };
            mesh.vertices  = verts;
            mesh.triangles = tris;
            mesh.uv        = uvs;
            mesh.RecalculateNormals();
            return mesh;
        }
    }

    // =========================================================================
    // ImpactRingVFX — anel que expande e desvanece no ponto de impacto da skill
    // =========================================================================
    internal class ImpactRingVFX : MonoBehaviour
    {
        private const float DURATION    = 0.55f;
        private const float RING_WIDTH  = 0.12f;
        private const int   SEGMENTS    = 48;

        private MeshRenderer _rend;
        private Material     _mat;
        private float        _elapsed;
        private float        _maxRadius;
        private Color        _color;

        public void Init(Vector3 pos, float radius, Color color)
        {
            transform.position = new Vector3(pos.x, pos.y + 0.06f, pos.z);
            _maxRadius = radius;
            _color     = color;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard");
            _mat = new Material(shader);
            _mat.SetFloat("_Surface", 1f);
            _mat.SetFloat("_Blend", 0f);
            _mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            _mat.EnableKeyword("_EMISSION");
            _mat.renderQueue = 3001;

            var mf = gameObject.AddComponent<MeshFilter>();
            _rend  = gameObject.AddComponent<MeshRenderer>();
            _rend.material = _mat;
            _rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _rend.receiveShadows    = false;
            mf.mesh = BuildRingMesh(0.01f);

            Destroy(gameObject, DURATION + 0.1f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = _elapsed / DURATION;           // 0 → 1
            float eased = 1f - (1f - t) * (1f - t); // ease-out quadrático

            float radius = eased * _maxRadius;
            GetComponent<MeshFilter>().mesh = BuildRingMesh(radius);

            // Alpha desvanece conforme expande
            float alpha = (1f - t) * 0.9f;
            Color c = new Color(_color.r, _color.g, _color.b, alpha);
            _mat.color = c;
            _mat.SetColor("_EmissionColor", c * (1f - t) * 1.5f);
        }

        private Mesh BuildRingMesh(float outer)
        {
            float inner = Mathf.Max(0f, outer - RING_WIDTH);
            int   segs  = SEGMENTS;
            var verts   = new Vector3[segs * 2];
            var tris    = new int[segs * 6];

            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                verts[i*2]   = new Vector3(Mathf.Cos(a)*inner, 0f, Mathf.Sin(a)*inner);
                verts[i*2+1] = new Vector3(Mathf.Cos(a)*outer, 0f, Mathf.Sin(a)*outer);
            }
            for (int i = 0; i < segs; i++)
            {
                int next = (i+1)%segs, t = i*6;
                tris[t]=i*2; tris[t+1]=next*2; tris[t+2]=i*2+1;
                tris[t+3]=next*2; tris[t+4]=next*2+1; tris[t+5]=i*2+1;
            }

            var m = new Mesh { name = "ImpactRing" };
            m.vertices  = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }
    }
}
