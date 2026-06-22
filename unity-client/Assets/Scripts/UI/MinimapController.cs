// MinimapController.cs
// Minimap em tempo real usando uma câmera ortogonal apontada para baixo (top-down).
// Renderiza numa RenderTexture e exibe num RawImage no HUD (canto superior direito).
//
// Sem assets externos — tudo criado proceduralmente em Start().
//
// Como funciona:
//   1. Cria uma Camera filha do jogador, apontando para baixo (Euler 90°, 0°, 0°)
//   2. Essa câmera renderiza numa RenderTexture 256×256
//   3. Uma RawImage no Canvas exibe a RenderTexture com máscara circular
//   4. O jogador aparece como um ponto vermelho centralizado
//
// Uso: adicione ao GameManager ou ao GameObject do jogador.
// SetTarget() deve ser chamado pelo GameManager após spawnar o jogador local.

using UnityEngine;
using UnityEngine.UI;

namespace MMORPG.UI
{
    public class MinimapController : MonoBehaviour
    {
        // ─── Inspector (todos opcionais — criados proceduralmente se nulos) ────────
        [Header("Configurações do minimap")]
        [SerializeField] private int   renderSize     = 256;  // resolução da RenderTexture
        [SerializeField] private float cameraHeight   = 30f;  // altura da câmera acima do mapa
        [SerializeField] private float orthoSize      = 18f;  // raio de visão (unidades Unity)
        [SerializeField] private float mapDisplaySize = 160f; // tamanho do widget na tela (px)

        // ─── Estado interno ───────────────────────────────────────────────────────
        private Transform       _target;      // jogador local
        private Camera          _mapCamera;
        private RenderTexture   _rt;
        private RawImage        _display;
        private GameObject      _playerDot;   // ponto vermelho no centro do minimap

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            BuildCamera();
            BuildUI();
        }

