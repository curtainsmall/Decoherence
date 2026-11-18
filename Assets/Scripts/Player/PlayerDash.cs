using UnityEngine;
using UnityEngine.InputSystem;

namespace Decoherence
{
    /// <summary>
    /// 冲刺：按空格向当前移动方向短促加速，冷却结束前不可再用。
    /// 外部只配置位移距离，速度与曲线由距离反推：正弦包络、两端慢中段快。
    /// 只提供状态与速度，实际位移仍由 PlayerSteering 写给 CharacterMotor。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDash : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        CharacterMotor motor;

        [SerializeField, HideInInspector]
        PlayerStats stats;

        [Header("Dash")]
        [Tooltip("一次冲刺的位移距离（米）")]
        [SerializeField, Min(0.2f)]
        float dashDistance = 2f;

        [SerializeField, Min(0.1f)]
        float cooldown = 2.5f;

        public bool IsDashing => Time.time < dashUntil;

        public float CooldownRemaining => Mathf.Max(0f, CooldownRemainingRaw());

        /// <summary>1 表示已就绪，0 表示刚用完。</summary>
        public float CooldownNormalized => Cooldown > 0f
            ? 1f - Mathf.Clamp01(CooldownRemaining / Cooldown)
            : 1f;

        /// <summary>冲刺速度：正弦包络，两端趋近 0，中段达到峰值。</summary>
        public Vector3 DashVelocity
        {
            get
            {
                if (!IsDashing)
                {
                    return Vector3.zero;
                }

                float progress = 1f - Mathf.Clamp01((dashUntil - Time.time) / Duration);

                return direction * (PeakSpeed * Mathf.Sin(Mathf.PI * progress));
            }
        }

        /// <summary>冲刺时长，固定值：曲线形状由它和距离共同决定，不对外暴露。</summary>
        const float Duration = 0.15f;

        float Distance => stats != null
            ? Mathf.Max(0.2f, stats.Resolve(StatKind.DashDistance, dashDistance))
            : dashDistance;

        float Cooldown => stats != null
            ? Mathf.Max(0.1f, stats.Scale(StatKind.DashCooldown) * cooldown)
            : cooldown;

        /// <summary>正弦包络下 ∫sin = 2/π，峰值速度 = 距离 · π / (2 · 时长)。</summary>
        float PeakSpeed => Distance * Mathf.PI / (2f * Duration);

        Vector3 direction;
        float dashUntil;
        float cooldownUntil;

        float CooldownRemainingRaw()
        {
            return cooldownUntil - Time.time;
        }

        void Awake()
        {
            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }
        }

        void Update()
        {
            // 标题 / 暂停 / 结束时世界被 timeScale 冻结，不接受冲刺输入
            if (Time.timeScale <= 0f)
            {
                return;
            }

            if (Time.time < cooldownUntil)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null || !keyboard.spaceKey.wasPressedThisFrame)
            {
                return;
            }

            Vector3 planar = motor != null ? motor.PlanarVelocity : Vector3.zero;
            planar.y = 0f;

            direction = planar.sqrMagnitude > 1e-4f ? planar.normalized : transform.forward;

            dashUntil = Time.time + Duration;
            cooldownUntil = Mathf.Max(cooldownUntil, Time.time + Cooldown);
        }
    }
}
