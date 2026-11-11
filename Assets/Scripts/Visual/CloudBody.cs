using UnityEngine;
using UnityEngine.Rendering;

namespace Decoherence
{
    /// <summary>
    /// 退相干态的云体：由若干球体并集构成，贴合任意障碍并保持体积。
    /// 视觉与判定共用同一组球体（唯一真值），因此"看得到即打得到"。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CloudBody : MonoBehaviour
    {
        [Header("Sources")]
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private PlayerCoherence coherence;
        [Tooltip("退相干时隐藏的方块身体")]
        [SerializeField] private Transform blockyBody;

        [Header("Shape")]
        [SerializeField, Min(1)] private int blobCount = 5;
        [SerializeField, Min(0.05f)] private float coherentContactRadius = 0.35f;
        [SerializeField, Min(0.05f)] private float lowCoherenceContactRadius = 0.55f;
        [Tooltip("退相干态的核心半径：云雾可被压到这一半径，也是挤压的硬下限")]
        [SerializeField, Min(0.05f)] private float coreRadius = 0.15f;
        [Tooltip("相干态的实体半径，用于恢复 CharacterController")]
        [SerializeField, Min(0.05f)] private float bodyRadius = 0.35f;
        [SerializeField, Range(0.2f, 0.8f)] private float blobSpacingFactor = 0.45f;
        [SerializeField, Min(0.1f)] private float volumeCap = 1.6f;
        [SerializeField, Min(0.01f)] private float shapeSmoothing = 0.08f;
        [SerializeField] private float cloudHeight = 0.8f;

        [Header("Look")]
        [SerializeField] private Material cloudMaterial;
        [SerializeField] private Material coreMaterial;
        [SerializeField] private float rotationDegreesPerSecond = 20f;
        [SerializeField, Min(0.01f)] private float transitionDuration = 0.15f;
        [SerializeField] private Color cloudColor = new Color(0.82f, 0.94f, 1f, 1f);
        [SerializeField] private float baseAlpha = 0.62f;
        [SerializeField] private Vector2 edgeSoftnessRange = new Vector2(0.07f, 0.28f);

        [Header("Barriers")]
        [SerializeField] private LayerMask barrierMask = ~0;

        private readonly Collider[] barrierBuffer = new Collider[32];
        private CharacterController controller;
        private Transform[] blobs;
        private Transform core;
        private Transform haze;
        private Material runtimeHazeMaterial;
        private Material runtimeCloudMaterial;
        private Material runtimeCoreMaterial;
        private Vector3[] blobCenters;
        private float[] blobRadii;
        private float[] blobTargetRadii;
        private float[] blobVariation;
        private Vector3[] blobJitter;
        private float visibility;
        private float noiseRotation;

        public bool IsVisible => visibility > 0.01f;
        public float ContactRadius { get; private set; }
        public float Visibility => visibility;

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

            BuildBlobs();
        }

