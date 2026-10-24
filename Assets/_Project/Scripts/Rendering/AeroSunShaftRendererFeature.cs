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
        public AeroRaySettings raySettings;
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
                public TextureHandle depth, shadow, additionalShadow;
                public Vector4 parameters;
                public Vector4 volume0, volume1, blend, noise, velocity, fills;
                public Color tint;
                public float clock, sunHasCasters;
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
                var camera = frameData.Get<UniversalCameraData>().camera;
                var context = AeroTowerLightContext.ForCamera(camera);
                if (context == null || !context.HasGeometry || context.InteriorWeight <= .0001f) return;
                if (!resources.cameraDepthTexture.IsValid() || !resources.mainShadowsTexture.IsValid() || lights.mainLightIndex < 0) return;
                var profile = context.raySettings != null ? context.raySettings : owner.raySettings;
                if (profile == null || profile.intensity <= 0) return;
                var descriptor = resources.activeColorTexture.GetDescriptor(graph);
                descriptor.name = "Aero half-resolution sun shafts";
                descriptor.width = Mathf.Max(1, (descriptor.width + profile.Downsample - 1) / profile.Downsample);
                descriptor.height = Mathf.Max(1, (descriptor.height + profile.Downsample - 1) / profile.Downsample);
                descriptor.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                descriptor.depthBufferBits = DepthBits.None;
                descriptor.msaaSamples = MSAASamples.None;
                descriptor.clearBuffer = false;
                var rays = graph.CreateTexture(descriptor);
                var s = owner.settings;
                var volume = VolumeManager.instance.stack.GetComponent<AeroShaftVolume>();
                float intensity = volume != null && volume.active ? volume.intensity.value : 1;
                float density = volume != null && volume.active ? volume.density.value : 1;
                using (var builder = graph.AddRasterRenderPass<ScatterData>("Aero sun shadow scattering", out var d, scatter))
                {
                    d.material = owner.material; d.depth = resources.cameraDepthTexture; d.shadow = resources.mainShadowsTexture;
                    d.additionalShadow = resources.additionalShadowsTexture;
                    d.parameters = new Vector4(Mathf.Clamp(profile.distance, 8, 32), Mathf.Clamp(profile.density * density, .001f, .1f), profile.intensity * s.intensity * intensity, profile.Steps);
                    d.volume0 = context.Volume0; d.volume1 = context.Volume1; d.blend = context.VolumeBlend;
                    d.noise = new Vector4(profile.noiseScale, profile.noiseContrast, profile.interiorHaze, profile.softness);
                    d.velocity = profile.noiseVelocity;
                    // ForwardLights packs visible lights in order, omitting the main light.
                    // Call GetAdditionalPerObjectLight with these indices, never the screen tile/per-object iterator.
                    d.fills = new Vector4(-1, -1, 0, 0);
                    int additionalIndex = 0;
                    for (int i = 0; i < lights.visibleLights.Length; i++)
                    {
                        if (i == lights.mainLightIndex) continue;
                        for (int slot = 0; slot < Mathf.Min(2, context.bounceLights?.Length ?? 0); slot++)
                            if (context.bounceLights[slot] != null && lights.visibleLights[i].light == context.bounceLights[slot] && context.bounceLights[slot].enabled)
                                d.fills[slot] = additionalIndex;
                        additionalIndex++;
                    }
                    d.tint = s.tint; d.clock = owner.previewTime;
                    d.sunHasCasters = frameData.Get<UniversalRenderingData>().cullResults.GetShadowCasterBounds(lights.mainLightIndex, out _) ? 1 : 0;
                    builder.UseTexture(d.depth, AccessFlags.Read); builder.UseTexture(d.shadow, AccessFlags.Read);
                    if (d.additionalShadow.IsValid()) builder.UseTexture(d.additionalShadow, AccessFlags.Read);
                    else d.fills = new Vector4(-1, -1, 0, 0);
                    builder.SetRenderAttachment(rays, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (ScatterData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetVector(Params, data.parameters); data.material.SetColor(Tint, data.tint);
                        data.material.SetFloat("_AeroShaftClock", data.clock);
                        data.material.SetFloat("_AeroSunHasCasters", data.sunHasCasters);
                        data.material.SetTexture(Shadow, data.shadow);
                        if (data.additionalShadow.IsValid()) data.material.SetTexture("_AdditionalLightsShadowmapTexture", data.additionalShadow);
                        data.material.SetVector("_AeroTowerVolume0", data.volume0); data.material.SetVector("_AeroTowerVolume1", data.volume1);
                        data.material.SetVector("_AeroTowerBlend", data.blend); data.material.SetVector("_AeroNoise", data.noise);
                        data.material.SetVector("_AeroNoiseVelocity", data.velocity); data.material.SetVector("_AeroFillIndices", data.fills);
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
