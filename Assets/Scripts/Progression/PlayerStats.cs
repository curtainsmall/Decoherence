using System;
using System.Collections.Generic;
using UnityEngine;

namespace Decoherence
{
    public enum StatKind
    {
        MaxCoherence = 0,
        DrainRate = 1,
        RegenRate = 2,
        CloudRadius = 3,
        CloudDps = 4,
        DashDistance = 5,
        DashCooldown = 6,
        MoveSpeed = 7,
        ScoreMultiplier = 8,
    }

    public enum PlayerFlag
    {
        None = 0,
        PulsedCloud = 1,
        SecondChance = 2,
        FourChoices = 3,
    }

    /// <summary>
    /// 本局升级的聚合层：只存"修正"与"规则开关"，基础数值仍留在各自组件上。
    /// 组件取值时用 Resolve（绝对值）或 Scale（百分比）过一次聚合。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerStats : MonoBehaviour
    {
        static readonly int StatCount = Enum.GetValues(typeof(StatKind)).Length;

        readonly float[] flat = new float[StatCount];
        readonly List<Upgrade> taken = new List<Upgrade>();
        readonly Dictionary<string, int> stacks = new Dictionary<string, int>();
        readonly HashSet<PlayerFlag> flags = new HashSet<PlayerFlag>();

        public event Action Changed;

        public IReadOnlyList<Upgrade> Taken => taken;

        /// <summary>绝对值：基准 + 累加值。</summary>
        public float Resolve(StatKind kind, float baseValue)
        {
            return baseValue + flat[(int)kind];
        }

        /// <summary>百分比：1 + 累加值，用来换算倍率。</summary>
        public float Scale(StatKind kind)
        {
            return 1f + flat[(int)kind];
        }

        public bool Has(PlayerFlag flag)
        {
            return flag != PlayerFlag.None && flags.Contains(flag);
        }

        public int Stacks(Upgrade upgrade)
        {
            if (upgrade == null)
            {
                return 0;
            }

            return stacks.TryGetValue(upgrade.Id, out int count) ? count : 0;
        }

        public bool CanTake(Upgrade upgrade)
        {
            return upgrade != null && Stacks(upgrade) < upgrade.MaxStacks;
        }

        public void Take(Upgrade upgrade)
        {
            if (!CanTake(upgrade))
            {
                return;
            }

            taken.Add(upgrade);
            stacks[upgrade.Id] = Stacks(upgrade) + 1;
            Recalculate();

            Changed?.Invoke();
        }

        public void Clear()
        {
            taken.Clear();
            stacks.Clear();
            flags.Clear();
            Array.Clear(flat, 0, flat.Length);

            Changed?.Invoke();
        }

        void Recalculate()
        {
            Array.Clear(flat, 0, flat.Length);
            flags.Clear();

            foreach (Upgrade upgrade in taken)
            {
                flat[(int)upgrade.Stat] += upgrade.Value;

                if (upgrade.Flag != PlayerFlag.None)
                {
                    flags.Add(upgrade.Flag);
                }
            }
        }
    }
}
