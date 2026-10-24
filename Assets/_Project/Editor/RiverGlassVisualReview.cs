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
    public static object Capture()
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
        bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        foreach (var light in originalLights) light.enabled = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var target = new RenderTexture(1280, 800, 24, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
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
            var results = new List<object>();
            void Save(string name, Vector3 position, Vector3 look, float time, bool night = false)
            {
                camera.transform.position = position; camera.transform.LookAt(look);
                Shader.SetGlobalFloat("_AeroDaylight", night ? 0 : 1); sun.intensity = night ? .035f : 1.2f;
                feature.previewTime = time; lighting.Sample(camera, time, 1);
                lighting.Sample(camera, time + .01f, 1);
                var watch = Stopwatch.StartNew();
                for (int i = 0; i < 2; i++)
                { typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate", Private).Invoke(far, null); camera.Render(); }
                watch.Stop(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0); pixels.Apply();
                Directory.CreateDirectory(Folder); File.WriteAllBytes($"{Folder}/River-Glass-{name}.png", pixels.EncodeToPNG());
                results.Add(new { name, waterDrawCalls = feature.LastDrawCalls, refractedLayers = feature.LastRefractedLayers,
                    status = feature.LastStatus,
                    riverLights = lighting.ActiveLightCount, cookies = lighting.AllocatedCookieCount,
                    farDrawCalls = far.DrawCallCount, cpuRenderMilliseconds = watch.Elapsed.TotalMilliseconds / 2,
                    gpuMilliseconds = (double?)null });
            }
            Save("Above", new Vector3(11,3.8f,-18), new Vector3(2,-.2f,-3), 2);
            Save("Below", new Vector3(11,-3,-18), new Vector3(1,.3f,-2), 2.5f);
            Save("Stacked", new Vector3(5,18,-9), new Vector3(0,-5,0), 3);
            Save("Ring-Join", new Vector3(39,3,-23), new Vector3(43,0,-15), 4);
            Save("Night-A", new Vector3(11,3.8f,-18), new Vector3(2,1,-3), 5, true);
            Save("Night-B", new Vector3(11,3.8f,-18), new Vector3(2,1,-3), 8, true);
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(results, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText("Documentation/FrutigerAero/Lighting/RIVER_GLASS_PREVIEW.json", json);
            return results;
        }
        finally
        {
            if (lighting != null) lighting.Release();
            feature.previewTime = oldClock; RenderTexture.active = oldTarget;
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
