#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Manually invoked Edit Mode art review and benchmark. Never changes saved construction.</summary>
public static class AeroBuildingFinishReview
{
    public const string Folder = "Documentation/FrutigerAero/Buildings/Finish";
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static Session benchmark;
    private static int frame;
    private static readonly List<double>[] cpu = { new(), new(), new() }, gpu = { new(), new(), new() };
    private static CustomSampler sampler;
    private static Recorder recorder;
    private static CommandBuffer envelope;
    private static bool previousProfiler, previousGpu;
    private static bool collectGpu;
    public static string Status { get; private set; } = "Idle";

    private sealed class Session : IDisposable
    {
        private readonly Scene original, scene;
        private readonly Light[] lights;
        private readonly bool[] lightStates;
        private readonly bool oldAsync;
        private readonly RenderTexture oldTarget;
        private readonly float oldDay, oldRiverClock, oldRayClock;
        private readonly RiverGlassRendererFeature river;
        private readonly AeroSunShaftRendererFeature rays;
        private readonly AeroDayNightCycle cycle;
        private readonly AeroTowerAtmosphere atmosphere;
        private readonly TowerAtmosphereBridge shadows;
        private readonly RiverGlassLighting caustics;
        private readonly Action tick;
        private readonly Texture2D pixels;
        public readonly Camera Camera;
        public readonly AeroBuildingFinish Finish;
        public readonly InstancedFarChunkRenderer Far;
        public readonly RenderTexture Target;
        public readonly GameObject Marker;
        public int Floor;
        public int TotalAssets;
        public readonly ChunkManager Manager;
        public Session(int floor)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
            original = SceneManager.GetActiveScene();
            if (original.path != "Assets/_Project/Scenes/Tower.unity") throw new InvalidOperationException("Start from the Tower scene.");
            var source = Object.FindFirstObjectByType<ChunkManager>();
            var sourceAtmosphere = Object.FindFirstObjectByType<AeroTowerAtmosphere>();
            var sourceSky = RenderSettings.skybox;
            var profile = AssetDatabase.LoadAssetAtPath<AeroBuildingFinishProfile>(AeroBuildingFinishAuthoring.ProfilePath);
            lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None); lightStates = lights.Select(l => l.enabled).ToArray();
            oldAsync = ShaderUtil.allowAsyncCompilation; oldTarget = RenderTexture.active; oldDay = Shader.GetGlobalFloat("_AeroDaylight");
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            river = renderer.rendererFeatures.OfType<RiverGlassRendererFeature>().Single(); oldRiverClock = river.previewTime;
            rays = renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single(); oldRayClock = rays.previewTime;
            foreach (var light in lights) light.enabled = false;
            ShaderUtil.allowAsyncCompilation = false;
            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = sourceSky;
            var root = new GameObject("Disposable building finish review") { layer = 31 }; root.SetActive(false);
            var manager = root.AddComponent<ChunkManager>();
            Manager = manager;
            foreach (TowerAssetType type in Enum.GetValues(typeof(TowerAssetType))) manager.AssetPrefabs.SetPrefab(type, source.AssetPrefabs.GetPrefab(type));
            var data = source.CachedChunks.Select(chunk => (ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache), Private, null,
                new object[] {chunk.Key, chunk.Assets.Select(a => new ChunkAssetData(a.LocalIndex,a.AssetType,a.Position,a.Rotation,a.Scale,10)).ToList()}, null)).ToList();
            TotalAssets = data.Sum(c => c.Assets.Count);
            typeof(ChunkManager).GetField("cachedChunks", Private).SetValue(manager, data);
            typeof(ChunkManager).GetField("radialFloors", Private).SetValue(manager, source.RadialFloors);
            typeof(ChunkManager).GetField("floorCenter", Private).SetValue(manager, source.FloorCenter);
            typeof(ChunkManager).GetMethod("RebuildChunkLookup", Private).Invoke(manager,null);
            Far = root.AddComponent<InstancedFarChunkRenderer>();
            var deck = root.AddComponent<TowerBuildingFinishBridge>(); deck.chunks = manager;
            Finish = root.AddComponent<AeroBuildingFinish>(); Finish.profile = profile;
            Target = new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf) { name = "Building finish art review" }; Target.Create();
            pixels = new Texture2D(1920,1080,TextureFormat.RGB24,false);
            var cameraRoot = new GameObject("Finish review camera",typeof(Camera)); Camera = cameraRoot.GetComponent<Camera>();
            Camera.enabled = false; Camera.scene = scene; Camera.targetTexture = Target; Camera.cullingMask = 1 << 31;
            Camera.fieldOfView = 68; Camera.nearClipPlane = .05f; Camera.farClipPlane = 350; Camera.allowHDR = true;
            Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.45f,.66f,.85f);
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            Camera.GetUniversalAdditionalCameraData().volumeLayerMask = 1 << 31;
            typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(Far, Camera);
            tick = (Action)Delegate.CreateDelegate(typeof(Action),Far,typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate",Private));
            var sun = new GameObject("Review sun",typeof(Light)).GetComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft; sun.cullingMask = 1 << 31;
            var moon = new GameObject("Review moon",typeof(Light)).GetComponent<Light>(); moon.type = LightType.Directional; moon.shadows = LightShadows.Soft; moon.cullingMask = 1 << 31;
            cycle = root.AddComponent<AeroDayNightCycle>(); cycle.profile = sourceAtmosphere.cycle.profile; cycle.sun = sun; cycle.moon = moon;
            atmosphere = root.AddComponent<AeroTowerAtmosphere>(); atmosphere.cycle = cycle; atmosphere.raySettings = sourceAtmosphere.raySettings;
            atmosphere.center = sourceAtmosphere.center; atmosphere.baseRadius = sourceAtmosphere.baseRadius; atmosphere.decrease = sourceAtmosphere.decrease;
            atmosphere.floorHeight = sourceAtmosphere.floorHeight; atmosphere.levels = sourceAtmosphere.levels; atmosphere.bounceLights = new Light[2];
            for(int i=0;i<2;i++)
            {
                var light = new GameObject("Review fill "+i,typeof(Light)).GetComponent<Light>(); light.cullingMask = 1 << 31; light.enabled = false; atmosphere.bounceLights[i] = light;
            }
            shadows = root.AddComponent<TowerAtmosphereBridge>(); shadows.chunks = manager; shadows.atmosphere = atmosphere;
            caustics = root.AddComponent<RiverGlassLighting>(); caustics.profile = river.profile; caustics.cookieShader = Shader.Find("Hidden/TowerOfBabel/River caustic cookie");
            var volumeRoot = new GameObject("Review grading",typeof(Volume)); volumeRoot.layer=31;
            var volume = volumeRoot.GetComponent<Volume>(); volume.isGlobal=true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            root.SetActive(true);
            foreach (var chunk in data) Far.LoadChunk(new FarChunkSnapshot(chunk.Key,chunk.Assets,chunk.Version));
            Marker = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/FrutigirGirl.prefab"));
            Marker.name = "Review original FrutigirGirl visual";
            foreach(var transform in Marker.GetComponentsInChildren<Transform>(true))transform.gameObject.layer=31;
            foreach(var animator in Marker.GetComponentsInChildren<Animator>(true))animator.enabled=false;
            foreach(var skinned in Marker.GetComponentsInChildren<SkinnedMeshRenderer>(true))skinned.updateWhenOffscreen=true;
            var idle = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Models/FrutigirGirl/Frutigirl@Idle.fbx").OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview"));
            if(idle!=null)idle.SampleAnimation(Marker,.5f);
            Floor = floor; Pose("Interior"); TimeOfDay(.4f);
        }
        public void Pose(string name)
        {
            float h = Floor * 6 + .2f;
            float wall = atmosphere.WallRadius(Floor);
            Vector3 position = new(wall-9,h+2.1f,-6), look = new(wall-23,h+.8f,13);
            if(name=="Glass") {position=new Vector3(7,h+3,-10);look=new Vector3(0,h,3);}
            if(name=="Underside") {position=new Vector3(7,h+2.4f,-10);look=new Vector3(0,h+6,0);}
            if(name=="Stairs") {position=new Vector3(wall-5,h+2,-9);look=new Vector3(wall-2,h+.8f,9);}
            if(name=="Arch")
            {
                var arch=Manager.CachedChunks.Where(c=>c.Key.FloorIndex==Floor).SelectMany(c=>c.Assets)
                    .Where(a=>a.AssetType==TowerAssetType.Arch).OrderBy(a=>(a.Position-position).sqrMagnitude).First();
                look=arch.ObjectToWorld.MultiplyPoint3x4(Manager.AssetPrefabs.GetPrefab(TowerAssetType.Arch).GetComponent<MeshFilter>().sharedMesh.bounds.center);
                Vector3 inward=new Vector3(atmosphere.center.x-look.x,0,atmosphere.center.y-look.z).normalized;
                position=look+inward*4+Vector3.down*.7f;
            }
            Camera.transform.position=position; Camera.transform.LookAt(look);
            Marker.transform.position=Vector3.Lerp(position,look,.55f); Marker.transform.position=new Vector3(Marker.transform.position.x,h,Marker.transform.position.z);
            Marker.transform.rotation=Quaternion.LookRotation(new Vector3(position.x-Marker.transform.position.x,0,position.z-Marker.transform.position.z));
        }
        public void TimeOfDay(float time) => cycle.ApplyTime(time);
        public void Render(bool enhanced, float seconds=10, float delta=.4f)
        {
            atmosphere.Sample(Camera.transform.position,seconds);
            shadows.Refresh(atmosphere.CurrentFloor,atmosphere.PreviousFloor);
            for(int warm=0;warm<20;warm++)atmosphere.Sample(Camera.transform.position,seconds-2+warm*.1f);
            atmosphere.Sample(Camera.transform.position,seconds+.1f);
            caustics.Sample(Camera,seconds,1f/30);
            river.previewTime=seconds; rays.previewTime=seconds;
            Finish.enabled=enhanced;
            if(enhanced)Finish.RenderForCamera(Camera,delta); else Shader.SetGlobalFloat("_AeroFinishEnabled",0);
            tick();
            RenderPipeline.SubmitRenderRequest(Camera,new UniversalRenderPipeline.SingleCameraRequest {destination=Target});
        }
        public void Save(string filename)
        {
            RenderTexture.active=Target; pixels.ReadPixels(new Rect(0,0,1920,1080),0,0); pixels.Apply();
            string folder=filename.StartsWith("Motion-")?"Temp/AeroBuildingFinishMotion":Folder;
            Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder,filename+".png"),pixels.EncodeToPNG());
            RenderTexture.active=oldTarget;
        }
        public void Dispose()
        {
            river.previewTime=oldRiverClock; rays.previewTime=oldRayClock;
            // Complete submitted offscreen work before destroying scene-owned GPU resources.
            // Native D3D12 camera requests can otherwise still reference the review targets.
            if(Target!=null && Target.IsCreated() && pixels!=null)
            {
                RenderTexture.active=Target;
                pixels.ReadPixels(new Rect(0,0,1,1),0,0,false);
            }
            RenderTexture.active=oldTarget;
            EditorSceneManager.CloseScene(scene,true); SceneManager.SetActiveScene(original);
            Object.DestroyImmediate(Target); Object.DestroyImmediate(pixels);
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=lightStates[i];
            ShaderUtil.allowAsyncCompilation=oldAsync; Shader.SetGlobalFloat("_AeroDaylight",oldDay);
            Object.FindFirstObjectByType<AeroBuildingFinish>()?.profile?.Apply();
        }
    }

    public static object Capture(int floor=0, bool motion=false)
    {
        using var session=new Session(floor);
        var report=new List<object>();
        foreach(var time in new[]{("Day",.4f),("Sunset",.73f),("Night",.05f)})
        {
            session.TimeOfDay(time.Item2);
            foreach(string pose in new[]{"Interior","Glass","Stairs","Underside","Arch"})
            {
                session.Pose(pose);
                session.Render(false); session.Save($"Floor{floor}-{time.Item1}-{pose}-Before");
                session.Render(true); session.Save($"Floor{floor}-{time.Item1}-{pose}-After");
                report.Add(new {floor,time=time.Item1,pose,session.Finish.LastStatus,session.Finish.ReflectionFloor,
                    session.Finish.LastSubmittedInstances,session.Far.LoadedInstanceCount,
                    width=session.Finish.ReflectionTexture?.width,height=session.Finish.ReflectionTexture?.height});
            }
        }
        if(motion)
        {
            session.TimeOfDay(.4f); session.Pose("Glass"); var position=session.Camera.transform.position; var rotation=session.Camera.transform.rotation;
            for(int i=0;i<36;i++)
            {
                float t=i/35f;
                session.Camera.transform.position=position+Vector3.right*Mathf.Sin(t*Mathf.PI)*2;
                session.Camera.transform.rotation=rotation*Quaternion.Euler(0,Mathf.Sin(t*Mathf.PI*2)*8,0);
                session.Marker.transform.position+=Vector3.right*.05f;
                session.Render(true,10+t*4);session.Save($"Motion-{i:D2}");
            }
        }
        File.WriteAllText(Path.Combine(Folder,$"Review-Floor{floor}.json"),Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    public static object InspectReflection()
    {
        using var session=new Session(0);
        session.Pose("Interior");session.TimeOfDay(.4f);
        session.Render(true);session.Save("Reflection-Inspection-Main");
        var target=session.Finish.ReflectionTexture;
        var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        var previous=RenderTexture.active;
        try
        {
            RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            File.WriteAllBytes(Path.Combine(Folder,"Reflection-Inspection-Texture.png"),pixels.EncodeToPNG());
            return new {session.Finish.LastStatus,session.Finish.ReflectionFloor,session.Finish.LastSubmittedInstances,
                planar=Shader.GetGlobalVector("_AeroPlanarParams"),enabled=Shader.GetGlobalFloat("_AeroFinishEnabled"),
                environment=Shader.GetGlobalFloat("_AeroFinishEnvironmentReady"),camera=session.Camera.transform.position,
                marker=session.Marker.transform.position,reflectionVP=Shader.GetGlobalMatrix("_AeroPlanarMatrix")};
        }
        finally{RenderTexture.active=previous;Object.DestroyImmediate(pixels);}
    }

    public static object InspectLifecycle()
    {
        using var session=new Session(0);
        var records=new List<object>();
        void Record(string label)=>records.Add(new {label,session.Finish.ReflectionFloor,session.Finish.ReflectionWeight,
            session.Finish.LastStatus,session.Finish.LastSubmittedInstances,session.Finish.CaptureCount,
            textureBytes=session.Finish.ReflectionTexture!=null?Profiler.GetRuntimeMemorySizeLong(session.Finish.ReflectionTexture):0,
            reflectionCameras=Resources.FindObjectsOfTypeAll<Camera>().Count(c=>c.cameraType==CameraType.Reflection),
            invertCulling=GL.invertCulling});
        session.Render(true,10,.05f);Record("Initial fade");
        session.Render(true,10.1f,.3f);Record("Settled floor 0");
        session.Floor=12;session.Pose("Interior");session.Render(true,11,.05f);Record("Teleport to floor 12; new fade");
        session.Render(true,11.1f,.3f);Record("Settled floor 12");
        session.Finish.liveReflections=false;session.Render(true,12);Record("Environment fallback; resources released");
        session.Finish.liveReflections=true;session.Render(true,12.1f);Record("Live restored");
        session.Manager.SetConstructionStage(null,0);
        session.Render(true,13);Record("All tiles unfinished; no capture");
        session.Finish.enabled=false;Record("Disabled; resources released");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder,"Lifecycle.json"),Newtonsoft.Json.JsonConvert.SerializeObject(records,Newtonsoft.Json.Formatting.Indented));
        return records;
    }

    public static string StartBenchmark(bool requestGpu=false)
    {
        if(benchmark!=null)throw new InvalidOperationException("Benchmark already running.");
        // An instrumented D3D12 review encountered a native driver fence/transition crash.
        // CPU review remains available without enabling the unstable instrumentation.
        if(requestGpu && SystemInfo.graphicsDeviceType==GraphicsDeviceType.Direct3D12)
            throw new InvalidOperationException("GPU profiling is unavailable on this D3D12 workstation after a native driver crash. Use CPU review.");
        collectGpu=requestGpu;
        benchmark=new Session(0);frame=0;foreach(var samples in cpu)samples.Clear();foreach(var samples in gpu)samples.Clear();
        previousProfiler=Profiler.enabled;previousGpu=UnityEditorInternal.ProfilerDriver.profileGPU;
        if(collectGpu)
        {
        UnityEditorInternal.ProfilerDriver.profileGPU=true;Profiler.enabled=true;
        sampler=CustomSampler.Create("Building finish review full camera GPU",true);recorder=sampler.GetRecorder();recorder.enabled=true;
        envelope=new CommandBuffer {name="Building finish review envelope"};
        RenderPipelineManager.beginCameraRendering+=BeginCameraSample;
        RenderPipelineManager.endCameraRendering+=EndCameraSample;
        }
        EditorApplication.update+=BenchmarkFrame;Status="Benchmark warming up";return Status;
    }
    private static void BeginCameraSample(ScriptableRenderContext context,Camera camera)
    {
        if(benchmark==null || camera.scene!=benchmark.Camera.scene)return;
        envelope.Clear();envelope.BeginSample(sampler);context.ExecuteCommandBuffer(envelope);
    }
    private static void EndCameraSample(ScriptableRenderContext context,Camera camera)
    {
        if(benchmark==null || camera.scene!=benchmark.Camera.scene)return;
        envelope.Clear();envelope.EndSample(sampler);context.ExecuteCommandBuffer(envelope);
    }
    private static void BenchmarkFrame()
    {
        try
        {
            // Alternating blocks, 15 warm-up frames then 45 measured frames; includes capture + main camera.
            int mode=(frame/60)%3,block=frame/180;
            if(collectGpu && frame%60>15 && recorder.gpuSampleBlockCount>0 && recorder.gpuElapsedNanoseconds>0)gpu[mode].Add(recorder.gpuElapsedNanoseconds/1e6);
            benchmark.Finish.liveReflections=mode==2;
            var clock=Stopwatch.StartNew(); benchmark.Render(mode>0,10);clock.Stop();
            if(frame%60>=15)cpu[mode].Add(clock.Elapsed.TotalMilliseconds);
            Status=$"Benchmark block {block+1}/3, {(mode==0?"baseline":mode==1?"environment":"live")}, frame {frame%60+1}/60";
            if(++frame<540)return;
            double? Median(List<double> list) {if(list.Count==0)return null;list.Sort();return list[list.Count/2];}
            var result=new {gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),resolution="1920x1080",
                scope=collectGpu ? "Manual Edit Mode review; GPU camera callbacks bracket source and reflection requests. Three alternating blocks; 45 measured frames after 15 warm-up frames per mode per block." :
                    "Manual Edit Mode CPU-only review, full completed tower cached; fixed floor view and lighting clock. No GPU instrumentation. Three alternating baseline/environment/live blocks; 45 measured frames after 15 warm-up frames per mode per block.",
                baselineCpuMs=Median(cpu[0]),environmentCpuMs=Median(cpu[1]),enhancedCpuMs=Median(cpu[2]),
                baselineGpuMs=Median(gpu[0]),environmentGpuMs=Median(gpu[1]),enhancedGpuMs=Median(gpu[2]),
                baselineGpuSamples=gpu[0].Count,environmentGpuSamples=gpu[1].Count,enhancedGpuSamples=gpu[2].Count,
                addedGpuMs=Median(gpu[2])-Median(gpu[0]),reflectionGpuMs=Median(gpu[2])-Median(gpu[1]),loadedInstances=benchmark.Far.LoadedInstanceCount,
                reflectionInstances=benchmark.Finish.LastSubmittedInstances,
                note="CPU timings include synchronous Editor overhead. GPU unavailable when sample counts are zero; no gameplay FPS claim. D3D12 GPU profiling disabled following a native driver fence/transition crash; the 2 ms GPU target remains unconfirmed."};
            Directory.CreateDirectory(Folder);File.WriteAllText(Path.Combine(Folder,"Performance.json"),Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));
            StopBenchmark();Status="Benchmark complete";
        }
        catch(Exception exception){UnityEngine.Debug.LogException(exception);StopBenchmark();Status="Benchmark failed";}
    }
    public static void StopBenchmark()
    {
        if(benchmark==null)return;
        RenderPipelineManager.beginCameraRendering-=BeginCameraSample;RenderPipelineManager.endCameraRendering-=EndCameraSample;
        EditorApplication.update-=BenchmarkFrame;if(recorder!=null)recorder.enabled=false;envelope?.Dispose();envelope=null;
        if(collectGpu){Profiler.enabled=previousProfiler;UnityEditorInternal.ProfilerDriver.profileGPU=previousGpu;}
        benchmark?.Dispose();benchmark=null;
    }
}
#endif
