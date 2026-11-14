using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Builds the lava lamp sconce: a chrome hourglass lamp with a blue-and-green lava glass, hung by a three-chain cradle
/// from a torch arm. It reuses the aqua lamp's animated water projection, pointed upward. The tower places one on
/// every fourth pillar.
/// </summary>
public static class LavaLampAuthoring
{
    public const string PrefabPath = "Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab";
    public const string ModelFolder = "Assets/_Project/Models/Lighting";
    public const string SconceMeshPath = ModelFolder + "/LavaLamp_Sconce.asset";
    public const string GlassMeshPath = ModelFolder + "/LavaLamp_Glass.asset";
    public const string ChromePath = AeroAtmosphereAuthoring.Folder + "/LavaLampChrome.mat";
    public const string FluidPath = AeroAtmosphereAuthoring.Folder + "/LavaLampFluid.mat";
    public const string FluidShader = "TowerOfBabel/Lighting/Lava Lamp Fluid";
    public const string SceneObjectName = "Tower lava lamps";
    public const string BuildablePrefabPath = "Assets/_Project/Prefabs/Buildings/Detail_LavaLamp.prefab";
    public const string StageMeshPath = ModelFolder + "/LavaLamp_Stages.asset";
    private const string OutlineSettingsPath = "Assets/_Project/Scripts/Buildings/OutlineSettings.asset";
    public const int IngotsPerStage = 1;

    // Sconce space: origin where the wall plate meets the pillar, +Z out from the wall, Y up, arm at y = 0.
    public const float ArmReach = .52f;
    public const float LampDrop = 1.15f;
    public const float LampHeight = .80f;
    public const float GlassBottom = .245f, GlassHeight = .455f, GlassRadiusBottom = .095f, GlassRadiusTop = .055f;
    public const float CradleRadius = .15f, CradleHeight = .26f;
    public const float MountHeight = 3.6f;
    public const float ExteriorChance = .5f, LightReach = 90;
    public const int MaxLights = 96;
    public static Vector3 LampOrigin => new(0, -LampDrop, ArmReach);
    public static Vector3 LightPosition => LampOrigin + Vector3.up * (LampHeight + .045f);
    public static Vector3 GlowPosition => LampOrigin + Vector3.up * (GlassBottom + GlassHeight * .4f);

