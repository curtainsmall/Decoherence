using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 一条奖励的数据：一个数值修正加（可选）一个规则开关。
    /// 百分比类修正写在一个 StatKind 上，绝对值类（上限、冲刺距离）写另一个。
    /// </summary>
    [System.Serializable]
    public sealed class Upgrade
    {
        [SerializeField]
        string id = "upgrade";

        [SerializeField]
        string title = "";

        [TextArea(2, 4)]
        [SerializeField]
        string description = "";

        [SerializeField, Min(1)]
        int maxStacks = 1;

        [SerializeField]
        StatKind stat = StatKind.MaxCoherence;

        [SerializeField]
        float value;

        [SerializeField]
        PlayerFlag flag = PlayerFlag.None;

        public Upgrade()
        {
        }

        public Upgrade(string id, string title, string description, int maxStacks, StatKind stat, float value, PlayerFlag flag = PlayerFlag.None)
        {
            this.id = id;
            this.title = title;
            this.description = description;
            this.maxStacks = maxStacks;
            this.stat = stat;
            this.value = value;
            this.flag = flag;
        }

        public string Id => id;

        public string Title => title;

        public string Description => description;

        public int MaxStacks => Mathf.Max(1, maxStacks);

        public StatKind Stat => stat;

        public float Value => value;

        public PlayerFlag Flag => flag;
    }
}
