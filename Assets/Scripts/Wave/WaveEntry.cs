using UnityEngine;

namespace Decoherence
{
    /// <summary>一波的构成：从哪些敌人里挑、生成几只、间隔多久、同时最多几只。</summary>
    [System.Serializable]
    public struct WaveEntry
    {
        [Tooltip("本波可出现的敌人；为空则本波不生成")]
        [SerializeField]
        EnemyDefinition[] composition;

        [SerializeField, Min(0)]
        int count;

        [Tooltip("相邻两只的间隔（秒）")]
        [SerializeField, Min(0.05f)]
        float interval;

        [Tooltip("同时存活上限")]
        [SerializeField, Min(1)]
        int maxAlive;

        public WaveEntry(EnemyDefinition[] composition, int count, float interval, int maxAlive)
        {
            this.composition = composition;
            this.count = count;
            this.interval = interval;
            this.maxAlive = maxAlive;
        }

        public int Count => Mathf.Max(0, count);

        public float Interval => Mathf.Max(0.05f, interval);

        public int MaxAlive => Mathf.Max(1, maxAlive);

        /// <summary>从构成里随机挑一个可用定义；全为空时返回 null。</summary>
        public EnemyDefinition PickDefinition()
        {
            if (composition == null || composition.Length == 0)
            {
                return null;
            }

            int start = Random.Range(0, composition.Length);

            for (int i = 0; i < composition.Length; i++)
            {
                EnemyDefinition candidate = composition[(start + i) % composition.Length];

                if (candidate != null && candidate.Prefab != null)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
