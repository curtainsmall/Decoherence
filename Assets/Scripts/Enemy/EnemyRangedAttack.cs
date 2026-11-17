using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 远程：目标进入射程后先蓄力，蓄力期间要求枪口到目标之间没有遮挡，
    /// 蓄满后从枪口发射弹丸；伤害只由弹丸命中结算，命中前玩家可以躲开。
    /// 瞄准点带提前量：玩家保持直线移动会被打中，变向才躲得掉。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyRangedAttack : EnemyAttack
    {
        [Header("Ranged")]
        [Tooltip("蓄力时长（秒）")]
        [SerializeField, Min(0f)]
        float windup = 0.6f;

        [Tooltip("弹丸 prefab，必须带 EnemyProjectile")]
        [SerializeField]
        GameObject projectile;

        [Tooltip("弹速，用于计算提前量")]
        [SerializeField, Min(1f)]
        float projectileSpeed = 14f;

        [SerializeField]
        LayerMask obstacleMask = ~0;

        Transform muzzle;
        CharacterMotor targetMotor;
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

            if (!Fire())
            {
                return false;
            }

            StartCooldown();

            return true;
        }

        protected override void OnAwaken()
        {
            Socket socket = Socket.Find(transform, SocketKey.Muzzle);

            muzzle = socket?.transform;
        }

        bool Fire()
        {
            if (projectile == null || State == null || State.Target == null)
            {
                return false;
            }

            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.7f;
            Vector3 direction = AimPoint(origin) - origin;

            if (direction.sqrMagnitude < 1e-4f)
            {
                return false;
            }

            direction.Normalize();

            GameObject instance = Instantiate(projectile, origin, Quaternion.LookRotation(direction));
            EnemyProjectile bullet = instance.GetComponent<EnemyProjectile>();

            if (bullet == null)
            {
                Destroy(instance);

                return false;
            }

            bullet.Launch(direction, Damage);

            return true;
        }

        Vector3 AimPoint(Vector3 origin)
        {
            Vector3 point = State.Target.position + Vector3.up * 0.9f;

            if (targetMotor == null)
            {
                targetMotor = State.Target.GetComponent<CharacterMotor>();
            }

            if (targetMotor == null)
            {
                return point;
            }

            float travel = Vector3.Distance(origin, point) / Mathf.Max(1f, projectileSpeed);

            return point + targetMotor.PlanarVelocity * travel;
        }

        bool HasLineOfFire()
        {
            if (State == null || State.Target == null)
            {
                return false;
            }

            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 0.7f;
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
