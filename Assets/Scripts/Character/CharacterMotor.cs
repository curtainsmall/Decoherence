using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 通用运动执行：接收一个平面速度意图，负责贴地、重力、CharacterController 位移。
    /// 不读输入、不决定速度上限；意图由各自的驱动者（玩家输入、敌人 AI）给出。
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public sealed class CharacterMotor : MonoBehaviour
    {
        [Header("Gravity")]
        [SerializeField]
        float stickToGroundSpeed = 1f;

        [SerializeField]
        float gravity = 9.81f;

        CharacterController controller;

        Vector3 intent;

        Vector3 planarVelocity;

        float verticalVelocity;

        public Vector3 PlanarVelocity => planarVelocity;

        public float CurrentSpeed => planarVelocity.magnitude;

        /// <summary>设置平面速度意图（米/秒），会一直保持到下次设置或 ClearIntent。</summary>
        public void SetIntent(Vector3 planarVelocity)
        {
            intent = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
        }

        public void ClearIntent()
        {
            intent = Vector3.zero;
        }

        void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            planarVelocity = intent;

            verticalVelocity = controller.isGrounded
                ? -stickToGroundSpeed
                : verticalVelocity - gravity * deltaTime;

            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * deltaTime);
        }
    }
}
