using UnityEngine;
using UnityEngine.Rendering;

namespace Decoherence
{
    /// <summary>
    /// 贴地的碰撞半径圆环（环状网格，无贴图、无光源依赖）。
    /// 默认自动读取父级 CharacterController 的半径，因此角色尺寸变化时圆环同步。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class GroundRing : MonoBehaviour
    {
        [Header("Placement")]
        [Tooltip("圆环跟随的目标；留空时自动查找场景中的 PlayerMotor")]
        [SerializeField] private Transform follow;
        [Tooltip("朝向来源；留空时取跟随目标的 Body 子物体")]
        [SerializeField] private Transform facing;
        [SerializeField] private float surfaceOffset = 0.012f;
        [SerializeField] private float radialOffset = 0f;

        [Header("Arc")]
        [Tooltip("360 为整环；小于 360 时以局部 +Z 为中心绘制一段弧")]
        [SerializeField, Range(10f, 360f)] private float sweepDegrees = 360f;
        [SerializeField] private bool arrowHead = false;

        [Header("Size")]
        [SerializeField] private bool autoFromCharacterController = true;
        [SerializeField, Min(0.05f)] private float radius = 0.35f;
        [SerializeField, Min(0.005f)] private float thickness = 0.06f;
        [SerializeField, Range(12, 160)] private int segments = 48;

        [Header("Look")]
        [SerializeField] private Material material;
        [SerializeField] private Color color = Color.white;

        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private Material runtimeMaterial;

        private float builtRadius = -1f;
        private float builtThickness = -1f;
        private float builtSweep = -1f;
        private float builtRadialOffset = 0f;
        private bool builtArrow;
        private int builtSegments = -1;

        private void OnEnable()
        {
            EnsureComponents();
            Rebuild();
        }

        private void OnValidate()
        {
            EnsureComponents();
            Rebuild();
        }

        private void Update()
        {
            SyncPlacement();

            float effective = ResolveRadius();
            if (!Mathf.Approximately(builtRadius, effective)
                || !Mathf.Approximately(builtThickness, thickness)
                || !Mathf.Approximately(builtSweep, sweepDegrees)
                || !Mathf.Approximately(builtRadialOffset, radialOffset)
                || builtArrow != arrowHead
                || builtSegments != segments)
            {
                Rebuild();
            }
        }

        private void LateUpdate()
        {
            SyncPlacement();
        }

        /// <summary>把圆环贴到跟随目标脚下的地面；朝向取 facing 的水平朝向。</summary>
        private void SyncPlacement()
        {
            if (follow == null)
            {
                PlayerMotor player = FindFirstObjectByType<PlayerMotor>();
                if (player != null)
                {
                    follow = player.transform;
                }
            }

            if (follow == null || follow == transform)
            {
                return;
            }

            if (facing == null)
            {
                facing = follow.Find("Body");
            }

            transform.position = follow.position + Vector3.up * surfaceOffset;
            transform.rotation = facing != null
                ? Quaternion.Euler(0f, facing.eulerAngles.y, 0f)
                : Quaternion.identity;
        }

        private void EnsureComponents()
        {
            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshRenderer == null)
            {
                meshRenderer = GetComponent<MeshRenderer>();
            }
        }

        private float ResolveRadius()
        {
            if (!autoFromCharacterController)
            {
                return radius;
            }

            // 圆环可脱离角色层级放到场景根，因此优先从跟随目标取胶囊半径。
            CharacterController controller = follow != null
                ? follow.GetComponent<CharacterController>()
                : GetComponentInParent<CharacterController>();

            return controller != null ? controller.radius : radius;
        }

