using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ArchConstructionPreview
{
    public static object CaptureStages()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output="Documentation/FrutigerAero/Buildings/Previews/";
        System.IO.Directory.CreateDirectory(output);
        var original=SceneManager.GetActiveScene(); var selection=Selection.objects;
        var active=RenderTexture.active; bool asynchronous=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var target=new RenderTexture(640,600,24); var pixels=new Texture2D(640,600,TextureFormat.RGB24,false);
        var scene=EditorSceneManager.NewPreviewScene();
        GameObject Create(string name,params Type[] types) {var go=new GameObject(name,types); SceneManager.MoveGameObjectToScene(go,scene); return go;}
        try
        {
            var camera=Create("Arch stage review",typeof(Camera)).GetComponent<Camera>(); camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.56f,.75f,.86f);
            camera.orthographic=true; camera.orthographicSize=1.5f; camera.nearClipPlane=.03f; camera.farClipPlane=100;
            camera.transform.position=new Vector3(3.8f,2.5f,-6); camera.transform.LookAt(new Vector3(.98f,1.05f,0));
            var key=Create("Soft sunlight",typeof(Light)).GetComponent<Light>(); key.type=LightType.Directional; key.intensity=1.3f;
            key.transform.rotation=Quaternion.Euler(40,-35,0); key.shadows=LightShadows.Soft;
            var fill=Create("Cool fill",typeof(Light)).GetComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.45f;
            fill.transform.rotation=Quaternion.Euler(35,135,0); fill.color=new Color(.8f,.94f,1);
            var probe=Create("Reflections",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>(); probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size=Vector3.one*30;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=new Vector3(0,-.05f,0); ground.transform.localScale=new Vector3(25,.1f,25);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            var arch=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ArchConstructionAuthoring.PrefabPath),scene);
            arch.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); arch.transform.localScale=Vector3.one;
            var building=arch.GetComponent<Building>(); var filter=arch.GetComponent<MeshFilter>();
            var collider=arch.GetComponent<MeshCollider>(); var collisionMesh=collider.sharedMesh;
            for (byte stage=0; stage<=10; stage++)
            {
                filter.sharedMesh=building.GetStageMesh(stage);
                camera.targetTexture=target; camera.Render(); camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,640,600),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(output+$"Arch-Stage-{stage:00}.png",pixels.EncodeToPNG());
            }
            Physics.SyncTransforms();
            bool Hits(float x,float y) => collider.Raycast(new Ray(new Vector3(x,y,-2),Vector3.forward),out _,5);
            var report=new {passageOpen=!Hits(.981f,.65f),legSolid=Hits(0,.65f),spandrelSolid=Hits(.981f,1.75f),
                colliderUnchanged=collider.sharedMesh==collisionMesh && !collider.convex,
                shaderMessages=ShaderUtil.GetShaderMessages(arch.GetComponent<Renderer>().sharedMaterial.shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
                scene=original.path,original.isDirty,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/ArchPreviewInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            RenderTexture.active=active; ShaderUtil.allowAsyncCompilation=asynchronous; EditorSceneManager.ClosePreviewScene(scene);
            Selection.objects=selection; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
