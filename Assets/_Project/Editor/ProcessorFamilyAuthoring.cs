using System;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;
using static ResourceStationKit;
using Kind = TowerOfBabel.Resources.Interaction.ProcessorMachineAnimation.ProcessKind;

/// <summary>
/// Builds the five processors from Tools/BuildingAero/build_processor_family.py. Each has a base made for its
/// material and output, and they share a family layout
/// (three status lights and an output point) and each runs its own
/// process through ProcessorMachineAnimation channels. Prefab GUIDs, scene instances and recipes are kept;
/// the former condenser prefabs are moved to their new names.
/// </summary>
public static class ProcessorFamilyAuthoring
{
    public const string Folder = "Assets/_Project/Prefabs/Processor/";
    private const string Data = "Assets/_Project/Resources/Definitions/";

    public sealed class Spec
    {
        public string Name, Source, Target, ProcessorId, Output, Formerly;
        public int SourceAmount, TargetAmount;
        public float Duration, X, Z = RowZ;
        public Kind Process;
        public Color Glow;
        public string Path => Folder + Name + ".prefab";
    }

    public static readonly Spec[] Machines =
    {
        new() { Name = "StoneProcessor", Source = "Stone", Target = "ProcessedStone", SourceAmount = 1, TargetAmount = 1, Duration = 2,
            // No fixed ID: it keeps the original scene-path ID, so test copies of the prefab never collide with it.
            ProcessorId = "", Output = "StoneBrick", X = -9, Z = -99.7f, Process = Kind.Crush, Glow = new Color(.35f, .9f, 1) },
        new() { Name = "GlassFurnace", Source = "Sand", Target = "Glass", SourceAmount = 2, TargetAmount = 1, Duration = 3.5f,
            ProcessorId = "tower-glass-kiln-01", Output = "GlassSlab", X = 1.2f, Process = Kind.Melt, Glow = new Color(1, .5f, .1f) },
        new() { Name = "RiverMill", Source = "RiverEssence", Target = "RiverBlock", SourceAmount = 2, TargetAmount = 1, Duration = 3.5f,
            ProcessorId = "tower-river-mill-01", Output = "RiverBlock", X = 8.4f, Process = Kind.Mill, Glow = new Color(.3f, 1, .75f), Formerly = "RiverCondenser" },
        new() { Name = "WaterPress", Source = "WaterEssence", Target = "WaterBlock", SourceAmount = 2, TargetAmount = 1, Duration = 3.5f,
            ProcessorId = "tower-water-press-01", Output = "WaterBlock", X = 15, Process = Kind.Press, Glow = new Color(.45f, .75f, 1), Formerly = "WaterCondenser" },
        // The Wind Condenser replaced the Sky spinner on 2026-11-04 (prefab moved, so its GUID and processor ID carry over).
        new() { Name = "WindCondenser", Source = "SkyEssence", Target = "SkyBlock", SourceAmount = 2, TargetAmount = 1, Duration = 3.5f,
            ProcessorId = "tower-sky-spinner-01", Output = "SkyBlock", X = 21.6f, Process = Kind.Spin, Glow = new Color(.5f, 1, .4f), Formerly = "SkySpinner" },
        // Built after IronWorkshopAuthoring has created the iron definitions and materials.
        new() { Name = "IronSmelter", Source = "IronStone", Target = "IronIngot", SourceAmount = 2, TargetAmount = 1, Duration = 4,
            ProcessorId = "tower-iron-smelter-01", Output = "IronIngot", X = 35.2f, Process = Kind.Smelt, Glow = new Color(1, .55f, .15f) },
    };

    public static Spec Machine(string name) => Machines.Single(m => m.Name == name);

