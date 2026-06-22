// KenneyAssetLoader.cs
// Carrega modelos Kenney Nature Kit em runtime via Resources.Load.
// Aplica shader URP automaticamente (modelos importados usam Standard por padrão).
//
// Estrutura esperada em Assets/Resources/:
//   NatureKit/trees/   ← tree_default.dae, tree_cone.dae, tree_oak.dae ...
//   NatureKit/rocks/   ← rock_largeA.dae, rock_tallA.dae ...
//   NatureKit/props/   ← campfire_logs.dae, mushroom_red.dae ...
//   Characters/        ← Superhero_Male_FullBody.fbx, Superhero_Female_FullBody.fbx
//
// Uso:
//   GameObject tree = KenneyAssetLoader.SpawnTree("tree_oak", parent, pos, scale);
//   GameObject rock = KenneyAssetLoader.SpawnRock("rock_largeA", parent, pos, rot, scale);
//   GameObject prop = KenneyAssetLoader.SpawnProp("campfire_logs", parent, pos);

using UnityEngine;

namespace MMORPG.World
{
    public static class KenneyAssetLoader
    {
        // ─── Cache: evita Resources.Load repetitivo ───────────────────────────────
        private static readonly System.Collections.Generic.Dictionary<string, GameObject>
            _cache = new System.Collections.Generic.Dictionary<string, GameObject>();

        // Shader URP (fallback para Standard se URP não estiver no projeto)
        private static Shader _urpLit;

        private static Shader URPLit
        {
            get
            {
                if (_urpLit == null)
                    _urpLit = Shader.Find("Universal Render Pipeline/Lit")
                           ?? Shader.Find("Standard");
                return _urpLit;
            }
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Spawna uma árvore Kenney. Retorna null se o modelo não estiver disponível.
        /// </summary>
        public static GameObject SpawnTree(string modelName, GameObject parent,
                                           Vector3 pos, float scale = 1f)
            => Spawn($"NatureKit/trees/{modelName}", parent, pos,
                     Quaternion.Euler(0f, Random.Range(0f, 360f), 0f),
                     scale, addCollider: true);

        /// <summary>Spawna uma rocha Kenney.</summary>
        public static GameObject SpawnRock(string modelName, GameObject parent,
                                           Vector3 pos, float yRot = 0f, float scale = 1f)
            => Spawn($"NatureKit/rocks/{modelName}", parent, pos,
                     Quaternion.Euler(0f, yRot, 0f), scale, addCollider: true);

        /// <summary>Spawna um prop (fogueira, cogumelo, etc.).</summary>
        public static GameObject SpawnProp(string modelName, GameObject parent,
                                           Vector3 pos, float yRot = 0f, float scale = 1f)
            => Spawn($"NatureKit/props/{modelName}", parent, pos,
                     Quaternion.Euler(0f, yRot, 0f), scale, addCollider: false);

        /// <summary>Retorna true se um modelo estiver disponível nos Resources.</summary>
        public static bool HasModel(string resourcePath)
            => Load(resourcePath) != null;

        // ─── Implementação interna ────────────────────────────────────────────────

        private static GameObject Spawn(string resourcePath, GameObject parent,
                                        Vector3 pos, Quaternion rot, float scale,
                                        bool addCollider)
        {
            var prefab = Load(resourcePath);
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, pos, rot);
            go.name = System.IO.Path.GetFileNameWithoutExtension(resourcePath);
            if (parent != null)
                go.transform.SetParent(parent.transform, true);

            go.transform.localScale = Vector3.one * scale;

            // Corrige materiais para URP (evita "pink material" em projetos URP)
            FixMaterialsURP(go);

            // Collider de cápsula no root para obstáculos (árvores, rochas)
            if (addCollider && go.GetComponentInChildren<Collider>() == null)
            {
                var col = go.AddComponent<CapsuleCollider>();
                // Estima tamanho pelo bounds do primeiro renderer
                var rend = go.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    var b = rend.bounds;
                    col.height = b.size.y;
                    col.radius = Mathf.Max(b.size.x, b.size.z) * 0.25f;
                    col.center = go.transform.InverseTransformPoint(b.center);
                }
                else
                {
                    col.height = 2f;
                    col.radius = 0.4f;
                }
            }

            return go;
        }

        /// <summary>Carrega do cache ou de Resources.</summary>
        private static GameObject Load(string resourcePath)
        {
            if (_cache.TryGetValue(resourcePath, out var cached))
                return cached;

            var prefab = Resources.Load<GameObject>(resourcePath);
            _cache[resourcePath] = prefab; // null também é cacheado (evita buscas repetidas)

            if (prefab == null)
                Debug.LogWarning($"[KenneyAssetLoader] Modelo não encontrado: {resourcePath}");

            return prefab;
        }

        /// <summary>
        /// Substitui shaders Standard/Legacy pelo URP Lit em todos os renderers.
        /// Preserva as cores diffuse originais (baked nas DAE pelo Kenney).
        /// </summary>
        private static void FixMaterialsURP(GameObject root)
        {
            if (URPLit == null) return;

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].shader == URPLit) continue; // já correto

                    // Extrai cor antes de trocar o shader
                    Color col = mats[i].HasProperty("_Color")
                                ? mats[i].color
                                : Color.white;

                    mats[i].shader = URPLit;
                    mats[i].SetFloat("_Smoothness", 0.1f);  // low-poly fica melhor fosco
                    mats[i].SetFloat("_Metallic",   0f);
                    mats[i].color = col;
                }
                rend.materials = mats;
            }
        }

        /// <summary>Limpa o cache (útil ao recarregar a cena).</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
