using System.Collections.Generic;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人血条：把每只敌人的血量投影到屏幕，画在头顶。
    /// 全部 IMGUI + 内置白色纹理绘制，不依赖美术资源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHealthBars : MonoBehaviour
    {
        [Header("Layout")]
        [SerializeField]
        float headHeight = 1.62f;

        [SerializeField]
        Vector2 barSize = new Vector2(40f, 5f);

        [SerializeField, Min(0f)]
        float offsetPixels = 10f;

        [Tooltip("刷新敌人列表的间隔，避免每帧查找")]
        [SerializeField, Min(0.05f)]
        float refreshInterval = 0.4f;

        [Header("Look")]
        [SerializeField]
        Color backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.75f);

        [SerializeField]
        Color fullColor = new Color(0.75f, 0.95f, 1f);

        [SerializeField]
        Color emptyColor = new Color(1f, 0.32f, 0.18f);

        readonly List<EnemyVitals> enemies = new List<EnemyVitals>();

        Camera view;
        float nextRefresh;

        void Update()
        {
            if (Time.time < nextRefresh)
            {
                return;
            }

            nextRefresh = Time.time + Mathf.Max(0.05f, refreshInterval);

            enemies.Clear();
            enemies.AddRange(FindObjectsByType<EnemyVitals>(FindObjectsSortMode.None));

            if (view == null)
            {
                view = Camera.main;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            if (view == null)
            {
                view = Camera.main;
            }

            if (view == null)
            {
                return;
            }

            foreach (EnemyVitals enemy in enemies)
            {
                if (enemy == null || enemy.IsDead)
                {
                    continue;
                }

                Vector3 head = enemy.transform.position + Vector3.up * headHeight;
                Vector3 screen = view.WorldToScreenPoint(head);

                if (screen.z <= 0f)
                {
                    continue;
                }

                float x = screen.x - barSize.x * 0.5f;
                float y = Screen.height - screen.y - barSize.y - offsetPixels;

                DrawRect(new Rect(x - 1f, y - 1f, barSize.x + 2f, barSize.y + 2f), backgroundColor);

                float fill = Mathf.Clamp01(enemy.Health / Mathf.Max(1f, enemy.MaxHealth));

                if (fill <= 0f)
                {
                    continue;
                }

                Color color = Color.Lerp(emptyColor, fullColor, fill);
                DrawRect(new Rect(x, y, barSize.x * fill, barSize.y), color);
            }
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
