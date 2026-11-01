using System;
using System.IO;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Resources;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Grows the three detail-phase vines over the real pillar and arch meshes, saves their construction stages and
/// prefabs, and replaces the cached detail data in the Tower scene. Vines cost Living Grass.
/// </summary>
public static class DetailVineAuthoring
{
    public const string PrefabFolder = "Assets/_Project/Prefabs/Buildings/";
    public const string MeshFolder = "Assets/_Project/Models/Foliage/";
    public const string MaterialPath = "Assets/_Project/Environment/Foliage/AeroVineLeaf.mat";
    private const string OutlineSettingsPath = "Assets/_Project/Scripts/Buildings/OutlineSettings.asset";
    private const string PreviewFolder = "_Screenshots-and-previews-to-delete/Vines/";

    public sealed class Variant
    {
        public string Name;
        public TowerAssetType Type;
        public VineGrowthBuilder.Style Style;
        public string PrefabPath => PrefabFolder + "Detail_Vine_" + Name + ".prefab";
        public string MeshPath => MeshFolder + "DetailVine_" + Name + ".asset";
    }

    public static readonly Variant[] Variants =
    {
        new() { Name = "Climbing", Type = TowerAssetType.Vine, Style = new VineGrowthBuilder.Style
            { seed = 1101, stems = 2, spreadHeight = 3.7f, spread = 1.6f, shortStems = .2f, drape = .35f, density = 1, leafSize = .18f, redChance = .02f, maxHeight = 5.85f } },
        new() { Name = "Draped", Type = TowerAssetType.VineDraped, Style = new VineGrowthBuilder.Style
            { seed = 2203, stems = 2, spreadHeight = 3.8f, spread = 2.1f, shortStems = .1f, drape = 1, density = .9f, leafSize = .17f, redChance = .01f, maxHeight = 5.85f } },
        new() { Name = "Dense", Type = TowerAssetType.VineDense, Style = new VineGrowthBuilder.Style
            { seed = 3307, stems = 3, spreadHeight = 3.5f, spread = .9f, shortStems = .45f, drape = .2f, density = 1.1f, leafSize = .19f, redChance = .06f, maxHeight = 5.85f } },
    };

    [MenuItem("Tower of Babel/Construction/Grow detail vines and place them on pillars")]
    public static void ApplyMenu() => Apply();

    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity")
            throw new InvalidOperationException("Open the Tower scene before growing vines.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>(FindObjectsInactive.Include);
        if (generator == null || manager == null) throw new InvalidOperationException("The Tower scene needs a TowerGenerator and ChunkManager.");

        var material = CreateMaterial();
        var built = new object[Variants.Length];
        generator.detailVineObjects = new GameObject[Variants.Length];
        using (var facade = new Facade(generator))
        {
            for (int i = 0; i < Variants.Length; i++)
            {
                var variant = Variants[i];
                var builder = VineGrowthBuilder.Grow(variant.Style, facade.Sample);
                var stages = SaveStages(variant, builder);
                var prefab = CreatePrefab(variant, stages, material);
                generator.detailVineObjects[i] = prefab;
                manager.AssetPrefabs.SetPrefab(variant.Type, prefab);
                built[i] = new { variant.Name, builder.StrandCount, builder.LeafCount, builder.HangingStrandCount,
                    length = Math.Round(builder.TotalLength, 2), triangles = stages[10].triangles.Length / 3, sproutTriangles = stages[0].triangles.Length / 3,
                    bounds = stages[10].bounds.ToString("F2") };
            }
        }
        EditorUtility.SetDirty(generator);
        RemoveBushAssets();

        var structure = manager.CachedChunks.SelectMany(c => c.Assets).Where(a => !a.AssetType.IsDetail()).ToArray();
        var vines = generator.BuildDetailVines(structure);
        manager.ReplaceCachedAssets(type => type.IsDetail(), vines);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        Vector2 c = generator.center;
        return new
        {
            variants = built,
            vines = vines.Count,
            byType = vines.GroupBy(v => v.AssetType).Select(g => new { type = g.Key.ToString(), count = g.Count() }).ToArray(),
            inside = vines.Count(v => Vector3.Dot(v.Rotation * Vector3.forward, new Vector3(v.Position.x - c.x, 0, v.Position.z - c.y)) < 0),
            mirrored = vines.Count(v => v.Scale.x < 0),
            perFloor = vines.GroupBy(v => manager.WorldToChunk(v.Position).FloorIndex).OrderBy(g => g.Key).Select(g => g.Count()).ToArray(),
            cachedAssets = manager.CachedAssetCount
        };
    }

    /// <summary>Front surface of a pillar with the two arches springing from it, built from the real meshes.</summary>
    public sealed class Facade : IDisposable
    {
        private readonly Scene scene;
        private readonly PhysicsScene physics;

