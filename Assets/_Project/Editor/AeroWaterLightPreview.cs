using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class AeroWaterLightPreview
{
    public static object Capture(bool frames = false)
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output = "Documentation/FrutigerAero/Lighting/Previews/";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/AeroWaterLight");
        var original = SceneManager.GetActiveScene(); var scene = EditorSceneManager.NewPreviewScene();
        var feature = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroWaterLightAuthoring.RendererPath).rendererFeatures.OfType<AeroLightRendererFeature>().Single();
        bool activeFeature = feature.isActive; float oldClock = feature.previewTime;
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var oldTarget = RenderTexture.active; var target = new RenderTexture(1440,900,24);
        var pixels = new Texture2D(1440,900,TextureFormat.RGB24,false);
        GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        void Save(Camera camera, string path)
        {
            camera.Render(); camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0,0,1440,900),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        try
        {
            var camera = Create("Nearby light review").AddComponent<Camera>(); camera.scene = scene;
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.55f,.75f,.87f); camera.nearClipPlane = .05f; camera.farClipPlane = 200;
            camera.fieldOfView = 55; camera.transform.position = new Vector3(5.2f,2.5f,-6.2f); camera.transform.LookAt(new Vector3(.6f,1.25f,1.8f));
            var key = Create("Daylight").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.1f;
            key.transform.rotation = Quaternion.Euler(38,-35,0); key.shadows = LightShadows.Soft;
            void Box(string name, Vector3 position, Vector3 size)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(go,scene); go.name=name;
                go.transform.position = position; go.transform.localScale = size;
                go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            }
            Box("Pearl floor", new Vector3(0,-.06f,1),new Vector3(18,.12f,18));
            Box("White reflected-light wall",new Vector3(0,2.3f,3.4f),new Vector3(15,4.6f,.15f));
            for (int i = 0; i < 3; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
                var arch = (GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                arch.transform.SetPositionAndRotation(new Vector3(i*2.1f-3,0,2.65f),Quaternion.identity); arch.transform.localScale=Vector3.one;
                arch.GetComponent<MeshFilter>().sharedMesh=arch.GetComponent<Building>().GetStageMesh(10);
            }
            var pillar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab"),scene);
            pillar.transform.SetPositionAndRotation(new Vector3(-2.9f,0,.6f),Quaternion.identity); pillar.transform.localScale=Vector3.one;
            pillar.GetComponent<MeshFilter>().sharedMesh=pillar.GetComponent<Building>().GetStageMesh(10);
            var sand=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/Sand.prefab"),scene);
            sand.transform.position=new Vector3(-.8f,.03f,-.4f);
            var kiln=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Processor/GlassFurnace.prefab"),scene);
            kiln.transform.position=new Vector3(2.25f,.03f,.4f);
            feature.previewTime=2.5f; feature.SetActive(false);
            Save(camera,output+"Nearby-Before.png");
            feature.SetActive(true);
            Save(camera,output+"Nearby-After.png");
            if (frames) for (int i=0;i<60;i++) {feature.previewTime=2.5f+i/20f; Save(camera,$"Temp/AeroWaterLight/frame-{i:000}.png");}

            // Review the actual lake, its shore, existing water shader and the scene's real atmosphere.
            var main=Camera.main; var position=main.transform.position; var rotation=main.transform.rotation;
            var mainTarget=main.targetTexture; float fov=main.fieldOfView;
            var landscape=UnityEngine.Object.FindFirstObjectByType<AeroTowerTerrain>();
            var water=UnityEngine.Object.FindFirstObjectByType<StylizedWater3.WaterObject>(); var waterMaterial=water.material;
            try
            {
                var lake=landscape.Lakes[0]; var point=new Vector3(landscape.ClearingCenter.x+lake.offset.x,landscape.WaterElevation,landscape.ClearingCenter.y+lake.offset.y);
                Vector3 approach=new Vector3(-lake.offset.x,0,-lake.offset.y).normalized;
                main.targetTexture=target; main.fieldOfView=65;
                main.transform.position=point+approach*42+Vector3.up*3.4f; main.transform.LookAt(point-approach*12);
                feature.SetActive(false);
                water.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Stylized Water 3/Materials/StylizedWater3_Default.mat"); water.meshRenderer.sharedMaterial=water.material;
                Save(main,output+"Lake-Before.png");
                feature.SetActive(true); feature.previewTime=2.5f;
                water.material=waterMaterial; water.meshRenderer.sharedMaterial=waterMaterial;
                Save(main,output+"Lake-After.png");
            }
            finally
            {
                main.transform.SetPositionAndRotation(position,rotation); main.targetTexture=mainTarget; main.fieldOfView=fov;
                water.material=waterMaterial; water.meshRenderer.sharedMaterial=waterMaterial;
            }
            return new { output, originalScene=original.path, original.isDirty, playing=EditorApplication.isPlaying,
                shaderMessages=ShaderUtil.GetShaderMessages(feature.lightShader).Select(m=>m.message).ToArray() };
        }
        finally
        {
            feature.SetActive(activeFeature); feature.previewTime=oldClock; RenderTexture.active=oldTarget;
            ShaderUtil.allowAsyncCompilation=asynchronous; EditorSceneManager.ClosePreviewScene(scene);
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
