using System;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class ResourceWorkshopPreview
{
    public static object Capture(bool frames=true)
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output="Documentation/FrutigerAero/Resources/Previews/";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/ProcessorWork");
        var original=SceneManager.GetActiveScene(); var selection=Selection.objects;
        var source=UnityEngine.Object.FindObjectsByType<ResourceProcessor>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(p=>p.gameObject.scene==original);
        var main=Camera.main; var mainPosition=main.transform.position; var mainRotation=main.transform.rotation;
        var mainTarget=main.targetTexture; float mainFov=main.fieldOfView,mainFar=main.farClipPlane; bool mainOrtho=main.orthographic;
        var oldActive=RenderTexture.active; bool async=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var target=new RenderTexture(1600,1000,24); var pixels=new Texture2D(1600,1000,TextureFormat.RGB24,false);
        var scene=EditorSceneManager.NewPreviewScene();
        void Save(Camera camera,string path)
        {
            camera.targetTexture=target; camera.Render(); camera.Render(); RenderTexture.active=target;
            pixels.ReadPixels(new Rect(0,0,1600,1000),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        GameObject Create(string name)
        {
            var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene); return go;
        }
        try
        {
            Vector3 center=source.transform.position+Vector3.right*1.4f+Vector3.down*.55f;
            main.orthographic=false; main.fieldOfView=53; main.farClipPlane=1800;
            main.transform.position=center+new Vector3(3.5f,2.8f,-5);
            main.transform.LookAt(center); Save(main,output+"Stone-Processor-World.png");
            main.transform.SetPositionAndRotation(mainPosition,mainRotation); main.targetTexture=mainTarget;
            main.orthographic=mainOrtho; main.fieldOfView=mainFov; main.farClipPlane=mainFar;
            var camera=Create("Resource workshop preview camera").AddComponent<Camera>();
            camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.66f,.81f,.9f);
            camera.orthographic=true; camera.orthographicSize=2.05f; camera.nearClipPlane=.03f; camera.farClipPlane=100;
            camera.transform.position=new Vector3(3.4f,2.8f,-6); camera.transform.LookAt(new Vector3(0,.95f,0));
            var key=Create("Soft sunlight").AddComponent<Light>(); key.type=LightType.Directional; key.intensity=1.3f;
            key.transform.rotation=Quaternion.Euler(40,-35,0); key.shadows=LightShadows.Soft; key.shadowStrength=.65f;
            var fill=Create("Cool fill").AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.5f;
            fill.transform.rotation=Quaternion.Euler(35,135,0); fill.color=new Color(.8f,.94f,1);
            var probe=Create("Studio reflections").AddComponent<ReflectionProbe>(); probe.mode=ReflectionProbeMode.Custom;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr");
            probe.size=Vector3.one*40;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=new Vector3(0,-.05f,1); ground.transform.localScale=new Vector3(30,.1f,30);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            var stone=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResourceWorkshopAuthoring.StonePrefab),scene);
            stone.transform.position=new Vector3(-1.65f,1.5f,0);
            var processor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResourceWorkshopAuthoring.ProcessorPrefab),scene);
            processor.transform.position=new Vector3(1.15f,1.5f,0);
            var animation=processor.GetComponent<ProcessorWorkAnimation>();
            var collider=processor.GetComponentInChildren<Collider>(); Bounds colliderBefore=collider.bounds;
            var states=new System.Collections.Generic.List<object>();
            var feed=processor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FeedStone");
            var result=processor.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="OutputStone");
            void Pose(string label,float seconds,float blend,float progress,float completion=-1)
            {
                animation.SampleEditorPose(seconds,blend,progress,completion);
                states.Add(new {label,feedVisible=feed.gameObject.activeSelf,outputVisible=result.gameObject.activeSelf});
                Save(camera,output+"Stone-Processor-"+label+".png");
            }
            Pose("Idle",0,0,0);
            Pose("Entering",.12f,1,.04f);
            Pose("Working",1.1f,1,.45f);
            Pose("Consumed",2.5f,1,.98f);
            Pose("Cancelled",2.5f,0,.6f);
            Pose("Complete",2.7f,0,1,.2f);
            Pose("Collected",4,0,1,1.3f);
            if (frames)
            {
                for (int frame=0; frame<96; frame++)
                {
                    float t=frame/24f;
                    float blend=t<2.7f ? Mathf.SmoothStep(0,1,t/.25f) : 1-Mathf.SmoothStep(0,1,(t-2.7f)/.25f);
                    animation.SampleEditorPose(Mathf.Min(t,2.95f),blend,Mathf.Clamp01(t/2.7f),t>=2.7f ? t-2.7f : -1);
                    Save(camera,$"Temp/ProcessorWork/frame-{frame:000}.png");
                }
            }
            bool fixedCollider=collider.bounds.center==colliderBefore.center && collider.bounds.size==colliderBefore.size;
            animation.ResetImmediately();
            var report=new {states,fixedCollider,originalScene=original.path,original.isDirty,playing=EditorApplication.isPlaying,
                compileFailed=EditorUtility.scriptCompilationFailed,
                shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("Universal Render Pipeline/Lit")).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray()};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/Resources/ProcessorMotionInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            main.transform.SetPositionAndRotation(mainPosition,mainRotation); main.targetTexture=mainTarget;
            main.orthographic=mainOrtho; main.fieldOfView=mainFov; main.farClipPlane=mainFar;
            RenderTexture.active=oldActive; ShaderUtil.allowAsyncCompilation=async;
            EditorSceneManager.ClosePreviewScene(scene); Selection.objects=selection;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
