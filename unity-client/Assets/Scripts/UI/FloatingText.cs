// FloatingText.cs
// Texto flutuante que aparece sobre targets ao receber dano, cura ou XP.
// Anima com subida suave + scale punch no spawn (críticos ficam maiores).
//
// Uso:
//   FloatingText.Spawn(pos, "-42",       FloatingText.Type.Damage);
//   FloatingText.Spawn(pos, "-42 CRIT!", FloatingText.Type.Critical);
//   FloatingText.Spawn(pos, "+25 HP",    FloatingText.Type.Heal);
//   FloatingText.Spawn(pos, "MISS",      FloatingText.Type.Miss);
//   FloatingText.Spawn(pos, "+120 XP",   FloatingText.Type.XP);
//
// Sem dependência de Canvas — usa TextMeshPro em world space (billboard).

using UnityEngine;
using TMPro;

namespace MMORPG.UI
{
    public class FloatingText : MonoBehaviour
    {
        // ─── Tipos de texto ───────────────────────────────────────────────────────
        public enum Type { Damage, Critical, Heal, Miss, XP, Gold }

        // ─── Constantes de animação ───────────────────────────────────────────────
        private const float LIFETIME   = 1.4f;
        private const float FADE_START = 0.7f;
        private const float RISE_SPEED = 2.2f;
        private const float DRIFT_MAX  = 0.25f;

        // Punch: o texto começa em escala PUNCH_SCALE e encolhe até 1 em PUNCH_TIME
        private const float PUNCH_SCALE = 1.6f;
        private const float PUNCH_TIME  = 0.12f;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private TextMeshPro _tmp;
        private float       _elapsed;
        private float       _driftX;
        private float       _driftZ;
        private Color       _baseColor;
        private float       _baseSize;
        private bool        _isPunch;  // true = tem animação de scale punch

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Cria um FloatingText com tipo pré-definido (cor e tamanho automáticos).</summary>
        public static FloatingText Spawn(Vector3 worldPos, string text, Type type = Type.Damage)
        {
            (Color color, float size, bool punch) = type switch
            {
                Type.Critical => (new Color(1.00f, 0.92f, 0.10f), 0.22f, true),   // amarelo, grande, punch
                Type.Heal     => (new Color(0.20f, 0.90f, 0.30f), 0.14f, false),  // verde
                Type.Miss     => (new Color(0.75f, 0.75f, 0.75f), 0.11f, false),  // cinza
                Type.XP       => (new Color(0.30f, 0.80f, 1.00f), 0.12f, false),  // ciano
                Type.Gold     => (new Color(1.00f, 0.80f, 0.15f), 0.12f, false),  // dourado
                _             => (new Color(1.00f, 0.30f, 0.20f), 0.14f, false),  // vermelho (damage)
            };

            return SpawnRaw(worldPos, text, color, size, punch);
        }

        /// <summary>Spawn com cor e tamanho customizados (compatibilidade retroativa).</summary>
        public static FloatingText Spawn(Vector3 worldPos, string text, Color color, float scale = 1f)
            => SpawnRaw(worldPos, text, color, 0.14f * scale, false);

        private static FloatingText SpawnRaw(Vector3 worldPos, string text,
                                              Color color, float size, bool punch)
        {
            var go = new GameObject($"FT_{text}");
            go.transform.position = worldPos + Vector3.up * 0.5f;

            var ft = go.AddComponent<FloatingText>();
            ft.Init(text, color, size, punch);
            return ft;
        }

        // ─── Inicialização ────────────────────────────────────────────────────────
        private void Init(string text, Color color, float size, bool punch)
        {
            _baseColor = color;
            _baseSize  = size;
            _isPunch   = punch;
            _driftX    = Random.Range(-DRIFT_MAX, DRIFT_MAX);
            _driftZ    = Random.Range(-DRIFT_MAX * 0.3f, DRIFT_MAX * 0.3f);

            _tmp = gameObject.AddComponent<TextMeshPro>();
            _tmp.text               = text;
            _tmp.fontSize           = size * 100f; // TMP usa pts, ~0.14 * 100 = 14pt mundo
            _tmp.color              = color;
            _tmp.alignment          = TextAlignmentOptions.Center;
            _tmp.fontStyle          = FontStyles.Bold;
            _tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;

            var rend = GetComponent<Renderer>();
            if (rend) rend.sortingOrder = 200;

            // Escala inicial para o punch
            if (_isPunch)
                transform.localScale = Vector3.one * PUNCH_SCALE;

            Destroy(gameObject, LIFETIME + 0.1f);
        }

        // ─── Animação ─────────────────────────────────────────────────────────────
        private void Update()
        {
            _elapsed += Time.deltaTime;

            // Movimento: sobe e deriva levemente
            float drift = _elapsed * 0.5f; // desacelera a deriva com o tempo
            transform.position += new Vector3(
                _driftX * (1f - drift),
                RISE_SPEED * Time.deltaTime,
                _driftZ * (1f - drift)
            );

            // Billboard — sempre de frente para a câmera
            if (Camera.main != null)
            {
                Vector3 lookDir = transform.position - Camera.main.transform.position;
                if (lookDir != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(lookDir);
            }

            // Scale punch: encolhe de PUNCH_SCALE → 1 nos primeiros PUNCH_TIME segundos
            if (_isPunch && _elapsed < PUNCH_TIME)
            {
                float t = _elapsed / PUNCH_TIME;
                float s = Mathf.Lerp(PUNCH_SCALE, 1f, t * t); // ease-out quadrático
                transform.localScale = Vector3.one * s;
            }

            // Fade-out
            float alpha = 1f;
            if (_elapsed > FADE_START)
                alpha = 1f - (_elapsed - FADE_START) / (LIFETIME - FADE_START);

            if (_tmp != null)
                _tmp.color = new Color(_baseColor.r, _baseColor.g, _baseColor.b,
                                       Mathf.Clamp01(alpha));

            if (_elapsed >= LIFETIME)
                Destroy(gameObject);
        }
    }
}