    [MenuItem("Tower of Babel/Resources/Apply processor family")]
    public static void Apply()
    {
        RequireCleanTowerScene();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        CreateSilkTurbineMaterials();
        // The Silk Turbine is fed the Silk Clock's seeds: their material and particle mesh come from the essence tools.
        if (Mat("DandelionSeeds") == null) EssenceWorkshopAuthoring.CreateDandelionMaterials();
        foreach (string model in Machines.SelectMany(m => new[] { m.Name, m.Output }).Distinct().Append("EssenceMote").Append("DandelionSeed")) ImportModel(model);
        foreach (string sound in new[] { "CondenserHum", "BlockChime", "IronClang", "SkyChime" }) MakeSound(sound);
        foreach (var spec in Machines)
        {
            if (spec.Formerly != null)
            {
                string old = Folder + spec.Formerly + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(old) != null && AssetDatabase.LoadAssetAtPath<GameObject>(spec.Path) == null)
                {
                    string error = AssetDatabase.MoveAsset(old, spec.Path);
                    if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
                }
                // Rename the old scene instance, or remove it when the renamed one already exists.
                for (var stale = SceneRoot(spec.Formerly); stale != null; stale = SceneRoot(spec.Formerly))
                {
                    if (SceneRoot(spec.Name) == null) stale.name = spec.Name;
                    else UnityEngine.Object.DestroyImmediate(stale.gameObject);
                }
            }
            Build(spec);
            PlaceOrGround(spec.Path, spec.Name, spec.X, spec.Z);
        }
        SaveTowerScene();
        Debug.Log("Processor family applied: " + string.Join(", ", Machines.Select(m => m.Name)));
    }

    internal static void Build(Spec spec)
    {
        var root = new GameObject(spec.Name);
        try
        {
            var model = Instance(spec.Name, root.transform, "Visuals");
            FitBox(root, .88f, 1.2f);
            var processor = new SerializedObject(root.AddComponent<ResourceProcessor>());
            processor.FindProperty("sourceResource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(Data + spec.Source + ".asset");
            processor.FindProperty("targetResource").objectReferenceValue = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(Data + spec.Target + ".asset");
            processor.FindProperty("sourceAmount").intValue = spec.SourceAmount; processor.FindProperty("targetAmount").intValue = spec.TargetAmount;
            processor.FindProperty("interactionDuration").floatValue = spec.Duration;
            processor.FindProperty("processorId").stringValue = spec.ProcessorId;
            processor.ApplyModifiedPropertiesWithoutUndo();

            var output = Instance(spec.Output, Find(model, "BlockSocket"), "Output");
            output.transform.localPosition = Vector3.zero; output.transform.localRotation = Quaternion.identity;
            var anim = new SerializedObject(root.AddComponent<ProcessorMachineAnimation>());
            anim.FindProperty("process").enumValueIndex = (int)spec.Process;
            anim.FindProperty("output").objectReferenceValue = output.transform;
            anim.FindProperty("glowColor").colorValue = spec.Glow;
            Refs(anim, "statusLights", Enumerable.Range(0, 3).Select(i => (UnityEngine.Object)Find(model, "StatusLight_" + i).GetComponent<Renderer>()));
            var hum = SoundSource(root, "Machine loop"); hum.loop = true; hum.volume = 0;
            anim.FindProperty("loop").objectReferenceValue = hum;
            anim.FindProperty("accents").objectReferenceValue = SoundSource(root, "Machine accents");
            anim.FindProperty("completionClip").objectReferenceValue = Clip("BlockChime");
            var glints = Particles(root.transform, "Output glints", Find(model, "BlockSocket").position - root.transform.position + Vector3.up * .05f, Mat("Glass"), Mesh("EssenceMote"), new Color(.7f, .95f, 1), .02f, .05f, .2f, .8f, .9f);
            Refs(anim, "completionEffects", new UnityEngine.Object[] { glints });
            hum.clip = Clip("CondenserHum");
            switch (spec.Process)
            {
                case Kind.Crush: Crusher(root, model, anim); break;
                case Kind.Melt: Kiln(root, model, anim, hum); break;
                case Kind.Mill: Mill(root, model, anim); break;
                case Kind.Press: Press(root, model, anim, glints); break;
                case Kind.Spin: WindCondenser(root, model, anim, glints); break;
                case Kind.Smelt: Smelter(root, model, anim, hum); break;
            }
            anim.ApplyModifiedPropertiesWithoutUndo();
            foreach (var hidden in new[] { "Feed" }.Select(n => Find(model, n))) hidden.gameObject.SetActive(false);
            output.SetActive(false);
            for (int i = 0; i < anim.FindProperty("fills").arraySize; i++)
            {
                var fill = anim.FindProperty("fills").GetArrayElementAtIndex(i);
                if (!fill.FindPropertyRelative("hideWhenIdle").boolValue) continue;
                ((Transform)fill.FindPropertyRelative("part").objectReferenceValue).gameObject.SetActive(false);
                var surface = (Transform)fill.FindPropertyRelative("surface").objectReferenceValue;
                if (surface != null) surface.gameObject.SetActive(false);
            }
            PrefabUtility.SaveAsPrefabAsset(root, spec.Path);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    // ------------------------------------------------------------------ machines
    private static void Crusher(GameObject root, GameObject model, SerializedObject anim)
    {
        Spin(anim, Find(model, "Roller_0"), Vector3.right, 260);
        Spin(anim, Find(model, "Roller_1"), Vector3.right, -260);
        Travel(anim, Find(model, "Hammer"), Vector3.down, .2f, new Vector2(.06f, .94f), 6, .003f);
        // A stone chunk is tossed up into the hopper, then ground down between the rollers.
        Feed(anim, model, new Vector3(0, -.5f, -1.3f), Vector3.zero, .6f, new Vector2(.3f, .88f), new Vector3(0, -.7f, 0), 0);
        var grit = Particles(root.transform, "Crushing grit", new Vector3(0, 1.84f, 0), Mat("Chip"), Mesh("RockChip"), new Color(.65f, .7f, .73f), .025f, .06f, .8f, .8f, 1.6f);
        var dust = Particles(root.transform, "Crusher dust", new Vector3(0, .62f, -.2f), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"),
            null, new Color(.78f, .8f, .82f, .35f), .12f, .3f, -.02f, .9f, .3f, 6, .25f);
        anim.FindProperty("strikeEffect").objectReferenceValue = grit;
        anim.FindProperty("strikeClip").objectReferenceValue = Clip("RockStroke");
        Refs(anim, "workEffects", new UnityEngine.Object[] { dust });
    }

    private static void Kiln(GameObject root, GameObject model, SerializedObject anim, AudioSource loop)
    {
        Pulse(anim, Find(model, "Bellows"), Vector3.right, .22f, 8);
        Travel(anim, Find(model, "Lid"), Vector3.up, .05f, new Vector2(.12f, .95f), 0, .004f);
        FillLevel(anim, Find(model, "MeltPool"), Vector3.up, new Vector2(.18f, .9f));
        // A heap of sand lands on the hot lid and sinks in as it melts.
        Feed(anim, model, new Vector3(0, -.6f, -1.3f), Vector3.zero, .5f, new Vector2(.28f, .86f), new Vector3(0, -.12f, 0), 0);
        Refs(anim, "heatSurfaces", new UnityEngine.Object[] { Find(model, "HeatGlow").GetComponent<Renderer>() });
        anim.FindProperty("heatColor").colorValue = new Color(1, .3f, .04f);
        var chimney = new Vector3(.32f, 2.42f, .3f);
        var embers = Particles(root.transform, "Kiln embers", chimney, Mat("Heat"), Mesh("RockChip"), new Color(1, .48f, .05f), .02f, .05f, -.05f, .7f, .4f, 6);
        var vapour = Particles(root.transform, "Heat vapour", chimney, AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"), null,
            new Color(.84f, .92f, 1, .22f), .12f, .3f, -.06f, 1, .3f, 4);
        Refs(anim, "workEffects", new UnityEngine.Object[] { embers, vapour });
        loop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Effects + "KilnMotor.wav") ?? loop.clip;
        anim.FindProperty("completionClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(Effects + "GlassChime.wav") ?? Clip("BlockChime");
    }

    private static void Mill(GameObject root, GameObject model, SerializedObject anim)
    {
        Spin(anim, Find(model, "WaterWheel"), Vector3.right, 150);
        Spin(anim, Find(model, "Gear_0"), Vector3.right, 120);
        Spin(anim, Find(model, "Gear_1"), Vector3.right, -175);
        FillLevel(anim, Find(model, "MouldFill"), Vector3.up, new Vector2(.15f, .88f));
        // River essence drops into the funnel, then flows along the sluice to the wheel.
        Feed(anim, model, new Vector3(.3f, -.7f, -1.4f), Vector3.zero, .5f, new Vector2(.25f, .6f), new Vector3(1.05f, -.3f, 0), 0);
        var splash = Particles(root.transform, "Wheel splash", new Vector3(.72f, .6f, 0), Mat("RiverGlass"), Mesh("EssenceMote"), new Color(.45f, 1, .85f, .9f), .02f, .05f, .6f, .7f, .9f, 14);
        var flow = Particles(root.transform, "Sluice sparkle", new Vector3(.22f, 1.7f, .15f), Mat("RiverGlow"), Mesh("EssenceMote"), new Color(.5f, 1, .85f), .015f, .035f, -.05f, .6f, .2f, 10, .3f);
        Refs(anim, "workEffects", new UnityEngine.Object[] { splash, flow });
    }

    private static void Press(GameObject root, GameObject model, SerializedObject anim, ParticleSystem glints)
    {
        Travel(anim, Find(model, "Piston"), Vector3.down, .5f, new Vector2(.45f, .95f), 0, .005f);
        // Water fills the cylinder as the essence melts in, then the piston squeezes it.
        var column = FillLevel(anim, Find(model, "WaterColumn"), Vector3.up, new Vector2(.28f, .45f));
        var surface = Find(model, "WaterSurface");
        column.FindPropertyRelative("surface").objectReferenceValue = surface;
        column.FindPropertyRelative("surfaceHeight").floatValue = .66f;
        Pulse(anim, surface, new Vector3(1, 0, 1), .05f, 9);
        column.FindPropertyRelative("drainWindow").vector2Value = new Vector2(.5f, .95f);
        column.FindPropertyRelative("drainTo").floatValue = .3f;
        var dial = Append(anim, "dials");
        dial.FindPropertyRelative("part").objectReferenceValue = Find(model, "Needle");
        dial.FindPropertyRelative("axis").vector3Value = Vector3.forward;
        dial.FindPropertyRelative("angle").floatValue = -220;
        // Water essence arcs into the front intake funnel and drops into the cylinder.
        Feed(anim, model, new Vector3(0, .1f, -1.4f), new Vector3(0, .3f, -.36f), .35f, new Vector2(.3f, .45f), new Vector3(0, -.3f, 0), 0);
        var bubbles = Particles(root.transform, "Chamber bubbles", new Vector3(0, .8f, 0), Mat("WaterFoam"), Mesh("EssenceMote"), new Color(.9f, .97f, 1, .8f), .015f, .04f, -.25f, .9f, .15f, 12, .2f);
        var steam = Particles(root.transform, "Press steam", new Vector3(0, 1.42f, 0), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"), null,
            new Color(.9f, .95f, 1, .4f), .15f, .35f, -.08f, 1.1f, .6f, 0, .25f);
        Refs(anim, "workEffects", new UnityEngine.Object[] { bubbles });
        Refs(anim, "completionEffects", new UnityEngine.Object[] { glints, steam });
    }

    /// <summary>
    /// The Silk Turbine's materials: the hand-painted atlas (Tools/BuildingAero/paint_silk_turbine.py) on every pearl,
    /// azure and blade surface, the painted wind-swirl medallion on the plinth and in the tray's dish, and the Orb Cloud
    /// shader on the fluffy clouds inside the glass orb. The atlas repeats in u, because each part's strokes wrap around it.
    /// </summary>
    public static void CreateSilkTurbineMaterials()
    {
        Texture2D Import(string file, TextureWrapMode wrap)
        {
            string texturePath = Models + "Textures/" + file;
            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.textureType = TextureImporterType.Default; importer.sRGBTexture = true; importer.mipmapEnabled = true;
            importer.wrapMode = wrap; importer.anisoLevel = 4; importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        }
        var texture = Import("SilkTurbinePaint.png", TextureWrapMode.Repeat);
        var top = Import("SilkTurbineTop.png", TextureWrapMode.Clamp);
        Material Make(string name, Shader shader)
        {
            string path = Models + "Materials/Workshop_" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader; mat.enableInstancing = true; return mat;
        }
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        var paint = Make("SilkTurbinePaint", lit); paint.SetTexture("_BaseMap", texture); paint.SetColor("_BaseColor", Color.white);
        paint.SetFloat("_Smoothness", .62f); paint.SetFloat("_Metallic", 0);
        // Blades stay frosted: the painted strokes on a slightly see-through glass.
        var blade = Make("SilkTurbineBlade", lit); blade.SetTexture("_BaseMap", texture); blade.SetColor("_BaseColor", new Color(1, 1, 1, .82f));
        blade.SetFloat("_Smoothness", .88f); blade.SetFloat("_Metallic", 0);
        blade.SetFloat("_Surface", 1); blade.SetFloat("_Blend", 0); blade.SetFloat("_ZWrite", 0);
        blade.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); blade.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        blade.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); blade.renderQueue = 3000; blade.SetOverrideTag("RenderType", "Transparent");
        var medallion = Make("SilkTurbineTop", lit); medallion.SetTexture("_BaseMap", top); medallion.SetColor("_BaseColor", Color.white);
        medallion.SetFloat("_Smoothness", .66f); medallion.SetFloat("_Metallic", 0);
        var cloud = Make("OrbCloud", Shader.Find("TowerOfBabel/Resources/Orb Cloud"));
        cloud.SetColor("_GlowColor", new Color(1, .97f, .93f)); cloud.SetFloat("_Glow", .7f);
        cloud.SetColor("_RimColor", new Color(.92f, .98f, 1)); cloud.SetFloat("_Rim", 1); cloud.SetFloat("_Wrap", .45f);
        cloud.SetFloat("_Billow", .01f); cloud.SetFloat("_BillowSpeed", .75f); cloud.SetFloat("_BillowScale", 16);
        // The orb's own glass: much clearer than the shared, blue-tinted SkyGlass, so the painted cloud inside reads.
        var orb = Make("OrbGlass", lit); orb.SetTexture("_BaseMap", null); orb.SetColor("_BaseColor", new Color(.84f, .94f, 1, .2f));
        orb.SetFloat("_Smoothness", .97f); orb.SetFloat("_Metallic", .1f);
        orb.SetFloat("_Surface", 1); orb.SetFloat("_Blend", 0); orb.SetFloat("_ZWrite", 0);
        orb.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha); orb.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        orb.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); orb.renderQueue = 3000; orb.SetOverrideTag("RenderType", "Transparent");
        foreach (var m in new[] { paint, blade, medallion, cloud, orb }) EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Wind Condenser (sky essence), rebuilt on 2026-11-15 as the Silk Turbine (Round 03 proposition 01): a real
    /// three-blade turbine over the glass cloud orb, with no meadow base, hand-painted from Textures/SilkTurbinePaint.png.
    /// It stands on a petal-scalloped painted plinth with a glass sky channel (small clouds drift round it) and a
    /// painted wind-swirl medallion; a fluffy painted cumulus billows inside the orb (Orb Cloud shader, clear OrbGlass).
    /// Its input is the Silk Clock's own seeds: two puffs drift up from the player's side
    /// into the rotor's wake, pappus-up, and are drawn down the tower into the orb, where their silk knits into the
    /// cloud. At rest the rotor turns in the breeze, a loose ring of seeds orbits the tower, two silk spirals carry
    /// seeds down to the orb and the orb's cloud drifts (AmbientMotion). Working, the rotor winds up, the orb fills
    /// with cloud and its glow ring charges. Completion whirls the rotor, pops the orb and sends a ring of seeds out.
    /// </summary>
    private static void WindCondenser(GameObject root, GameObject model, SerializedObject anim, ParticleSystem glints)
    {
        Spin(anim, Find(model, "Rotor"), Vector3.forward, 320);
        anim.FindProperty("spins").GetArrayElementAtIndex(0).FindPropertyRelative("idleSpeed").floatValue = 30;
        FillLevel(anim, Find(model, "CloudMass"), Vector3.up, new Vector2(.32f, .92f));
        // The seeds rise from in front of the player into the rotor's wake, then are drawn down the tower into the orb.
        // They stay pappus-up all the way: seeds float, they do not tumble like a thrown stone.
        Vector3 rest = Find(model, "Feed").position - root.transform.position, orbCentre = Find(model, "Orb").position - root.transform.position;
        Feed(anim, model, new Vector3(0, .35f, -1.45f) - rest, Vector3.zero, .7f, new Vector2(.3f, .7f), orbCentre + Vector3.up * .15f - rest, .06f);
        anim.FindProperty("feedTumble").floatValue = 0;
        Refs(anim, "heatSurfaces", new UnityEngine.Object[] { Find(model, "OrbGlow").GetComponent<Renderer>() });
        anim.FindProperty("heatColor").colorValue = new Color(.45f, .85f, 1);
        anim.FindProperty("completionClip").objectReferenceValue = Clip("SkyChime");
        var dust = AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat");
        var mote = Mesh("EssenceMote");
        var hub = Find(model, "Rotor").position - root.transform.position;
        var orb = Find(model, "Orb").position - root.transform.position;

        // Work: silk is drawn in around the hub and motes swirl inside the orb.
        var intake = Particles(root.transform, "Silk intake", hub, dust, null, new Color(.97f, .99f, 1, .55f), .06f, .14f, 0, .9f, .05f, 14, .75f);
        var v = intake.velocityOverLifetime; v.enabled = true; v.space = ParticleSystemSimulationSpace.Local; v.radial = -1.1f;
        var swirl = Particles(root.transform, "Orb swirl", orb, Mat("SkyGlow"), mote, new Color(.75f, .9f, 1), .015f, .035f, 0, .8f, .05f, 12, .3f);
        var o = swirl.velocityOverLifetime; o.enabled = true; o.space = ParticleSystemSimulationSpace.Local; o.orbitalZ = 3.2f; o.radial = -.15f;
        // A few loose parachutes peel off the incoming seeds, so the player can follow them into the machine.
        var trail = EssenceWorkshopAuthoring.SeedParticles(Find(model, "Feed"), "Feed trail", Vector3.zero, .9f, 12);
        Refs(anim, "workEffects", new UnityEngine.Object[] { intake, swirl, trail });

        // Completion: a puff of cloud from the orb and a ring of seeds blown out from the pad, with the output glints.
        var puff = Particles(root.transform, "Cloud burst", orb, dust, null, new Color(1, 1, 1, .75f), .14f, .3f, -.04f, 1, 1.1f, 0, .35f);
        var ring = EssenceWorkshopAuthoring.SeedParticles(root.transform, "Seed gust", new Vector3(0, .3f, 0), 1.2f, 0);
        var gust = ring.main; gust.loop = false; gust.startSpeed = new ParticleSystem.MinMaxCurve(.7f, 1.3f);
        var circle = ring.shape; circle.shapeType = ParticleSystemShapeType.Circle; circle.radius = .35f; circle.radiusThickness = 0;
        Refs(anim, "completionEffects", new UnityEngine.Object[] { glints, puff, ring });

        // Ambient: motes and loose seeds drift at rest and thicken while it works; wind streaks blow through the rotor.
        var motes = Particles(root.transform, "Sky motes", orb + Vector3.up * .2f, Mat("SkyGlow"), mote, new Color(.8f, .92f, 1), .014f, .03f, -.02f, 2.2f, .08f, 3, 1);
        var drift = EssenceWorkshopAuthoring.SeedParticles(root.transform, "Seed drift", Find(model, "SeedRing").position - root.transform.position, 3, 1);
        var streaks = Particles(root.transform, "Wind streaks", hub + new Vector3(0, -.1f, -1.1f), dust, null, new Color(.95f, .98f, 1, .32f), .03f, .06f, 0, .8f, 2.2f, 1);
        streaks.transform.localRotation = Quaternion.identity;          // the cone points along +Z: front to back, through the rotor
        var cone = streaks.shape; cone.angle = 8; cone.radius = .55f;
        var stretch = streaks.GetComponent<ParticleSystemRenderer>(); stretch.renderMode = ParticleSystemRenderMode.Stretch; stretch.lengthScale = 5; stretch.velocityScale = .05f;

        var ambient = new SerializedObject(root.AddComponent<AmbientMotion>());
        AmbientChannel(ambient, Find(model, "SeedRing"), AmbientMotion.Kind.Spin, Vector3.up, 8, 42, 0, 0, .4f);
        // Both silk spirals turn clockwise seen from above, so their seeds appear to flow down the tower into the orb.
        AmbientChannel(ambient, Find(model, "SilkSpiral_0"), AmbientMotion.Kind.Spin, Vector3.up, 18, 95, 0, 0, .5f);
        AmbientChannel(ambient, Find(model, "SilkSpiral_1"), AmbientMotion.Kind.Spin, Vector3.up, 15, 80, 0, 0, .5f);
        AmbientChannel(ambient, Find(model, "SilkSpiral_0"), AmbientMotion.Kind.Breathe, Vector3.up, .02f, .035f, 1.1f, 0, .3f);
        AmbientChannel(ambient, Find(model, "SilkSpiral_1"), AmbientMotion.Kind.Breathe, Vector3.up, .02f, .035f, .9f, 1.4f, .3f);
        AmbientChannel(ambient, Find(model, "OrbCloud"), AmbientMotion.Kind.Spin, Vector3.up, 6, 32, 0, 0, .2f);
        AmbientChannel(ambient, Find(model, "OrbCloud"), AmbientMotion.Kind.Bob, Vector3.up, .01f, .018f, .8f, 0, 0);
        // Small clouds drift round the plinth's glass sky channel, the same way as the silk.
        AmbientChannel(ambient, Find(model, "ChannelClouds"), AmbientMotion.Kind.Spin, Vector3.up, 5, 24, 0, 0, .3f);
        AmbientChannel(ambient, Find(model, "Orb"), AmbientMotion.Kind.Punch, Vector3.up, .04f, .08f, 0, 0, 0);
        AmbientEffects(ambient, (motes, 3, 12), (drift, 1, 4), (streaks, .8f, 10));
        ambient.FindProperty("visibility").objectReferenceValue = Find(model, "MachineBody").GetComponent<Renderer>();
        ambient.ApplyModifiedPropertiesWithoutUndo();
    }

    internal static void AmbientChannel(SerializedObject so, Transform part, AmbientMotion.Kind kind, Vector3 axis, float idle, float work, float rate, float phase, float gust)
    {
        var e = Append(so, "channels");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("kind").enumValueIndex = (int)kind;
        e.FindPropertyRelative("axis").vector3Value = axis; e.FindPropertyRelative("idle").floatValue = idle; e.FindPropertyRelative("work").floatValue = work;
        e.FindPropertyRelative("rate").floatValue = rate; e.FindPropertyRelative("phase").floatValue = phase; e.FindPropertyRelative("gust").floatValue = gust;
    }

    internal static void AmbientEffects(SerializedObject so, params (ParticleSystem Effect, float Idle, float Work)[] effects)
    {
        foreach (var (effect, idle, work) in effects)
        {
            var e = Append(so, "ambients");
            e.FindPropertyRelative("effect").objectReferenceValue = effect;
            e.FindPropertyRelative("idleRate").floatValue = idle; e.FindPropertyRelative("workRate").floatValue = work;
            var emission = effect.emission; emission.rateOverTime = 0;      // AmbientMotion sets the rate at run time
        }
    }

    private static void Smelter(GameObject root, GameObject model, SerializedObject anim, AudioSource loop)
    {
        Spin(anim, Find(model, "Fan"), Vector3.right, 520);
        // Molten iron rises in the sight glass as the ore melts, then runs down the runner into the mould.
        var melt = FillLevel(anim, Find(model, "MeltColumn"), Vector3.up, new Vector2(.25f, .68f));
        melt.FindPropertyRelative("drainWindow").vector2Value = new Vector2(.7f, .86f);
        melt.FindPropertyRelative("drainTo").floatValue = .25f;
        FillLevel(anim, Find(model, "MouldFill"), Vector3.up, new Vector2(.7f, .86f));
        // Once the mould is full, the stamp comes down on the cast with one ringing strike.
        Travel(anim, Find(model, "Stamp"), Vector3.down, .3f, new Vector2(.86f, .97f), 1, .004f);
        var dial = Append(anim, "dials");
        dial.FindPropertyRelative("part").objectReferenceValue = Find(model, "Needle");
        dial.FindPropertyRelative("axis").vector3Value = Vector3.forward;
        dial.FindPropertyRelative("angle").floatValue = -200;
        // An ore chunk is lobbed up into the hopper on the dome and sinks into the furnace.
        Feed(anim, model, new Vector3(0, -1.1f, -1.4f), Vector3.zero, .6f, new Vector2(.28f, .6f), new Vector3(0, -.55f, 0), 0);
        Refs(anim, "heatSurfaces", new UnityEngine.Object[] { Find(model, "HeatCoil").GetComponent<Renderer>() });
        anim.FindProperty("heatColor").colorValue = new Color(1, .35f, .05f);
        var sparks = Particles(root.transform, "Sight glass sparks", new Vector3(0, 1.1f, -.62f), Mat("Molten"), Mesh("RockChip"), new Color(1, .6f, .15f), .012f, .03f, .5f, .6f, .7f, 10);
        var smoke = Particles(root.transform, "Chimney smoke", new Vector3(-.28f, 2.64f, .3f), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"), null,
            new Color(.82f, .86f, .9f, .3f), .14f, .32f, -.06f, 1.2f, .3f, 5);
        var strike = Particles(root.transform, "Stamp sparks", new Vector3(0, .46f, -1f), Mat("Molten"), Mesh("RockChip"), new Color(1, .7f, .25f), .015f, .035f, .9f, .5f, 1.6f, 0, .12f);
        anim.FindProperty("strikeEffect").objectReferenceValue = strike;
        anim.FindProperty("strikeClip").objectReferenceValue = Clip("IronClang");
        Refs(anim, "workEffects", new UnityEngine.Object[] { sparks, smoke });
        loop.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Effects + "KilnMotor.wav") ?? loop.clip;
    }

    // ------------------------------------------------------------------ channel helpers
    internal static SerializedProperty Append(SerializedObject so, string field)
    {
        var list = so.FindProperty(field); list.arraySize++;
        return list.GetArrayElementAtIndex(list.arraySize - 1);
    }

    internal static void Refs(SerializedObject so, string field, System.Collections.Generic.IEnumerable<UnityEngine.Object> values)
    {
        var items = values.ToArray(); var list = so.FindProperty(field); list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    internal static void Spin(SerializedObject so, Transform part, Vector3 axis, float speed)
    {
        var e = Append(so, "spins");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("axis").vector3Value = axis;
        e.FindPropertyRelative("speed").floatValue = speed; e.FindPropertyRelative("rampWithProgress").boolValue = true;
    }

    private static void Travel(SerializedObject so, Transform part, Vector3 direction, float distance, Vector2 window, int strokes, float shake)
    {
        var e = Append(so, "travels");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("direction").vector3Value = direction;
        e.FindPropertyRelative("distance").floatValue = distance; e.FindPropertyRelative("window").vector2Value = window;
        e.FindPropertyRelative("strokes").intValue = strokes; e.FindPropertyRelative("shake").floatValue = shake;
    }

    internal static void Pulse(SerializedObject so, Transform part, Vector3 axis, float amount, float rate)
    {
        var e = Append(so, "pulses");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("axis").vector3Value = axis;
        e.FindPropertyRelative("amount").floatValue = amount; e.FindPropertyRelative("rate").floatValue = rate;
    }

    internal static SerializedProperty FillLevel(SerializedObject so, Transform part, Vector3 axis, Vector2 window)
    {
        var e = Append(so, "fills");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("axis").vector3Value = axis;
        e.FindPropertyRelative("window").vector2Value = window; e.FindPropertyRelative("hideWhenIdle").boolValue = true;
        e.FindPropertyRelative("drainWindow").vector2Value = Vector2.zero; e.FindPropertyRelative("drainTo").floatValue = .3f;
        return e;
    }

    internal static void Feed(SerializedObject so, GameObject model, Vector3 entry, Vector3 via, float arc, Vector2 consume, Vector3 consumeOffset, float swirl)
    {
        so.FindProperty("feed").objectReferenceValue = Find(model, "Feed");
        so.FindProperty("feedEntry").vector3Value = entry; so.FindProperty("feedVia").vector3Value = via;
        so.FindProperty("feedArc").floatValue = arc; so.FindProperty("consumeWindow").vector2Value = consume;
        so.FindProperty("feedConsume").vector3Value = consumeOffset; so.FindProperty("feedSwirl").floatValue = swirl;
    }
}
