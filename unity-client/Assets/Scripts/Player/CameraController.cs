// CameraController.cs
// Câmera 3D perspectiva estilo Albion Online / WoW: rotação fixa, smooth follow, zoom com scroll.
//
// Por que câmera perspectiva (não ortográfica)?
//   Ortográfica elimina a profundidade — objetos ao fundo parecem planos e o chão fica
//   visível como um quadrilátero achatado. Perspectiva dá sensação de distância e volume,
//   criando imersão comparável à do Albion Online e WoW.
//
// Ângulo X=50°:
//   X=30° (configuração anterior) é raso demais — vê-se o topo do chão em perspectiva quase
//   isométrica pura, sem profundidade. X=50° inclina mais a câmera, aproximando-a do ângulo
//   usado no Albion Online (~45-55°). O plano do chão desaparece atrás dos objetos e o
//   mundo parece tridimensional.
//
// Zoom por distância (não FOV):
//   Alterar o FOV ao dar zoom distorce a perspectiva ("dolly zoom"). Mover a câmera
//   ao longo do vetor offset mantém a proporção natural dos objetos — é o que Albion faz.
//
// Rotação Y=45°:
//   Ângulo diagonal clássico do gênero isométrico. Mantido.

using UnityEngine;

namespace MMORPG.Player
{
    [RequireComponent(typeof(Camera))]
    public class CameraController : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Alvo")]
        [Tooltip("Transform do jogador a seguir. Pode ser atribuído em runtime pelo GameManager.")]
        public Transform Target;

        [Header("Perspectiva")]
        [Tooltip("Field of View da câmera. 60° é o padrão para câmera de ARPG/MMORPG.")]
        [SerializeField] private float fieldOfView = 60f;

        [Header("Offset")]
        [Tooltip("Posição base da câmera relativa ao alvo. Com rot X=50°/Y=45°, " +
                 "(-10, 16, -10) aponta para o jogador a uma distância de ~22u.")]
        [SerializeField] private Vector3 offset = new Vector3(-10f, 16f, -10f);

        [Header("Suavização")]
        [Tooltip("Velocidade do smooth follow. Menor = mais suave mas com mais atraso.")]
        [SerializeField] private float followSmoothTime = 0.12f;

        [Header("Rotação (fixa)")]
        [Tooltip("X=50° dá profundidade. Y=45° dá a diagonal isométrica. Não expor ao jogador.")]
        [SerializeField] private Vector3 fixedRotation = new Vector3(50f, 45f, 0f);

        [Header("Zoom (distância do alvo)")]
        [Tooltip("Fator mínimo de zoom (mais perto do alvo).")]
        [SerializeField] private float zoomMin          = 0.4f;
        [Tooltip("Fator máximo de zoom (mais longe do alvo).")]
        [SerializeField] private float zoomMax          = 2.2f;
        [Tooltip("Fator de zoom padrão.")]
        [SerializeField] private float zoomDefault      = 1.0f;
        [Tooltip("Sensibilidade do scroll do mouse.")]
        [SerializeField] private float zoomSpeed        = 0.12f;
        [Tooltip("Suavização do zoom. Menor = mais suave.")]
        [SerializeField] private float zoomSmoothTime   = 0.1f;

        // ─── Componentes ──────────────────────────────────────────────────────────
        private Camera _camera;

        // ─── Estado do smooth follow ──────────────────────────────────────────────
        private Vector3 _velocity = Vector3.zero;

        // ─── Estado do zoom ───────────────────────────────────────────────────────
        // Zoom = fator multiplicado sobre o offset. 1.0 = distância padrão.
        private float _zoomScale;
        private float _targetZoomScale;
        private float _zoomVelocity;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // Perspectiva: dá profundidade e imersão que ortográfica não oferece
            _camera.orthographic = false;
            _camera.fieldOfView  = fieldOfView;

            _zoomScale       = zoomDefault;
            _targetZoomScale = zoomDefault;

            transform.rotation = Quaternion.Euler(fixedRotation);
        }

        private void LateUpdate()
        {
            // LateUpdate garante que o Follow roda DEPOIS do PlayerController mover o alvo.
            if (Target != null)
                FollowTarget();

            HandleZoomInput();
            ApplyZoom();

            // Re-aplica rotação fixa para garantir que nada sobrescreveu
            transform.rotation = Quaternion.Euler(fixedRotation);
        }

        // ─── Follow ───────────────────────────────────────────────────────────────
        private void FollowTarget()
        {
            // Offset escalado pelo fator de zoom — zoom in/out move a câmera para/contra o alvo
            Vector3 desiredPosition = Target.position + offset * _zoomScale;

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref _velocity,
                followSmoothTime
            );
        }

        // ─── Zoom ─────────────────────────────────────────────────────────────────
        private void HandleZoomInput()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) < 0.001f) return;

            // Scroll para cima = aproximar (reduz o fator de distância)
            // Scroll para baixo = afastar (aumenta o fator de distância)
            _targetZoomScale -= scroll * zoomSpeed * 10f;
            _targetZoomScale  = Mathf.Clamp(_targetZoomScale, zoomMin, zoomMax);
        }

        private void ApplyZoom()
        {
            _zoomScale = Mathf.SmoothDamp(
                _zoomScale,
                _targetZoomScale,
                ref _zoomVelocity,
                zoomSmoothTime
            );
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Define o alvo a seguir. Chamado pelo GameManager após spawnar o jogador.
        /// </summary>
        public void SetTarget(Transform target)
        {
            Target = target;

            if (target != null)
            {
                transform.position = target.position + offset * _zoomScale;
                _velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Snap imediato para a posição do alvo (sem smooth). Útil após teleporte.
        /// </summary>
        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = Target.position + offset * _zoomScale;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Reseta o zoom para o valor padrão.
        /// </summary>
        public void ResetZoom()
        {
            _targetZoomScale = zoomDefault;
        }

        // ─── Propriedades ─────────────────────────────────────────────────────────
        /// <summary>Fator de zoom atual (1.0 = padrão).</summary>
        public float CurrentZoom => _zoomScale;
    }
}
