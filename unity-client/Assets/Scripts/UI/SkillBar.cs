// SkillBar.cs
// Barra de habilidades com 6 slots (teclas Q/W/E/R/D/F — estilo Albion Online).
//
// Funcionamento:
//   1. GameManager chama Configure(skills) após receber player:joined.
//   2. Q/W/E/R/D/F ativa o slot correspondente:
//      Q=weapon_Q, W=weapon_W, E=weapon_E, R=chest_R, D=head_D, F=boots_F
//   3a. Skills não-AOE: disparo imediato com posição do mouse.
//   3b. Skills AOE (type contém "aoe" ou range > 0):
//       - 1ª tecla → entra em "aim mode" (preview circular no terreno segue o mouse)
//       - Clique esquerdo → confirma e dispara; Escape/mesma tecla → cancela
//   4. O cliente faz raycast ao chão para obter (tx, ty) e emite skill:use.
//   5. Cooldown local (pré-emptivo) é aplicado; corrigido pelo skill:result do servidor.
//   6. Ao disparar, SkillEffectSystem.SpawnImpact gera anel expansivo no ponto de impacto.
//
// UI (criada proceduralmente):
//   Canvas (Screen Space – Overlay, gerado em Awake se não existir)
//   └── SkillBarPanel (HorizontalLayout)
//       └── SlotN × 6
//           ├── Background (Image, cinza/dourado; brilhante quando em aim mode)
//           ├── CooldownOverlay (Image, fillAmount radial360, semitransparente)
//           ├── KeyText (TMP "Q"/"W"/"E"/"R"/"D"/"F")
//           └── NameText (TMP, nome da skill)
//
// Integração:
//   Adicione este script ao GameManager (ou a qualquer GO persistente).
//   Não requer referências manuais no Inspector — se Canvas for nulo, cria automaticamente.
//
// Nota sobre teclas:
//   Q/W/E/R/D/F são o mapeamento padrão. O sistema será configurável pelo jogador
//   via tela de configurações (KeyBindings) em versão futura.

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
        private readonly Image[]    _cdOverlays = new Image[6];
        private readonly TMP_Text[] _nameTexts  = new TMP_Text[6];
        private readonly Image[]    _slotBgs    = new Image[6];   // fundo de cada slot

        // Aim mode — fluxo 2 passos para skills AOE
        // -1 = nenhuma skill em aim mode; 0..5 = índice do slot em aim
        private int _aimingSlot = -1;

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

        // Teclas de ativação dos 6 slots (estilo Albion: Q/W/E/R/D/F).
        // Serão configuráveis pelo jogador em versão futura (KeyBindings).
        private static readonly KeyCode[] SLOT_KEYS   = { KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.D, KeyCode.F };
        private static readonly string[]  SLOT_LABELS = { "Q",       "W",       "E",       "R",       "D",       "F"       };

        private void Update()
        {
            if (!enabled || _skills.Count == 0) return;
            if (_net != null && !_net.IsConnected) return;

            // ── Aim mode ativo ────────────────────────────────────────────────────
            if (_aimingSlot >= 0)
            {
                var sk = _skills[_aimingSlot];
                float radius = SkillEffectSystem.RangeToUnits(sk.range);
                Color color  = SkillTypeColor(sk);

                // Mantém o preview no cursor
                SkillEffectSystem.ShowAOEPreview(radius, color);

                // Clique esquerdo confirma o lançamento
                if (Input.GetMouseButtonDown(0))
                {
                    CancelAimMode();
                    TryCast(_aimingSlot, getMouseWorld: true);
                    return;
                }

                // Escape ou mesma tecla cancela
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(SLOT_KEYS[_aimingSlot]))
                {
                    CancelAimMode();
                    return;
                }

                // Impede processamento das teclas de slot enquanto em aim mode
                UpdateCooldownVisuals();
                return;
            }

            // ── Teclas de slot (fora do aim mode) ────────────────────────────────
            for (int i = 0; i < _skills.Count && i < 6; i++)
            {
                if (!Input.GetKeyDown(SLOT_KEYS[i])) continue;

                if (IsAOE(_skills[i]))
                    EnterAimMode(i);   // skill AOE → exibe preview antes de confirmar
                else
                    TryCast(i);        // skill direta → disparo imediato
            }

            UpdateCooldownVisuals();
        }

        // ─── Aim mode helpers ─────────────────────────────────────────────────────

        private bool IsAOE(SkillDef sk)
        {
            if (sk == null) return false;
            // Considera AOE: type contém "aoe" (ex: "aoe", "aoe_heal") OU range > 50px
            bool typeIsAOE = !string.IsNullOrEmpty(sk.type)
                             && sk.type.IndexOf("aoe", System.StringComparison.OrdinalIgnoreCase) >= 0;
            return typeIsAOE || sk.range > 50;
        }

        private Color SkillTypeColor(SkillDef sk)
        {
            if (sk == null) return Color.white;
            string t = sk.type ?? "";
            if (t.IndexOf("heal", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.20f, 0.90f, 0.30f); // verde
            if (t.IndexOf("buff", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return new Color(0.40f, 0.60f, 1.00f); // azul
            // Dano/padrão
            return new Color(1.00f, 0.30f, 0.15f);     // laranja-vermelho
        }

        private void EnterAimMode(int slot)
        {
            _aimingSlot = slot;
            // Destaca o slot visualmente (borda brilhante)
            if (_slotBgs[slot] != null)
                _slotBgs[slot].color = new Color(1f, 0.85f, 0.10f); // dourado
            Debug.Log($"[SkillBar] Aim mode: {_skills[slot].name} (AOE r={_skills[slot].range}px)");
        }

        private void CancelAimMode()
        {
            SkillEffectSystem.HideAOEPreview();
            if (_aimingSlot >= 0 && _slotBgs[_aimingSlot] != null)
                _slotBgs[_aimingSlot].color = new Color(0.14f, 0.14f, 0.14f); // restaura cinza
            _aimingSlot = -1;
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

            // Atualiza textos dos slots (6 slots)
            for (int i = 0; i < 6; i++)
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

        /// <summary>
        /// Tenta lançar a skill no slot.
        /// getMouseWorld=true garante a posição mais recente do mouse (usado após aim mode).
        /// </summary>
        private void TryCast(int slot, bool getMouseWorld = true)
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

            // Impact VFX no ponto de destino (anel expansivo no terreno)
            Vector3 worldTarget = new Vector3(tx / 50f, 0f, ty / 50f);
            float   impactR     = SkillEffectSystem.RangeToUnits(sk.range);
            SkillEffectSystem.SpawnImpact(worldTarget, impactR, SkillTypeColor(sk));

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

            for (int i = 0; i < _cdEnds.Length && i < 6; i++)
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
            // 6 slots × 80px + 5 gaps × 8px = 528px
            panelRect.sizeDelta = new Vector2(528f, 90f);

            var layout = panel.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleCenter;

            // 6 slots: weapon_Q(Q) W(W) E(E), chest_R(R), head_D(D), boots_F(F)
            for (int i = 0; i < 6; i++)
                CreateSlot(panel.transform, i);
        }

        private void CreateSlot(Transform parent, int index)
        {
            // Fundo do slot
            var slotGO   = NewRectGO($"Slot{index + 1}", parent);
            var slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(80f, 80f);

            var bg = slotGO.AddComponent<Image>();
            bg.color = new Color(0.14f, 0.14f, 0.14f, 0.88f);
            _slotBgs[index] = bg; // referência para highlight do aim mode

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
            cdImg.color         = new Color(0f, 0f, 0f, 0.65f);
            cdImg.type          = Image.Type.Filled;
            cdImg.fillMethod    = Image.FillMethod.Radial360;
            cdImg.fillOrigin    = (int)Image.Origin360.Top;
            cdImg.fillClockwise = true;
            cdImg.fillAmount    = 0f;
            _cdOverlays[index]  = cdImg;

            // Tecla de atalho (canto superior esquerdo)
            var keyGO   = NewRectGO("KeyText", slotGO.transform);
            var keyRect = keyGO.GetComponent<RectTransform>();
            keyRect.anchorMin = new Vector2(0f, 1f);
            keyRect.anchorMax = new Vector2(0f, 1f);
            keyRect.pivot     = new Vector2(0f, 1f);
            keyRect.anchoredPosition = new Vector2(4f, -4f);
            keyRect.sizeDelta        = new Vector2(20f, 20f);

            var keyTxt = keyGO.AddComponent<TextMeshProUGUI>();
            keyTxt.text      = SLOT_LABELS[index];
            keyTxt.fontSize  = 14f;
            keyTxt.fontStyle = FontStyles.Bold;
            keyTxt.color     = new Color(1f, 0.85f, 0.3f);
            keyTxt.alignment = TextAlignmentOptions.TopLeft;

            // Nome da skill (centro-inferior do slot)
            var nameGO   = NewRectGO("NameText", slotGO.transform);
            var nameRect = nameGO.GetComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot     = new Vector2(0.5f, 0f);
            nameRect.anchoredPosition = new Vector2(0f, 4f);
            nameRect.sizeDelta        = new Vector2(0f, 20f);

            var nameTxt = nameGO.AddComponent<TextMeshProUGUI>();
            nameTxt.text      = "";
            nameTxt.fontSize  = 11f;
            nameTxt.color     = new Color(0.90f, 0.88f, 0.82f);
            nameTxt.alignment = TextAlignmentOptions.Bottom;
            _nameTexts[index] = nameTxt;
        }

        // ─── Utilidade de UI ──────────────────────────────────────────────────────

        private static GameObject NewRectGO(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }
    }
}
