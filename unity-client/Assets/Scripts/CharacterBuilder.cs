// CharacterBuilder.cs
// Constrói o visual do personagem jogador.
// Tenta carregar o modelo FBX (Universal Base Characters) primeiro;
// usa StickManBuilder como fallback se o FBX não estiver disponível.
//
// Estrutura esperada em Assets/Resources/Characters/:
//   Superhero_Male_FullBody.fbx
//   Superhero_Female_FullBody.fbx
//
// Retorna a altura aproximada do personagem para que sistemas externos
// (CharacterController, PlayerNameTag) possam se ajustar.

using UnityEngine;

namespace MMORPG
{
    public static class CharacterBuilder
    {
        // ─── Alturas de referência ────────────────────────────────────────────────
        /// <summary>Altura aproximada do modelo FBX (para CharacterController e NameTag).</summary>
        public const float FBX_HEIGHT = 1.85f;

        /// <summary>Altura do StickMan procedural (para CharacterController e NameTag).</summary>
        public const float STICKMAN_HEIGHT = 1.05f;

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Constrói o visual do personagem como filhos de <paramref name="parent"/>.
        /// Retorna a altura do personagem (use para ajustar CharacterController/NameTag).
        /// </summary>
        /// <param name="parent">GameObject raiz do jogador (LocalPlayer / RemotePlayer).</param>
        /// <param name="armorColor">Cor de tint para o StickMan; não aplicada sobre o FBX.</param>
        /// <param name="gender">"male" ou "female" — determina qual FBX carregar.</param>
        public static float Build(GameObject parent, Color armorColor, string gender = "male")
        {
            if (parent == null) return STICKMAN_HEIGHT;

            // Limpa filhos visuais anteriores (re-builds e reconexões)
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                Object.Destroy(parent.transform.GetChild(i).gameObject);

            // ── Tenta FBX ────────────────────────────────────────────────────────
            string fbxPath = gender == "female"
                ? "Characters/Superhero_Female_FullBody"
                : "Characters/Superhero_Male_FullBody";

            var prefab = Resources.Load<GameObject>(fbxPath);
            if (prefab != null)
            {
                var model = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                model.name = "CharacterModel";
                model.transform.SetParent(parent.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale    = Vector3.one;

                // Corrige materiais para URP (evita material magenta em projetos URP)
                FixMaterialsURP(model);

                Debug.Log($"[CharacterBuilder] FBX carregado: {fbxPath}");
                return FBX_HEIGHT;
            }

            // ── Fallback: StickMan ────────────────────────────────────────────────
            Debug.LogWarning($"[CharacterBuilder] '{fbxPath}' não encontrado em Resources — usando StickMan.");
            StickManBuilder.Build(parent, armorColor);
            return STICKMAN_HEIGHT;
        }

        /// <summary>
        /// Mapeia playerClass para gênero do personagem (para escolher o FBX correto).
        /// Mago e ranger → female; demais → male.
        /// </summary>
        public static string ClassToGender(string playerClass) =>
            playerClass is "mage" or "ranger" ? "female" : "male";

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Corrige shaders de todos os Renderers para URP/Lit.
        /// Preserva as cores difusas originais do FBX (baked nos materiais).
        /// </summary>
        private static void FixMaterialsURP(GameObject root)
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit")
                      ?? Shader.Find("Standard");
            if (urpLit == null) return;

            foreach (var rend in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = rend.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    if (mats[i].shader == urpLit) continue; // já correto

                    // Preserva a cor diffuse antes de trocar o shader
                    Color col = mats[i].HasProperty("_Color") ? mats[i].color : Color.white;

                    mats[i].shader = urpLit;
                    mats[i].color  = col;
                    mats[i].SetFloat("_Smoothness", 0.25f);
                    mats[i].SetFloat("_Metallic",   0.1f);
                }
                rend.materials = mats;
            }
        }
    }
}
