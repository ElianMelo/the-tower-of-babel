using System;
using System.Linq;
using TowerOfBabel.Resources;
using UnityEditor;
using UnityEngine;
using static ResourceStationKit;

/// <summary>
/// Iron Stone → Iron Ingot: a gatherable iron stone node and the iron smelter that melts it into ingots, the material
/// the lava lamps are built from. Also swaps the stone node onto the reworked stone model. Models come from
/// Tools/BuildingAero/build_iron_workshop.py. Re-running keeps prefab GUIDs, scene placements and definitions.
/// </summary>
public static class IronWorkshopAuthoring
{
    public const string StonePrefab = "Assets/_Project/Prefabs/Resources/Stone.prefab";
    public const string NodePrefab = "Assets/_Project/Prefabs/Resources/IronStone.prefab";
    public const string SmelterPrefab = ProcessorFamilyAuthoring.Folder + "IronSmelter.prefab";
    public const ulong NodeId = 4001;
    public const float NodeX = 31.8f;
    private const string Data = "Assets/_Project/Resources/Definitions/";
    public static readonly string[] ModelNames = { "StoneCluster", "IronStoneCluster", "IronIngot", "IronSmelter" };

    [MenuItem("Tower of Babel/Resources/Apply iron stone, smelter and ingot")]
    public static object Apply()
    {
        RequireCleanTowerScene();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        ResourceStationKit.CreateMaterials();
        CreateMaterials();
        foreach (string sound in new[] { "IronClang", "CondenserHum", "BlockChime" }) MakeSound(sound);
        foreach (string model in ModelNames) ImportModel(model);

        var stone = Definition("Iron Stone", ResourceType.IronStone, 1, 2.2f, 5, .05f);
        var ingot = Definition("Iron Ingot", ResourceType.IronIngot, 1, 3, 0, 0);
        var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Data + "ResourceDefinitions.asset");
        Add(collection, stone); Add(collection, ingot);

