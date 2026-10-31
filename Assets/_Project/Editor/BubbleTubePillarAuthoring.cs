using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Replaces the pillar prefab's look with the Bubble Tube design (Round03 design 04): completed mesh,
/// stages 0-9, incoming chunk library and the per-instance bubble/water material. The prefab GUID,
/// root transform, capsule collider, Building settings and motion component are preserved.
/// Source: Tools/BuildingAero/build_bubble_tube_pillar.py.
/// </summary>
public static class BubbleTubePillarAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Pillar.prefab";
    public const string Art = "Assets/_Project/Models/Buildings/FrutigerAero/";
    public const string CompletePath = Art + "BubbleTubePillar.fbx";
    public const string StagesPath = Art + "Construction/BubbleTubeStages.fbx";
    public const string ChunksPath = Art + "Construction/BubbleTubeChunks.fbx";
    public const string LibraryPath = Art + "Construction/BubbleTubeChunks.asset";
    public const string MaterialPath = Art + "Aero_BubbleTube_Pillar.mat";
    public const string ShaderName = "TowerOfBabel/Buildings/Bubble Tube Pillar";
    private const string PreviousMaterialPath = Art + "Aero_WhiteMarble_Pillar.mat";

    [MenuItem("Tower of Babel/Buildings/Apply bubble tube pillar")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (string path in new[] { CompletePath, StagesPath, ChunksPath }) ConfigureImporter(path);

        Mesh complete = Meshes(CompletePath).Single();
        Mesh[] stages = Meshes(StagesPath);
        Mesh[] chunks = Meshes(ChunksPath);
        if (stages.Length != 10 || chunks.Length != 80 || stages.Concat(chunks).Append(complete).Any(m => m.subMeshCount != 1))
            throw new InvalidOperationException($"Expected 10 stages and 80 chunks with one submesh each; found {stages.Length} and {chunks.Length}.");

        var library = AssetDatabase.LoadAssetAtPath<PillarChunkLibrary>(LibraryPath);
        if (library == null) { library = ScriptableObject.CreateInstance<PillarChunkLibrary>(); AssetDatabase.CreateAsset(library, LibraryPath); }
        library.Pieces = chunks.Select(m => new PillarChunkLibrary.Piece { Mesh = m, Stage = int.Parse(m.name.Split('_')[2]) }).ToArray();
        EditorUtility.SetDirty(library);

        Material material = CreateMaterial();
        string guid = AssetDatabase.AssetPathToGUID(PrefabPath);
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var serialized = new SerializedObject(root.GetComponent<Building>());
            var slots = serialized.FindProperty("stageMeshes"); slots.arraySize = 11;
            for (int i = 0; i < 10; i++)
                slots.GetArrayElementAtIndex(i).objectReferenceValue = stages.Single(m => m.name == "Pillar_Stage_" + i.ToString("00"));
            slots.GetArrayElementAtIndex(10).objectReferenceValue = complete;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var motion = new SerializedObject(root.GetComponent<PillarConstructionMotion>());
            motion.FindProperty("library").objectReferenceValue = library;
            motion.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<MeshFilter>().sharedMesh = stages[0];
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var building = prefab.GetComponent<Building>();
        var report = new
        {
            prefab = PrefabPath, prefabGuidPreserved = guid == AssetDatabase.AssetPathToGUID(PrefabPath),
            material = AssetDatabase.GetAssetPath(prefab.GetComponent<MeshRenderer>().sharedMaterial),
            shader = material.shader.name, instancing = material.enableInstancing,
            shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Select(m => new { m.message, severity = m.severity.ToString(), m.line }).ToArray(),
            collider = prefab.GetComponent<CapsuleCollider>() is { } c ? new { c.radius, c.height, center = c.center.ToString("F3") } : null,
            chunkCount = library.Pieces.Length,
            chunksPerStage = library.Pieces.GroupBy(p => p.Stage).OrderBy(g => g.Key).Select(g => new { stage = g.Key, count = g.Count() }).ToArray(),
            stages = Enumerable.Range(0, 11).Select(i =>
            {
                Mesh mesh = building.GetStageMesh((byte)i);
                return new { stage = i, mesh = mesh.name, asset = AssetDatabase.GetAssetPath(mesh), triangles = mesh.triangles.Length / 3,
                    mesh.vertexCount, mesh.subMeshCount, uv1 = mesh.uv2.Length, min = mesh.bounds.min.ToString("F4"), max = mesh.bounds.max.ToString("F4") };
            }).ToArray(),
            playing = EditorApplication.isPlaying, compileFailed = EditorUtility.scriptCompilationFailed
        };
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/BubbleTubePillarInspection.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    private static Mesh[] Meshes(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().OrderBy(m => m.name, StringComparer.Ordinal).ToArray();

    private static void ConfigureImporter(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path)
            ?? throw new InvalidOperationException("Missing exported model " + path + "; run build_bubble_tube_pillar.py first.");
        importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
        importer.importAnimation = false; importer.importCameras = false; importer.importLights = false;
        importer.importBlendShapes = false; importer.addCollider = false; importer.isReadable = true;
        importer.meshCompression = ModelImporterMeshCompression.Off;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.None;
        importer.generateSecondaryUV = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
    }

    private static Material CreateMaterial()
    {
        var shader = Shader.Find(ShaderName) ?? throw new InvalidOperationException("Shader not compiled: " + ShaderName);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) { material = new Material(shader) { name = "Aero_BubbleTube_Pillar" }; AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        var previous = AssetDatabase.LoadAssetAtPath<Material>(PreviousMaterialPath);
        if (previous != null)
        {
            // Keep the same marble reference so the pearl matches the arches and floors.
            foreach (string texture in new[] { "_MarbleMap", "_NormalMap" })
                if (previous.HasTexture(texture)) material.SetTexture(texture, previous.GetTexture(texture));
            foreach (string value in new[] { "_MarbleScale", "_TextureAspect", "_NormalStrength", "_PatternSeed", "_AlbedoCeiling", "_Porosity", "_WetExposure" })
                if (previous.HasFloat(value)) material.SetFloat(value, previous.GetFloat(value));
        }
        // Subtle bubbles (2026-10-30 feedback): few on average, varying per pillar and over time.
        material.SetFloat("_BubbleAmountMin", .06f); material.SetFloat("_BubbleAmountMax", .45f);
        material.SetFloat("_BubbleBurstiness", .85f); material.SetFloat("_BigBubbleChance", .45f);
        material.SetFloat("_BubbleIridescence", 1.2f); material.SetFloat("_GlassDistortion", 1f);
        material.enableInstancing = true;
        material.SetFloat("_WetnessOn", 1); material.EnableKeyword("_AERO_WETNESS");
        EditorUtility.SetDirty(material);
        return material;
    }
}
