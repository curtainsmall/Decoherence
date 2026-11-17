using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 冲刺 HUD：左下角一个图标，配上冷却填充与剩余秒数。全部 IMGUI 绘制。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DashHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerDash dash;

        [Header("Layout")]
        [SerializeField]
        Vector2 margin = new Vector2(28f, 28f);

        [SerializeField, Min(24f)]
        float iconSize = 56f;

        [Header("Look")]
        [SerializeField]
        Color readyColor = new Color(0.45f, 0.9f, 1f);

        [SerializeField]
        Color coolingColor = new Color(0.45f, 0.9f, 1f, 0.35f);

        [SerializeField]
        Color backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.75f);

        GUIStyle labelStyle;
        GUIStyle valueStyle;

        void Awake()
        {
            if (dash == null)
            {
                dash = FindFirstObjectByType<PlayerDash>();
            }
        }

        void OnGUI()
        {
            if (dash == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyles();

            float size = iconSize;
            Rect icon = new Rect(margin.x, Screen.height - margin.y - size, size, size);

            float readiness = Mathf.Clamp01(dash.CooldownNormalized);
            bool ready = readiness >= 0.999f;

            DrawRect(new Rect(icon.x - 2f, icon.y - 2f, size + 4f, size + 4f), backgroundColor);

            // 冷却从下往上填满，满了就是可用
            Color accent = ready ? readyColor : coolingColor;

            if (readiness > 0f)
            {
                DrawRect(new Rect(icon.x, icon.y + size * (1f - readiness), size, size * readiness), new Color(accent.r, accent.g, accent.b, 0.35f));
            }

            DrawFrame(icon, ready ? readyColor : new Color(accent.r, accent.g, accent.b, 0.55f));

            labelStyle.normal.textColor = ready ? readyColor : new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(icon, ready ? ">>" : Mathf.CeilToInt(dash.CooldownRemaining).ToString(), valueStyle);

            GUI.Label(new Rect(icon.x - 8f, icon.yMax + 2f, size + 16f, 16f), "SPACE", labelStyle);
        }

        static void DrawFrame(Rect rect, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), color);
            DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), color);
            DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
            DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), color);
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void EnsureStyles()
        {
            if (labelStyle != null)
            {
                return;
            }

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 10,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
