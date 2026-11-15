using System;
using UnityEngine;

namespace Decoherence
{
    public enum WaveState
    {
        Idle = 0,
        Spawning = 1,
        Clearing = 2,
        Reward = 3,
        Ended = 4,
    }

    /// <summary>
    /// 波次调度：Spawning → Clearing → Reward → 下一波，直到玩家死亡。
    /// 只推进状态与计时，生成交给 EnemySpawner，敌人行为留在敌人自己身上。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WaveDirector : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField]
        EnemySpawner spawner;

        [SerializeField]
        PlayerCoherence player;

        [Header("Pacing")]
        [Tooltip("每波持续生成的时长")]
        [SerializeField, Min(1f)]
        float spawnDuration = 8f;

        [Tooltip("3 选 1 的停留时长")]
        [SerializeField, Min(0f)]
        float rewardDuration = 3f;

        public WaveState State { get; private set; }

        public int WaveIndex { get; private set; }

        public int AliveCount => spawner != null ? spawner.AliveCount : 0;

        public event Action<int> WaveStarted;

        public event Action<int> WaveCleared;

        float timer;

        void Awake()
        {
            ResolveReferences();
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

        void Start()
        {
            if (spawner != null)
            {
                BeginWave(1);
            }
        }

        void Update()
        {
            if (State == WaveState.Idle || State == WaveState.Ended)
            {
                return;
            }

            timer -= Time.deltaTime;

            switch (State)
            {
                case WaveState.Spawning:
                    if (timer <= 0f)
                    {
                        State = WaveState.Clearing;
                    }
                    break;

                case WaveState.Clearing:
                    if (AliveCount <= 0)
                    {
                        ClearWave();
                    }
                    break;

                case WaveState.Reward:
                    if (timer <= 0f)
                    {
                        BeginWave(WaveIndex + 1);
                    }
                    break;
            }
        }

        void BeginWave(int index)
        {
            WaveIndex = index;
            State = WaveState.Spawning;
            timer = spawnDuration;
            WaveStarted?.Invoke(index);
        }

        void ClearWave()
        {
            State = WaveState.Reward;
            timer = rewardDuration;
            WaveCleared?.Invoke(WaveIndex);
        }

        void OnPlayerDied()
        {
            State = WaveState.Ended;
        }

        void ResolveReferences()
        {
            if (spawner == null)
            {
                spawner = GetComponent<EnemySpawner>();
            }

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCoherence>();
            }
        }
    }
}
