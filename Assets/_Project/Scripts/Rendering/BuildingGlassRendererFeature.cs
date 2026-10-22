using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    // Reuses each building's existing mesh/material. Only its tagged glass pass is drawn here.
    public sealed class BuildingGlassRendererFeature : ScriptableRendererFeature
    {
        private GlassPass pass;

        public override void Create()
        {
            pass = new GlassPass { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(pass);
        }

        private sealed class GlassPass : ScriptableRenderPass
        {
            private static readonly ShaderTagId GlassTag = new("AeroGlassBorder");
            private readonly ProfilingSampler sampler = new("Building glass borders");

            private sealed class PassData
            {
                public RendererListHandle renderers;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var rendering = frameData.Get<UniversalRenderingData>();
                var camera = frameData.Get<UniversalCameraData>();
                var lights = frameData.Get<UniversalLightData>();
                if (!resources.cameraOpaqueTexture.IsValid() || !resources.cameraDepthTexture.IsValid())
                    return;

                var drawing = RenderingUtils.CreateDrawingSettings(GlassTag, rendering, camera, lights,
                    SortingCriteria.CommonTransparent);
                drawing.enableInstancing = true;
                var filtering = new FilteringSettings(RenderQueueRange.opaque, camera.camera.cullingMask);
                var rendererList = renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));

                using var builder = renderGraph.AddRasterRenderPass<PassData>("Building glass borders", out var data, sampler);
                data.renderers = rendererList;
                builder.UseRendererList(rendererList);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                builder.UseTexture(resources.cameraOpaqueTexture, AccessFlags.Read);
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                // Like URP's DrawObjectsPass, the lit material consumes the pipeline's published
                // reflection/shadow/light resources. Color/depth reads above remain explicit.
                builder.UseAllGlobalTextures(true);
                builder.SetRenderFunc(static (PassData passData, RasterGraphContext context) =>
                    context.cmd.DrawRendererList(passData.renderers));
            }
        }
    }
}
