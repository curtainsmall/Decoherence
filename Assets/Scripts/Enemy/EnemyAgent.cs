using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人的移动决策：朝目标推进，进入攻击射程后停下并让攻击组件出手。
    /// 一只敌人可以同时带多招（例如远程 + 近战），进入射程时选射程最短的那一招，
    /// 所以远距离会停火，贴脸会自动切成近战。位移始终交给 CharacterMotor 执行。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyAgent : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        EnemyState state;

        [SerializeField, HideInInspector]
        CharacterMotor motor;

        [SerializeField, HideInInspector]
        EnemyAttack[] attacks;

        void Awake()
        {
            ResolveReferences();
        }

        void Update()
        {
            if (state == null || motor == null)
            {
                return;
            }

            if (state.Phase == EnemyPhase.Dead || !state.CanMove)
            {
                motor.ClearIntent();
                return;
            }

            Transform target = state.Target;

            if (target == null)
            {
                motor.ClearIntent();
                return;
            }

            EnemyAttack attack = PickAttack();

            if (attack != null)
            {
                motor.ClearIntent();
                attack.TryAttack();
                state.Phase = EnemyPhase.Attack;
                return;
            }

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;

            Vector3 direction = delta.sqrMagnitude > 1e-4f ? delta.normalized : Vector3.zero;

            motor.SetIntent(direction * state.MaxSpeed);
            state.Phase = EnemyPhase.Chase;
        }

        /// <summary>射程内、射程最短的那一招；都不在射程内返回 null（该继续追）。</summary>
        EnemyAttack PickAttack()
        {
            if (attacks == null)
            {
                return null;
            }

            EnemyAttack picked = null;
            float pickedRange = float.MaxValue;

            foreach (EnemyAttack candidate in attacks)
            {
                if (candidate == null || !candidate.InRange || candidate.Range >= pickedRange)
                {
                    continue;
                }

                picked = candidate;
                pickedRange = candidate.Range;
            }

            return picked;
        }

        void ResolveReferences()
        {
            if (state == null)
            {
                state = GetComponent<EnemyState>();
            }

            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (attacks == null || attacks.Length == 0)
            {
                attacks = GetComponents<EnemyAttack>();
            }
        }
    }
}
