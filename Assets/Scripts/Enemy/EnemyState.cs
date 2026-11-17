using UnityEngine;

namespace Decoherence
{
    public enum EnemyPhase
    {
        Idle = 0,

        Chase = 1,

        Attack = 2,

        Dead = 3,

    }

    /// <summary>
    /// 敌人状态：行为阶段、目标、移动速度。
    /// 数值写在这里，行为逻辑由敌人自己的决策组件读取。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyState : CharacterState
    {
        [Header("Behaviour")]
        [SerializeField]
        EnemyPhase phase = EnemyPhase.Chase;

        [SerializeField, HideInInspector]
        Transform target;

        [Header("Movement")]
        [SerializeField, Min(0f)]
        float moveSpeed = 3.2f;

        public EnemyPhase Phase
        {
            get => phase;
            set => phase = value;
        }

        public Transform Target => target;

        public override float MaxSpeed => moveSpeed;

        void Awake()
        {
            if (target == null)
            {
                target = FindFirstObjectByType<PlayerState>()?.transform;
            }
        }
    }
}
