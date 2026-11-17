using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人攻击的公共部分：伤害、冷却、射程，以及“是否该出手”的判定。
    /// 具体出手方式由子类实现；攻击组件自己不移动。
    /// </summary>
    public abstract class EnemyAttack : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        EnemyState state;

        [SerializeField, HideInInspector]
        PlayerCoherence player;

        [Header("Attack")]
        [SerializeField, Min(0f)]
        float damage = 7f;

        [SerializeField, Min(0.01f)]
        float cooldown = 0.8f;

        [SerializeField, Min(0f)]
        float range = 0.9f;

        public bool IsReady => Time.time >= nextReadyTime;

        public bool InRange => DistanceToTarget() <= Mathf.Max(0f, range);

        /// <summary>射程，决策侧用它在多个攻击里挑"最近的那一招"。</summary>
        public float Range => range;

        /// <summary>单次伤害，弹丸需要把它传下去。</summary>
        public float Damage => damage;

        protected EnemyState State => state;

        protected PlayerCoherence Player => player;

        float nextReadyTime;

        void Awake()
        {
            ResolveReferences();
            OnAwaken();
        }

        /// <summary>条件满足就出手一次并进入冷却；返回这一帧是否真的命中。</summary>
        public abstract bool TryAttack();

        /// <summary>子类的初始化钩子（基类没有虚 Awake）。</summary>
        protected virtual void OnAwaken()
        {
        }

        protected void StartCooldown()
        {
            nextReadyTime = Time.time + Mathf.Max(0.01f, cooldown);
        }

        protected float DistanceToTarget()
        {
            if (state == null || state.Target == null)
            {
                return float.MaxValue;
            }

            Vector3 delta = state.Target.position - transform.position;
            delta.y = 0f;

            return delta.magnitude;
        }

        protected Vector3 DirectionToTarget()
        {
            if (state == null || state.Target == null)
            {
                return Vector3.zero;
            }

            Vector3 delta = state.Target.position - transform.position;
            delta.y = 0f;

            return delta.sqrMagnitude > 1e-6f ? delta.normalized : Vector3.zero;
        }

        protected void DrainPlayer()
        {
            player?.Drain(damage);
        }

        void ResolveReferences()
        {
            if (state == null)
            {
                state = GetComponent<EnemyState>();
            }

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCoherence>();
            }
        }
    }
}
