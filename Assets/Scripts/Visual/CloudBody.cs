using UnityEngine;
using UnityEngine.Rendering;

namespace Decoherence
{
    /// <summary>
    /// 退相干态的身体：不可见的核心 + 8 根 2D 柱（加速度驱动滞后、弹簧阻尼回正），
    /// 柱尖经闭合三次 B 样条生成触发边界，其外再偏移一层可见的暗影。
    /// 判定完全基于该多边形（2D，XZ 平面）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudBody : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerCoherence coherence;
        [Tooltip("退相干时隐藏的方块身体")]
        [SerializeField] private Transform blockyBody;

        [Header("Pillars (2D)")]
        [SerializeField, Range(3, 24)] private int pillarCount = 8;
        [SerializeField, Min(0.05f)] private float pillarLength = 0.40f;
        [SerializeField, Min(0.05f)] private float lowCoherencePillarLength = 0.60f;
        [Tooltip("角刚度：作用于 1 rad 偏角的角加速度，单位 rad/s²")]
        [SerializeField, Min(1f)] private float angularStiffness = 55f;
        [Tooltip("角阻尼，单位 1/s")]
        [SerializeField, Min(0f)] private float angularDamping = 7f;
        [Tooltip("核心加速度到角加速度的增益")]
        [SerializeField, Min(0f)] private float accelerationGain = 2.6f;
        [SerializeField, Range(10f, 89f)] private float maxLagDegrees = 78f;
        [SerializeField, Min(0.05f)] private float rayHeight = 0.5f;
        [SerializeField, Min(0f)] private float wallClearance = 0.03f;
        [SerializeField] private LayerMask barrierMask = ~0;

        [Header("Boundary")]
        [SerializeField, Range(16, 128)] private int boundarySamples = 64;

        [Header("Shadow")]
        [SerializeField] private Material shadowMaterial;
        [SerializeField] private Color shadowColor = new Color(0.010f, 0.012f, 0.018f, 1f);
        [SerializeField, Range(0f, 1f)] private float shadowAlpha = 1f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.15f;

        [Header("Puffs")]
        [Tooltip("同时存在的烟团数量；整团云只用一个 mesh")]
        [SerializeField, Range(8, 200)] private int puffCount = 56;
        [Tooltip("单个烟团半径（米）")]
        [SerializeField, Min(0.02f)] private float puffRadius = 0.22f;
        [SerializeField, Min(0.1f)] private float puffLifetime = 1.8f;
        [Tooltip("生命周期内向上飘升的高度（米）")]
        [SerializeField, Min(0f)] private float puffRise = 0.65f;
        [Tooltip("水平漂移幅度（米）")]
        [SerializeField, Min(0f)] private float puffWander = 0.07f;
        [Tooltip("生长期尺寸增益")]
        [SerializeField, Range(0f, 1f)] private float puffGrow = 0.45f;
        [SerializeField, Range(0f, 1f)] private float puffAlpha = 0.55f;
        [Tooltip("贴近判定边界时的淡出带宽度（米）")]
        [SerializeField, Min(0.01f)] private float edgeFadeWidth = 0.14f;
        [Tooltip("0 表示内外一致，1 表示中心比边缘浓得多")]
        [SerializeField, Range(0f, 1f)] private float centerBias = 0.55f;

        [Header("Camera")]
        [Tooltip("billboard 朝向用的相机；留空时取 Camera.main")]
        [SerializeField] private Camera viewCamera;

        [Header("Volume (3D)")]
        [Tooltip("云底面离地高度（米）；整团云都在这个高度之上")]
        [SerializeField, Min(0f)] private float baseHeight = 0.20f;
        [Tooltip("云的竖向高度（米）；判定仍是 2D，这个高度只影响观感")]
        [SerializeField, Min(0.1f)] private float cloudHeight = 1.15f;
        [Tooltip("起伏的速度")]
        [SerializeField, Min(0f)] private float noiseSpeed = 0.5f;
        [Tooltip("起伏的空间频率")]
        [SerializeField, Min(0.1f)] private float noiseScale = 2.6f;

