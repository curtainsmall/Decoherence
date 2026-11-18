using System;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// coherence 单资源结算。
    /// 该数值同时是攻击预算、机动预算与生命值；归零即结束。
    /// 基础值写在本组件的序列化字段里，本局升级带来的修正从 PlayerStats 读。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCoherence : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerState state;

        [SerializeField, HideInInspector]
        PlayerDash dash;

        [SerializeField, HideInInspector]
        PlayerStats stats;

        [Header("Pool")]
        [SerializeField, Min(1f)]
        float maxCoherence = 100f;

        [SerializeField, Min(0f)]
        float drainPerSecond = 12.5f;

        [SerializeField, Min(0f)]
        float regenPerSecond = 20f;

        [Header("Second chance")]
        [Tooltip("免死触发后强制恢复相干态的时长（秒）")]
        [SerializeField, Min(0f)]
        float secondChanceRecoverTime = 0.6f;

        public float Coherence { get; private set; }

        public float MaxCoherence => stats != null
            ? stats.Resolve(StatKind.MaxCoherence, maxCoherence)
            : maxCoherence;

        public float Normalized => MaxCoherence > 0f ? Mathf.Clamp01(Coherence / MaxCoherence) : 0f;

        public bool IsDead => state != null && !state.IsAlive;

        public bool IsDecoherent => state != null && state.IsDecoherent;

        public event Action Died;

        public event Action StateChanged;

        /// <summary>受到外部伤害时触发，参数是本次扣除量；退相干态的伤害不会触发。</summary>
        public event Action<float> Damaged;

        float drainBlockedUntil;
        bool secondChanceUsed;

        float DrainPerSecond => stats != null ? stats.Scale(StatKind.DrainRate) * drainPerSecond : drainPerSecond;

        float RegenPerSecond => stats != null ? stats.Scale(StatKind.RegenRate) * regenPerSecond : regenPerSecond;

        void Awake()
        {
            if (state == null)
            {
                state = GetComponent<PlayerState>();
            }

            if (dash == null)
            {
                dash = GetComponent<PlayerDash>();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }

            Coherence = MaxCoherence;
        }

        void Update()
        {
            if (IsDead || state == null)
            {
                return;
            }

            Tick(state.HoldIntent, Time.deltaTime);
        }

        /// <summary>推进一帧：holding 为“按住状态键”的意图。</summary>
        void Tick(bool holding, float deltaTime)
        {
            if (IsDead || deltaTime <= 0f)
            {
                return;
            }

            bool wasDecoherent = state.IsDecoherent;
            bool blocked = Time.time < drainBlockedUntil;
            bool enter = holding && Coherence > 0f && !blocked;

            if (enter)
            {
                Coherence -= DrainPerSecond * deltaTime;

                if (Coherence <= 0f)
                {
                    if (!TryConsumeSecondChance())
                    {
                        Coherence = 0f;
                        state.State = CoherenceState.Coherent;
                        OnDied();

                        return;
                    }
                }

                state.State = CoherenceState.Decoherent;
            }
            else
            {
                Coherence = Mathf.Min(MaxCoherence, Coherence + RegenPerSecond * deltaTime);
                state.State = CoherenceState.Coherent;
            }

            if (wasDecoherent != state.IsDecoherent)
            {
                StateChanged?.Invoke();
            }
        }

        void OnDied()
        {
            if (IsDead)
            {
                return;
            }

            state.MarkDead();

            Died?.Invoke();
            Debug.Log("coherence depleted — run over.", this);
        }

        /// <summary>扣除相干度（受到伤害）。退相干态免疫；归零即死亡，且只触发一次。</summary>
        public void Drain(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            // 退相干态是虚体：外部伤害不生效，避免穿过敌人时被接触伤害打掉资源
            if (state != null && state.IsDecoherent)
            {
                return;
            }

            // 冲刺期间无敌帧：整段位移都不吃伤害
            if (dash != null && dash.IsDashing)
            {
                return;
            }

            Coherence -= amount;

            Damaged?.Invoke(amount);

            if (Coherence > 0f)
            {
                return;
            }

            if (TryConsumeSecondChance())
            {
                return;
            }

            Coherence = 0f;

            if (state != null)
            {
                state.State = CoherenceState.Coherent;
            }

            OnDied();
        }

        /// <summary>回复相干度（获得奖励），不会超过上限。</summary>
        public void Restore(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            Coherence = Mathf.Min(MaxCoherence, Coherence + amount);
        }

        /// <summary>免死：一次致命伤害后保留 1 点，并强制恢复相干态一小段时间。</summary>
        bool TryConsumeSecondChance()
        {
            if (stats == null || secondChanceUsed || !stats.Has(PlayerFlag.SecondChance))
            {
                return false;
            }

            secondChanceUsed = true;
            Coherence = 1f;
            drainBlockedUntil = Time.time + secondChanceRecoverTime;

            if (state != null)
            {
                state.State = CoherenceState.Coherent;
            }

            return true;
        }
    }
}
