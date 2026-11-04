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

    public static readonly Variant[] Variants = Enumerable.Range(0, TowerAssetTypeExtensions.VineCount)
        .Select(i => new Variant { Name = i.ToString(), Type = TowerAssetTypeExtensions.VineTypes[i], Style = RandomStyle(i) }).ToArray();

    /// <summary>
    /// Shapes cycle through four archetypes (climbing, draped, dense, sparse) so the set always covers them, and every
    /// other setting is randomized per shape, so no two vines on the tower grow alike.
    /// </summary>
    public enum Archetype { Climbing, Draped, Dense, Sparse, PillarOnly, ArchOnly }
    public static Archetype ArchetypeOf(int index) => (Archetype)(index % 6);

    public static VineGrowthBuilder.Style RandomStyle(int index)
    {
        var r = new System.Random(9001 + index * 131);
        float R(float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());
        var kind = ArchetypeOf(index);
        var style = new VineGrowthBuilder.Style
        {
            seed = 1100 + index * 977,
            stems = kind == Archetype.Dense ? 3 : 1 + r.Next(3),
            spreadHeight = R(3.3f, 4f),
            // Wide reach: the facade includes the neighbouring bays, so stems can run past the next pillar.
            spread = kind switch { Archetype.Draped => R(2.4f, 3.4f), Archetype.Dense => R(1.4f, 2.2f), Archetype.Sparse => R(1f, 1.8f), _ => R(1.8f, 3f) },
            shortStems = kind == Archetype.Dense ? R(.35f, .6f) : R(0, .3f),
            drape = kind == Archetype.Draped ? R(.8f, 1) : R(0, .45f),
            density = kind == Archetype.Dense ? R(1f, 1.15f) : kind == Archetype.Sparse ? R(.75f, .9f) : R(.9f, 1.1f),
            leafSize = R(.16f, .2f),
            redChance = R(0, .05f),
            maxHeight = 5.85f,
            wander = R(.25f, .6f),
            sideBias = R(.5f, .85f),
            spreadJitter = R(.2f, .6f)
        };
        if (kind == Archetype.PillarOnly)
        {
            // Climbs the pillar and stops under the capital; never reaches the arch.
            style.stems = 2 + r.Next(3); style.maxHeight = 3.85f; style.spreadHeight = 99; style.drape = 0;
            style.shortStems = R(.2f, .5f); style.density = R(1.05f, 1.25f);
        }
        else if (kind == Archetype.ArchOnly)
        {
            // Roots on the spandrel above the capital and only covers the arch, with strands hanging into the opening.
            style.stems = 2 + r.Next(2); style.rootHeight = R(4.15f, 4.4f); style.spreadHeight = style.rootHeight - .1f;
            style.spread = R(1.8f, 3.2f); style.drape = R(.5f, 1);
        }
        return style;
    }

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
                var builder = GrowWithinBudget(variant, facade);
                var stages = SaveStages(variant, builder);
                var prefab = CreatePrefab(variant, stages, material);
                generator.detailVineObjects[i] = prefab;
                manager.AssetPrefabs.SetPrefab(variant.Type, prefab);
                built[i] = new { variant.Name, archetype = ArchetypeOf(i).ToString(), builder.StrandCount, builder.LeafCount, builder.HangingStrandCount,
                    length = Math.Round(builder.TotalLength, 2), triangles = stages[10].triangles.Length / 3, sproutTriangles = stages[0].triangles.Length / 3,
                    bounds = stages[10].bounds.ToString("F2") };
            }
        }
        EditorUtility.SetDirty(generator);
        RemoveBushAssets();

        var structure = manager.CachedChunks.SelectMany(c => c.Assets).Where(a => !a.AssetType.IsDetail()).ToArray();
        var vines = generator.BuildDetailVines(structure);
        manager.ReplaceCachedAssets(type => type.IsVine(), vines);
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

    /// <summary>Thousands of vines are drawn at once, so each shape stays near this many triangles when fully grown.</summary>
    public const int TriangleBudget = 6000;

    // Wide-spreading shapes collect many leaves; thin their leaves and branches until the grown vine fits the budget.
    private static VineGrowthBuilder GrowWithinBudget(Variant variant, Facade facade)
    {
        var style = variant.Style;
        var builder = VineGrowthBuilder.Grow(style, facade.Sample);
        for (int attempt = 0; attempt < 4; attempt++)
        {
            var grown = builder.BuildStage(10);
            int triangles = grown.triangles.Length / 3;
            UnityEngine.Object.DestroyImmediate(grown);
            if (triangles <= TriangleBudget) break;
            style.density *= Mathf.Clamp((float)TriangleBudget / triangles, .55f, .92f);
            style.leafSize *= 1.05f;
            builder = VineGrowthBuilder.Grow(style, facade.Sample);
        }
        return builder;
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
            Vector3 archScale = generator.archObject.transform.localScale; archScale.x *= bays[0].ArchScale;
            // In vine space the arch to the next pillar runs along +X. Two bays each way let stems run past the
            // neighbouring pillars.
            for (int bay = -2; bay <= 2; bay++)
            {
                if (Mathf.Abs(bay) < 2) Collider(generator.pillarObject, new Vector3(bay * chord, 0, 0), generator.pillarObject.transform.localScale);
                if (bay < 2) Collider(generator.archObject, new Vector3(bay * chord, generator.yOffset, 0), archScale);
            }
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
        // Far foliage reads light green: lift toward lime and add a soft glow with distance.
        material.SetFloat("_DistanceLift", .85f); material.SetFloat("_DistanceGlow", .55f);
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
            var collider = root.AddComponent<BoxCollider>();
            Bounds grown = stages[10].bounds;
            if (grown.min.y < 3.5f)
            {
                // Covers the vine on the pillar face only, so the arch opening and the stair ramp stay walkable.
                float top = Mathf.Min(3.9f, grown.max.y);
                collider.center = new Vector3(0, top * .5f, .49f); collider.size = new Vector3(.9f, top, .18f);
            }
            else
            {
                // Arch-only vines sit well above head height, so the box can cover the whole vine on the arch face.
                collider.center = new Vector3(grown.center.x, grown.center.y, .32f); collider.size = new Vector3(grown.size.x, grown.size.y, .2f);
            }
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

    // The placeholder bush and the first three named vine shapes are superseded by the numbered shapes.
    private static void RemoveBushAssets()
    {
        var stale = new[] { "Detail_Bush.prefab", "Detail_Vine_Climbing.prefab", "Detail_Vine_Draped.prefab", "Detail_Vine_Dense.prefab" }.Select(n => PrefabFolder + n)
            .Concat(new[] { "DetailBushStages.asset", "DetailVine_Climbing.asset", "DetailVine_Draped.asset", "DetailVine_Dense.asset" }.Select(n => MeshFolder + n));
        foreach (string path in stale)
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
    }

    /// <summary>
    /// Rebuilds a real section of the cached tower (pillars, arches, stairs and finished vines) around one pillar and
    /// renders it from outside and inside. The vines on the right are also drawn the way the distant renderer draws
    /// them (instanced, no light probes), to compare their colour with the near view.
    /// </summary>
    public static string CaptureTower(int floor = 4, float angleDegrees = 20)
    {
        WarmShaders();
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>(FindObjectsInactive.Include);
        Directory.CreateDirectory(PreviewFolder);
        var all = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
        Vector2 c = generator.center;
        float a = angleDegrees * Mathf.Deg2Rad;
        float bottom = floor * generator.heighIncrease;
        var pillar = all.Where(p => p.AssetType == TowerAssetType.Pillar && Mathf.Abs(p.Position.y - bottom) < .1f)
            .OrderBy(p => Vector2.Distance(new Vector2(p.Position.x, p.Position.z) - c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 200)).First();
        Vector3 focus = pillar.Position;
        Vector3 outward = new Vector3(focus.x - c.x, 0, focus.z - c.y).normalized, side = Vector3.Cross(Vector3.up, outward);
        var preview = EditorSceneManager.NewPreviewScene();
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            void Add(Mesh mesh, Material material, Matrix4x4 matrix)
            {
                var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); SceneManager.MoveGameObjectToScene(go, preview);
                go.transform.SetPositionAndRotation(matrix.GetColumn(3), matrix.rotation); go.transform.localScale = matrix.lossyScale;
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
                // Light near and far copies with the same ambient probe, so only the draw path differs.
                var renderer = go.GetComponent<MeshRenderer>();
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.CustomProvided;
                var block = new MaterialPropertyBlock();
                block.CopySHCoefficientArraysFrom(new[] { RenderSettings.ambientProbe });
                renderer.SetPropertyBlock(block);
            }
            var near = all.Where(p => (p.Position - focus).sqrMagnitude < 14 * 14 && p.Position.y > bottom - 7 && p.Position.y < bottom + 7 && !p.AssetType.IsFloor()).ToArray();
            foreach (var asset in near)
            {
                var prefab = manager.AssetPrefabs.GetPrefab(asset.AssetType);
                if (prefab == null) continue;
                var mesh = prefab.GetComponent<Building>().GetStageMesh(10);
                Add(mesh, prefab.GetComponent<MeshRenderer>().sharedMaterial, asset.ObjectToWorld);
            }
            var deck = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(deck, preview);
            deck.transform.position = focus + Vector3.up * .25f - outward * 3; deck.transform.rotation = Quaternion.LookRotation(outward);
            deck.transform.localScale = new Vector3(30, .1f, 9);
            deck.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ResourceStationKit.Models + "Materials/Workshop_Pearl.mat");
            var sun = new GameObject("Sun"); SceneManager.MoveGameObjectToScene(sun, preview);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.LookRotation(-outward * .6f + Vector3.down + side * .4f);
            var camGo = new GameObject("Camera"); SceneManager.MoveGameObjectToScene(camGo, preview);
            var camera = camGo.AddComponent<Camera>(); camera.scene = preview; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.62f, .8f, .97f); camera.fieldOfView = 50;
            // Far-renderer path: the same vines drawn instanced with no probes, beside the near ones.
            var farVines = near.Where(v => v.AssetType.IsVine()).ToArray();
            string Shot(string name, Vector3 eye, Vector3 look)
            {
                camera.transform.position = eye; camera.transform.LookAt(look);
                foreach (var group in farVines.GroupBy(v => v.AssetType))
                {
                    var prefab = manager.AssetPrefabs.GetPrefab(group.Key);
                    var matrices = group.Select(v => Matrix4x4.Translate(side * 30) * v.ObjectToWorld).ToArray();
                    // Same lighting as the distant renderer: no baked probes, so the scene's ambient probe is provided.
                    var rp = new RenderParams(prefab.GetComponent<MeshRenderer>().sharedMaterial)
                        { camera = camera, lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.CustomProvided,
                          matProps = InstancedFarChunkRenderer.AmbientProbeProperties(), worldBounds = new Bounds(focus + side * 30, Vector3.one * 60) };
                    Graphics.RenderMeshInstanced(rp, prefab.GetComponent<Building>().GetStageMesh(10), 0, matrices);
                }
                var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 }; var old = RenderTexture.active;
                try
                {
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    var texture = new Texture2D(1600, 900, TextureFormat.RGB24, false); texture.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); texture.Apply();
                    string path = PreviewFolder + name + ".png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
                    return path;
                }
                finally { camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); }
            }
            string outside = Shot("Tower-Outside", focus + outward * 9 + Vector3.up * 3, focus + Vector3.up * 3);
            string inside = Shot("Tower-Inside", focus - outward * 6 + Vector3.up * 2.4f, focus + Vector3.up * 3);
            string far = Shot("Tower-Far-Compare", focus + outward * 34 + side * 15 + Vector3.up * 6, focus + side * 15 + Vector3.up * 3);
            return string.Join("\n", outside, inside, far) + $"\nvines near pillar: {farVines.Length}";
        }
        finally { ShaderUtil.allowAsyncCompilation = async; EditorSceneManager.ClosePreviewScene(preview); }
    }

    // Offscreen preview renders draw nothing, or a stale variant, for passes still compiling; compile them first.
    private static void WarmShaders()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material == null) return;
        for (int i = 0; i < material.passCount; i++) ShaderUtil.CompilePass(material, i, true);
    }

    /// <summary>Review stills of every variant and the growth stages on a pillar and its arches.</summary>
    public static string Capture()
    {
        WarmShaders();
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

            // Every shape, one per pillar.
            for (int i = -1; i <= Variants.Length; i++) Bay(i * chord);
            for (int i = 0; i < Variants.Length; i++)
                Place(AssetDatabase.LoadAssetAtPath<GameObject>(Variants[i].PrefabPath), new Vector3(i * chord, 0, 0), Vector3.one);
            float middle = (Variants.Length - 1) * chord * .5f;
            string variants = Shot("Vine-Variants", new Vector3(middle, 3, 23), new Vector3(middle, 3, 0), 2400, 800);
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
