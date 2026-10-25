#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Samples actual instanced river tiles in a disposable Edit Mode scene.</summary>
public static class RiverGlassVisualReview
{
    public const string Folder = "Documentation/FrutigerAero/Lighting/Previews";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static object Capture(string revision = "Enhanced", string poseFilter = null, bool captureMotion = false, int width = 1920, int height = 1080,
        float motionDuration = 36, bool navigationMotion = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Review in Edit Mode.");
        var original = SceneManager.GetActiveScene();
        var originalLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
        var originalEnabled = originalLights.Select(l => l.enabled).ToArray();
        var oldSun = RenderSettings.sun; var oldAmbient = RenderSettings.ambientMode;
        var oldAmbientLight = RenderSettings.ambientLight; var oldFog = RenderSettings.fog;
        var oldTarget = RenderTexture.active; var oldDay = Shader.GetGlobalFloat("_AeroDaylight");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var feature = renderer.rendererFeatures.OfType<RiverGlassRendererFeature>().Single();
        float oldClock = feature.previewTime;
        bool oldActive = feature.isActive;
        bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        foreach (var light in originalLights) light.enabled = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var ownedMaterials = new List<Material>();
        RiverGlassLighting lighting = null;
        try
        {
            RenderSettings.fog = false; RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.22f,.25f,.29f);
            var root = new GameObject("River glass review") { layer = 31 }; root.SetActive(false);
            var manager = root.AddComponent<ChunkManager>();
            var data = new List<ChunkSceneCache>();
            var matricesByFloor = new List<ChunkAssetData>[3];
            for (int floor = 0; floor < 3; floor++)
            {
                var tiles = new List<ChunkAssetData>(); matricesByFloor[floor] = tiles;
                foreach (var sector in TowerFloorLayout.Build(Vector2.zero, floor * 6, 54, 3.9f, 3.351f))
                {
                    Vector2 center = GlassWaterFloorLayout.TileCenter(sector.Position, sector.Rotation, sector.Scale, Vector2.zero);
                    var type = GlassWaterFloorLayout.IsChannel(center, floor < 2 ? 93 : 90, 3.9f)
                        ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor;
                    tiles.Add(new ChunkAssetData(tiles.Count, type, sector.Position, sector.Rotation, sector.Scale, 10));
                }
                data.Add((ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache), Private, null,
                    new object[] { new ChunkKey(floor, 0, 0), tiles }, null));
            }
            typeof(ChunkManager).GetField("cachedChunks", Private).SetValue(manager, data);
            typeof(ChunkManager).GetField("radialFloors", Private).SetValue(manager, true);
            foreach (TowerAssetType type in Enum.GetValues(typeof(TowerAssetType)))
            {
                string name = type == TowerAssetType.Floor ? "Floor_Tile" : type.ToString();
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Buildings/{name}.prefab");
                if (prefab != null) manager.AssetPrefabs.SetPrefab(type, prefab);
            }
            var far = root.AddComponent<InstancedFarChunkRenderer>();
            var cameraRoot = new GameObject("River glass review camera"); var camera = cameraRoot.AddComponent<Camera>();
            camera.enabled = false; camera.scene = scene; camera.cullingMask = 1 << 31; camera.targetTexture = target;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f,.18f,.24f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 160;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            typeof(InstancedFarChunkRenderer).GetField("renderCamera", Private).SetValue(far, camera);
            lighting = root.AddComponent<RiverGlassLighting>(); lighting.profile = feature.profile;
            lighting.cookieShader = Shader.Find("Hidden/TowerOfBabel/River caustic cookie"); root.SetActive(true);
            for (int floor = 0; floor < 3; floor++) far.LoadChunk(new FarChunkSnapshot(new ChunkKey(floor,0,0), matricesByFloor[floor]));
            var lightRoot = new GameObject("Review sun"); var sun = lightRoot.AddComponent<Light>();
            sun.type = LightType.Directional; sun.color = new Color(1,.96f,.88f); sun.intensity = 1.2f;
            sun.shadows = LightShadows.Hard; sun.cullingMask = 1 << 31; sun.transform.rotation = Quaternion.Euler(58, -32, 0); RenderSettings.sun = sun;
            void Box(string name, Vector3 position, Vector3 scale, Color color)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 31;
                go.transform.position = position; go.transform.localScale = scale;
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.color = color;
                mat.SetFloat("_Smoothness", .3f); go.GetComponent<Renderer>().sharedMaterial = mat; ownedMaterials.Add(mat);
            }
            Box("Lower floor visible through water", new Vector3(0,-6,0), new Vector3(110,.3f,110), new Color(.8f,.83f,.84f));
            for (int i = -3; i <= 3; i++)
                Box("Underwater visibility marker", new Vector3(i*3,-5.7f,0), new Vector3(1,.2f,18), i%2==0?new Color(.12f,.43f,.24f):new Color(.9f,.38f,.12f));
            Box("White receiver wall", new Vector3(-9,2,-4), new Vector3(.3f,4,16), Color.white);
            Box("White receiver pillar", new Vector3(5,2,-4), new Vector3(.8f,4,.8f), Color.white);
            Box("Upper white receiver wall", new Vector3(-3,14,-4), new Vector3(.3f,4,12), Color.white);
            Box("Upper white receiver pillar", new Vector3(3,14,-4), new Vector3(.8f,4,.8f), Color.white);
            var results = new List<object>();
            void Save(string name, Vector3 position, Vector3 look, float time, bool night = false)
            {
                if(poseFilter!=null&&!name.StartsWith(poseFilter,StringComparison.Ordinal))return;
                camera.transform.position = position; camera.transform.LookAt(look);
                lighting.Release();
                Shader.SetGlobalFloat("_AeroDaylight", night ? 0 : 1); sun.intensity = night ? .035f : 1.2f;
                feature.previewTime = time;
                lighting.Sample(camera, 0, 1);
                for(int step=1;step<=Mathf.CeilToInt(time*30);step++)
                    lighting.Sample(camera,Mathf.Min(time,step/30f),1f/30);
                var projectionLights=root.GetComponentsInChildren<Light>();
                var sampledEnabled=projectionLights.Select(l=>l.enabled).ToArray();
                var tick = (Action)Delegate.CreateDelegate(typeof(Action), far, typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private));
                foreach (string mode in new[] { "Disabled", "Surface", "Projection", "Combined" })
                {
                    feature.SetActive(mode == "Surface" || mode == "Combined");
                    for(int i=0;i<projectionLights.Length;i++)
                        projectionLights[i].enabled=sampledEnabled[i]&&(mode=="Projection"||mode=="Combined");
                    for (int i = 0; i < 8; i++) { tick(); camera.Render(); }
                    long allocated = GC.GetAllocatedBytesForCurrentThread();
                    var watch = Stopwatch.StartNew();
                    for (int i = 0; i < 12; i++) { tick(); camera.Render(); }
                    watch.Stop();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
                    results.Add(new { name, mode, seconds=time, width = target.width, height = target.height,
                        waterDrawCalls = feature.isActive ? feature.LastDrawCalls : 0,
                        refractedLayers = feature.isActive ? feature.LastRefractedLayers : 0,
                        riverLights = mode == "Projection" || mode == "Combined" ? lighting.ActiveLightCount : 0,
                        cookies = lighting.AllocatedCookieCount, farDrawCalls = far.DrawCallCount,
                        projectionPositions=root.GetComponentsInChildren<Light>().Where(l=>l.enabled&&l.transform.forward.y>.5f)
                            .Select(l=>new {x=l.transform.parent.position.x,y=l.transform.parent.position.y,z=l.transform.parent.position.z}).ToArray(),
                        backgroundWidth=feature.isActive?feature.LastBackgroundWidth:0,
                        backgroundHeight=feature.isActive?feature.LastBackgroundHeight:0,
                        shadowLights=root.GetComponentsInChildren<Light>().Count(l=>l.enabled&&l.shadows!=LightShadows.None),
                        shadowResolution=RiverGlassLighting.ShadowResolution,
                        cookieResolution=RiverGlassLighting.CookieResolution,
                        projectionRange=feature.profile.projectionRange,
                        projectionAngle=feature.profile.projectionAngle,
                        projectionPatchCount=feature.profile.projectionPatchCount,
                        projectionAheadDistance=feature.profile.projectionAheadDistance,
                        projectionPatternDensity=feature.profile.projectionPatternDensity,
                        highlightOpacity=feature.profile.projectionHighlightOpacity,
                        detailOpacity=feature.profile.projectionDetailOpacity,
                        patchSeparation=feature.profile.patchSeparation,
                        daylightIntensity=feature.profile.daylightIntensity,
                        nightIntensity=feature.profile.nightIntensity,
                        cutoutSpeed=feature.profile.projectionCutoutSpeed,
                        cutoutWarp=feature.profile.projectionCutoutWarp,
                        fadeSeconds=feature.profile.projectionBurstFade,
                        maxActivePatches=feature.profile.projectionMaxActivePatches,
                        flowVariation=feature.profile.projectionFlowVariation,
                        flowSway=feature.profile.projectionFlowSway,
                        detailLifetime=new[]{feature.profile.projectionDetailLifetime.x,feature.profile.projectionDetailLifetime.y},
                        highlightLifetime=new[]{feature.profile.projectionHighlightLifetime.x,feature.profile.projectionHighlightLifetime.y},
                        lightIntensities=projectionLights.Where(l=>l.enabled).Select(l=>l.intensity).ToArray(),
                        projectionCullingMasks=projectionLights.Select(l=>l.cullingMask).Distinct().ToArray(),
                        surfacePattern=feature.profile.surfaceCaustics!=null?feature.profile.surfaceCaustics.name:feature.profile.caustics.name,
                        surfacePatternOpacity=feature.profile.surfacePatternOpacity,
                        surfacePatternBlur=feature.profile.surfacePatternBlur,
                        surfacePatternScale=feature.profile.surfacePatternScale,
                        cpuRenderMilliseconds = watch.Elapsed.TotalMilliseconds / 12,
                        managedBytesPerRender = allocated / 12, gpuMilliseconds = (double?)null,
                        timingNote = "Synchronous Edit Mode Camera.Render includes editor and submission overhead; GPU frame timing unavailable. Not gameplay FPS." });
                }
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
                Directory.CreateDirectory(Folder); File.WriteAllBytes($"{Folder}/River-{revision}-{name}.png", pixels.EncodeToPNG());
                if(captureMotion&&name=="Weave-Motion")
                {
                    string frames=Path.GetFullPath($"Temp/CodexRiverWeave-{revision}");Directory.CreateDirectory(frames);
                    var motion=new List<object>();
                    Vector3 basePosition=camera.transform.position;
                    Quaternion baseRotation=camera.transform.rotation;
                    Vector3 heading=Vector3.ProjectOnPlane(camera.transform.forward,Vector3.up).normalized;
                    void PositionCamera(float seconds)
                    {
                        float walk=navigationMotion?Mathf.SmoothStep(0,1,Mathf.InverseLerp(12,24,seconds)):0;
                        float turn=navigationMotion?Mathf.SmoothStep(0,1,Mathf.InverseLerp(24,34,seconds)):0;
                        camera.transform.position=basePosition+heading*(walk*20);
                        camera.transform.rotation=Quaternion.AngleAxis(turn*180,Vector3.up)*baseRotation;
                    }
                    lighting.Release();feature.SetActive(true);PositionCamera(0);lighting.Sample(camera,0,1);
                    for(int frame=0;frame<Mathf.CeilToInt(motionDuration*10);frame++)
                    {
                        float seconds=frame/10f;
                        if(frame>0)for(int step=1;step<=3;step++)
                        {
                            float sample=(frame-1)/10f+step/30f;PositionCamera(sample);lighting.Sample(camera,sample,1f/30);
                        }
                        feature.previewTime=seconds;tick();camera.Render();RenderTexture.active=target;
                        pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                        File.WriteAllBytes($"{frames}/{frame:000}.png",pixels.EncodeToPNG());
                        motion.Add(new {frame,seconds,lights=lighting.ActiveLightCount,cookies=lighting.AllocatedCookieCount,
                            windows=lighting.RunningWindowCount,globalVisibility=lighting.GlobalVisibility,startsAllowed=lighting.WindowStartsAllowed,
                            starts=lighting.WindowStartCount,lastStartedSlot=lighting.LastStartedSlot,lastStart=lighting.LastWindowStartedAt,
                            lastDuration=lighting.LastWindowDuration,lastRest=lighting.LastWindowRest,nextGlobalQuiet=lighting.NextGlobalQuietAt,
                            flow=new[]{lighting.CurrentFlowOffset.x,lighting.CurrentFlowOffset.y},
                            cameraPosition=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},
                            cameraHeading=new[]{camera.transform.forward.x,camera.transform.forward.y,camera.transform.forward.z},
                            cameraMotion=navigationMotion?(seconds<12?"Stationary":seconds<24?"Walking":"Turning"):"Stationary",
                            directions=root.GetComponentsInChildren<Light>(true).Where(l=>l.transform.forward.y>.5f)
                                .Select(l=>new {slot=l.transform.parent.GetSiblingIndex(),x=l.transform.forward.x,y=l.transform.forward.y,z=l.transform.forward.z,
                                    rotation=new[]{l.transform.rotation.x,l.transform.rotation.y,l.transform.rotation.z,l.transform.rotation.w},
                                    source=new[]{l.transform.parent.position.x,l.transform.parent.position.y,l.transform.parent.position.z},
                                    intensity=l.intensity,enabled=l.isActiveAndEnabled,mask=l.cullingMask}).ToArray()});
                    }
                    File.WriteAllText($"Documentation/FrutigerAero/Lighting/RIVER_{revision.ToUpperInvariant()}_MOTION.json",
                        Newtonsoft.Json.JsonConvert.SerializeObject(motion,Newtonsoft.Json.Formatting.Indented));
                }
            }
            Save("Above", new Vector3(11,15.8f,-18), new Vector3(2,11.8f,-3), 2);
            Save("Below", new Vector3(11,9,-18), new Vector3(1,12.3f,-2), 2.5f);
            Save("Underside-Close",new Vector3(.8f,8,-24),new Vector3(0,12,-24),2.5f);
            Save("Underside-Pool",new Vector3(0,8.5f,0),new Vector3(1,12,0),2.5f);
            Save("Stacked", new Vector3(5,24,-9), new Vector3(0,-5,0), 3);
            Save("Ring-Join", new Vector3(39,15,-23), new Vector3(43,12,-15), 4);
            Save("Night-A", new Vector3(11,15.8f,-18), new Vector3(2,13,-3), 5, true);
            Save("Night-B", new Vector3(11,15.8f,-18), new Vector3(2,13,-3), 8, true);
            Save("Distance-40-A",new Vector3(24,17,-35),new Vector3(0,12,-3),2);
            Save("Distance-40-B",new Vector3(24,17,-35),new Vector3(0,12,-3),6);
            // The original comparison fixture puts an entire floor in one artificial chunk.
            // Local projection review uses real spatial chunks so distance pruning cannot
            // reject nearby negative-Z receivers using that artificial chunk's distant bounds.
            lighting.enabled=false;
            var spatialChunks=matricesByFloor.SelectMany(tiles=>tiles)
                .GroupBy(a=>ChunkGrid.WorldToChunk(a.Position,manager.ChunkSizeMeters,manager.FloorHeight))
                .Select(group=>(ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),Private,null,
                    new object[]{group.Key,group.Select((a,i)=>new ChunkAssetData(i,a.AssetType,a.Position,a.Rotation,a.Scale,a.Stage)).ToList()},null)).ToList();
            typeof(ChunkManager).GetField("cachedChunks",Private).SetValue(manager,spatialChunks);
            typeof(ChunkManager).GetMethod("RebuildChunkLookup",Private).Invoke(manager,null);
            lighting.enabled=true;
            GameObject.Find("Upper white receiver wall").transform.position=new Vector3(5,14,-12);
            GameObject.Find("Upper white receiver pillar").transform.position=new Vector3(10,14,-7);
            Save("Local-Caustics-Day",new Vector3(12,15,-16),new Vector3(4,13,-10),5);
            Save("Local-Caustics-Night-A",new Vector3(12,15,-16),new Vector3(4,13,-10),5,true);
            Save("Local-Caustics-Night-B",new Vector3(12,15,-16),new Vector3(4,13,-10),8,true);
            GameObject.Find("Upper white receiver wall").transform.position=new Vector3(5,8,-12);
            GameObject.Find("Upper white receiver pillar").transform.position=new Vector3(10,8,-7);
            Save("Eye-Level-Day",new Vector3(12,7.8f,-16),new Vector3(5,8,-12),5);
            Save("Eye-Level-Night-A",new Vector3(12,7.8f,-16),new Vector3(5,8,-12),5,true);
            Save("Eye-Level-Night-B",new Vector3(12,7.8f,-16),new Vector3(5,8,-12),8,true);
            Box("Ahead coverage ceiling",new Vector3(0,5.5f,0),new Vector3(24,.2f,96),Color.white);
            Save("Ahead-Day",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),5);
            Save("Ahead-Night-A",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),5,true);
            Save("Ahead-Night-B",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),8,true);
            Save("Ahead-Turned-Day",new Vector3(8,1.8f,6),new Vector3(0,5.3f,34),5);
            Save("Ahead-Turned-Night",new Vector3(8,1.8f,6),new Vector3(0,5.3f,34),5,true);
            Save("Bursts-Quiet",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),.1f,true);
            Save("Bursts-A",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),2,true);
            Save("Bursts-B",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),4,true);
            Save("Bursts-C",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),6,true);
            Save("Bursts-D",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),8,true);
            Save("Bursts-E",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),11,true);
            Save("Weave-Day",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),10);
            Save("Weave-Night",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),10,true);
            Save("Weave-Motion",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),10,true);
            if(poseFilter=="Occlusion")
            {
                Save("Occlusion-Clear",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),6,true);
                var source=root.GetComponentsInChildren<Light>().Where(l=>l.enabled&&l.transform.forward.y>.5f)
                    .OrderByDescending(l=>l.intensity).First();
                Vector3 middle=source.transform.position;middle.y=(middle.y+5.4f)*.5f;
                Box("Occlusion control slab",middle,new Vector3(6.5f,.2f,6.5f),Color.white);
                GameObject.Find("Occlusion control slab").GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
                Save("Occlusion-Blocked",new Vector3(8,1.8f,6),new Vector3(0,5.3f,-22),6,true);
            }
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText($"Documentation/FrutigerAero/Lighting/RIVER_{revision.ToUpperInvariant()}_PREVIEW.json", json);
            return results;
        }
        finally
        {
            if (lighting != null) lighting.Release();
            feature.previewTime = oldClock; feature.SetActive(oldActive); RenderTexture.active = oldTarget;
            EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(original);
            foreach (var mat in ownedMaterials) Object.DestroyImmediate(mat);
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            for (int i = 0; i < originalLights.Length; i++) if (originalLights[i] != null) originalLights[i].enabled = originalEnabled[i];
            RenderSettings.sun = oldSun; RenderSettings.ambientMode = oldAmbient; RenderSettings.ambientLight = oldAmbientLight;
            RenderSettings.fog = oldFog; Shader.SetGlobalFloat("_AeroDaylight", oldDay); ShaderUtil.allowAsyncCompilation = oldAsync;
        }
    }
}
#endif
