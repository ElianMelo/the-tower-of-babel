using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;
using static ResourceStationKit;

/// <summary>
/// Builds the river, water and sky essence nodes and their block resources, renders their icons, and
/// assigns every processed material to its building. Processors are built by ProcessorFamilyAuthoring.
/// Models come from Tools/BuildingAero/build_essence_workshop.py. Re-running keeps placements and GUIDs.
/// </summary>
public static class EssenceWorkshopAuthoring
{
    public const string NodeFolder = "Assets/_Project/Prefabs/Resources/";
    private const string Data = "Assets/_Project/Resources/Definitions/";
    private const string Buildings = "Assets/_Project/Prefabs/Buildings/";

    public sealed class Family
    {
        public string Essence, Block, NodeModel, Container, Body, Spinner, FloaterPrefix, Base, Glass, Glow, Stroke;
        public ResourceType EssenceType, BlockType;
        public ulong NodeId;
        public EssenceHarvestFeedback.EssenceStyle Style;
        public Color GlowColor, MoteColor;
        public float NodeX;
        public bool GlowOnBase;
        public string NodePrefab => NodeFolder + Essence.Replace(" ", "") + ".prefab";
        public string BlockModel => Block.Replace(" ", "");
    }

    public static readonly Family[] Families =
    {
        new() { Essence = "River Essence", Block = "River Block", NodeModel = "RiverPool", Container = "RiverEssence", Body = "Water", Spinner = "Current",
            FloaterPrefix = "Wisp_", Base = "PoolBed", Glass = "RiverGlass", Glow = "RiverGlow", Stroke = "RiverStroke",
            EssenceType = ResourceType.RiverEssence, BlockType = ResourceType.RiverBlock, NodeId = 3001, Style = EssenceHarvestFeedback.EssenceStyle.RiverSwirl,
            GlowColor = new Color(.3f, 1, .75f), MoteColor = new Color(.45f, 1, .82f, .9f), NodeX = 5.2f },
        new() { Essence = "Water Essence", Block = "Water Block", NodeModel = "WaterBubbles", Container = "WaterEssence", Body = "Bubbles",
            FloaterPrefix = "Droplet_", Base = "MossBed", Glass = "WaterGlass", Glow = "WaterFoam", Stroke = "WaterStroke",
            EssenceType = ResourceType.WaterEssence, BlockType = ResourceType.WaterBlock, NodeId = 3002, Style = EssenceHarvestFeedback.EssenceStyle.WaterWobble,
            GlowColor = new Color(.35f, .55f, .8f), MoteColor = new Color(.6f, .85f, 1, .9f), NodeX = 11.8f },
        // Cloud Dandelions replaced the fallen cloud on 2026-11-04; the Silk Clock model on 2026-11-15. The body is the
        // first flower picked (Flowers), so it is the first part to sink.
        new() { Essence = "Sky Essence", Block = "Sky Block", NodeModel = "SkyDandelions", Container = "SkyEssence", Body = "Stalk_2",
            FloaterPrefix = "Seed_", Base = "Rosette", Glass = "SkyGlass", Glow = "SkyGlow", Stroke = "SkyStroke",
            EssenceType = ResourceType.SkyEssence, BlockType = ResourceType.SkyBlock, NodeId = 3003, Style = EssenceHarvestFeedback.EssenceStyle.SkyBloom,
            GlowColor = new Color(.9f, .93f, 1), MoteColor = new Color(.92f, .95f, 1, .9f), NodeX = 18.4f },
    };

    /// <summary>Which processed material each building consumes per stage.</summary>
    public static readonly (string Prefab, ResourceType Type)[] BuildingCosts =
    {
        ("Step_Tile", ResourceType.Glass), ("GlassWaterFloor", ResourceType.RiverBlock), ("Pillar", ResourceType.WaterBlock),
        ("Floor_Tile", ResourceType.ProcessedStone), ("Arch", ResourceType.SkyBlock),
    };

