using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Sun shafts. Inside the tower: shadow-mapped light shafts through the arches (Sun Shafts shader). Outdoors:
    /// separate beams of sunlight in the air near the camera, cut by the sun's shadows and strongest looking towards
    /// the sun (Sun Rays shader, driven by AeroSunGlow). Both follow the Sun shafts graphics setting.
    /// </summary>
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
        [Tooltip("Outdoor light shafts in the air towards the sun (Hidden/TowerOfBabel/Sun Rays).")]
        public Shader raysShader;
        public ShaftSettings settings = new();
        public AeroRaySettings raySettings;
        [NonSerialized] public float previewTime = -1;
        /// <summary>Review and tests: draw the shaft light alone instead of adding it to the frame.</summary>
        [NonSerialized] public bool isolate;
        /// <summary>Review and tests: draw the outdoor light shafts alone instead of adding them to the frame.</summary>
        [NonSerialized] public bool isolateRays;
        private Material material;
        // Separate materials for the scatter and the composite: values set on a material apply when the recorded frame
        // runs, so two steps sharing one would both see the last step's values.
        private Material raysScatter, raysComposite;
        private ShaftPass pass;
        private OutdoorShaftPass rayPass;

        public override void Create()
        {
            CoreUtils.Destroy(material); CoreUtils.Destroy(raysScatter); CoreUtils.Destroy(raysComposite);
            material = shaftShader != null ? CoreUtils.CreateEngineMaterial(shaftShader) : null;
            raysScatter = raysShader != null ? CoreUtils.CreateEngineMaterial(raysShader) : null;
            raysComposite = raysShader != null ? CoreUtils.CreateEngineMaterial(raysShader) : null;
            pass = new ShaftPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            // After the sun haze (same event), so far ground fades into the glow before the shafts cross it.
            rayPass = new OutdoorShaftPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 1 };
            rayPass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (AeroRenderQuality.SunShafts <= AeroRenderQuality.Off || data.cameraData.renderType != CameraRenderType.Base) return;
            var camera = data.cameraData.camera;
            if (material != null && settings.intensity > 0 && AeroLightRendererFeature.SupportsCamera(camera)) renderer.EnqueuePass(pass);
            if (raysScatter != null && AeroSunHazeRendererFeature.SupportsCamera(camera) && OutdoorStrength(camera, AeroSunGlow.Current) > .001f)
                renderer.EnqueuePass(rayPass);
        }
        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(material); material = null;
            CoreUtils.Destroy(raysScatter); raysScatter = null; CoreUtils.Destroy(raysComposite); raysComposite = null;
        }

        /// <summary>
        /// Strength of the outdoor light shafts for a camera: none with the sun well behind it (they scatter forwards and
        /// would barely show), and fading inside the tower, where the interior shafts carry the light.
        /// </summary>
        public static float OutdoorStrength(Camera camera, in AeroSunGlowState glow)
        {
            if (camera == null || !glow.RaysVisible) return 0;
            float facing = Vector3.Dot(camera.transform.forward, glow.direction);
            float strength = glow.rays.x * AeroSunGlow.SmoothStep(-.35f, .1f, facing);
            var context = AeroTowerLightContext.ForCamera(camera);
            if (context != null) strength *= 1 - .85f * Mathf.Clamp01(context.InteriorWeight);
            return strength;
        }

        private sealed class OutdoorShaftPass : ScriptableRenderPass
        {
            private readonly AeroSunShaftRendererFeature owner;
            private readonly ProfilingSampler scatterSampler = new("Aero outdoor light shafts");
            private readonly ProfilingSampler compositeSampler = new("Aero outdoor light shaft composite");
            private static readonly int ParamsId = Shader.PropertyToID("_AeroRaysParams");
            private static readonly int VolumeId = Shader.PropertyToID("_AeroRaysVolume");
            private static readonly int MarchId = Shader.PropertyToID("_AeroRaysMarch");
            private static readonly int TexelId = Shader.PropertyToID("_AeroRaysTexelSize");
            private static readonly int ShadowId = Shader.PropertyToID("_AeroSunShadow");
            private static readonly int DepthId = Shader.PropertyToID("_AeroFullDepth");
            private sealed class ScatterData
            {
                public Material material;
                public TextureHandle depth, shadow;
                public Vector4 parameters, volume, march;
            }
            private sealed class CompositeData
            {
                public Material material;
                public TextureHandle depth, shafts;
                public Vector4 texel;
                public int pass;
            }
            public OutdoorShaftPass(AeroSunShaftRendererFeature owner) { this.owner = owner; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var lights = frameData.Get<UniversalLightData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                var glow = AeroSunGlow.Current;
                if (owner.raysScatter == null || owner.raysComposite == null || !resources.cameraDepthTexture.IsValid()) return;
                float strength = OutdoorStrength(camera, glow);
                if (strength <= .001f) return;
                bool low = AeroRenderQuality.SunShafts <= AeroRenderQuality.Low;
                int downsample = low ? 4 : 2, steps = low ? 12 : 24;
                var descriptor = resources.activeColorTexture.GetDescriptor(graph);
                descriptor.name = "Aero outdoor light shafts";
                descriptor.width = Mathf.Max(1, (descriptor.width + downsample - 1) / downsample);
                descriptor.height = Mathf.Max(1, (descriptor.height + downsample - 1) / downsample);
                descriptor.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
                descriptor.depthBufferBits = DepthBits.None; descriptor.msaaSamples = MSAASamples.None; descriptor.clearBuffer = false;
                var shafts = graph.CreateTexture(descriptor);
                bool shadows = resources.mainShadowsTexture.IsValid() && lights.mainLightIndex >= 0;
                using (var builder = graph.AddRasterRenderPass<ScatterData>("Aero outdoor light shafts", out var d, scatterSampler))
                {
                    d.material = owner.raysScatter; d.depth = resources.cameraDepthTexture;
                    d.shadow = shadows ? resources.mainShadowsTexture : TextureHandle.nullHandle;
                    d.parameters = new Vector4(strength, glow.rays.y, glow.rays.z, glow.rays.w);
                    d.volume = glow.shafts;
                    float hasCasters = shadows && frameData.Get<UniversalRenderingData>().cullResults.GetShadowCasterBounds(lights.mainLightIndex, out _) ? 1 : 0;
                    d.march = new Vector4(steps, owner.previewTime >= 0 ? owner.previewTime : Time.realtimeSinceStartup, hasCasters, 0);
                    builder.UseTexture(d.depth, AccessFlags.Read);
                    if (d.shadow.IsValid()) builder.UseTexture(d.shadow, AccessFlags.Read);
                    builder.SetRenderAttachment(shafts, 0, AccessFlags.WriteAll);
                    builder.SetRenderFunc(static (ScatterData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetVector(ParamsId, data.parameters); data.material.SetVector(VolumeId, data.volume);
                        data.material.SetVector(MarchId, data.march);
                        if (data.shadow.IsValid()) data.material.SetTexture(ShadowId, data.shadow);
                        Blitter.BlitTexture(ctx.cmd, data.depth, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }
                using (var builder = graph.AddRasterRenderPass<CompositeData>("Aero outdoor light shaft composite", out var d, compositeSampler))
                {
                    d.material = owner.raysComposite; d.depth = resources.cameraDepthTexture; d.shafts = shafts;
                    d.texel = new Vector4(1f / descriptor.width, 1f / descriptor.height, descriptor.width, descriptor.height);
                    d.pass = owner.isolateRays ? 2 : 1;
                    builder.UseTexture(d.depth, AccessFlags.Read); builder.UseTexture(d.shafts, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, owner.isolateRays ? AccessFlags.Write : AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(DepthId, data.depth); data.material.SetVector(TexelId, data.texel);
                        Blitter.BlitTexture(ctx.cmd, data.shafts, new Vector4(1, 1, 0, 0), data.material, data.pass);
                    });
                }
            }
        }

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
                public Vector4 sun;
            }
            private sealed class CompositeData
            {
                public Material material;
                public TextureHandle depth, rays;
                public Vector4 texel;
                public int pass;
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
                    d.sun = new Vector4(AeroShaftLook.SunGain(profile, context.cycle, lights.visibleLights[lights.mainLightIndex].light),
                        Mathf.Clamp(profile.sunDensity * density, .001f, .2f), 0, 0);
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
                        data.material.SetVector("_AeroSunShaft", data.sun);
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
                    d.pass = owner.isolate ? 2 : 1;
                    builder.UseTexture(d.depth, AccessFlags.Read); builder.UseTexture(d.rays, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, owner.isolate ? AccessFlags.Write : AccessFlags.ReadWrite);
                    builder.SetRenderFunc(static (CompositeData data, RasterGraphContext ctx) =>
                    {
                        data.material.SetTexture(Depth, data.depth); data.material.SetVector(Texel, data.texel);
                        Blitter.BlitTexture(ctx.cmd, data.rays, new Vector4(1, 1, 0, 0), data.material, data.pass);
                    });
                }
            }
        }
    }
}
