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
        public int LastBackgroundWidth { get; private set; }
        public int LastBackgroundHeight { get; private set; }
        // Surface geometry is shared with the tile renderer. Never expand a tower into CPU meshes.
        public int CachedGeometryCount => 0;
        public int LastSubmittedInstances { get; private set; }
        public int LastDetailedFloors { get; private set; }
        private Material material;
        private GlassPass pass;

        public override void Create()
        {
            pass?.Dispose();
            CoreUtils.Destroy(material);
            material = surfaceShader != null ? CoreUtils.CreateEngineMaterial(surfaceShader) : null;
            if (material != null) material.enableInstancing = true;
            pass = new GlassPass(this) { renderPassEvent = RenderPassEvent.BeforeRenderingTransparents };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Color);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            LastDrawCalls = LastRefractedLayers = LastBackgroundWidth = LastBackgroundHeight = LastSubmittedInstances = LastDetailedFloors = 0;
            LastStatus = "Camera rejected";
            if (material != null && profile != null && renderingData.cameraData.renderType == CameraRenderType.Base &&
                AeroLightRendererFeature.SupportsCamera(renderingData.cameraData.camera)) { LastStatus = "Queued"; renderer.EnqueuePass(pass); }
        }
        protected override void Dispose(bool disposing) { pass?.Dispose(); pass=null; CoreUtils.Destroy(material); material = null; }

        public static float RefractionWeight(float distance, float nextDistance, float floorHeight) =>
            (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(24, 42, distance))) *
            Mathf.Clamp01((nextDistance - distance) / Mathf.Max(1, floorHeight));

        private sealed class GlassPass : ScriptableRenderPass
        {
            private readonly RiverGlassRendererFeature owner;
            private readonly List<RiverGlassDraw> draws = new();
            private readonly List<FloorGroup> floors = new();
            private readonly Dictionary<int,FloorGroup> floorPool = new();
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
                public List<RiverGlassDraw> draws;
                public float weight, clock;
                public bool detailed;
            }
            public GlassPass(RiverGlassRendererFeature owner) { this.owner = owner; requiresIntermediateTexture = true; }
            public void Dispose()
            {
                floorPool.Clear(); floors.Clear();
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var camera = frameData.Get<UniversalCameraData>().camera;
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer || !resources.cameraDepthTexture.IsValid())
                { owner.LastStatus = resources.isActiveTargetBackBuffer ? "Backbuffer" : "Missing depth"; return; }
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                RiverGlassRenderRegistry.Collect(camera, planes, draws);
                owner.LastStatus = "Compositing shared instanced water and glass";
                foreach(var group in floors)group.draws.Clear();
                floors.Clear();
                foreach (var draw in draws)
                {
                    if(draw.mesh==null || draw.count<=0)continue;
                    if(!floorPool.TryGetValue(draw.floor,out var floor))
                    { floor=new FloorGroup {floor=draw.floor};floorPool.Add(draw.floor,floor); }
                    if(floor.draws.Count==0)
                    { floor.distance=Mathf.Abs(camera.transform.position.y-draw.height); floors.Add(floor); }
                    floor.draws.Add(draw);
                    owner.LastSubmittedInstances+=draw.count;
                }
                floors.Sort(static (a, b) => a.distance.CompareTo(b.distance));
                owner.profile.Apply(owner.material);
                for (int i = floors.Count - 1; i >= 0; i--)
                {
                    FloorGroup floor = floors[i];
                    bool detailed=i<(owner.reducedRefraction?1:Mathf.Clamp(owner.profile.detailedFloorLimit,1,3));
                    if(detailed)owner.LastDetailedFloors++;
                    float weight = i==0 ? 1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(owner.profile.refractionDistance*.5f,
                        owner.profile.refractionDistance,floor.distance)) : 0;
                    if (owner.reducedRefraction) weight *= .35f;
                    TextureHandle background = default;
                    if (weight > .001f)
                    {
                        var description = graph.GetTextureDesc(resources.activeColorTexture);
                        description.name = "River glass accumulated background";
                        description.width = Mathf.Max(1,description.width/2);
                        description.height = Mathf.Max(1,description.height/2);
                        description.filterMode = FilterMode.Bilinear;
                        description.clearBuffer = false;
                        description.msaaSamples = MSAASamples.None;
                        background = graph.CreateTexture(description);
                        graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(resources.activeColorTexture,
                            background, Blitter.GetBlitMaterial(TextureDimension.Tex2D), 0), passName: "River glass background copy");
                        owner.LastRefractedLayers++;
                        owner.LastBackgroundWidth=description.width; owner.LastBackgroundHeight=description.height;
                    }
                    using var builder = graph.AddRasterRenderPass<PassData>("River glass floor composition", out var data);
                    data.background = background; data.depth = resources.cameraDepthTexture;
                    data.material = owner.material; data.draws = floor.draws; data.weight = weight;
                    data.detailed=detailed;
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
                            draw.properties.SetFloat("_RiverDetailWeight",d.detailed?1:0);
                            draw.properties.SetFloat("_RadialFloor",draw.radial);
                            context.cmd.DrawMeshInstanced(draw.mesh, 0, d.material, 0, draw.matrices, draw.count, draw.properties);
                        }
                        if(d.detailed)foreach(var draw in d.draws)
                            context.cmd.DrawMeshInstanced(draw.mesh,0,d.material,1,draw.matrices,draw.count,draw.properties);
                    });
                    owner.LastDrawCalls += data.draws.Count*(detailed?2:1);
                }
            }
        }
    }
}
