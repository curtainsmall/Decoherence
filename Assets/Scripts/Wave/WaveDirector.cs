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
        [SerializeField, HideInInspector]
        EnemySpawner spawner;

        [SerializeField, HideInInspector]
        PlayerCoherence player;

        [Tooltip("波次表：决定每一波出哪些敌人、几只、间隔多久")]
        [SerializeField]
        WaveProfile profile;

        [Header("Pacing")]
        [Tooltip("3 选 1 的停留时长")]
        [SerializeField, Min(0f)]
        float rewardDuration = 3f;

        public WaveState State { get; private set; }

        public int WaveIndex { get; private set; }

        public int AliveCount => spawner != null ? spawner.AliveCount : 0;

        /// <summary>本波还剩多少敌人：场上存活 + 尚未生成。</summary>
        public int RemainingCount
        {
            get
            {
                if (profile == null)
                {
                    return AliveCount;
                }

                WaveEntry entry = profile.GetWave(WaveIndex);

                return Mathf.Max(0, AliveCount + Mathf.Max(0, entry.Count - spawnedThisWave));
            }
        }

        public event Action<int> WaveStarted;

        public event Action<int> WaveCleared;

        float timer;
        float spawnTimer;
        int spawnedThisWave;

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

            switch (State)
            {
                case WaveState.Spawning:
                    UpdateSpawning();
                    break;

                case WaveState.Clearing:
                    if (AliveCount <= 0)
                    {
                        ClearWave();
                    }
                    break;

                case WaveState.Reward:
                    timer -= Time.deltaTime;

                    if (timer <= 0f)
                    {
                        BeginWave(WaveIndex + 1);
                    }
                    break;
            }
        }

        void UpdateSpawning()
        {
            if (spawner == null || profile == null)
            {
                State = WaveState.Clearing;
                return;
            }

            WaveEntry entry = profile.GetWave(WaveIndex);

            spawnTimer -= Time.deltaTime;

            while (spawnTimer <= 0f && spawnedThisWave < entry.Count)
            {
                if (AliveCount >= entry.MaxAlive)
                {
                    break;
                }

                EnemyDefinition definition = entry.PickDefinition();

                if (definition == null || spawner.Spawn(definition) == null)
                {
                    break;
                }

                spawnedThisWave++;
                spawnTimer += entry.Interval;
            }

            if (spawnedThisWave >= entry.Count)
            {
                State = WaveState.Clearing;
            }
        }

        void BeginWave(int index)
        {
            WaveIndex = index;
            State = WaveState.Spawning;
            spawnedThisWave = 0;
            spawnTimer = 0f;
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
                spawner = GetComponentInChildren<EnemySpawner>(true);
            }

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerCoherence>();
            }
        }
    }
}
