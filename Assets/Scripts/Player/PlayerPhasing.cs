using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 退相干态与冲刺中的玩家是虚体：按层对关闭/恢复与敌人之间的碰撞。
    /// 用层的碰撞矩阵而不是逐对忽略，之后生成的敌人自动适用同一条规则。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerPhasing : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerState state;

        [SerializeField, HideInInspector]
        PlayerDash dash;

        [Tooltip("敌人的层；留空时按层名 Enemy 解析")]
        [SerializeField]
        LayerMask enemyLayers;

        bool phasedThroughEnemies;

        void Awake()
        {
            if (state == null)
            {
                state = GetComponent<PlayerState>();
            }

            if (dash == null)
            {
                dash = GetComponent<PlayerDash>();
            }

            if (enemyLayers.value == 0)
            {
                enemyLayers = LayerMask.GetMask("Enemy");
            }

            Apply(false);
        }

        void Update()
        {
            if (state == null)
            {
                return;
            }

            // 退相干态或冲刺中都能穿过敌人
            bool phased = state.IsDecoherent || (dash != null && dash.IsDashing);

            if (phased == phasedThroughEnemies)
            {
                return;
            }

            phasedThroughEnemies = phased;
            Apply(phased);
        }

        void Apply(bool ignore)
        {
            int playerLayer = gameObject.layer;

            for (int layer = 0; layer < 32; layer++)
            {
                if ((enemyLayers.value & (1 << layer)) == 0 || layer == playerLayer)
                {
                    continue;
                }

                Physics.IgnoreLayerCollision(playerLayer, layer, ignore);
            }
        }
    }
}
