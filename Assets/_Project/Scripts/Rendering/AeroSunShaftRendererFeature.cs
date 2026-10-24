using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    public sealed class AeroSunShaftRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class ShaftSettings
        {
            [Range(0, 3)] public float intensity = .85f;
            [Range(.001f, .1f)] public float density = .035f;
            [Range(8, 48)] public float distance = 42;
            [Range(8, 24)] public int samples = 24;
            public Color tint = new(1, .96f, .86f);
        }
        public Shader shaftShader;
        public ShaftSettings settings = new();
        [NonSerialized] public float previewTime = -1;
        private Material material;
        private ShaftPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = shaftShader != null ? CoreUtils.CreateEngineMaterial(shaftShader) : null;
            pass = new ShaftPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (material != null && settings.intensity > 0 && AeroLightRendererFeature.SupportsCamera(data.cameraData.camera) &&
                data.cameraData.renderType == CameraRenderType.Base) renderer.EnqueuePass(pass);
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; }

        private sealed class ShaftPass : ScriptableRenderPass
        {
            private readonly AeroSunShaftRendererFeature owner;
            private readonly ProfilingSampler scatter = new("Aero sun shadow scattering");
            private readonly ProfilingSampler composite = new("Aero sun depth-aware composite");
            private static readonly int Params = Shader.PropertyToID("_AeroShaftParams");
            private static readonly int Tint = Shader.PropertyToID("_AeroSunTint");
            private static readonly int Shadow = Shader.PropertyToID("_AeroSunShadow");
            private static readonly int Depth = Shader.PropertyToID("_AeroFullDepth");
            private static readonly int Texel = Shader.PropertyToID("_AeroRaysTexelSize");
            private sealed class ScatterData
            {
                public Material material;
                public TextureHandle depth, shadow;
                public Vector4 parameters;
                public Color tint;
                public float clock;
            }
            private sealed class CompositeData
            {
                public Material material;
                public TextureHandle depth, rays;
                public Vector4 texel;
            }
            public ShaftPass(AeroSunShaftRendererFeature owner) { this.owner = owner; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var lights = frameData.Get<UniversalLightData>();
                if (!resources.cameraDepthTexture.IsValid() || !resources.mainShadowsTexture.IsValid() || lights.mainLightIndex < 0) return;
                var descriptor = resources.activeColorTexture.GetDescriptor(graph);
                descriptor.name = "Aero half-resolution sun shafts";
                descriptor.width = Mathf.Max(1, (descriptor.width + 1) / 2);
                descriptor.height = Mathf.Max(1, (descriptor.height + 1) / 2);
                descriptor.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.clearBuffer = false;
                var rays = graph.CreateTexture(descriptor);
                var s = owner.settings;
                using (var builder = graph.AddRasterRenderPass<ScatterData>("Aero sun shadow scattering", out var d, scatter))
                {
                    d.material = owner.material; d.depth = resources.cameraDepthTexture; d.shadow = resources.mainShadowsTexture;
                    d.parameters = new Vector4(Mathf.Clamp(s.distance, 8, 48), Mathf.Clamp(s.density, .001f, .1f), s.intensity, Mathf.Clamp(s.samples, 8, 24));
                    d.tint = s.tint; d.clock = owner.previewTime;
                    builder.UseTexture(d.depth, AccessFlags.Read); builder.UseTexture(d.shadow, AccessFlags.Read);
                    builder.SetRenderAttachment(rays, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (ScatterData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetVector(Params, data.parameters); data.material.SetColor(Tint, data.tint);
                        data.material.SetFloat("_AeroShaftClock", data.clock);
                        data.material.SetTexture(Shadow, data.shadow);
                        Blitter.BlitTexture(ctx.cmd, data.depth, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
                using (var builder = graph.AddRasterRenderPass<CompositeData>("Aero sun depth-aware composite", out var d, composite))
                {
                    d.material = owner.material; d.depth = resources.cameraDepthTexture; d.rays = rays;
                    d.texel = new Vector4(1f / descriptor.width, 1f / descriptor.height, descriptor.width, descriptor.height);
                    builder.UseTexture(d.depth, AccessFlags.Read); builder.UseTexture(d.rays, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(Depth, data.depth); data.material.SetVector(Texel, data.texel);
                        Blitter.BlitTexture(ctx.cmd, data.rays, new Vector4(1, 1, 0, 0), data.material, 1);
                    });
                }
            }
        }
    }
}
