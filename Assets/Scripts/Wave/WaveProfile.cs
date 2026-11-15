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

    /// <summary>
    /// 波次表：每个元素是一波的构成。超过表长时重复最后一波。
    /// 只描述“出什么”，不描述敌人自身的行为与数值。
    /// </summary>
    [CreateAssetMenu(menuName = "Decoherence/Wave Profile", fileName = "WaveProfile")]
    public sealed class WaveProfile : ScriptableObject
    {
        [SerializeField]
        WaveEntry[] waves;

        public int WaveCount => waves != null ? waves.Length : 0;

        /// <summary>按 1 起的波次号取构成；超出表长时返回最后一波，空表返回空构成。</summary>
        public WaveEntry GetWave(int waveIndex)
        {
            if (waves == null || waves.Length == 0)
            {
                return default;
            }

            int index = Mathf.Clamp(waveIndex - 1, 0, waves.Length - 1);

            return waves[index];
        }
    }
}
