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
        new() { Name = "SkySpinner", Source = "SkyEssence", Target = "SkyBlock", SourceAmount = 2, TargetAmount = 1, Duration = 3.5f,
            ProcessorId = "tower-sky-spinner-01", Output = "SkyBlock", X = 21.6f, Process = Kind.Spin, Glow = new Color(.75f, .78f, 1), Formerly = "SkyCondenser" },
    };

    [MenuItem("Tower of Babel/Resources/Apply processor family")]
    public static void Apply()
    {
        RequireCleanTowerScene();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        foreach (string model in Machines.SelectMany(m => new[] { m.Name, m.Output }).Distinct().Append("EssenceMote")) ImportModel(model);
        foreach (string sound in new[] { "CondenserHum", "BlockChime" }) MakeSound(sound);
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

    private static void Build(Spec spec)
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
                case Kind.Spin: Spinner(root, model, anim); break;
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

    private static void Spinner(GameObject root, GameObject model, SerializedObject anim)
    {
        Spin(anim, Find(model, "Rotor"), Vector3.up, 300);
        Spin(anim, Find(model, "Fan"), Vector3.up, 720);
        FillLevel(anim, Find(model, "CloudMass"), Vector3.up, new Vector2(.15f, .9f));
        // A cloud drifts in from the side onto the hub and is spun out across the rotor.
        Feed(anim, model, new Vector3(-1.2f, -.5f, -1f), Vector3.zero, .5f, new Vector2(.3f, .65f), Vector3.zero, .25f);
        var wisps = Particles(root.transform, "Rotor wisps", new Vector3(0, 1.5f, 0), AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_Dust.mat"), null,
            new Color(.97f, .98f, 1, .55f), .1f, .25f, -.03f, .9f, .5f, 10, .75f);
        Refs(anim, "workEffects", new UnityEngine.Object[] { wisps });
    }

    // ------------------------------------------------------------------ channel helpers
    private static SerializedProperty Append(SerializedObject so, string field)
    {
        var list = so.FindProperty(field); list.arraySize++;
        return list.GetArrayElementAtIndex(list.arraySize - 1);
    }

    private static void Refs(SerializedObject so, string field, System.Collections.Generic.IEnumerable<UnityEngine.Object> values)
    {
        var items = values.ToArray(); var list = so.FindProperty(field); list.arraySize = items.Length;
        for (int i = 0; i < items.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static void Spin(SerializedObject so, Transform part, Vector3 axis, float speed)
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

    private static void Pulse(SerializedObject so, Transform part, Vector3 axis, float amount, float rate)
    {
        var e = Append(so, "pulses");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("axis").vector3Value = axis;
        e.FindPropertyRelative("amount").floatValue = amount; e.FindPropertyRelative("rate").floatValue = rate;
    }

    private static SerializedProperty FillLevel(SerializedObject so, Transform part, Vector3 axis, Vector2 window)
    {
        var e = Append(so, "fills");
        e.FindPropertyRelative("part").objectReferenceValue = part; e.FindPropertyRelative("axis").vector3Value = axis;
        e.FindPropertyRelative("window").vector2Value = window; e.FindPropertyRelative("hideWhenIdle").boolValue = true;
        e.FindPropertyRelative("drainWindow").vector2Value = Vector2.zero; e.FindPropertyRelative("drainTo").floatValue = .3f;
        return e;
    }

    private static void Feed(SerializedObject so, GameObject model, Vector3 entry, Vector3 via, float arc, Vector2 consume, Vector3 consumeOffset, float swirl)
    {
        so.FindProperty("feed").objectReferenceValue = Find(model, "Feed");
        so.FindProperty("feedEntry").vector3Value = entry; so.FindProperty("feedVia").vector3Value = via;
        so.FindProperty("feedArc").floatValue = arc; so.FindProperty("consumeWindow").vector2Value = consume;
        so.FindProperty("feedConsume").vector3Value = consumeOffset; so.FindProperty("feedSwirl").floatValue = swirl;
    }
}
