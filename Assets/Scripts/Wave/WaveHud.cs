using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 波次 HUD：右上角显示当前波次、剩余敌人数与波次状态。
    /// 全部用 IMGUI 绘制，不依赖美术资源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        WaveDirector director;

        [Header("Layout")]
        [SerializeField]
        Vector2 margin = new Vector2(24f, 20f);

        [SerializeField]
        float width = 220f;

        [Header("Look")]
        [SerializeField]
        Color titleColor = new Color(0.75f, 0.95f, 1f);

        GUIStyle titleStyle;
        GUIStyle valueStyle;
        GUIStyle stateStyle;

        void Awake()
        {
            if (director == null)
            {
                director = FindFirstObjectByType<WaveDirector>();
            }
        }

        void OnGUI()
        {
            if (director == null)
            {
                return;
            }

            EnsureStyles();

            float x = Screen.width - margin.x - width;
            float y = margin.y;

            titleStyle.normal.textColor = titleColor;

            string title = director.State == WaveState.Ended ? "RUN OVER" : "WAVE " + director.WaveIndex;
            string remaining = director.State == WaveState.Ended ? "-" : director.RemainingCount.ToString();

            GUI.Label(new Rect(x, y, width, 28f), title, titleStyle);
            GUI.Label(new Rect(x, y + 30f, width, 20f), "REMAINING  " + remaining, valueStyle);
            GUI.Label(new Rect(x, y + 52f, width, 18f), director.State.ToString().ToUpperInvariant(), stateStyle);
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };
            valueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            stateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleRight
            };
            stateStyle.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        }
    }
}
