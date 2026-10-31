using System;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;

public static class ResourceWorkshopAuthoring
{
    public const string Root="Assets/_Project/Models/Resources/";
    public const string StonePrefab="Assets/_Project/Prefabs/Resources/Stone.prefab";
    public const string ProcessorPrefab="Assets/_Project/Prefabs/Processor/StoneProcessor.prefab";
    private static readonly string[] Kinds={"Ceramic","BlueMetal","Steel","Glow","Stone","Refined"};

    [MenuItem("Tower of Babel/Resources/Apply textured stone")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path!="Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Tower scene before updating resource prefabs.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string texture in new[]{"StoneSurface-Imagegen","ProcessorAtlas-Imagegen"})
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"Textures/"+texture+".png");
            importer.textureType=TextureImporterType.Default; importer.sRGBTexture=true;
            importer.wrapMode=texture.StartsWith("Stone",StringComparison.Ordinal) ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.mipmapEnabled=true; importer.maxTextureSize=2048; importer.npotScale=TextureImporterNPOTScale.None;
            importer.anisoLevel=4; importer.textureCompression=TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
        System.IO.Directory.CreateDirectory(Root+"Materials"); AssetDatabase.Refresh();
        var materials=new Material[Kinds.Length];
        float[] metallic={.05f,.35f,.72f,.1f,.02f,.12f}, smoothness={.65f,.7f,.55f,.8f,.18f,.65f};
        for (int i=0; i<Kinds.Length; i++)
        {
            string path=Root+"Materials/Resource_"+Kinds[i]+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material==null) { material=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material,path); }
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/"+(i>=4 ? "StoneSurface-Imagegen" : "ProcessorAtlas-Imagegen")+".png"));
            material.SetColor("_BaseColor",i==5 ? new Color(.78f,.95f,1,1) : Color.white);
            material.SetFloat("_Metallic",metallic[i]); material.SetFloat("_Smoothness",smoothness[i]);
            material.enableInstancing=true;
            if (i==3)
            {
                material.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"Textures/ProcessorAtlas-Imagegen.png"));
                material.SetColor("_EmissionColor",new Color(.04f,.22f,.34f)); material.EnableKeyword("_EMISSION");
            }
            EditorUtility.SetDirty(material); materials[i]=material;
        }
        foreach (string name in new[]{"StoneCluster"})
        {
            var importer=(ModelImporter)AssetImporter.GetAtPath(Root+name+".fbx");
            importer.globalScale=1; importer.useFileScale=true; importer.bakeAxisConversion=true; importer.importAnimation=false;
            importer.importCameras=false; importer.importLights=false; importer.addCollider=false; importer.isReadable=true;
            importer.importNormals=ModelImporterNormals.Import; importer.importTangents=ModelImporterTangents.CalculateMikk;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard; importer.meshCompression=ModelImporterMeshCompression.Off;
            for (int i=0; i<Kinds.Length; i++)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Resource_"+Kinds[i]),materials[i]);
            importer.SaveAndReimport();
        }
        string stoneGuid=AssetDatabase.AssetPathToGUID(StonePrefab);
        var stone=PrefabUtility.LoadPrefabContents(StonePrefab);
        try
        {
            var visuals=stone.transform.Find("Visuals");
            var mesh=AssetDatabase.LoadAllAssetsAtPath(Root+"StoneCluster.fbx").OfType<Mesh>().Single();
            visuals.GetComponent<MeshFilter>().sharedMesh=mesh; visuals.GetComponent<MeshRenderer>().sharedMaterial=materials[4];
            var collider=visuals.GetComponent<SphereCollider>(); collider.center=mesh.bounds.center;
            collider.radius=mesh.vertices.Max(v=>(v-mesh.bounds.center).magnitude);
            visuals.localPosition=new Vector3(0,.02f-stone.transform.position.y-mesh.bounds.min.y*visuals.localScale.y,0);
            PrefabUtility.SaveAsPrefabAsset(stone,StonePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(stone); }
        // The stone processor is built by ProcessorFamilyAuthoring with the rest of the processor family.
        AssetDatabase.SaveAssets();
        var report=new
        {
            stoneGuidPreserved=stoneGuid==AssetDatabase.AssetPathToGUID(StonePrefab),
            assets=new[]{StonePrefab}.Select(p=>
            {
                var go=AssetDatabase.LoadAssetAtPath<GameObject>(p);
                return new {prefab=p,rootPosition=go.transform.position.ToString(),rootScale=go.transform.localScale.ToString(),
                    meshes=go.GetComponentsInChildren<MeshFilter>().Select(f=>new {f.name,triangles=f.sharedMesh.triangles.Length/3,uvs=f.sharedMesh.uv.Length}).ToArray(),
                    colliders=go.GetComponentsInChildren<Collider>().Select(c=>new {c.name,type=c.GetType().Name,parent=c.transform.parent.name}).ToArray()};
            }).ToArray(),
            textures=materials.Select(m=>new {m.name,texture=AssetDatabase.GetAssetPath(m.GetTexture("_BaseMap"))}).ToArray(),
            sceneDirty=scene.isDirty,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed
        };
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Resources/ResourceWorkshopInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
    }
}
