using System;
using System.Collections.Generic;
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

public static class AeroIslandReview
{
    public const string Output = "Documentation/FrutigerAero/Lighting/Previews/";
    public static object Capture()
    {
        var original = SceneManager.GetActiveScene();
        if (EditorApplication.isPlayingOrWillChangePlaymode || original.path != "Assets/_Project/Scenes/Tower.unity" || original.isDirty) throw new InvalidOperationException("Use the saved Tower in Edit Mode.");
        System.IO.Directory.CreateDirectory(Output);
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var sourceFx = UnityEngine.Object.FindFirstObjectByType<AeroTowerAtmosphere>();
        var sourceCycle = sourceFx.cycle;
        var skybox = RenderSettings.skybox;
        var sourceCamera = Camera.main;
        var sourcePosition = sourceCamera.transform.position; var sourceRotation = sourceCamera.transform.rotation;
        var sourceTarget = sourceCamera.targetTexture;
        var ocean = UnityEngine.Object.FindFirstObjectByType<AeroOceanSurface>(); var oceanPosition = ocean.transform.position;
        var rt = new RenderTexture(1440,900,24,RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(1440,900,TextureFormat.RGB24,false);
        var oldRT = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var scene = default(Scene); var meshes = new RadialFloorMeshCache(); var materials = new Dictionary<TowerAssetType,Material>();
        Material skyClone = null;
        var sourceLights=original.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Light>(true)).ToArray();
        var lightStates=sourceLights.Select(l=>l.enabled).ToArray();
        void Save(Camera camera, string name)
        {
            camera.targetTexture = rt;
            for (int i=0;i<4;i++) camera.Render();
            RenderTexture.active=rt; pixels.ReadPixels(new Rect(0,0,1440,900),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(Output+name+".png",pixels.EncodeToPNG());
        }
        try
        {
            sourceCycle.ApplyTime(.4f);
            sourceCamera.transform.position=new Vector3(610,520,-610); sourceCamera.transform.LookAt(new Vector3(0,0,0));
            Save(sourceCamera,"Island-Overview"); sourceCamera.targetTexture=sourceTarget;
            foreach(var light in sourceLights) light.enabled=false;
            scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
            GameObject Create(string name,params Type[] types) {var go=new GameObject(name,types);go.layer=31; SceneManager.MoveGameObjectToScene(go,scene);return go;}
            if(skybox!=null) {skyClone=new Material(skybox);RenderSettings.skybox=skyClone;} RenderSettings.fog=false;
            var sun=Create("Review sun",typeof(Light)).GetComponent<Light>(); sun.type=LightType.Directional; sun.shadows=LightShadows.Soft; sun.cullingMask=1<<31;
            var camera=Create("Review camera",typeof(Camera),typeof(UniversalAdditionalCameraData)).GetComponent<Camera>();
            camera.scene=scene; camera.enabled=false; camera.cullingMask=1<<31; camera.nearClipPlane=.05f; camera.farClipPlane=65536; camera.allowHDR=true; camera.fieldOfView=68;
            var cd=camera.GetComponent<UniversalAdditionalCameraData>(); cd.renderPostProcessing=true; cd.volumeLayerMask=1<<31; cd.requiresDepthTexture=true;
            var volume=Create("Global atmosphere",typeof(Volume)).GetComponent<Volume>(); volume.isGlobal=true;
            volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            var fxRoot=UnityEngine.Object.Instantiate(sourceFx.gameObject); fxRoot.name="Review circumference atmosphere"; SceneManager.MoveGameObjectToScene(fxRoot,scene);
            foreach(var tr in fxRoot.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer=31;
            var fx=fxRoot.GetComponent<AeroTowerAtmosphere>(); var cycle=fxRoot.GetComponent<AeroDayNightCycle>(); cycle.sun=sun; cycle.moon.cullingMask=1<<31;
            var water=Create("Review ocean",typeof(MeshFilter),typeof(MeshRenderer)); water.transform.position=new Vector3(0,-1,0);
            water.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(AeroOceanAuthoring.MeshPath);
            water.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(AeroWaterLightAuthoring.WaterPath);
            water.AddComponent<StylizedWater3.WaterObject>();
            var roofObjects=new List<GameObject>(); int count=0;
            foreach(var asset in manager.CachedChunks.SelectMany(c=>c.Assets))
            {
                if(asset.Position.y < -.01f || asset.Position.y > manager.FloorHeight+.25f) continue;
                var prefab=manager.AssetPrefabs.GetPrefab(asset.AssetType); var mesh=prefab.GetComponent<Building>().GetStageMesh(10);
                if(!materials.TryGetValue(asset.AssetType,out var material))
                {
                    material=new Material(prefab.GetComponent<Renderer>().sharedMaterial); materials.Add(asset.AssetType,material);
                    if(asset.AssetType.IsFloor()) TowerFloorLayout.ConfigureMaterial(material,true,manager.FloorCenter,true);
                }
                if(asset.AssetType.IsFloor()) mesh=meshes.Get(mesh,Vector2.Distance(new Vector2(asset.Position.x,asset.Position.z),manager.FloorCenter),asset.Scale);
                var go=Create(asset.AssetType.ToString(),typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetPositionAndRotation(asset.Position,asset.Rotation); go.transform.localScale=asset.Scale;
                go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<Renderer>().sharedMaterial=material;
                if(asset.Position.y > 5.9f || asset.AssetType==TowerAssetType.Arch || asset.AssetType==TowerAssetType.Pillar || asset.AssetType==TowerAssetType.Stair) roofObjects.Add(go);
                count++;
            }
            cycle.ApplyTime(.36f); camera.transform.position=new Vector3(0,155,0); camera.transform.rotation=Quaternion.Euler(90,0,0); camera.orthographic=true; camera.orthographicSize=103;
            foreach(var go in roofObjects) go.SetActive(false);
            if(fx.beams!=null)fx.beams.gameObject.SetActive(false); fx.fireflies.gameObject.SetActive(false); Save(camera,"Glass-River-Blueprint");
            foreach(var go in roofObjects) go.SetActive(true);
            if(fx.beams!=null)fx.beams.gameObject.SetActive(true); fx.fireflies.gameObject.SetActive(true); camera.orthographic=false;
            foreach(int angle in new[]{0,90,180,270})
            {
                float a=angle*Mathf.Deg2Rad; Vector3 radial=new(Mathf.Cos(a),0,Mathf.Sin(a));
                camera.transform.position=radial*81+Vector3.up*2.15f; camera.transform.LookAt(radial*93+new Vector3(-radial.z,0,radial.x)*15+Vector3.up*2.9f);
                cycle.ApplyTime(.29f); fx.Sample(camera.transform.position,4); fx.fireflies.Simulate(12,true,true,false);
                Save(camera,"Tower-Rays-"+angle);
            }
            cycle.ApplyTime(.04f); if(skyClone!=null&&skyClone.HasProperty("_Exposure"))skyClone.SetFloat("_Exposure",cycle.profile.nightExposure);
            fx.Sample(camera.transform.position,8); Save(camera,"Tower-Night");
            return new {assets=count,files=System.IO.Directory.GetFiles(Output,"*.png").Select(System.IO.Path.GetFileName).ToArray()};
        }
        finally
        {
            sourceCamera.targetTexture=sourceTarget; sourceCamera.transform.SetPositionAndRotation(sourcePosition,sourceRotation); ocean.transform.position=oceanPosition;
            RenderTexture.active=oldRT; ShaderUtil.allowAsyncCompilation=async;
            if(scene.IsValid()) {SceneManager.SetActiveScene(original);EditorSceneManager.CloseScene(scene,true);}
            meshes.Dispose(); foreach(var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels); if(skyClone!=null) UnityEngine.Object.DestroyImmediate(skyClone);
            sourceCycle.ApplyTime(sourceCycle.profile.startTime);
            for(int i=0;i<sourceLights.Length;i++) if(sourceLights[i]!=null)sourceLights[i].enabled=lightStates[i];
        }
    }
}
