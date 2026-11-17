using UnityEngine;

namespace Decoherence
{
    /// <summary>
    /// 受击闪光：把子物体渲染器的底色短暂推向高亮色，不改材质资源。
    /// 只负责表现；什么时候闪、闪多久的语义由调用方决定。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterHitFlash : MonoBehaviour
    {
        [Header("Look")]
        [SerializeField]
        Color flashColor = new Color(1f, 0.86f, 0.82f);

        [Tooltip("单次闪光持续时长（秒）")]
        [SerializeField, Min(0.01f)]
        float duration = 0.09f;

        [Tooltip("留空时自动收集子物体上的渲染器")]
        [SerializeField]
        Renderer[] renderers;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        MaterialPropertyBlock block;
        float flashUntil;
        bool painted;

        void Awake()
        {
            if (renderers == null || renderers.Length == 0)
            {
                renderers = GetComponentsInChildren<Renderer>(true);
            }

            block = new MaterialPropertyBlock();
        }

        /// <summary>开始一次闪光；重复调用只刷新持续时间，所以持续受伤会持续发亮。</summary>
        public void Flash()
        {
            flashUntil = Time.time + duration;
        }

        void Update()
        {
            bool flashing = Time.time < flashUntil;

            if (flashing == painted)
            {
                return;
            }

            painted = flashing;
            Apply(flashing);
        }

        void Apply(bool flashing)
        {
            if (renderers == null)
            {
                return;
            }

            foreach (Renderer target in renderers)
            {
                if (target == null)
                {
                    continue;
                }

                if (!flashing)
                {
                    target.SetPropertyBlock(null);
                    continue;
                }

                target.GetPropertyBlock(block);
                block.SetColor(BaseColorId, flashColor);
                target.SetPropertyBlock(block);
            }
        }
    }
}