        private void LateUpdate()
        {
            if (_target == null || _mapCamera == null) return;

            // Câmera segue o jogador mantendo altura fixa e olhando para baixo
            _mapCamera.transform.position = new Vector3(
                _target.position.x,
                _target.position.y + cameraHeight,
                _target.position.z
            );
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Define o jogador local a ser seguido. Chamado pelo GameManager.</summary>
        public void SetTarget(Transform target)
        {
            _target = target;
        }

        // ─── Criação da câmera ────────────────────────────────────────────────────
        private void BuildCamera()
        {
            _rt = new RenderTexture(renderSize, renderSize, 16, RenderTextureFormat.ARGB32);
            _rt.name = "MinimapRT";
            _rt.Create();

            var camGO = new GameObject("MinimapCamera");
            camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // olha para baixo
            camGO.transform.position = new Vector3(0f, cameraHeight, 0f);

            _mapCamera                     = camGO.AddComponent<Camera>();
            _mapCamera.orthographic        = true;
            _mapCamera.orthographicSize    = orthoSize;
            _mapCamera.targetTexture       = _rt;
            _mapCamera.cullingMask         = ~0;                           // renderiza tudo
            _mapCamera.clearFlags          = CameraClearFlags.SolidColor;
            _mapCamera.backgroundColor     = new Color(0.08f, 0.12f, 0.08f);
            _mapCamera.nearClipPlane       = 0.1f;
            _mapCamera.farClipPlane        = cameraHeight + 5f;
            _mapCamera.depth               = -2;  // renderiza antes da câmera principal
            _mapCamera.allowHDR            = false;
            _mapCamera.allowMSAA           = false;
        }

        // ─── Criação do widget de UI ──────────────────────────────────────────────
        private void BuildUI()
        {
            // Canvas raiz
            var canvasGO = new GameObject("MinimapCanvas");
            canvasGO.transform.SetParent(transform, false);
            // Não precisamos de DontDestroyOnLoad aqui — jogo usa uma única cena.

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 6;  // entre HUD (5) e SkillBar (10)

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGO.AddComponent<GraphicRaycaster>();

            // Container (canto superior direito)
            var container = NewRectGO("MinimapContainer", canvasGO.transform);
            var cRT = container.GetComponent<RectTransform>();
            cRT.anchorMin        = new Vector2(1f, 1f);
            cRT.anchorMax        = new Vector2(1f, 1f);
            cRT.pivot            = new Vector2(1f, 1f);
            cRT.anchoredPosition = new Vector2(-12f, -44f); // abaixo da top bar
            cRT.sizeDelta        = new Vector2(mapDisplaySize + 10f, mapDisplaySize + 24f);

            // Fundo escuro com borda dourada
            var bg = container.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 0.85f);

            // Borda dourada
            var borderGO = NewRectGO("Border", container.transform);
            var borderRT = borderGO.GetComponent<RectTransform>();
            borderRT.anchorMin = Vector2.zero;
            borderRT.anchorMax = Vector2.one;
            borderRT.offsetMin = new Vector2(-2f, -2f);
            borderRT.offsetMax = new Vector2(2f, 2f);
            borderGO.AddComponent<Image>().color = new Color(0.50f, 0.42f, 0.12f, 0.90f);
            borderGO.transform.SetAsFirstSibling();

            // Label "MAPA" acima do display
            var labelGO = NewRectGO("Label", container.transform);
            var labelRT = labelGO.GetComponent<RectTransform>();
            labelRT.anchorMin = new Vector2(0f, 1f);
            labelRT.anchorMax = new Vector2(1f, 1f);
            labelRT.pivot     = new Vector2(0.5f, 1f);
            labelRT.anchoredPosition = new Vector2(0f, -2f);
            labelRT.sizeDelta        = new Vector2(0f, 18f);
            var lbl = labelGO.AddComponent<TMPro.TextMeshProUGUI>();
            lbl.text      = "MAPA";
            lbl.fontSize  = 10f;
            lbl.color     = new Color(0.80f, 0.72f, 0.30f);
            lbl.alignment = TMPro.TextAlignmentOptions.Center;
            lbl.fontStyle = TMPro.FontStyles.Bold;

            // Display da RenderTexture
            var displayGO = NewRectGO("MinimapDisplay", container.transform);
            var displayRT = displayGO.GetComponent<RectTransform>();
            displayRT.anchorMin        = new Vector2(0f, 0f);
            displayRT.anchorMax        = new Vector2(1f, 1f);
            displayRT.offsetMin        = new Vector2(5f, 5f);
            displayRT.offsetMax        = new Vector2(-5f, -20f);

            _display = displayGO.AddComponent<RawImage>();
            _display.texture = _rt;

            // Ponto do jogador (quadrado vermelho no centro do minimap)
            _playerDot = NewRectGO("PlayerDot", displayGO.transform);
            var dotRT = _playerDot.GetComponent<RectTransform>();
            dotRT.anchorMin        = new Vector2(0.5f, 0.5f);
            dotRT.anchorMax        = new Vector2(0.5f, 0.5f);
            dotRT.pivot            = new Vector2(0.5f, 0.5f);
            dotRT.anchoredPosition = Vector2.zero;
            dotRT.sizeDelta        = new Vector2(6f, 6f);
            var dot = _playerDot.AddComponent<Image>();
            dot.color = new Color(1f, 0.2f, 0.2f, 1f); // vermelho

            // Anel (outline do ponto)
            var ringGO = NewRectGO("PlayerRing", displayGO.transform);
            var ringRT = ringGO.GetComponent<RectTransform>();
            ringRT.anchorMin        = new Vector2(0.5f, 0.5f);
            ringRT.anchorMax        = new Vector2(0.5f, 0.5f);
            ringRT.pivot            = new Vector2(0.5f, 0.5f);
            ringRT.anchoredPosition = Vector2.zero;
            ringRT.sizeDelta        = new Vector2(10f, 10f);
            var ring = ringGO.AddComponent<Image>();
            ring.color = new Color(1f, 1f, 1f, 0.8f);
            ringGO.transform.SetAsFirstSibling(); // vai atrás do ponto vermelho
        }

        // ─── Helper ───────────────────────────────────────────────────────────────
        private static GameObject NewRectGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
