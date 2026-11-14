using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 角色的运动观感：竖直弹跳节律、体积压扁、前倾与头部滞后。
    /// 只写视觉节点（rig 根 / HeadPivot / 通过 ShapeRig 写身体压扁），不碰 CharacterController，
    /// 也不含玩法数值——速度由 PlayerMotor 提供，或在没有 motor 时由 SetMotion 推入。
    /// 同一个 Transform 只有一个写者：本组件独占 rig 根的 rotation，因此不再与旧的转身脚本叠加。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterRig : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private ShapeRig shapeRig;
        [Tooltip("为空时用 SetMotion 推入速度（敌人用）")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform headPivot;

        [Header("Hop")]
        [Tooltip("一次跳跃对应的位移（米）")]
        [SerializeField, Min(0.1f)] private float hopLength = 0.7f;
        [SerializeField, Min(0f)] private float hopHeight = 0.10f;
        [Tooltip("频率上限；超出后让步幅变长，避免高速抖成噪点")]
        [SerializeField, Min(0.5f)] private float maxHopHz = 4f;
        [SerializeField, Min(0f)] private float hopResponse = 26f;

        [Header("Squash")]
        [Tooltip("只在触地瞬间压缩：实心物体不该随步频持续变形")]
        [SerializeField, Range(0f, 0.3f)] private float contactSquash = 0.06f;
        [Tooltip("触地脉冲的角度窗口，越小越像一次撞击")]
        [SerializeField, Range(5f, 170f)] private float impactWindowDegrees = 70f;
        [SerializeField, Min(0.1f)] private float squashResponse = 26f;

        [Header("Idle")]
        [SerializeField, Range(0f, 0.05f)] private float idleBreath = 0.008f;
        [SerializeField, Min(0.01f)] private float breathHz = 0.45f;

        [Header("Lean")]
        [SerializeField, Range(0f, 45f)] private float leanMaxDegrees = 14f;
        [SerializeField, Range(0f, 30f)] private float turnLeanDegrees = 8f;
        [SerializeField, Min(0.1f)] private float leanResponse = 9f;
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;

        [Header("Head")]
        [SerializeField, Min(0f)] private float headLagDistance = 0.02f;
        [SerializeField, Min(0.1f)] private float headLagResponse = 8f;
        [SerializeField, Range(0f, 1f)] private float headCounterTilt = 0.3f;

        private Vector3 baseLocalPosition;
        private Vector3 headBasePosition;
        private Vector3 pushedVelocity;
        private Vector3 previousVelocity;
        private Vector3 headOffset;
        private Quaternion currentYaw = Quaternion.identity;
        private float phase;
        private float breathPhase;
        private float hopOffset;
        private float squash = 1f;
        private float lean;
        private float sideLean;
        private float previousYawAngle;

        public float Speed { get; private set; }
        public float HopPhase => phase;

        private void Awake()
        {
            ResolveReferences();

            baseLocalPosition = transform.localPosition;
            currentYaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            previousYawAngle = transform.eulerAngles.y;

            if (shapeRig != null)
            {
                // ShapeRig 的 Apply 是幂等的，先确保基础几何已就位再取头部基点
                shapeRig.Apply();
                headBasePosition = shapeRig.HeadPivotBasePosition;
            }
        }

        private void OnValidate()
        {
            ResolveReferences();
        }

        /// <summary>没有 PlayerMotor 时由宿主推入平面速度，用于敌人复用同一套观感。</summary>
        public void SetMotion(Vector3 planarVelocity)
        {
            pushedVelocity = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (dt <= 0f)
            {
                return;
            }

            Vector3 velocity = motor != null ? motor.PlanarVelocity : pushedVelocity;
            velocity.y = 0f;

            float speed = velocity.magnitude;
            float maxSpeed = motor != null ? Mathf.Max(motor.MaxSpeed, 0.01f) : Mathf.Max(1f, speed);
            Speed = speed;

            UpdateHop(dt, speed);
            UpdateLean(dt, velocity, speed, maxSpeed);
            UpdateHead(dt, velocity);
            UpdateSquash(dt, speed);

            previousVelocity = velocity;
        }

        private void UpdateHop(float dt, float speed)
        {
            // hopHeight 为 0 表示不要弹跳：实心角色贴地滑行，只保留倾斜与头部滞后
            if (hopHeight <= 0.0001f)
            {
                hopOffset = Mathf.Lerp(hopOffset, 0f, 1f - Mathf.Exp(-hopResponse * dt));
                transform.localPosition = baseLocalPosition;
                phase = 0f;
                return;
            }

            float length = Mathf.Max(0.1f, hopLength);
            float hz = Mathf.Min(speed / length, maxHopHz);
            phase += hz * 360f * dt;

            if (phase > 360f)
            {
                phase -= 360f;
            }

            float normalized = Mathf.Abs(Mathf.Sin(phase * Mathf.Deg2Rad));

            // 停下时相位会冻结在半空，用 active 把高度收回地面
            float active = Mathf.Clamp01(speed / 0.3f);
            float target = hopHeight * normalized * active;

            hopOffset = Mathf.Lerp(hopOffset, target, 1f - Mathf.Exp(-hopResponse * dt));
            transform.localPosition = baseLocalPosition + Vector3.up * hopOffset;
        }

        private void UpdateSquash(float dt, float speed)
        {
            if (shapeRig == null)
            {
                return;
            }

            breathPhase += breathHz * 360f * dt;

            if (breathPhase > 360f)
            {
                breathPhase -= 360f;
            }

            float moving = Mathf.Clamp01(speed / Mathf.Max(0.5f, shapeRig.BodyRadius * 4f));

            // |sin| 的波谷就是触地点，每 180° 一次；只在这一小段里压缩，其余时间保持实心。
            // 不弹跳时相位恒为 0，这里必须显式关掉撞击，否则会被当成“一直在触地”。
            float impact = 0f;

            if (hopHeight > 0.0001f)
            {
                float halfCycle = Mathf.Repeat(phase, 180f);
                float distanceToContact = Mathf.Min(halfCycle, 180f - halfCycle);

                impact = 1f - Mathf.Clamp01(distanceToContact / Mathf.Max(5f, impactWindowDegrees));
                impact *= impact;
            }

            float breath = idleBreath * Mathf.Sin(breathPhase * Mathf.Deg2Rad) * (1f - moving);
            float target = 1f - contactSquash * impact * moving + breath;

            squash = Mathf.Lerp(squash, target, 1f - Mathf.Exp(-squashResponse * dt));
            shapeRig.SetSquash(squash);
        }

        private void UpdateLean(float dt, Vector3 velocity, float speed, float maxSpeed)
        {
            var planar = new Vector3(velocity.x, 0f, velocity.z);

            if (planar.sqrMagnitude > 0.0025f)
            {
                Quaternion targetYaw = Quaternion.LookRotation(planar.normalized, Vector3.up);
                currentYaw = turnDegreesPerSecond > 0f
                    ? Quaternion.RotateTowards(currentYaw, targetYaw, turnDegreesPerSecond * dt)
                    : targetYaw;
            }

            float speedNorm = Mathf.Clamp01(speed / maxSpeed);
            lean = Mathf.Lerp(lean, leanMaxDegrees * speedNorm, 1f - Mathf.Exp(-leanResponse * dt));

            float yawAngle = currentYaw.eulerAngles.y;
            float turnRate = dt > 0f ? Mathf.DeltaAngle(previousYawAngle, yawAngle) / dt : 0f;
            previousYawAngle = yawAngle;

            float targetSide = Mathf.Clamp(-turnRate * 0.02f, -turnLeanDegrees, turnLeanDegrees);
            sideLean = Mathf.Lerp(sideLean, targetSide, 1f - Mathf.Exp(-leanResponse * dt));

            // yaw 与倾斜合成同一个旋转：倾角在 rig 局部空间里前倾(X)、侧倾(Z)
            transform.rotation = currentYaw * Quaternion.Euler(lean, 0f, sideLean);
        }

        private void UpdateHead(float dt, Vector3 velocity)
        {
            if (headPivot == null)
            {
                return;
            }

            Vector3 acceleration = dt > 0f ? (velocity - previousVelocity) / dt : Vector3.zero;
            acceleration.y = 0f;
            acceleration = Vector3.ClampMagnitude(acceleration, 40f);

            // 头朝加速度反方向滞后，并在局部空间里表达（rig 带倾斜，必须换算）
            Vector3 worldOffset = -acceleration * headLagDistance * 0.05f;
            Vector3 localOffset = transform.InverseTransformVector(worldOffset);

            headOffset = Vector3.Lerp(headOffset, localOffset, 1f - Mathf.Exp(-headLagResponse * dt));

            headPivot.localPosition = headBasePosition + headOffset;
            headPivot.localRotation = Quaternion.Euler(-lean * headCounterTilt, 0f, -sideLean * headCounterTilt);
        }

        private void ResolveReferences()
        {
            if (shapeRig == null)
            {
                shapeRig = GetComponent<ShapeRig>();
            }

            if (motor == null)
            {
                motor = GetComponentInParent<PlayerMotor>();
            }

            if (headPivot == null && shapeRig != null)
            {
                headPivot = shapeRig.HeadPivot;
            }
        }
    }
}
