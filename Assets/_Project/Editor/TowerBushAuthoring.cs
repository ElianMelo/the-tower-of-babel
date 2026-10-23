using System;
using System.Linq;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerBushAuthoring
{
    public const string Prefabs="Assets/_Project/Prefabs/Foliage/";
    public const string MaterialPath="Assets/_Project/Environment/Foliage/AeroBushLeaf.mat";
    private static readonly string[] Sizes={"Small","Medium","Large"};

    [MenuItem("Tower of Babel/Foliage/Import and scatter exterior bushes")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path!="Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Tower scene before authoring bushes.");
        System.IO.Directory.CreateDirectory(Prefabs); System.IO.Directory.CreateDirectory("Assets/_Project/Environment/Foliage");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        const string model="Assets/_Project/Models/Foliage/AeroBushes.fbx";
        var importer=(ModelImporter)AssetImporter.GetAtPath(model);
        importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
        importer.importAnimation=false; importer.importCameras=false; importer.importLights=false;
        importer.isReadable=true; importer.addCollider=false; importer.importBlendShapes=false;
        importer.importNormals=ModelImporterNormals.Import; importer.importTangents=ModelImporterTangents.CalculateMikk;
        importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.meshCompression=ModelImporterMeshCompression.Off;
        importer.SaveAndReimport();
        var shader=Shader.Find("TowerOfBabel/Foliage/Buoyant Bush");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material==null) { material=new Material(shader); AssetDatabase.CreateAsset(material,MaterialPath); }
        material.shader=shader; material.enableInstancing=true; EditorUtility.SetDirty(material);
        var meshes=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Mesh>().ToArray();
        var prefabs=new GameObject[3];
        for (int i=0; i<3; i++)
        {
            var root=new GameObject("Bush_"+Sizes[i],typeof(MeshFilter),typeof(MeshRenderer));
            try
            {
                root.GetComponent<MeshFilter>().sharedMesh=meshes.Single(m=>m.name=="AeroBush_"+Sizes[i]);
                root.GetComponent<MeshRenderer>().sharedMaterial=material;
                prefabs[i]=PrefabUtility.SaveAsPrefabAsset(root,Prefabs+root.name+".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var scatter=generator.GetComponent<TowerBushScatter>() ?? generator.gameObject.AddComponent<TowerBushScatter>();
        var serialized=new SerializedObject(scatter); var references=serialized.FindProperty("bushPrefabs"); references.arraySize=3;
        for (int i=0; i<3; i++) references.GetArrayElementAtIndex(i).objectReferenceValue=prefabs[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();
        scatter.Generate(generator); EditorUtility.SetDirty(scatter);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    public static object Inspect()
    {
        var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var scatter=generator.GetComponent<TowerBushScatter>();
        scatter.RebuildBatches();
        var chunks=UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var stairs=chunks.CachedChunks.SelectMany(c=>c.Assets).Where(a=>a.AssetType==TowerAssetType.Stair).ToArray();
        var stairBounds=generator.stairStepObject.GetComponent<MeshFilter>().sharedMesh.bounds;
        int conflicts=0;
        foreach (var plant in scatter.Placements)
        {
            foreach (var stair in stairs)
            {
                var center=Vector3.Scale(stairBounds.center,stair.Scale);
                var extents=Vector3.Scale(stairBounds.extents,stair.Scale);
                float top=stair.Position.y+center.y+extents.y,bottom=stair.Position.y+center.y-extents.y;
                if (top<plant.position.y-1.5f || bottom>plant.position.y+plant.height+.6f) continue;
                Vector3 local=Quaternion.Inverse(stair.Rotation)*(plant.position-stair.Position)-center;
                float x=Mathf.Max(0,Mathf.Abs(local.x)-extents.x), z=Mathf.Max(0,Mathf.Abs(local.z)-extents.z);
                if (x*x+z*z<Mathf.Pow(plant.footprint+scatter.StairClearance,2)) { conflicts++; break; }
            }
        }
        var report=new
        {
            total=scatter.Placements.Count,scatter.BatchCount,
            sizes=scatter.Placements.GroupBy(p=>p.variant).Select(g=>new {variant=Sizes[g.Key],count=g.Count()}).ToArray(),
            floors=scatter.Placements.GroupBy(p=>p.floor).Select(g=>new {floor=g.Key,count=g.Count()}).ToArray(),
            minimumWallGap=scatter.Placements.Min(p=>Vector2.Distance(new Vector2(p.position.x,p.position.z),generator.center)-p.footprint-p.wallRadius),
            minimumLedgeMargin=scatter.Placements.Min(p=>p.floorRadius-Vector2.Distance(new Vector2(p.position.x,p.position.z),generator.center)-p.footprint),
            cachedStairCount=stairs.Length,cachedStairConflicts=conflicts,
            colliderCount=scatter.BushPrefabs.Sum(p=>p.GetComponentsInChildren<Collider>(true).Length),
            assets=scatter.BushPrefabs.Select(p=>new {p.name,triangles=p.GetComponent<MeshFilter>().sharedMesh.triangles.Length/3,
                bounds=p.GetComponent<MeshFilter>().sharedMesh.bounds.ToString(),instancing=p.GetComponent<MeshRenderer>().sharedMaterial.enableInstancing}).ToArray(),
            shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/Foliage/Buoyant Bush")).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
            cachedConstructionAssets=chunks.CachedAssetCount,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed,
            sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty
        };
        System.IO.Directory.CreateDirectory("Documentation/FrutigerAero/Foliage");
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Foliage/BushInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
