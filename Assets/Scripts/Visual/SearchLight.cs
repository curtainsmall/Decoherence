using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 绕出厂的朝向左右扫掠的探照灯灯头：正弦往返，不改光束形状。
    /// 灯头的静止朝向由场景里摆好的局部旋转决定，运行时只在其上叠加偏航。
    /// </summary>
    public sealed class SearchLight : MonoBehaviour
    {
        [Tooltip("左右扫掠幅度，度")]
        [SerializeField]
        float sweepDegrees = 40f;

        [Tooltip("每秒完成的往返周期数，越小越慢")]
        [SerializeField]
        float sweepSpeed = 0.06f;

        [Tooltip("相位，用于错开多盏灯的节奏")]
        [SerializeField, Range(0f, 1f)]
        float phase = 0.1f;

        Quaternion restRotation;

        void Awake()
        {
            restRotation = transform.localRotation;
        }

        void Update()
        {
            float angle = sweepDegrees * Mathf.Sin((Time.time * sweepSpeed + phase) * Mathf.PI * 2f);

            transform.localRotation = restRotation * Quaternion.AngleAxis(angle, Vector3.up);
        }
    }
}
