using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 相干度 HUD：底部进度条、状态文字、受击时的红闪、退相干时的屏幕边缘脉冲。
    /// 全部用 IMGUI 与内置白色纹理绘制，不依赖任何美术资源。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CoherenceHud : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerCoherence coherence;

        [Header("Layout")]
        [SerializeField]
        Vector2 barSize = new Vector2(340f, 16f);

        [SerializeField, Min(0f)]
        float bottomMargin = 40f;

        [SerializeField, Min(0f)]
        float edgeWidth = 10f;

        [Header("Look")]
        [SerializeField]
        Color coherentColor = new Color(0.25f, 0.85f, 1f);

        [SerializeField]
        Color decoherentColor = new Color(1f, 0.32f, 0.18f);

        [SerializeField]
        Color damageColor = new Color(1f, 0.15f, 0.1f);

        [SerializeField, Min(0.01f)]
        float damageFlashDuration = 0.14f;

        [SerializeField, Min(0f)]
        float damageFlashAlpha = 0.16f;

        [SerializeField, Min(0f)]
        float edgePulseSpeed = 2.2f;

        [SerializeField, Min(0f)]
        float edgePulseAlpha = 0.26f;

        GUIStyle stateStyle;
        GUIStyle valueStyle;
        float shownLevel;
        float damageFlashUntil;

        void Awake()
        {
            if (coherence == null)
            {
                coherence = FindFirstObjectByType<PlayerCoherence>();
            }

            if (coherence != null)
            {
                shownLevel = coherence.Normalized;
            }
        }

        void OnEnable()
        {
            if (coherence != null)
            {
                coherence.Damaged += OnDamaged;
            }
        }

        void OnDisable()
        {
            if (coherence != null)
            {
                coherence.Damaged -= OnDamaged;
            }
        }

        void Update()
        {
            if (coherence == null)
            {
                return;
            }

            shownLevel = Mathf.MoveTowards(shownLevel, coherence.Normalized, Time.deltaTime * 2.5f);
        }

        void OnGUI()
        {
            if (coherence == null)
            {
                return;
            }

            EnsureStyles();

            bool decoherent = coherence.IsDecoherent;
            Color accent = decoherent ? decoherentColor : coherentColor;

            DrawBar(accent);
            DrawState(accent, decoherent);

            if (decoherent)
            {
                DrawEdgePulse(accent);
            }

            DrawDamageFlash();
        }

        void OnDamaged(float amount)
        {
            damageFlashUntil = Time.time + damageFlashDuration;
        }

        void DrawBar(Color accent)
        {
            Rect bar = new Rect(
                (Screen.width - barSize.x) * 0.5f,
                Screen.height - bottomMargin - barSize.y,
                barSize.x,
                barSize.y);

            DrawRect(bar, new Color(0.02f, 0.03f, 0.05f, 0.85f));

            float fill = Mathf.Clamp01(shownLevel);

            if (fill > 0f)
            {
                Rect inner = new Rect(bar.x + 1f, bar.y + 1f, (bar.width - 2f) * fill, bar.height - 2f);
                DrawRect(inner, accent);
            }

            Color border = new Color(accent.r, accent.g, accent.b, 0.85f);

            DrawRect(new Rect(bar.x, bar.y, bar.width, 1f), border);
            DrawRect(new Rect(bar.x, bar.yMax - 1f, bar.width, 1f), border);
            DrawRect(new Rect(bar.x, bar.y, 1f, bar.height), border);
            DrawRect(new Rect(bar.xMax - 1f, bar.y, 1f, bar.height), border);
        }

        void DrawState(Color accent, bool decoherent)
        {
            Rect line = new Rect(
                (Screen.width - barSize.x) * 0.5f,
                Screen.height - bottomMargin - barSize.y - 24f,
                barSize.x,
                18f);

            stateStyle.normal.textColor = accent;
            valueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.75f);

            GUI.Label(line, decoherent ? "DECOHERENT" : "COHERENT", stateStyle);
            GUI.Label(line, Mathf.RoundToInt(coherence.Coherence) + " / " + Mathf.RoundToInt(coherence.MaxCoherence), valueStyle);
        }

        void DrawEdgePulse(Color accent)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * edgePulseSpeed * Mathf.PI * 2f);
            Color color = new Color(accent.r, accent.g, accent.b, edgePulseAlpha * pulse);

            DrawRect(new Rect(0f, 0f, Screen.width, edgeWidth), color);
            DrawRect(new Rect(0f, Screen.height - edgeWidth, Screen.width, edgeWidth), color);
            DrawRect(new Rect(0f, 0f, edgeWidth, Screen.height), color);
            DrawRect(new Rect(Screen.width - edgeWidth, 0f, edgeWidth, Screen.height), color);
        }

        void DrawDamageFlash()
        {
            if (Time.time >= damageFlashUntil)
            {
                return;
            }

            float remaining = Mathf.InverseLerp(damageFlashUntil - damageFlashDuration, damageFlashUntil, Time.time);
            Color color = new Color(damageColor.r, damageColor.g, damageColor.b, damageFlashAlpha * remaining);

            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), color);
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
            if (stateStyle != null)
            {
                return;
            }

            stateStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleRight
            };
        }
    }
}
