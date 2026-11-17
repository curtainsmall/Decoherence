using UnityEngine;
using UnityEngine.InputSystem;

namespace Decoherence
{
    /// <summary>
    /// 玩家输入源：把鼠标位置折算成平面速度意图写给 CharacterMotor，
    /// 同时把“按住状态键”的意图写给 PlayerState。自己不移动、不改状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSteering : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        CharacterMotor motor;

        [SerializeField, HideInInspector]
        PlayerState state;

        [SerializeField, HideInInspector]
        PlayerDash dash;

        [Header("Pointer steering")]
        [SerializeField, Min(0f)]
        float deadZone = 0.5f;

        [SerializeField, Min(0.01f)]
        float fullSpeedRadius = 4f;

        [Tooltip("俯角的正弦值；用于把世界深度折算成屏幕比例距离（45° 时为 0.7071）")]
        [SerializeField, Range(0.1f, 1f)]
        float pitchSin = 0.7071068f;

        [Header("Response")]
        [Tooltip("方向滤波的时间常数 τ（秒）")]
        [SerializeField, Min(0.001f)]
        float directionSmoothingTimeConstant = 0.05f;

        Camera viewCamera;

        Vector3 smoothedDirection;

        /// <summary>恢复后必须先松开一次鼠标，才重新接受按住输入。</summary>
        bool requireRelease = true;

        void Awake()
        {
            ResolveReferences();
            viewCamera = Camera.main;
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (motor == null || state == null)
            {
                return;
            }

            // 标题 / 暂停 / 结束时世界被 timeScale 冻结：不读按住输入，并要求下一次重新按下
            if (Time.timeScale <= 0f)
            {
                requireRelease = true;
                state.HoldIntent = false;
                motor.ClearIntent();

                return;
            }

            if (deltaTime <= 0f)
            {
                return;
            }

            Mouse mouse = Mouse.current;

            // 左右键同义：都可以按住进入退相干
            bool held = mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed);

            // 按键重按检测：冻结前按住的那一下不算，必须松开再按
            if (requireRelease)
            {
                if (!held)
                {
                    requireRelease = false;
                }

                held = false;
            }

            state.HoldIntent = held;

            if (!state.CanMove)
            {
                motor.ClearIntent();
                return;
            }

            // 冲刺期间用冲刺速度覆盖常规走位
            if (dash != null && dash.IsDashing)
            {
                motor.SetIntent(dash.DashVelocity);
                return;
            }

            Vector3 targetDirection = ReadPointerDirection(out float throttle);

            if (targetDirection != Vector3.zero)
            {
                float k = 1f - Mathf.Exp(-deltaTime / directionSmoothingTimeConstant);
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

            motor.SetIntent(smoothedDirection * (throttle * state.MaxSpeed));
        }

        Vector3 ReadPointerDirection(out float throttle)
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

        void ResolveReferences()
        {
            if (motor == null)
            {
                motor = GetComponent<CharacterMotor>();
            }

            if (state == null)
            {
                state = GetComponent<PlayerState>();
            }

            if (dash == null)
            {
                dash = GetComponent<PlayerDash>();
            }
        }
    }
}
