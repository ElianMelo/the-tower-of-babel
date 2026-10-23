using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>Assign floor/step chunks while retaining their finished walking colliders.</summary>
public static class DeckConstructionAuthoring
{
    public const string Art="Assets/_Project/Models/Buildings/FrutigerAero/Construction/";
    public static string PrefabPath(string kind) => "Assets/_Project/Prefabs/Buildings/"+kind+".prefab";

    [MenuItem("Tower of Babel/Buildings/Apply floor and step construction chunks")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var others=new[]{"Pillar","Arch"}.Select(PrefabPath).ToArray();
        var hashes=others.Select(p=>AssetDatabase.GetAssetDependencyHash(p).ToString()).ToArray();
        var reports=new System.Collections.Generic.List<object>();
        foreach (string kind in new[]{"Floor_Tile","Step_Tile"})
        {
            string path=PrefabPath(kind),guid=AssetDatabase.AssetPathToGUID(path);
            var before=AssetDatabase.LoadAssetAtPath<GameObject>(path); var complete=before.GetComponent<Building>().GetStageMesh(10);
            var material=before.GetComponent<Renderer>().sharedMaterial;
            string colliderBefore=EditorJsonUtility.ToJson(before.GetComponent<Collider>());
            Vector3 position=before.transform.position,scale=before.transform.localScale;
            foreach (string suffix in new[]{"Stages.fbx","Chunks.fbx"})
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(Art+kind+suffix);
                importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true;
                importer.importAnimation=false; importer.importCameras=false; importer.importLights=false; importer.importBlendShapes=false;
                importer.addCollider=false; importer.isReadable=true; importer.generateSecondaryUV=false;
                importer.meshCompression=ModelImporterMeshCompression.Off;
                importer.importNormals=ModelImporterNormals.Import; importer.importTangents=ModelImporterTangents.CalculateMikk;
                importer.materialImportMode=ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
            }
            var meshes=AssetDatabase.LoadAllAssetsAtPath(Art+kind+"Stages.fbx").OfType<Mesh>().OrderBy(m=>m.name).ToArray();
            var pieces=AssetDatabase.LoadAllAssetsAtPath(Art+kind+"Chunks.fbx").OfType<Mesh>().OrderBy(m=>m.name).ToArray();
            if (meshes.Length!=10 || pieces.Length!=80 || meshes.Concat(pieces).Any(m=>m.subMeshCount!=1))
                throw new InvalidOperationException("Expected ten stages and eighty single-submesh chunks for "+kind);
            var library=AssetDatabase.LoadAssetAtPath<PillarChunkLibrary>(Art+kind+"Chunks.asset");
            if (library==null) {library=ScriptableObject.CreateInstance<PillarChunkLibrary>(); AssetDatabase.CreateAsset(library,Art+kind+"Chunks.asset");}
            library.Pieces=pieces.Select(m=>new PillarChunkLibrary.Piece {Mesh=m,Stage=int.Parse(m.name.Split('_')[^2])}).ToArray();
            EditorUtility.SetDirty(library);
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var data=new SerializedObject(root.GetComponent<Building>()); var stages=data.FindProperty("stageMeshes"); stages.arraySize=11;
                for (int i=0; i<10; i++) stages.GetArrayElementAtIndex(i).objectReferenceValue=meshes.Single(m=>m.name==kind+"_Stage_"+i.ToString("00"));
                stages.GetArrayElementAtIndex(10).objectReferenceValue=complete; data.ApplyModifiedPropertiesWithoutUndo();
                root.GetComponent<MeshFilter>().sharedMesh=meshes[0];
                var motion=root.GetComponent<PillarConstructionMotion>() ?? root.AddComponent<PillarConstructionMotion>();
                var motionData=new SerializedObject(motion); motionData.FindProperty("library").objectReferenceValue=library; motionData.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var after=AssetDatabase.LoadAssetAtPath<GameObject>(path); var building=after.GetComponent<Building>();
            reports.Add(new {kind,guidPreserved=guid==AssetDatabase.AssetPathToGUID(path),completedMeshPreserved=building.GetStageMesh(10)==complete,
                materialPreserved=after.GetComponent<Renderer>().sharedMaterial==material,
                positionPreserved=after.transform.position==position,scalePreserved=after.transform.localScale==scale,
                colliderPreserved=EditorJsonUtility.ToJson(after.GetComponent<Collider>())==colliderBefore,
                rootChildren=after.transform.childCount,chunkCount=pieces.Length,
                chunksPerStage=library.Pieces.GroupBy(p=>p.Stage).Select(g=>new {stage=g.Key,count=g.Count()}).ToArray(),
                stages=Enumerable.Range(0,11).Select(i=>{var m=building.GetStageMesh((byte)i);return new {stage=i,m.name,triangles=m.triangles.Length/3,m.vertexCount,m.subMeshCount,uv0=m.uv.Length,uv1=m.uv2.Length,normals=m.normals.Length,tangents=m.tangents.Length};}).ToArray(),
                shaderMessages=ShaderUtil.GetShaderMessages(material.shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray()});
        }
        AssetDatabase.SaveAssets();
        var report=new {assets=reports,otherPrefabsUnchanged=others.Select((p,i)=>new {path=p,unchanged=hashes[i]==AssetDatabase.GetAssetDependencyHash(p).ToString()}).ToArray(),
            scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/DeckConstructionInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
