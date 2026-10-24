using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AeroAtmospherePreview
{
    public static object Capture(bool frames=false)
    {
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output="Documentation/FrutigerAero/Lighting/Previews/";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/AquaProjection");
        var original=SceneManager.GetActiveScene(); var scene=EditorSceneManager.NewPreviewScene();
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroWaterLightAuthoring.RendererPath);
        var rays=renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();
        var waterLight=renderer.rendererFeatures.OfType<AeroLightRendererFeature>().Single();
        bool rayActive=rays.isActive, waterActive=waterLight.isActive; float clock=rays.previewTime, waterClock=waterLight.previewTime;
        bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var oldTarget=RenderTexture.active; var target=new RenderTexture(1440,900,24,RenderTextureFormat.ARGBHalf);
        var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);
        var atmosphere=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
        atmosphere.TryGet<Bloom>(out var bloom); float bloomIntensity=bloom.intensity.value;
        GameObject Create(string name) {var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene); return go;}
        Renderer Box(string name,Vector3 position,Vector3 scale)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(go,scene);
            go.name=name; go.transform.position=position; go.transform.localScale=scale;
            var r=go.GetComponent<Renderer>(); r.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat"); return r;
        }
        void Model(string kind,Vector3 position)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/"+kind+".prefab"),scene);
            go.transform.SetPositionAndRotation(position,Quaternion.identity); go.transform.localScale=Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh=go.GetComponent<Building>().GetStageMesh(10);
        }
        void Save(Camera camera,string path)
        {
            for(int i=0;i<4;i++) camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1440,900),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        try
        {
            var camera=Create("Atmosphere review camera").AddComponent<Camera>(); camera.scene=scene; camera.targetTexture=target;
            camera.allowHDR=true; camera.nearClipPlane=.05f; camera.farClipPlane=150; camera.fieldOfView=65;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.3f,.55f,.75f);
            var data=camera.gameObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing=true;
            var volume=Create("Review atmosphere").AddComponent<Volume>(); volume.isGlobal=true; volume.priority=200;
            volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            var sun=Create("Review sun").AddComponent<Light>(); sun.type=LightType.Directional; sun.intensity=2;
            sun.color=new Color(1,.97f,.9f); sun.shadows=LightShadows.Soft; sun.transform.rotation=Quaternion.Euler(10,160,0);
            Box("Pearl walking floor",new Vector3(0,-.12f,3),new Vector3(26,.24f,28));
            var backWall=Box("Shaded interior side wall",new Vector3(-12,2,-2),new Vector3(.2f,4,18));
            var roof=Create("Solid covered tower floor");
            Box("Unbroken roof",new Vector3(0,3.7f,-2),new Vector3(26,.2f,18)).transform.SetParent(roof.transform,true);
            for(int i=-4;i<=4;i++) Box("Side opening pier",new Vector3(i*2.5f,1.8f,7),new Vector3(.65f,3.6f,.5f)).transform.SetParent(roof.transform,true);
            Box("Side opening lintel",new Vector3(0,3.35f,7),new Vector3(26,.7f,.5f)).transform.SetParent(roof.transform,true);
            for(int i=-2;i<=2;i++) {Model("Arch",new Vector3(i*2.1f,0,5)); Model("Pillar",new Vector3(i*2.1f,0,1.7f));}
            camera.transform.position=new Vector3(5,1.5f,-3); camera.transform.LookAt(new Vector3(-2,1.25f,4));
            rays.previewTime=3; waterLight.previewTime=3;
            rays.SetActive(false); Save(camera,output+"Sun-Rays-Before.png");
            rays.SetActive(true); Save(camera,output+"Sun-Rays-After.png");

            roof.SetActive(false); backWall.gameObject.SetActive(false); rays.SetActive(false); waterLight.SetActive(false); sun.intensity=.07f;
            Box("Projection receiving wall",new Vector3(0,1.8f,2.5f),new Vector3(9,3.6f,.16f));
            var lamp=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AeroAtmosphereAuthoring.PrefabPath),scene);
            lamp.transform.position=new Vector3(-.9f,.02f,-.2f);
            var projector=lamp.GetComponent<AeroProjectionLamp>();
            projector.projection.transform.rotation=Quaternion.LookRotation(new Vector3(.1f,.3f,1));
            AeroProjectionLamp.RefreshBudget(lamp.transform.position); projector.Sample(2); projector.projection.enabled=true;
            camera.transform.position=new Vector3(3,1.65f,-3.7f); camera.transform.LookAt(new Vector3(-.4f,1.05f,1));
            bloom.intensity.value=0; Save(camera,output+"Projection-Before-Bloom.png");
            bloom.intensity.value=bloomIntensity; Save(camera,output+"Projection-After.png");
            if(frames) for(int i=0;i<60;i++) {projector.Sample(2+i/20f); Save(camera,$"Temp/AquaProjection/frame-{i:000}.png");}
            return new {output,originalScene=original.path,original.isDirty,playing=EditorApplication.isPlaying,
                shaders=new[]{rays.shaftShader,projector.cookieMaterial.shader,Shader.Find("TowerOfBabel/Buildings/Aquatic Finish")}.Select(s=>new {s.name,messages=ShaderUtil.GetShaderMessages(s).Select(m=>m.message).ToArray()}).ToArray()};
        }
        finally
        {
            rays.SetActive(rayActive); waterLight.SetActive(waterActive); rays.previewTime=clock; waterLight.previewTime=waterClock;
            bloom.intensity.value=bloomIntensity;
            RenderTexture.active=oldTarget; ShaderUtil.allowAsyncCompilation=async; EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            if(Camera.main!=null) AeroProjectionLamp.RefreshBudget(Camera.main.transform.position);
        }
    }
}