        private void BuildBlobs()
        {
            blobCount = Mathf.Max(1, blobCount);
            blobs = new Transform[blobCount];
            blobCenters = new Vector3[blobCount];
            blobRadii = new float[blobCount];
            blobTargetRadii = new float[blobCount];
            blobVariation = new float[blobCount];
            blobJitter = new Vector3[blobCount];

            for (int i = 0; i < blobCount; i++)
            {
                blobVariation[i] = 0.70f + 0.55f * Hash01(i, 1);

                float angle = Hash01(i, 2) * Mathf.PI * 2f;
                float radial = 0.30f * Hash01(i, 3);
                blobJitter[i] = new Vector3(
                    Mathf.Cos(angle) * radial,
                    (Hash01(i, 4) - 0.5f) * 0.55f,
                    Mathf.Sin(angle) * radial);
            }

            for (int i = 0; i < blobCount; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Blob" + i;
                go.transform.SetParent(transform, false);
                go.transform.localPosition = Vector3.zero;

                var collider = go.GetComponent<Collider>();
                if (collider != null)
                {
                    DestroyImmediate(collider);
                }

                var renderer = go.GetComponent<MeshRenderer>();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (runtimeCloudMaterial == null && cloudMaterial != null)
                {
                    runtimeCloudMaterial = new Material(cloudMaterial) { hideFlags = HideFlags.DontSave };
                    ApplyCloudLook(0f);
                }

                if (runtimeCloudMaterial != null)
                {
                    renderer.sharedMaterial = runtimeCloudMaterial;
                }

                DisplaceSphere(go, i);

                blobs[i] = go.transform;
            }

            var coreGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            coreGo.name = "Core";
            coreGo.transform.SetParent(transform, false);
            var coreCollider = coreGo.GetComponent<Collider>();
            if (coreCollider != null)
            {
                DestroyImmediate(coreCollider);
            }

            var coreRenderer = coreGo.GetComponent<MeshRenderer>();
            coreRenderer.shadowCastingMode = ShadowCastingMode.Off;
            coreRenderer.receiveShadows = false;
            if (coreMaterial != null)
            {
                runtimeCoreMaterial = new Material(coreMaterial) { hideFlags = HideFlags.DontSave };
                coreRenderer.sharedMaterial = runtimeCoreMaterial;
            }

            core = coreGo.transform;
            core.localScale = Vector3.one * (coreRadius * 2f);

            // 雾晕：比判定体更大的一层薄雾，只做视觉，不参与判定。
            var hazeGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            hazeGo.name = "Haze";
            hazeGo.transform.SetParent(transform, false);
            var hazeCollider = hazeGo.GetComponent<Collider>();
            if (hazeCollider != null)
            {
                DestroyImmediate(hazeCollider);
            }

            var hazeRenderer = hazeGo.GetComponent<MeshRenderer>();
            hazeRenderer.shadowCastingMode = ShadowCastingMode.Off;
            hazeRenderer.receiveShadows = false;
            if (cloudMaterial != null)
            {
                runtimeHazeMaterial = new Material(cloudMaterial) { hideFlags = HideFlags.DontSave };
                runtimeHazeMaterial.SetFloat("_Alpha", 0.16f);
                runtimeHazeMaterial.SetFloat("_EdgeSoftness", 0.55f);
                runtimeHazeMaterial.SetFloat("_NoiseStrength", 1.1f);
                runtimeHazeMaterial.SetFloat("_WispStrength", 0.25f);
                runtimeHazeMaterial.SetFloat("_DriftSpeed", 0.18f);
                hazeRenderer.sharedMaterial = runtimeHazeMaterial;
            }

            haze = hazeGo.transform;
        }

        /// <summary>把球体网格顶点按噪声推挤成不规则团块，使并集轮廓呈絮状而非光滑球。</summary>
        private static void DisplaceSphere(GameObject go, int seed)
        {
            var filter = go.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                return;
            }

            var mesh = Object.Instantiate(filter.sharedMesh);
            mesh.name = "CloudBlob" + seed;

            Vector3[] vertices = mesh.vertices;
            float seedOffset = 12.7f + seed * 5.31f;

            for (int v = 0; v < vertices.Length; v++)
            {
                Vector3 vertex = vertices[v];
                Vector3 direction = vertex.sqrMagnitude > 1e-6f ? vertex.normalized : Vector3.up;

                float noise =
                    Mathf.PerlinNoise(direction.x * 2.4f + seedOffset, direction.y * 2.4f + seedOffset) * 0.6f +
                    Mathf.PerlinNoise(direction.y * 4.9f + seedOffset, direction.z * 4.9f + seedOffset) * 0.4f;

                float radius = 0.5f * (1f + (noise - 0.5f) * 0.85f);
                vertices[v] = direction * radius;
            }

