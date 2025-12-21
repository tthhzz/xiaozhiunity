using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace XiaoZhi.Unity
{
    public class DecalFadeFeature : ScriptableRendererFeature
    {
        private DecalFadePass _pass;

        public override void Create()
        {
            _pass = new DecalFadePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _pass?.Dispose();
            _pass = null;
        }

        private class DecalFadePass : ScriptableRenderPass
        {
            private const string ShaderName = "XiaoZhi/PostProcessing/DecalFade";
            private static readonly MaterialPropertyBlock SharedPropertyBlock = new();
            private static readonly int BlitTexture = Shader.PropertyToID("_BlitTexture");
            private static readonly int BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
            private static readonly int MaskTexture = Shader.PropertyToID("_MaskTex");
            private static readonly int MaskColor = Shader.PropertyToID("_MaskColor");
            private static readonly int MaskOffset = Shader.PropertyToID("_MaskOffset");
            private static readonly int MaskScale = Shader.PropertyToID("_MaskScale");
            private static readonly int MaskFade = Shader.PropertyToID("_MaskFade");

            private Material _material;
            private RTHandle _copiedColor;

            public DecalFadePass()
            {
                var shader = Shader.Find(ShaderName);
                if (shader) _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                profilingSampler = new ProfilingSampler(GetType().Name);
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                ResetTarget();
                ReAllocate(renderingData.cameraData.cameraTargetDescriptor);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (!_material) return;
                var settings = VolumeManager.instance.stack.GetComponent<DecalFade>();
                if (!settings || !settings.IsActive()) return;
                ref var cameraData = ref renderingData.cameraData;
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, profilingSampler))
                {
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                    SetupMaterial(settings);
                    CoreUtils.SetRenderTarget(cmd, _copiedColor);
                    ExecuteCopyColorPass(cmd, cameraData.renderer.cameraColorTargetHandle);
                    CoreUtils.SetRenderTarget(cmd, cameraData.renderer.cameraColorTargetHandle);
                    ExecuteMainPass(cmd, _copiedColor, _material, 0);
                }
                
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                CommandBufferPool.Release(cmd);
            }

            public void Dispose()
            {
                _copiedColor?.Release();
                _copiedColor = null;
                if (_material)
                {
#if UNITY_EDITOR
                    DestroyImmediate(_material);
#else
                    Destroy(_material);
#endif
                    _material = null;
                }
            }

            private void SetupMaterial(DecalFade settings)
            {
                _material.SetTexture(MaskTexture, settings.MaskTexture.value);
                _material.SetColor(MaskColor, settings.MaskColor.value);
                _material.SetFloat(MaskOffset, settings.MaskOffset.value);
                _material.SetFloat(MaskScale, settings.MaskScale.value);
                _material.SetFloat(MaskFade, settings.MaskFade.value);
            }

            private void ReAllocate(RenderTextureDescriptor desc)
            {
                desc.msaaSamples = 1;
                desc.depthBufferBits = (int)DepthBits.None;
                RenderingUtils.ReAllocateIfNeeded(ref _copiedColor, desc, name: "_DecalFadePassColorCopy");
            }

            private static void ExecuteCopyColorPass(CommandBuffer cmd, RTHandle sourceTexture)
            {
                Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            private static void ExecuteMainPass(CommandBuffer cmd, RTHandle sourceTexture, Material material,
                int passIndex)
            {
                SharedPropertyBlock.Clear();
                SharedPropertyBlock.SetTexture(BlitTexture, sourceTexture);
                SharedPropertyBlock.SetVector(BlitScaleBias, new Vector4(1, 1, 0, 0));
                cmd.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3, 1,
                    SharedPropertyBlock);
            }
        }
    }
}