        ReworkStone();
        BuildNode(stone);
        ProcessorFamilyAuthoring.Build(ProcessorFamilyAuthoring.Machine("IronSmelter"));
        AssetDatabase.SaveAssets();
        var smelter = ProcessorFamilyAuthoring.Machine("IronSmelter");
        Place(NodePrefab, "IronStone", "Grass", NodeX, RowZ);
        Place(SmelterPrefab, smelter.Name, "GrassPlanter", smelter.X, smelter.Z);
        SetIcon(stone, RenderIcon("IronStone", AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefab), "Visuals"));
        SetIcon(ingot, RenderIcon("IronIngot", Model("IronIngot")));
        SaveTowerScene();
        var smelterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmelterPrefab);
        return new
        {
            node = NodePrefab, smelter = SmelterPrefab,
            stoneTriangles = Mesh("StoneCluster").triangles.Length / 3, ironTriangles = Mesh("IronStoneCluster").triangles.Length / 3,
            smelterRenderers = smelterPrefab.GetComponentsInChildren<Renderer>(true).Length
        };
    }

    // ------------------------------------------------------------------ data
    private static ResourceDefinition Definition(string name, ResourceType type, int amount, float duration, float cooldown, float weave)
    {
        string file = name.Replace(" ", ""), path = Data + file + ".asset";
        var d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
        if (d == null) { d = ScriptableObject.CreateInstance<ResourceDefinition>(); AssetDatabase.CreateAsset(d, path); }
        var so = new SerializedObject(d);
        so.FindProperty("resourceId").stringValue = file.ToLowerInvariant();
        so.FindProperty("resourceType").intValue = (int)type; so.FindProperty("displayName").stringValue = name;
        so.FindProperty("amountGathered").intValue = amount; so.FindProperty("interactionDuration").floatValue = duration;
        so.FindProperty("respawnCooldown").floatValue = cooldown;
        so.FindProperty("shakeStrength").floatValue = weave;
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

    private static bool IsStation(Transform t) => t.GetComponent<Resource>() != null || t.GetComponent<ResourceProcessor>() != null;

    /// <summary>
    /// Adds a missing scene instance in the same hierarchy folder as <paramref name="neighbour"/> (the workshop row is
    /// grouped), or moves an existing one there; either way it is regrounded, keeping its x and z.
    /// </summary>
    private static void Place(string prefabPath, string name, string neighbour, float x, float z)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(t => t.gameObject.scene == scene && IsStation(t)).ToArray();
        var group = all.FirstOrDefault(t => t.name == neighbour)?.parent;
        var copies = all.Where(t => t.name == name).ToList();
        var existing = copies.FirstOrDefault();
        foreach (var extra in copies.Skip(1)) UnityEngine.Object.DestroyImmediate(extra.gameObject);
        if (existing == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath), scene);
            go.name = name; existing = go.transform; existing.position = new Vector3(x, 0, z);
        }
        if (group != null && existing.parent != group) existing.SetParent(group, true);
        var p = existing.position; existing.position = new Vector3(p.x, GroundY(p.x, p.z), p.z);
    }

    // ------------------------------------------------------------------ materials
    public static Material IronRock => AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Resource_IronRock.mat");

    private static void CreateMaterials()
    {
        // Iron rock reuses the stone texture, tinted rust red.
        var rock = Lit(Models + "Materials/Resource_IronRock.mat");
        rock.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Models + "Textures/StoneSurface-Imagegen.png"));
        rock.SetColor("_BaseColor", new Color(.86f, .56f, .46f)); rock.SetFloat("_Smoothness", .22f); rock.SetFloat("_Metallic", 0);
        var ore = Lit(Models + "Materials/Workshop_IronOre.mat");
        ore.SetColor("_BaseColor", new Color(.2f, .25f, .32f)); ore.SetFloat("_Metallic", .85f); ore.SetFloat("_Smoothness", .72f);
        var rust = Lit(Models + "Materials/Workshop_Rust.mat");
        rust.SetColor("_BaseColor", new Color(.74f, .36f, .14f)); rust.SetFloat("_Metallic", .05f); rust.SetFloat("_Smoothness", .22f);
        var bar = Lit(Models + "Materials/Workshop_Ingot.mat");
        bar.SetColor("_BaseColor", new Color(.74f, .82f, .9f)); bar.SetFloat("_Metallic", .95f); bar.SetFloat("_Smoothness", .82f);
        var molten = Lit(Models + "Materials/Workshop_Molten.mat");
        molten.SetColor("_BaseColor", new Color(1, .56f, .14f)); molten.SetFloat("_Smoothness", .7f); molten.SetFloat("_Metallic", 0);
        molten.EnableKeyword("_EMISSION"); molten.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        molten.SetColor("_EmissionColor", new Color(1, .42f, .06f) * 2.2f);
        foreach (var m in new[] { rock, ore, rust, bar, molten }) { m.enableInstancing = true; EditorUtility.SetDirty(m); }
        AssetDatabase.SaveAssets();
    }

    private static Material Lit(string path)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        return material;
    }

    // ------------------------------------------------------------------ nodes
    private static MeshRenderer ModelRenderer(string model) => Model(model).GetComponentsInChildren<MeshRenderer>(true).Single();

    /// <summary>
    /// Puts the reworked stone model on the stone node. Its root sits 1.5 m above the ground (as in the Tower scene), so
    /// the model is lowered until its base meets the ground, as ResourceWorkshopAuthoring did for the first cluster.
    /// </summary>
    private static void ReworkStone()
    {
        var source = ModelRenderer("StoneCluster");
        var stone = PrefabUtility.LoadPrefabContents(StonePrefab);
        try
        {
            var visuals = stone.transform.Find("Visuals");
            var filter = visuals.GetComponent<MeshFilter>();
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            var p = visuals.localPosition;
            visuals.localPosition = new Vector3(p.x, .02f - stone.transform.position.y - mesh.bounds.min.y * visuals.localScale.y, p.z);
            filter.sharedMesh = mesh;
            visuals.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            FitSphere(visuals.GetComponent<SphereCollider>(), mesh);
            PrefabUtility.SaveAsPrefabAsset(stone, StonePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(stone); }
    }

    private static void FitSphere(SphereCollider sphere, Mesh mesh)
    {
        if (sphere == null) return;
        sphere.center = mesh.bounds.center;
        sphere.radius = Mathf.Max(mesh.bounds.extents.x, mesh.bounds.extents.z) * .85f;
    }

    /// <summary>
    /// The iron stone node starts as a copy of the stone node, so it gathers with the same rock feedback (chips, dust,
    /// sounds, spawner bricks during cooldown), then gets its own model, ore-coloured fragments and definition.
    /// </summary>
    private static void BuildNode(ResourceDefinition definition)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(NodePrefab) == null && !AssetDatabase.CopyAsset(StonePrefab, NodePrefab))
            throw new InvalidOperationException("Could not copy the stone node.");
        var node = PrefabUtility.LoadPrefabContents(NodePrefab);
        try
        {
            node.name = "IronStone";
            // The stone prefab's root is raised with its parts offset down; this node's root stands on the ground instead.
            float lift = node.transform.position.y;
            foreach (Transform child in node.transform) child.localPosition += Vector3.up * lift;
            node.transform.position = Vector3.zero;

            var source = ModelRenderer("IronStoneCluster");
            var visuals = node.transform.Find("Visuals");
            var mesh = source.GetComponent<MeshFilter>().sharedMesh;
            visuals.GetComponent<MeshFilter>().sharedMesh = mesh;
            visuals.GetComponent<MeshRenderer>().sharedMaterials = source.sharedMaterials;
            var p = visuals.localPosition;
            visuals.localPosition = new Vector3(p.x, .02f - mesh.bounds.min.y * visuals.localScale.y, p.z);
            FitSphere(visuals.GetComponent<SphereCollider>(), mesh);

            var resource = new SerializedObject(node.GetComponent<Resource>());
            resource.FindProperty("definition").objectReferenceValue = definition;
            resource.FindProperty("nodeId").ulongValue = NodeId;
            resource.ApplyModifiedPropertiesWithoutUndo();

            var feedback = node.transform.Find("Harvest feedback");
            foreach (var brick in feedback.GetComponentsInChildren<MeshRenderer>(true)) brick.sharedMaterial = Mat("IronOre");
            var chips = feedback.Find("3D fragments");
            if (chips != null)
            {
                chips.GetComponent<ParticleSystemRenderer>().sharedMaterial = Mat("Rust");
                var main = chips.GetComponent<ParticleSystem>().main; main.startColor = new Color(.78f, .45f, .28f);
            }
            var dust = feedback.Find("Gather dust");
            if (dust != null) { var main = dust.GetComponent<ParticleSystem>().main; main.startColor = new Color(.66f, .44f, .34f, .35f); }
            PrefabUtility.SaveAsPrefabAsset(node, NodePrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(node); }
    }
}

