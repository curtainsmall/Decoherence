using System.Collections.Generic;
using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 敌人高亮：屏幕空间轮廓描边，隔着障碍也能看见。
    /// 每帧三步：遮罩（写入模板）→ 外扩描边（只在遮罩外绘制）→ 清遮罩。
    /// 用 Graphics.DrawMesh 重画，不复制物体、不改原材质。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EnemyHighlight : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField]
        Material maskMaterial;

        [Tooltip("描边材质，颜色区分兵种")]
        [SerializeField]
        Material outlineMaterial;

        [SerializeField]
        Material clearMaterial;

        [Tooltip("留空时自动收集子物体上的渲染器")]
        [SerializeField]
        Renderer[] renderers;

        readonly List<MeshFilter> parts = new List<MeshFilter>();

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            foreach (Renderer target in renderers)
            {
                if (target != null && target.TryGetComponent(out MeshFilter filter) && filter.sharedMesh != null)
                {
                    parts.Add(filter);
                }
            }
        }

        void LateUpdate()
        {
            if (maskMaterial == null || outlineMaterial == null || clearMaterial == null || parts.Count == 0)
            {
                return;
            }

            Draw(maskMaterial);
            Draw(outlineMaterial);
            Draw(clearMaterial);
        }

        void Draw(Material material)
        {
            int layer = gameObject.layer;

            foreach (MeshFilter part in parts)
            {
                if (part == null || !part.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Graphics.DrawMesh(part.sharedMesh, part.transform.localToWorldMatrix, material, layer);
            }
        }
    }
}
