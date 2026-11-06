using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Ray-marched volumetric cloud layer, drawn after the skybox and before transparents:
    /// 1. trace at 1/<see cref="AeroCloudQuality.downsample"/> resolution with per-frame jitter,
    /// 2. temporal accumulation into a per-camera history (reprojected at the layer, variance-clamped),
    /// 3. full-resolution composite that hides the layer behind nearer geometry.
    /// When the camera is inside or above the layer (the top tower floors), rays that look down are composited
    /// after transparents instead, so the cloud lies over the ocean and other water below.
    /// The look comes from <see cref="AeroVolumetricClouds"/> in the camera's scene.
    /// </summary>
    public sealed class AeroVolumetricCloudsRendererFeature : ScriptableRendererFeature
    {
        public Shader cloudShader;
        public AeroCloudQuality quality;
        public Texture3D shapeNoise;
        public Texture3D detailNoise;
        public Texture2D coverageMap;

        [Header("Scale (metres per noise tile at cloud size 1)")]
        [Min(100)] public float shapeTile = 1200;
        [Min(10)] public float detailTile = 200;
        [Min(500)] public float coverageTile = 7000;
        [Tooltip("Extinction per metre at density 1.")]
        [Range(.001f, .1f)] public float extinction = .009f;

        [Header("Exposure")]
        [Tooltip("Scales direct sun/moon light on the clouds. The scattering model is far brighter than the sky, so " +
                 "at 1 every sunlit cloud clips to one flat white; lower keeps shaded bellies and lit billows apart.")]
        [Range(.05f, 2)] public float sunExposure = DefaultSunExposure;
        [Tooltip("Scales sky light (the blue fill in shadowed sides and bases).")]
        [Range(0, 3)] public float skyExposure = DefaultSkyExposure;

        public const float DefaultSunExposure = .45f, DefaultSkyExposure = 1.1f;

        private Material material;
        private CloudPass pass;
        private LateCompositePass latePass;

        /// <summary>Per-cloud height noise repeats this many times per coverage tile (~1 km patches at size 1).</summary>
        public const float HeightNoiseFrequency = 6;

        public const int TracePass = 0, TemporalPass = 1, CompositePass = 2;
        /// <summary>Distinct ray-march jitter patterns before the sequence repeats.</summary>
        public const int JitterCycle = 64;
        public bool Ready => material != null && shapeNoise != null && detailNoise != null && coverageMap != null && quality != null;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            material = cloudShader != null ? CoreUtils.CreateEngineMaterial(cloudShader) : null;
            pass?.Dispose();
            pass = new CloudPass(this) { renderPassEvent = RenderPassEvent.AfterRenderingSkybox };
            pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            latePass = new LateCompositePass(pass) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            latePass.ConfigureInput(ScriptableRenderPassInput.Depth);
        }

        /// <summary>True when the camera is inside or above the cloud base, so downward rays cross the layer
        /// before reaching water.</summary>
        public static bool NeedsLateComposite(Camera camera, AeroVolumetricClouds clouds) =>
            camera != null && clouds != null && camera.transform.position.y > clouds.Look.baseAltitude;

        public static bool SupportsCamera(Camera camera) =>
            camera != null && (camera.cameraType == CameraType.Game || camera.cameraType == CameraType.SceneView) && !camera.orthographic;

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            var camera = data.cameraData.camera;
            if (!Ready || !SupportsCamera(camera) || data.cameraData.renderType != CameraRenderType.Base) return;
            var clouds = AeroVolumetricClouds.ForCamera(camera);
            if (clouds == null || !clouds.Visible) return;
            renderer.EnqueuePass(pass);
            if (NeedsLateComposite(camera, clouds)) renderer.EnqueuePass(latePass);
        }

        protected override void Dispose(bool disposing)
        {
            pass?.Dispose(); pass = null; latePass = null;
            CoreUtils.Destroy(material); material = null;
        }

        /// <summary>Shader constants for one camera and look. Public for tests.</summary>
        public static void FillConstants(MaterialPropertyBlock p, AeroVolumetricClouds clouds, AeroCloudQuality quality,
            float shapeTile, float detailTile, float coverageTile, float extinction,
            float sunExposure = DefaultSunExposure, float skyExposure = DefaultSkyExposure)
        {
            var l = clouds.Look;
            float size = Mathf.Max(.25f, l.cloudSize);
            // The shell reaches the highest cloud: base + variation + thickness.
            p.SetVector(Ids.Layer, new Vector4(l.baseAltitude, l.baseAltitude + l.heightVariation + l.thickness, clouds.PlanetRadius, l.thickness));
            p.SetVector(Ids.Heights, new Vector4(l.heightVariation, HeightNoiseFrequency, 0, 0));
            p.SetVector(Ids.Shape, new Vector4(l.coverage, l.density * extinction, 1 / (shapeTile * size), 1 / (detailTile * Mathf.Sqrt(size))));
            p.SetVector(Ids.Shape2, new Vector4(l.erosion, l.cloudType, 1 / (coverageTile * size), Mathf.Max(.02f, l.edgeSoftness)));
            var offset = clouds.WindOffset;
            p.SetVector(Ids.Wind, new Vector4(offset.x, offset.y, offset.z, l.shear));
            var dir = l.WindVector.sqrMagnitude > 1e-6f ? l.WindVector.normalized : Vector2.up;
            p.SetVector(Ids.WindDir, new Vector4(dir.x, dir.y, 0, 0));
            p.SetVector(Ids.Lighting, new Vector4(l.powder, l.silverLining, l.multiScatter, l.darkness));
            p.SetVector(Ids.Haze, new Vector4(l.hazeDistance, l.hazeStrength, l.fadeDistance * .6f, l.fadeDistance));
            p.SetVector(Ids.SunDir, clouds.LightDirection.normalized);
            p.SetVector(Ids.SunColor, (Vector4)clouds.LightColor);
            p.SetVector(Ids.AmbientTop, (Vector4)clouds.AmbientTop);
            p.SetVector(Ids.AmbientBottom, (Vector4)clouds.AmbientBottom);
            p.SetVector(Ids.FogColor, (Vector4)clouds.FogColor);
            p.SetVector(Ids.March, new Vector4(quality.primarySteps, quality.lightSteps, Time.frameCount % JitterCycle, quality.maxStep));
            p.SetVector(Ids.Tone, new Vector4(sunExposure, skyExposure, 0, 0));
        }

        private static class Ids
        {
            public static readonly int Layer = Shader.PropertyToID("_AeroCloudLayer"), Shape = Shader.PropertyToID("_AeroCloudShape"),
                Shape2 = Shader.PropertyToID("_AeroCloudShape2"), Wind = Shader.PropertyToID("_AeroCloudWind"),
                WindDir = Shader.PropertyToID("_AeroCloudWindDir"), Lighting = Shader.PropertyToID("_AeroCloudLighting"),
                March = Shader.PropertyToID("_AeroCloudMarch"), Haze = Shader.PropertyToID("_AeroCloudHaze"),
                SunDir = Shader.PropertyToID("_AeroCloudSunDir"), SunColor = Shader.PropertyToID("_AeroCloudSunColor"),
                AmbientTop = Shader.PropertyToID("_AeroCloudAmbientTop"), AmbientBottom = Shader.PropertyToID("_AeroCloudAmbientBottom"),
                FogColor = Shader.PropertyToID("_AeroCloudFogColor"), Camera = Shader.PropertyToID("_AeroCloudCamera"),
                Forward = Shader.PropertyToID("_AeroCloudForward"), Texel = Shader.PropertyToID("_AeroCloudTexel"),
                DepthTexel = Shader.PropertyToID("_AeroCloudDepthTexel"), Temporal = Shader.PropertyToID("_AeroCloudTemporal"),
                PrevView = Shader.PropertyToID("_AeroCloudPrevView"), Composite = Shader.PropertyToID("_AeroCloudComposite"), Heights = Shader.PropertyToID("_AeroCloudHeights"),
                Tone = Shader.PropertyToID("_AeroCloudTone"),
                ShapeNoise = Shader.PropertyToID("_AeroCloudShapeNoise"), DetailNoise = Shader.PropertyToID("_AeroCloudDetailNoise"),
                Coverage = Shader.PropertyToID("_AeroCloudCoverage"), Depth = Shader.PropertyToID("_AeroCloudDepth"),
                History = Shader.PropertyToID("_AeroCloudHistory"), BlitTexture = Shader.PropertyToID("_BlitTexture"),
                BlitScaleBias = Shader.PropertyToID("_BlitScaleBias");
        }

        private sealed class History
        {
            public readonly RTHandle[] targets = new RTHandle[2];
            public int write;
            public bool valid;
            public Matrix4x4 view;
            public int width, height;
            public void Release() { for (int i = 0; i < 2; i++) { targets[i]?.Release(); targets[i] = null; } valid = false; }
        }

        private sealed class PassData
        {
            public Material material;
            public MaterialPropertyBlock props;
            public int shaderPass;
            public TextureHandle source, depth, history;
            public Texture3D shape, detail;
            public Texture2D coverage;
        }

        /// <summary>This frame's cloud result, handed from the main pass to the late composite.</summary>
        private sealed class CloudFrameData : ContextItem
        {
            public TextureHandle result;
            public Camera camera;
            public override void Reset() { result = TextureHandle.nullHandle; camera = null; }
        }

        private sealed class LateCompositePass : ScriptableRenderPass
        {
            private readonly CloudPass main;
            public LateCompositePass(CloudPass main) { this.main = main; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData) => main.RecordLateComposite(graph, frameData);
        }

        private sealed class CloudPass : ScriptableRenderPass, IDisposable
        {
            private readonly AeroVolumetricCloudsRendererFeature owner;
            private readonly MaterialPropertyBlock[] blocks = { new(), new(), new(), new() };
            private readonly Dictionary<Camera, History> histories = new();
            private readonly List<Camera> stale = new();
            private readonly ProfilingSampler traceSampler = new("Aero cloud trace"), temporalSampler = new("Aero cloud temporal"),
                compositeSampler = new("Aero cloud composite"), lateSampler = new("Aero cloud composite over water");

            private readonly Dictionary<Camera, int> jitterFrames = new();

            public CloudPass(AeroVolumetricCloudsRendererFeature owner) { this.owner = owner; }

            private int NextJitterFrame(Camera camera)
            {
                if (jitterFrames.Count > 16)
                {
                    stale.Clear();
                    foreach (var key in jitterFrames.Keys) if (key == null) stale.Add(key);
                    foreach (var key in stale) jitterFrames.Remove(key);
                }
                jitterFrames.TryGetValue(camera, out int frame);
                jitterFrames[camera] = (frame + 1) % JitterCycle;
                return frame;
            }

            public void Dispose()
            {
                foreach (var h in histories.Values) h.Release();
                histories.Clear(); jitterFrames.Clear();
            }

            private History HistoryFor(Camera camera, int width, int height)
            {
                stale.Clear();
                foreach (var key in histories.Keys) if (key == null) stale.Add(key);
                foreach (var key in stale) { histories[key].Release(); histories.Remove(key); }
                if (!histories.TryGetValue(camera, out var history)) histories[camera] = history = new History();
                if (history.targets[0] == null || history.width != width || history.height != height)
                {
                    history.Release();
                    for (int i = 0; i < 2; i++)
                        history.targets[i] = RTHandles.Alloc(width, height, colorFormat: GraphicsFormat.R16G16B16A16_SFloat,
                            filterMode: FilterMode.Bilinear, wrapMode: TextureWrapMode.Clamp, name: $"Aero cloud history {i}");
                    history.width = width; history.height = height;
                }
                return history;
            }

            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                var cameraData = frameData.Get<UniversalCameraData>();
                var camera = cameraData.camera;
                var clouds = AeroVolumetricClouds.ForCamera(camera);
                if (clouds == null || !clouds.Visible || !resources.cameraDepthTexture.IsValid()) return;
                var quality = owner.quality;
                var color = resources.activeColorTexture.GetDescriptor(graph);
                int down = quality.Downsample;
                int width = Mathf.Max(1, (color.width + down - 1) / down), height = Mathf.Max(1, (color.height + down - 1) / down);

                // Rays use URP's bound matrices (UNITY_MATRIX_I_VP / UNITY_MATRIX_P) so they always match the depth
                // texture's orientation; only last frame's view matrix comes from here.
                var view = camera.worldToCameraMatrix;
                Vector3 position = camera.transform.position;

                var constants = blocks[0];
                FillConstants(constants, clouds, quality, owner.shapeTile, owner.detailTile, owner.coverageTile, owner.extinction, owner.sunExposure, owner.skyExposure);
                // A new jitter pattern every traced frame, per camera: Time.frameCount stands still between several
                // renders in one frame (editor captures, extra cameras), which froze the step slices into bands.
                var march = constants.GetVector(Ids.March); march.z = NextJitterFrame(camera); constants.SetVector(Ids.March, march);
                constants.SetVector(Ids.Camera, new Vector4(position.x, position.y, position.z, Mathf.Max(1, position.y)));
                constants.SetVector(Ids.Forward, camera.transform.forward);
                constants.SetVector(Ids.Texel, new Vector4(1f / width, 1f / height, width, height));
                constants.SetVector(Ids.DepthTexel, new Vector4(1f / color.width, 1f / color.height, down, 0));
                constants.SetVector(Ids.BlitScaleBias, new Vector4(1, 1, 0, 0));
                // The temporal and composite passes share every constant.
                for (int i = 1; i < blocks.Length; i++) blocks[i].Clear();

                var descriptor = new TextureDesc(width, height)
                {
                    name = "Aero cloud trace", colorFormat = GraphicsFormat.R16G16B16A16_SFloat, filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp, clearBuffer = false, msaaSamples = MSAASamples.None
                };
                var trace = graph.CreateTexture(descriptor);
                AddPass(graph, "Aero cloud trace", traceSampler, TracePass, constants, trace, TextureHandle.nullHandle, resources.cameraDepthTexture, TextureHandle.nullHandle, false);

                var result = trace;
                if (quality.temporal)
                {
                    var history = HistoryFor(camera, width, height);
                    int read = 1 - history.write;
                    var previous = graph.ImportTexture(history.targets[read]);
                    var next = graph.ImportTexture(history.targets[history.write]);
                    var temporal = blocks[1];
                    CopyConstants(constants, temporal);
                    temporal.SetMatrix(Ids.PrevView, history.valid ? history.view : view);
                    temporal.SetVector(Ids.Temporal, new Vector4(history.valid ? quality.temporalBlend : 0, 0, 0, 0));
                    AddPass(graph, "Aero cloud temporal", temporalSampler, TemporalPass, temporal, next, trace, TextureHandle.nullHandle, previous, false);
                    history.view = view; history.valid = true; history.write = read;
                    result = next;
                }

                var composite = blocks[2];
                CopyConstants(constants, composite);
                composite.SetVector(Ids.Composite, Vector4.zero);
                AddPass(graph, "Aero cloud composite", compositeSampler, CompositePass, composite, resources.activeColorTexture, result, resources.cameraDepthTexture, TextureHandle.nullHandle, true);

                var late = blocks[3];
                CopyConstants(constants, late);
                late.SetVector(Ids.Composite, new Vector4(1, 0, 0, 0));
                var handoff = frameData.Contains<CloudFrameData>() ? frameData.Get<CloudFrameData>() : frameData.Create<CloudFrameData>();
                handoff.result = result; handoff.camera = camera;
            }

            /// <summary>Downward rays from inside or above the layer, composited after water and glass.</summary>
            public void RecordLateComposite(RenderGraph graph, ContextContainer frameData)
            {
                if (!frameData.Contains<CloudFrameData>()) return;
                var handoff = frameData.Get<CloudFrameData>();
                var camera = frameData.Get<UniversalCameraData>().camera;
                var resources = frameData.Get<UniversalResourceData>();
                if (handoff.camera != camera || !handoff.result.IsValid() || !resources.cameraDepthTexture.IsValid()) return;
                AddPass(graph, "Aero cloud composite over water", lateSampler, CompositePass, blocks[3], resources.activeColorTexture, handoff.result, resources.cameraDepthTexture, TextureHandle.nullHandle, true);
                handoff.Reset();
            }

            private static readonly int[] SharedVectors =
            {
                Ids.Layer, Ids.Shape, Ids.Shape2, Ids.Wind, Ids.WindDir, Ids.Lighting, Ids.Haze, Ids.SunDir, Ids.SunColor,
                Ids.AmbientTop, Ids.AmbientBottom, Ids.FogColor, Ids.March, Ids.Camera, Ids.Forward, Ids.Texel, Ids.DepthTexel, Ids.BlitScaleBias,
                Ids.Composite, Ids.Heights, Ids.Tone
            };

            private static void CopyConstants(MaterialPropertyBlock from, MaterialPropertyBlock to)
            {
                foreach (int id in SharedVectors) to.SetVector(id, from.GetVector(id));
            }

            private void AddPass(RenderGraph graph, string name, ProfilingSampler sampler, int shaderPass, MaterialPropertyBlock props,
                TextureHandle target, TextureHandle source, TextureHandle depth, TextureHandle history, bool blendIntoTarget)
            {
                using var builder = graph.AddRasterRenderPass<PassData>(name, out var d, sampler);
                d.material = owner.material; d.props = props; d.shaderPass = shaderPass;
                d.source = source; d.depth = depth; d.history = history;
                d.shape = owner.shapeNoise; d.detail = owner.detailNoise; d.coverage = owner.coverageMap;
                if (source.IsValid()) builder.UseTexture(source, AccessFlags.Read);
                if (depth.IsValid()) builder.UseTexture(depth, AccessFlags.Read);
                if (history.IsValid()) builder.UseTexture(history, AccessFlags.Read);
                builder.SetRenderAttachment(target, 0, blendIntoTarget ? AccessFlags.ReadWrite : AccessFlags.WriteAll);
                builder.SetRenderFunc(static (PassData data, RasterGraphContext ctx) =>
                {
                    var p = data.props;
                    p.SetTexture(Ids.ShapeNoise, data.shape); p.SetTexture(Ids.DetailNoise, data.detail); p.SetTexture(Ids.Coverage, data.coverage);
                    if (data.source.IsValid()) p.SetTexture(Ids.BlitTexture, (Texture)data.source);
                    if (data.depth.IsValid()) p.SetTexture(Ids.Depth, (Texture)data.depth);
                    if (data.history.IsValid()) p.SetTexture(Ids.History, (Texture)data.history);
                    ctx.cmd.DrawProcedural(Matrix4x4.identity, data.material, data.shaderPass, MeshTopology.Triangles, 3, 1, p);
                });
            }
        }
    }
}