        public Facade(TowerGenerator generator)
        {
            scene = EditorSceneManager.NewPreviewScene();
            physics = scene.GetPhysicsScene();
            var bays = generator.RingLayout(0).Take(2).ToArray();
            float chord = Vector3.Distance(bays[0].Pillar, bays[1].Pillar);
            Collider(generator.pillarObject, Vector3.zero, generator.pillarObject.transform.localScale);
            Vector3 archScale = generator.archObject.transform.localScale; archScale.x *= bays[0].ArchScale;
            // In vine space the arch to the next pillar runs along +X and the one from the previous pillar ends here.
            Collider(generator.archObject, new Vector3(0, generator.yOffset, 0), archScale);
            Collider(generator.archObject, new Vector3(-chord, generator.yOffset, 0), archScale);
            physics.Simulate(0);
        }

        private void Collider(GameObject prefab, Vector3 position, Vector3 scale)
        {
            var go = new GameObject(prefab.name + " facade");
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.SetPositionAndRotation(position, Quaternion.identity); go.transform.localScale = scale;
            go.AddComponent<MeshCollider>().sharedMesh = prefab.GetComponent<Building>().GetStageMesh(10);
        }

        public bool Sample(Vector2 point, out float depth, out Vector3 normal)
        {
            depth = 0; normal = Vector3.forward;
            if (!physics.Raycast(new Vector3(point.x, point.y, 3), Vector3.back, out RaycastHit hit, 6) || hit.point.z < -.05f) return false;
            depth = hit.point.z; normal = hit.normal;
            return normal.z > .2f;
        }

        public void Dispose() => EditorSceneManager.ClosePreviewScene(scene);
    }

