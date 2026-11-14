using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 角色外观的唯一参数入口：把「胶囊身体 + 球头 + 可选手臂」的几何参数应用到节点上。
    /// 只摆 Transform 与材质，不含任何玩法数值，也不做动画——动画由 CharacterRig 写通道，
    /// 二者不抢同一个 Transform 的写权限（squash 只作用于 body 节点，且走 SetSquash）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShapeRig : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("胶囊身体 mesh；整棵子树里唯一允许被 squash 的节点")]
        [SerializeField] private Transform body;
        [Tooltip("承载头部滞后/倾斜的空节点")]
        [SerializeField] private Transform headPivot;
        [SerializeField] private Transform head;
        [SerializeField] private Transform shoulderLeft;
        [SerializeField] private Transform shoulderRight;
        [SerializeField] private Transform handLeft;
        [SerializeField] private Transform handRight;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;

        [Header("Capability")]
        [Tooltip("关闭时整条臂链（肩/手/臂/插槽）失效，相关参数不参与计算")]
        [SerializeField] private bool hasArm = false;

        [Header("Body")]
        [SerializeField, Min(0.05f)] private float bodyHeight = 1.30f;
        [SerializeField, Min(0.02f)] private float bodyRadius = 0.32f;

        [Header("Head")]
        [SerializeField, Min(0.02f)] private float headRadius = 0.30f;
        [Tooltip("头陷进身体的高度")]
        [SerializeField, Min(0f)] private float headOverlap = 0.10f;

        [Header("Arm")]
        [Tooltip("臂长（肩到手的距离）")]
        [SerializeField, Min(0.05f)] private float armLength = 0.45f;
        [SerializeField, Min(0.01f)] private float armThickness = 0.08f;
        [Tooltip("肩高占身体高度的比例")]
        [SerializeField, Range(0f, 1f)] private float shoulderHeight = 0.85f;
        [Tooltip("肩的水平位置占身体半径的比例")]
        [SerializeField, Range(0f, 1.5f)] private float shoulderSpread = 0.95f;

        [Header("Materials")]
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material headMaterial;
        [SerializeField] private Material armMaterial;

        private float squash = 1f;
        private Vector3 headBasePosition = Vector3.zero;

        public bool HasArm => hasArm;
        public float BodyHeight => Mathf.Max(bodyRadius * 2f + 0.001f, bodyHeight) * squash;
        public float BodyRadius => Mathf.Max(0.02f, bodyRadius);
        public float HeadRadius => Mathf.Max(0.02f, headRadius);
        public float Squash => squash;
        public Transform Body => body;
        public Transform HeadPivot => headPivot;
        /// <summary>头部枢轴的基础本地位置；动画只在其上叠加偏移，不要覆盖。</summary>
        public Vector3 HeadPivotBasePosition => headBasePosition;

        private void Awake()
        {
            ResolveReferences();
            Apply();
        }

        private void OnValidate()
        {
            ResolveReferences();
            Apply();
        }

        /// <summary>按参数重建几何。幂等：重复调用结果一致。</summary>
        public void Apply()
        {
            ResolveReferences();

            if (body == null)
            {
                return;
            }

            ApplyBodyTransform();

            float headScale = Mathf.Max(0.02f, headRadius) * 2f;

            if (head != null)
            {
                head.localPosition = Vector3.zero;
                head.localRotation = Quaternion.identity;
                head.localScale = new Vector3(headScale, headScale, headScale);
                SetMaterial(head, headMaterial);
            }

            ApplyArm(shoulderLeft, handLeft, armLeft, -1f);
            ApplyArm(shoulderRight, handRight, armRight, 1f);

            SetActive(shoulderLeft, hasArm);
            SetActive(shoulderRight, hasArm);

            SetMaterial(body, bodyMaterial);
        }

        /// <summary>竖直压扁系数，1 为原始高度。脚底保持在本地 y = 0。</summary>
        public void SetSquash(float factor)
        {
            float clamped = Mathf.Clamp(factor, 0.2f, 2f);

            if (Mathf.Approximately(clamped, squash))
            {
                return;
            }

            squash = clamped;
            ApplyBodyTransform();
        }

        private void ApplyBodyTransform()
        {
            if (body == null)
            {
                return;
            }

            float radius = Mathf.Max(0.02f, bodyRadius);
            float height = Mathf.Max(radius * 2f + 0.001f, bodyHeight);
            float vertical = height * squash;

            // Unity 胶囊原始尺寸：高 2、直径 1
            body.localScale = new Vector3(radius * 2f, vertical * 0.5f, radius * 2f);
            body.localPosition = new Vector3(0f, vertical * 0.5f, 0f);

            if (headPivot != null)
            {
                float overlap = Mathf.Clamp(headOverlap, 0f, Mathf.Max(0.02f, headRadius) * 2f);
                float pivotY = vertical - overlap + Mathf.Max(0.02f, headRadius);

                headBasePosition = new Vector3(0f, pivotY, 0f);
                headPivot.localPosition = headBasePosition;
                headPivot.localRotation = Quaternion.identity;
                headPivot.localScale = Vector3.one;
            }
        }

        private void ApplyArm(Transform shoulder, Transform hand, Transform arm, float side)
        {
            if (shoulder == null)
            {
                return;
            }

            float radius = Mathf.Max(0.02f, bodyRadius);
            float height = Mathf.Max(radius * 2f + 0.001f, bodyHeight) * squash;
            float length = Mathf.Max(0.05f, armLength);
            float thickness = Mathf.Max(0.01f, armThickness);

            shoulder.localPosition = new Vector3(radius * shoulderSpread * side, height * Mathf.Clamp01(shoulderHeight), 0f);
            shoulder.localRotation = Quaternion.identity;
            shoulder.localScale = Vector3.one;

            if (hand != null)
            {
                // 手在肩的下方 armLength 处；臂作为手的子物体往上长出，长度变化不会推动手
                hand.localPosition = new Vector3(0f, -length, 0f);
                hand.localRotation = Quaternion.identity;
                hand.localScale = Vector3.one;
            }

            if (arm != null)
            {
                arm.localPosition = new Vector3(0f, length * 0.5f, 0f);
                arm.localRotation = Quaternion.identity;
                arm.localScale = new Vector3(thickness * 2f, length * 0.5f, thickness * 2f);
                SetMaterial(arm, armMaterial);
            }
        }

        private void ResolveReferences()
        {
            if (body == null)
            {
                body = FindChild("Capsule");
            }

            if (headPivot == null)
            {
                headPivot = FindChild("HeadPivot");
            }

            if (head == null)
            {
                head = FindChild("Head");
            }

            if (shoulderLeft == null)
            {
                shoulderLeft = FindChild("Shoulder_L");
            }

            if (shoulderRight == null)
            {
                shoulderRight = FindChild("Shoulder_R");
            }

            if (handLeft == null)
            {
                handLeft = FindChild("Hand_L");
            }

            if (handRight == null)
            {
                handRight = FindChild("Hand_R");
            }

            if (armLeft == null)
            {
                armLeft = FindChild("Arm_L");
            }

            if (armRight == null)
            {
                armRight = FindChild("Arm_R");
            }
        }

        private Transform FindChild(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i] != transform && all[i].name == childName)
                {
                    return all[i];
                }
            }

            return null;
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null && target.gameObject.activeSelf != active)
            {
                target.gameObject.SetActive(active);
            }
        }

        private static void SetMaterial(Transform target, Material material)
        {
            if (target == null || material == null)
            {
                return;
            }

            MeshRenderer renderer = target.GetComponent<MeshRenderer>();

            if (renderer != null && renderer.sharedMaterial != material)
            {
                renderer.sharedMaterial = material;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);

            if (shoulderLeft != null && handLeft != null)
            {
                Gizmos.DrawLine(shoulderLeft.position, handLeft.position);
            }

            if (shoulderRight != null && handRight != null)
            {
                Gizmos.DrawLine(shoulderRight.position, handRight.position);
            }

            if (body != null)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
                Gizmos.DrawWireSphere(body.position, Mathf.Max(0.02f, bodyRadius));
            }
        }
    }
}
