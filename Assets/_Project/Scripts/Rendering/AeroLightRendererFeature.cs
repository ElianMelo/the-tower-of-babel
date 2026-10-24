using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>Depth-projected water light restricted to registered lake footprints.</summary>
    public sealed class AeroLightRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class LightSettings
        {
            [Min(1)] public float distance = 42;
            [Min(0)] public float fadeStart = 24;
            [Range(0, 3)] public float causticIntensity = .7f;
            [Range(0, 1)] public float reflectedLight = .18f;
            [Min(.01f)] public float scale = .2f;
            [Range(0, .3f)] public float speed = .055f;
            public Color tint = new(.38f, .85f, 1);

            public float WeightAt(float metres) => 1 - Mathf.SmoothStep(0, 1,
                Mathf.InverseLerp(Mathf.Clamp(fadeStart, 0, Mathf.Max(1, distance) - .01f), Mathf.Max(1, distance), metres));
        }

        public Shader lightShader;
        public Texture2D caustics;
        public Cubemap environment;
        public LightSettings settings = new();
        [NonSerialized] public float previewTime = -1;
        private Material material;
        private LightPass pass;

        public static bool SupportsCamera(Camera camera) => camera != null &&
            (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView);

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = lightShader != null ? CoreUtils.CreateEngineMaterial(lightShader) : null;
            pass = new LightPass(this) { renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingOpaques + 2) };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (material != null && caustics != null && SupportsCamera(renderingData.cameraData.camera) &&
                renderingData.cameraData.renderType == CameraRenderType.Base &&
                AeroWaterLightRegion.HasNearbyRegion(renderingData.cameraData.camera, settings.distance) &&
                (settings.causticIntensity > 0 || settings.reflectedLight > 0))
                renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; }

        private sealed class LightPass : ScriptableRenderPass
        {
            private readonly AeroLightRendererFeature owner;
            private readonly ProfilingSampler sampler = new("Aero nearby water light");
            private static readonly int Pattern = Shader.PropertyToID("_AeroCaustics");
            private static readonly int Range = Shader.PropertyToID("_AeroRange");
            private static readonly int Motion = Shader.PropertyToID("_AeroMotion");
            private static readonly int Tint = Shader.PropertyToID("_AeroTint");
            private static readonly int Environment = Shader.PropertyToID("_AeroEnvironment");
            private sealed class PassData
            {
                public TextureHandle depth;
                public Material material;
                public Texture2D pattern;
                public Cubemap environment;
                public Vector4 range, motion;
                public Color tint;
                public int regionCount;
                public readonly Vector4[] centers = new Vector4[AeroWaterLightRegion.MaximumRegions];
                public readonly Vector4[] shapes = new Vector4[AeroWaterLightRegion.MaximumRegions];
                public readonly Vector4[] limits = new Vector4[AeroWaterLightRegion.MaximumRegions];
            }
            public LightPass(AeroLightRendererFeature owner) { this.owner = owner; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (!resources.cameraDepthTexture.IsValid()) return;
                var camera = frameData.Get<UniversalCameraData>().camera;
                var s = owner.settings;
                using var builder = graph.AddRasterRenderPass<PassData>("Aero nearby water light", out var data, sampler);
                data.depth = resources.cameraDepthTexture;
                data.material = owner.material;
                data.pattern = owner.caustics;
                data.environment = owner.environment;
                data.range = new Vector4(Mathf.Max(1, s.distance), Mathf.Clamp(s.fadeStart, 0, Mathf.Max(1, s.distance) - .01f), s.causticIntensity, s.reflectedLight);
                data.motion = new Vector4(s.scale, s.speed, owner.previewTime, 0);
                data.tint = s.tint;
                data.regionCount = AeroWaterLightRegion.CopyTo(camera, data.centers, data.shapes, data.limits);
                builder.UseTexture(data.depth, AccessFlags.Read);
                // This is a multiplicative/additive lighting overlay. Preserve and blend with the existing
                // attachment, without a color copy or intermediate texture (the generic blit declares Write).
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                {
                    d.material.SetTexture(Pattern, d.pattern);
                    d.material.SetTexture(Environment, d.environment != null ? d.environment : CoreUtils.blackCubeTexture);
                    d.material.SetVector(Range, d.range);
                    d.material.SetVector(Motion, d.motion);
                    d.material.SetColor(Tint, d.tint);
                    d.material.SetInt("_AeroRegionCount", d.regionCount);
                    d.material.SetVectorArray("_AeroRegionCenters", d.centers);
                    d.material.SetVectorArray("_AeroRegionShapes", d.shapes);
                    d.material.SetVectorArray("_AeroRegionLimits", d.limits);
                    // Blitter binds the graph-tracked depth explicitly as _BlitTexture.
                    Blitter.BlitTexture(context.cmd, d.depth, new Vector4(1, 1, 0, 0), d.material, 0);
                });
            }
        }
    }
}
