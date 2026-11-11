using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Decoherence
{
    /// <summary>
    /// coherence 单资源 + 状态切换。
    /// 按住鼠标左键进入退相干态（不朽、可攻击、按时间消耗）；松开立即回到相干态（可被伤害、回复）。
    /// 该数值同时是攻击预算、机动预算与生命值；归零即结束。
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    [DisallowMultipleComponent]
    public sealed class PlayerCoherence : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private PlayerMotor motor;

        [Header("Pool")]
        [SerializeField, Min(1f)] private float maxCoherence = 100f;
        [SerializeField, Min(0f)] private float drainPerSecond = 12.5f;
        [SerializeField, Min(0f)] private float regenPerSecond = 20f;

        [Header("Input")]
        [SerializeField] private bool readMouseInput = true;

        public float Coherence { get; private set; }
        public float MaxCoherence => maxCoherence;
        public float Normalized => maxCoherence > 0f ? Coherence / maxCoherence : 0f;
        public bool IsDead { get; private set; }
        public bool IsDecoherent => motor != null && motor.State == CoherenceState.Decoherent;

        public event Action Died;
        public event Action StateChanged;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<PlayerMotor>();
            }

            Coherence = maxCoherence;
        }

        private void Update()
        {
            if (IsDead)
            {
                return;
            }

            bool holding = readMouseInput
                           && Mouse.current != null
                           && Mouse.current.leftButton.isPressed;

            Tick(holding, Time.deltaTime);
        }

        /// <summary>推进一帧：holding 为“按住状态键”的意图。</summary>
        public void Tick(bool holding, float deltaTime)
        {
            if (IsDead || deltaTime <= 0f)
            {
                return;
            }

            bool wasDecoherent = IsDecoherent;
            bool enter = holding && Coherence > 0f;

            if (enter)
            {
                Coherence -= drainPerSecond * deltaTime;

                if (Coherence <= 0f)
                {
                    Coherence = 0f;
                    SetState(CoherenceState.Coherent);
                    Die();
                    return;
                }

                SetState(CoherenceState.Decoherent);
            }
            else
            {
                Coherence = Mathf.Min(maxCoherence, Coherence + regenPerSecond * deltaTime);
                SetState(CoherenceState.Coherent);
            }

            if (wasDecoherent != IsDecoherent)
            {
                StateChanged?.Invoke();
            }
        }

        private void SetState(CoherenceState state)
        {
            if (motor != null)
            {
                motor.State = state;
            }
        }

        private void Die()
        {
            if (IsDead)
            {
                return;
            }

            IsDead = true;

            if (motor != null)
            {
                motor.enabled = false;
            }

            Died?.Invoke();
            Debug.Log("coherence depleted — run over.", this);
        }
    }
}
