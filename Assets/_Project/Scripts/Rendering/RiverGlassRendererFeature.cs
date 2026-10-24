using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>Composites enclosed water from far floors to near floors against accumulated scene color.</summary>
    public sealed class RiverGlassRendererFeature : ScriptableRendererFeature
    {
        public RiverGlassProfile profile;
        public Shader surfaceShader;
        public bool reducedRefraction;
        [System.NonSerialized] public float previewTime = -1;
        public int LastDrawCalls { get; private set; }
        public int LastRefractedLayers { get; private set; }
        public string LastStatus { get; private set; }
        private Material material;
        private GlassPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = surfaceShader != null ? CoreUtils.CreateEngineMaterial(surfaceShader) : null;
            if (material != null) material.enableInstancing = true;
            pass = new GlassPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            LastDrawCalls = LastRefractedLayers = 0;
            LastStatus = "Camera rejected";
            if (material != null && profile != null && renderingData.cameraData.renderType == CameraRenderType.Base &&
                AeroLightRendererFeature.SupportsCamera(renderingData.cameraData.camera)) { LastStatus = "Queued"; renderer.EnqueuePass(pass); }
        }
        protected override void Dispose(bool disposing) { CoreUtils.Destroy(material); material = null; }

        public static float RefractionWeight(float distance, float nextDistance, float floorHeight) =>
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(24, 42, distance))) *
            Mathf.Clamp01((nextDistance - distance) / Mathf.Max(1, floorHeight));

        private sealed class GlassPass : ScriptableRenderPass
        {
            private readonly RiverGlassRendererFeature owner;
            private readonly List<RiverGlassDraw> draws = new();
            private readonly List<FloorGroup> floors = new();
            private readonly Plane[] planes = new Plane[6];
            private static readonly int Background = Shader.PropertyToID("_RiverBackground");
            private static readonly int Clock = Shader.PropertyToID("_RiverClock");
            private sealed class FloorGroup
            {
                public int floor;
                public float distance;
                public readonly List<RiverGlassDraw> draws = new();
            }
            private sealed class PassData
            {
                public TextureHandle background;
                public TextureHandle depth;
                public Material material;
                public RiverGlassDraw[] draws;
                public float weight, clock;
            }
            public GlassPass(RiverGlassRendererFeature owner) { this.owner = owner; requiresIntermediateTexture = true; }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>().camera;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())
                { owner.LastStatus = resources.isActiveTargetBackBuffer ? "Backbuffer" : "Missing depth"; return; }
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                RiverGlassRenderRegistry.Collect(camera, planes, draws);
                owner.LastStatus = $"Collected {draws.Count} draws";
                floors.Clear();
                foreach (var draw in draws)
                {
                    FloorGroup floor = floors.Find(f => f.floor == draw.floor);
                    if (floor == null)
                    {
                        floor = new FloorGroup { floor = draw.floor, distance = Mathf.Abs(camera.transform.position.y - draw.height) };
                        floors.Add(floor);
                    }
                    floor.draws.Add(draw);
                }
                floors.Sort((a, b) => a.distance.CompareTo(b.distance));
                owner.profile.Apply(owner.material);
                for (int i = floors.Count - 1; i >= 0; i--)
                {
                    FloorGroup floor = floors[i];
                    float next = floors.Count > 3 ? floors[3].distance : 10000;
                    float weight = i < 3 ? RefractionWeight(floor.distance, next, owner.profile.layout.z) : 0;
                    if (owner.reducedRefraction) weight *= .35f;
                    TextureHandle background = default;
                    if (weight > .001f)
                    {
                        var description = graph.GetTextureDesc(resources.activeColorTexture);
                        description.name = "River glass accumulated background";
                        description.clearBuffer = false;
                        description.msaaSamples = MSAASamples.None;
                        background = graph.CreateTexture(description);
                        graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(resources.activeColorTexture,
                            background, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0), passName: "River glass background copy");
                        owner.LastRefractedLayers++;
                    }
                    using var builder = graph.AddRasterRenderPass<PassData>("River glass floor composition", out var data);
                    data.background = background; data.depth = resources.cameraDepthTexture;
                    data.material = owner.material; data.draws = floor.draws.ToArray(); data.weight = weight;
                    data.clock = owner.previewTime >= 0 ? owner.previewTime : Time.time;
                    if (background.IsValid()) builder.UseTexture(background, AccessFlags.Read);
                    builder.UseTexture(data.depth, AccessFlags.Read);
                    if (resources.mainShadowsTexture.IsValid()) builder.UseTexture(resources.mainShadowsTexture, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
                    {
                        if (d.background.IsValid()) context.cmd.SetGlobalTexture(Background, d.background);
                        context.cmd.SetGlobalFloat(Clock, d.clock);
                        foreach (var draw in d.draws)
                        {
                            draw.properties.SetFloat("_RiverRefractionWeight", d.weight);
                            context.cmd.DrawMeshInstanced(draw.mesh, 0, d.material, 0, draw.matrices, draw.count, draw.properties);
                        }
                    });
                    owner.LastDrawCalls += data.draws.Length;
                }
            }
        }
    }
}
