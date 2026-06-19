// SkillBar.cs
// Barra de habilidades com 5 slots (teclas 1–5).
//
// Funcionamento:
//   1. GameManager chama Configure(skills) após receber player:joined.
//   2. Tecla 1–5 ativa a skill correspondente.
//   3. O cliente faz raycast ao chão para obter (tx, ty) e emite skill:use.
//   4. Cooldown local (pré-emptivo) é aplicado; corrigido pelo skill:result do servidor.
//
// UI (criada proceduralmente):
//   Canvas (Screen Space – Overlay, gerado em Awake se não existir)
//   └── SkillBarPanel (HorizontalLayout)
//       └── SlotN × 5
//           ├── Background (Image, cinza/dourado)
//           ├── CooldownOverlay (Image, fillAmount radial360, semitransparente)
//           ├── KeyText (TMP "1"–"5")
//           └── NameText (TMP, nome da skill)
//
// Integração:
//   Adicione este script ao GameManager (ou a qualquer GO persistente).
//   Não requer referências manuais no Inspector — se Canvas for nulo, cria automaticamente.

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.Network;

namespace MMORPG.UI
{
    /// <summary>Definição de uma skill recebida do servidor no evento player:joined.</summary>
    [System.Serializable]
    public class SkillDef
    {
        public string id;
        public string name;
        public string type;
        public int    castTime;
        public int    cooldown;
        public int    mana;
        public int    stamina;
        public int    range;
        public int    damage;
        public string description;
    }

    public class SkillBar : MonoBehaviour
    {
        // ─── Inspector (opcional) ─────────────────────────────────────────────────
        [Header("Câmera (auto-detectada se nulo)")]
        [SerializeField] private Camera mainCamera;

        [Header("Máscara de camada do chão para raycast")]
        [SerializeField] private LayerMask groundMask = ~0;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private NetworkManager   _net;
        private List<SkillDef>  _skills  = new();
        private float[]          _cdEnds;    // Time.time em que o cooldown termina por slot

        // Referências aos elementos visuais dos slots
        private readonly Image[]    _cdOverlays = new Image[5];
        private readonly TMP_Text[] _nameTexts  = new TMP_Text[5];

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            _net = NetworkManager.Instance;
            if (mainCamera == null) mainCamera = Camera.main;
        }

        private void Start()
        {
            // Cria canvas procedural caso não haja UI montada manualmente
            CreateProceduralUI();
        }

        private void Update()
        {
            if (!enabled || _skills.Count == 0) return;
            if (_net != null && !_net.IsConnected) return;

            // Hotkeys 1–5
            for (int i = 0; i < _skills.Count && i < 5; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    TryCast(i);
            }

            UpdateCooldownVisuals();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>
        /// Configura o SkillBar com as skills do jogador.
        /// Chamado pelo GameManager logo após receber player:joined.
        /// </summary>
        public void Configure(List<SkillDef> skills)
        {
            _skills = skills ?? new List<SkillDef>();
            _cdEnds = new float[_skills.Count];

            // Atualiza textos dos slots
            for (int i = 0; i < 5; i++)
            {
                bool hasSkill = i < _skills.Count;
                if (_nameTexts[i] != null)
                    _nameTexts[i].text = hasSkill ? _skills[i].name : "";
                if (_cdOverlays[i] != null)
                    _cdOverlays[i].fillAmount = 0f;
            }

            Debug.Log($"[SkillBar] Configurado com {_skills.Count} skills.");
        }

        /// <summary>
        /// Registra cooldown confirmado pelo servidor (skill:result).
        /// Se o servidor rejeitar, reset o cooldown local do slot.
        /// </summary>
        public void OnSkillResult(string skillId, bool rejected, int cooldownMs)
        {
            for (int i = 0; i < _skills.Count; i++)
            {
                if (_skills[i].id != skillId) continue;

                if (rejected)
                {
                    // Servidor rejeitou — limpa cooldown local que foi pré-aplicado
                    _cdEnds[i] = 0f;
                    if (_cdOverlays[i] != null) _cdOverlays[i].fillAmount = 0f;
                }
                else
                {
                    // Sincroniza com cooldown autoritativo do servidor
                    float cdSec = cooldownMs / 1000f;
                    _cdEnds[i] = Time.time + cdSec;
                }
                break;
            }
        }

        // ─── Cast ─────────────────────────────────────────────────────────────────
        private void TryCast(int slot)
        {
            if (slot >= _skills.Count) return;

            // Verifica cooldown local
            if (Time.time < _cdEnds[slot])
            {
                float left = _cdEnds[slot] - Time.time;
                Debug.Log($"[SkillBar] {_skills[slot].name} em cooldown ({left:F1}s)");
                return;
            }

            var sk = _skills[slot];

            // Posição do mouse no mundo (plano XZ do servidor)
            (float tx, float ty) = GetMouseServerCoords();

            _net.Emit("skill:use", $"{{\"skillId\":\"{sk.id}\",\"tx\":{tx:F1},\"ty\":{ty:F1}}}");

            // Pré-aplica cooldown localmente para UX responsiva
            float cdPre = sk.cooldown / 1000f;
            _cdEnds[slot] = Time.time + cdPre;
            if (_cdOverlays[slot] != null)
                _cdOverlays[slot].fillAmount = 1f;

            Debug.Log($"[SkillBar] Usou {sk.name} → servidor ({tx:F0},{ty:F0})");
        }

        private (float tx, float ty) GetMouseServerCoords()
        {
            if (mainCamera != null)
            {
                Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);

                // Tenta acertar o terreno via layerMask
                if (Physics.Raycast(ray, out RaycastHit hit, 300f, groundMask))
                    return (hit.point.x * 50f, hit.point.z * 50f);

                // Fallback: intersecção com o plano y=0
                if (Mathf.Abs(ray.direction.y) > 0.001f)
                {
                    float t = -ray.origin.y / ray.direction.y;
                    if (t > 0f)
                    {
                        Vector3 p = ray.origin + ray.direction * t;
                        return (p.x * 50f, p.z * 50f);
                    }
                }
            }
            return (0f, 0f);
        }

