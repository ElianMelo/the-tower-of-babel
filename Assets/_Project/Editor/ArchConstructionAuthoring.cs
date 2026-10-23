using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>Apply the pillar's chunk structure to the existing arch, preserving its completed collider.</summary>
public static class ArchConstructionAuthoring
{
    public const string PrefabPath="Assets/_Project/Prefabs/Buildings/Arch.prefab";
    public const string Art="Assets/_Project/Models/Buildings/FrutigerAero/Construction/";

    [MenuItem("Tower of Babel/Buildings/Apply arch construction chunks")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var otherPaths=new[]{"Pillar","Floor_Tile","Step_Tile"}.Select(n=>"Assets/_Project/Prefabs/Buildings/"+n+".prefab").ToArray();
        var hashes=otherPaths.Select(p=>AssetDatabase.GetAssetDependencyHash(p).ToString()).ToArray();
        string guid=AssetDatabase.AssetPathToGUID(PrefabPath);
        var before=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var complete=before.GetComponent<Building>().GetStageMesh(10);
        var material=before.GetComponent<Renderer>().sharedMaterial;
        var colliderMesh=before.GetComponent<MeshCollider>().sharedMesh;
        Vector3 position=before.transform.position,scale=before.transform.localScale;
        foreach (string file in new[]{"ArchStages.fbx","ArchChunks.fbx"})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Art+file);
            importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
            importer.importAnimation=false; importer.importCameras=false; importer.importLights=false; importer.importBlendShapes=false;
            importer.addCollider=false; importer.isReadable=true; importer.generateSecondaryUV=false;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.importNormals=ModelImporterNormals.Import; importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
        }
        var meshes=AssetDatabase.LoadAllAssetsAtPath(Art+"ArchStages.fbx").OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        var pieces=AssetDatabase.LoadAllAssetsAtPath(Art+"ArchChunks.fbx").OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        if (meshes.Length!=10 || pieces.Length!=80 || meshes.Concat(pieces).Any(m=>m.subMeshCount!=1))
            throw new InvalidOperationException("Expected ten single-submesh arch stages and eighty chunks.");
        var library=AssetDatabase.LoadAssetAtPath<PillarChunkLibrary>(Art+"ArchChunks.asset");
        if (library==null) {library=ScriptableObject.CreateInstance<PillarChunkLibrary>(); AssetDatabase.CreateAsset(library,Art+"ArchChunks.asset");}
        library.Pieces=pieces.Select(m=>new PillarChunkLibrary.Piece {Mesh=m,Stage=int.Parse(m.name.Split('_')[2])}).ToArray();
        EditorUtility.SetDirty(library);
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var building=new SerializedObject(root.GetComponent<Building>());
            var stages=building.FindProperty("stageMeshes"); stages.arraySize=11;
            for (int i=0; i<10; i++) stages.GetArrayElementAtIndex(i).objectReferenceValue=meshes.Single(m=>m.name=="Arch_Stage_"+i.ToString("00"));
            stages.GetArrayElementAtIndex(10).objectReferenceValue=complete; building.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<MeshFilter>().sharedMesh=meshes[0];
            var motion=root.GetComponent<PillarConstructionMotion>() ?? root.AddComponent<PillarConstructionMotion>();
            var data=new SerializedObject(motion); data.FindProperty("library").objectReferenceValue=library; data.ApplyModifiedPropertiesWithoutUndo();
            // The existing nonconvex finished mesh collider keeps the doorway clear.
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath); var configured=prefab.GetComponent<Building>();
        var collider=prefab.GetComponent<MeshCollider>();
        var report=new {prefab=PrefabPath,guidPreserved=guid==AssetDatabase.AssetPathToGUID(PrefabPath),
            completedMeshPreserved=configured.GetStageMesh(10)==complete,materialPreserved=prefab.GetComponent<Renderer>().sharedMaterial==material,
            positionPreserved=prefab.transform.position==position,scalePreserved=prefab.transform.localScale==scale,
            colliderPreserved=collider.sharedMesh==colliderMesh && !collider.convex && !collider.isTrigger,
            rootChildren=prefab.transform.childCount,chunkCount=pieces.Length,
            chunksPerStage=library.Pieces.GroupBy(p=>p.Stage).Select(g=>new {stage=g.Key,count=g.Count()}).ToArray(),
            otherPrefabsUnchanged=otherPaths.Select((p,i)=>new {path=p,unchanged=hashes[i]==AssetDatabase.GetAssetDependencyHash(p).ToString()}).ToArray(),
            stages=Enumerable.Range(0,11).Select(i=>{var mesh=configured.GetStageMesh((byte)i);return new {stage=i,mesh=mesh.name,triangles=mesh.triangles.Length/3,mesh.vertexCount,mesh.subMeshCount,uv0=mesh.uv.Length,uv1=mesh.uv2.Length,normals=mesh.normals.Length,tangents=mesh.tangents.Length};}).ToArray(),
            shaderMessages=ShaderUtil.GetShaderMessages(material.shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
            scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/ArchConstructionInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
