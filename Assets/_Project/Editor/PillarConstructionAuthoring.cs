using System;
using System.Linq;
using TMPro;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Imports pillar-only construction meshes and creates a separate visual review scene.</summary>
public static class PillarConstructionAuthoring
{
    public const string ModelPath = "Assets/_Project/Models/Buildings/FrutigerAero/Construction/PillarStages.fbx";
    public const string ChunkModelPath = "Assets/_Project/Models/Buildings/FrutigerAero/Construction/PillarChunks.fbx";
    public const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Pillar.prefab";
    public const string ReviewPath = "Assets/_Project/Scenes/PillarConstructionReview.unity";
    private const string Art = "Assets/_Project/Models/Buildings/FrutigerAero/";
    private const string Output = "Documentation/FrutigerAero/Buildings/Previews/";
    private static readonly string[] Labels = { "Blueprint", "Random fill 10%", "Random fill 20%", "Random fill 30%", "Random fill 40%", "Random fill 50%",
        "Random fill 60%", "Random fill 70%", "Random fill 80%", "Random fill 90%", "Complete" };

    private static void Guard()
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
    }

    [MenuItem("Tower of Babel/Buildings/Apply pillar construction stages")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        Guard(); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var paths = new[] { "Arch", "Floor_Tile", "Step_Tile" }.Select(n=>"Assets/_Project/Prefabs/Buildings/"+n+".prefab").ToArray();
        var before = paths.Select(p=>AssetDatabase.GetAssetDependencyHash(p).ToString()).ToArray();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        var completed = prefab.GetComponent<Building>().GetStageMesh(10);
        var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.importBlendShapes = false; importer.addCollider = false; importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.generateSecondaryUV = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var meshes = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        if (meshes.Length != 10 || meshes.Any(m=>m.subMeshCount!=1))
            throw new InvalidOperationException("Expected ten single-submesh construction models.");
        var chunkImporter=(ModelImporter)AssetImporter.GetAtPath(ChunkModelPath);
        chunkImporter.globalScale=1; chunkImporter.useFileScale=true; chunkImporter.bakeAxisConversion=true;
        chunkImporter.importAnimation=false; chunkImporter.importCameras=false; chunkImporter.importLights=false;
        chunkImporter.addCollider=false; chunkImporter.isReadable=true; chunkImporter.generateSecondaryUV=false;
        chunkImporter.importNormals=ModelImporterNormals.Import; chunkImporter.importTangents=ModelImporterTangents.CalculateMikk;
        chunkImporter.materialImportMode=ModelImporterMaterialImportMode.None; chunkImporter.SaveAndReimport();
        var chunks=AssetDatabase.LoadAllAssetsAtPath(ChunkModelPath).OfType<Mesh>().OrderBy(m=>m.name).ToArray();
        const string libraryPath=Art+"Construction/PillarChunks.asset";
        var library=AssetDatabase.LoadAssetAtPath<PillarChunkLibrary>(libraryPath);
        if (library==null) {library=ScriptableObject.CreateInstance<PillarChunkLibrary>(); AssetDatabase.CreateAsset(library,libraryPath);}
        library.Pieces=chunks.Select(m=>new PillarChunkLibrary.Piece {Mesh=m,Stage=int.Parse(m.name.Split('_')[2])}).ToArray();
        EditorUtility.SetDirty(library);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var building = root.GetComponent<Building>();
            var serialized = new SerializedObject(building);
            var stages = serialized.FindProperty("stageMeshes"); stages.arraySize = 11;
            for (int i=0; i<10; i++)
                stages.GetArrayElementAtIndex(i).objectReferenceValue = meshes.Single(m=>m.name=="Pillar_Stage_"+i.ToString("00"));
            stages.GetArrayElementAtIndex(10).objectReferenceValue = completed;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var motion=root.GetComponent<PillarConstructionMotion>() ?? root.AddComponent<PillarConstructionMotion>();
            var motionData=new SerializedObject(motion); motionData.FindProperty("library").objectReferenceValue=library;
            motionData.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<MeshFilter>().sharedMesh = meshes[0];
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var configured = prefab.GetComponent<Building>();
        var report = new
        {
            prefab = PrefabPath, prefabGuidPreserved = guid==AssetDatabase.AssetPathToGUID(PrefabPath),
            completedMeshPreserved = configured.GetStageMesh(10)==completed,
            materialPreserved = prefab.GetComponent<MeshRenderer>().sharedMaterial==material,
            otherPrefabsUnchanged = paths.Select((p,i)=>new {path=p,unchanged=before[i]==AssetDatabase.GetAssetDependencyHash(p).ToString()}).ToArray(),
            rootChildren = prefab.transform.childCount, rootScale = prefab.transform.localScale.ToString(),
            collider = prefab.GetComponent<Collider>().GetType().Name,
            chunkCount=library.Pieces.Length,
            chunksPerStage=library.Pieces.GroupBy(p=>p.Stage).Select(g=>new {stage=g.Key,count=g.Count()}).ToArray(),
            stages = Enumerable.Range(0,11).Select(i=>
            {
                var mesh = configured.GetStageMesh((byte)i);
                return new {stage=i,phase=Labels[i],mesh=mesh.name,asset=AssetDatabase.GetAssetPath(mesh),triangles=mesh.triangles.Length/3,
                    mesh.vertexCount,mesh.subMeshCount,uv0=mesh.uv.Length,uv1=mesh.uv2.Length,normals=mesh.normals.Length,
                    tangents=mesh.tangents.Length,min=mesh.bounds.min.ToString("F4"),max=mesh.bounds.max.ToString("F4")};
            }).ToArray(),
            shaderMessages=ShaderUtil.GetShaderMessages(material.shader).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
            playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed
        };
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/PillarConstructionInspection.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    [MenuItem("Tower of Babel/Buildings/Create pillar stages review")]
    public static void CreateReview()
    {
        Guard();
        if (Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save scene changes before opening the separate pillar review.");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var building = source.GetComponent<Building>();
        var material = source.GetComponent<MeshRenderer>().sharedMaterial;
        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.4f,.52f,.59f);
        RenderSettings.ambientEquatorColor = new Color(.22f,.32f,.4f);
        RenderSettings.ambientGroundColor = new Color(.13f,.19f,.25f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.customReflection = AssetDatabase.LoadAssetAtPath<Cubemap>(Art+"Review_AquaticReflection.exr");
        RenderSettings.fog = false;
        var camera = new GameObject("Pillar stages review camera",typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 4.5f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.58f,.72f,.81f);
        camera.transform.position = new Vector3(6.75f,4.9f,-18);
        camera.transform.LookAt(new Vector3(6.75f,1.8f,0)); camera.farClipPlane = 100;
        var key = new GameObject("Soft white key",typeof(Light)).GetComponent<Light>();
        key.type = LightType.Directional; key.intensity = 1.5f; key.color = Color.white;
        key.transform.rotation = Quaternion.Euler(35,-30,0); key.shadows = LightShadows.Soft; key.shadowStrength = .45f;
        var fill = new GameObject("Cool fill",typeof(Light)).GetComponent<Light>();
        fill.type = LightType.Directional; fill.intensity = .4f; fill.color = new Color(.72f,.91f,1);
        fill.transform.rotation = Quaternion.Euler(25,140,0);
        var lineup = new GameObject("Pillar construction · stages 0–10");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
        for (int i=0; i<11; i++)
        {
            var go = new GameObject("Stage "+i.ToString("00")+" · "+Labels[i],typeof(MeshFilter),typeof(MeshRenderer));
            go.transform.SetParent(lineup.transform,false); go.transform.position = new Vector3(i*1.35f,0,0);
            go.GetComponent<MeshFilter>().sharedMesh = building.GetStageMesh((byte)i);
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            var label = new GameObject("Caption "+i,typeof(TextMeshPro)).GetComponent<TextMeshPro>();
            label.transform.SetParent(lineup.transform,false); label.transform.position = new Vector3(i*1.35f,-.5f,-.05f);
            label.font = font; label.fontSize = 1.7f; label.enableAutoSizing = true; label.fontSizeMin = .8f; label.fontSizeMax = 1.7f; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(1.3f,.55f); label.text = i.ToString("00")+"\n"+Labels[i];
            label.color = new Color(.045f,.16f,.24f,1); label.textWrappingMode = TextWrappingModes.NoWrap;
        }
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.name = "Review ground";
        floor.transform.position = new Vector3(6.75f,-.78f,1); floor.transform.localScale = new Vector3(40,.1f,30);
        floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(Art+"Review_White.mat");
        UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
        EditorSceneManager.SaveScene(scene,ReviewPath);
        Selection.activeGameObject = lineup;
        SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(6.75f,2,0),new Vector3(15,5,1)),false);
    }

    public static object Capture()
    {
        Guard();
        if (SceneManager.GetActiveScene().path!=ReviewPath) throw new InvalidOperationException("Open the pillar review scene first.");
        System.IO.Directory.CreateDirectory(Output);
        var camera = Camera.main; var oldTarget = camera.targetTexture; var oldActive = RenderTexture.active;
        var oldPosition = camera.transform.position; var oldRotation = camera.transform.rotation;
        float oldSize = camera.orthographicSize;
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var material = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).GetComponent<Renderer>().sharedMaterial;
        float motionTime = material.GetFloat("_MotionTime");
        var items = GameObject.Find("Pillar construction · stages 0–10").transform.Cast<Transform>().ToArray();
        var activeStates = items.Select(t=>t.gameObject.activeSelf).ToArray();
        try
        {
            material.SetFloat("_MotionTime",2.5f);
            camera.orthographicSize = 3.3f;
            Render(camera,2200,1000,Output+"Pillar-Construction-Lineup.png");
            for (int i=0; i<11; i++)
            {
                foreach (var item in items)
                    item.gameObject.SetActive(item.name.StartsWith("Stage "+i.ToString("00")+" ·",StringComparison.Ordinal) || item.name=="Caption "+i);
                camera.transform.position = new Vector3(i*1.35f,3.6f,-12);
                camera.transform.LookAt(new Vector3(i*1.35f,1.8f,0)); camera.orthographicSize = 2.65f;
                Render(camera,360,760,Output+"Pillar-Stage-"+i.ToString("00")+".png");
            }
        }
        finally
        {
            for (int i=0; i<items.Length; i++) items[i].gameObject.SetActive(activeStates[i]);
            camera.targetTexture = oldTarget; RenderTexture.active = oldActive;
            camera.transform.SetPositionAndRotation(oldPosition,oldRotation); camera.orthographicSize = oldSize;
            material.SetFloat("_MotionTime",motionTime); ShaderUtil.allowAsyncCompilation = asynchronous;
        }
        return new {scene=SceneManager.GetActiveScene().path,SceneManager.GetActiveScene().isDirty,playing=EditorApplication.isPlaying};
    }

    private static void Render(Camera camera,int width,int height,string path)
    {
        var target = new RenderTexture(width,height,24); var pixels = new Texture2D(width,height,TextureFormat.RGB24,false);
        try
        {
            camera.targetTexture = target;
            for (int i=0; i<3; i++) camera.Render();
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply();
            System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
