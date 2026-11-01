using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static ResourceStationKit;
using Kind = TowerOfBabel.Resources.Interaction.ProcessorMachineAnimation.ProcessKind;

/// <summary>
/// Grass → Living Grass: a gatherable meadow tuft and a glass-dome planter that grows it into Living Grass,
/// the material the detail vines are built from. All models are generated here (no Blender source).
/// Re-running keeps prefab GUIDs, scene placements and definitions.
/// </summary>
public static class GrassWorkshopAuthoring
{
    public const string NodePrefab = "Assets/_Project/Prefabs/Resources/Grass.prefab";
    public const string BlockPrefab = "Assets/_Project/Prefabs/Resources/LivingGrassBlock.prefab";
    public const string PlanterPrefab = "Assets/_Project/Prefabs/Processor/GrassPlanter.prefab";
    public const string MeshPath = Models + "Grass/GrassMeshes.asset";
    public const ulong NodeId = 3004;
    public const string ProcessorId = "tower-grass-planter-01";
    public const float NodeX = 25f, PlanterX = 28.2f;
    private const string Data = "Assets/_Project/Resources/Definitions/";
    private static readonly Color LeafGlowColor = new(.45f, 1, .35f);
    private static readonly Dictionary<string, Mesh> meshes = new();
    private static Mesh container;

    [MenuItem("Tower of Babel/Resources/Apply grass and living grass")]
    public static object Apply()
    {
        RequireCleanTowerScene();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        Directory.CreateDirectory(Path.GetDirectoryName(MeshPath));
        foreach (string sound in new[] { "GrassStroke", "GrassBurst", "Respawn", "CondenserHum", "BlockChime" }) MakeSound(sound);
        LoadMeshes();

        var grass = Definition("Grass", ResourceType.Grass, 2, 2.6f, 6);
        var living = Definition("Living Grass", ResourceType.LivingGrass, 1, 3, 0);
        var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Data + "ResourceDefinitions.asset");
        Add(collection, grass); Add(collection, living);

