using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 所有角色共有的状态：是否存活、是否可移动、速度上限。
    /// 具体角色（玩家/敌人）从这里派生，只往里加自己独有的状态。
    /// </summary>
    public abstract class CharacterState : MonoBehaviour
    {
        [SerializeField]
        bool isAlive = true;

        [SerializeField]
        bool canMove = true;

        public bool IsAlive => isAlive;

        public bool CanMove => canMove;

        /// <summary>当前速度上限，由具体角色按自己的状态给出。</summary>
        public abstract float MaxSpeed { get; }

        /// <summary>标记死亡：不再存活、也不再能移动。</summary>
        public void MarkDead()
        {
            isAlive = false;
            canMove = false;
        }
    }
}
