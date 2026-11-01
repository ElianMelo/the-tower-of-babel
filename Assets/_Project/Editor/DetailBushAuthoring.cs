using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Creates the buildable detail-phase bush and places it beside pillars in the cached tower data.</summary>
public static class DetailBushAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Buildings/Detail_Bush.prefab";
    public const string StagesPath = "Assets/_Project/Models/Foliage/DetailBushStages.asset";
    private const string SourcePrefab = "Assets/_Project/Prefabs/Foliage/Bush_Medium.prefab";
    private const string OutlineSettingsPath = "Assets/_Project/Scripts/Buildings/OutlineSettings.asset";
    private const float SproutScale = .3f;

    [MenuItem("Tower of Babel/Construction/Create detail bush and place beside pillars")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity")
            throw new InvalidOperationException("Open the Tower scene before placing detail bushes.");

        var prefab = CreatePrefab();
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>(FindObjectsInactive.Include);
        if (generator == null || manager == null) throw new InvalidOperationException("The Tower scene needs a TowerGenerator and ChunkManager.");
        generator.detailBushObject = prefab;
        EditorUtility.SetDirty(generator);
        manager.AssetPrefabs.SetPrefab(TowerAssetType.Bush, prefab);

        var structure = manager.CachedChunks.SelectMany(c => c.Assets).Where(a => !a.AssetType.IsDetail()).ToArray();
        var bushes = generator.BuildDetailBushes(structure);
        manager.ReplaceCachedAssets(type => type.IsDetail(), bushes);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        return new
        {
            prefab = PrefabPath,
            bushes = bushes.Count,
            perFloor = bushes.GroupBy(b => manager.WorldToChunk(b.Position).FloorIndex).OrderBy(g => g.Key)
                .Select(g => new { floor = g.Key, count = g.Count() }).ToArray(),
            cachedAssets = manager.CachedAssetCount
        };
    }

    private static GameObject CreatePrefab()
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefab);
        if (source == null) throw new InvalidOperationException($"Missing {SourcePrefab}.");
        var sourceMesh = source.GetComponent<MeshFilter>().sharedMesh;
        var material = source.GetComponent<MeshRenderer>().sharedMaterial;
        var stages = CreateStageMeshes(sourceMesh);

        var root = new GameObject("Detail_Bush", typeof(MeshFilter), typeof(MeshRenderer));
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh = stages[0];
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = stages[10].bounds.center;
            collider.size = stages[10].bounds.size;
            root.AddComponent<TowerAsset>().SetAssetType(TowerAssetType.Bush);
            var outline = root.AddComponent<TowerOfBabel.Outline>();
            var outlineData = new SerializedObject(outline);
            outlineData.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(OutlineSettingsPath);
            outlineData.ApplyModifiedPropertiesWithoutUndo();
            var building = root.AddComponent<Building>();
            var data = new SerializedObject(building);
            data.FindProperty("resourceType").intValue = (int)TowerOfBabel.Resources.ResourceType.Stone;
            data.FindProperty("resourceCostPerStage").intValue = 1;
            data.FindProperty("interactionDuration").floatValue = 2f;
            data.FindProperty("meshFilter").objectReferenceValue = root.GetComponent<MeshFilter>();
            data.FindProperty("outline").objectReferenceValue = outline;
            var meshes = data.FindProperty("stageMeshes");
            meshes.arraySize = Building.StageCount;
            for (int i = 0; i < Building.StageCount; i++) meshes.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    /// <summary>Stage 0 is a small sprout; each stage grows the bush from its base until stage 10 is full size.</summary>
    private static Mesh[] CreateStageMeshes(Mesh source)
    {
        var existing = AssetDatabase.LoadAllAssetsAtPath(StagesPath).OfType<Mesh>().ToDictionary(m => m.name);
        // The stage 0 mesh is the main asset; later stages are sub-assets of the same file.
        Mesh container = AssetDatabase.LoadMainAssetAtPath(StagesPath) as Mesh;
        if (container == null)
        {
            container = new Mesh { name = "DetailBush_Stage_00" };
            AssetDatabase.CreateAsset(container, StagesPath);
            existing[container.name] = container;
        }
        var result = new Mesh[Building.StageCount];
        Vector3[] vertices = source.vertices;
        float baseY = source.bounds.min.y;
        for (int stage = 0; stage < Building.StageCount; stage++)
        {
            float growth = GrowthAt(stage);
            string name = $"DetailBush_Stage_{stage:00}";
            if (!existing.TryGetValue(name, out Mesh mesh))
            {
                mesh = new Mesh { name = name };
                AssetDatabase.AddObjectToAsset(mesh, container);
            }
            mesh.Clear();
            var grown = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
                grown[i] = new Vector3(vertices[i].x * growth, baseY + (vertices[i].y - baseY) * growth, vertices[i].z * growth);
            mesh.indexFormat = source.indexFormat;
            mesh.vertices = grown;
            mesh.normals = source.normals;
            mesh.tangents = source.tangents;
            mesh.colors = source.colors;
            mesh.uv = source.uv;
            mesh.uv2 = source.uv2;
            mesh.subMeshCount = source.subMeshCount;
            for (int sub = 0; sub < source.subMeshCount; sub++) mesh.SetTriangles(source.GetTriangles(sub), sub);
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            result[stage] = mesh;
        }
        EditorUtility.SetDirty(container);
        AssetDatabase.SaveAssets();
        return result;
    }

    public static float GrowthAt(int stage) =>
        Mathf.Lerp(SproutScale, 1f, Mathf.Clamp01(stage / (float)ChunkAssetData.CompletedStage));
}