        var block = BuildBlock();
        BuildNode(grass);
        BuildPlanter(grass, living, block);
        AssetDatabase.SaveAssets();
        PlaceOrGround(NodePrefab, "Grass", NodeX);
        PlaceOrGround(PlanterPrefab, "GrassPlanter", PlanterX);
        SetIcon(grass, RenderIcon("Grass", AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefab), "GrassTuft"));
        SetIcon(living, RenderIcon("LivingGrass", block));
        SaveTowerScene();
        return new
        {
            node = NodePrefab, planter = PlanterPrefab, block = BlockPrefab,
            tuftTriangles = Get("GrassTuft").triangles.Length / 3, planterRenderers = AssetDatabase.LoadAssetAtPath<GameObject>(PlanterPrefab).GetComponentsInChildren<Renderer>(true).Length
        };
    }

    // ------------------------------------------------------------------ data
    private static ResourceDefinition Definition(string name, ResourceType type, int amount, float duration, float cooldown)
    {
        string file = name.Replace(" ", ""), path = Data + file + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
        if (d == null) { d = ScriptableObject.CreateInstance<ResourceDefinition>(); AssetDatabase.CreateAsset(d, path); }
        var so = new SerializedObject(d);
        so.FindProperty("resourceId").stringValue = file.ToLowerInvariant();
        so.FindProperty("resourceType").intValue = (int)type; so.FindProperty("displayName").stringValue = name;
        so.FindProperty("amountGathered").intValue = amount; so.FindProperty("interactionDuration").floatValue = duration;
        so.FindProperty("respawnCooldown").floatValue = cooldown;
        // Grounded like the sand pile: the tuft itself is pulled down by its feedback, the node never weaves.
        so.FindProperty("shakeStrength").floatValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo(); return d;
    }

    private static void Add(ResourceDefinitionCollection collection, ResourceDefinition definition)
    {
        var so = new SerializedObject(collection); var entries = so.FindProperty("definitions");
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).objectReferenceValue == definition) return;
        entries.arraySize++; entries.GetArrayElementAtIndex(entries.arraySize - 1).objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetIcon(ResourceDefinition definition, Sprite icon)
    {
        var so = new SerializedObject(definition); so.FindProperty("icon").objectReferenceValue = icon; so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ materials
    public static Material GrassMaterial => AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_GrassBlade.mat");

    private static void CreateMaterials()
    {
        var blade = MakeMaterial("GrassBlade", Shader.Find("TowerOfBabel/Foliage/Buoyant Bush"));
        blade.SetColor("_BaseColor", Color.white); blade.SetFloat("_WindStrength", .12f); blade.SetFloat("_WindSpeed", 1.1f);
        blade.SetColor("_SubsurfaceColor", new Color(.7f, 1, .35f)); blade.SetColor("_TranslucencyColor", new Color(.85f, 1, .45f));
        var soil = MakeMaterial("Soil", Shader.Find("Universal Render Pipeline/Lit"));
        soil.SetColor("_BaseColor", new Color(.3f, .24f, .16f)); soil.SetFloat("_Smoothness", .2f);
        var clip = MakeMaterial("GrassClip", Shader.Find("Universal Render Pipeline/Lit"));
        clip.SetColor("_BaseColor", new Color(.42f, .78f, .22f)); clip.SetFloat("_Smoothness", .3f);
        var glow = MakeMaterial("LeafGlow", Shader.Find("Universal Render Pipeline/Lit"));
        glow.SetColor("_BaseColor", LeafGlowColor); glow.SetFloat("_Smoothness", .8f);
        glow.EnableKeyword("_EMISSION"); glow.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glow.SetColor("_EmissionColor", LeafGlowColor * .9f);
        var leafGlass = MakeMaterial("LeafGlass", Shader.Find("Universal Render Pipeline/Lit"));
        leafGlass.SetColor("_BaseColor", new Color(.55f, 1, .55f, .3f)); leafGlass.SetFloat("_Smoothness", .96f); leafGlass.SetFloat("_Metallic", .1f);
        leafGlass.SetFloat("_Surface", 1); leafGlass.SetFloat("_Blend", 0); leafGlass.SetFloat("_ZWrite", 0);
        leafGlass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); leafGlass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        leafGlass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); leafGlass.renderQueue = 3000; leafGlass.SetOverrideTag("RenderType", "Transparent");
        foreach (var m in new[] { blade, soil, clip, glow, leafGlass }) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
        AssetDatabase.SaveAssets();
    }

    private static Material MakeMaterial(string name, Shader shader)
    {
        string path = Models + "Materials/Workshop_" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        return material;
    }

    // ------------------------------------------------------------------ meshes
    private static void LoadMeshes()
    {
        meshes.Clear();
        container = AssetDatabase.LoadMainAssetAtPath(MeshPath) as Mesh;
        foreach (var mesh in AssetDatabase.LoadAllAssetsAtPath(MeshPath).OfType<Mesh>()) meshes[mesh.name] = mesh;
        Save("GrassTuft", Blades(41, 230, .78f, .26f, .62f, true));
        Save("GrassStubble", Blades(42, 150, .72f, .05f, .1f, false));
        Save("MeadowBed", Bed(43, .86f, .09f));
        Save("PlanterSprouts", Blades(44, 150, .4f, .14f, .34f, true));
        Save("FeedTuft", Blades(45, 40, .13f, .1f, .2f, false));
        Save("BlockSprouts", Blades(46, 34, .13f, .05f, .13f, true));
        Save("Clipping", Blades(47, 1, .001f, .06f, .06f, false));
        AssetDatabase.SaveAssets();
    }

    private static Mesh Get(string name) => meshes[name];

    private static void Save(string name, Mesh built)
    {
        if (!meshes.TryGetValue(name, out var mesh))
        {
            mesh = new Mesh { name = name };
            if (container == null) { AssetDatabase.CreateAsset(mesh, MeshPath); container = mesh; }
            else AssetDatabase.AddObjectToAsset(mesh, container);
            meshes[name] = mesh;
        }
        mesh.Clear();
        mesh.SetVertices(built.vertices); mesh.SetNormals(built.normals); mesh.SetColors(built.colors);
        mesh.SetTriangles(built.triangles, 0); mesh.RecalculateBounds();
        UnityEngine.Object.DestroyImmediate(built);
        EditorUtility.SetDirty(mesh);
    }

    /// <summary>
    /// A clump of curved, tapering blades, double-sided, rising from a disc. Vertex alpha is the wind weight, so tips
    /// sway in the Buoyant Bush shader. Optional small white flowers sit on some blades, like clover in a meadow.
    /// </summary>
    public static Mesh Blades(int seed, int count, float radius, float minHeight, float maxHeight, bool flowers)
    {
        var random = new System.Random(seed);
        float R(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        var v = new List<Vector3>(); var n = new List<Vector3>(); var c = new List<Color>(); var t = new List<int>();
        Color baseColor = new(.14f, .4f, .1f), midColor = new(.3f, .66f, .17f), tipColor = new(.62f, .9f, .3f);
        for (int i = 0; i < count; i++)
        {
            float angle = R(0, Mathf.PI * 2), distance = Mathf.Sqrt(R(0, 1)) * radius;
            var root = new Vector3(Mathf.Cos(angle) * distance, 0, Mathf.Sin(angle) * distance);
            // Taller in the middle so the clump reads as a mound.
            float height = Mathf.Lerp(maxHeight, minHeight, distance / Mathf.Max(radius, .001f) * .8f) * R(.75f, 1.1f);
            Vector3 lean = (new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * R(.1f, .45f) + new Vector3(R(-.2f, .2f), 0, R(-.2f, .2f)));
            float facing = R(0, Mathf.PI * 2), width = R(.018f, .032f) * Mathf.Clamp(height / .4f, .6f, 1.3f);
            Vector3 side = new(Mathf.Cos(facing), 0, Mathf.Sin(facing));
            float hue = R(-.08f, .08f);
            bool yellow = random.NextDouble() < .12;
            int first = v.Count;
            float[] heights = { 0, .4f, .75f, 1 };
            for (int k = 0; k < heights.Length; k++)
            {
                float h = heights[k];
                Vector3 p = root + Vector3.up * height * h + lean * height * h * h;
                float w = width * (1 - h * .92f);
                Vector3 normal = Vector3.Cross(side, (Vector3.up + lean * 2 * h).normalized).normalized;
                Color color = h < .5f ? Color.Lerp(baseColor, midColor, h * 2) : Color.Lerp(midColor, tipColor, (h - .5f) * 2);
                if (yellow) color = Color.Lerp(color, new Color(.82f, .86f, .32f), .5f * h);
                color.r += hue * .5f; color.g += hue; color.a = h;
                if (k == heights.Length - 1) { v.Add(p); n.Add(normal); c.Add(color); }
                else { v.Add(p - side * w); v.Add(p + side * w); n.Add(normal); n.Add(normal); c.Add(color); c.Add(color); }
            }
            // Front then back faces so blades read from every side with a single-sided shader.
            int[] front = { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5 };
            int back = v.Count;
            for (int k = first; k < back; k++) { v.Add(v[k]); n.Add(-n[k]); c.Add(c[k]); }
            for (int k = 0; k < front.Length; k += 3)
            {
                t.Add(first + front[k]); t.Add(first + front[k + 1]); t.Add(first + front[k + 2]);
                t.Add(back + front[k]); t.Add(back + front[k + 2]); t.Add(back + front[k + 1]);
            }
            if (flowers && random.NextDouble() < .05) Flower(v[back - 1], R(.025f, .04f), v, n, c, t);
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateBounds();
        return mesh;
    }

    private static void Flower(Vector3 at, float size, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
    {
        int center = v.Count;
        v.Add(at + Vector3.up * .006f); n.Add(Vector3.up); c.Add(new Color(1, .86f, .3f, 1));
        const int petals = 10;
        for (int i = 0; i < petals; i++)
        {
            float a = i * Mathf.PI * 2 / petals, r = size * (i % 2 == 0 ? 1 : .55f);
            v.Add(at + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r)); n.Add(Vector3.up); c.Add(new Color(.98f, .98f, .95f, 1));
        }
        for (int i = 0; i < petals; i++)
        {
            int a = center + 1 + i, b = center + 1 + (i + 1) % petals;
            t.Add(center); t.Add(b); t.Add(a); t.Add(center); t.Add(a); t.Add(b);
        }
    }

    /// <summary>Low, slightly lumpy mossy soil mound that stays on the ground while the grass regrows.</summary>
    public static Mesh Bed(int seed, float radius, float height)
    {
        var random = new System.Random(seed);
        const int rings = 4, sides = 28;
        var v = new List<Vector3>(); var t = new List<int>(); var c = new List<Color>();
        v.Add(Vector3.up * height); c.Add(Color.white);
        float[] lumps = Enumerable.Range(0, sides).Select(_ => (float)random.NextDouble()).ToArray();
        for (int r = 1; r <= rings; r++)
        {
            float f = r / (float)rings;
            for (int s = 0; s < sides; s++)
            {
                float a = s * Mathf.PI * 2 / sides, rr = radius * f * (1 + (lumps[s] - .5f) * .14f * f);
                v.Add(new Vector3(Mathf.Cos(a) * rr, height * (1 - f * f) * (.9f + lumps[(s + r) % sides] * .2f), Mathf.Sin(a) * rr)); c.Add(Color.white);
            }
        }
        for (int s = 0; s < sides; s++) { t.Add(0); t.Add(1 + (s + 1) % sides); t.Add(1 + s); }
        for (int r = 1; r < rings; r++)
        for (int s = 0; s < sides; s++)
        {
            int a = 1 + (r - 1) * sides + s, b = 1 + (r - 1) * sides + (s + 1) % sides, a2 = a + sides, b2 = b + sides;
            t.Add(a); t.Add(b); t.Add(a2); t.Add(b); t.Add(b2); t.Add(a2);
        }
        var mesh = new Mesh(); mesh.SetVertices(v); mesh.SetColors(c); mesh.SetTriangles(t, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        return mesh;
    }

    // ------------------------------------------------------------------ prefabs
    private static GameObject Part(string name, Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localRotation = rotation ?? Quaternion.identity;
        go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    // Built-in meshes: the cylinder is radius 1 and 2 m tall; the sphere and cube are 1 m across.
    private static Mesh Primitive(PrimitiveType type) => UnityEngine.Resources.GetBuiltinResource<Mesh>(type switch
    {
        PrimitiveType.Cylinder => "Cylinder.fbx", PrimitiveType.Sphere => "New-Sphere.fbx", PrimitiveType.Cube => "Cube.fbx", _ => "Cube.fbx"
    });

    private static GameObject BuildBlock()
    {
        var root = new GameObject("LivingGrassBlock");
        try
        {
            Part("Moss", root.transform, Primitive(PrimitiveType.Cube), Mat("Moss"), new Vector3(0, .13f, 0), new Vector3(.26f, .26f, .26f));
            Part("Shell", root.transform, Primitive(PrimitiveType.Cube), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlass.mat"),
                new Vector3(0, .14f, 0), new Vector3(.3f, .3f, .3f));
            Part("Sprouts", root.transform, Get("BlockSprouts"), GrassMaterial, new Vector3(0, .26f, 0), Vector3.one);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 2.1f + .4f;
                Part("Glow_" + i, root.transform, Primitive(PrimitiveType.Sphere), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlow.mat"),
                    new Vector3(Mathf.Cos(a) * .07f, .3f + i * .015f, Mathf.Sin(a) * .07f), Vector3.one * .03f);
            }
            return PrefabUtility.SaveAsPrefabAsset(root, BlockPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void BuildNode(ResourceDefinition grass)
    {
        var node = new GameObject("Grass");
        try
        {
            var bed = Part("MeadowBed", node.transform, Get("MeadowBed"), Mat("Moss"), Vector3.zero, Vector3.one);
            var pebbles = new GameObject("Pebbles"); pebbles.transform.SetParent(bed.transform, false);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.37f + .3f;
                Part("Pebble_" + i, pebbles.transform, Mesh("RockChip"), Mat("Chip"), new Vector3(Mathf.Cos(a) * .7f, .02f, Mathf.Sin(a) * .62f),
                    Vector3.one * (.12f + i % 3 * .04f), Quaternion.Euler(0, i * 67, 0));
            }
            var tuft = Part("GrassTuft", node.transform, Get("GrassTuft"), GrassMaterial, new Vector3(0, .05f, 0), Vector3.one);
            var stubble = Part("Stubble", node.transform, Get("GrassStubble"), GrassMaterial, new Vector3(0, .05f, 0), Vector3.one);
            stubble.SetActive(false);
            var collider = node.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .32f, 0); collider.size = new Vector3(1.55f, .64f, 1.55f);
            var resource = new SerializedObject(node.AddComponent<Resource>());
            resource.FindProperty("definition").objectReferenceValue = grass; resource.FindProperty("visuals").objectReferenceValue = tuft;
            resource.FindProperty("nodeId").ulongValue = NodeId; resource.ApplyModifiedPropertiesWithoutUndo();
            node.AddComponent<AeroInteractionMotion>();

            var group = new GameObject("Harvest feedback"); group.transform.SetParent(node.transform, false);
            var clippings = Particles(group.transform, "Grass clippings", new Vector3(0, .35f, 0), Mat("GrassClip"), Get("Clipping"),
                new Color(.45f, .8f, .22f), .5f, 1.1f, .5f, 1, 1.3f, 0, .4f);
            var pollen = Particles(group.transform, "Pollen motes", new Vector3(0, .4f, 0), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlow.mat"),
                Mesh("EssenceMote"), new Color(.8f, 1, .5f, .9f), .02f, .045f, -.1f, 1.2f, .5f, 0, .5f);
            var audio = SoundSource(group, "Gather sounds");
            var feedback = new SerializedObject(node.AddComponent<ResourceHarvestFeedback>());
            // The sand-style gather: the tuft is pulled down and narrows; the stubble stays through cooldown.
            feedback.FindProperty("sand").boolValue = true;
            feedback.FindProperty("visualRoot").objectReferenceValue = tuft.transform;
            feedback.FindProperty("cooldownMarker").objectReferenceValue = stubble;
            feedback.FindProperty("chips").objectReferenceValue = clippings; feedback.FindProperty("dust").objectReferenceValue = pollen;
            feedback.FindProperty("audioSource").objectReferenceValue = audio;
            feedback.FindProperty("strokeClip").objectReferenceValue = Clip("GrassStroke");
            feedback.FindProperty("breakClip").objectReferenceValue = Clip("GrassBurst");
            feedback.FindProperty("respawnClip").objectReferenceValue = Clip("Respawn");
            feedback.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(node, NodePrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(node); }
    }

    /// <summary>
    /// A pearl planter with an azure band, a soil bed under a clear glass dome and a mister on top. Grass drops through
    /// the dome, the mister spins and sprays, and living grass sprouts fill the dome before the block appears on the tray.
    /// </summary>
    private static void BuildPlanter(ResourceDefinition grass, ResourceDefinition living, GameObject block)
    {
        var root = new GameObject("GrassPlanter");
        try
        {
            var model = new GameObject("Visuals"); model.transform.SetParent(root.transform, false);
            var m = model.transform;
            Mesh cylinder = Primitive(PrimitiveType.Cylinder), sphere = Primitive(PrimitiveType.Sphere), cube = Primitive(PrimitiveType.Cube);
            Part("Base", m, cylinder, Mat("Pearl"), new Vector3(0, .16f, 0), new Vector3(.62f, .16f, .62f));
            Part("Band", m, cylinder, Mat("Azure"), new Vector3(0, .27f, 0), new Vector3(.64f, .035f, .64f));
            Part("SoilBed", m, cylinder, AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Soil.mat"), new Vector3(0, .34f, 0), new Vector3(.5f, .02f, .5f));
            var sprouts = Part("Sprouts", m, Get("PlanterSprouts"), GrassMaterial, new Vector3(0, .36f, 0), Vector3.one);
            Part("Dome", m, sphere, AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlass.mat"), new Vector3(0, .36f, 0), Vector3.one * 1.02f);
            Part("MisterCap", m, cylinder, Mat("Pearl"), new Vector3(0, .9f, 0), new Vector3(.09f, .03f, .09f));
            var mister = new GameObject("Mister"); mister.transform.SetParent(m, false); mister.transform.localPosition = new Vector3(0, .94f, 0);
            for (int i = 0; i < 3; i++)
            {
                var arm = new GameObject("Arm_" + i); arm.transform.SetParent(mister.transform, false); arm.transform.localRotation = Quaternion.Euler(0, i * 120, 0);
                Part("Rod", arm.transform, cube, Mat("Azure"), new Vector3(.09f, 0, 0), new Vector3(.18f, .015f, .02f));
                Part("Nozzle", arm.transform, sphere, AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlow.mat"), new Vector3(.18f, 0, 0), Vector3.one * .035f);
            }
            for (int i = 0; i < 3; i++)
            {
                float a = (-90 + (i - 1) * 32) * Mathf.Deg2Rad;
                Part("StatusLight_" + i, m, sphere, AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlow.mat"),
                    new Vector3(Mathf.Cos(a) * .63f, .17f, Mathf.Sin(a) * .63f), Vector3.one * .07f);
            }
            Part("TrayArm", m, cube, Mat("Azure"), new Vector3(0, .24f, -.68f), new Vector3(.08f, .04f, .3f));
            Part("Tray", m, cylinder, Mat("Pearl"), new Vector3(0, .27f, -.86f), new Vector3(.18f, .02f, .18f));
            var socket = new GameObject("BlockSocket"); socket.transform.SetParent(m, false); socket.transform.localPosition = new Vector3(0, .29f, -.86f);
            var feed = Part("Feed", m, Get("FeedTuft"), GrassMaterial, new Vector3(0, .98f, 0), Vector3.one);

            FitBox(root, .88f, 1.2f);
            var processor = new SerializedObject(root.AddComponent<ResourceProcessor>());
            processor.FindProperty("sourceResource").objectReferenceValue = grass;
            processor.FindProperty("targetResource").objectReferenceValue = living;
            processor.FindProperty("sourceAmount").intValue = 2; processor.FindProperty("targetAmount").intValue = 1;
            processor.FindProperty("interactionDuration").floatValue = 3.5f;
            processor.FindProperty("processorId").stringValue = ProcessorId;
            processor.ApplyModifiedPropertiesWithoutUndo();

            var output = (GameObject)PrefabUtility.InstantiatePrefab(block, socket.transform); output.name = "Output";
            output.transform.localPosition = Vector3.zero; output.transform.localRotation = Quaternion.identity;
            var anim = new SerializedObject(root.AddComponent<ProcessorMachineAnimation>());
            anim.FindProperty("process").enumValueIndex = (int)Kind.Grow;
            anim.FindProperty("output").objectReferenceValue = output.transform;
            anim.FindProperty("glowColor").colorValue = LeafGlowColor;
            ProcessorFamilyAuthoring.Refs(anim, "statusLights", Enumerable.Range(0, 3).Select(i => (UnityEngine.Object)Find(model, "StatusLight_" + i).GetComponent<Renderer>()));
            ProcessorFamilyAuthoring.Spin(anim, mister.transform, Vector3.up, 420);
            // The new grass sprouts in the dome as the cut grass is absorbed.
            ProcessorFamilyAuthoring.FillLevel(anim, sprouts.transform, Vector3.up, new Vector2(.32f, .9f));
            ProcessorFamilyAuthoring.Pulse(anim, Find(model, "Dome"), new Vector3(1, 1, 1), .012f, 6);
            // Cut grass arcs from the player onto the dome top, then sinks through it into the soil.
            ProcessorFamilyAuthoring.Feed(anim, model, new Vector3(0, -.65f, -1.35f), Vector3.zero, .45f, new Vector2(.28f, .6f), new Vector3(0, -.6f, 0), .1f);
            var hum = SoundSource(root, "Machine loop"); hum.loop = true; hum.volume = 0; hum.clip = Clip("CondenserHum");
            anim.FindProperty("loop").objectReferenceValue = hum;
            anim.FindProperty("accents").objectReferenceValue = SoundSource(root, "Machine accents");
            anim.FindProperty("completionClip").objectReferenceValue = Clip("BlockChime");
            var mist = Particles(root.transform, "Mister spray", new Vector3(0, .92f, 0), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"),
                null, new Color(.88f, .96f, 1, .4f), .05f, .12f, .15f, .6f, .5f, 14, .12f);
            var spores = Particles(root.transform, "Leaf sparkles", new Vector3(0, .45f, 0), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_LeafGlow.mat"),
                Mesh("EssenceMote"), new Color(.7f, 1, .5f, .9f), .015f, .035f, -.08f, .9f, .15f, 10, .3f);
            var glints = Particles(root.transform, "Output glints", socket.transform.localPosition + Vector3.up * .05f, Mat("Glass"), Mesh("EssenceMote"),
                new Color(.7f, 1, .7f), .02f, .05f, .2f, .8f, .9f);
            var leaves = Particles(root.transform, "Leaf burst", new Vector3(0, .8f, 0), Mat("GrassClip"), Get("Clipping"), new Color(.5f, .85f, .25f), .5f, 1, .4f, .9f, 1, 0, .3f);
            ProcessorFamilyAuthoring.Refs(anim, "workEffects", new UnityEngine.Object[] { mist, spores });
            ProcessorFamilyAuthoring.Refs(anim, "completionEffects", new UnityEngine.Object[] { glints, leaves });
            anim.ApplyModifiedPropertiesWithoutUndo();
            feed.SetActive(false); sprouts.SetActive(false); output.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root, PlanterPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}

/// <summary>Review stills of the grass node and the planter mid-growth, written to the disposable preview folder.</summary>
public static class GrassWorkshopPreview
{
    public static string Capture()
    {
        const string folder = "_Screenshots-and-previews-to-delete/Grass/";
        System.IO.Directory.CreateDirectory(folder);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject Spawn(string path, Vector3 at)
            {
                var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = at; return go;
            }
            var node = Spawn(GrassWorkshopAuthoring.NodePrefab, new Vector3(-1.1f, 0, 0));
            var planter = Spawn(GrassWorkshopAuthoring.PlanterPrefab, new Vector3(1.1f, 0, 0));
            // Show the planter mid-cycle: sprouts grown, block on the tray.
            foreach (var t in planter.GetComponentsInChildren<Transform>(true)) if (t.name == "Sprouts" || t.name == "Output") t.gameObject.SetActive(true);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.localScale = Vector3.one * 2; ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ResourceStationKit.Models + "Materials/Workshop_Pearl.mat");
            var sun = new GameObject("Sun"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sun, scene);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(40, 140, 0);
            var fillGo = new GameObject("Fill"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillGo, scene);
            var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .5f; fill.color = new Color(.75f, .88f, 1); fillGo.transform.rotation = Quaternion.Euler(25, -40, 0);
            var camGo = new GameObject("Camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
            var camera = camGo.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.62f, .8f, .97f);
            camera.fieldOfView = 35; camera.transform.position = new Vector3(0, 1.6f, -4.3f); camera.transform.LookAt(new Vector3(0, .4f, 0));
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 }; var old = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                string path = folder + "Grass-Node-And-Planter.png"; System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                return path;
            }
            finally { ShaderUtil.allowAsyncCompilation = async; camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
