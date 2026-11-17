using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人的存活与受击：被玩家的退相干云覆盖时按秒掉血，归零后一次性结算死亡并交给生成器回收。
    /// 只关心自己的血量，不驱动移动也不负责攻击。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyVitals : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField, Min(1f)]
        float maxHealth = 36f;

        [Tooltip("停留在玩家云内时每秒损失的血量")]
        [SerializeField, Min(0f)]
        float cloudDamagePerSecond = 30f;

        [Header("Wiring")]
        [SerializeField, HideInInspector]
        EnemyState state;

        [SerializeField, HideInInspector]
        PlayerCloudField cloud;

        [SerializeField, HideInInspector]
        EnemySpawner spawner;

        [SerializeField, HideInInspector]
        Collider body;

        [SerializeField, HideInInspector]
        CharacterHitFlash hitFlash;

        public float Health { get; private set; }

        public bool IsDead => Health <= 0f;

        void Awake()
        {
            ResolveReferences();
            Health = maxHealth;
        }

        void Update()
        {
            if (IsDead)
            {
                return;
            }

            if (cloud == null || body == null || !cloud.Overlaps(body))
            {
                return;
            }

            ApplyDamage(cloudDamagePerSecond * Time.deltaTime);
        }

        public void ApplyDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            Health -= amount;

            hitFlash?.Flash();

            if (Health <= 0f)
            {
                Health = 0f;
                OnDied();
            }
        }

        void OnDied()
        {
            if (state != null)
            {
                state.MarkDead();

                if (state is EnemyState enemyState)
                {
                    enemyState.Phase = EnemyPhase.Dead;
                }
            }

            if (spawner != null)
            {
                spawner.Despawn(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        void ResolveReferences()
        {
            if (state == null)
            {
                state = GetComponent<EnemyState>();
            }

            if (cloud == null)
            {
                cloud = FindFirstObjectByType<PlayerCloudField>();
            }

            if (spawner == null)
            {
                spawner = FindFirstObjectByType<EnemySpawner>();
            }

            if (body == null)
            {
                body = GetComponent<Collider>();
            }

            if (hitFlash == null)
            {
                hitFlash = GetComponentInChildren<CharacterHitFlash>(true);
            }
        }
    }
}
