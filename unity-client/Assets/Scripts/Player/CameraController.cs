// CameraController.cs
// Câmera isométrica 3D estilo Albion Online: rotação fixa, smooth follow, zoom com scroll.
//
// Por que câmera ortográfica?
//   Câmeras perspectivas distorcem objetos longe do centro — em jogos isométricos isso
//   cria inconsistências visuais (tiles de tamanhos diferentes na tela). Câmera
//   ortográfica mantém proporções consistentes em todo o campo de visão.
//
// Ângulo de câmera X=30° (não X=45°):
//   X=45° seria isométrico "matemático" (ângulos iguais), mas visualmente parece muito
//   inclinado. X=30° é o padrão usado por Albion Online, Diablo III e a maioria dos
//   ARPGs modernos — mais visibilidade horizontal e sensação mais natural.
//
// Rotação Y=45°:
//   Rotaciona 45° em torno do eixo vertical para a visão diagonal característica
//   dos jogos isométricos. Combinado com X=30° dá o ângulo clássico do gênero.

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

        [Header("Offset")]
        [Tooltip("Posição da câmera relativa ao alvo. Com rot X=30°/Y=45°, " +
                 "(-10, 14, -10) posiciona atrás e acima do jogador.")]
        [SerializeField] private Vector3 offset = new Vector3(-10f, 14f, -10f);

        [Header("Suavização")]
        [Tooltip("Velocidade do smooth follow. Menor = mais suave mas com mais atraso.")]
        [SerializeField] private float followSmoothTime = 0.15f;

        [Header("Rotação (fixa)")]
        [Tooltip("Rotação fixa da câmera. X=30° Y=45° é o padrão isométrico. " +
                 "Altere no Inspector para experimentar, mas não exponha ao jogador.")]
        [SerializeField] private Vector3 fixedRotation = new Vector3(30f, 45f, 0f);

        [Header("Zoom")]
        [SerializeField] private float zoomMin          = 5f;
        [SerializeField] private float zoomMax          = 20f;
        [SerializeField] private float zoomDefault      = 10f;
        [SerializeField] private float zoomSpeed        = 3f;
        [Tooltip("Suavização do zoom. Menor = mais suave.")]
        [SerializeField] private float zoomSmoothTime   = 0.1f;

        // ─── Componentes ──────────────────────────────────────────────────────────
        private Camera _camera;

        // ─── Estado do smooth follow ──────────────────────────────────────────────
        private Vector3 _velocity = Vector3.zero;  // Usado internamente pelo SmoothDamp

        // ─── Estado do zoom ───────────────────────────────────────────────────────
        private float _targetZoom;
        private float _zoomVelocity; // Usado internamente pelo SmoothDamp do zoom

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _camera = GetComponent<Camera>();

            // Garante configuração ortográfica — se estiver em perspectiva no Editor, corrige
            _camera.orthographic = true;
            _camera.orthographicSize = zoomDefault;
            _targetZoom = zoomDefault;

            // Aplica rotação fixa imediatamente (sem interpolação no início)
            transform.rotation = Quaternion.Euler(fixedRotation);
        }

        private void LateUpdate()
        {
            // LateUpdate garante que o Follow roda DEPOIS do PlayerController mover o alvo.
            // Se fizéssemos em Update, a câmera estaria um frame atrás do movimento.

            if (Target != null)
                FollowTarget();

            HandleZoomInput();
            ApplyZoom();

            // A rotação nunca muda em runtime — re-aplica para garantir que ninguém sobrescreveu
            transform.rotation = Quaternion.Euler(fixedRotation);
        }

        // ─── Follow ───────────────────────────────────────────────────────────────
        private void FollowTarget()
        {
            // Posição desejada = alvo + offset (no espaço do mundo, não local)
            // Por que não usar offset local? Porque a câmera tem rotação fixa e o offset
            // em espaço mundo é mais intuitivo de ajustar no Inspector.
            Vector3 desiredPosition = Target.position + offset;

            // SmoothDamp: suaviza o movimento sem over/undershooting.
            // É preferível a Lerp(pos, target, t) que tem aceleração inconsistente por frame.
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

            // ScrollWheel positivo = scroll para cima = aproximar (reduz orthographicSize)
            // ScrollWheel negativo = scroll para baixo = afastar (aumenta orthographicSize)
            _targetZoom -= scroll * zoomSpeed;
            _targetZoom  = Mathf.Clamp(_targetZoom, zoomMin, zoomMax);
        }

        private void ApplyZoom()
        {
            // SmoothDamp para zoom também — evita zoom abrupto
            _camera.orthographicSize = Mathf.SmoothDamp(
                _camera.orthographicSize,
                _targetZoom,
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

            // Teleporta a câmera para perto do alvo imediatamente (sem suavização inicial)
            // para evitar o "slide" de inicialização de (0,0,0) até a posição do jogador
            if (target != null)
            {
                transform.position = target.position + offset;
                _velocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Snap imediato para a posição do alvo (sem smooth). Útil após teleporte do jogador.
        /// </summary>
        public void SnapToTarget()
        {
            if (Target == null) return;
            transform.position = Target.position + offset;
            _velocity = Vector3.zero;
        }

        /// <summary>
        /// Reseta o zoom para o valor padrão.
        /// </summary>
        public void ResetZoom()
        {
            _targetZoom = zoomDefault;
        }

        // ─── Propriedades ─────────────────────────────────────────────────────────
        public float CurrentZoom => _camera.orthographicSize;
    }
}
