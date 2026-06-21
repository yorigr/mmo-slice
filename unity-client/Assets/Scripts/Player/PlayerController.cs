// PlayerController.cs
// Controla o jogador local com click-to-move (estilo Albion Online) e reconciliação
// de posição com o servidor autoritativo.
//
// Movimento:
//   Botão direito do mouse (segurar) → define destino continuamente (como no Albion:
//   o cursor age como um joystick enquanto o botão direito estiver pressionado).
//   Ao soltar, o jogador continua até o último ponto e para sozinho.
//
// Client-Side Prediction:
//   O jogador se move localmente sem esperar confirmação do servidor.
//   Se o servidor discordar, a posição é corrigida suavemente via lerp.
//
// Conversão de coordenadas:
//   Servidor usa pixels: (x, y) com max (2400, 1800)
//   Unity usa unidades:  (x, y, z) — Y vem do terreno via GroundSampler
//   Divisor XZ: 50  →  serverX / 50f = unityX,  serverY / 50f = unityZ

using UnityEngine;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG.Player
{
    /// <summary>
    /// Direções cardinais e diagonais do movimento, usadas para animação futura.
    /// </summary>
    public enum MoveDirection { None, North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

    public class PlayerController : MonoBehaviour
    {
        // ─── Constantes de conversão ──────────────────────────────────────────────
        /// <summary>
        /// Divisor de escala: servidor usa pixels, Unity usa unidades.
        /// MAP_W=2400px → 48u; 2400/48 = 50.
        /// </summary>
        private const float COORD_SCALE = 50f;

        /// <summary>Velocidade em unidades Unity/s (200px/s ÷ 50 = 4u/s).</summary>
        private const float UNITY_SPEED = 200f / COORD_SCALE;

        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Click-to-Move")]
        [Tooltip("Distância (unidades Unity) para considerar que chegou ao destino.")]
        [SerializeField] private float arrivalThreshold = 0.25f;

        [Tooltip("LayerMask do chão para o raycast de clique. " +
                 "'Everything' funciona se não houver layer separada para o terreno.")]
        [SerializeField] private LayerMask groundLayerMask = ~0;

        [Header("Reconciliação")]
        [Tooltip("Distância mínima (u) para corrigir desvio do servidor. 2u ≈ 100px.")]
        [SerializeField] private float reconcileThreshold = 2f;

        [Tooltip("Velocidade da correção de posição (0=sem correção, 1=snap). 0.2 = suave.")]
        [SerializeField] [Range(0.01f, 1f)] private float reconcileLerpSpeed = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private Camera              _cam;
        private NetworkManager      _net;
        private CharacterController _cc;   // Adicionado em runtime pelo GameManager

        // Destino de click-to-move (null = parado)
        private Vector3?       _destination;

        // Reconciliação
        private Vector3        _serverAuthPosition;
        private bool           _hasServerPosition;

        // Throttle de envio ao servidor
        private Vector3        _lastSentPosition;
        private const float    SEND_THRESHOLD = 0.05f;

        // Estado de movimento público
        private MoveDirection  _currentDirection;
        private bool           _isMoving;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _net = NetworkManager.Instance;
            _cam = Camera.main;
            _cc  = GetComponent<CharacterController>();

            if (_net == null)
                Debug.LogError("[PlayerController] NetworkManager não encontrado!");
        }

        private void Update()
        {
            if (_net == null || !_net.IsConnected) return;
            if (_cam == null) _cam = Camera.main;

            HandleInput();
        }

        // ─── Input ────────────────────────────────────────────────────────────────

        private void HandleInput()
        {
            // Segurar botão direito atualiza o destino continuamente — exatamente como o
            // Albion Online: o cursor funciona como um "joystick" de direção enquanto
            // o botão direito estiver pressionado.
            if (Input.GetMouseButton(1))
                TrySetDestination();

            MoveTowardDestination();
        }

        /// <summary>
        /// Lança raycast do cursor ao chão e define o ponto de destino.
        /// Se nenhum collider for atingido, usa interseção com o plano y=0.
        /// </summary>
        private void TrySetDestination()
        {
            if (_cam == null) return;

            Ray ray = _cam.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out RaycastHit hit, 300f, groundLayerMask))
            {
                _destination = hit.point;
                return;
            }

            // Fallback: plano y=0 (quando não há collider de chão)
            if (Mathf.Abs(ray.direction.y) > 0.001f)
            {
                float t = -ray.origin.y / ray.direction.y;
                if (t > 0f)
                    _destination = ray.origin + ray.direction * t;
            }
        }

        /// <summary>
        /// Move o jogador em direção ao destino do clique.
        /// Para automaticamente ao chegar dentro de <see cref="arrivalThreshold"/>.
        /// </summary>
        private void MoveTowardDestination()
        {
            if (_destination == null)
            {
                _isMoving = false;
                _currentDirection = MoveDirection.None;
                return;
            }

            Vector3 dest = _destination.Value;
            Vector3 pos  = transform.position;

            // Distância no plano XZ (Y é visual — não conta para decisão de chegada)
            float distXZ = new Vector2(dest.x - pos.x, dest.z - pos.z).magnitude;

            if (distXZ < arrivalThreshold)
            {
                _destination = null;
                _isMoving = false;
                _currentDirection = MoveDirection.None;
                return;
            }

            // Direção planar (ignora Y para evitar jitter em terreno inclinado)
            Vector3 dir = new Vector3(dest.x - pos.x, 0f, dest.z - pos.z).normalized;

            _isMoving = true;
            _currentDirection = GetMoveDirection(dir);

            // Aplica movimento localmente (client prediction)
            // CharacterController.Move() lida com colisão contra árvores/rochas/muros.
            // Sem CharacterController (fallback), usa transform.position diretamente.
            Vector3 next = pos + dir * UNITY_SPEED * Time.deltaTime;
            next = ClampToMap(next);
            next.y = GroundSampler.GetHeight(next.x, next.z);

            if (_cc != null)
            {
                Vector3 delta = next - pos;
                _cc.Move(delta);
            }
            else
            {
                transform.position = next;
            }

            // Envia ao servidor apenas se deslocou o suficiente (evita flood)
            if (Vector3.Distance(transform.position, _lastSentPosition) >= SEND_THRESHOLD)
            {
                SendMoveToServer();
                _lastSentPosition = transform.position;
            }
        }

        // ─── Servidor ─────────────────────────────────────────────────────────────
        private void SendMoveToServer()
        {
            float sx  = transform.position.x * COORD_SCALE;
            float sy  = transform.position.z * COORD_SCALE;
            string dir = DirectionToString(_currentDirection);

            // InvariantCulture garante ponto decimal na rede (pt-BR usa vírgula e gera JSON inválido)
            _net.Emit("player:move", string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "{{\"x\":{0:F1},\"y\":{1:F1},\"dir\":\"{2}\"}}", sx, sy, dir));
        }

        /// <summary>
        /// Chamado pelo GameManager com a posição autoritativa do servidor (pixels).
        /// </summary>
        public void ApplyServerPosition(float serverX, float serverY)
        {
            _serverAuthPosition = GroundSampler.ServerToUnity(serverX, serverY);
            _hasServerPosition  = true;
        }

        private void LateUpdate()
        {
            if (!_hasServerPosition) return;

            // Reconciliação apenas no plano XZ — Y (terreno) não vem do servidor.
            Vector2 localXZ  = new Vector2(transform.position.x, transform.position.z);
            Vector2 serverXZ = new Vector2(_serverAuthPosition.x, _serverAuthPosition.z);
            float drift = Vector2.Distance(localXZ, serverXZ);

            if (drift > reconcileThreshold)
            {
                Vector3 lerped = Vector3.Lerp(transform.position, _serverAuthPosition, reconcileLerpSpeed);
                lerped.y = GroundSampler.GetHeight(lerped.x, lerped.z);

                if (_cc != null)
                    _cc.Move(lerped - transform.position);
                else
                    transform.position = lerped;
            }
        }

        // ─── Conversão de coordenadas ─────────────────────────────────────────────

        /// <summary>Servidor (pixels) → Unity (unidades) com altura do terreno.</summary>
        public static Vector3 ServerToUnity(float sx, float sy)
            => GroundSampler.ServerToUnity(sx, sy, COORD_SCALE);

        /// <summary>Unity (unidades) → Servidor (pixels).</summary>
        public static (float sx, float sy) UnityToServer(Vector3 pos)
            => (pos.x * COORD_SCALE, pos.z * COORD_SCALE);

        // ─── Utilitários ──────────────────────────────────────────────────────────
        private static Vector3 ClampToMap(Vector3 pos)
        {
            const float maxX = 2400f / COORD_SCALE; // 48u
            const float maxZ = 1800f / COORD_SCALE; // 36u
            return new Vector3(
                Mathf.Clamp(pos.x, 0f, maxX),
                pos.y,
                Mathf.Clamp(pos.z, 0f, maxZ)
            );
        }

        private static MoveDirection GetMoveDirection(Vector3 dir)
        {
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;

            return angle switch
            {
                < 22.5f  => MoveDirection.North,
                < 67.5f  => MoveDirection.NorthEast,
                < 112.5f => MoveDirection.East,
                < 157.5f => MoveDirection.SouthEast,
                < 202.5f => MoveDirection.South,
                < 247.5f => MoveDirection.SouthWest,
                < 292.5f => MoveDirection.West,
                < 337.5f => MoveDirection.NorthWest,
                _        => MoveDirection.North
            };
        }

        private static string DirectionToString(MoveDirection dir) => dir switch
        {
            MoveDirection.North     => "N",
            MoveDirection.NorthEast => "NE",
            MoveDirection.East      => "E",
            MoveDirection.SouthEast => "SE",
            MoveDirection.South     => "S",
            MoveDirection.SouthWest => "SW",
            MoveDirection.West      => "W",
            MoveDirection.NorthWest => "NW",
            _                       => "N"
        };

        // ─── Gizmos de debug ──────────────────────────────────────────────────────
        private void OnDrawGizmos()
        {
            if (!showDebugGizmos || !Application.isPlaying) return;

            // Posição local (prediction) — azul
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(transform.position, 0.3f);

            // Destino do clique — verde
            if (_destination.HasValue)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawSphere(_destination.Value, 0.2f);
                Gizmos.DrawLine(transform.position, _destination.Value);
            }

            // Posicao autoritativa do servidor — vermelho
            if (_hasServerPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_serverAuthPosition, 0.3f);
                Gizmos.DrawLine(transform.position, _serverAuthPosition);
            }
        }

        // Propriedades publicas
        public MoveDirection CurrentDirection => _currentDirection;
        public bool IsMoving => _isMoving;
    }
}
