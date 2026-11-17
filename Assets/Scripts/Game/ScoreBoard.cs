using System;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 计分：按实际造成的伤害累加，而不是按击杀数。
    /// 生成时挂上每只敌人的伤害事件，敌人被回收时事件随对象一起销毁。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreBoard : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        EnemySpawner spawner;

        [Header("Scoring")]
        [Tooltip("每 1 点伤害折算的分数")]
        [SerializeField, Min(0f)]
        float pointsPerDamage = 1f;

        double totalDamage;

        public int Score => Mathf.FloorToInt((float)(totalDamage * pointsPerDamage));

        public float TotalDamage => (float)totalDamage;

        void Awake()
        {
            if (spawner == null)
            {
                spawner = FindFirstObjectByType<EnemySpawner>();
            }
        }

        void OnEnable()
        {
            if (spawner == null)
            {
                return;
            }

            spawner.EnemySpawned += OnEnemySpawned;
        }

        void OnDisable()
        {
            if (spawner == null)
            {
                return;
            }

            spawner.EnemySpawned -= OnEnemySpawned;
        }

        void OnEnemySpawned(GameObject enemy)
        {
            if (enemy == null)
            {
                return;
            }

            EnemyVitals vitals = enemy.GetComponent<EnemyVitals>();

            if (vitals != null)
            {
                vitals.Damaged += OnEnemyDamaged;
            }
        }

        void OnEnemyDamaged(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            totalDamage += amount;
        }
    }
}
