using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 奖励目录：一个 asset 存全部条目，Inspector 里可直接改数值。
    /// </summary>
    [CreateAssetMenu(menuName = "Decoherence/Upgrade Catalog", fileName = "UpgradeCatalog")]
    public sealed class UpgradeCatalog : ScriptableObject
    {
        [SerializeField]
        Upgrade[] entries;

        public int Count => entries != null ? entries.Length : 0;

        public Upgrade this[int index] => entries[index];

        /// <summary>首次生成用：把默认条目写进 asset。</summary>
        public void ResetToDefaults()
        {
            entries = Defaults();
        }

        /// <summary>默认条目：只包含"已接上消费者"的数值与规则。</summary>
        public static Upgrade[] Defaults()
        {
            return new[]
            {
                new Upgrade("capacity", "扩容", "相干度上限 +20", 3, StatKind.MaxCoherence, 20f),
                new Upgrade("efficient", "深蓄", "退相干消耗 −10%", 2, StatKind.DrainRate, -0.1f),
                new Upgrade("recover", "快回", "相干态回复 +25%", 3, StatKind.RegenRate, 0.25f),
                new Upgrade("width", "云幅", "云半径 +15%", 4, StatKind.CloudRadius, 0.15f),
                new Upgrade("dense", "浓云", "云伤害 +20%", 4, StatKind.CloudDps, 0.2f),
                new Upgrade("pulse", "脉冲化", "接触伤害改为每 0.45 秒结算一次", 1, StatKind.CloudDps, 0f, PlayerFlag.PulsedCloud),
                new Upgrade("swift", "轻步", "移动速度 +10%", 4, StatKind.MoveSpeed, 0.1f),
                new Upgrade("reach", "长距", "冲刺距离 +1 m", 3, StatKind.DashDistance, 1f),
                new Upgrade("recharge", "速充", "冲刺冷却 −20%", 3, StatKind.DashCooldown, -0.2f),
                new Upgrade("multiplier", "倍率", "伤害得分 ×1.25", 3, StatKind.ScoreMultiplier, 0.25f),
                new Upgrade("secondchance", "免死", "一次致命伤害后保留 1 点相干度", 1, StatKind.MaxCoherence, 0f, PlayerFlag.SecondChance),
                new Upgrade("fourchoices", "四选一", "之后每次奖励可选 4 个", 1, StatKind.MaxCoherence, 0f, PlayerFlag.FourChoices),
            };
        }
    }
}
