using System;
using System.Collections.Generic;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 生成与登记：把 prefab 实例化到刷怪点、记录存活、负责回收。
    /// 不决定波次节奏（属于 WaveDirector），也不驱动敌人行为。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Points")]
        [Tooltip("为空时在自身位置附近随机取点")]
        [SerializeField]
        Transform[] spawnPoints;

        readonly List<GameObject> alive = new List<GameObject>();

        public event Action<GameObject> EnemySpawned;

        public event Action<GameObject> EnemyDespawned;

        public int AliveCount => alive.Count;

        /// <summary>按给定定义生成一只；定义或其 prefab 为空时返回 null。</summary>
        public GameObject Spawn(EnemyDefinition definition)
        {
            if (definition == null || definition.Prefab == null)
            {
                return null;
            }

            GameObject instance = Instantiate(definition.Prefab, PickPosition(), Quaternion.identity, transform);

            alive.Add(instance);
            EnemySpawned?.Invoke(instance);

            return instance;
        }

        /// <summary>回收一只；不属于本 spawner 的对象会被忽略。</summary>
        public void Despawn(GameObject enemy)
        {
            if (enemy == null || !alive.Remove(enemy))
            {
                return;
            }

            EnemyDespawned?.Invoke(enemy);
            Destroy(enemy);
        }

        Vector3 PickPosition()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                Transform point = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];

                if (point != null)
                {
                    return point.position;
                }
            }

            Vector2 offset = UnityEngine.Random.insideUnitCircle * 6f;

            return transform.position + new Vector3(offset.x, 0f, offset.y);
        }
    }
}
