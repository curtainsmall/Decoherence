using UnityEngine;

namespace Decoherence
{
    public enum CoherenceState
    {
        Coherent = 0,

        Decoherent = 1

    }

    /// <summary>
    /// 玩家状态：相干态、两态速度上限、以及由输入源写入的“按住状态键”意图。
    /// 相干度数值本身属于 PlayerCoherence，不在这里。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerState : CharacterState
    {
        [Header("Coherence state")]
        [SerializeField]
        CoherenceState state = CoherenceState.Coherent;

        [SerializeField, Min(0f)]
        float coherentSpeed = 5f;

        [SerializeField, Min(0f)]
        float decoherentSpeed = 7f;

        [SerializeField, HideInInspector]
        PlayerStats stats;

        public CoherenceState State
        {
            get => state;
            set => state = value;
        }

        public override float MaxSpeed
        {
            get
            {
                float baseSpeed = state == CoherenceState.Decoherent ? decoherentSpeed : coherentSpeed;

                return stats != null ? baseSpeed * stats.Scale(StatKind.MoveSpeed) : baseSpeed;
            }
        }

        void Awake()
        {
            if (stats == null)
            {
                stats = GetComponent<PlayerStats>();
            }
        }

        public bool IsDecoherent => state == CoherenceState.Decoherent;

        /// <summary>输入源每帧写入的“按住状态键”意图。</summary>
        public bool HoldIntent { get; set; }
    }
}