    [MenuItem("Tower of Babel/Environment/Apply tower lava lamps")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open this project's saved Tower scene in Edit Mode.");
        var prefab = BuildPrefab();

        var chunks = UnityEngine.Object.FindObjectsByType<ChunkManager>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(m => m.gameObject.scene == scene);
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var folder = scene.GetRootGameObjects().Single(r => r.name == "Lighting & Atmosphere").transform;
        var holder = folder.Find(SceneObjectName)?.gameObject;
        if (holder == null) { holder = new GameObject(SceneObjectName); holder.transform.SetParent(folder, false); }
        var lamps = holder.GetComponent<TowerLavaLamps>() ?? holder.AddComponent<TowerLavaLamps>();
        // Inside the tower, over the deck and stair ramp; skip only lamps that would touch the steps.
        var settings = new SerializedObject(lamps);
        settings.FindProperty("interior").boolValue = true;
        settings.FindProperty("stairHeadroom").floatValue = 0;
        // Random lamps on the outside of free pillars; every lamp in view, up to 24, carries its spotlight.
        settings.FindProperty("exteriorChance").floatValue = ExteriorChance;
        settings.FindProperty("maxLights").intValue = MaxLights;
        settings.FindProperty("lightReach").floatValue = LightReach;
        settings.ApplyModifiedPropertiesWithoutUndo();
        lamps.Configure(chunks, prefab, MountHeight, ShaftRadius(generator.pillarObject, MountHeight));
        EditorUtility.SetDirty(lamps);

        // Each lamp is a buildable detail asset: the tower builds its sconce from iron ingots, then it lights up.
        var buildable = BuildBuildablePrefab();
        generator.detailLavaLampObject = buildable;
        chunks.AssetPrefabs.SetPrefab(TowerAssetType.LavaLamp, buildable);
        EditorUtility.SetDirty(generator); EditorUtility.SetDirty(chunks);
        var lampAssets = lamps.BuildLampAssets();
        chunks.ReplaceCachedAssets(type => type == TowerAssetType.LavaLamp, lampAssets);
        lamps.Rebuild();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        int pillars = chunks.CachedChunks.Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar));
        return new
        {
            prefab = PrefabPath, buildable = BuildablePrefabPath, lamps = lamps.Placements.Count, lampAssets = lampAssets.Count,
            usesLampAssets = lamps.UsesLampAssets, pillars, pillarRadius = lamps.PillarRadius, cachedAssets = chunks.CachedAssetCount
        };
    }

    /// <summary>
    /// The buildable lamp: a detail-phase Building whose stage meshes assemble the chrome sconce (stage 10 is the
    /// finished sconce mesh itself), costing one iron ingot per stage.
    /// </summary>
    public static GameObject BuildBuildablePrefab()
    {
        EnsureFolder(ModelFolder); EnsureFolder("Assets/_Project/Prefabs/Buildings");
        var finished = AssetDatabase.LoadAssetAtPath<Mesh>(SconceMeshPath) ?? throw new InvalidOperationException("Build the lava lamp prefab first.");
        var stages = new Mesh[TowerOfBabel.Buildings.Building.StageCount];
        var container = AssetDatabase.LoadMainAssetAtPath(StageMeshPath) as Mesh;
        var existing = AssetDatabase.LoadAllAssetsAtPath(StageMeshPath).OfType<Mesh>().ToDictionary(m => m.name);
        for (int stage = 0; stage < stages.Length - 1; stage++)
        {
            var built = BuildSconceStage(stage);
            if (!existing.TryGetValue(built.name, out var mesh))
            {
                mesh = new Mesh { name = built.name };
                if (container == null) { AssetDatabase.CreateAsset(mesh, StageMeshPath); container = mesh; }
                else AssetDatabase.AddObjectToAsset(mesh, container);
            }
            mesh.Clear(); mesh.indexFormat = built.indexFormat;
            mesh.SetVertices(built.vertices); mesh.SetNormals(built.normals); mesh.SetUVs(0, built.uv);
            mesh.SetTriangles(built.triangles, 0); mesh.RecalculateBounds(); mesh.RecalculateTangents();
            UnityEngine.Object.DestroyImmediate(built); EditorUtility.SetDirty(mesh);
            stages[stage] = mesh;
        }
        stages[^1] = finished;
        AssetDatabase.SaveAssets();

        var root = new GameObject("Detail_LavaLamp", typeof(MeshFilter), typeof(MeshRenderer));
        try
        {
            root.GetComponent<MeshFilter>().sharedMesh = stages[0];
            var renderer = root.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = Chrome(); renderer.shadowCastingMode = ShadowCastingMode.On; renderer.lightProbeUsage = LightProbeUsage.Off;
            // Covers the whole hanging lamp, so it can be aimed at from the deck below.
            var collider = root.AddComponent<BoxCollider>();
            var bounds = finished.bounds;
            collider.center = bounds.center; collider.size = bounds.size + new Vector3(.12f, .06f, .06f);
            root.AddComponent<TowerAsset>().SetAssetType(TowerAssetType.LavaLamp);
            var outline = root.AddComponent<TowerOfBabel.Outline>();
            var outlineData = new SerializedObject(outline);
            outlineData.FindProperty("settings").objectReferenceValue = AssetDatabase.LoadMainAssetAtPath(OutlineSettingsPath);
            outlineData.ApplyModifiedPropertiesWithoutUndo();
            var building = new SerializedObject(root.AddComponent<TowerOfBabel.Buildings.Building>());
            building.FindProperty("resourceType").intValue = (int)TowerOfBabel.Resources.ResourceType.IronIngot;
            building.FindProperty("resourceCostPerStage").intValue = IngotsPerStage;
            building.FindProperty("interactionDuration").floatValue = 2.5f;
            building.FindProperty("shakeStrength").floatValue = .01f;
            building.FindProperty("meshFilter").objectReferenceValue = root.GetComponent<MeshFilter>();
            building.FindProperty("outline").objectReferenceValue = outline;
            var meshes = building.FindProperty("stageMeshes");
            meshes.arraySize = stages.Length;
            for (int i = 0; i < stages.Length; i++) meshes.GetArrayElementAtIndex(i).objectReferenceValue = stages[i];
            building.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(root, BuildablePrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    /// <summary>Pillar shaft radius below the capital, from the finished pillar mesh.</summary>
    public static float ShaftRadius(GameObject pillar, float height)
    {
        var mesh = pillar.GetComponent<TowerOfBabel.Buildings.Building>()?.GetStageMesh(10) ?? pillar.GetComponent<MeshFilter>().sharedMesh;
        float radius = 0;
        foreach (var v in mesh.vertices)
            if (v.y > .3f && v.y < height + .1f) radius = Mathf.Max(radius, new Vector2(v.x, v.z).magnitude);
        return radius > 0 ? radius * pillar.transform.localScale.x : .38f;
    }

    public static GameObject BuildPrefab()
    {
        EnsureFolder(ModelFolder); EnsureFolder(AeroAtmosphereAuthoring.Folder); EnsureFolder("Assets/_Project/Prefabs/Lighting");
        var sconce = SaveMesh(BuildSconceMesh(), SconceMeshPath);
        var glass = SaveMesh(BuildGlassMesh(), GlassMeshPath);
        var chrome = Chrome();
        var fluid = Fluid();
        var cookie = AssetDatabase.LoadAssetAtPath<Material>(AeroAtmosphereAuthoring.CookiePath);
        if (cookie == null) throw new InvalidOperationException("Apply the aqua projection lamp first: its cookie material is missing.");

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("LavaProjectionLamp");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview);
            Renderer Piece(string name, Mesh mesh, Material material, Vector3 position, ShadowCastingMode shadows)
            {
                var go = new GameObject(name); go.transform.SetParent(root.transform, false); go.transform.localPosition = position;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = shadows;
                r.lightProbeUsage = LightProbeUsage.Off;
                return r;
            }
            Piece("Chrome sconce", sconce, chrome, Vector3.zero, ShadowCastingMode.On);
            Piece("Lava glass", glass, fluid, LampOrigin + Vector3.up * GlassBottom, ShadowCastingMode.Off);

            var lightObject = new GameObject("Water projection spotlight");
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localPosition = LightPosition;
            // Upward, leaning a little toward the wall so the water pattern washes the arch and pillar above.
            lightObject.transform.localRotation = Quaternion.LookRotation(new Vector3(0, 1, -.32f), Vector3.forward);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Spot; light.color = new Color(.2f, .72f, 1f);
            light.intensity = 30; light.range = 9; light.spotAngle = 110; light.innerSpotAngle = 70;
            light.shadowBias = .025f; light.shadowNormalBias = .12f; light.shadowNearPlane = .05f;
            var lightData = lightObject.AddComponent<UniversalAdditionalLightData>();
            var settings = new SerializedObject(lightData);
            settings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            // Soft blue glow from the glass onto its own chrome, the chains and the pillar behind it.
            var glowObject = new GameObject("Lava glow");
            glowObject.transform.SetParent(root.transform, false);
            glowObject.transform.localPosition = GlowPosition;
            var glowLight = glowObject.AddComponent<Light>();
            glowLight.type = LightType.Point; glowLight.color = new Color(.16f, .5f, 1f);
            glowLight.intensity = 1.8f; glowLight.range = 2.6f; glowLight.shadows = LightShadows.None;
            glowObject.AddComponent<UniversalAdditionalLightData>();
            var lamp = root.AddComponent<AeroProjectionLamp>();
            lamp.projection = light; lamp.cookieMaterial = cookie; lamp.glowingCore = null;
            lamp.glow = new Color(.1f, 3.2f, 5.5f);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); }
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    private static Material Chrome()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(ChromePath);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, ChromePath); }
        m.SetColor("_BaseColor", new Color(.86f, .88f, .92f)); m.SetFloat("_Metallic", .92f); m.SetFloat("_Smoothness", .84f);
        m.SetFloat("_EnvironmentReflections", 1); m.SetFloat("_SpecularHighlights", 1);
        m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    private static Material Fluid()
    {
        var shader = Shader.Find(FluidShader) ?? throw new InvalidOperationException("Lava lamp shader did not compile.");
        var m = AssetDatabase.LoadAssetAtPath<Material>(FluidPath);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, FluidPath); }
        m.shader = shader;
        m.SetFloat("_VesselHeight", GlassHeight); m.SetFloat("_RadiusBottom", GlassRadiusBottom); m.SetFloat("_RadiusTop", GlassRadiusTop);
        m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
    }

    private static Mesh SaveMesh(Mesh mesh, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
        existing.Clear(); EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
        EditorUtility.SetDirty(existing); return existing;
    }

    // ---------- Geometry ----------

    /// <summary>The glass vessel, origin at its bottom centre; its tapered side is what the fluid shader marches inside.</summary>
    public static Mesh BuildGlassMesh()
    {
        var b = new MeshBuilder();
        var profile = new List<Vector2> { new(0, 0), new(GlassRadiusBottom, 0), new(GlassRadiusBottom, 0) };
        for (int i = 0; i <= 10; i++) profile.Add(new Vector2(Mathf.Lerp(GlassRadiusBottom, GlassRadiusTop, i / 10f), GlassHeight * i / 10f));
        profile.Add(new Vector2(GlassRadiusTop, GlassHeight)); profile.Add(new Vector2(0, GlassHeight));
        b.Lathe(profile, 40, Matrix4x4.identity);
        var mesh = b.ToMesh("Lava lamp glass");
        return mesh;
    }

    /// <summary>Everything chrome: wall rosette, torch arm and brace, hook, three chains, lamp base, collar and cap.</summary>
    public static Mesh BuildSconceMesh() => BuildSconceStage(TowerOfBabel.Buildings.Building.StageCount - 1, "Lava lamp sconce");

    /// <summary>
    /// The sconce as it is assembled: stage 0 is the wall rosette, and each stage adds the next part (arm, brace, hook,
    /// one chain at a time, collar, the two halves of the base) until stage 10 caps the lamp. The lava glass is not
    /// part of it; it appears with the light once the lamp is finished.
    /// </summary>
    public static Mesh BuildSconceStage(int stage, string name = null)
    {
        var b = new MeshBuilder();
        var lamp = Matrix4x4.Translate(LampOrigin);
        Vector3 apex = new(0, -.09f, ArmReach);
        var lug = LampOrigin + Vector3.up * .25f;
        Vector3 Direction(int i) { float angle = (i * 120 + 90) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)); }
        Vector3 Eye(int i) => lug + Direction(i) * CradleRadius + Vector3.up * .01f;
        var parts = new List<Action>
        {
            () =>
            {
                // Wall rosette: a stepped disk facing out from the pillar, and the small foot rosette for the brace.
                var toWall = Matrix4x4.TRS(new Vector3(0, 0, -.012f), Quaternion.Euler(90, 0, 0), Vector3.one);
                b.Lathe(new List<Vector2> { new(0, 0), new(.075f, 0), new(.075f, 0), new(.078f, .008f), new(.07f, .018f), new(.05f, .022f), new(.045f, .03f), new(0, .03f) }, 28, toWall);
                var footWall = Matrix4x4.TRS(new Vector3(0, -.17f, -.012f), Quaternion.Euler(90, 0, 0), Vector3.one);
                b.Lathe(new List<Vector2> { new(0, 0), new(.034f, 0), new(.034f, 0), new(.036f, .008f), new(.022f, .02f), new(0, .022f) }, 18, footWall);
            },
            () =>
            {
                // Torch arm: rises gently out of the rosette and ends in a drop to the hook.
                b.Tube(Bezier(new Vector3(0, 0, 0), new Vector3(0, .07f, .3f), new Vector3(0, .015f, ArmReach), 18), .016f, 10, true);
                b.Tube(new List<Vector3> { new(0, .02f, ArmReach - .005f), new(0, -.03f, ArmReach) }, .011f, 8, true);
            },
            () =>
            {
                // Brace from the lower rosette sweeping up under the arm, with a small scroll finial past the arm.
                b.Tube(Bezier(new Vector3(0, -.17f, 0), new Vector3(0, -.15f, .26f), new Vector3(0, .04f, .33f), 16), .009f, 8, true);
                b.Sphere(new Vector3(0, .018f, ArmReach + .014f), .02f, 12, 8);
            },
            () =>
            {
                // Hook and the hanging ring the cradle chains share.
                b.Link(new Vector3(0, -.047f, ArmReach), Vector3.down, Vector3.right, .017f, .017f, .0045f);
                b.Link(new Vector3(0, -.075f, ArmReach), Vector3.down, Vector3.forward, .016f, .016f, .0045f);
            },
            () => Chain(b, apex, Eye(0) + Vector3.up * .01f),
            () => Chain(b, apex, Eye(1) + Vector3.up * .01f),
            () => Chain(b, apex, Eye(2) + Vector3.up * .01f),
            () =>
            {
                // Cradle collar around the base rim with three lugs and eyelets for the chains.
                b.Ring(lug, .112f, .0055f, 36, 6);
                for (int i = 0; i < 3; i++)
                {
                    b.Tube(new List<Vector3> { lug + Direction(i) * .105f, lug + Direction(i) * (CradleRadius - .008f) }, .0055f, 6, true);
                    b.Link(Eye(i), Vector3.up, Direction(i), .011f, .011f, .0035f);
                }
            },
            // Hourglass base: two cones meeting at a waist, the upper one holding the glass. The lower cone is closed at
            // the waist so the half-built base never shows a hole.
            () => b.Lathe(new List<Vector2> {
                new(0, 0), new(.118f, 0), new(.118f, 0), new(.123f, .008f), new(.118f, .017f), new(.118f, .017f),
                new(.071f, .125f), new(.068f, .13f), new(.071f, .135f), new(.071f, .135f), new(0, .135f)
            }, 36, lamp),
            () => b.Lathe(new List<Vector2> {
                new(.071f, .135f), new(.071f, .135f),
                new(.104f, .243f), new(.104f, .243f), new(.109f, .25f), new(.107f, .258f), new(.107f, .258f), new(.093f, .258f), new(.093f, .258f), new(.093f, .25f)
            }, 36, lamp),
            // Cap: a cone over the top of the glass with a small finial.
            () => b.Lathe(new List<Vector2> {
                new(.05f, GlassBottom + GlassHeight - .012f), new(.062f, GlassBottom + GlassHeight - .006f), new(.062f, GlassBottom + GlassHeight - .006f),
                new(.062f, GlassBottom + GlassHeight + .004f), new(.062f, GlassBottom + GlassHeight + .004f), new(.043f, LampHeight - .006f),
                new(.043f, LampHeight - .006f), new(.04f, LampHeight), new(.04f, LampHeight), new(.012f, LampHeight + .006f), new(0, LampHeight + .006f)
            }, 30, lamp),
        };
        for (int i = 0; i <= stage && i < parts.Count; i++) parts[i]();
        return b.ToMesh(name ?? "Lava lamp stage " + stage);
    }

    // Oval links, each turned a quarter about the chain from the last, so they read as interlocked.
    private static void Chain(MeshBuilder b, Vector3 from, Vector3 to)
    {
        const float half = .017f, width = .0095f, wire = .003f;
        float pitch = 2 * (half - wire) * .92f;
        var axis = (to - from).normalized;
        float length = Vector3.Distance(from, to);
        int count = Mathf.Max(1, Mathf.RoundToInt(length / pitch));
        var side = Vector3.Cross(axis, Vector3.forward).sqrMagnitude > .01f ? Vector3.Cross(axis, Vector3.forward).normalized : Vector3.right;
        for (int i = 0; i < count; i++)
        {
            var center = from + axis * ((i + .5f) * length / count);
            var across = i % 2 == 0 ? side : Vector3.Cross(axis, side).normalized;
            b.Link(center, axis, across, half, width, wire);
        }
    }

    private static List<Vector3> Bezier(Vector3 a, Vector3 c, Vector3 d, int steps)
    {
        var result = new List<Vector3>();
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            result.Add((1 - t) * (1 - t) * a + 2 * (1 - t) * t * c + t * t * d);
        }
        return result;
    }

    private sealed class MeshBuilder
    {
        private readonly List<Vector3> vertices = new(), normals = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> triangles = new();

        /// <summary>Turns a (radius, height) profile about Y. A repeated point makes a hard edge.</summary>
        public void Lathe(List<Vector2> profile, int segments, Matrix4x4 m)
        {
            int start = vertices.Count, n = profile.Count;
            for (int s = 0; s <= segments; s++)
            {
                float a = s / (float)segments * Mathf.PI * 2;
                var radial = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                for (int i = 0; i < n; i++)
                {
                    var prev = i > 0 && profile[i - 1] != profile[i] ? profile[i - 1] : profile[i];
                    var next = i < n - 1 && profile[i + 1] != profile[i] ? profile[i + 1] : profile[i];
                    if (prev == next) next = i < n - 1 ? profile[Mathf.Min(n - 1, i + 2)] : profile[i];
                    var tangent = (next - prev).normalized;
                    var n2 = new Vector2(tangent.y, -tangent.x);
                    vertices.Add(m.MultiplyPoint3x4(radial * profile[i].x + Vector3.up * profile[i].y));
                    normals.Add(m.MultiplyVector(radial * n2.x + Vector3.up * n2.y).normalized);
                    uvs.Add(new Vector2(s / (float)segments, i / (float)(n - 1)));
                }
            }
            for (int s = 0; s < segments; s++)
                for (int i = 0; i < n - 1; i++)
                {
                    if (profile[i] == profile[i + 1]) continue;
                    int a = start + s * n + i, c = start + (s + 1) * n + i;
                    triangles.AddRange(new[] { a, a + 1, c, c, a + 1, c + 1 });
                }
        }

        public void Tube(List<Vector3> path, float radius, int sides, bool caps)
        {
            int start = vertices.Count;
            var tangent = (path[1] - path[0]).normalized;
            var normal = Vector3.Cross(tangent, Mathf.Abs(tangent.y) < .9f ? Vector3.up : Vector3.right).normalized;
            for (int i = 0; i < path.Count; i++)
            {
                var t = (path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                normal = Vector3.ProjectOnPlane(normal, t).normalized;
                var binormal = Vector3.Cross(t, normal);
                for (int s = 0; s <= sides; s++)
                {
                    float a = s / (float)sides * Mathf.PI * 2;
                    var dir = normal * Mathf.Cos(a) + binormal * Mathf.Sin(a);
                    vertices.Add(path[i] + dir * radius); normals.Add(dir); uvs.Add(new Vector2(s / (float)sides, i / (float)(path.Count - 1)));
                }
            }
            for (int i = 0; i < path.Count - 1; i++)
                for (int s = 0; s < sides; s++)
                {
                    int a = start + i * (sides + 1) + s, c = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            if (!caps) return;
            Sphere(path[0], radius, sides, 4);
            Sphere(path[^1], radius, sides, 4);
        }

        public void Sphere(Vector3 center, float radius, int segments, int rings)
        {
            var profile = new List<Vector2>();
            for (int i = 0; i <= rings; i++)
            {
                float a = -Mathf.PI / 2 + Mathf.PI * i / rings;
                profile.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
            }
            profile[0] = new Vector2(0, -radius); profile[^1] = new Vector2(0, radius);
            Lathe(profile, segments, Matrix4x4.Translate(center));
        }

        /// <summary>Horizontal torus ring about Y.</summary>
        public void Ring(Vector3 center, float major, float minor, int segments, int sides)
        {
            var path = new List<Vector3>();
            for (int i = 0; i <= segments; i++) { float a = i / (float)segments * Mathf.PI * 2; path.Add(center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * major); }
            Tube(path, minor, sides, false);
        }

        /// <summary>Oval chain link: long half-length along <paramref name="axis"/>, width across <paramref name="across"/>.</summary>
        public void Link(Vector3 center, Vector3 axis, Vector3 across, float half, float width, float wire)
        {
            axis.Normalize(); across = Vector3.ProjectOnPlane(across, axis).normalized;
            var path = new List<Vector3>();
            const int segments = 12;
            for (int i = 0; i <= segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2;
                path.Add(center + axis * (Mathf.Sin(a) * (half - wire)) + across * (Mathf.Cos(a) * (width - wire)));
            }
            Tube(path, wire, 4, false);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, indexFormat = vertices.Count > 65000 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16 };
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }
    }
}
