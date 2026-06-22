// PlayerAnimator.cs
// Animação procedural de walking — suporta DOIS tipos de personagem:
//
//  1. StickManBuilder (primitivos)
//     Bones buscados: ArmL, ArmR, LegL, LegR, Body, Weapon
//
//  2. Universal Base Characters FBX
//     Bones buscados: upperarm_l/r, thigh_l/r, spine_01, Head
//     Naming convention do FBX: lowercase com sufixo _l/_r (ex: upperarm_l)
//
// Não usa Animator, AnimationClip nem assets externos — calcula rotações via Mathf.Sin.
// Detecta automaticamente qual tipo de rig está presente e anima os bones corretos.
//
// Movimentos animados:
//   Walk: braços e pernas oscilam em fase oposta (natural walking cycle)
//   Idle: respiração suave (body sobe/desce levemente) + bob muito sutil
//   Attack: oscilação do braço direito para frente (ativado via TriggerAttack())

using UnityEngine;
using MMORPG.Player;

namespace MMORPG
{
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        // ─── Inspector ────────────────────────────────────────────────────────────
        [Header("Walking")]
        [SerializeField] private float walkFrequency = 6f;   // ciclos por segundo
        [SerializeField] private float limbAngle     = 28f;  // graus de oscilação (braços/pernas)
        [SerializeField] private float bodyBob       = 0.04f; // amplitude do bob vertical

        [Header("Idle")]
        [SerializeField] private float idleBreathFreq  = 0.8f;  // ciclos/s da "respiração"
        [SerializeField] private float idleBreathAngle = 3f;    // graus leves de inclinação

        [Header("Attack")]
        [SerializeField] private float attackDuration = 0.35f; // segundos de animação de ataque

        // ─── Transforms dos membros ───────────────────────────────────────────────
        private Transform _armL;
        private Transform _armR;
        private Transform _legL;
        private Transform _legR;
        private Transform _body;
        private Transform _weapon;

        // ─── Estado ───────────────────────────────────────────────────────────────
        private PlayerController _ctrl;
        private float _walkPhase;     // fase acumulada do ciclo de walking
        private float _idlePhase;     // fase acumulada da respiração idle
        private float _attackTimer;   // countdown do ataque (0 = sem ataque)
        private Vector3 _bodyLocalPosBase;

        // ─── Unity Lifecycle ──────────────────────────────────────────────────────
        private void Start()
        {
            _ctrl = GetComponent<PlayerController>();
            FindLimbs();
        }

        private void LateUpdate()
        {
            // LateUpdate: roda depois do PlayerController mover o transform
            if (_ctrl == null) return;

            bool moving = _ctrl.IsMoving;

            if (moving)
                AnimateWalk();
            else
                AnimateIdle();

            if (_attackTimer > 0f)
                AnimateAttack();
        }

        // ─── Animação de Walking ──────────────────────────────────────────────────
        private void AnimateWalk()
        {
            _walkPhase += Time.deltaTime * walkFrequency * Mathf.PI * 2f;
            _idlePhase  = 0f; // reseta idle ao andar

            float sin = Mathf.Sin(_walkPhase);

            // Pernas: fase oposta (LegR avança quando LegL recua)
            if (_legL) _legL.localRotation = Quaternion.Euler( sin * limbAngle, 0f, 0f);
            if (_legR) _legR.localRotation = Quaternion.Euler(-sin * limbAngle, 0f, 0f);

            // Braços: inverso das pernas (contrapeso natural)
            if (_armL) _armL.localRotation = Quaternion.Euler(-sin * limbAngle * 0.65f, 0f, 0f);
            if (_armR) _armR.localRotation = Quaternion.Euler( sin * limbAngle * 0.65f, 0f, 0f);

            // Bob vertical do corpo (sobe 2x por ciclo completo)
            if (_body)
            {
                float bob = Mathf.Abs(Mathf.Sin(_walkPhase)) * bodyBob;
                Vector3 bp = _bodyLocalPosBase;
                _body.localPosition = new Vector3(bp.x, bp.y + bob, bp.z);
            }
        }

