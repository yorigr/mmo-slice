// UIManager.cs
// Orquestra os 4 painéis de UI do jogo: Status (P), PaperDoll (C),
// SkillTree (K) e Inventory (I).
//
// Responsabilidade:
//   - Cria os 4 painéis proceduralmente (zero configuração no Editor).
//   - Hotkeys globais para abrir/fechar painéis.
//   - Garante que apenas UM painel fique aberto por vez (abrir um fecha o anterior).
//   - Esc fecha todos.
//
// Por que um singleton dedicado e não lógica espalhada em cada painel?
//   Centralizar o controle de "qual painel está aberto" evita estados inconsistentes
//   (dois painéis sobrepostos) e dá um ponto único para outros sistemas pedirem
//   OpenPanel/CloseAll (ex: um NPC abrir a loja, o tutorial destacar o inventário).
//
// Integração:
//   GameManager cria este componente automaticamente em Start().

using UnityEngine;

namespace MMORPG.UI
{
    /// <summary>Identifica cada um dos 4 painéis controlados pelo UIManager.</summary>
    public enum PanelType { Status, PaperDoll, SkillTree, Inventory }

    public class UIManager : MonoBehaviour
    {
        // ─── Singleton ────────────────────────────────────────────────────────────
        public static UIManager Instance { get; private set; }

        // ─── Painéis ──────────────────────────────────────────────────────────────
        private StatusPanel    _status;
        private PaperDollPanel _paperDoll;
        private SkillTreePanel _skillTree;
        private InventoryPanel _inventory;

        // Painel atualmente aberto (null = nenhum).
        private PanelType? _open;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Cada painel cria sua própria Canvas e se auto-esconde no Awake/Start.
            _status    = gameObject.AddComponent<StatusPanel>();
            _paperDoll = gameObject.AddComponent<PaperDollPanel>();
            _skillTree = gameObject.AddComponent<SkillTreePanel>();
            _inventory = gameObject.AddComponent<InventoryPanel>();

            CloseAll();
        }

        private void Update()
        {
            // Hotkeys de toggle. Tecla pressionada com o painel já aberto = fecha.
            if (Input.GetKeyDown(KeyCode.P)) Toggle(PanelType.Status);
            if (Input.GetKeyDown(KeyCode.C)) Toggle(PanelType.PaperDoll);
            if (Input.GetKeyDown(KeyCode.K)) Toggle(PanelType.SkillTree);
            if (Input.GetKeyDown(KeyCode.I)) Toggle(PanelType.Inventory);
            if (Input.GetKeyDown(KeyCode.Escape)) CloseAll();
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Abre um painel, fechando qualquer outro que esteja aberto.</summary>
        public void OpenPanel(PanelType type)
        {
            CloseAll();
            SetVisible(type, true);
            _open = type;
        }

        /// <summary>Fecha todos os painéis.</summary>
        public void CloseAll()
        {
            SetVisible(PanelType.Status,    false);
            SetVisible(PanelType.PaperDoll, false);
            SetVisible(PanelType.SkillTree, false);
            SetVisible(PanelType.Inventory, false);
            _open = null;
        }

        /// <summary>Alterna a visibilidade: abre se fechado, fecha se já aberto.</summary>
        public void Toggle(PanelType type)
        {
            if (_open == type) CloseAll();
            else               OpenPanel(type);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────
        private void SetVisible(PanelType type, bool visible)
        {
            switch (type)
            {
                case PanelType.Status:    _status?.SetVisible(visible);    break;
                case PanelType.PaperDoll: _paperDoll?.SetVisible(visible); break;
                case PanelType.SkillTree: _skillTree?.SetVisible(visible); break;
                case PanelType.Inventory: _inventory?.SetVisible(visible); break;
            }
        }
    }
}
