using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 冲刺冷却的地面指示：用脚印环那段弧的长短表示冷却进度，满了就是整环。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashRing : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField]
        GroundRing ring;

        [SerializeField, HideInInspector]
        PlayerDash dash;

        [Header("Arc")]
        [SerializeField, Range(5f, 90f)]
        float idleDegrees = 12f;

        [SerializeField, Range(90f, 360f)]
        float readyDegrees = 360f;

        void Awake()
        {
            if (ring == null)
            {
                ring = GetComponent<GroundRing>();
            }

            if (dash == null)
            {
                dash = FindFirstObjectByType<PlayerDash>();
            }
        }

        void Update()
        {
            if (ring == null || dash == null)
            {
                return;
            }

            ring.SweepDegrees = Mathf.Lerp(idleDegrees, readyDegrees, Mathf.Clamp01(dash.CooldownNormalized));
        }
    }
}
