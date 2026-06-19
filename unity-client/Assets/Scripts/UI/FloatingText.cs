// FloatingText.cs
// Texto flutuante que aparece sobre targets quando recebem dano, cura ou XP.
// Sobe, desvanece e se auto-destrói.
//
// Uso:
//   FloatingText.Spawn(position, "-42", Color.red);         // dano
//   FloatingText.Spawn(position, "-42 CRIT!", Color.yellow); // crítico
//   FloatingText.Spawn(position, "+XP", Color.cyan);         // XP
//
// Integração:
//   Não requer prefab. Cria um Canvas/TMP em world space e anima via Update().
//   GameManager chama Spawn() ao processar combat:hits e player:xp.

using UnityEngine;
using TMPro;

namespace MMORPG.UI
{
    public class FloatingText : MonoBehaviour
    {
        // ─── Configuração ────────────────────────────────────────────────────────
        private const float RISE_SPEED    = 1.8f;  // unidades/segundo (sobe)
        private const float DRIFT_SPEED   = 0.3f;  // leve deriva lateral aleatória
        private const float LIFETIME      = 1.2f;  // segundos até desaparecer
        private const float FADE_START    = 0.5f;  // começa a desvanecer em X segundos
        private const float FONT_SIZE     = 0.08f; // tamanho no espaço mundo

        // ─── Estado interno ──────────────────────────────────────────────────────
        private TextMeshPro _tmp;
        private float       _elapsed;
        private float       _driftX;
        private Color       _baseColor;

        // ─── API Pública ─────────────────────────────────────────────────────────

        /// <summary>
        /// Cria um texto flutuante na posição mundo dada.
        /// </summary>
        /// <param name="worldPos">Posição mundo onde o texto vai aparecer.</param>
        /// <param name="text">Texto exibido (ex: "-42", "+15 HP", "CRIT!").</param>
        /// <param name="color">Cor do texto.</param>
        /// <param name="scale">Fator de escala adicional (1 = padrão).</param>
        public static FloatingText Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
        {
            var go = new GameObject($"FloatingText_{text}");
            go.transform.position = worldPos + Vector3.up * 0.3f; // leve elevação inicial

            var ft = go.AddComponent<FloatingText>();
            ft.Initialize(text, color, scale);
            return ft;
        }

        // ─── Inicialização ────────────────────────────────────────────────────────
        private void Initialize(string text, Color color, float scale)
        {
            _baseColor = color;
            _driftX    = Random.Range(-DRIFT_SPEED, DRIFT_SPEED);

            // TextMeshPro em world space (sem Canvas)
            _tmp = gameObject.AddComponent<TextMeshPro>();
            _tmp.text              = text;
            _tmp.fontSize          = FONT_SIZE * scale * 12f; // TMP usa pts, ajustamos
            _tmp.color             = color;
            _tmp.alignment         = TextAlignmentOptions.Center;
            _tmp.fontStyle         = FontStyles.Bold;
            _tmp.enableWordWrapping = false;

            // Garante visibilidade (sorting layer)
            var rend = GetComponent<Renderer>();
            if (rend != null) rend.sortingOrder = 100;

            Destroy(gameObject, LIFETIME + 0.1f); // segurança
        }

        // ─── Animação ─────────────────────────────────────────────────────────────
        private void Update()
        {
            _elapsed += Time.deltaTime;

            // Sobe e deriva levemente
            transform.position += new Vector3(_driftX, RISE_SPEED, 0f) * Time.deltaTime;

            // Sempre vira para a câmera (billboard)
            if (Camera.main != null)
                transform.rotation = Quaternion.LookRotation(
                    transform.position - Camera.main.transform.position
                );

            // Fade-out progressivo
            float alpha = 1f;
            if (_elapsed > FADE_START)
                alpha = 1f - (_elapsed - FADE_START) / (LIFETIME - FADE_START);

            if (_tmp != null)
                _tmp.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b, Mathf.Clamp01(alpha));

            if (_elapsed >= LIFETIME)
                Destroy(gameObject);
        }
    }
}
