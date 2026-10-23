using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class DeckConstructionPreview
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
        var scene=EditorSceneManager.NewPreviewScene(); var radialMeshes=new RadialFloorMeshCache(); Material floorMaterial=null;
        GameObject Create(string name,params Type[] types) {var go=new GameObject(name,types); SceneManager.MoveGameObjectToScene(go,scene); return go;}
        try
        {
            var camera=Create("Deck stage review",typeof(Camera)).GetComponent<Camera>(); camera.scene=scene;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.56f,.75f,.86f);
            camera.orthographic=true; camera.nearClipPlane=.03f; camera.farClipPlane=100;
            var key=Create("Soft sunlight",typeof(Light)).GetComponent<Light>(); key.type=LightType.Directional; key.intensity=1.3f;
            key.transform.rotation=Quaternion.Euler(40,-35,0); key.shadows=LightShadows.Soft;
            var fill=Create("Cool fill",typeof(Light)).GetComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.45f;
            fill.transform.rotation=Quaternion.Euler(35,135,0); fill.color=new Color(.8f,.94f,1);
            var probe=Create("Reflections",typeof(ReflectionProbe)).GetComponent<ReflectionProbe>(); probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;
            probe.customBakedTexture=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size=Vector3.one*30;
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=new Vector3(0,-.25f,0); ground.transform.localScale=new Vector3(25,.1f,25);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            var reports=new System.Collections.Generic.List<object>();
            foreach (string kind in new[]{"Floor_Tile","Step_Tile"})
            {
                bool radial=kind=="Floor_Tile";
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(DeckConstructionAuthoring.PrefabPath(kind));
                var deck=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);
                deck.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity); deck.transform.localScale=prefab.transform.localScale;
                var building=deck.GetComponent<Building>(); var filter=deck.GetComponent<MeshFilter>(); var collider=deck.GetComponent<Collider>();
                var scale=radial ? TowerFloorLayout.Build(Vector2.zero,0,12,4,4).First().Scale : deck.transform.localScale;
                deck.transform.localScale=scale;
                if (radial)
                {
                    var renderer=deck.GetComponent<Renderer>(); floorMaterial=new Material(renderer.sharedMaterial);
                    TowerFloorLayout.ConfigureMaterial(floorMaterial,true,Vector2.zero,true); renderer.sharedMaterial=floorMaterial;
                    ((MeshCollider)collider).sharedMesh=radialMeshes.Get(building.GetStageMesh(10),12,scale);
                }
                camera.orthographicSize=radial ? 2.9f : 2.2f;
                camera.transform.position=radial ? new Vector3(7,5.8f,4) : new Vector3(6,3.8f,4);
                camera.transform.LookAt(radial ? new Vector3(2,-.05f,-2) : new Vector3(2,-.05f,-.7f));
                Physics.SyncTransforms();
                var boundsBefore=collider.bounds; var collisionMesh=radial ? ((MeshCollider)collider).sharedMesh : null;
                var collisionSamples=new System.Collections.Generic.List<object>();
                for (byte stage=0; stage<=10; stage++)
                {
                    var mesh=building.GetStageMesh(stage); filter.sharedMesh=radial ? radialMeshes.Get(mesh,12,scale) : mesh;
                    camera.targetTexture=target; camera.Render(); camera.Render(); RenderTexture.active=target;
                    pixels.ReadPixels(new Rect(0,0,640,600),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(output+$"{kind}-Stage-{stage:00}.png",pixels.EncodeToPNG());
                    Vector3 point=radial ? deck.transform.TransformPoint(TowerFloorLayout.Bend(new Vector3(2,0,-2),12,scale)) : new Vector3(2,0,-.7f);
                    bool hit=collider.Raycast(new Ray(point+Vector3.up*2,Vector3.down),out RaycastHit contact,4);
                    collisionSamples.Add(new {stage,walkSurfaceHit=hit,height=hit ? contact.point.y : float.NaN});
                }
                reports.Add(new {kind,radial,collisionSamples,colliderBoundsFixed=collider.bounds.center==boundsBefore.center && collider.bounds.size==boundsBefore.size,
                    collisionMeshFixed=!radial || ((MeshCollider)collider).sharedMesh==collisionMesh});
                UnityEngine.Object.DestroyImmediate(deck);
            }
            var library=AssetDatabase.LoadAssetAtPath<PillarChunkLibrary>(DeckConstructionAuthoring.Art+"Floor_TileChunks.asset");
            var radialReports=new System.Collections.Generic.List<object>();
            foreach (float radius in new[]{4f,12f})
            {
                var scale=TowerFloorLayout.Build(Vector2.zero,0,radius,4,4).First().Scale;
                var meshes=library.Pieces.Select(p=>radialMeshes.Get(p.Mesh,radius,scale)).ToArray();
                radialReports.Add(new {radius,chunkCount=meshes.Length,
                    nonfiniteVertices=meshes.Sum(m=>m.vertices.Count(v=>!float.IsFinite(v.x) || !float.IsFinite(v.y) || !float.IsFinite(v.z))),
                    patchCoordinatesRetained=meshes.All(m=>m.uv3.Length==m.vertexCount),normalsRetained=meshes.All(m=>m.normals.Length==m.vertexCount),
                    tangentsRetained=meshes.All(m=>m.tangents.Length==m.vertexCount)});
            }
            var report=new {assets=reports,radialReports,scene=original.path,original.isDirty,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/DeckPreviewInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            RenderTexture.active=active; ShaderUtil.allowAsyncCompilation=asynchronous; EditorSceneManager.ClosePreviewScene(scene);
            Selection.objects=selection; UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            radialMeshes.Dispose(); if (floorMaterial!=null) UnityEngine.Object.DestroyImmediate(floorMaterial);
        }
    }
}
