#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Disposable completed floor review through the actual near pool and far renderer.</summary>
public static class TowerTileVisualReview
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public const string Folder = "Documentation/FrutigerAero/Buildings/Previews";
    public static string Capture(int floor = 0)
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Review in Edit Mode.");
        var original = SceneManager.GetActiveScene(); if(original.isDirty) throw new InvalidOperationException("Review needs a clean saved scene.");
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var sourceFar = manager.GetComponent<InstancedFarChunkRenderer>();
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None); var enabled = lights.Select(l=>l.enabled).ToArray();
        var oldSun = RenderSettings.sun; var ambient = RenderSettings.ambientProbe;
        var sky = RenderSettings.skybox; var ambientMode = RenderSettings.ambientMode;
        var ambientSky = RenderSettings.ambientSkyColor; var ambientEquator = RenderSettings.ambientEquatorColor; var ambientGround = RenderSettings.ambientGroundColor;
        bool fog = RenderSettings.fog; var fogMode = RenderSettings.fogMode; var fogColor = RenderSettings.fogColor;
        float fogDensity = RenderSettings.fogDensity, reflection = RenderSettings.reflectionIntensity, daylight = Shader.GetGlobalFloat("_AeroDaylight");
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        foreach(var light in lights) light.enabled = false;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        var target = new RenderTexture(1488,887,24,RenderTextureFormat.ARGBHalf); var oldTarget = RenderTexture.active;
        var feature = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();
        float preview = feature.previewTime;
        GameObject root = null;
        try
        {
            root = TowerRayReview.Build(manager,floor); SceneManager.MoveGameObjectToScene(root,scene); TowerRayReview.Initialize(root,floor);
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                UnityEngine.Object.DestroyImmediate(filter.sharedMesh); UnityEngine.Object.DestroyImmediate(filter.GetComponent<Renderer>().sharedMaterial);
                UnityEngine.Object.DestroyImmediate(filter.gameObject);
            }
            var data = new GameObject("Real pooled and instanced tile paths"); data.transform.SetParent(root.transform); data.SetActive(false); data.layer = 31;
            var owner = data.AddComponent<ChunkManager>(); owner.enabled = false;
            foreach(var name in new[]{"radialFloors","floorCenter","floorRadialDepth","floorArcLength"})
                typeof(ChunkManager).GetField(name,Private).SetValue(owner,typeof(ChunkManager).GetField(name,Private).GetValue(manager));
            foreach(TowerAssetType type in Enum.GetValues(typeof(TowerAssetType))) owner.AssetPrefabs.SetPrefab(type,manager.AssetPrefabs.GetPrefab(type));
            var far = data.AddComponent<InstancedFarChunkRenderer>(); far.enabled = false;
            typeof(InstancedFarChunkRenderer).GetField("models",Private).SetValue(far,typeof(InstancedFarChunkRenderer).GetField("models",Private).GetValue(sourceFar));
            var near = data.AddComponent<NearTowerAssetPool>(); near.Configure(owner.AssetPrefabs,owner);
            var camera = root.GetComponentInChildren<Camera>(); camera.scene = scene; camera.targetTexture = target;
            typeof(InstancedFarChunkRenderer).GetField("renderCamera",Private).SetValue(far,camera);
            var fx = root.GetComponent<AeroTowerAtmosphere>(); fx.cycle.ApplyTime(.29f);
            var chunks = manager.CachedChunks.Where(c=>c.Key.FloorIndex==floor||c.Key.FloorIndex==floor+1).Select(c=>
                (ChunkSceneCache)Activator.CreateInstance(typeof(ChunkSceneCache),Private,null,new object[]{c.Key,c.Assets
                    .Where(a=>c.Key.FloorIndex==floor||a.AssetType.IsFloor()).Select((a,i)=>new ChunkAssetData(i,a.AssetType,a.Position,a.Rotation,a.Scale,10)).ToList()},null)).ToArray();
            data.SetActive(true); // manager/far stay disabled; drawing is explicit, without entering Play Mode.
            void Save(string name,Vector3 position,Vector3 look)
            {
                camera.transform.position = position; camera.transform.LookAt(look);
                var key = ChunkGrid.WorldToChunk(position,manager.ChunkSizeMeters,manager.FloorHeight);
                near.BeginUpdate();
                foreach(var chunk in chunks)
                {
                    if(Mathf.Abs(chunk.Key.X-key.X)<=1&&Mathf.Abs(chunk.Key.Z-key.Z)<=1&&Mathf.Abs(chunk.Key.FloorIndex-key.FloorIndex)<=1)
                    {far.RemoveChunk(chunk.Key); near.AddChunk(chunk);}
                    else far.LoadChunk(new FarChunkSnapshot(chunk.Key,chunk.Assets,chunk.Version));
                }
                near.EndUpdate(); foreach(var go in near.ActiveAssets) foreach(var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31;
                for(int i=0;i<30;i++) fx.Sample(position,i*.1f); feature.previewTime = 3;
                for(int i=0;i<2;i++) {typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate",Private).Invoke(far,null); camera.Render();}
                RenderTexture.active=target; var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,target.width,target.height),0,0); image.Apply();
                Directory.CreateDirectory(Folder); File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(image);
            }
            float height=floor*6, radius=fx.WallRadius(floor);
            if(floor==0)
            {
                var position=new Vector3(8.84f,2.17f,-90.58f);
                Save("Tile-Fix-Interior",position,position+Quaternion.Euler(2.45f,12.33f,0)*Vector3.forward*30);
            }
            Save($"Tile-Fix-Floor{floor}-Joins",new Vector3(8,height+4,-radius*.52f),new Vector3(0,height+.2f,-radius*.42f));
            Save($"Tile-Fix-Floor{floor}-Underside",new Vector3(8,height+2,-radius*.52f),new Vector3(0,height+6,-radius*.42f));
            return Folder;
        }
        finally
        {
            feature.previewTime=preview; RenderTexture.active=oldTarget; ShaderUtil.allowAsyncCompilation=async;
            if(root!=null)foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {if(filter.sharedMesh!=null&&!AssetDatabase.Contains(filter.sharedMesh)&&filter.sharedMesh.hideFlags==HideFlags.None) UnityEngine.Object.DestroyImmediate(filter.sharedMesh);}
            EditorSceneManager.CloseScene(scene,true); target.Release(); UnityEngine.Object.DestroyImmediate(target); SceneManager.SetActiveScene(original);
            for(int i=0;i<lights.Length;i++)if(lights[i])lights[i].enabled=enabled[i]; RenderSettings.sun=oldSun; RenderSettings.ambientProbe=ambient;
            RenderSettings.skybox=sky;RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=ambientSky;
            RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
            RenderSettings.fog=fog;RenderSettings.fogMode=fogMode;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;
            RenderSettings.reflectionIntensity=reflection;Shader.SetGlobalFloat("_AeroDaylight",daylight);
        }
    }
}
#endif