    [MenuItem("Tower of Babel/Resources/Apply essence nodes, blocks and building costs")]
    public static void Apply()
    {
        RequireCleanTowerScene();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        CreateDandelionMaterials();
        foreach (string model in Families.SelectMany(f => new[] { f.NodeModel, f.BlockModel }).Append("EssenceMote").Append("DandelionSeed")) ImportModel(model);
        foreach (string name in new[] { "RiverStroke", "WaterStroke", "SkyStroke", "EssenceBurst", "CondenserHum", "BlockChime" }) MakeSound(name);
        var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Data + "ResourceDefinitions.asset");
        foreach (var family in Families)
        {
            var essence = Definition(family.Essence, family.EssenceType, 2, 2.8f, 6);
            var block = Definition(family.Block, family.BlockType, 1, 3, 0);
            AddToCollection(collection, essence); AddToCollection(collection, block);
            BuildNode(family, essence);
            PlaceOrGround(family.NodePrefab, family.Essence.Replace(" ", ""), family.NodeX);
            SetIcon(essence, RenderIcon(family.Essence.Replace(" ", ""), Model(family.NodeModel), family.Container));
            SetIcon(block, RenderIcon(family.BlockModel, Model(family.BlockModel)));
        }
        AssignBuildingCosts();
        SaveTowerScene();
        Debug.Log("Essence nodes, blocks and building costs applied.");
    }

    public static void AssignBuildingCosts()
    {
        foreach (var (prefab, type) in BuildingCosts)
        {
            string path = Buildings + prefab + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var so = new SerializedObject(root.GetComponent<Building>());
                so.FindProperty("resourceType").intValue = (int)type;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    private static void BuildNode(Family f, ResourceDefinition essence)
    {
        var node = new GameObject(f.Essence.Replace(" ", ""));
        try
        {
            var model = Instance(f.NodeModel, node.transform, "Model");
            var container = Find(model, f.Container);
            FitBox(node, .92f, .45f);
            var so = new SerializedObject(node.AddComponent<Resource>());
            so.FindProperty("definition").objectReferenceValue = essence; so.FindProperty("visuals").objectReferenceValue = container.gameObject;
            so.FindProperty("nodeId").ulongValue = f.NodeId; so.ApplyModifiedPropertiesWithoutUndo();
            node.AddComponent<AeroInteractionMotion>();
            var group = new GameObject("Harvest feedback"); group.transform.SetParent(node.transform, false);
            var mote = Mesh("EssenceMote");
            bool bloom = f.Style == EssenceHarvestFeedback.EssenceStyle.SkyBloom;
            var motes = Particles(group.transform, "Essence motes", new Vector3(0, .35f, 0), Mat(f.Glow), mote, f.MoteColor, .03f, .07f, -.14f, 1.3f, .7f, 0, .45f);
            // Dandelions: loose parachute seeds peel off the head being picked and drift on downwind (EmitSeed).
            var burst = bloom ? SeedParticles(group.transform, "Essence burst", new Vector3(0, .8f, 0), 2.6f, 0)
                : Particles(group.transform, "Essence burst", new Vector3(0, .25f, 0), Mat(f.Glass), mote, f.MoteColor, .04f, .1f,
                    f.Style == EssenceHarvestFeedback.EssenceStyle.SkyDrift ? -.05f : .5f, 1.1f, 1.8f, 0, .5f);
            var audio = SoundSource(group, "Gather sounds");
            var feedback = new SerializedObject(node.AddComponent<EssenceHarvestFeedback>());
            feedback.FindProperty("style").enumValueIndex = (int)f.Style;
            feedback.FindProperty("body").objectReferenceValue = Find(container.gameObject, f.Body);
            feedback.FindProperty("spinner").objectReferenceValue = f.Spinner != null ? Find(container.gameObject, f.Spinner) : null;
            var floaters = container.Cast<Transform>().Where(t => t.name.StartsWith(f.FloaterPrefix)).OrderBy(t => t.name).ToArray();
            var list = feedback.FindProperty("floaters"); list.arraySize = floaters.Length;
            for (int i = 0; i < floaters.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = floaters[i];
            // Dandelions: the silk of the seed clocks glows softly (Dandelion Seeds shader emission).
            var glows = bloom ? Flowers.Select(n => Find(container.gameObject, "Seeds_" + n)).ToArray()
                : f.GlowOnBase ? new[] { Find(model, f.Base) }
                : f.Style == EssenceHarvestFeedback.EssenceStyle.WaterWobble ? new[] { Find(container.gameObject, f.Body) }
                : floaters.Append(Find(container.gameObject, f.Spinner)).ToArray();
            var glowList = feedback.FindProperty("glowRenderers"); glowList.arraySize = glows.Length;
            for (int i = 0; i < glows.Length; i++) glowList.GetArrayElementAtIndex(i).objectReferenceValue = glows[i].GetComponent<Renderer>();
            feedback.FindProperty("glowColor").colorValue = f.GlowColor;
            feedback.FindProperty("motes").objectReferenceValue = motes; feedback.FindProperty("burst").objectReferenceValue = burst;
            feedback.FindProperty("audioSource").objectReferenceValue = audio;
            feedback.FindProperty("strokeClip").objectReferenceValue = Clip(f.Stroke);
            feedback.FindProperty("breakClip").objectReferenceValue = Clip("EssenceBurst");
            feedback.FindProperty("respawnClip").objectReferenceValue = Clip("Respawn");
            if (bloom)
            {
                // Picking order: the small left flower, the right one, then the tall centre bloom as the finale.
                var flowerList = feedback.FindProperty("flowers"); var headList = feedback.FindProperty("seedHeads");
                flowerList.arraySize = headList.arraySize = Flowers.Length;
                for (int i = 0; i < Flowers.Length; i++)
                {
                    flowerList.GetArrayElementAtIndex(i).objectReferenceValue = Find(container.gameObject, Flowers[i]);
                    headList.GetArrayElementAtIndex(i).objectReferenceValue = Find(container.gameObject, "Seeds_" + Flowers[i]).GetComponent<Renderer>();
                }
                feedback.FindProperty("windDirection").vector3Value = new Vector3(1, 0, -.25f);
            }
            feedback.ApplyModifiedPropertiesWithoutUndo();
            if (bloom) BloomAmbience(node, container.gameObject, group, mote);
            PrefabUtility.SaveAsPrefabAsset(node, f.NodePrefab);
        }
        finally { Object.DestroyImmediate(node); }
    }

    /// <summary>Dandelion flowers in picking order (see EssenceHarvestFeedback.PoseFlowers).</summary>
    public static readonly string[] Flowers = { "Stalk_2", "Stalk_1", "Bloom" };

    /// <summary>
    /// Idle life for the dandelions: seeds keep drifting off downwind and sparkles rise; while gathered they thicken.
    /// The flowers' own nod and their picking are all EssenceHarvestFeedback's, so no two components turn a stalk.
    /// </summary>
    private static void BloomAmbience(GameObject node, GameObject container, GameObject group, Mesh mote)
    {
        var drift = SeedParticles(group.transform, "Drifting seeds", new Vector3(0, .95f, 0), 3.2f, 1);
        var sparkle = Particles(group.transform, "Sky sparkle", new Vector3(0, .55f, 0), Mat("SkyGlow"), mote, new Color(.8f, .9f, 1), .012f, .026f, -.03f, 2, .05f, 1, .7f);
        var ambient = new SerializedObject(node.AddComponent<AmbientMotion>());
        ProcessorFamilyAuthoring.AmbientEffects(ambient, (drift, 1.2f, 6), (sparkle, 1.5f, 4));
        ambient.FindProperty("visibility").objectReferenceValue = Find(container, "Bloom").GetComponent<Renderer>();
        ambient.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Loose parachute seeds (DandelionSeed.fbx, pappus up) drifting downwind with a slow turn.</summary>
    internal static ParticleSystem SeedParticles(Transform parent, string name, Vector3 position, float lifetime, float rate)
    {
        var ps = Particles(parent, name, position, Mat("Cloud"), Mesh("DandelionSeed"), new Color(1, 1, 1, .95f), .55f, .85f, -.02f, lifetime, .05f, rate, .3f);
        var main = ps.main; main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(-.3f, .3f); main.startRotationY = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(-.3f, .3f);
        var turn = ps.rotationOverLifetime; turn.enabled = true; turn.separateAxes = true;
        turn.x = new ParticleSystem.MinMaxCurve(-.15f, .15f); turn.y = new ParticleSystem.MinMaxCurve(.4f, 1.2f); turn.z = new ParticleSystem.MinMaxCurve(-.15f, .15f);
        var wind = ps.velocityOverLifetime; wind.enabled = true; wind.space = ParticleSystemSimulationSpace.World;
        wind.x = new ParticleSystem.MinMaxCurve(.22f, .45f); wind.y = new ParticleSystem.MinMaxCurve(0, .05f); wind.z = new ParticleSystem.MinMaxCurve(-.05f, .08f);
        var noise = ps.noise; noise.enabled = true; noise.strength = .2f; noise.frequency = .5f; noise.scrollSpeed = .25f;
        ps.GetComponent<ParticleSystemRenderer>().alignment = ParticleSystemRenderSpace.World;
        return ps;
    }

    /// <summary>
    /// The Silk Clock's own materials: the hand-painted green atlas (Tools/BuildingAero/paint_dandelion_greens.py) on
    /// leaves, stems, bud and crown; the seed clocks' Dandelion Seeds shader; and the white and aqua pebbles (natural
    /// nodes keep clear of the machine Pearl and Azure).
    /// </summary>
    public static void CreateDandelionMaterials()
    {
        const string texturePath = Models + "Textures/DandelionGreens.png";
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 4; importer.maxTextureSize = 1024;
        importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        Material Make(string name, Shader shader)
        {
            string path = Models + "Materials/Workshop_" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; mat.enableInstancing = true; return mat;
        }
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var green = Make("DandelionGreen", lit);
        green.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath)); green.SetColor("_BaseColor", Color.white);
        green.SetFloat("_Smoothness", .5f); green.SetFloat("_Cull", 0); green.doubleSidedGI = true;      // leaves are single surfaces
        var seeds = Make("DandelionSeeds", Shader.Find("TowerOfBabel/Foliage/Dandelion Seeds"));
        seeds.SetFloat("_Flight", EssenceHarvestFeedback.FlowerFlight / EssenceHarvestFeedback.FlowerWave);
        var aqua = Make("AquaPebble", lit); aqua.SetColor("_BaseColor", new Color(.55f, .86f, .95f)); aqua.SetFloat("_Smoothness", .85f);
        var white = Make("WhitePebble", lit); white.SetColor("_BaseColor", new Color(.88f, .94f, .97f)); white.SetFloat("_Smoothness", .8f);
        foreach (var m in new[] { green, seeds, aqua, white }) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
    }

    private static ResourceDefinition Definition(string name, ResourceType type, int amount, float duration, float cooldown)
    {
        string file = name.Replace(" ", "");
        string path = Data + file + ".asset"; var d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
        if (d == null) { d = ScriptableObject.CreateInstance<ResourceDefinition>(); AssetDatabase.CreateAsset(d, path); }
        var so = new SerializedObject(d); so.FindProperty("resourceId").stringValue = file.ToLowerInvariant();
        so.FindProperty("resourceType").intValue = (int)type; so.FindProperty("displayName").stringValue = name;
        so.FindProperty("amountGathered").intValue = amount; so.FindProperty("interactionDuration").floatValue = duration;
        so.FindProperty("respawnCooldown").floatValue = cooldown;
        // Grounded nodes stay put like the sand pile; their own feedback animates the essence.
        so.FindProperty("shakeStrength").floatValue = 0;
        so.ApplyModifiedPropertiesWithoutUndo(); return d;
    }

    private static void AddToCollection(ResourceDefinitionCollection collection, ResourceDefinition definition)
    {
        var so = new SerializedObject(collection); var entries = so.FindProperty("definitions");
        for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).objectReferenceValue == definition) return;
        int index = entries.arraySize++; entries.GetArrayElementAtIndex(index).objectReferenceValue = definition;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetIcon(ResourceDefinition definition, Sprite icon)
    {
        var so = new SerializedObject(definition); so.FindProperty("icon").objectReferenceValue = icon; so.ApplyModifiedPropertiesWithoutUndo();
    }
}
