using UnityEngine;

namespace Decoherence
{
    /// <summary>近战：目标进入接触距离且冷却结束时，直接扣玩家相干。</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyMeleeAttack : EnemyAttack
    {
        public override bool TryAttack()
        {
            if (!IsReady || !InRange)
            {
                return false;
            }

            DrainPlayer();
            StartCooldown();

            return true;
        }
    }
}
