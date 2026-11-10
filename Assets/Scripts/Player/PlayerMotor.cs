using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Decoherence
{
    public enum CoherenceState
    {
        Coherent = 0,
        Decoherent = 1
    }

    /// <summary>
    /// 指针牵引式移动：鼠标位置决定方向，光标距玩家的屏幕比例距离决定速度。
    /// 只做移动（含两态速度），不含状态切换与资源结算。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour
    {
        [Header("State")]
        [SerializeField] private CoherenceState state = CoherenceState.Coherent;
        [SerializeField, Min(0f)] private float coherentSpeed = 3.5f;
        [SerializeField, Min(0f)] private float decoherentSpeed = 5f;

        [Header("Response")]
        [SerializeField, Min(0f)] private float acceleration = 50f;
        [SerializeField, Min(0f)] private float deceleration = 80f;

        [Tooltip("方向滤波的时间常数 τ（秒）。0.05 表示约 0.05 s 收敛 63%、约 0.15 s 收敛 95%；越小越锐利，越大越顺滑")]
        [FormerlySerializedAs("directionSmoothing")]
        [SerializeField, Min(0.001f)] private float directionSmoothingTimeConstant = 0.05f;

        [Header("Pointer steering")]
        [SerializeField, Min(0f)] private float deadZone = 0.5f;
        [SerializeField, Min(0.01f)] private float fullSpeedRadius = 4f;
        [Tooltip("俯角的正弦值；用于把世界深度折算成屏幕比例距离（45° 时为 0.7071）")]
        [SerializeField, Range(0.1f, 1f)] private float pitchSin = 0.7071068f;

        [Header("Gravity")]
        [SerializeField] private float stickToGroundSpeed = 1f;
        [SerializeField] private float gravity = 9.81f;

        private CharacterController controller;
        private Camera viewCamera;
        private Vector3 planarVelocity;
        private Vector3 smoothedDirection;
        private float verticalVelocity;

        public CoherenceState State
        {
            get => state;
            set => state = value;
        }

        public float MaxSpeed => state == CoherenceState.Coherent ? coherentSpeed : decoherentSpeed;

        public Vector3 PlanarVelocity => planarVelocity;

        public float CurrentSpeed => planarVelocity.magnitude;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            viewCamera = Camera.main;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 targetDirection = ReadPointerDirection(out float throttle);
            if (targetDirection != Vector3.zero)
            {
                float k = 1f - Mathf.Exp(-dt / directionSmoothingTimeConstant);
                smoothedDirection = Vector3.Lerp(smoothedDirection, targetDirection, k);
                if (smoothedDirection.sqrMagnitude > 1e-6f)
                {
                    smoothedDirection.Normalize();
                }
            }
            else
            {
                smoothedDirection = Vector3.zero;
            }

            Vector3 targetVelocity = smoothedDirection * (throttle * MaxSpeed);
            float rate = targetVelocity.sqrMagnitude > planarVelocity.sqrMagnitude ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * dt);

            verticalVelocity = controller.isGrounded
                ? -stickToGroundSpeed
                : verticalVelocity - gravity * dt;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);
        }

        private Vector3 ReadPointerDirection(out float throttle)
        {
            throttle = 0f;

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
                if (viewCamera == null)
                {
                    return Vector3.zero;
                }
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return Vector3.zero;
            }

            Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out float enter))
            {
                return Vector3.zero;
            }

            Vector3 delta = ray.GetPoint(enter) - transform.position;
            delta.y = 0f;

            // 深度方向按 sin(俯角) 折算，使屏幕上的圆对应地面上的圆
            float screenDistance = new Vector2(delta.x, delta.z * pitchSin).magnitude;
            if (screenDistance <= deadZone || delta.sqrMagnitude < 1e-6f)
            {
                return Vector3.zero;
            }

            throttle = Mathf.Clamp01((screenDistance - deadZone) / Mathf.Max(fullSpeedRadius - deadZone, 1e-3f));
            return delta.normalized;
        }
    }
}
