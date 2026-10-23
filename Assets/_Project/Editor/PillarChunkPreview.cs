using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Render the actual incoming-piece controller without Play Mode or progression changes.</summary>
public static class PillarChunkPreview
{
    public static object Capture(bool frames=true,string kind="Pillar")
    {
        if (!new[]{"Pillar","Arch","Floor_Tile","Step_Tile"}.Contains(kind)) throw new ArgumentException("Choose a building kit asset.");
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output="Documentation/FrutigerAero/Buildings/Previews/";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/"+kind+"Chunks");
        var original=SceneManager.GetActiveScene(); var selection=Selection.objects;
        var active=RenderTexture.active; bool asynchronous=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var target=new RenderTexture(1200,900,24); var pixels=new Texture2D(1200,900,TextureFormat.RGB24,false);
        var scene=EditorSceneManager.NewPreviewScene();
        var radialMeshes=new RadialFloorMeshCache(); Material previewMaterial=null;
        GameObject Create(string name,params Type[] types)
        {
            var go=new GameObject(name,types); SceneManager.MoveGameObjectToScene(go,scene); return go;
        }
        try
        {
            var camera=Create("Chunk arrival review",typeof(Camera)).GetComponent<Camera>(); camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.56f,.75f,.86f);
            camera.orthographic=true; camera.orthographicSize=2.9f; camera.nearClipPlane=.03f; camera.farClipPlane=100;
            camera.transform.position=new Vector3(4.6f,3.5f,-7.5f); camera.transform.LookAt(new Vector3(0,2,0));
            if (kind=="Arch") {camera.orthographicSize=2.4f; camera.transform.position=new Vector3(4.2f,2.8f,-6); camera.transform.LookAt(new Vector3(.98f,1.1f,0));}
            if (kind=="Floor_Tile") {camera.orthographicSize=3.5f; camera.transform.position=new Vector3(7,6,4); camera.transform.LookAt(new Vector3(2,0,-2));}
            if (kind=="Step_Tile") {camera.orthographicSize=3f; camera.transform.position=new Vector3(6,3.8f,4); camera.transform.LookAt(new Vector3(2,.2f,-.7f));}
            var light=Create("Sunlight",typeof(Light)).GetComponent<Light>(); light.type=LightType.Directional; light.intensity=1.3f;
            light.transform.rotation=Quaternion.Euler(40,-35,0); light.shadows=LightShadows.Soft;
            var fill=Create("Cool fill",typeof(Light)).GetComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.45f;
            fill.transform.rotation=Quaternion.Euler(35,135,0); fill.color=new Color(.8f,.94f,1);
            var probe=Create("Reflections",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>(); probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size=Vector3.one*30;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=new Vector3(0,-.05f,0); ground.transform.localScale=new Vector3(25,.1f,25);
            if (kind=="Floor_Tile" || kind=="Step_Tile") ground.transform.position=new Vector3(0,-.25f,0);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            var prefabPath=kind=="Arch" ? ArchConstructionAuthoring.PrefabPath : kind=="Pillar" ? PillarConstructionAuthoring.PrefabPath : DeckConstructionAuthoring.PrefabPath(kind);
            var pillar=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath),scene);
            pillar.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); pillar.transform.localScale=Vector3.one;
            if (kind=="Step_Tile") pillar.transform.localScale=new Vector3(1,1,.35f);
            var building=pillar.GetComponent<Building>(); var filter=pillar.GetComponent<MeshFilter>(); var renderer=pillar.GetComponent<MeshRenderer>();
            var motion=pillar.GetComponent<PillarConstructionMotion>(); const byte stage=2;
            filter.sharedMesh=building.GetStageMesh(stage);
            if (kind=="Floor_Tile")
            {
                var sector=TowerFloorLayout.Build(Vector2.zero,0,12,4,4).First(); pillar.transform.localScale=sector.Scale;
                previewMaterial=new Material(renderer.sharedMaterial); TowerFloorLayout.ConfigureMaterial(previewMaterial,true,Vector2.zero,true);
                renderer.sharedMaterial=previewMaterial;
                filter.sharedMesh=radialMeshes.Get(filter.sharedMesh,12,sector.Scale);
                pillar.GetComponent<MeshCollider>().sharedMesh=radialMeshes.Get(building.GetStageMesh(10),12,sector.Scale);
                motion.ConfigureRadialFloor(radialMeshes,12,sector.Scale);
            }
            var previous=filter.sharedMesh;
            var collider=pillar.GetComponent<Collider>(); var colliderBefore=collider.bounds;
            var poseReports=new System.Collections.Generic.List<object>();
            void Save(float progress,string path)
            {
                motion.SampleEditorFlight(stage,progress);
                camera.targetTexture=target; camera.Render(); camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1200,900),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            foreach (var sample in new[]{new {name="Forming",progress=.11f},new {name="Flying",progress=.5f},new {name="Landed",progress=1f}})
            {
                Save(sample.progress,output+kind+"-Chunks-"+sample.name+".png");
                var parts=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<MeshRenderer>()).Where(r=>r.name=="Forming chunk").ToArray();
                var properties=new MaterialPropertyBlock();
                poseReports.Add(new {sample.name,visiblePieces=parts.Count(r=>r.enabled),
                    dissolveOnlyOnIncoming=parts.All(r=>{r.GetPropertyBlock(properties); return properties.GetFloat("_BuildMode")==1;}),
                    sourceMeshUnchanged=filter.sharedMesh==previous});
            }
            if (frames)
                for (int i=0; i<90; i++) Save(Mathf.Clamp01(i/30f/2.4f),$"Temp/{kind}Chunks/frame-{i:000}.png");
            bool fixedCollider=collider.bounds.center==colliderBefore.center && collider.bounds.size==colliderBefore.size;
            motion.ResetImmediately();
            var report=new {kind,radialFloor=kind=="Floor_Tile",poseReports,fixedCollider,sourceRestored=!renderer.forceRenderingOff,
                shaderMessages=ShaderUtil.GetShaderMessages(renderer.sharedMaterial.shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
                scene=original.path,original.isDirty,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/"+kind+"ChunkMotionInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            RenderTexture.active=active; ShaderUtil.allowAsyncCompilation=asynchronous; EditorSceneManager.ClosePreviewScene(scene);
            Selection.objects=selection; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            radialMeshes.Dispose(); if (previewMaterial!=null) UnityEngine.Object.DestroyImmediate(previewMaterial);
        }
    }
}
