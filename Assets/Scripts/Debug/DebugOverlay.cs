using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 左上角调试覆盖层：显示玩家当前状态与平面速度标量。
    /// 仅在编辑器与 Development Build 中绘制；正式包中方法体为空，保留组件以避免场景缺失脚本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerMotor target;
        [SerializeField] private Vector2 margin = new Vector2(10f, 10f);

        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            if (target == null)
            {
                target = FindFirstObjectByType<PlayerMotor>();
            }
        }

        private void OnGUI()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (target == null)
            {
                return;
            }

            EnsureStyles();

            const float lineHeight = 20f;
            const float width = 220f;
            float x = margin.x;
            float y = margin.y;

            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * 2f + 12f), GUIContent.none, boxStyle);

            string state = target.State == CoherenceState.Decoherent ? "Decoherent" : "Coherent";
            GUI.Label(new Rect(x, y, width, lineHeight), "State : " + state, labelStyle);
            GUI.Label(new Rect(x, y + lineHeight, width, lineHeight), "Speed : " + target.CurrentSpeed.ToString("F2") + " m/s", labelStyle);
#endif
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void EnsureStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleLeft
                };
                labelStyle.normal.textColor = Color.white;
            }

            if (boxStyle == null)
            {
                boxStyle = new GUIStyle(GUI.skin.box);
            }
        }
#endif
    }
}
