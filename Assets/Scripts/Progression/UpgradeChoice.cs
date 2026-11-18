using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Decoherence
{
    /// <summary>
    /// 波间奖励：清完一波后冻结世界，给出 3 个（拿到四选一后为 4 个）候选，
    /// 选一个立即生效。全部 IMGUI 绘制，不依赖美术资源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UpgradeChoice : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        WaveDirector director;

        [SerializeField, HideInInspector]
        PlayerStats stats;

        [SerializeField]
        UpgradeCatalog catalog;

        [Header("Layout")]
        [SerializeField, Min(1)]
        int baseOptionCount = 3;

        [SerializeField]
        Vector2 cardSize = new Vector2(230f, 150f);

        [SerializeField, Min(0f)]
        float cardGap = 18f;

        [Header("Look")]
        [SerializeField]
        Color backdropColor = new Color(0.004f, 0.008f, 0.014f, 0.72f);

        [SerializeField]
        Color cardColor = new Color(0.02f, 0.04f, 0.06f, 1f);

        [SerializeField]
        Color accentColor = new Color(0.35f, 0.9f, 1f);

        readonly List<Upgrade> options = new List<Upgrade>();

        public bool IsOpen { get; private set; }

        GUIStyle titleStyle;
        GUIStyle cardTitleStyle;
        GUIStyle bodyStyle;
        GUIStyle hintStyle;

        void Awake()
        {
            if (director == null)
            {
                director = FindFirstObjectByType<WaveDirector>();
            }

            if (stats == null)
            {
                stats = FindFirstObjectByType<PlayerStats>();
            }
        }

        void OnEnable()
        {
            if (director != null)
            {
                director.WaveCleared += OnWaveCleared;
            }
        }

        void OnDisable()
        {
            if (director != null)
            {
                director.WaveCleared -= OnWaveCleared;
            }
        }

        void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            for (int i = 0; i < options.Count && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame)
                {
                    Choose(i);

                    return;
                }
            }
        }

        void OnGUI()
        {
            if (!IsOpen)
            {
                return;
            }

            EnsureStyles();

            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), backdropColor);

            float totalWidth = options.Count * cardSize.x + Mathf.Max(0, options.Count - 1) * cardGap;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float cardY = Screen.height * 0.5f - cardSize.y * 0.5f + 20f;

            titleStyle.normal.textColor = accentColor;
            GUI.Label(new Rect(0f, cardY - 90f, Screen.width, 40f), "选择一项奖励", titleStyle);

            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.5f);
            GUI.Label(new Rect(0f, cardY - 48f, Screen.width, 22f), "点击 或 按数字键", hintStyle);

            for (int i = 0; i < options.Count; i++)
            {
                var card = new Rect(startX + i * (cardSize.x + cardGap), cardY, cardSize.x, cardSize.y);

                if (DrawCard(card, options[i], i + 1))
                {
                    Choose(i);

                    return;
                }
            }
        }

        bool DrawCard(Rect rect, Upgrade upgrade, int index)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Mouse mouse = Mouse.current;
            bool clicked = hover && mouse != null && mouse.leftButton.wasPressedThisFrame;

            DrawRect(rect, cardColor);
            DrawFrame(rect, hover ? Color.white : accentColor);

            cardTitleStyle.normal.textColor = accentColor;
            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, rect.width - 28f, 28f), upgrade.Title, cardTitleStyle);

            bodyStyle.normal.textColor = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 46f, rect.width - 28f, 60f), upgrade.Description, bodyStyle);

            int have = stats != null ? stats.Stacks(upgrade) : 0;

            hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(rect.x + 14f, rect.yMax - 44f, rect.width - 28f, 18f), "已获得 " + have + " / " + upgrade.MaxStacks, hintStyle);
            GUI.Label(new Rect(rect.x + 14f, rect.yMax - 26f, rect.width - 28f, 18f), "按 " + index, hintStyle);

            return clicked;
        }

        void OnWaveCleared(int wave)
        {
            Open();
        }

        void Open()
        {
            if (catalog == null || stats == null)
            {
                return;
            }

            options.Clear();

            var pool = new List<Upgrade>();

            for (int i = 0; i < catalog.Count; i++)
            {
                Upgrade candidate = catalog[i];

                if (stats.CanTake(candidate))
                {
                    pool.Add(candidate);
                }
            }

            int count = Mathf.Min(stats.Has(PlayerFlag.FourChoices) ? baseOptionCount + 1 : baseOptionCount, pool.Count);

            for (int i = 0; i < count; i++)
            {
                int pick = Random.Range(0, pool.Count);
                options.Add(pool[pick]);
                pool.RemoveAt(pick);
            }

            if (options.Count == 0)
            {
                return;
            }

            IsOpen = true;
            Time.timeScale = 0f;
        }

        void Choose(int index)
        {
            if (index < 0 || index >= options.Count)
            {
                return;
            }

            stats?.Take(options[index]);

            options.Clear();
            IsOpen = false;
            Time.timeScale = 1f;
        }

        static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        static void DrawFrame(Rect rect, Color color)
        {
            DrawRect(new Rect(rect.x, rect.y, rect.width, 2f), color);
            DrawRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), color);
            DrawRect(new Rect(rect.x, rect.y, 2f, rect.height), color);
            DrawRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), color);
        }

        void EnsureStyles()
        {
            if (titleStyle != null)
            {
                return;
            }

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 26,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            cardTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true
            };

            hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}
