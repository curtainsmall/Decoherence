using UnityEngine;
using UnityEngine.Rendering;

namespace Decoherence
{
    /// <summary>
    /// 退相干态的外观：读取 PlayerCloudField 给出的边界与核心位置，在边界内生成烟团网格，
    /// 自己处理可见性淡入淡出与身体渲染器的开合。不读取任何玩法状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCloudVisual : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        PlayerCloudField field;

        [Tooltip("退相干时隐藏的身体外观")]
        [SerializeField, HideInInspector]
        Transform body;

        [Header("Shadow")]
        [SerializeField]
        Material shadowMaterial;

        [SerializeField]
        Color shadowColor = new Color(0.010f, 0.012f, 0.018f, 1f);

        [SerializeField, Range(0f, 1f)]
        float shadowAlpha = 1f;

        [SerializeField, Min(0.01f)]
        float transitionDuration = 0.15f;

        [Header("Puffs")]
        [Tooltip("同时存在的烟团数量；整团云只用一个 mesh")]
        [SerializeField, Range(8, 200)]
        int puffCount = 56;

        [Tooltip("单个烟团半径（米）")]
        [SerializeField, Min(0.02f)]
        float puffRadius = 0.22f;

        [SerializeField, Min(0.1f)]
        float puffLifetime = 1.8f;

        [Tooltip("生命周期内向上飘升的高度（米）")]
        [SerializeField, Min(0f)]
        float puffRise = 0.65f;

        [Tooltip("水平漂移幅度（米）")]
        [SerializeField, Min(0f)]
        float puffWander = 0.07f;

        [Tooltip("生长期尺寸增益")]
        [SerializeField, Range(0f, 1f)]
        float puffGrow = 0.45f;

        [SerializeField, Range(0f, 1f)]
        float puffAlpha = 0.55f;

        [Tooltip("贴近判定边界时的淡出带宽度（米）")]
        [SerializeField, Min(0.01f)]
        float edgeFadeWidth = 0.14f;

        [Tooltip("0 表示内外一致，1 表示中心比边缘浓得多")]
        [SerializeField, Range(0f, 1f)]
        float centerBias = 0.55f;

        [Header("Camera")]
        [Tooltip("billboard 朝向用的相机；留空时取 Camera.main")]
        [SerializeField]
        Camera viewCamera;

        [Header("Volume (3D)")]
        [Tooltip("云底面离地高度（米）；整团云都在这个高度之上")]
        [SerializeField, Min(0f)]
        float baseHeight = 0.20f;

        [Tooltip("云的竖向高度（米）；判定仍是 2D，这个高度只影响观感")]
        [SerializeField, Min(0.1f)]
        float cloudHeight = 1.15f;

        [Tooltip("起伏的速度")]
        [SerializeField, Min(0f)]
        float noiseSpeed = 0.5f;

        [Tooltip("起伏的空间频率")]
        [SerializeField, Min(0.1f)]
        float noiseScale = 2.6f;

        Mesh shadowMesh;

        MeshFilter shadowFilter;

        MeshRenderer shadowRenderer;

        Material shadowInstance;

        Vector3[] shadowVertices;

        Color[] shadowColors;

        Vector2[] shadowUvs;

        int[] shadowTriangles;

        float[] puffDepth;

        int[] puffOrder;

        Vector3[] pendingPosition;

        float[] pendingSize;

        float[] pendingValue;

        float visibility;

        Renderer[] bodyRenderers;

        bool bodyVisible = true;

        public float Visibility => visibility;

        public bool IsVisible => visibility > 0.01f;

        void Awake()
        {
            ResolveReferences();

            puffOrder = new int[Mathf.Max(1, puffCount)];
            puffDepth = new float[puffOrder.Length];
            pendingPosition = new Vector3[puffOrder.Length];
            pendingSize = new float[puffOrder.Length];
            pendingValue = new float[puffOrder.Length];

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            bodyRenderers = body?.GetComponentsInChildren<Renderer>(true);

            CreateShadowObject();
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void CreateShadowObject()
        {
            var go = new GameObject("ShadowMesh");
            go.transform.SetParent(transform, false);

            shadowFilter = go.AddComponent<MeshFilter>();
            shadowRenderer = go.AddComponent<MeshRenderer>();
            shadowRenderer.shadowCastingMode = ShadowCastingMode.Off;
            shadowRenderer.receiveShadows = false;

            if (shadowMaterial != null)
            {
                shadowInstance = new Material(shadowMaterial) { hideFlags = HideFlags.DontSave };
                shadowRenderer.sharedMaterial = shadowInstance;
            }

            shadowMesh = new Mesh { name = "ShadowCloud (runtime)", hideFlags = HideFlags.DontSave };
            shadowFilter.sharedMesh = shadowMesh;
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f)
            {
                return;
            }

            bool active = field != null && field.Active;

            visibility = Mathf.MoveTowards(visibility, active ? 1f : 0f, deltaTime / Mathf.Max(0.01f, transitionDuration));

            SetBodyVisible(visibility < 0.5f);

            bool visible = visibility > 0.001f;

            if (shadowRenderer != null)
            {
                shadowRenderer.enabled = visible;
            }

            if (!visible || field == null || field.Boundary == null)
            {
                return;
            }

            BuildCloudMesh();
            ApplyShadowMaterial(field.CoherenceNormalized);
        }

