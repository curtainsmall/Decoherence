using System;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// coherence 单资源结算。
    /// 该数值同时是攻击预算、机动预算与生命值；归零即结束。
    /// 状态与输入意图由 PlayerState 提供，本类只做资源的加减与死亡判定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCoherence : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerState state;

        [Header("Pool")]
        [SerializeField, Min(1f)]
        float maxCoherence = 100f;

        [SerializeField, Min(0f)]
        float drainPerSecond = 12.5f;

        [SerializeField, Min(0f)]
        float regenPerSecond = 20f;

        public float Coherence { get; private set; }

        public float MaxCoherence => maxCoherence;

        public float Normalized => maxCoherence > 0f ? Coherence / maxCoherence : 0f;

        public bool IsDead => state != null && !state.IsAlive;

        public bool IsDecoherent => state != null && state.IsDecoherent;

        public event Action Died;

        public event Action StateChanged;

        /// <summary>受到外部伤害时触发，参数是本次扣除量；退相干态的伤害不会触发。</summary>
        public event Action<float> Damaged;

        void Awake()
        {
            if (state == null)
            {
                state = GetComponent<PlayerState>();
            }

            Coherence = maxCoherence;
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
            bool enter = holding && Coherence > 0f;

            if (enter)
            {
                Coherence -= drainPerSecond * deltaTime;

                if (Coherence <= 0f)
                {
                    Coherence = 0f;
                    state.State = CoherenceState.Coherent;
                    OnDied();
                    return;
                }

                state.State = CoherenceState.Decoherent;
            }
            else
            {
                Coherence = Mathf.Min(maxCoherence, Coherence + regenPerSecond * deltaTime);
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

            Coherence -= amount;

            Damaged?.Invoke(amount);

            if (Coherence > 0f)
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

            Coherence = Mathf.Min(maxCoherence, Coherence + amount);
        }
    }
}
