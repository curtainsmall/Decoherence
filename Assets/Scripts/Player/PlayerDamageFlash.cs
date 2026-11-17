using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 玩家受击表现：订阅相干度的伤害事件，扣血时让身体闪一次。
    /// 只做转发，不参与结算。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamageFlash : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerCoherence coherence;

        [SerializeField, HideInInspector]
        CharacterHitFlash flash;

        void Awake()
        {
            if (coherence == null)
            {
                coherence = GetComponentInParent<PlayerCoherence>();
            }

            if (flash == null)
            {
                flash = GetComponentInChildren<CharacterHitFlash>(true);
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

        void OnDamaged(float amount)
        {
            flash?.Flash();
        }
    }
}
