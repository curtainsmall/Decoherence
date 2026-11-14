using UnityEngine;

namespace Decoherence
{
    /// <summary>插槽的类型。角色身上提供插槽，装备按键去找，不互相持有引用。</summary>
    public enum SocketKey
    {
        None = 0,
        HandLeft = 1,
        HandRight = 2,
        Back = 3,
        Head = 4,

        // 以下属于武器自身
        Grip = 10,
        ForeGrip = 11,
        Muzzle = 12,
    }

    /// <summary>
    /// 挂载点：一个只带 key 的空节点。
    /// 它不记录自己被谁挂载——谁挂着东西由持有者（EquipSlot）的字段说明，
    /// 这样禁用/销毁插槽时只需要让查询失败，没有双向引用要清理。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Socket : MonoBehaviour
    {
        [SerializeField] private SocketKey key = SocketKey.None;
        [SerializeField] private float gizmoRadius = 0.05f;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.78f, 0.25f, 1f);

        public SocketKey Key => key;

        /// <summary>按键在子树里找插槽；默认跳过未激活的节点，因此禁用臂就等于禁用其插槽。</summary>
        public static Socket Find(Transform root, SocketKey key)
        {
            if (root == null || key == SocketKey.None)
            {
                return null;
            }

            Socket[] sockets = root.GetComponentsInChildren<Socket>(false);

            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i] != null && sockets[i].key == key)
                {
                    return sockets[i];
                }
            }

            return null;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = gizmoColor;
            Gizmos.DrawWireSphere(transform.position, gizmoRadius);
            Gizmos.DrawRay(transform.position, transform.forward * (gizmoRadius * 3f));
        }

        private void OnValidate()
        {
            gizmoRadius = Mathf.Max(0.005f, gizmoRadius);
        }
    }
}
