using System.Collections.Generic;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 波次生成：不再有预定义的波次表，每一波的参数都按波次号现算。
    /// 只负责算出这一波的数量、间隔、同屏上限与兵种配比，具体生成仍交给 EnemySpawner。
    /// </summary>
    [System.Serializable]
    public sealed class WaveScaling
    {
        [Header("Composition")]
        [Tooltip("近战敌人")]
        [SerializeField]
        EnemyDefinition meleeDefinition;

        [Tooltip("远程敌人；留空则不会出现远程")]
        [SerializeField]
        EnemyDefinition rangedDefinition;

        [Tooltip("远程从第几波开始出现")]
        [SerializeField, Min(1)]
        int rangedUnlockWave = 3;

        [Tooltip("远程初始占比")]
        [SerializeField, Range(0f, 1f)]
        float rangedShareBase = 0.15f;

        [Tooltip("每过一波，远程占比增加多少")]
        [SerializeField, Min(0f)]
        float rangedSharePerWave = 0.1f;

        [Tooltip("远程占比上限")]
        [SerializeField, Range(0f, 1f)]
        float rangedShareCap = 0.5f;

        [Header("Count")]
        [SerializeField, Min(1)]
        int baseCount = 12;

        [Tooltip("每波增加的总数")]
        [SerializeField, Min(0f)]
        float countPerWave = 2f;

        [SerializeField, Min(1)]
        int countCap = 60;

        [Header("Interval")]
        [SerializeField, Min(0.05f)]
        float baseInterval = 0.5f;

        [Tooltip("每波缩短的间隔")]
        [SerializeField, Min(0f)]
        float intervalPerWave = 0.01f;

        [SerializeField, Min(0.05f)]
        float minInterval = 0.2f;

        [Header("Alive Cap")]
        [SerializeField, Min(1)]
        int baseMaxAlive = 8;

        [Tooltip("每几波，同屏上限加一")]
        [SerializeField, Min(1)]
        int aliveStepEvery = 2;

        [SerializeField, Min(1)]
        int aliveCap = 30;

        /// <summary>按波次号算出这一波的参数，波次从 1 开始，没有上限。</summary>
        public WaveEntry Build(int waveIndex)
        {
            int index = Mathf.Max(1, waveIndex);
            int steps = index - 1;

            int count = Mathf.Min(countCap, baseCount + Mathf.RoundToInt(countPerWave * steps));
            float interval = Mathf.Max(minInterval, baseInterval - intervalPerWave * steps);
            int maxAlive = Mathf.Min(aliveCap, baseMaxAlive + steps / Mathf.Max(1, aliveStepEvery));

            return new WaveEntry(BuildComposition(index), count, interval, maxAlive);
        }

        EnemyDefinition[] BuildComposition(int index)
        {
            const int slots = 9;

            float share = 0f;

            if (rangedDefinition != null && index >= rangedUnlockWave)
            {
                share = Mathf.Clamp(
                    rangedShareBase + rangedSharePerWave * (index - rangedUnlockWave),
                    0f,
                    rangedShareCap);
            }

            int rangedSlots = Mathf.Clamp(Mathf.RoundToInt(share * slots), 0, slots);
            int meleeSlots = slots - rangedSlots;

            var composition = new List<EnemyDefinition>(slots);

            for (int i = 0; i < meleeSlots; i++)
            {
                if (meleeDefinition != null)
                {
                    composition.Add(meleeDefinition);
                }
            }

            for (int i = 0; i < rangedSlots; i++)
            {
                composition.Add(rangedDefinition);
            }

            return composition.ToArray();
        }
    }
}
