using System;
using System.Linq;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Replaces the arch prefab's look with the Aurora Curtain design (Round05 design 10): completed mesh,
/// stages 0-9, incoming chunk library, the animated aurora material and a collider built from the new
/// completed mesh. The prefab GUID, root transform, Building settings and motion component are preserved.
/// Source: Tools/BuildingAero/build_aurora_arch.py.
/// </summary>
public static class AuroraArchAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Arch.prefab";
    public const string Art = "Assets/_Project/Models/Buildings/FrutigerAero/";
    public const string CompletePath = Art + "AuroraArch.fbx";
    public const string StagesPath = Art + "Construction/AuroraArchStages.fbx";
    public const string ChunksPath = Art + "Construction/AuroraArchChunks.fbx";
    public const string LibraryPath = Art + "Construction/AuroraArchChunks.asset";
    public const string MaterialPath = Art + "Aero_AuroraArch.mat";
    public const string ShaderName = "TowerOfBabel/Buildings/Aurora Arch";
    /// <summary>Pillar axis to pillar axis at the base radius (96 m, 1.25 degrees).</summary>
    public const float Span = 2.0944f;

    [MenuItem("Tower of Babel/Buildings/Apply aurora arch")]
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
                slots.GetArrayElementAtIndex(i).objectReferenceValue = stages.Single(m => m.name == "Arch_Stage_" + i.ToString("00"));
            slots.GetArrayElementAtIndex(10).objectReferenceValue = complete;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var motionComponent = root.GetComponent<PillarConstructionMotion>() ?? root.AddComponent<PillarConstructionMotion>();
            var motion = new SerializedObject(motionComponent);
            motion.FindProperty("library").objectReferenceValue = library;
            motion.ApplyModifiedPropertiesWithoutUndo();
            root.GetComponent<MeshFilter>().sharedMesh = stages[0];
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            // The finished arch is the walkable/blocking shape; the opening stays clear.
            var collider = root.GetComponent<MeshCollider>() ?? root.AddComponent<MeshCollider>();
            collider.sharedMesh = complete; collider.convex = false; collider.isTrigger = false;
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
            collider = AssetDatabase.GetAssetPath(prefab.GetComponent<MeshCollider>().sharedMesh) + " " + prefab.GetComponent<MeshCollider>().sharedMesh.name,
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
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AuroraArchInspection.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    /// <summary>
    /// Rewrites only the saved tower's pillars and arches with TowerGenerator.RingLayout (whole bays per ring,
    /// arches scaled to the chord), then rebuilds pillar supports with TowerSupportAuthoring. Floors, glass floors
    /// and stairs keep their authored data. Construction stages are kept from the nearest old asset of each type.
    /// </summary>
    [MenuItem("Tower of Babel/Buildings/Repair pillar and arch rings")]
    public static object RepairTowerRings()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode
            || scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Tower scene in this project's Edit Mode.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var manager = UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.ChunkManager>();
        var all = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
        int Level(Vector3 p) => Mathf.FloorToInt((p.y + .001f) / generator.heighIncrease);
        var ring = new[] { TowerOfBabel.World.Tower.TowerAssetType.Pillar, TowerOfBabel.World.Tower.TowerAssetType.Arch };
        var old = all.Where(a => ring.Contains(a.AssetType)).ToLookup(a => (a.AssetType, Level(a.Position)));
        Vector3 parentScale = generator.TowerParent.lossyScale;
        Vector3 pillarScale = Vector3.Scale(parentScale, generator.pillarObject.transform.localScale);
        Vector3 archScale = Vector3.Scale(parentScale, generator.archObject.transform.localScale);
        var rebuilt = new System.Collections.Generic.List<TowerOfBabel.World.Chunks.ChunkAssetData>();
        var levels = new System.Collections.Generic.List<object>();
        float maxShift = 0;
        for (int level = 0; level < generator.levels; level++)
        {
            int before = old[(ring[0], level)].Count(), added = 0;
            foreach (var bay in generator.RingLayout(level))
            {
                var pillar = Nearest(old[(ring[0], level)], bay.Pillar, ref maxShift);
                var arch = Nearest(old[(ring[1], level)], bay.Arch, ref maxShift);
                rebuilt.Add(new TowerOfBabel.World.Chunks.ChunkAssetData(0, ring[0], bay.Pillar, Quaternion.identity, pillarScale, pillar.Stage));
                rebuilt.Add(new TowerOfBabel.World.Chunks.ChunkAssetData(0, ring[1], bay.Arch, bay.ArchRotation,
                    new Vector3(archScale.x * bay.ArchScale, archScale.y, archScale.z), arch.Stage));
                added++;
            }
            levels.Add(new { level, before, after = added });
        }
        int nonRingBefore = all.Length - all.Count(a => ring.Contains(a.AssetType));
        manager.ReplaceCachedAssets(t => ring.Contains(t), rebuilt);
        EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        object supports = TowerSupportAuthoring.Apply();
        var after = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
        var report = new
        {
            pillarsBefore = all.Count(a => a.AssetType == ring[0]), pillarsAfter = after.Count(a => a.AssetType == ring[0]),
            archesBefore = all.Count(a => a.AssetType == ring[1]), archesAfter = after.Count(a => a.AssetType == ring[1]),
            maxPositionShift = maxShift, stairsPreserved = all.Count(a => a.AssetType == TowerOfBabel.World.Tower.TowerAssetType.Stair) ==
                after.Count(a => a.AssetType == TowerOfBabel.World.Tower.TowerAssetType.Stair),
            nonRingBefore, levels, supports, sceneDirty = scene.isDirty
        };
        System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AuroraArchRingRepair.json",
            Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }

    private static TowerOfBabel.World.Chunks.ChunkAssetData Nearest(
        System.Collections.Generic.IEnumerable<TowerOfBabel.World.Chunks.ChunkAssetData> candidates, Vector3 position, ref float maxShift)
    {
        var best = candidates.OrderBy(a => (a.Position - position).sqrMagnitude).First();
        maxShift = Mathf.Max(maxShift, Vector3.Distance(best.Position, position));
        return best;
    }

    private static Mesh[] Meshes(string path) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().OrderBy(m => m.name, StringComparer.Ordinal).ToArray();

    private static void ConfigureImporter(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path)
            ?? throw new InvalidOperationException("Missing exported model " + path + "; run build_aurora_arch.py first.");
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
        if (material == null) { material = new Material(shader) { name = "Aero_AuroraArch" }; AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader;
        var generator = UnityEngine.Object.FindAnyObjectByType<TowerGenerator>();
        if (generator != null)
        {
            // The generator places pillars and arches at world positions around `center`.
            material.SetVector("_TowerCenter", new Vector4(generator.center.x, 0, generator.center.y, 0));
            material.SetFloat("_FloorHeight", generator.heighIncrease);
        }
        // Accepted look (2026-10-31): curtain readable by day, edge not blown out at night.
        material.SetFloat("_AuroraStrength", 1.3f); material.SetFloat("_AuroraNightBoost", 1.25f);
        material.SetFloat("_AuroraTint", .6f); material.SetFloat("_AuroraCoverage", .3f); material.SetFloat("_AuroraHeight", .5f); material.SetFloat("_AuroraBase", 1.7f); material.SetFloat("_AuroraEdgeWander", .12f); material.SetFloat("_AuroraSpeed", .5f);
        // Aurora palette from the user's reference (2026-10-31): green base, teal, blue, violet-pink top and tinges.
        material.SetColor("_AuroraLowColor", new Color(.22f, 1f, .42f)); material.SetColor("_AuroraMidColor", new Color(.05f, .85f, .82f));
        material.SetColor("_AuroraHighColor", new Color(.2f, .36f, 1f)); material.SetColor("_AuroraFringeColor", new Color(.9f, .32f, .95f));
        material.enableInstancing = true;
        material.SetFloat("_WetnessOn", 1); material.EnableKeyword("_AERO_WETNESS");
        EditorUtility.SetDirty(material);
        return material;
    }
}
