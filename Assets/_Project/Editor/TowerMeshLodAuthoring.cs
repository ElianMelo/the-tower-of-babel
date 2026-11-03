using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Mesh-only LOD for the far tower (performance pass, 2026-11-03). Far views are vertex bound (~11.5 million vertices
/// per pass from outside), so distant pages draw simplified copies of the finished pieces with the same material and
/// shader. The switch distances keep the simplification under a pixel: at 70 m one pixel is ~7.5 cm at 1080p, at 140 m
/// ~15 cm, while the simplified silhouettes move by a few centimetres. Vines keep their full mesh.
/// </summary>
public static class TowerMeshLodAuthoring
{
    public const string Folder = "Assets/_Project/Models/Buildings/MeshLod";
    public const float Lod1Distance = 70, Lod2Distance = 140;
    /// <summary>Share of the full triangle count for LOD1 and LOD2.</summary>
    public const float Lod1Share = .5f, Lod2Share = .3f;
    public static readonly TowerAssetType[] Types = { TowerAssetType.Floor, TowerAssetType.Stair, TowerAssetType.Pillar, TowerAssetType.Arch, TowerAssetType.GlassWaterFloor };

    public static string MeshPath(TowerAssetType type, int level) => $"{Folder}/{type}_LOD{level}.asset";

    [MenuItem("Tower of Babel/Performance/Build far mesh LODs")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open this project's saved Tower scene in Edit Mode.");
        EnsureFolder(Folder);
        var chunks = UnityEngine.Object.FindObjectsByType<ChunkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(m => m.gameObject.scene == scene);
        var meshes = new Mesh[InstancedFarChunkRenderer.LodSlots];
        var report = new List<string>();
        foreach (var type in Types)
        {
            var prefab = chunks.AssetPrefabs.GetPrefab(type);
            var building = prefab.GetComponent<TowerOfBabel.Buildings.Building>();
            var source = building != null ? building.GetStageMesh(ChunkAssetData.CompletedStage) : prefab.GetComponent<MeshFilter>().sharedMesh;
            for (int level = 1; level <= 2; level++)
            {
                // Glass stairs lose their rounded slab edges below ~50%; they keep that level for both steps.
                float share = level == 1 || type == TowerAssetType.Stair ? Lod1Share : Lod2Share;
                var mesh = SaveMesh(Simplify(source, share), MeshPath(type, level));
                meshes[InstancedFarChunkRenderer.LodSlot(type, level)] = mesh;
                report.Add($"{type} LOD{level} {mesh.vertexCount}v/{mesh.triangles.Length / 3}t (full {source.vertexCount}v/{source.triangles.Length / 3}t)");
            }
        }
        var far = UnityEngine.Object.FindObjectsByType<InstancedFarChunkRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(r => r.gameObject.scene == scene);
        far.ConfigureMeshLod(meshes, Lod1Distance, Lod2Distance);
        EditorUtility.SetDirty(far);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }

    /// <summary>
    /// Generates Unity mesh LODs on a copy and keeps the level closest to the target share of triangles (48 at least),
    /// compacted into a new mesh with every vertex attribute and the source bounds.
    /// </summary>
    public static Mesh Simplify(Mesh source, float share)
    {
        var work = UnityEngine.Object.Instantiate(source);
        try
        {
            MeshLodUtility.GenerateMeshLods(work, 8);
            int full = (int)work.GetLod(0, 0).indexCount;
            int level = 0;
            for (int l = 1; l < work.lodCount; l++)
            {
                int count = (int)work.GetLod(0, l).indexCount;
                if (count < 48 * 3) break;
                if (Mathf.Abs(count - full * share) < Mathf.Abs((int)work.GetLod(0, level).indexCount - full * share)) level = l;
            }
            var indices = work.GetIndices(0, level, true);
            var map = new Dictionary<int, int>();
            var order = new List<int>();
            for (int i = 0; i < indices.Length; i++)
            {
                if (!map.TryGetValue(indices[i], out int remapped)) { remapped = order.Count; map[indices[i]] = remapped; order.Add(indices[i]); }
                indices[i] = remapped;
            }
            T[] Pick<T>(T[] values) => values == null || values.Length == 0 ? values : order.Select(i => values[i]).ToArray();
            var mesh = new Mesh { name = $"{source.name} LOD ({share:P0})", indexFormat = order.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = Pick(work.vertices);
            if (work.normals.Length > 0) mesh.normals = Pick(work.normals);
            if (work.tangents.Length > 0) mesh.tangents = Pick(work.tangents);
            if (work.colors32.Length > 0) mesh.colors32 = Pick(work.colors32);
            for (int channel = 0; channel < 4; channel++)
            {
                var uvs = new List<Vector4>();
                work.GetUVs(channel, uvs);
                if (uvs.Count > 0) mesh.SetUVs(channel, order.Select(i => uvs[i]).ToList());
            }
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.bounds = source.bounds;
            return mesh;
        }
        finally { UnityEngine.Object.DestroyImmediate(work); }
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null) { EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh); EditorUtility.SetDirty(existing); return existing; }
        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }
}
