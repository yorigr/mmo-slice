// PlayerController.cs
// Controla o jogador local com client-side prediction e reconciliação de posição.
//
// Client-Side Prediction (por que?):
//   Em um servidor autoritativo a 20Hz (50ms de ciclo), se esperarmos a confirmação
//   do servidor para mover, o jogador parece "lagado" mesmo com baixa latência.
//   A solução é: mover localmente imediatamente, e corrigir suavemente se o servidor
//   discordar. Isso é o que fazem Minecraft, Fortnite, Albion Online, etc.
//
// Fluxo:
//   1. WASD pressionado → calcula nova posição localmente → aplica imediatamente
//   2. Envia "player:move" ao servidor com a posição local
//   3. Servidor retorna posição autoritativa via "world:update"
//   4. Se diferença > threshold → interpola suavemente (não teletransporta)
//
// Conversão de coordenadas:
//   Servidor usa pixels: (x, y) com max (2400, 1800) — sistema 2D, sem elevação
//   Unity usa unidades: (x, y, z) — Y vem do terreno via GroundSampler (raycast)
//   Divisor XZ: 50  →  serverX / 50f = unityX,  serverY / 50f = unityZ
//   Y (altura): amostrado do terreno Unity a cada frame — puramente visual, servidor não sabe

using System;
using UnityEngine;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG.Player
{
    /// <summary>
    /// Direções cardinais e diagonais do movimento, usadas para animação futura.
    /// Nomeadas em inglês para facilitar correspondência com assets de animação.
    /// </summary>
    public enum MoveDirection { None, North, NorthEast, East, SouthEast, South, SouthWest, West, NorthWest }

    public class PlayerController : MonoBehaviour
    {
        // ─── Constantes de conversão ──────────────────────────────────────────────
        /// <summary>
        /// Divisor de escala entre servidor (pixels) e Unity (unidades).
        /// Servidor: MAP_W=2400px → Unity: 48 unidades. 2400/48 = 50.
        /// </summary>
        private const float COORD_SCALE = 50f;

        /// <summary>
        /// Velocidade em pixels/segundo no servidor. Convertida para unidades Unity
        /// multiplicando por 1/COORD_SCALE = 200/50 = 4 unidades/segundo.
        /// </summary>
        private const float SERVER_SPEED_PX = 200f;
        private const float UNITY_SPEED     = SERVER_SPEED_PX / COORD_SCALE; // 4 u/s

        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Reconciliação")]
        [Tooltip("Distância mínima (unidades Unity) para iniciar correção de posição. " +
                 "50px no servidor = 1 unidade Unity. Threshold de 2u = 100px de drift.")]
        [SerializeField] private float reconcileThreshold = 2f;

        [Tooltip("Velocidade da interpolação de correção (0=sem correção, 1=snap imediato). " +
                 "0.2 por frame = suave mas responsivo.")]
        [SerializeField] [Range(0.01f, 1f)] private float reconcileLerpSpeed = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool showDebugGizmos = true;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private Vector3         _serverAuthPosition;   // Última posição confirmada pelo servidor
        private bool            _hasServerPosition;    // Se já recebemos ao menos uma posição
        private MoveDirection   _currentDirection;
        private bool            _isMoving;
        private NetworkManager  _net;

        // Para enviar só quando há input (não spammar o servidor a cada frame parado)
        private Vector3         _lastSentPosition;
        private const float     SEND_THRESHOLD = 0.01f; // Mínimo de deslocamento para enviar

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _net = NetworkManager.Instance;

            if (_net == null)
                Debug.LogError("[PlayerController] NetworkManager não encontrado! Coloque na cena.");

            // Ouve o evento world:update para receber posição autoritativa
            // GameManager injeta a posição inicial; aqui tratamos atualizações contínuas
        }

        private void Update()
        {
            if (_net == null || !_net.IsConnected) return;

            HandleInput();
        }

        // ─── Input e movimento local ──────────────────────────────────────────────
        private void HandleInput()
        {
            // Lê input bruto (sem normalização ainda)
            float horizontal = Input.GetAxisRaw("Horizontal"); // A/D ou ←/→
            float vertical   = Input.GetAxisRaw("Vertical");   // W/S ou ↑/↓

            _isMoving = horizontal != 0f || vertical != 0f;

            if (!_isMoving)
            {
                _currentDirection = MoveDirection.None;
                return;
            }

            // Normaliza o vetor para evitar movimento diagonal mais rápido
            Vector2 inputDir = new Vector2(horizontal, vertical).normalized;

            // Em câmera isométrica Y=45°, o input "W" deve mover na diagonal isométrica,
            // não para cima na tela. Rotacionamos o input 45° para alinhar com a câmera.
            // Com câmera fixa Y=45°, o "norte" na tela é a diagonal (x+, z+) no mundo.
            Vector3 worldDir = IsoDirection(inputDir);

            // Move localmente (prediction) — aplica antes da confirmação do servidor
            // Só atualiza X e Z; Y será corrigido pelo SnapToGround abaixo
            Vector3 next = transform.position + worldDir * UNITY_SPEED * Time.deltaTime;

            // Clamp dentro dos limites do mapa (X e Z)
            next = ClampToMap(next);

            // Snaupa ao terreno: Y calculado por raycast sobre a geometria do chão.
            // Fazemos isso a cada frame pois o jogador pode estar subindo/descendo rampas.
            next.y = GroundSampler.GetHeight(next.x, next.z);

            transform.position = next;

            // Determina direção para animação
            _currentDirection = GetMoveDirection(worldDir);

            // Envia ao servidor apenas se moveu o suficiente (evita flood)
            if (Vector3.Distance(transform.position, _lastSentPosition) >= SEND_THRESHOLD)
            {
                SendMoveToServer();
                _lastSentPosition = transform.position;
            }
        }

        /// <summary>
        /// Converte input 2D em direção 3D alinhada à câmera isométrica (Y=45°).
        /// Na câmera iso com Y=45°: input "up" (vertical+) → mundo (X+, Z-)
        ///                          input "right" (horizontal+) → mundo (X+, Z+)
        /// </summary>
        private static Vector3 IsoDirection(Vector2 input)
        {
            // Rotaciona 45° para alinhar com o ângulo da câmera
            // Isso faz W mover "ao norte" na perspectiva isométrica
            return new Vector3(
                x: input.x + input.y,
                y: 0f,
                z: -input.x + input.y
            ).normalized;
        }

        // ─── Envio de posição ao servidor ────────────────────────────────────────
        private void SendMoveToServer()
        {
            // Converte posição Unity de volta para coordenadas do servidor (pixels)
            float serverX = transform.position.x * COORD_SCALE;
            float serverY = transform.position.z * COORD_SCALE;

            string dirString = DirectionToString(_currentDirection);

            // Monta JSON manualmente — evita reflection do JsonUtility para estruturas simples
            string json = $"{{\"x\":{serverX:F1},\"y\":{serverY:F1},\"dir\":\"{dirString}\"}}";
            _net.Emit("player:move", json);
        }

        // ─── Reconciliação com servidor ───────────────────────────────────────────

        /// <summary>
        /// Chamado pelo GameManager quando recebe a posição autoritativa do servidor.
        /// serverX, serverY estão em pixels (coordenadas do servidor).
        /// </summary>
        public void ApplyServerPosition(float serverX, float serverY)
        {
            // Usa GroundSampler para obter Y real do terreno na posição autoritativa.
            // O servidor envia apenas (x, y) 2D — a altura é determinada pelo cliente.
            _serverAuthPosition = GroundSampler.ServerToUnity(serverX, serverY);
            _hasServerPosition  = true;

            float drift = Vector3.Distance(transform.position, _serverAuthPosition);

            if (drift > reconcileThreshold)
            {
                // Drift significativo: não faz snap (jarring), interpola suavemente.
                // Se o servidor diz que estamos em outro lugar, é porque houve desync —
                // o lerp gradual é imperceptível ao jogador, mas corrige o problema.
                // O lerp é aplicado em LateUpdate para não brigar com o HandleInput.
            }
            else
            {
                // Drift dentro do tolerável — não corrige nada.
                // A diferença de < 2 unidades é imperceptível ao jogador.
            }
        }

        private void LateUpdate()
        {
            // Aplicamos reconciliação em LateUpdate para que HandleInput já tenha rodado.
            if (!_hasServerPosition) return;

            // Compara drift apenas no plano XZ — ignoramos Y porque altura é visual e
            // muda constantemente com o terreno. Reconcilar Y causaria jitter.
            Vector2 localXZ  = new Vector2(transform.position.x, transform.position.z);
            Vector2 serverXZ = new Vector2(_serverAuthPosition.x, _serverAuthPosition.z);
            float drift = Vector2.Distance(localXZ, serverXZ);

            if (drift > reconcileThreshold)
            {
                // Interpola XZ suavemente; Y vem do terreno na posição interpolada
                Vector3 lerped = Vector3.Lerp(transform.position, _serverAuthPosition, reconcileLerpSpeed);
                lerped.y = GroundSampler.GetHeight(lerped.x, lerped.z);
                transform.position = lerped;
            }
        }

        // ─── Conversão de coordenadas ─────────────────────────────────────────────

        /// <summary>
        /// Servidor (pixels) → Unity (unidades) com altura do terreno.
        /// Y servidor → Z Unity; altura Unity Y via GroundSampler.
        /// </summary>
        public static Vector3 ServerToUnity(float sx, float sy)
            => GroundSampler.ServerToUnity(sx, sy, COORD_SCALE);

        /// <summary>Unity (unidades) → Servidor (pixels).</summary>
        public static (float sx, float sy) UnityToServer(Vector3 pos)
            => (pos.x * COORD_SCALE, pos.z * COORD_SCALE);

        // ─── Utilitários ──────────────────────────────────────────────────────────
        private static Vector3 ClampToMap(Vector3 pos)
        {
            // MAP_W=2400, MAP_H=1800 em pixels → 48, 36 em unidades Unity
            const float mapMaxX = 2400f / COORD_SCALE; // 48
            const float mapMaxZ = 1800f / COORD_SCALE; // 36
            // Y não é clampado aqui — vem do terreno via GroundSampler após o clamp XZ
            return new Vector3(
                Mathf.Clamp(pos.x, 0f, mapMaxX),
                pos.y,
                Mathf.Clamp(pos.z, 0f, mapMaxZ)
            );
        }

        private static MoveDirection GetMoveDirection(Vector3 dir)
        {
            // Usa atan2 para mapear o vetor de movimento para uma das 8 direções
            float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            // Normaliza para [0, 360)
            if (angle < 0) angle += 360f;

            return angle switch
            {
                < 22.5f    => MoveDirection.North,
                < 67.5f    => MoveDirection.NorthEast,
                < 112.5f   => MoveDirection.East,
                < 157.5f   => MoveDirection.SouthEast,
                < 202.5f   => MoveDirection.South,
                < 247.5f   => MoveDirection.SouthWest,
                < 292.5f   => MoveDirection.West,
                < 337.5f   => MoveDirection.NorthWest,
                _          => MoveDirection.North
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

            // Posição autoritativa do servidor — vermelho
            if (_hasServerPosition)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(_serverAuthPosition, 0.3f);
                Gizmos.DrawLine(transform.position, _serverAuthPosition);
            }
        }

        // ─── Propriedades para outros sistemas ───────────────────────────────────
        public MoveDirection CurrentDirection => _currentDirection;
        public bool IsMoving => _isMoving;
    }
}
