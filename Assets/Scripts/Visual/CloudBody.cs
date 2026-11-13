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
        [SerializeField, Min(0f)] private float baseMargin = 0.22f;
        [Tooltip("-velocity 方向的最大额外外扩")]
        [SerializeField, Min(0f)] private float trailMargin = 0.26f;
        [Tooltip("每 1 m/s 的额外外扩")]
        [SerializeField, Min(0f)] private float speedMargin = 0.03f;
        [SerializeField] private Color shadowColor = new Color(0.010f, 0.012f, 0.018f, 1f);
        [SerializeField, Range(0f, 1f)] private float shadowAlpha = 1f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.15f;

        [Header("Volume (3D)")]
        [Tooltip("云底面离地高度（米）；整团云都在这个高度之上")]
        [SerializeField, Min(0f)] private float baseHeight = 0.20f;
        [Tooltip("云的竖向高度（米）；判定仍是 2D，这个高度只影响观感")]
        [SerializeField, Min(0.1f)] private float cloudHeight = 1.15f;
        [SerializeField, Range(2, 12)] private int ringCount = 7;
        [Tooltip("起伏振幅（米）")]
        [SerializeField, Min(0f)] private float noiseAmplitude = 0.10f;
        [SerializeField, Min(0f)] private float noiseSpeed = 0.5f;
        [Tooltip("起伏的空间频率")]
        [SerializeField, Min(0.1f)] private float noiseScale = 2.6f;

        [Header("Layers")]
        [Tooltip("内核半径占外轮廓的比例")]
        [SerializeField, Range(0.2f, 1f)] private float coreRadiusScale = 0.72f;
        [SerializeField, Range(0f, 1f)] private float coreAlpha = 1f;
        [SerializeField, Range(0f, 1f)] private float outerAlpha = 0.55f;

        private const int ShellCount = 2;

        private Transform shadowTransform;
        private Mesh shadowMesh;
        private MeshFilter shadowFilter;
        private MeshRenderer shadowRenderer;
        private Material shadowInstance;
        private Vector3[] shadowVertices;
        private Color[] shadowColors;
        private int[] shadowTriangles;
        private Vector2[] expanded;

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

        /// <summary>两层体积：内核算实心，外层稀疏、边缘破碎成烟。</summary>
        private void BuildCloudMesh()
        {
            if (shadowMesh == null || boundary == null)
            {
                return;
            }

            int count = boundary.Length;
            int rings = Mathf.Clamp(ringCount, 2, 12);

            centroid = Vector2.zero;
            for (int i = 0; i < count; i++)
            {
                centroid += boundary[i];
            }

            centroid /= count;

            Vector2 velocity = GetPlanarVelocity();
            float speed = velocity.magnitude;
            Vector2 trailing = speed > 0.01f ? -velocity / speed : Vector2.zero;
            float margin = baseMargin + speedMargin * speed;

            // 顶点写成“相对核心”的局部坐标：ShadowMesh 是 Cloud 的子物体，跟着玩家一起走
            Vector2 origin = GetCorePosition();

            if (expanded == null || expanded.Length != count)
            {
                expanded = new Vector2[count];
            }

            float boundaryRadius = 0.1f;

            for (int i = 0; i < count; i++)
            {
                Vector2 outward = boundary[i] - centroid;
                float distance = outward.magnitude;
                Vector2 direction = distance > 1e-4f ? outward / distance : Vector2.up;
                float alignment = trailing.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(direction, trailing)) : 0f;
                float extra = trailMargin * Mathf.Pow(alignment, 1.5f);

                expanded[i] = boundary[i] + direction * (margin + extra);
                boundaryRadius = Mathf.Max(boundaryRadius, Vector2.Distance(expanded[i], centroid));
            }

            int perShellVertices = rings * count + 2; // 顶盖 + 底盖
            int perShellTriangles = (rings - 1) * count * 2 + count * 2;
            int vertexCount = perShellVertices * ShellCount;
            int triangleCount = perShellTriangles * ShellCount;

            if (shadowVertices == null || shadowVertices.Length != vertexCount)
            {
                shadowVertices = new Vector3[vertexCount];
                shadowColors = new Color[vertexCount];
                shadowTriangles = new int[triangleCount * 3];
                BuildCloudTriangles(count, rings, perShellVertices);
            }

            float time = Time.time;
            float bottom = baseHeight;
            float top = baseHeight + cloudHeight;

            // 内核：小、密、暗
            AppendShell(count, rings, 0, boundaryRadius, origin, coreRadiusScale,
                Mathf.Lerp(bottom, top, 0.10f), Mathf.Lerp(bottom, top, 0.86f),
                coreAlpha, 0.75f, time);

            // 外层：大、稀、轮廓被噪声打碎
            AppendShell(count, rings, perShellVertices, boundaryRadius, origin, 1f,
                bottom, top, outerAlpha, 1.45f, time);

            shadowMesh.Clear();
            shadowMesh.vertices = shadowVertices;
            shadowMesh.colors = shadowColors;
            shadowMesh.triangles = shadowTriangles;
            shadowMesh.RecalculateBounds();
        }

        private void AppendShell(
            int count,
            int rings,
            int vertexOffset,
            float boundaryRadius,
            Vector2 origin,
            float radiusScale,
            float heightStart,
            float heightEnd,
            float alphaScale,
            float noiseGain,
            float time)
        {
            for (int ring = 0; ring < rings; ring++)
            {
                float t = ring / (float)(rings - 1);
                float height = Mathf.Lerp(heightStart, heightEnd, t);

                // 上半收得比椭球慢，整团更像堆积的烟而不是水滴
                float profile = Mathf.Pow(Mathf.Cos(t * Mathf.PI * 0.5f), 0.55f);

                float lower = Mathf.Lerp(0.75f, 1f, Mathf.SmoothStep(0f, 0.22f, t));
                float upper = Mathf.Lerp(1f, 0.45f, Mathf.SmoothStep(0.72f, 1f, t));
                float alpha = alphaScale * lower * upper;

                for (int i = 0; i < count; i++)
                {
                    // 用圆周采样保证接缝处连续；两层噪声给出大小两级团块
                    float angle = (Mathf.PI * 2f * i) / count;
                    float n1 = Mathf.PerlinNoise(
                        Mathf.Cos(angle) * noiseScale + 11.7f,
                        Mathf.Sin(angle) * noiseScale + t * 1.7f + time * noiseSpeed);
                    float n2 = Mathf.PerlinNoise(
                        Mathf.Cos(angle) * noiseScale * 2.3f + 41.2f,
                        Mathf.Sin(angle) * noiseScale * 2.3f - t * 2.1f - time * noiseSpeed * 1.7f);

                    float lump = (n1 - 0.5f) * 1.35f + (n2 - 0.5f) * 0.65f;
                    float wobble = lump * 2f * noiseAmplitude * noiseGain;

                    Vector2 point = centroid + (expanded[i] - centroid) * (radiusScale * profile);
                    Vector2 outward = point - centroid;
                    float length = outward.magnitude;

                    if (length > 1e-4f)
                    {
                        point += outward / length * wobble;
                    }

                    float radial = Mathf.Clamp01(Vector2.Distance(point, centroid) / boundaryRadius);

                    int index = vertexOffset + ring * count + i;
                    shadowVertices[index] = new Vector3(
                        point.x - origin.x,
                        height + wobble * 0.7f,
                        point.y - origin.y);

                    // color.r = 高度比例，color.g = 径向比例，color.a = 该点的透明度权重
                    shadowColors[index] = new Color(t, radial, 1f, Mathf.Clamp01(alpha));
                }
            }

            int ringVertices = vertexOffset + rings * count;

            // 顶部收口
            shadowVertices[ringVertices] = new Vector3(centroid.x - origin.x, heightEnd, centroid.y - origin.y);
            shadowColors[ringVertices] = new Color(1f, 0f, 1f, alphaScale * 0.12f);

            // 底部中心：整团云悬空，底面要封住，否则从斜上方会看穿
            shadowVertices[ringVertices + 1] = new Vector3(centroid.x - origin.x, heightStart, centroid.y - origin.y);
            shadowColors[ringVertices + 1] = new Color(0f, 0f, 1f, alphaScale * 0.55f);
        }

        private void BuildCloudTriangles(int count, int rings, int perShellVertices)
        {
            int index = 0;

            for (int shell = 0; shell < ShellCount; shell++)
            {
                int start = shell * perShellVertices;

                for (int ring = 0; ring < rings - 1; ring++)
                {
                    int current = start + ring * count;
                    int next = start + (ring + 1) * count;

                    for (int i = 0; i < count; i++)
                    {
                        int i1 = (i + 1) % count;

                        shadowTriangles[index++] = current + i;
                        shadowTriangles[index++] = next + i;
                        shadowTriangles[index++] = next + i1;

                        shadowTriangles[index++] = current + i;
                        shadowTriangles[index++] = next + i1;
                        shadowTriangles[index++] = current + i1;
                    }
                }

                int top = start + (rings - 1) * count;
                int apex = start + rings * count;
                int bottomCenter = apex + 1;

                for (int i = 0; i < count; i++)
                {
                    int i1 = (i + 1) % count;

                    shadowTriangles[index++] = top + i;
                    shadowTriangles[index++] = apex;
                    shadowTriangles[index++] = top + i1;

                    shadowTriangles[index++] = start + i;
                    shadowTriangles[index++] = bottomCenter;
                    shadowTriangles[index++] = start + i1;
                }
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