    private static Material CreateMaterial()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath));
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var shader = Shader.Find("TowerOfBabel/Foliage/Clinging Vine");
        if (shader == null) throw new InvalidOperationException("Missing the Clinging Vine shader.");
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, MaterialPath); }
        material.shader = shader; material.enableInstancing = true;
        material.SetColor("_BaseColor", Color.white);
        material.SetColor("_SubsurfaceColor", new Color(.72f, 1, .38f));
        material.SetColor("_TranslucencyColor", new Color(.85f, 1, .45f));
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Mesh[] SaveStages(Variant variant, VineGrowthBuilder builder)
    {
        var existing = AssetDatabase.LoadAllAssetsAtPath(variant.MeshPath).OfType<Mesh>().ToDictionary(m => m.name);
        var result = new Mesh[Building.StageCount];
        Mesh container = AssetDatabase.LoadMainAssetAtPath(variant.MeshPath) as Mesh;
        for (int stage = 0; stage < Building.StageCount; stage++)
        {
            string name = $"DetailVine_{variant.Name}_Stage_{stage:00}";
            var grown = builder.BuildStage(stage, name);
            if (!existing.TryGetValue(name, out Mesh mesh))
            {
                mesh = new Mesh { name = name };
                if (container == null) { AssetDatabase.CreateAsset(mesh, variant.MeshPath); container = mesh; }
                else AssetDatabase.AddObjectToAsset(mesh, container);
            }
            mesh.Clear();
            mesh.indexFormat = grown.indexFormat;
            mesh.SetVertices(grown.vertices); mesh.SetNormals(grown.normals); mesh.SetColors(grown.colors);
            mesh.SetTriangles(grown.triangles, 0); mesh.RecalculateBounds();
            UnityEngine.Object.DestroyImmediate(grown);
            EditorUtility.SetDirty(mesh);
            result[stage] = mesh;
        }
        AssetDatabase.SaveAssets();
        return result;
    }

    private static GameObject CreatePrefab(Variant variant, Mesh[] stages, Material material)
    {
        var root = new GameObject("Detail_Vine_" + variant.Name, typeof(MeshFilter), typeof(MeshRenderer));
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh = stages[0];
            root.GetComponent<MeshRenderer>().sharedMaterial = material;
            // Covers the vine on the pillar face only, so the arch opening stays walkable.
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1.95f, .5f); collider.size = new Vector3(.95f, 3.9f, .3f);
            root.AddComponent<TowerAsset>().SetAssetType(variant.Type);
            var outline = root.AddComponent<TowerOfBabel.Outline>();
            var outlineData = new SerializedObject(outline);
            outlineData.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(OutlineSettingsPath);
            outlineData.ApplyModifiedPropertiesWithoutUndo();
            var building = root.AddComponent<Building>();
            var data = new SerializedObject(building);
            data.FindProperty("resourceType").intValue = (int)ResourceType.LivingGrass;
            data.FindProperty("resourceCostPerStage").intValue = 1;
            data.FindProperty("interactionDuration").floatValue = 2.5f;
            data.FindProperty("shakeStrength").floatValue = .015f;
            data.FindProperty("meshFilter").objectReferenceValue = root.GetComponent<MeshFilter>();
            data.FindProperty("outline").objectReferenceValue = outline;
            var meshes = data.FindProperty("stageMeshes");
            meshes.arraySize = Building.StageCount;
            for (int i = 0; i < Building.StageCount; i++) meshes.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            data.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, variant.PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // The detail bush was the placeholder detail; vines replace it.
    private static void RemoveBushAssets()
    {
        foreach (string path in new[] { PrefabFolder + "Detail_Bush.prefab", MeshFolder + "DetailBushStages.asset" })
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
    }

    /// <summary>Review stills of every variant and the growth stages on a pillar and its arches.</summary>
    public static string Capture()
    {
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
        Directory.CreateDirectory(PreviewFolder);
        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var bays = generator.RingLayout(0).Take(2).ToArray();
            float chord = Vector3.Distance(bays[0].Pillar, bays[1].Pillar);
            var lightGo = new GameObject("Sun"); SceneManager.MoveGameObjectToScene(lightGo, preview);
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(38, 150, 0);
            var fillGo = new GameObject("Fill"); SceneManager.MoveGameObjectToScene(fillGo, preview);
            var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .45f; fill.color = new Color(.75f, .88f, 1);
            fill.transform.rotation = Quaternion.Euler(20, -30, 0);
            var cameraGo = new GameObject("Camera"); SceneManager.MoveGameObjectToScene(cameraGo, preview);
            var camera = cameraGo.AddComponent<Camera>(); camera.scene = preview; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.62f, .8f, .97f); camera.fieldOfView = 40;

            GameObject Place(GameObject prefab, Vector3 position, Vector3 scale, Mesh mesh = null)
            {
                var go = new GameObject(prefab.name, typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(go, preview);
                go.transform.position = position; go.transform.localScale = scale;
                go.GetComponent<MeshFilter>().sharedMesh = mesh ?? prefab.GetComponent<Building>().GetStageMesh(10);
                go.GetComponent<MeshRenderer>().sharedMaterial = prefab.GetComponent<MeshRenderer>().sharedMaterial;
                return go;
            }
            void Bay(float x)
            {
                Place(generator.pillarObject, new Vector3(x, 0, 0), Vector3.one);
                Place(generator.archObject, new Vector3(x, generator.yOffset, 0), Vector3.one);
            }
            string Shot(string name, Vector3 at, Vector3 look, int width = 1600, int height = 900)
            {
                camera.transform.position = at; camera.transform.LookAt(look);
                var rt = new RenderTexture(width, height, 24) { antiAliasing = 4 }; var old = RenderTexture.active;
                // Offscreen renders skip draws whose shader variants are still compiling in the background.
                bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
                try
                {
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    var texture = new Texture2D(width, height, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, width, height), 0, 0); texture.Apply();
                    string path = PreviewFolder + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                    return path;
                }
                finally { ShaderUtil.allowAsyncCompilation = async; camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); }
            }

            // Three variants, one per pillar, plus a mirrored climbing vine.
            for (int i = -1; i <= 4; i++) Bay(i * chord);
            for (int i = 0; i < Variants.Length; i++)
                Place(AssetDatabase.LoadAssetAtPath<GameObject>(Variants[i].PrefabPath), new Vector3(i * chord, 0, 0), Vector3.one);
            Place(AssetDatabase.LoadAssetAtPath<GameObject>(Variants[0].PrefabPath), new Vector3(3 * chord, 0, 0), new Vector3(-1, 1, 1));
            string variants = Shot("Vine-Variants", new Vector3(3.2f, 2.8f, 9.5f), new Vector3(3.2f, 3.1f, 0));
            string close = Shot("Vine-Close", new Vector3(1.2f, 3.6f, 3.2f), new Vector3(.6f, 3.9f, .3f));
            foreach (var root in preview.GetRootGameObjects()) if (root.GetComponent<Camera>() == null && root.GetComponent<Light>() == null) UnityEngine.Object.DestroyImmediate(root);

            // Growth: stages 0, 2, 4, 6, 8 and 10 of the climbing vine.
            var climbing = AssetDatabase.LoadAssetAtPath<GameObject>(Variants[0].PrefabPath).GetComponent<Building>();
            for (int i = 0; i < 6; i++)
            {
                Bay(i * chord * 1.25f);
                Place(climbing.gameObject, new Vector3(i * chord * 1.25f, 0, 0), Vector3.one, climbing.GetStageMesh((byte)(i * 2)));
            }
            string growth = Shot("Vine-Growth-Stages", new Vector3(6.6f, 3, 12.5f), new Vector3(6.6f, 2.9f, 0), 1800, 800);
            return string.Join("\n", variants, close, growth);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
    }
}
