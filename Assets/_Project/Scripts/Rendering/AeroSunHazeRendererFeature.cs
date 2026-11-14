using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Distant ground on the sun's side fades into the sun's glow (AeroSunHaze.shader): one full-screen draw that reads
    /// depth and blends over the frame before post-processing. The glow and haze values come from the globals
    /// AeroDayNightCycle pushes (AeroSunGlow); the pass is skipped when there is no haze to draw.
    /// </summary>
    public sealed class AeroSunHazeRendererFeature : ScriptableRendererFeature
    {
        public Shader hazeShader;
        private Material material;
        private HazePass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = hazeShader != null ? CoreUtils.CreateEngineMaterial(hazeShader) : null;
            pass = new HazePass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        /// <summary>
        /// Outdoor views only: game and scene cameras that draw the sky. Where nothing wrote depth below the horizon the
        /// pass assumes open sea, which only holds when the background is the sky.
        /// </summary>
        public static bool SupportsCamera(Camera camera) =>
            AeroLightRendererFeature.SupportsCamera(camera) && camera.clearFlags == CameraClearFlags.Skybox;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (material != null && AeroRenderQuality.SunHaze && AeroSunGlow.Current.HazeVisible &&
                SupportsCamera(data.cameraData.camera) && data.cameraData.renderType == CameraRenderType.Base) renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; }

        private sealed class HazePass : ScriptableRenderPass
        {
            private readonly AeroSunHazeRendererFeature owner;
            private readonly ProfilingSampler sampler = new("Aero sun haze");
            private sealed class PassData
            {
                public Material material;
                public TextureHandle depth;
            }

            public HazePass(AeroSunHazeRendererFeature owner) { this.owner = owner; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid() || owner.material == null) return;
                using var builder = graph.AddRasterRenderPass<PassData>("Aero sun haze", out var d, sampler);
                d.material = owner.material; d.depth = resources.cameraDepthTexture;
                builder.UseTexture(d.depth, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                    Blitter.BlitTexture(ctx.cmd, data.depth, new Vector4(1, 1, 0, 0), data.material, 0));
            }
        }
    }
}
