using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人的移动决策：朝目标推进，进入攻击射程后停下并让攻击组件出手。
    /// 每只敌人只有一种攻击；位移始终交给 CharacterMotor 执行。
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
        EnemyAttack attack;

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

            if (attack != null && attack.InRange)
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

            if (attack == null)
            {
                attack = GetComponent<EnemyAttack>();
            }
        }
    }
}
