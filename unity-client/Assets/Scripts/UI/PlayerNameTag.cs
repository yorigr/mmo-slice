// PlayerNameTag.cs
// Exibe o nome do jogador acima do stick man, sempre virado para a câmera.
//
// Uso:
//   PlayerNameTag.Attach(playerGameObject, "Yuri", Color.white);
//   PlayerNameTag.Attach(remoteGO, "Fulano", StickManBuilder.ClassColor("mage"));
//
// A tag é criada proceduralmente como filho do GameObject do player.
// Atualiza a rotação billboard a cada frame (Awake: create, Update: rotate).

using UnityEngine;
using TMPro;

namespace MMORPG.UI
{
    public class PlayerNameTag : MonoBehaviour
    {
        // Offset acima da cabeça do stick man (cabeça está em ~0.85u)
        private const float HEIGHT_OFFSET = 1.05f;
        private const float FONT_SIZE     = 0.07f;

        private TextMeshPro _tmp;

        // ─── API Pública ─────────────────────────────────────────────────────────

        /// <summary>
        /// Adiciona (ou substitui) a name tag em <paramref name="playerGO"/>.
        /// </summary>
        public static PlayerNameTag Attach(GameObject playerGO, string playerName, Color color)
        {
            // Remove tag anterior se existir
            var existing = playerGO.GetComponentInChildren<PlayerNameTag>();
            if (existing != null) Destroy(existing.gameObject);

            var tagGO = new GameObject("NameTag");
            tagGO.transform.SetParent(playerGO.transform, false);
            tagGO.transform.localPosition = new Vector3(0f, HEIGHT_OFFSET, 0f);

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
