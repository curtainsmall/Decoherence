using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 远程：目标进入射程后先蓄力，蓄力期间要求枪口到目标之间没有遮挡，
    /// 蓄满后开火（命中判定为瞬时命中）并进入冷却。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRangedAttack : EnemyAttack
    {
        [Header("Ranged")]
        [Tooltip("蓄力时长（秒）")]
        [SerializeField, Min(0f)]
        float windup = 0.6f;

        [SerializeField]
        LayerMask obstacleMask = ~0;

        Transform muzzle;

        float windupTimer;

        public override bool TryAttack()
        {
            if (!IsReady || !InRange || !HasLineOfFire())
            {
                windupTimer = 0f;
                return false;
            }

            windupTimer += Time.deltaTime;

            if (windupTimer < windup)
            {
                return false;
            }

            windupTimer = 0f;

            DrainPlayer();
            StartCooldown();

            return true;
        }

        protected override void OnAwaken()
        {
            Socket socket = Socket.Find(transform, SocketKey.Muzzle);

            muzzle = socket?.transform;
        }

        bool HasLineOfFire()
        {
            if (State == null || State.Target == null)
            {
                return false;
            }

            Vector3 origin = muzzle != null
                ? muzzle.position
                : transform.position + Vector3.up * 0.7f;

            Vector3 destination = State.Target.position + Vector3.up * 0.9f;
            Vector3 delta = destination - origin;
            float distance = delta.magnitude;

            if (distance <= 1e-3f)
            {
                return true;
            }

            if (!Physics.Raycast(origin, delta / distance, out RaycastHit hit, distance, obstacleMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // 第一个命中若是玩家自己，则视为无遮挡
            return hit.collider != null && hit.collider.transform.root == State.Target;
        }
    }
}