        private Transform shadowTransform;
        private Mesh shadowMesh;
        private MeshFilter shadowFilter;
        private MeshRenderer shadowRenderer;
        private Material shadowInstance;
        private Vector3[] shadowVertices;
        private Color[] shadowColors;
        private Vector2[] shadowUvs;
        private int[] shadowTriangles;
        private float[] puffDepth;
        private int[] puffOrder;
        private Vector3[] pendingPosition;
        private float[] pendingSize;
        private float[] pendingValue;

        private float[] lag;
        private float[] lagVelocity;
        private Vector2[] tips;
        private Vector2[] boundary;
        private Vector2 centroid;
        private Vector2 previousVelocity;
        private float visibility;
        private Renderer[] bodyRenderers;
        private bool bodyVisible = true;

        public float Area { get; private set; }
        public float Visibility => visibility;
        public bool IsVisible => visibility > 0.01f;

        private void Awake()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<PlayerMotor>();
            }

            if (coherence == null)
            {
                coherence = GetComponentInParent<PlayerCoherence>();
            }

            if (blockyBody == null && motor != null)
            {
                blockyBody = motor.transform.Find("Body");
            }

            // 只关渲染器、不关 GameObject：Body 上的 BlockyBodyAnimator 必须继续转身，
            // 否则脚印环/朝向弧的来源朝向会停在退相干前的值。
            if (blockyBody != null)
            {
                bodyRenderers = blockyBody.GetComponentsInChildren<Renderer>(true);
            }

            pillarCount = Mathf.Max(3, pillarCount);

            // 序列化下来的默认 LayerMask 为 0（Nothing），那样射线什么都打不到，按“全部层”处理
            if (barrierMask.value == 0)
            {
                barrierMask = ~0;
            }

            lag = new float[pillarCount];
            lagVelocity = new float[pillarCount];
            tips = new Vector2[pillarCount];
            boundary = new Vector2[Mathf.Max(16, boundarySamples)];

            puffOrder = new int[Mathf.Max(1, puffCount)];
            puffDepth = new float[puffOrder.Length];
            pendingPosition = new Vector3[puffOrder.Length];
            pendingSize = new float[puffOrder.Length];
            pendingValue = new float[puffOrder.Length];

            if (viewCamera == null)
            {
                viewCamera = Camera.main;
            }

            CreateShadowObject();
        }

        private void CreateShadowObject()
        {
            var go = new GameObject("ShadowMesh");
            go.transform.SetParent(transform, false);
            shadowTransform = go.transform;

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

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || lag == null)
            {
                return;
            }

            bool decoherent = motor != null && motor.State == CoherenceState.Decoherent;
            visibility = Mathf.MoveTowards(visibility, decoherent ? 1f : 0f, dt / transitionDuration);

            SetBodyVisible(visibility < 0.5f);

            bool visible = visibility > 0.001f;
            if (shadowRenderer != null)
            {
                shadowRenderer.enabled = visible;
            }

            if (!visible)
            {
                previousVelocity = GetPlanarVelocity();
                return;
            }

            float normalized = coherence != null ? Mathf.Clamp01(coherence.Normalized) : 1f;
            float length = Mathf.Lerp(lowCoherencePillarLength, pillarLength, normalized);

            SimulatePillars(dt, length);
            BuildBoundary();
            BuildCloudMesh();
            ApplyShadowMaterial(normalized);

            previousVelocity = GetPlanarVelocity();
        }

        /// <summary>隐藏/显示方块身体：只切渲染器，保持 Body 上的动画与转身照常运行。</summary>
        private void SetBodyVisible(bool visible)
        {
            if (bodyVisible == visible)
            {
                return;
            }

            bodyVisible = visible;

            if (bodyRenderers == null)
            {
                if (blockyBody == null)
                {
                    return;
                }

                bodyRenderers = blockyBody.GetComponentsInChildren<Renderer>(true);
            }

            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                if (bodyRenderers[i] != null)
                {
                    bodyRenderers[i].enabled = visible;
                }
            }
        }

        private Vector2 GetPlanarVelocity()
        {
            if (motor == null)
            {
                return Vector2.zero;
            }

            Vector3 velocity = motor.PlanarVelocity;
            return new Vector2(velocity.x, velocity.z);
        }

        private void SimulatePillars(float dt, float length)
        {
            Vector2 velocity = GetPlanarVelocity();
            Vector2 acceleration = dt > 0f ? (velocity - previousVelocity) / dt : Vector2.zero;
            acceleration = Vector2.ClampMagnitude(acceleration, 80f);

            Vector2 origin = GetCorePosition();
            float maxLag = maxLagDegrees * Mathf.Deg2Rad;

            for (int i = 0; i < pillarCount; i++)
            {
                float baseAngle = (Mathf.PI * 2f * i) / pillarCount;
                float absolute = baseAngle + lag[i];
                Vector2 direction = new Vector2(Mathf.Cos(absolute), Mathf.Sin(absolute));

                // 伪力对枢轴的扭矩：τ ∝ cross(direction, acceleration)
                float cross = direction.x * acceleration.y - direction.y * acceleration.x;
                float angularAcceleration = accelerationGain * cross / Mathf.Max(length, 0.05f);
                angularAcceleration -= angularStiffness * lag[i];
                angularAcceleration -= angularDamping * lagVelocity[i];

                lagVelocity[i] += angularAcceleration * dt;
                lag[i] += lagVelocity[i] * dt;

                if (lag[i] > maxLag)
                {
                    lag[i] = maxLag;
                    lagVelocity[i] = 0f;
                }
                else if (lag[i] < -maxLag)
                {
                    lag[i] = -maxLag;
                    lagVelocity[i] = 0f;
                }

                float reach = ResolveReach(origin, direction, length);
                tips[i] = origin + direction * reach;
            }
        }

        /// <summary>柱子为刚体：柱尖被墙体截停，返回该方向上可达到的长度。</summary>
        private float ResolveReach(Vector2 origin, Vector2 direction, float length)
        {
            Vector3 start = new Vector3(origin.x, rayHeight, origin.y);
            Vector3 dir = new Vector3(direction.x, 0f, direction.y);

            if (Physics.Raycast(start, dir, out RaycastHit hit, length + wallClearance, barrierMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0f, hit.distance - wallClearance);
            }

            return length;
        }

        private Vector2 GetCorePosition()
        {
            Vector3 position = transform.position;
            return new Vector2(position.x, position.z);
        }

        /// <summary>闭合三次 B 样条穿过 8 个控制点，采样为边界多边形。</summary>
        private void BuildBoundary()
        {
            int count = Mathf.Max(16, boundarySamples);
            if (boundary == null || boundary.Length != count)
            {
                boundary = new Vector2[count];
            }

            for (int s = 0; s < count; s++)
            {
                float t = (s / (float)count) * pillarCount;
                int index = Mathf.FloorToInt(t) % pillarCount;
                float u = t - Mathf.FloorToInt(t);

                Vector2 p0 = tips[(index - 1 + pillarCount) % pillarCount];
                Vector2 p1 = tips[index];
                Vector2 p2 = tips[(index + 1) % pillarCount];
                Vector2 p3 = tips[(index + 2) % pillarCount];

                float u2 = u * u;
                float u3 = u2 * u;

                Vector2 point =
                    ((1f - 3f * u + 3f * u2 - u3) * p0 +
                     (4f - 6f * u2 + 3f * u3) * p1 +
                     (1f + 3f * u + 3f * u2 - 3f * u3) * p2 +
                     (u3) * p3) / 6f;

                boundary[s] = point;
            }

            Area = ComputeArea(boundary);
        }

        private static float ComputeArea(Vector2[] polygon)
        {
            float sum = 0f;
            for (int i = 0; i < polygon.Length; i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[(i + 1) % polygon.Length];
                sum += a.x * b.y - b.x * a.y;
            }

            return Mathf.Abs(sum) * 0.5f;
        }

        /// <summary>
        /// 判定区即生成域：烟团只在柱尖多边形内生成，靠近边界的一圈按到边距离淡出。
        /// 这样看到的“云”和判定区是同一份数据，粒子不参与命中计算。
        /// </summary>
        private void BuildCloudMesh()
        {
            if (shadowMesh == null || boundary == null || puffOrder == null)
            {
                return;
            }

            int count = boundary.Length;
            int active = Mathf.Clamp(puffCount, 0, puffOrder.Length);

            centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                centroid += boundary[i];
            }

            centroid /= count;

            float boundaryRadius = 0.1f;
            for (int i = 0; i < count; i++)
            {
                boundaryRadius = Mathf.Max(boundaryRadius, Vector2.Distance(boundary[i], centroid));
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

            // 相对核心的局部坐标：ShadowMesh 是 Cloud 的子物体，跟着玩家一起走
            Vector2 origin = GetCorePosition();

            Vector3 right = Vector3.right;
            Vector3 up = Vector3.forward;
            Vector3 viewPosition = origin;
            Vector3 viewForward = Vector3.down;

            if (viewCamera != null)
            {
                Transform cam = viewCamera.transform;
                right = cam.right;
                up = cam.up;
                viewPosition = cam.transform.position;
                viewForward = cam.forward;
            }

            float lifetime = Mathf.Max(0.1f, puffLifetime);
            float cycleTime = Time.time / lifetime;

            for (int i = 0; i < active; i++)
            {
                // 每个烟团一组固定相位/种子，帧率不影响分布
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

                Vector2 point = centroid + (edgeB - centroid) * a1 + (edgeC - centroid) * a2;
                float edgeDistance = DistanceToSegment(point, edgeB, edgeC);

                // 让烟团压住多边形：只收 0.3 个半径，边缘 puffs 可以越过边界当柔边
                float inset = Mathf.Min(edgeDistance, puffRadius * 0.3f);
                Vector2 inward = centroid - point;

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

                float radial = Mathf.Clamp01(Vector2.Distance(point, centroid) / boundaryRadius);
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
                Vector3 corner = center3 - new Vector3(origin.x, 0f, origin.y);

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

        /// <summary>把 value 映射到 edge0..edge1 后再平滑到 0..1（Mathf.SmoothStep 本身是按值插值，不是归一化）。</summary>
        private static float Smooth01(float edge0, float edge1, float value)
        {
            if (Mathf.Approximately(edge0, edge1))
            {
                return value < edge0 ? 0f : 1f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(edge0, edge1, value));
        }

        /// <summary>确定性哈希：同一下标/场次永远得到同一个 0..1 值。</summary>
        private static float Hash(int index, int salt)
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

        private void ApplyShadowMaterial(float normalized)
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

        /// <summary>敌人是否进入触发区（与边界多边形同一份数据）。</summary>
        public bool Overlaps(Collider other)
        {
            if (other == null || boundary == null)
            {
                return false;
            }

            Vector3 center = other.bounds.center;
            Vector3 extents = other.bounds.extents;
            float radius = Mathf.Max(extents.x, extents.z);

            Vector2 point = new Vector2(center.x, center.z);

            if (IsInsidePolygon(point))
            {
                return true;
            }

            return DistanceToPolygon(point) <= radius;
        }

        private bool IsInsidePolygon(Vector2 point)
        {
            bool inside = false;
            int count = boundary.Length;

            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Vector2 a = boundary[i];
                Vector2 b = boundary[j];

                if ((a.y > point.y) != (b.y > point.y))
                {
                    float x = (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x;
                    if (point.x < x)
                    {
                        inside = !inside;
                    }
                }
            }

            return inside;
        }

        private float DistanceToPolygon(Vector2 point)
        {
            float best = float.MaxValue;
            int count = boundary.Length;

            for (int i = 0; i < count; i++)
            {
                Vector2 a = boundary[i];
                Vector2 b = boundary[(i + 1) % count];
                best = Mathf.Min(best, DistanceToSegment(point, a, b));
            }

            return best;
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
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
    }
}
