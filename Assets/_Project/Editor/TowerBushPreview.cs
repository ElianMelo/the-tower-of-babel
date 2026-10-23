using System;
using System.Linq;
using TMPro;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Samples the actual meshes, wind shader and cached terrace geometry without Play Mode.</summary>
public static class TowerBushPreview
{
    public static object Capture(bool windFrames=true)
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var scatter=generator.GetComponent<TowerBushScatter>();
        var chunks=UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var original=SceneManager.GetActiveScene(); var selection=Selection.objects;
        var scene=EditorSceneManager.NewPreviewScene();
        var material=AssetDatabase.LoadAssetAtPath<Material>(TowerBushAuthoring.MaterialPath);
        float time=material.GetFloat("_WindTime");
        bool asynchronous=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var previous=RenderTexture.active;
        var target=new RenderTexture(1600,900,24); var pixels=new Texture2D(1600,900,TextureFormat.RGB24,false);
        var floorMeshes=new RadialFloorMeshCache();
        var floorMaterial=new Material(generator.floorTileObject.GetComponent<Renderer>().sharedMaterial);
        TowerFloorLayout.ConfigureMaterial(floorMaterial,true,generator.center,true);
        const string output="Documentation/FrutigerAero/Foliage/Previews/";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/BushWind");
        GameObject Create(string name,Transform parent=null)
        {
            var go=new GameObject(name); SceneManager.MoveGameObjectToScene(go,scene);
            if (parent!=null) go.transform.SetParent(parent,false);
            return go;
        }
        GameObject Model(string name,Mesh mesh,Material mat,Vector3 position,Quaternion rotation,Vector3 scale,Transform parent)
        {
            var go=Create(name,parent); go.transform.SetPositionAndRotation(position,rotation); go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh; go.AddComponent<MeshRenderer>().sharedMaterial=mat;
            return go;
        }
        try
        {
            var camera=Create("Foliage preview camera").AddComponent<Camera>();
            camera.scene=scene; camera.targetTexture=target; camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.65f,.8f,.89f); camera.nearClipPlane=.03f; camera.farClipPlane=400;
            var key=Create("Soft sunlight").AddComponent<Light>(); key.type=LightType.Directional; key.intensity=1.3f;
            key.transform.rotation=Quaternion.Euler(45,-25,0); key.shadows=LightShadows.Soft;
            var fill=Create("Sky fill").AddComponent<Light>(); fill.type=LightType.Directional; fill.intensity=.5f;
            fill.color=new Color(.8f,.92f,1); fill.transform.rotation=Quaternion.Euler(40,130,0);
            var lineup=Create("Three bush sizes");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
            for (int i=0; i<3; i++)
            {
                var prefab=scatter.BushPrefabs[i];
                Model(prefab.name,prefab.GetComponent<MeshFilter>().sharedMesh,material,new Vector3(i*1.8f-1.8f,0,0),Quaternion.identity,Vector3.one,lineup.transform);
                var label=Create("Size label "+i,lineup.transform).AddComponent<TextMeshPro>();
                label.font=font; label.text=new[]{"Small","Medium","Large"}[i]; label.fontSize=2.2f;
                label.alignment=TextAlignmentOptions.Center; label.color=new Color(.04f,.19f,.25f,1);
                label.rectTransform.sizeDelta=new Vector2(1.6f,.4f); label.transform.position=new Vector3(i*1.8f-1.8f,.12f,-.9f);
            }
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.SetParent(lineup.transform,false); ground.transform.position=new Vector3(0,-.05f,1);
            ground.transform.localScale=new Vector3(30,.1f,30); UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            camera.orthographic=true; camera.orthographicSize=2.1f;
            camera.transform.position=new Vector3(3,2.5f,-7); camera.transform.LookAt(new Vector3(0,.4f,0));
            void Save(string path)
            {
                camera.Render(); camera.Render(); RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            material.SetFloat("_WindTime",1.2f); Save(output+"Bush-Sizes.png");
            if (windFrames)
                for (int frame=0; frame<72; frame++) { material.SetFloat("_WindTime",frame/12f); Save($"Temp/BushWind/frame-{frame:000}.png"); }
            lineup.SetActive(false);
            var terrace=Create("Actual exterior terrace");
            var focus=scatter.Placements.OrderByDescending(p=>scatter.Placements.Count(q=>q.floor==p.floor && (q.position-p.position).sqrMagnitude<49)).First();
            var plants=scatter.Placements.Where(p=>p.floor==focus.floor && (p.position-focus.position).sqrMagnitude<225).ToArray();
            foreach (var plant in plants)
            {
                var prefab=scatter.BushPrefabs[plant.variant];
                Model(prefab.name,prefab.GetComponent<MeshFilter>().sharedMesh,material,plant.position,Quaternion.Euler(0,plant.yaw,0),plant.scale,terrace.transform);
            }
            int geometry=0;
            foreach (var asset in chunks.CachedChunks.SelectMany(c=>c.Assets))
            {
                Vector2 offset=new(asset.Position.x-focus.position.x,asset.Position.z-focus.position.z);
                if (offset.sqrMagnitude>400) continue;
                float baseY=focus.floor*generator.heighIncrease;
                if (asset.AssetType==TowerAssetType.Floor && Mathf.Abs(asset.Position.y-(baseY+.2f))>.05f) continue;
                if (asset.AssetType==TowerAssetType.Pillar && Mathf.Abs(asset.Position.y-baseY)>.05f) continue;
                if (asset.AssetType==TowerAssetType.Arch && Mathf.Abs(asset.Position.y-(baseY+generator.yOffset))>.05f) continue;
                if (asset.AssetType==TowerAssetType.Stair && (asset.Position.y<baseY-3 || asset.Position.y>baseY+3)) continue;
                var prefab=generator.GetModelPrefab(asset.AssetType);
                var mesh=prefab.GetComponent<Building>().GetStageMesh(10);
                var mat=prefab.GetComponent<Renderer>().sharedMaterial;
                if (asset.AssetType==TowerAssetType.Floor)
                {
                    float radius=Vector2.Distance(new Vector2(asset.Position.x,asset.Position.z),generator.center);
                    mesh=floorMeshes.Get(mesh,radius,asset.Scale); mat=floorMaterial;
                }
                Model(asset.AssetType.ToString(),mesh,mat,asset.Position,asset.Rotation,asset.Scale,terrace.transform); geometry++;
            }
            Vector3 radial=(focus.position-new Vector3(generator.center.x,focus.position.y,generator.center.y)).normalized;
            Vector3 tangent=Vector3.Cross(Vector3.up,radial);
            camera.orthographic=false; camera.fieldOfView=48;
            camera.transform.position=focus.position+radial*7+tangent*4+Vector3.up*3;
            camera.transform.LookAt(focus.position+Vector3.up*.5f);
            material.SetFloat("_WindTime",1.2f); Save(output+"Bush-Terrace.png");
            camera.orthographic=true; camera.orthographicSize=8;
            camera.transform.position=focus.position+Vector3.up*17; camera.transform.rotation=Quaternion.Euler(90,0,0);
            Save(output+"Bush-Terrace-Overhead.png");
            return new {previewFloor=focus.floor,visibleBushes=plants.Length,constructionMeshes=geometry,originalScene=original.path,original.isDirty,playing=EditorApplication.isPlaying};
        }
        finally
        {
            material.SetFloat("_WindTime",time); RenderTexture.active=previous; ShaderUtil.allowAsyncCompilation=asynchronous;
            EditorSceneManager.ClosePreviewScene(scene); Selection.objects=selection; floorMeshes.Dispose();
            UnityEngine.Object.DestroyImmediate(floorMaterial); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
