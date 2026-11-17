using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人的弹丸：直线飞行，每帧用球扫检查这一帧走过的线段。
    /// 只有命中玩家才结算伤害；撞到别的东西直接消失。
    /// 默认的命中层排除了敌人层（9）与射线忽略层（2），所以子弹不会误伤同类和自己。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [Header("Flight")]
        [SerializeField, Min(0.1f)]
        float speed = 14f;

        [SerializeField, Min(0.05f)]
        float lifetime = 1.5f;

        [Tooltip("球扫半径，和模型的视觉半径一致")]
        [SerializeField, Min(0.01f)]
        float radius = 0.12f;

        [SerializeField]
        LayerMask hitMask = ~((1 << 9) | (1 << 2));

        float damage;
        float age;
        Vector3 direction;

        /// <summary>发射：方向归一化，伤害由发射方给出。</summary>
        public void Launch(Vector3 flightDirection, float amount)
        {
            direction = flightDirection.normalized;
            damage = amount;
            age = 0f;

            if (direction.sqrMagnitude > 0f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        void Update()
        {
            if (direction.sqrMagnitude <= 0f)
            {
                return;
            }

            age += Time.deltaTime;

            if (age >= lifetime)
            {
                Destroy(gameObject);

                return;
            }

            Vector3 step = direction * (speed * Time.deltaTime);
            float distance = step.magnitude;

            if (distance <= 1e-4f)
            {
                return;
            }

            if (Physics.SphereCast(transform.position, radius, direction, out RaycastHit hit, distance, hitMask, QueryTriggerInteraction.Ignore))
            {
                PlayerCoherence coherence = hit.collider.GetComponentInParent<PlayerCoherence>();
                coherence?.Drain(damage);

                Destroy(gameObject);

                return;
            }

            transform.position += step;
        }
    }
}
