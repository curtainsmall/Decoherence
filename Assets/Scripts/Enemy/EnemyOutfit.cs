using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 换装：开局把指定名字的渲染器换成给定材质。
    /// 用运行期赋值，避免在 prefab 变体里覆盖嵌套 prefab 的材质（那条路径会把改动丢掉）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOutfit : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField]
        Material material;

        [Tooltip("要换材质的子物体名字")]
        [SerializeField]
        string[] targets = { "Body", "Head", "Arm_L", "Arm_R" };

        void Awake()
        {
            if (material == null || targets == null)
            {
                return;
            }

            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (string target in targets)
                {
                    if (renderer.gameObject.name != target)
                    {
                        continue;
                    }

                    renderer.sharedMaterial = material;
                    break;
                }
            }
        }
    }
}