        // ─── Animação Idle (respiração) ───────────────────────────────────────────
        private void AnimateIdle()
        {
            _idlePhase += Time.deltaTime * idleBreathFreq * Mathf.PI * 2f;
            _walkPhase  = 0f;

            float sin = Mathf.Sin(_idlePhase);

            // Reset suave dos membros para pose neutra
            float smooth = 1f - Mathf.Exp(-Time.deltaTime * 8f); // approx lerp
            if (_legL) _legL.localRotation = Quaternion.Slerp(_legL.localRotation, Quaternion.identity, smooth);
            if (_legR) _legR.localRotation = Quaternion.Slerp(_legR.localRotation, Quaternion.identity, smooth);
            if (_armL) _armL.localRotation = Quaternion.Slerp(_armL.localRotation, Quaternion.identity, smooth);
            if (_armR) _armR.localRotation = Quaternion.Slerp(_armR.localRotation, Quaternion.identity, smooth);

            // Respiração no body (inclinação frontal suave)
            if (_body)
            {
                float angle = sin * idleBreathAngle;
                _body.localRotation = Quaternion.Euler(angle, 0f, 0f);

                // Bob muito suave
                float bob = (sin * 0.5f + 0.5f) * bodyBob * 0.4f;
                Vector3 bp = _bodyLocalPosBase;
                _body.localPosition = new Vector3(bp.x, bp.y + bob, bp.z);
            }
        }

        // ─── Animação de Ataque ───────────────────────────────────────────────────
        private void AnimateAttack()
        {
            _attackTimer -= Time.deltaTime;

            // Normalizado 0→1→0 (meio ciclo de seno)
            float t    = 1f - (_attackTimer / attackDuration);
            float swing = Mathf.Sin(t * Mathf.PI) * 70f; // arco de 70° para frente

            if (_armR)   _armR.localRotation   = Quaternion.Euler(-swing, 0f, 0f);
            if (_weapon) _weapon.localRotation  = Quaternion.Euler(-swing * 0.5f + 10f, 0f, 8f);

            if (_attackTimer <= 0f)
            {
                _attackTimer = 0f;
                if (_armR)  _armR.localRotation  = Quaternion.identity;
                if (_weapon) _weapon.localRotation = Quaternion.Euler(10f, 0f, 8f);
            }
        }

        // ─── API Pública ──────────────────────────────────────────────────────────

        /// <summary>Dispara animação de ataque (chamada pelo GameManager ao usar skill).</summary>
        public void TriggerAttack()
        {
            _attackTimer = attackDuration;
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Busca os transforms dos membros por nome na hierarquia filha.
        /// Tenta primeiro os nomes do StickManBuilder; se não encontrar,
        /// usa os nomes dos bones do FBX (Universal Base Characters).
        /// </summary>
        private void FindLimbs()
        {
            // ── StickMan (primitivos Unity) ───────────────────────────────────────
            _armL   = FindChild("ArmL");
            _armR   = FindChild("ArmR");
            _legL   = FindChild("LegL");
            _legR   = FindChild("LegR");
            _body   = FindChild("Body");
            _weapon = FindChild("Weapon");

            // ── FBX (Universal Base Characters) ──────────────────────────────────
            // Fallback para os bones do FBX caso o StickMan não esteja presente.
            // Nomes extraídos do arquivo Superhero_Male/Female_FullBody.fbx.
            if (_armL == null) _armL = FindChild("upperarm_l");
            if (_armR == null) _armR = FindChild("upperarm_r");
            if (_legL == null) _legL = FindChild("thigh_l");
            if (_legR == null) _legR = FindChild("thigh_r");
            if (_body == null) _body = FindChild("spine_01");

            // Salva posição base do body para o bob não acumular drift
            if (_body != null)
                _bodyLocalPosBase = _body.localPosition;
        }

        /// <summary>
        /// Busca recursivamente um filho pelo nome exato.
        /// </summary>
        private Transform FindChild(string childName)
        {
            // Busca direta no nível 1 (GetChild por nome não existe no Unity — usamos Find)
            var t = transform.Find(childName);
            if (t != null) return t;

            // Busca recursiva nos demais níveis
            return FindChildRecursive(transform, childName);
        }

        private static Transform FindChildRecursive(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindChildRecursive(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
