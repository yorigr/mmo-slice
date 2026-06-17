// HUD.cs
// Interface básica do jogador: HP, mana, nome, level e ping.
//
// Design:
//   O HUD ouve eventos do WorldState e NetworkManager — não faz polling.
//   Isso significa que o HUD só atualiza quando algo mudou, economizando
//   ciclos de CPU que seriam gastos em Update() verificando valores iguais.
//
// Hierarquia de UI esperada na cena Game.unity:
//   Canvas (Screen Space - Overlay)
//   └── HUD (este script)
//       ├── HPBar (Slider) com Fill Image
//       ├── ManaBar (Slider) com Fill Image
//       ├── PlayerNameText (TMP_Text)
//       ├── LevelText (TMP_Text)
//       └── PingText (TMP_Text)
//
// TMPro (TextMeshPro) é usado porque é o padrão do Unity 6 e tem melhor
// qualidade de renderização que o legado UI.Text.

using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using MMORPG.Network;
using MMORPG.World;

namespace MMORPG.UI
{
    public class HUD : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Barras de status")]
        [Tooltip("Slider para HP. Max Value = 1 (usamos fillAmount normalizado).")]
        [SerializeField] private Slider hpBar;
        [SerializeField] private Image  hpFill;   // Image do fill para mudar cor dinamicamente
        [SerializeField] private Slider manaBar;
        [SerializeField] private Image  manaFill;

        [Header("Textos")]
        [SerializeField] private TMP_Text playerNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text pingText;

        [Header("Cores HP")]
        [Tooltip("Cor da barra quando HP está alto (> 60%).")]
        [SerializeField] private Color hpColorHigh   = new Color(0.2f, 0.8f, 0.2f); // Verde
        [Tooltip("Cor da barra quando HP está médio (30% - 60%).")]
        [SerializeField] private Color hpColorMedium = new Color(0.9f, 0.7f, 0.1f); // Amarelo
        [Tooltip("Cor da barra quando HP está baixo (< 30%).")]
        [SerializeField] private Color hpColorLow    = new Color(0.9f, 0.2f, 0.1f); // Vermelho

        [Header("Atualização de ping")]
        [Tooltip("Intervalo de atualização do display de ping (segundos). " +
                 "Atualizar todo frame seria visual ruído.")]
        [SerializeField] private float pingUpdateInterval = 1f;

        // ─── Estado interno ───────────────────────────────────────────────────────
        private NetworkManager _net;
        private WorldState     _world;
        private float          _pingUpdateTimer;
        private int            _currentHp;
        private int            _currentMaxHp;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _net   = NetworkManager.Instance;
            _world = WorldState.Instance;

            if (_net == null)   Debug.LogError("[HUD] NetworkManager não encontrado.");
            if (_world == null) Debug.LogError("[HUD] WorldState não encontrado.");

            // Ouve atualizações do mundo — chamado 20x/s pelo servidor
            if (_world != null)
                _world.OnWorldUpdated += HandleWorldUpdated;

            // Estado inicial: barras no mínimo até receber dados reais
            SetHP(0, 100);
            SetMana(0, 100);
            SetPlayerName("---");
            SetLevel(1);
            SetPing(0);
        }

        private void OnDestroy()
        {
            // Remove listener para evitar referências perdidas após destruição
            if (_world != null)
                _world.OnWorldUpdated -= HandleWorldUpdated;
        }

        private void Update()
        {
            // Atualiza ping na frequência definida (não todo frame)
            _pingUpdateTimer += Time.deltaTime;
            if (_pingUpdateTimer >= pingUpdateInterval)
            {
                _pingUpdateTimer = 0f;
                if (_net != null)
                    SetPing(Mathf.RoundToInt(_net.LatencyMs));
            }
        }

        // ─── Handlers de eventos ──────────────────────────────────────────────────
        private void HandleWorldUpdated(System.Collections.Generic.HashSet<string> updatedIds)
        {
            // Procura o jogador local no estado do mundo e atualiza o HUD
            if (_world == null || !_world.TryGetLocalPlayer(out var localPlayer)) return;

            SetHP(localPlayer.hp, localPlayer.maxHp);

            // Mana não vem no world:update básico — adicionar quando o servidor enviar
            // Por enquanto, mantemos o valor atual (não regride sozinho)
        }

        // ─── Setters de UI ────────────────────────────────────────────────────────

        /// <summary>Atualiza a barra e texto de HP. hp e maxHp em valores absolutos.</summary>
        public void SetHP(int hp, int maxHp)
        {
            _currentHp    = hp;
            _currentMaxHp = maxHp;

            float ratio = maxHp > 0 ? (float)hp / maxHp : 0f;

            if (hpBar != null)
                hpBar.value = ratio;

            // Muda a cor da barra de acordo com o percentual de HP
            if (hpFill != null)
            {
                hpFill.color = ratio switch
                {
                    > 0.6f => hpColorHigh,
                    > 0.3f => hpColorMedium,
                    _      => hpColorLow
                };
            }
        }

        /// <summary>Atualiza a barra de mana. mana e maxMana em valores absolutos.</summary>
        public void SetMana(int mana, int maxMana)
        {
            float ratio = maxMana > 0 ? (float)mana / maxMana : 0f;

            if (manaBar != null)
                manaBar.value = ratio;

            // Mana usa cor fixa (azul) — definida no fill material no Editor
        }

        /// <summary>Exibe o nome do personagem.</summary>
        public void SetPlayerName(string playerName)
        {
            if (playerNameText != null)
                playerNameText.text = playerName;
        }

        /// <summary>Exibe o level do personagem.</summary>
        public void SetLevel(int level)
        {
            if (levelText != null)
                levelText.text = $"Lv {level}";
        }

        /// <summary>Exibe a latência em ms. Usa cor para indicar qualidade da conexão.</summary>
        public void SetPing(int pingMs)
        {
            if (pingText == null) return;

            pingText.text = $"{pingMs} ms";

            // Cor visual da qualidade do ping
            pingText.color = pingMs switch
            {
                < 80   => Color.green,
                < 150  => Color.yellow,
                < 300  => new Color(1f, 0.5f, 0f), // Laranja
                _      => Color.red
            };
        }
    }
}
