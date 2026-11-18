using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Decoherence
{
    public enum GamePhase
    {
        Title = 0,
        Intro = 1,
        Playing = 2,
        GameOver = 3,
        Paused = 4,
    }

    /// <summary>
    /// 流程：全息标题 → 角色入场 → 游玩 →（ESC 暂停）→ 结束。
    /// 标题时世界静止、相机偏到一侧让角色站在画面右侧；按下开始后相机移回中间再放行。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameFlow : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        WaveDirector director;

        [SerializeField, HideInInspector]
        PlayerCoherence player;

        [SerializeField, HideInInspector]
        ScoreBoard score;

        [SerializeField, HideInInspector]
        UpgradeChoice upgradeChoice;

        [SerializeField]
        GameObject hudRoot;

        [SerializeField]
        Transform cameraRig;

        [Header("Camera")]
        [Tooltip("标题画面里相机相对角色向左偏移的距离，让角色出现在画面右侧")]
        [SerializeField]
        float titleCameraOffset = -5.5f;

        [Tooltip("开始后相机回到中间的时长（秒，不受 timeScale 影响）")]
        [SerializeField, Min(0.05f)]
        float introDuration = 0.9f;

        [Header("Look")]
        [SerializeField]
        Color holoColor = new Color(0.35f, 0.9f, 1f);

        [SerializeField]
        Color gameOverColor = new Color(1f, 0.45f, 0.3f);

        public GamePhase Phase { get; private set; }

        Vector3 cameraBasePosition;
        float introTimer;
        bool introStarted;

        /// <summary>重开时置位：场景重载后跳过标题直接开始。</summary>
        static bool autoStartOnLoad;

        bool confirmingExit;

        GUIStyle titleStyle;
        GUIStyle buttonStyle;
        GUIStyle valueStyle;

        void Awake()
        {
            if (director == null)
            {
                director = FindFirstObjectByType<WaveDirector>();
            }

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCoherence>();
            }

            if (score == null)
            {
                score = FindFirstObjectByType<ScoreBoard>();
            }

            if (upgradeChoice == null)
            {
                upgradeChoice = FindFirstObjectByType<UpgradeChoice>();
            }

            if (cameraRig == null)
            {
                cameraRig = transform.root.Find("Player/CameraRig");
            }

            if (cameraRig != null)
            {
                cameraBasePosition = cameraRig.localPosition;
            }

            if (autoStartOnLoad)
            {
                autoStartOnLoad = false;
                StartRun();

                return;
            }

            EnterTitle();
        }

        void OnEnable()
        {
            if (player != null)
            {
                player.Died += OnPlayerDied;
            }
        }

        void OnDisable()
        {
            if (player != null)
            {
                player.Died -= OnPlayerDied;
            }
        }

        void Update()
        {
            switch (Phase)
            {
                case GamePhase.Playing:
                    // 奖励选择期间暂停界面不响应 ESC，避免两个面板叠加
                    if (upgradeChoice != null && upgradeChoice.IsOpen)
                    {
                        break;
                    }

                    if (EscapePressed())
                    {
                        Pause();
                    }

                    break;

                case GamePhase.Paused:
                    if (EscapePressed())
                    {
                        if (confirmingExit)
                        {
                            confirmingExit = false;

                            break;
                        }

                        Resume();
                    }

                    break;

                case GamePhase.Intro:
                    UpdateIntro();
                    break;

                case GamePhase.GameOver:
                    if (RestartPressed())
                    {
                        Restart();
                    }

                    break;
            }
        }

        void UpdateIntro()
        {
            introTimer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(introTimer / introDuration);
            ApplyCameraOffset(Mathf.Lerp(titleCameraOffset, 0f, Mathf.SmoothStep(0f, 1f, t)));

            if (t >= 1f)
            {
                StartRun();
            }
        }

        void OnGUI()
        {
            switch (Phase)
            {
                case GamePhase.Title:
                    DrawTitle();
                    break;

                case GamePhase.Paused:
                    DrawPause();
                    break;

                case GamePhase.GameOver:
                    DrawGameOver();
                    break;
            }
        }

        void EnterTitle()
        {
            Phase = GamePhase.Title;
            Time.timeScale = 0f;

            ApplyCameraOffset(titleCameraOffset);

            if (director != null)
            {
                director.enabled = false;
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
        }

        void BeginIntro()
        {
            if (Phase != GamePhase.Title)
            {
                return;
            }

            Phase = GamePhase.Intro;
            introTimer = 0f;
            introStarted = true;
        }

        void StartRun()
        {
            Phase = GamePhase.Playing;
            Time.timeScale = 1f;
            ApplyCameraOffset(0f);

            if (director != null)
            {
                director.enabled = true;
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(true);
            }
        }

        void OnPlayerDied()
        {
            Phase = GamePhase.GameOver;
            Time.timeScale = 0f;

            if (director != null)
            {
                director.enabled = false;
            }

            if (hudRoot != null)
            {
                hudRoot.SetActive(false);
            }
        }

        /// <summary>重开：场景重载后直接进游戏，跳过标题。</summary>
        void Restart()
        {
            autoStartOnLoad = true;
            ReloadScene();
        }

        /// <summary>暂停：世界冻结，保留 HUD。</summary>
        void Pause()
        {
            Phase = GamePhase.Paused;
            confirmingExit = false;
            Time.timeScale = 0f;
        }

        /// <summary>继续游戏。</summary>
        void Resume()
        {
            Phase = GamePhase.Playing;
            confirmingExit = false;
            Time.timeScale = 1f;
        }

        /// <summary>回标题：场景重载后停在标题页。</summary>
        void BackToTitle()
        {
            autoStartOnLoad = false;
            ReloadScene();
        }

        static void ReloadScene()
        {
            Time.timeScale = 1f;

            Scene scene = SceneManager.GetActiveScene();

            if (scene.buildIndex >= 0)
            {
                SceneManager.LoadScene(scene.buildIndex);

                return;
            }

            SceneManager.LoadScene(scene.name);
        }

        void ExitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ApplyCameraOffset(float offset)
        {
            if (cameraRig == null)
            {
                return;
            }

            cameraRig.localPosition = cameraBasePosition + Vector3.right * offset;
        }

        static bool RestartPressed()
        {
            Keyboard keyboard = Keyboard.current;

            return keyboard != null && keyboard.rKey.wasPressedThisFrame;
        }

        static bool EscapePressed()
        {
            Keyboard keyboard = Keyboard.current;

            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }

        // ---------- 全息标题 ----------

        void DrawTitle()
        {
            EnsureStyles();

            float panelWidth = Mathf.Min(Screen.width * 0.4f, 440f);
            float x = Screen.width * 0.08f;
            float top = Screen.height * 0.5f - 200f;

            titleStyle.normal.textColor = holoColor;
            GUI.Label(new Rect(x, top, panelWidth, 68f), "DECOHERENCE", titleStyle);

            if (HoloButton(new Rect(x, top + 96f, panelWidth, 58f), "START GAME"))
            {
                BeginIntro();
            }

            if (HoloButton(new Rect(x, top + 168f, panelWidth, 58f), "EXIT"))
            {
                ExitGame();
            }
        }

        bool HoloButton(Rect rect, string label)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Mouse mouse = Mouse.current;
            bool clicked = hover && mouse != null && mouse.leftButton.wasPressedThisFrame;

            Color fill = hover ? new Color(0.06f, 0.16f, 0.2f, 1f) : new Color(0.02f, 0.04f, 0.06f, 1f);

            DrawRect(rect, fill);
            DrawFrame(rect, hover ? new Color(0.85f, 1f, 1f, 1f) : holoColor);

            buttonStyle.normal.textColor = hover ? Color.white : holoColor;
            GUI.Label(rect, label, buttonStyle);

            return clicked;
        }

        // ---------- 暂停 ----------

        void DrawPause()
        {
            EnsureStyles();

            DrawBackdrop(new Color(0.01f, 0.015f, 0.025f, 0.72f));

            const float buttonWidth = 300f;
            const float buttonHeight = 56f;
            const float gap = 18f;

            float x = (Screen.width - buttonWidth) * 0.5f;
            float y = Screen.height * 0.5f - 150f;

            titleStyle.normal.textColor = holoColor;
            GUI.Label(new Rect(x, y, buttonWidth, 64f), "PAUSED", titleStyle);

            if (confirmingExit)
            {
                valueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);
                GUI.Label(new Rect(x, y + 84f, buttonWidth, 24f), "返回标题会放弃本局进度", valueStyle);

                if (HoloButton(new Rect(x, y + 132f, buttonWidth, buttonHeight), "CONFIRM"))
                {
                    BackToTitle();
                }

                if (HoloButton(new Rect(x, y + 132f + buttonHeight + gap, buttonWidth, buttonHeight), "CANCEL"))
                {
                    confirmingExit = false;
                }

                return;
            }

            if (HoloButton(new Rect(x, y + 96f, buttonWidth, buttonHeight), "RESUME"))
            {
                Resume();
            }

            if (HoloButton(new Rect(x, y + 96f + buttonHeight + gap, buttonWidth, buttonHeight), "BACK TO TITLE"))
            {
                confirmingExit = true;
            }
        }

        // ---------- 结束 ----------

        void DrawGameOver()
        {
            EnsureStyles();

            DrawBackdrop(new Color(0.01f, 0.015f, 0.025f, 0.9f));

            float width = Mathf.Min(Screen.width - 80f, 720f);
            float x = (Screen.width - width) * 0.5f;
            float y = Screen.height * 0.5f - 110f;

            titleStyle.normal.textColor = gameOverColor;
            GUI.Label(new Rect(x, y, width, 64f), "GAME OVER", titleStyle);

            valueStyle.normal.textColor = new Color(1f, 1f, 1f, 0.85f);

            int finalScore = score != null ? score.Score : 0;
            int wave = director != null ? director.WaveIndex : 0;

            GUI.Label(new Rect(x, y + 74f, width, 26f), "得分 " + finalScore, valueStyle);
            GUI.Label(new Rect(x, y + 104f, width, 26f), "抵达第 " + wave + " 波", valueStyle);

            const float buttonWidth = 220f;
            const float buttonHeight = 56f;
            const float gap = 20f;

            float totalWidth = buttonWidth * 2f + gap;
            float bx = (Screen.width - totalWidth) * 0.5f;
            float by = y + 168f;

            if (HoloButton(new Rect(bx, by, buttonWidth, buttonHeight), "RESTART"))
            {
                Restart();
            }

            if (HoloButton(new Rect(bx + buttonWidth + gap, by, buttonWidth, buttonHeight), "BACK TO TITLE"))
            {
                BackToTitle();
            }
        }

        // ---------- 绘制工具 ----------

        void DrawBackdrop(Color color)
        {
            DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), color);
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
                fontSize = 46,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            buttonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter
            };
        }
    }
}
