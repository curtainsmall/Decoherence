using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 退相干态的玩法场：不可见的核心 + 8 根 2D 柱（加速度驱动滞后、弹簧阻尼回正），
    /// 柱尖经闭合三次 B 样条生成触发边界，面积与重叠判定都基于该多边形（XZ 平面）。
    /// 只做判定，不渲染；渲染由 PlayerCloudVisual 读取这里的结果。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerCloudField : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, HideInInspector]
        CharacterMotor motor;

        [SerializeField, HideInInspector]
        PlayerState state;

        [SerializeField, HideInInspector]
        PlayerCoherence coherence;

        [SerializeField, HideInInspector]
        PlayerStats stats;

        [Header("Pillars (2D)")]
        [SerializeField, Range(3, 24)]
        int pillarCount = 8;

        [SerializeField, Min(0.05f)]
        float pillarLength = 0.40f;

        [SerializeField, Min(0.05f)]
        float lowCoherencePillarLength = 0.60f;

        [Tooltip("角刚度：作用于 1 rad 偏角的角加速度，单位 rad/s²")]
        [SerializeField, Min(1f)]
        float angularStiffness = 55f;

        [Tooltip("角阻尼，单位 1/s")]
        [SerializeField, Min(0f)]
        float angularDamping = 7f;

        [Tooltip("核心加速度到角加速度的增益")]
        [SerializeField, Min(0f)]
        float accelerationGain = 2.6f;

        [SerializeField, Range(10f, 89f)]
        float maxLagDegrees = 78f;

        [SerializeField, Min(0.05f)]
        float rayHeight = 0.5f;

        [SerializeField, Min(0f)]
        float wallClearance = 0.03f;

        [SerializeField]
        LayerMask barrierMask = ~0;

        [Header("Boundary")]
        [SerializeField, Range(16, 128)]
        int boundarySamples = 64;

        float[] lag;

        float[] lagVelocity;

        Vector2[] tips;

        Vector2[] boundary;

        Vector2 previousVelocity;

        public bool Active { get; private set; }

        public float Area { get; private set; }

        public float CoherenceNormalized { get; private set; }

        public Vector2 Core => new Vector2(transform.position.x, transform.position.z);

        public Vector2[] Boundary => boundary;

        void Awake()
        {
            ResolveReferences();

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
        }

        void OnValidate()
        {
            ResolveReferences();
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;

            if (deltaTime <= 0f || lag == null)
            {
                return;
            }

            Active = state != null && state.State == CoherenceState.Decoherent;

            if (!Active)
            {
                previousVelocity = GetPlanarVelocity();
                return;
            }

            CoherenceNormalized = coherence != null ? Mathf.Clamp01(coherence.Normalized) : 1f;

            // 升级带来的半径倍率
            float radiusScale = stats != null ? stats.Scale(StatKind.CloudRadius) : 1f;
            float length = Mathf.Lerp(lowCoherencePillarLength, pillarLength, CoherenceNormalized) * radiusScale;

            SimulatePillars(deltaTime, length);
            BuildBoundary();

            previousVelocity = GetPlanarVelocity();
        }

        /// <summary>敌人是否进入触发区（与边界多边形同一份数据）。</summary>
        public bool Overlaps(Collider other)
        {
            if (!Active || other == null || boundary == null)
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

        Vector2 GetPlanarVelocity()
        {
            if (motor == null)
            {
                return Vector2.zero;
            }

            Vector3 velocity = motor.PlanarVelocity;
            return new Vector2(velocity.x, velocity.z);
        }

        void SimulatePillars(float deltaTime, float length)
        {
            Vector2 velocity = GetPlanarVelocity();
            Vector2 acceleration = deltaTime > 0f ? (velocity - previousVelocity) / deltaTime : Vector2.zero;
            acceleration = Vector2.ClampMagnitude(acceleration, 80f);

            Vector2 origin = Core;
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

                lagVelocity[i] += angularAcceleration * deltaTime;
                lag[i] += lagVelocity[i] * deltaTime;

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
        float ResolveReach(Vector2 origin, Vector2 direction, float length)
        {
            Vector3 start = new Vector3(origin.x, rayHeight, origin.y);
            Vector3 dir = new Vector3(direction.x, 0f, direction.y);

            if (Physics.Raycast(start, dir, out RaycastHit hit, length + wallClearance, barrierMask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(0f, hit.distance - wallClearance);
            }

            return length;
        }

        /// <summary>闭合三次 B 样条穿过 8 个控制点，采样为边界多边形。</summary>
        void BuildBoundary()
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

        static float ComputeArea(Vector2[] polygon)
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

        bool IsInsidePolygon(Vector2 point)
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

        float DistanceToPolygon(Vector2 point)
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

        void ResolveReferences()
        {
            if (motor == null)
            {
                motor = GetComponentInParent<CharacterMotor>();
            }

            if (state == null)
            {
                state = GetComponentInParent<PlayerState>();
            }

            if (coherence == null)
            {
                coherence = GetComponentInParent<PlayerCoherence>();
            }

            if (stats == null)
            {
                stats = GetComponentInParent<PlayerStats>();
            }
        }
    }
}