        // ─── Visuals ──────────────────────────────────────────────────────────────
        private void UpdateCooldownVisuals()
        {
            if (_cdEnds == null) return;

            for (int i = 0; i < _cdEnds.Length && i < 5; i++)
            {
                if (_cdOverlays[i] == null) continue;

                float cdTotal = i < _skills.Count ? _skills[i].cooldown / 1000f : 1f;
                float cdLeft  = Mathf.Max(0f, _cdEnds[i] - Time.time);

                _cdOverlays[i].fillAmount = cdTotal > 0f ? cdLeft / cdTotal : 0f;
            }
        }

        // ─── Criação de UI Procedural ─────────────────────────────────────────────

        private void CreateProceduralUI()
        {
            // Cria Canvas raiz
            var canvasGO = new GameObject("SkillBarCanvas");
            canvasGO.transform.SetParent(transform, false);
            DontDestroyOnLoad(canvasGO);

            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            canvasGO.AddComponent<GraphicRaycaster>();

            // Painel principal — barra horizontal centralizada na parte inferior
            var panel = NewRectGO("SkillBarPanel", canvasGO.transform);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot     = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 20f);
            panelRect.sizeDelta = new Vector2(440f, 90f);

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // 5 slots
            for (int i = 0; i < 5; i++)
                CreateSlot(panel.transform, i);
        }

        private void CreateSlot(Transform parent, int index)
        {
            // Fundo do slot
            var slotGO   = NewRectGO($"Slot{index + 1}", parent);
            var slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(80f, 80f);

            var bg = slotGO.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

            // Borda colorida fina (skill ativa vs inativa — cor dourada)
            var borderGO   = NewRectGO("Border", slotGO.transform);
            var borderRect = borderGO.GetComponent<RectTransform>();
            borderRect.anchorMin = Vector2.zero;
            borderRect.anchorMax = Vector2.one;
            borderRect.offsetMin = new Vector2(-2f, -2f);
            borderRect.offsetMax = new Vector2(2f, 2f);
            var border = borderGO.AddComponent<Image>();
            border.color = new Color(0.6f, 0.5f, 0.1f, 0.9f);
            border.transform.SetAsFirstSibling();

            // Overlay de cooldown (Radial 360, sobreposto, preenchido 1→0)
            var cdGO   = NewRectGO("CooldownOverlay", slotGO.transform);
            var cdRect = cdGO.GetComponent<RectTransform>();
            cdRect.anchorMin = Vector2.zero;
            cdRect.anchorMax = Vector2.one;
            cdRect.offsetMin = Vector2.zero;
            cdRect.offsetMax = Vector2.zero;

            var cdImg = cdGO.AddComponent<Image>();
            cdImg.color       = new Color(0f, 0f, 0f, 0.65f);
            cdImg.type        = Image.Type.Filled;
            cdImg.fillMethod  = Image.FillMethod.Radial360;
            cdImg.fillOrigin  = (int)Image.Origin360.Top;
            cdImg.fillClockwise = true;
            cdImg.fillAmount  = 0f;
            _cdOverlays[index] = cdImg;

            // Tecla de atalho (canto superior esquerdo)
            var keyGO   = NewRectGO("KeyText", slotGO.transform);
            var keyRect = keyGO.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot     = new Vector2(0f, 1f);
            keyRect.anchoredPosition = new Vector2(4f, -2f);
            keyRect.sizeDelta = new Vector2(20f, 20f);

            var keyText = keyGO.AddComponent<TextMeshProUGUI>();
            keyText.text      = (index + 1).ToString();
            keyText.fontSize  = 13f;
            keyText.color     = new Color(0.9f, 0.8f, 0.3f);
            keyText.fontStyle = FontStyles.Bold;
            keyText.alignment = TextAlignmentOptions.TopLeft;

            // Nome da skill (centro-inferior)
            var nameGO   = NewRectGO("NameText", slotGO.transform);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0.45f);
            nameRect.offsetMin = new Vector2(2f, 2f);
            nameRect.offsetMax = new Vector2(-2f, 0f);

            var nameText = nameGO.AddComponent<TextMeshProUGUI>();
            nameText.text      = "";
            nameText.fontSize  = 9f;
            nameText.color     = Color.white;
            nameText.alignment = TextAlignmentOptions.Center;
            nameText.enableWordWrapping = true;
            _nameTexts[index] = nameText;
        }

        // ─── Utilitário ───────────────────────────────────────────────────────────
        private static GameObject NewRectGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.AddComponent<RectTransform>();
            go.transform.SetParent(parent, false);
            return go;
        }
    }
}
