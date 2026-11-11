using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 左上角调试覆盖层：显示玩家状态、平面速度标量与 coherence 余量。
    /// 仅在编辑器与 Development Build 中绘制；正式包中方法体为空，保留组件以避免场景缺失脚本。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerMotor target;
        [SerializeField] private PlayerCoherence pool;
        [SerializeField] private Vector2 margin = new Vector2(10f, 10f);

        private GUIStyle labelStyle;
        private GUIStyle boxStyle;

        private void Awake()
        {
            if (target == null)
            {
                target = FindFirstObjectByType<PlayerMotor>();
            }

            if (pool == null)
            {
                pool = FindFirstObjectByType<PlayerCoherence>();
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
            const float width = 240f;
            float x = margin.x;
            float y = margin.y;

            GUI.Box(new Rect(x - 6f, y - 6f, width + 12f, lineHeight * 3f + 12f), GUIContent.none, boxStyle);

            string state = target.State == CoherenceState.Decoherent ? "Decoherent" : "Coherent";
            GUI.Label(new Rect(x, y, width, lineHeight), "State : " + state, labelStyle);
            GUI.Label(new Rect(x, y + lineHeight, width, lineHeight), "Speed : " + target.CurrentSpeed.ToString("F2") + " m/s", labelStyle);

            if (pool != null)
            {
                string coherence = pool.Coherence.ToString("F1") + " / " + pool.MaxCoherence.ToString("F0");
                GUI.Label(new Rect(x, y + lineHeight * 2f, width, lineHeight), "Coherence : " + coherence, labelStyle);
            }
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
