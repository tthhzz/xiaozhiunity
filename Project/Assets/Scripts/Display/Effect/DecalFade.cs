using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace XiaoZhi.Unity
{
    [Serializable]
    [VolumeComponentMenu("XiaoZhi/PostProcessing/DecalFade")]
    public sealed class DecalFade : VolumeComponent, IPostProcessComponent
    {
        public TextureParameter MaskTexture = new(null);
        public ColorParameter MaskColor = new(Color.white);
        public ClampedFloatParameter MaskOffset = new(0, 0, 1);
        public ClampedFloatParameter MaskScale = new(1, 0, 1);
        public ClampedFloatParameter MaskFade = new(0, 0, 1);

        public bool IsActive() => MaskFade.value > 0 || MaskOffset.value > 0;

        public bool IsTileCompatible()
        {
            return true;
        }
    }
}