            mesh.vertices = vertices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            filter.sharedMesh = mesh;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f || blobs == null)
            {
                return;
            }

            bool decoherent = motor != null && motor.State == CoherenceState.Decoherent;
            float targetVisibility = decoherent ? 1f : 0f;
            visibility = Mathf.MoveTowards(visibility, targetVisibility, dt / transitionDuration);

            ApplyBodyRadius(decoherent);
            ToggleRenderers(visibility > 0.001f);

            if (blockyBody != null)
            {
                blockyBody.gameObject.SetActive(visibility < 0.5f);
            }

            if (visibility <= 0.001f)
            {
                return;
            }

            float normalized = coherence != null ? Mathf.Clamp01(coherence.Normalized) : 1f;
            ContactRadius = Mathf.Lerp(lowCoherenceContactRadius, coherentContactRadius, normalized);

            noiseRotation += rotationDegreesPerSecond * dt;
            UpdateBlobLayout(ContactRadius);
            ResolveBarriers();
            ApplyTransforms();
            ApplyCloudLook(normalized);
        }

        /// <summary>退相干态把实体胶囊收缩到核心半径；恢复时若空间不足则暂不扩张。</summary>
        private void ApplyBodyRadius(bool decoherent)
        {
            if (controller == null)
            {
                controller = motor != null ? motor.GetComponent<CharacterController>() : GetComponentInParent<CharacterController>();
            }

            if (controller == null)
            {
                return;
            }

            float target = decoherent ? coreRadius : bodyRadius;
            if (Mathf.Abs(controller.radius - target) < 0.001f)
            {
                return;
            }

            if (target > controller.radius)
            {
                Vector3 center = controller.transform.position + controller.center;
                float halfHeight = Mathf.Max(0f, controller.height * 0.5f - target);
                int count = Physics.OverlapCapsuleNonAlloc(
                    center + Vector3.up * halfHeight,
                    center - Vector3.up * halfHeight,
                    target,
                    barrierBuffer,
                    barrierMask,
                    QueryTriggerInteraction.Ignore);

                for (int i = 0; i < count; i++)
                {
                    if (barrierBuffer[i] != null && !IsOwn(barrierBuffer[i].transform))
                    {
                        return;
                    }
                }
            }

            controller.radius = target;
        }

        private void UpdateBlobLayout(float contactRadius)
        {
            float spacing = contactRadius * blobSpacingFactor;
            float baseBlobRadius = Mathf.Max(coreRadius, contactRadius - spacing);

            for (int i = 0; i < blobCount; i++)
            {
                Vector3 ring = i == 0
                    ? Vector3.zero
                    : Quaternion.Euler(0f, i * (360f / Mathf.Max(1, blobCount - 1)), 0f) * new Vector3(spacing, 0f, 0f);

                Vector3 jitter = blobJitter != null && i < blobJitter.Length ? blobJitter[i] : Vector3.zero;
                Vector3 offset = (ring * 0.65f + jitter) * Mathf.Max(contactRadius, 0.01f);

                Vector3 center = transform.position + Vector3.up * cloudHeight + offset;
                blobCenters[i] = center;

                float variation = blobVariation != null && i < blobVariation.Length ? blobVariation[i] : 1f;
                float blobRadius = baseBlobRadius * variation;
                blobTargetRadii[i] = blobRadius;

                if (blobRadii[i] <= 0f)
                {
                    blobRadii[i] = blobRadius;
                }
            }
        }

        /// <summary>对每个球体求最近障碍，按体积守恒放大半径（穿墙部分由墙体遮挡）。</summary>
        private void ResolveBarriers()
        {
            for (int i = 0; i < blobCount; i++)
            {
                float baseRadius = blobTargetRadii[i];
                float nearest = float.MaxValue;

                int count = Physics.OverlapSphereNonAlloc(blobCenters[i], baseRadius + 0.05f, barrierBuffer, barrierMask, QueryTriggerInteraction.Ignore);
                for (int c = 0; c < count; c++)
                {
                    Collider collider = barrierBuffer[c];
                    if (collider == null || IsOwn(collider.transform))
                    {
                        continue;
                    }

                    Vector3 closest = collider.ClosestPoint(blobCenters[i]);
                    float distance = Vector3.Distance(closest, blobCenters[i]);
                    if (distance < nearest)
                    {
                        nearest = distance;
                    }
                }

                float resolved = baseRadius;
                if (nearest < baseRadius)
                {
                    resolved = SolveVolumePreservingRadius(baseRadius, Mathf.Max(nearest, 0.01f));
                    resolved = Mathf.Min(resolved, baseRadius * volumeCap);
                }

                float smoothing = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(shapeSmoothing, 0.001f));
                blobRadii[i] = Mathf.Lerp(blobRadii[i], resolved, smoothing);
            }
        }

        /// <summary>球被距离 h 的平面裁切后，求保持原体积所需的半径。</summary>
        private static float SolveVolumePreservingRadius(float targetRadius, float h)
        {
            float target = 4f / 3f * Mathf.PI * targetRadius * targetRadius * targetRadius;
            float low = targetRadius;
            float high = targetRadius * 3f;

            for (int i = 0; i < 24; i++)
            {
                float mid = (low + high) * 0.5f;
                if (SolidVolume(mid, h) < target)
                {
                    low = mid;
                }
                else
                {
                    high = mid;
                }
            }

            return (low + high) * 0.5f;
        }

        private static float SolidVolume(float radius, float h)
        {
            if (h >= radius)
            {
                return 4f / 3f * Mathf.PI * radius * radius * radius;
            }

            float capHeight = radius - h;
            float cap = Mathf.PI * capHeight * capHeight * (3f * radius - capHeight) / 3f;
            return 4f / 3f * Mathf.PI * radius * radius * radius - cap;
        }

        private void ApplyTransforms()
        {
            for (int i = 0; i < blobCount; i++)
            {
                blobs[i].position = blobCenters[i];
                blobs[i].localScale = Vector3.one * (blobRadii[i] * 2f);
            }

            if (core != null)
            {
                core.position = transform.position + Vector3.up * cloudHeight;
                core.localScale = Vector3.one * (coreRadius * 2f);
            }

            if (haze != null)
            {
                haze.position = transform.position + Vector3.up * cloudHeight;
                haze.localScale = Vector3.one * (Mathf.Max(ContactRadius, coreRadius) * 2f * 1.6f);
            }
        }

        private void ApplyCloudLook(float normalized)
        {
            if (runtimeCloudMaterial == null)
            {
                return;
            }

            float softness = Mathf.Lerp(edgeSoftnessRange.y, edgeSoftnessRange.x, normalized);
            float dissolve = 1f - visibility;

            runtimeCloudMaterial.SetColor("_Color", cloudColor);
            runtimeCloudMaterial.SetFloat("_Alpha", baseAlpha);
            runtimeCloudMaterial.SetFloat("_EdgeSoftness", softness);
            runtimeCloudMaterial.SetFloat("_NoiseRotation", noiseRotation * Mathf.Deg2Rad);
            runtimeCloudMaterial.SetFloat("_Dissolve", dissolve);

            if (runtimeHazeMaterial != null)
            {
                runtimeHazeMaterial.SetColor("_Color", cloudColor);
                runtimeHazeMaterial.SetFloat("_Alpha", Mathf.Lerp(0.20f, 0.10f, normalized));
                runtimeHazeMaterial.SetFloat("_NoiseRotation", -noiseRotation * Mathf.Deg2Rad * 0.6f);
                runtimeHazeMaterial.SetFloat("_Dissolve", dissolve);
            }
        }

        private void ToggleRenderers(bool visible)
        {
            foreach (var blob in blobs)
            {
                var renderer = blob.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }

            if (core != null)
            {
                var renderer = core.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }

            if (haze != null)
            {
                var renderer = haze.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.enabled = visible;
                }
            }
        }

        private static float Hash01(int index, int salt)
        {
            uint x = (uint)(index * 73856093 ^ salt * 19349663) | 1u;
            x ^= x >> 13;
            x *= 0x5bd1e995u;
            x ^= x >> 15;
            return (x & 0xFFFFFFu) / 16777215f;
        }

        private bool IsOwn(Transform candidate)
        {
            return candidate == transform || candidate.IsChildOf(transform) || (motor != null && candidate.IsChildOf(motor.transform));
        }

        /// <summary>敌人是否与云体相交（与渲染同一组球体）。</summary>
        public bool Overlaps(Collider other)
        {
            if (other == null || blobs == null)
            {
                return false;
            }

            Vector3 corePosition = transform.position + Vector3.up * cloudHeight;

            for (int i = 0; i < blobCount; i++)
            {
                Vector3 closest = other.ClosestPoint(blobCenters[i]);
                if (Vector3.Distance(closest, blobCenters[i]) > blobRadii[i])
                {
                    continue;
                }

                if (Physics.Linecast(corePosition, closest, out RaycastHit hit, barrierMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != other && !hit.collider.transform.IsChildOf(other.transform) && !IsOwn(hit.collider.transform))
                    {
                        continue;
                    }
                }

                return true;
            }

            return false;
        }
    }
}