        private void Rebuild()
        {
            if (meshFilter == null || meshRenderer == null)
            {
                return;
            }

            if (mesh == null)
            {
                mesh = new Mesh { name = "GroundRing (runtime)", hideFlags = HideFlags.DontSave };
                meshFilter.sharedMesh = mesh;
            }

            float outer = ResolveRadius() + radialOffset;
            float inner = Mathf.Max(0.01f, outer - thickness);
            float sweep = Mathf.Clamp(sweepDegrees, 1f, 360f);
            bool closed = sweep >= 359.9f;
            int count = Mathf.Max(2, Mathf.RoundToInt(segments * (sweep / 360f)));

            int ringVertexCount = (count + 1) * 2;
            int arrowVertexCount = arrowHead ? 3 : 0;
            int ringTriangleCount = closed ? count * 2 : (count) * 2;

            var vertices = new Vector3[ringVertexCount + arrowVertexCount];
            var triangles = new int[ringTriangleCount * 3 + (arrowHead ? 3 : 0)];

            // 弧段以局部 +Z（角度 0）为中心，左右对称展开。
            float halfSweep = sweep * 0.5f;
            float startDegrees = -halfSweep;

            for (int i = 0; i <= count; i++)
            {
                float degrees = startDegrees + (sweep * i) / count;
                float radians = degrees * Mathf.Deg2Rad;
                float sin = Mathf.Sin(radians);
                float cos = Mathf.Cos(radians);

                vertices[i * 2] = new Vector3(sin * outer, 0f, cos * outer);
                vertices[i * 2 + 1] = new Vector3(sin * inner, 0f, cos * inner);
            }

            int t = 0;
            for (int i = 0; i < count; i++)
            {
                int v = i * 2;

                triangles[t] = v;
                triangles[t + 1] = v + 1;
                triangles[t + 2] = v + 2;

                triangles[t + 3] = v + 1;
                triangles[t + 4] = v + 3;
                triangles[t + 5] = v + 2;

                t += 6;
            }

            if (arrowHead)
            {
                // 箭头：位于局部 +Z，尖端朝外。
                int baseIndex = ringVertexCount;
                float headLength = thickness * 2.2f;
                float headHalfWidth = thickness * 1.6f;

                vertices[baseIndex] = new Vector3(0f, 0f, outer + headLength);
                vertices[baseIndex + 1] = new Vector3(-headHalfWidth, 0f, outer - thickness * 0.5f);
                vertices[baseIndex + 2] = new Vector3(headHalfWidth, 0f, outer - thickness * 0.5f);

                triangles[t] = baseIndex;
                triangles[t + 1] = baseIndex + 1;
                triangles[t + 2] = baseIndex + 2;
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.triangles = triangles;

            var normals = new Vector3[vertices.Length];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.up;
            }

            mesh.normals = normals;
            mesh.RecalculateBounds();

            ApplyMaterial();

            builtRadius = outer;
            builtThickness = thickness;
            builtSweep = sweepDegrees;
            builtRadialOffset = radialOffset;
            builtArrow = arrowHead;
            builtSegments = segments;
        }

        private void ApplyMaterial()
        {
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            // 优先使用材质资产：它的着色器 ZTest Always，因此圆环不会被角色的
            // billboard 遮挡（站在角色脚下也能看见完整一圈）。
            // 颜色写在派生实例上，避免修改资产，也避免 edit 模式下调用 renderer.material。
            if (material != null)
            {
                if (runtimeMaterial == null)
                {
                    runtimeMaterial = new Material(material) { name = material.name + " (ring)", hideFlags = HideFlags.DontSave };
                }

                if (runtimeMaterial.HasProperty("_Color"))
                {
                    runtimeMaterial.SetColor("_Color", color);
                }

                if (meshRenderer.sharedMaterial != runtimeMaterial)
                {
                    meshRenderer.sharedMaterial = runtimeMaterial;
                }

                return;
            }

            if (runtimeMaterial != null)
            {
                meshRenderer.sharedMaterial = runtimeMaterial;
                return;
            }

            Shader shader = Shader.Find("Decoherence/GroundRing");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            runtimeMaterial = new Material(shader) { name = "GroundRing (runtime)", hideFlags = HideFlags.DontSave };

            if (runtimeMaterial.HasProperty("_BaseColor"))
            {
                runtimeMaterial.SetColor("_BaseColor", color);
            }

            if (runtimeMaterial.HasProperty("_Color"))
            {
                runtimeMaterial.SetColor("_Color", color);
            }

            if (runtimeMaterial.HasProperty("_Cull"))
            {
                runtimeMaterial.SetFloat("_Cull", 0f);
            }

            meshRenderer.sharedMaterial = runtimeMaterial;
        }
    }
}