        /// <summary>隐藏/显示身体外观：只切渲染器，保持节点上的动画与转身照常运行。</summary>
        void SetBodyVisible(bool visible)
        {
            if (bodyVisible == visible)
            {
                return;
            }

            bodyVisible = visible;

            if (bodyRenderers == null)
            {
                if (body == null)
                {
                    return;
                }

                bodyRenderers = body.GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    bodyRenderers[i].enabled = visible;
                }
            }
        }

        void BuildCloudMesh()
        {
            Vector2[] boundary = field.Boundary;

            if (shadowMesh == null || boundary == null || puffOrder == null)
            {
                return;
            }

            int count = boundary.Length;
            int active = Mathf.Clamp(puffCount, 0, puffOrder.Length);

            Vector2 core = field.Core;
            Vector2 centroid = Vector2.zero;

            for (int i = 0; i < count; i++)
            {
                centroid += boundary[i];
            }

            centroid /= count;

            float boundaryRadius = 0.1f;

            for (int i = 0; i < count; i++)
            {
                boundaryRadius = Mathf.Max(boundaryRadius, Vector2.Distance(boundary[i], core));
            }

            int vertexCount = active * 4;
            int triangleCount = active * 2;

            if (shadowVertices == null || shadowVertices.Length != vertexCount)
            {
                shadowVertices = new Vector3[vertexCount];
                shadowColors = new Color[vertexCount];
                shadowUvs = new Vector2[vertexCount];
                shadowTriangles = new int[triangleCount * 3];

                for (int i = 0; i < active; i++)
                {
                    int v = i * 4;
                    int t = i * 6;

                    shadowUvs[v] = new Vector2(0f, 0f);
                    shadowUvs[v + 1] = new Vector2(1f, 0f);
                    shadowUvs[v + 2] = new Vector2(1f, 1f);
                    shadowUvs[v + 3] = new Vector2(0f, 1f);

                    shadowTriangles[t] = v;
                    shadowTriangles[t + 1] = v + 1;
                    shadowTriangles[t + 2] = v + 2;

                    shadowTriangles[t + 3] = v;
                    shadowTriangles[t + 4] = v + 2;
                    shadowTriangles[t + 5] = v + 3;
                }
            }

            Vector3 right = Vector3.right;
            Vector3 up = Vector3.forward;
            Vector3 viewPosition = core;
            Vector3 viewForward = Vector3.down;

            if (viewCamera != null)
            {
                Transform cam = viewCamera.transform;
                right = cam.right;
                up = cam.up;
                viewPosition = cam.position;
                viewForward = cam.forward;
            }

            float lifetime = Mathf.Max(0.1f, puffLifetime);
            float cycleTime = Time.time / lifetime;

            for (int i = 0; i < active; i++)
            {
                float seedPhase = Hash(i, 1);
                float cycle = Mathf.Floor(cycleTime + seedPhase);
                float phase = cycleTime + seedPhase - cycle;

                int salt = (int)cycle * 131 + i * 17;
                int sector = Mathf.Clamp((int)(Hash(salt, 2) * count), 0, count - 1);

                float a1 = Hash(salt, 3);
                float a2 = Hash(salt, 4);

                if (a1 + a2 > 1f)
                {
                    a1 = 1f - a1;
                    a2 = 1f - a2;
                }

                Vector2 edgeB = boundary[sector];
                Vector2 edgeC = boundary[(sector + 1) % count];

                // 以核心为扇形基点切分多边形：形状被柱子甩歪时，烟仍从角色身上铺开
                Vector2 point = core + (edgeB - core) * a1 + (edgeC - core) * a2;
                float edgeDistance = DistanceToSegment(point, edgeB, edgeC);

                // 让烟团压住多边形：只收 0.3 个半径，边缘 puffs 可以越过边界当柔边
                float inset = Mathf.Min(edgeDistance, puffRadius * 0.3f);
                Vector2 inward = core - point;

                if (inward.sqrMagnitude > 1e-6f)
                {
                    point += inward.normalized * inset;
                }

                edgeDistance += inset;

                float wanderAngle = Hash(i, 6) * Mathf.PI * 2f;
                float wander = puffWander * (phase - 0.5f) * 2f;
                point.x += Mathf.Cos(wanderAngle) * wander;
                point.y += Mathf.Sin(wanderAngle) * wander;

                float lifeFade = Smooth01(0f, 0.22f, phase) * (1f - Smooth01(0.6f, 1f, phase));
                float edgeFade = Mathf.Clamp01(edgeDistance / Mathf.Max(edgeFadeWidth, 0.01f));

                float radial = Mathf.Clamp01(Vector2.Distance(point, core) / boundaryRadius);
                float center = Mathf.Lerp(1f, 1f - centerBias, radial);

                // 每个 puff 有自己的大小/浓度，避免一团均匀的雾
                float sizeScale = 0.7f + Hash(i, 8) * 0.65f;
                float valueScale = 0.8f + Hash(i, 9) * 0.4f;

                float alpha = puffAlpha * lifeFade * Mathf.Lerp(0.4f, 1f, edgeFade) * center * valueScale;

                float rise = Mathf.Min(puffRise * phase, cloudHeight);

                pendingPosition[i] = new Vector3(
                    point.x,
                    baseHeight + rise + (Hash(i, 7) - 0.5f) * 0.15f * cloudHeight,
                    point.y);

                pendingSize[i] = puffRadius * sizeScale * (0.85f + puffGrow * phase);
                pendingValue[i] = Mathf.Clamp01(alpha);
                puffDepth[i] = Vector3.Dot(pendingPosition[i] - viewPosition, viewForward);
                puffOrder[i] = i;
            }

            // 远的先画：单个 mesh 内不会自动排序，按相机深度从后往前填槽位
            for (int i = 1; i < active; i++)
            {
                int value = puffOrder[i];
                float key = puffDepth[value];
                int j = i - 1;

                while (j >= 0 && puffDepth[puffOrder[j]] < key)
                {
                    puffOrder[j + 1] = puffOrder[j];
                    j--;
                }

                puffOrder[j + 1] = value;
            }

            for (int slot = 0; slot < active; slot++)
            {
                int i = puffOrder[slot];

                Vector3 center3 = pendingPosition[i];
                float size = pendingSize[i];
                Vector3 corner = center3 - new Vector3(core.x, 0f, core.y);

                int v = slot * 4;
                shadowVertices[v] = corner - right * size - up * size;
                shadowVertices[v + 1] = corner + right * size - up * size;
                shadowVertices[v + 2] = corner + right * size + up * size;
                shadowVertices[v + 3] = corner - right * size + up * size;

                Color color = new Color(1f, 1f, 1f, pendingValue[i]);
                shadowColors[v] = color;
                shadowColors[v + 1] = color;
                shadowColors[v + 2] = color;
                shadowColors[v + 3] = color;
            }

            shadowMesh.Clear();
            shadowMesh.vertices = shadowVertices;
            shadowMesh.colors = shadowColors;
            shadowMesh.uv = shadowUvs;
            shadowMesh.triangles = shadowTriangles;
            shadowMesh.RecalculateBounds();
        }

        void ApplyShadowMaterial(float normalized)
        {
            if (shadowInstance == null)
            {
                return;
            }

            shadowInstance.SetColor("_Color", shadowColor);
            shadowInstance.SetFloat("_Alpha", shadowAlpha);
            shadowInstance.SetFloat("_NoiseScale", noiseScale * 3f);
            // 相干度越低，边缘越散
            shadowInstance.SetFloat("_NoiseStrength", Mathf.Lerp(0.62f, 0.30f, normalized));
        }

        static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;

            if (lengthSquared < 1e-6f)
            {
                return Vector2.Distance(point, a);
            }

            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSquared);

            return Vector2.Distance(point, a + ab * t);
        }

        /// <summary>确定性哈希：同一下标/场次永远得到同一个 0..1 值。</summary>
        static float Hash(int index, int salt)
        {
            unchecked
            {
                uint h = (uint)(index * 73856093) ^ (uint)(salt * 19349663);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        /// <summary>把 value 映射到 edge0..edge1 后再平滑到 0..1。</summary>
        static float Smooth01(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return value < edge0 ? 0f : 1f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, value));
        }

        void ResolveReferences()
        {
            if (field == null)
            {
                field = GetComponentInParent<PlayerCloudField>();
            }

            if (body == null)
            {
                body = GetComponentInParent<CharacterMotor>()?.transform.Find("CharacterBody");
            }
        }
    }
}
