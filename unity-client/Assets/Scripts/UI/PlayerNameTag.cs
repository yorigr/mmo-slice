// PlayerNameTag.cs
// Exibe o nome do jogador acima do personagem, sempre virado para a câmera.
//
// Uso:
//   PlayerNameTag.Attach(playerGO, "Yuri", Color.white, characterHeight);
//   characterHeight vem do retorno de CharacterBuilder.Build() —
//   1.05f para StickMan, 1.85f para FBX (Universal Base Characters).
//
// A tag é criada proceduralmente como filho do GameObject do player.
// Atualiza a rotação billboard a cada frame (Awake: create, Update: rotate).

using UnityEngine;
using TMPro;

namespace MMORPG.UI
{
    public class PlayerNameTag : MonoBehaviour
    {
        // Margem extra acima do topo do personagem
        private const float HEAD_MARGIN = 0.25f;
        private const float FONT_SIZE   = 0.07f;

        private TextMeshPro _tmp;

        // ─── API Pública ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adiciona (ou substitui) a name tag em <paramref name="playerGO"/>.
        /// </summary>
        /// <param name="characterHeight">
        /// Altura do personagem em unidades Unity.
        /// Use CharacterBuilder.FBX_HEIGHT ou CharacterBuilder.STICKMAN_HEIGHT.
        /// </param>
        public static PlayerNameTag Attach(GameObject playerGO, string playerName, Color color,
                                           float characterHeight = CharacterBuilder.STICKMAN_HEIGHT)
        {
            // Remove tag anterior se existir
            var existing = playerGO.GetComponentInChildren<PlayerNameTag>();
            if (existing != null) Destroy(existing.gameObject);

            var tagGO = new GameObject("NameTag");
            tagGO.transform.SetParent(playerGO.transform, false);
            tagGO.transform.localPosition = new Vector3(0f, characterHeight + HEAD_MARGIN, 0f);

            var tag = tagGO.AddComponent<PlayerNameTag>();
            tag.Build(playerName, color);
            return tag;
        }

        // ─── Inicialização ────────────────────────────────────────────────────────
        private void Build(string playerName, Color color)
        {
            _tmp = gameObject.AddComponent<TextMeshPro>();
            _tmp.text              = playerName;
            _tmp.fontSize          = FONT_SIZE * 12f;
            _tmp.color             = color;
            _tmp.alignment         = TextAlignmentOptions.Center;
            _tmp.fontStyle         = FontStyles.Bold;
            _tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            var rend = GetComponent<Renderer>();
            if (rend != null) rend.sortingOrder = 50;
        }

        // ─── Billboard ────────────────────────────────────────────────────────────
        private void Update()
        {
            if (Camera.main == null || _tmp == null) return;

            transform.rotation = Quaternion.LookRotation(
                transform.position - Camera.main.transform.position
            );
        }
    }
}
