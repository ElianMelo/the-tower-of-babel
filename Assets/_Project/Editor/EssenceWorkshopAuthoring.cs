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
        // Cloud Dandelions replaced the fallen cloud on 2026-11-04: cloud seed heads on stalks over a leaf rosette.
        new() { Essence = "Sky Essence", Block = "Sky Block", NodeModel = "SkyDandelions", Container = "SkyEssence", Body = "Bloom",
            FloaterPrefix = "Seed_", Base = "Rosette", Glass = "SkyGlass", Glow = "SkyGlow", Stroke = "SkyStroke",
            EssenceType = ResourceType.SkyEssence, BlockType = ResourceType.SkyBlock, NodeId = 3003, Style = EssenceHarvestFeedback.EssenceStyle.SkyBloom,
            GlowColor = new Color(.75f, .78f, 1), MoteColor = new Color(.92f, .95f, 1, .9f), NodeX = 18.4f },
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
        foreach (string model in Families.SelectMany(f => new[] { f.NodeModel, f.BlockModel }).Append("EssenceMote")) ImportModel(model);
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
            var motes = Particles(group.transform, "Essence motes", new Vector3(0, .35f, 0), Mat(f.Glow), mote, f.MoteColor, .03f, .07f, -.14f, 1.3f, .7f, 0, .45f);
            var burst = Particles(group.transform, "Essence burst", new Vector3(0, .25f, 0), Mat(f.Glass), mote, f.MoteColor, .04f, .1f,
                f.Style is EssenceHarvestFeedback.EssenceStyle.SkyDrift or EssenceHarvestFeedback.EssenceStyle.SkyBloom ? -.05f : .5f, 1.1f, 1.8f, 0, .5f);
            var audio = SoundSource(group, "Gather sounds");
            var feedback = new SerializedObject(node.AddComponent<EssenceHarvestFeedback>());
            feedback.FindProperty("style").enumValueIndex = (int)f.Style;
            feedback.FindProperty("body").objectReferenceValue = Find(container.gameObject, f.Body);
            feedback.FindProperty("spinner").objectReferenceValue = f.Spinner != null ? Find(container.gameObject, f.Spinner) : null;
            var floaters = container.Cast<Transform>().Where(t => t.name.StartsWith(f.FloaterPrefix)).OrderBy(t => t.name).ToArray();
            var list = feedback.FindProperty("floaters"); list.arraySize = floaters.Length;
            for (int i = 0; i < floaters.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = floaters[i];
            bool bloom = f.Style == EssenceHarvestFeedback.EssenceStyle.SkyBloom;
            // Dandelions: the glowing hearts inside the seed heads (the Receptacle part of each stalk) pulse.
            var glows = bloom ? new[] { Find(container.gameObject, "Bloom"), Find(container.gameObject, "Stalk_1"), Find(container.gameObject, "Stalk_2") }
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
            feedback.ApplyModifiedPropertiesWithoutUndo();
            if (bloom) BloomAmbience(node, container.gameObject, group, mote);
            PrefabUtility.SaveAsPrefabAsset(node, f.NodePrefab);
        }
        finally { Object.DestroyImmediate(node); }
    }

    /// <summary>
    /// Idle life for the dandelions: the side stalks nod out of step with the centre one (which the harvest feedback
    /// sways), seeds keep drifting off downwind and sparkles rise; while gathered the seeds stream away in numbers.
    /// </summary>
    private static void BloomAmbience(GameObject node, GameObject container, GameObject group, Mesh mote)
    {
        var drift = Particles(group.transform, "Drifting seeds", new Vector3(0, .95f, 0), Mat("Cloud"), mote, new Color(1, 1, 1, .95f), .02f, .04f, -.025f, 3.2f, .06f, 1, .3f);
        var wind = drift.velocityOverLifetime; wind.enabled = true; wind.space = ParticleSystemSimulationSpace.World;
        wind.x = new ParticleSystem.MinMaxCurve(.22f, .45f); wind.y = new ParticleSystem.MinMaxCurve(0, 0); wind.z = new ParticleSystem.MinMaxCurve(-.05f, .08f);
        var noise = drift.noise; noise.enabled = true; noise.strength = .25f; noise.frequency = .6f; noise.scrollSpeed = .3f;
        var sparkle = Particles(group.transform, "Sky sparkle", new Vector3(0, .55f, 0), Mat("SkyGlow"), mote, new Color(.8f, .9f, 1), .012f, .026f, -.03f, 2, .05f, 1, .7f);
        var ambient = new SerializedObject(node.AddComponent<AmbientMotion>());
        ProcessorFamilyAuthoring.AmbientChannel(ambient, Find(container, "Stalk_1"), AmbientMotion.Kind.Sway, Vector3.right, 2.5f, 4.5f, 1.05f, 0, .7f);
        ProcessorFamilyAuthoring.AmbientChannel(ambient, Find(container, "Stalk_2"), AmbientMotion.Kind.Sway, Vector3.forward, 2.8f, 5, .85f, 2, .7f);
        ProcessorFamilyAuthoring.AmbientEffects(ambient, (drift, 1.2f, 9), (sparkle, 1.5f, 4));
        ambient.FindProperty("visibility").objectReferenceValue = Find(container, "Bloom").GetComponent<Renderer>();
        ambient.ApplyModifiedPropertiesWithoutUndo();
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
