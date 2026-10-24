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

/// <summary>Completed covered-floor review using saved tower transforms; never changes construction stages.</summary>
public static class TowerLightingRepairPreview
{
    public static object Capture(bool neutralMaterial=false,int reviewFloor=0)
    {
        var original=SceneManager.GetActiveScene();
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            original.path!="Assets/_Project/Scenes/Tower.unity" || original.isDirty) throw new InvalidOperationException("Use saved Tower in Edit Mode.");
        var manager=UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var sourceSun=RenderSettings.sun;
        var skybox=RenderSettings.skybox; var ambientMode=RenderSettings.ambientMode;
        var sky=RenderSettings.ambientSkyColor; var equator=RenderSettings.ambientEquatorColor; var ground=RenderSettings.ambientGroundColor;
        var rotation=sourceSun.transform.rotation; var color=sourceSun.color; float intensity=sourceSun.intensity;
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroWaterLightAuthoring.RendererPath);
        var shafts=renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single(); bool oldShafts=shafts.isActive;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var oldTarget=RenderTexture.active; var rt=new RenderTexture(1440,900,24,RenderTextureFormat.ARGBHalf);
        var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var materials=new Dictionary<TowerAssetType,Material>(); var meshes=new RadialFloorMeshCache();
        bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        GameObject Create(string name,params Type[] types) {var go=new GameObject(name,types); go.layer=31; SceneManager.MoveGameObjectToScene(go,scene); return go;}
        try
        {
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox=skybox; RenderSettings.ambientMode=ambientMode;
            RenderSettings.ambientSkyColor=sky; RenderSettings.ambientEquatorColor=equator; RenderSettings.ambientGroundColor=ground;
            var sun=Create("Saved scene sunlight").AddComponent<Light>(); sun.type=LightType.Directional;
            sun.transform.rotation=rotation; sun.color=color; sun.intensity=intensity; sun.shadows=LightShadows.Soft; sun.cullingMask=1<<31; RenderSettings.sun=sun;
            var camera=Create("Covered floor review").AddComponent<Camera>(); camera.scene=scene; camera.enabled=false;
            camera.targetTexture=rt; camera.allowHDR=true; camera.nearClipPlane=.05f; camera.farClipPlane=160; camera.fieldOfView=68;
            camera.cullingMask=1<<31;
            camera.transform.position=new Vector3(-18,2.1f,-83); camera.transform.LookAt(new Vector3(8,2,-96));
            if(reviewFloor>0)
            {
                var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
                float wall=generator.GetWallRadius(reviewFloor),angle=-4*Mathf.Deg2Rad,target=-17*Mathf.Deg2Rad;
                camera.transform.position=new Vector3(Mathf.Cos(angle)*(wall+1.4f),reviewFloor*manager.FloorHeight+1.8f,Mathf.Sin(angle)*(wall+1.4f));
                camera.transform.LookAt(new Vector3(Mathf.Cos(target)*wall,reviewFloor*manager.FloorHeight+.8f,Mathf.Sin(target)*wall));
                camera.farClipPlane=AeroOceanSurface.MinimumFarClip;
                var water=Create("Continuous ocean",typeof(MeshFilter),typeof(MeshRenderer));
                water.transform.position=new Vector3(camera.transform.position.x,-1,camera.transform.position.z);
                water.GetComponent<MeshFilter>().sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(AeroOceanAuthoring.MeshPath);
                water.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(AeroWaterLightAuthoring.WaterPath);
                water.AddComponent<StylizedWater3.WaterObject>();
            }
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing=true;
            if(neutralMaterial)
            {
                camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.magenta;
                camera.GetComponent<UniversalAdditionalCameraData>().renderPostProcessing=false;
            }
            var volume=Create("Saved scene atmosphere").AddComponent<Volume>(); volume.isGlobal=true;
            volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            int count=0;
            foreach(var asset in manager.CachedChunks.SelectMany(x=>x.Assets))
            {
                if(asset.Position.y<Mathf.Max(0,reviewFloor-1)*manager.FloorHeight-.01f || asset.Position.y>(reviewFloor+1)*manager.FloorHeight+1 ||
                    Vector2.Distance(new Vector2(asset.Position.x,asset.Position.z),new Vector2(camera.transform.position.x,camera.transform.position.z))>68) continue;
                var prefab=manager.AssetPrefabs.GetPrefab(asset.AssetType);
                var mesh=prefab.GetComponent<Building>().GetStageMesh(10);
                if(!materials.TryGetValue(asset.AssetType,out var material))
                {
                    material=neutralMaterial ? new Material(Shader.Find("Universal Render Pipeline/Unlit")) : new Material(prefab.GetComponent<Renderer>().sharedMaterial);
                    if(neutralMaterial) material.SetColor("_BaseColor",new Color(.5f,.5f,.5f));
                    materials.Add(asset.AssetType,material);
                    if(asset.AssetType==TowerAssetType.Floor) TowerFloorLayout.ConfigureMaterial(material,true,manager.FloorCenter,true);
                }
                if(asset.AssetType==TowerAssetType.Floor)
                    mesh=meshes.Get(mesh,Vector2.Distance(new Vector2(asset.Position.x,asset.Position.z),manager.FloorCenter),asset.Scale);
                var go=Create(asset.AssetType.ToString(),typeof(MeshFilter),typeof(MeshRenderer));
                go.transform.SetPositionAndRotation(asset.Position,asset.Rotation); go.transform.localScale=asset.Scale;
                go.GetComponent<MeshFilter>().sharedMesh=mesh; go.GetComponent<Renderer>().sharedMaterial=material; count++;
            }
            var canvas=Create("Actual HUD review",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler)).GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=.1f;
            var scaler=canvas.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/Screens/HUD.prefab"),canvas.transform);
            foreach(var transform in canvas.GetComponentsInChildren<Transform>(true)) transform.gameObject.layer=31;
            void Save(string name)
            {
                Canvas.ForceUpdateCanvases(); for(int i=0;i<4;i++) camera.Render(); RenderTexture.active=rt;
                pixels.ReadPixels(new Rect(0,0,1440,900),0,0); pixels.Apply();
                string path=reviewFloor>0?"Documentation/FrutigerAero/Buildings/Previews/Setback-Connected.png":"Documentation/FrutigerAero/Lighting/Previews/"+name+".png";
                System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            if(reviewFloor==0) {shafts.SetActive(false); Save("Tower-Side-Before");}
            shafts.SetActive(true); Save("Tower-Side-After");
            return new {assets=count,sunElevation=rotation.eulerAngles.x,covered=true,source=original.path};
        }
        finally
        {
            shafts.SetActive(oldShafts); RenderTexture.active=oldTarget; ShaderUtil.allowAsyncCompilation=async;
            SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene,true); meshes.Dispose();
            foreach(var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