/// <summary>Review stills of the stone, the iron stone and the smelter mid-cycle, written to the disposable preview folder.</summary>
public static class IronWorkshopPreview
{
    public static string Capture(float progress = .8f)
    {
        const string folder = "_Screenshots-and-previews-to-delete/IronWorkshop/";
        System.IO.Directory.CreateDirectory(folder);
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        try
        {
            GameObject Spawn(string path, Vector3 at)
            {
                var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene); go.transform.position = at; return go;
            }
            // The stone prefab's root is raised 1.5 m above its base, like its scene instance.
            Spawn(IronWorkshopAuthoring.StonePrefab, new Vector3(-2.4f, 1.5f, .3f));
            Spawn(IronWorkshopAuthoring.NodePrefab, new Vector3(-.6f, 0, .3f));
            var smelter = Spawn(IronWorkshopAuthoring.SmelterPrefab, new Vector3(1.8f, 0, 0));
            smelter.transform.rotation = Quaternion.Euler(0, -20, 0);
            var anim = smelter.GetComponent<TowerOfBabel.Resources.Interaction.ProcessorMachineAnimation>();
            var processor = smelter.GetComponent<ResourceProcessor>();
            processor.BeginInteraction(null);
            for (int step = 1; step <= 20; step++) anim.SampleEditorPose(step * .1f, progress * step / 20f);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.localScale = Vector3.one * 2; ground.GetComponent<Renderer>().sharedMaterial = ResourceStationKit.Mat("Pearl");
            var sun = new GameObject("Sun"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(sun, scene);
            var light = sun.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(40, 140, 0);
            var fillGo = new GameObject("Fill"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillGo, scene);
            var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .5f; fill.color = new Color(.75f, .88f, 1); fillGo.transform.rotation = Quaternion.Euler(25, -40, 0);
            var camGo = new GameObject("Camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camGo, scene);
            var camera = camGo.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.62f, .8f, .97f);
            camera.fieldOfView = 35; camera.transform.position = new Vector3(0, 2.1f, -6.4f); camera.transform.LookAt(new Vector3(0, .8f, 0));
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 }; var old = RenderTexture.active;
            bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
                string path = folder + "Iron-Workshop-" + Mathf.RoundToInt(progress * 100) + ".png"; System.IO.File.WriteAllBytes(path, tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex);
                return path;
            }
            finally { ShaderUtil.allowAsyncCompilation = async; camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }
}
