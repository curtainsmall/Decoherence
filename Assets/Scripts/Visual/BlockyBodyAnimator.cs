using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 方块角色的基础程序化动画与朝向：
    /// 依据 PlayerMotor 的平面速度驱动四肢摆动与身体上下微动，并让身体朝向移动方向。
    /// 只旋转/位移本物体（Body），因此挂在 Player 下的相机不受影响。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlockyBodyAnimator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;

        [Header("Swing")]
        [SerializeField, Min(0f)] private float strideDegrees = 38f;
        [SerializeField, Min(0f)] private float stepsPerMeter = 0.9f;
        [SerializeField, Min(0f)] private float swingResponse = 12f;

        [Header("Turn")]
        [SerializeField, Min(0f)] private float turnDegreesPerSecond = 720f;

        [Header("Bob")]
        [SerializeField, Min(0f)] private float bobHeight = 0.035f;

        private float phase;
        private float currentSwing;
        private Vector3 baseLocalPosition;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<PlayerMotor>();
            }

            if (armLeft == null)
            {
                armLeft = transform.Find("Arm_L");
            }

            if (armRight == null)
            {
                armRight = transform.Find("Arm_R");
            }

            if (legLeft == null)
            {
                legLeft = transform.Find("Leg_L");
            }

            if (legRight == null)
            {
                legRight = transform.Find("Leg_R");
            }

            baseLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            Vector3 velocity = motor != null ? motor.PlanarVelocity : Vector3.zero;
            float speed = velocity.magnitude;
            float maxSpeed = motor != null ? Mathf.Max(motor.MaxSpeed, 0.01f) : 1f;
            float normalized = Mathf.Clamp01(speed / maxSpeed);

            // 摆动相位由行走距离驱动，速度快则步频高。
            phase += speed * stepsPerMeter * 360f * deltaTime;

            float targetSwing = Mathf.Sin(phase * Mathf.Deg2Rad) * strideDegrees * normalized;
            currentSwing = Mathf.Lerp(currentSwing, targetSwing, 1f - Mathf.Exp(-swingResponse * deltaTime));

            ApplySwing(armLeft, currentSwing);
            ApplySwing(armRight, -currentSwing);
            ApplySwing(legLeft, -currentSwing);
            ApplySwing(legRight, currentSwing);

            // 身体上下微动：速度越快幅度越大。
            Vector3 bobPosition = baseLocalPosition;
            bobPosition.y += Mathf.Abs(Mathf.Sin(phase * Mathf.Deg2Rad)) * bobHeight * normalized;
            transform.localPosition = bobPosition;

            TurnTowards(velocity, deltaTime);
        }

        private static void ApplySwing(Transform limb, float degrees)
        {
            if (limb != null)
            {
                limb.localRotation = Quaternion.Euler(degrees, 0f, 0f);
            }
        }

        private void TurnTowards(Vector3 velocity, float deltaTime)
        {
            Vector3 planar = new Vector3(velocity.x, 0f, velocity.z);
            if (planar.sqrMagnitude < 0.0025f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planar.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, target, turnDegreesPerSecond * deltaTime);
        }
    }
}
