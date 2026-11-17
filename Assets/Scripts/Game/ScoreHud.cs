using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 分数显示：顶部居中的 "SCORE" 数值。IMGUI 绘制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScoreHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        ScoreBoard score;

        [Header("Layout")]
        [SerializeField]
        float topMargin = 18f;

        [SerializeField]
        float width = 260f;

        [Header("Look")]
        [SerializeField]
        Color labelColor = new Color(1f, 1f, 1f, 0.6f);

        [SerializeField]
        Color valueColor = new Color(0.75f, 0.95f, 1f);

        GUIStyle labelStyle;
        GUIStyle valueStyle;

        void Awake()
        {
            if (score == null)
            {
                score = FindFirstObjectByType<ScoreBoard>();
            }
        }

        void OnGUI()
        {
            if (score == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();

            float x = (Screen.width - width) * 0.5f;

            labelStyle.normal.textColor = labelColor;
            valueStyle.normal.textColor = valueColor;

            GUI.Label(new Rect(x, topMargin, width, 14f), "SCORE", labelStyle);
            GUI.Label(new Rect(x, topMargin + 12f, width, 30f), score.Score.ToString(), valueStyle);
        }

        void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
