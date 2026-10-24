#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Disposable completed geometry for visual review; never changes gameplay construction.</summary>
public static class TowerRayReview
{
    public const string Folder="Documentation/FrutigerAero/Lighting/DynamicRays";
    public const string FixturePath="Assets/_Project/Tests/Fixtures/TowerRayReview.prefab";
    [Serializable] private sealed class Snapshot { public ChunkAssetData[] assets; }
    public static GameObject Build(ChunkManager manager, int floor)
    {
        var root=new GameObject("Completed tower review floor "+floor); root.layer=31;
        var openings=new GameObject("Opening sockets"); openings.transform.SetParent(root.transform);
        var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var source=UnityEngine.Object.FindFirstObjectByType<AeroTowerAtmosphere>();
        var bent=new RadialFloorMeshCache();
        var groups=new Dictionary<TowerAssetType,List<CombineInstance>>();
        foreach(var chunk in manager.CachedChunks)
        {
            int f=chunk.Key.FloorIndex;
            if(f!=floor && f!=floor+1)continue;
            foreach(var asset in chunk.Assets)
            {
                if(f==floor+1 && !asset.AssetType.IsFloor())continue;
                var prefab=manager.AssetPrefabs.GetPrefab(asset.AssetType);
                Mesh mesh=prefab.GetComponent<Building>().GetStageMesh(10);
                if(asset.AssetType.IsFloor()) mesh=bent.Get(mesh,Vector2.Distance(new Vector2(asset.Position.x,asset.Position.z),manager.FloorCenter),asset.Scale);
                if(!groups.TryGetValue(asset.AssetType,out var group))groups[asset.AssetType]=group=new List<CombineInstance>();
                group.Add(new CombineInstance {mesh=mesh,transform=asset.ObjectToWorld});
                if(asset.AssetType==TowerAssetType.Arch)
                {
                    Vector3 point=asset.ObjectToWorld.MultiplyPoint3x4(new Vector3(mesh.bounds.center.x,0,0)); point.y=f*manager.FloorHeight+3.6f;
                    var inward=new Vector3(manager.FloorCenter.x-point.x,0,manager.FloorCenter.y-point.z).normalized;
                    var socket=new GameObject("Arch "+openings.transform.childCount);socket.transform.SetParent(openings.transform);socket.transform.SetPositionAndRotation(point+inward*.25f,Quaternion.LookRotation(inward));
                }
            }
        }
        foreach(var group in groups)
        {
            var mesh=new Mesh {name="Review "+group.Key,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Value.ToArray(),true,true);
            var go=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root.transform);go.layer=31;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var material=new Material(manager.AssetPrefabs.GetPrefab(group.Key).GetComponent<Renderer>().sharedMaterial);
            material.name="Review "+group.Key;material.SetFloat("_ReceiveShadows",1);material.DisableKeyword("_RECEIVE_SHADOWS_OFF");
            if(group.Key.IsFloor()) TowerFloorLayout.ConfigureMaterial(material,false,manager.FloorCenter);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
        bent.Dispose();
        var sun=new GameObject("Review sunlight",typeof(Light));sun.transform.SetParent(root.transform);
        var light=sun.GetComponent<Light>();light.type=LightType.Directional;light.shadows=LightShadows.Soft;light.cullingMask=1<<31;
        var moon=new GameObject("Review moon",typeof(Light));moon.transform.SetParent(root.transform);var moonLight=moon.GetComponent<Light>();moonLight.type=LightType.Directional;moonLight.shadows=LightShadows.Soft;moonLight.cullingMask=1<<31;
        var cycle=root.AddComponent<AeroDayNightCycle>();cycle.profile=source.cycle.profile;cycle.sun=light;cycle.moon=moonLight;
        var fx=root.AddComponent<AeroTowerAtmosphere>();fx.cycle=cycle;fx.raySettings=source.raySettings;fx.center=source.center;fx.baseRadius=source.baseRadius;fx.decrease=source.decrease;fx.floorHeight=source.floorHeight;fx.levels=source.levels;
        fx.bounceLights=new Light[2];
        for(int i=0;i<2;i++)
        {
            var go=new GameObject("Review anchored fill "+i,typeof(Light));go.transform.SetParent(root.transform);var fill=go.GetComponent<Light>();fill.cullingMask=1<<31;fill.enabled=false;fx.bounceLights[i]=fill;
            var data=fill.GetUniversalAdditionalLightData();var so=new SerializedObject(data);so.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=1;so.ApplyModifiedPropertiesWithoutUndo();
        }
        var camera=new GameObject("Review camera",typeof(Camera),typeof(UniversalAdditionalCameraData));camera.transform.SetParent(root.transform);
        var cam=camera.GetComponent<Camera>();cam.enabled=false;cam.cullingMask=1<<31;cam.nearClipPlane=.05f;cam.farClipPlane=400;cam.fieldOfView=68;cam.allowHDR=true;
        var cd=cam.GetUniversalAdditionalCameraData();cd.renderPostProcessing=true;cd.requiresDepthTexture=true;cd.volumeLayerMask=1<<31;
        var global=new GameObject("Review grading",typeof(Volume));global.layer=31;global.transform.SetParent(root.transform);var volume=global.GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
        return root;
    }
    public static void Initialize(GameObject root,int floor)
    {
        var fx=root.GetComponent<AeroTowerAtmosphere>();var sockets=root.transform.Find("Opening sockets");var openings=new List<AeroArchOpening>();
        foreach(Transform socket in sockets)openings.Add(new AeroArchOpening(openings.Count,floor,socket.position,socket.forward));
        fx.SetConstructionContext(true,openings);
    }
    public static void SaveBenchmarkFixture()
    {
        Directory.CreateDirectory("Assets/_Project/Tests/Fixtures");AssetDatabase.Refresh();
        var manager=UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var snapshot=new Snapshot {assets=manager.CachedChunks.Where(c=>c.Key.FloorIndex<=1).SelectMany(c=>c.Assets.Where(a=>c.Key.FloorIndex==0||a.AssetType.IsFloor())).Select(a=>new ChunkAssetData(a.LocalIndex,a.AssetType,a.Position,a.Rotation,a.Scale,10)).ToArray()};
        File.WriteAllText("Assets/_Project/Tests/Fixtures/TowerRaySnapshot.json",JsonUtility.ToJson(snapshot));
        var root=Build(manager,0);
        try
        {
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                UnityEngine.Object.DestroyImmediate(filter.sharedMesh);UnityEngine.Object.DestroyImmediate(filter.GetComponent<Renderer>().sharedMaterial);UnityEngine.Object.DestroyImmediate(filter.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root,FixturePath);AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if(AssetDatabase.IsValidFolder("Assets/_Project/Tests/Fixtures/TowerRayGeometry")) AssetDatabase.DeleteAsset("Assets/_Project/Tests/Fixtures/TowerRayGeometry");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
    }
    public static string Capture(int floor=0)
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Review in Edit Mode.");
        var original=SceneManager.GetActiveScene();if(original.isDirty)throw new InvalidOperationException("Save the scene before review.");
        var manager=UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var oldSun=RenderSettings.sun;var sky=RenderSettings.skybox;var ambient=RenderSettings.ambientProbe;var fog=RenderSettings.fog;var fogColor=RenderSettings.fogColor;float fogDensity=RenderSettings.fogDensity,reflection=RenderSettings.reflectionIntensity;
        Color ambientSky=RenderSettings.ambientSkyColor,ambientEquator=RenderSettings.ambientEquatorColor,ambientGround=RenderSettings.ambientGroundColor;var ambientMode=RenderSettings.ambientMode;
        var lights=UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);var enabled=lights.Select(l=>l.enabled).ToArray();foreach(var l in lights)l.enabled=false;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
        var feature=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();bool wasActive=feature.isActive;
        var rt=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf);var oldTarget=RenderTexture.active;
        GameObject root=null;bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
        Directory.CreateDirectory(Folder);
        try
        {
            root=Build(manager,floor);SceneManager.MoveGameObjectToScene(root,scene);Initialize(root,floor);
            var camera=root.GetComponentInChildren<Camera>();camera.scene=scene;camera.targetTexture=rt;
            var fx=root.GetComponent<AeroTowerAtmosphere>();float clock=0;
            void Set(float angle,float time)
            {
                float r=fx.WallRadius(floor);var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var tangent=new Vector3(-radial.z,0,radial.x);
                camera.transform.position=radial*(r-13)+Vector3.up*(floor*6+2.05f);
                camera.transform.LookAt(radial*(r-1)+tangent*15+Vector3.up*(floor*6+2.8f));
                fx.cycle.ApplyTime(time);for(int i=0;i<30;i++){clock+=.1f;fx.Sample(camera.transform.position,clock);} feature.previewTime=clock;
            }
            void Save(string name)
            {
                camera.Render();camera.Render();RenderTexture.active=rt;
                var image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
            }
            for(int side=0;side<4;side++)
            {
                Set(side*Mathf.PI*.5f,.29f);feature.SetActive(false);foreach(var l in fx.bounceLights)l.enabled=false;Save($"Floor{floor}-Side{side}-Before");
                feature.SetActive(true);fx.Sample(camera.transform.position,clock+.01f);Save($"Floor{floor}-Side{side}-After");
            }
            foreach(var time in new[]{.26f,.5f,.74f,.04f}){Set(0,time);Save($"Floor{floor}-Time{time:F2}");}
            Set(0,.29f);
            camera.transform.position=new Vector3(fx.WallRadius(floor)-8,floor*6+2.05f,0);
            camera.transform.LookAt(new Vector3(fx.WallRadius(floor)-25,floor*6+2.1f,28));
            feature.SetActive(false);foreach(var l in fx.bounceLights)l.enabled=false;Save($"Floor{floor}-Interior-Before");
            feature.SetActive(true);fx.Sample(camera.transform.position,clock+.01f);Save($"Floor{floor}-Interior-After");
            camera.transform.position=new Vector3(0,floor*6+2.05f,0);camera.transform.LookAt(new Vector3(fx.WallRadius(floor)-1,floor*6+2.8f,15));Save($"Floor{floor}-Center");
            Set(Mathf.PI*1.5f,.29f);
            var startPosition=camera.transform.position;var startRotation=camera.transform.rotation;
            for(int frame=0;frame<(floor==0?36:0);frame++)
            {
                feature.previewTime=clock+frame*.12f;
                Save($"Floor{floor}-Motion{frame:D2}");
            }
            for(int frame=0;frame<(floor==0?36:0);frame++)
            {
                camera.transform.position=startPosition+camera.transform.right*Mathf.Sin(frame*Mathf.PI/35)*2;
                camera.transform.rotation=startRotation*Quaternion.Euler(0,Mathf.Sin(frame*Mathf.PI*2/35)*9,0);
                clock+=.12f;feature.previewTime=clock;fx.Sample(camera.transform.position,clock);
                Save($"Floor{floor}-Walk{frame:D2}");
            }
            return Folder;
        }
        finally
        {
            feature.SetActive(wasActive);feature.previewTime=-1;ShaderUtil.allowAsyncCompilation=async;RenderTexture.active=oldTarget;
            if(root!=null)foreach(var filter in root.GetComponentsInChildren<MeshFilter>()){UnityEngine.Object.DestroyImmediate(filter.sharedMesh);UnityEngine.Object.DestroyImmediate(filter.GetComponent<Renderer>().sharedMaterial);}
            EditorSceneManager.CloseScene(scene,true);UnityEngine.Object.DestroyImmediate(rt);SceneManager.SetActiveScene(original);
            for(int i=0;i<lights.Length;i++)if(lights[i]!=null)lights[i].enabled=enabled[i];
            RenderSettings.sun=oldSun;RenderSettings.skybox=sky;RenderSettings.ambientProbe=ambient;RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.reflectionIntensity=reflection;
            RenderSettings.ambientMode=ambientMode;RenderSettings.ambientSkyColor=ambientSky;RenderSettings.ambientEquatorColor=ambientEquator;RenderSettings.ambientGroundColor=ambientGround;
        }
    }
}
#endif
