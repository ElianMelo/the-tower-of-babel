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

    [MenuItem("Tower of Babel/Resources/Apply textured stone and processor")]
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
        foreach (string name in new[]{"StoneCluster","StoneProcessor"})
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
        string stoneGuid=AssetDatabase.AssetPathToGUID(StonePrefab), processorGuid=AssetDatabase.AssetPathToGUID(ProcessorPrefab);
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
        var processor=PrefabUtility.LoadPrefabContents(ProcessorPrefab);
        try
        {
            var visuals=processor.transform.Find("Visuals");
            var oldRenderer=visuals.GetComponent<MeshRenderer>(); if (oldRenderer!=null) UnityEngine.Object.DestroyImmediate(oldRenderer);
            var oldFilter=visuals.GetComponent<MeshFilter>(); if (oldFilter!=null) UnityEngine.Object.DestroyImmediate(oldFilter);
            for (int i=visuals.childCount-1; i>=0; i--) UnityEngine.Object.DestroyImmediate(visuals.GetChild(i).gameObject);
            visuals.localScale=Vector3.one; visuals.localPosition=Vector3.zero;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"StoneProcessor.fbx"),visuals);
            model.name="ProcessorModel"; model.transform.localPosition=Vector3.zero;
            var bounds=new Bounds(); bool first=true;
            foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
            {
                var b=filter.sharedMesh.bounds;
                for (int x=0; x<2; x++) for (int y=0; y<2; y++) for (int z=0; z<2; z++)
                {
                    var point=visuals.InverseTransformPoint(filter.transform.TransformPoint(new Vector3(x==0?b.min.x:b.max.x,y==0?b.min.y:b.max.y,z==0?b.min.z:b.max.z)));
                    if (first) {bounds=new Bounds(point,Vector3.zero); first=false;} else bounds.Encapsulate(point);
                }
            }
            var collider=visuals.GetComponent<BoxCollider>(); collider.center=bounds.center; collider.size=bounds.size;
            visuals.localPosition=new Vector3(0,.02f-processor.transform.position.y-bounds.min.y,0);
            var animation=processor.GetComponent<ProcessorWorkAnimation>() ?? processor.AddComponent<ProcessorWorkAnimation>();
            var serialized=new SerializedObject(animation);
            foreach (var pair in new[]{("leftRoller","RollerLeft"),("rightRoller","RollerRight"),("pressHead","PressHead"),("feedStone","FeedStone"),("outputStone","OutputStone")})
                serialized.FindProperty(pair.Item1).objectReferenceValue=model.GetComponentsInChildren<Transform>().Single(t=>t.name==pair.Item2);
            var lights=serialized.FindProperty("statusLights"); lights.arraySize=3;
            for (int i=0; i<3; i++) lights.GetArrayElementAtIndex(i).objectReferenceValue=model.GetComponentsInChildren<Renderer>().Single(r=>r.name=="StatusLight_"+i);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="FeedStone").gameObject.SetActive(false);
            model.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="OutputStone").gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(processor,ProcessorPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(processor); }
        AssetDatabase.SaveAssets();
        var report=new
        {
            stoneGuidPreserved=stoneGuid==AssetDatabase.AssetPathToGUID(StonePrefab),processorGuidPreserved=processorGuid==AssetDatabase.AssetPathToGUID(ProcessorPrefab),
            assets=new[]{StonePrefab,ProcessorPrefab}.Select(p=>
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
