using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>The Wind Condenser and Cloud Dandelions: idle life, anticipation, escalation and the completion pop.</summary>
    public sealed class SkyWorkshopEditModeTests
    {
        private const string Condenser = "Assets/_Project/Prefabs/Processor/WindCondenser.prefab";
        private const string Node = "Assets/_Project/Prefabs/Resources/SkyEssence.prefab";
        private static Transform Part(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t != root.transform && t.name == name);

        private static ParticleSystem[] AmbientEffects(AmbientMotion ambient)
        {
            var list = new SerializedObject(ambient).FindProperty("ambients");
            return Enumerable.Range(0, list.arraySize).Select(i => (ParticleSystem)list.GetArrayElementAtIndex(i).FindPropertyRelative("effect").objectReferenceValue).ToArray();
        }

        private static void AssertAmbientRatesRise(AmbientMotion ambient, int expected)
        {
            var list = new SerializedObject(ambient).FindProperty("ambients");
            Assert.That(list.arraySize, Is.GreaterThanOrEqualTo(expected));
            for (int i = 0; i < list.arraySize; i++)
            {
                var e = list.GetArrayElementAtIndex(i);
                var effect = (ParticleSystem)e.FindPropertyRelative("effect").objectReferenceValue;
                Assert.That(effect, Is.Not.Null);
                Assert.That(effect.main.playOnAwake, Is.False, "Ambient effects are started by AmbientMotion, not on awake.");
                Assert.That(e.FindPropertyRelative("idleRate").floatValue, Is.GreaterThan(0), effect.name + " drifts at rest.");
                Assert.That(e.FindPropertyRelative("workRate").floatValue, Is.GreaterThan(e.FindPropertyRelative("idleRate").floatValue), effect.name + " thickens while working.");
            }
        }

        // Silk Turbine (2026-11-15): the Wind Condenser rebuilt as Round 03 proposition 01, without a green base.
        private static readonly string[] TurbineParts =
            { "Base", "ChannelClouds", "MachineBody", "Rotor", "Orb", "OrbGlow", "OrbCloud", "OrbSilk", "OrbSeeds", "CloudMass", "SeedRing",
              "SilkSpiral_0", "SilkSpiral_1", "SilkSeeds_0", "SilkSeeds_1", "Feed" };
        private static readonly string[] SeedParts = { "Feed", "SeedRing", "SilkSeeds_0", "SilkSeeds_1", "OrbSeeds" };
        private const string OrbCloudShader = "TowerOfBabel/Resources/Orb Cloud";

        [Test]
        public void Condenser_StandsOnAPaintedPetalPlinthWithASkyChannel()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Condenser);
            var baseRenderer = Part(prefab, "Base").GetComponent<Renderer>();
            var materials = baseRenderer.sharedMaterials.Select(m => m.name).ToArray();
            foreach (string m in new[] { "Workshop_SilkTurbinePaint", "Workshop_SilkTurbineTop", "Workshop_SkyGlass" })
                Assert.That(materials, Does.Contain(m), "The plinth uses " + m);
            Assert.That(baseRenderer.sharedMaterials.Single(m => m.name == "Workshop_SilkTurbineTop").GetTexture("_BaseMap").name, Is.EqualTo("SilkTurbineTop"));

            // The foot swells into petal lobes: round the back half, its outer edge moves in and out with the angle.
            var mesh = Part(prefab, "Base").GetComponent<MeshFilter>().sharedMesh; var local = Part(prefab, "Base");
            var edge = new float[36];
            foreach (var v in mesh.vertices)
            {
                var p = prefab.transform.InverseTransformPoint(local.TransformPoint(v));
                float r = new Vector2(p.x, p.z).magnitude;
                if (p.z < .05f || p.y > .03f || r < .4f) continue;     // the back half of the foot rim, clear of the walkway and tray
                int bin = Mathf.Clamp((int)(Mathf.Atan2(p.z, p.x) / Mathf.PI * 36), 0, 35); edge[bin] = Mathf.Max(edge[bin], r);
            }
            var filled = edge.Where(e => e > 0).ToArray();
            Assert.That(filled.Length, Is.GreaterThan(24));
            Assert.That(filled.Max() - filled.Min(), Is.GreaterThan(.015f), "The foot is scalloped into petals.");

            // The medallion: a warm sunny centre, an azure rim band and a brushed wind swirl round the middle.
            var top = new Texture2D(2, 2);
            try
            {
                Assert.That(top.LoadImage(System.IO.File.ReadAllBytes("Assets/_Project/Models/Resources/Textures/SilkTurbineTop.png")), Is.True);
                Color At(float r, float a) => top.GetPixelBilinear(.5f + .5f * r * Mathf.Cos(a), .5f + .5f * r * Mathf.Sin(a));
                Assert.That(At(0, 0).r, Is.GreaterThan(At(0, 0).b), "A warm centre shows through the orb.");
                var rim = Enumerable.Range(0, 24).Select(i => At(.95f, i * Mathf.PI / 12)).ToArray();
                Assert.That(rim.All(c => c.b > c.r + .3f), Is.True, "An azure rim band.");
                var ring = Enumerable.Range(0, 96).Select(i => { Color c = At(.55f, i * Mathf.PI / 48); return c.b - c.r; }).ToArray();
                Assert.That(ring.Max() - ring.Min(), Is.GreaterThan(.08f), "Swirl arms cross the middle of the medallion.");
            }
            finally { Object.DestroyImmediate(top); }

            // Small clouds sit in the glass channel round the drum and drift round it at rest.
            var puffs = Part(prefab, "ChannelClouds");
            var bounds = puffs.GetComponent<Renderer>().bounds;
            Assert.That(bounds.extents.x, Is.InRange(.33f, .45f), "The clouds ride the channel, round the drum.");
            Assert.That(bounds.max.y - prefab.transform.position.y, Is.LessThan(.1f), "They stay low, in the channel.");
            var root = Object.Instantiate(prefab);
            try
            {
                var rest = Part(root, "ChannelClouds").localRotation;
                root.GetComponent<AmbientMotion>().SampleEditorPose(3, 0);
                Assert.That(Quaternion.Angle(Part(root, "ChannelClouds").localRotation, rest), Is.GreaterThan(5), "The channel clouds drift.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Condenser_OrbHoldsAFluffyPaintedCloudThatBillowsSlowly()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Condenser);
            foreach (string part in new[] { "OrbCloud", "CloudMass" })
            {
                var renderer = Part(prefab, part).GetComponent<Renderer>(); var material = renderer.sharedMaterial;
                Assert.That(material.shader.name, Is.EqualTo(OrbCloudShader), part);
                Assert.That(material.GetFloat("_Billow"), Is.InRange(.003f, .03f), part + " billows a little.");
                // The fastest of the three billow waves turns at 1.21 x the speed: it must stay under 0.6 Hz.
                Assert.That(material.GetFloat("_BillowSpeed") * 1.21f / (2 * Mathf.PI), Is.LessThan(.6f), "Calm motion.");
                // Imported mesh space is Blender's (x, y, -z) with the object turned upright, so heights are read in world space.
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh; var c = mesh.colors;
                var v = mesh.vertices.Select(p => renderer.transform.TransformPoint(p)).ToArray();
                Assert.That(c.Length, Is.EqualTo(v.Length), part + " carries painted vertex colours.");
                Assert.That(v.Length, Is.LessThan(mesh.triangles.Length / 3), part + " is smooth-shaded (shared vertices).");
                // Lobed, not a ball: the distance from its centre varies a lot.
                var centre = renderer.transform.TransformPoint(mesh.bounds.center); var radii = v.Select(p => (p - centre).magnitude).ToArray();
                Assert.That((radii.Max() - radii.Min()) / radii.Max(), Is.GreaterThan(.25f), part + " is a lobed cumulus.");
                // Painted like a Frutiger cloud: brighter and warmer on top, lilac-blue and dimmer underneath, glowing most on top.
                float minY = v.Min(p => p.y), maxY = v.Max(p => p.y), span = maxY - minY;
                var low = Enumerable.Range(0, v.Length).Where(i => v[i].y < minY + span * .2f).ToArray();
                var high = Enumerable.Range(0, v.Length).Where(i => v[i].y > maxY - span * .2f).ToArray();
                float Lum(Color k) => k.r * .3f + k.g * .59f + k.b * .11f;
                Assert.That(high.Average(i => Lum(c[i])), Is.GreaterThan(low.Average(i => Lum(c[i])) + .06f), part + ": a bright top.");
                Assert.That(low.Average(i => c[i].b - c[i].r), Is.GreaterThan(high.Average(i => c[i].b - c[i].r) + .05f), part + ": a cool underside.");
                Assert.That(high.Average(i => c[i].a), Is.GreaterThan(low.Average(i => c[i].a) + .2f), part + ": the top glows most.");
                Assert.That(v.Count(p => p.y < minY + .015f), Is.GreaterThan(v.Length / 25), part + " sits on a flat cumulus base.");
            }
            Assert.That(Part(prefab, "OrbSilk").GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo("Workshop_WindRibbon"), "Soft silk ribbons, not wire.");
            Assert.That(Part(prefab, "OrbSeeds").parent, Is.EqualTo(Part(prefab, "OrbCloud")), "Seeds sink into the cloud and turn with it.");
            // Seen through clear glass of its own (the shared SkyGlass is tinted), and kept inside it.
            var glass = Part(prefab, "Orb").GetComponent<Renderer>().sharedMaterial;
            Assert.That(glass.name, Is.EqualTo("Workshop_OrbGlass")); Assert.That(glass.GetColor("_BaseColor").a, Is.LessThan(.3f), "The orb glass is clear.");
            var orb = Part(prefab, "Orb").GetComponent<Renderer>().bounds; var orbRadius = orb.extents.x;
            foreach (string part in new[] { "OrbCloud", "CloudMass" })
            {
                var renderer = Part(prefab, part).GetComponent<Renderer>(); var t = renderer.transform;
                float worst = renderer.GetComponent<MeshFilter>().sharedMesh.vertices.Max(p => (t.TransformPoint(p) - orb.center).magnitude);
                Assert.That(worst, Is.LessThan(orbRadius - .01f), part + " stays inside the orb.");
            }
        }

        [Test]
        public void Condenser_IsAHandPaintedSilkTurbineWithoutAGreenBase()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Condenser);
            foreach (string part in TurbineParts) Assert.That(Part(prefab, part), Is.Not.Null, part);
            // The meadow hill, its grass and daisies are gone: the turbine stands on its own small painted pad.
            var names = prefab.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToArray();
            Assert.That(names.Where(n => n == "Hill" || n == "Grass" || n.StartsWith("Daisy") || n == "CloudRing" || n.StartsWith("WindSpiral")), Is.Empty);
            var materials = prefab.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).SelectMany(r => r.sharedMaterials).Select(m => m.name).Distinct().ToArray();
            foreach (string green in new[] { "Workshop_Meadow", "Workshop_GrassBlade", "Workshop_Daisy", "Workshop_Pollen" })
                Assert.That(materials, Does.Not.Contain(green), "No green base: " + green);
            Assert.That(Part(prefab, "Base").GetComponent<Renderer>().bounds.size.y, Is.LessThan(.35f), "The pad is low.");

            // Hand-painted: the pad, body and rotor sample the brushed atlas; the blades are the painted frosted glass.
            foreach (string part in new[] { "Base", "MachineBody", "Rotor" })
            {
                var paint = Part(prefab, part).GetComponent<Renderer>().sharedMaterials.SingleOrDefault(m => m.name == "Workshop_SilkTurbinePaint");
                Assert.That(paint, Is.Not.Null, part + " is painted.");
                Assert.That(paint.GetTexture("_BaseMap").name, Is.EqualTo("SilkTurbinePaint"));
                var uv = new System.Collections.Generic.List<Vector2>(); Part(prefab, part).GetComponent<MeshFilter>().sharedMesh.GetUVs(0, uv);
                Assert.That(uv.Select(u => Mathf.Round(u.y * 20)).Distinct().Count(), Is.GreaterThan(6), part + " spreads over its painted region.");
            }
            var blade = Part(prefab, "Rotor").GetComponent<Renderer>().sharedMaterials.Single(m => m.name == "Workshop_SilkTurbineBlade");
            Assert.That(blade.GetTexture("_BaseMap").name, Is.EqualTo("SilkTurbinePaint")); Assert.That(blade.renderQueue, Is.GreaterThanOrEqualTo(3000), "Frosted, slightly see-through.");
            var atlas = new Texture2D(2, 2);
            try
            {
                Assert.That(atlas.LoadImage(System.IO.File.ReadAllBytes("Assets/_Project/Models/Resources/Textures/SilkTurbinePaint.png")), Is.True);
                Color[] Region(float v0, float v1) => Enumerable.Range(0, 60).SelectMany(row => Enumerable.Range(0, 60)
                    .Select(col => atlas.GetPixel(col * atlas.width / 60, (int)((v0 + (v1 - v0) * (row + .5f) / 60) * atlas.height)))).ToArray();
                // Pearl: cool blue and lilac shading strokes beside warm cream highlights, not one flat white.
                var pearl = Region(.58f, .96f);
                Assert.That(pearl.Count(c => c.b > c.r + .06f), Is.GreaterThan(pearl.Length * .05f), "Cool strokes on the pearl.");
                Assert.That(pearl.Count(c => c.r >= c.b - .01f), Is.GreaterThan(pearl.Length * .03f), "Warm strokes on the pearl.");
                Assert.That(pearl.Select(c => (Mathf.Round(c.r * 25), Mathf.Round(c.g * 25), Mathf.Round(c.b * 25))).Distinct().Count(), Is.GreaterThan(60), "Brushwork, not a flat coat.");
                // Azure trim: deep shadow strokes and bright cyan glints, a quarter of full brightness apart.
                var azure = Region(.33f, .5f).Select(c => c.maxColorComponent).OrderBy(b => b).ToArray();
                Assert.That(azure[azure.Length * 19 / 20] - azure[azure.Length / 20], Is.GreaterThan(.25f), "Shadow and highlight strokes on the trim.");
                Assert.That(Region(.33f, .5f).Count(c => c.maxColorComponent > .85f && c.g > .6f), Is.GreaterThan(azure.Length * .01f), "Cyan glints on the trim.");
                // Blades: frost white at the root, a cyan tip.
                var root = Region(.05f, .25f).Where((c, i) => i % 60 < 20).ToArray(); var tip = Region(.05f, .25f).Where((c, i) => i % 60 >= 54).ToArray();
                Assert.That(root.Average(c => c.r), Is.GreaterThan(tip.Average(c => c.r) + .1f), "The blade fades from frost white to a cyan tip.");
            }
            finally { Object.DestroyImmediate(atlas); }

            // Silk Clock seeds: the input, the drifting ring and the seeds riding the silk spirals all use the node's seed shader.
            foreach (string part in SeedParts)
            {
                var renderer = Part(prefab, part).GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(SeedShader), part);
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh; var pivots = new System.Collections.Generic.List<Vector2>(); mesh.GetUVs(1, pivots);
                Assert.That(pivots.Count, Is.EqualTo(mesh.vertexCount), part + " carries its seeds' pivots."); Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
            }
            Assert.That(Part(prefab, "SilkSeeds_0").parent, Is.EqualTo(Part(prefab, "SilkSpiral_0")), "Seeds ride the silk spiral.");
            Assert.That(Part(prefab, "OrbSilk").parent, Is.EqualTo(Part(prefab, "OrbCloud")), "Silk knits into the orb's cloud.");
        }

        [Test]
        public void Condenser_TurnsInTheBreezeAtRest()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Condenser);
            var spin = new SerializedObject(prefab.GetComponent<ProcessorMachineAnimation>()).FindProperty("spins").GetArrayElementAtIndex(0);
            Assert.That(((Transform)spin.FindPropertyRelative("part").objectReferenceValue).name, Is.EqualTo("Rotor"));
            Assert.That(spin.FindPropertyRelative("idleSpeed").floatValue, Is.GreaterThan(10), "The rotor never stops dead at rest.");
            var ambient = prefab.GetComponent<AmbientMotion>();
            Assert.That(ambient, Is.Not.Null); Assert.That(ambient.ChannelCount, Is.GreaterThanOrEqualTo(9));
            AssertAmbientRatesRise(ambient, 3);
            var drift = AmbientEffects(ambient).Single(e => e.name == "Seed drift");
            Assert.That(drift.GetComponent<ParticleSystemRenderer>().mesh.name, Is.EqualTo("DandelionSeedMesh"), "Loose parachute seeds drift off the ring.");

            var root = Object.Instantiate(prefab);
            try
            {
                var motion = root.GetComponent<AmbientMotion>();
                Quaternion ring = Part(root, "SeedRing").localRotation, silk = Part(root, "SilkSpiral_1").localRotation;
                Vector3 breath = Part(root, "SilkSpiral_0").localScale;
                motion.SampleEditorPose(2.5f, 0);
                Assert.That(Quaternion.Angle(Part(root, "SeedRing").localRotation, ring), Is.GreaterThan(10), "The seed ring orbits the tower at rest.");
                Assert.That(Quaternion.Angle(Part(root, "SilkSpiral_1").localRotation, silk), Is.GreaterThan(10), "The silk spirals turn.");
                float early = (Part(root, "SilkSpiral_0").localScale - breath).magnitude;
                motion.SampleEditorPose(3.4f, 0);
                Assert.That((Part(root, "SilkSpiral_0").localScale - breath).magnitude, Is.Not.EqualTo(early).Within(.001f), "The silk breathes.");
                // Both spirals turn the same way (clockwise from above), so their seeds flow down the tower together.
                motion.RestoreRestPose();
                var rest = new[] { "SilkSpiral_0", "SilkSpiral_1" }.Select(n => Part(root, n).rotation).ToArray();
                motion.SampleEditorPose(.5f, 0);
                for (int i = 0; i < 2; i++)
                {
                    // In world space: the imported parents carry their own axis rotation.
                    var turned = Part(root, "SilkSpiral_" + i).rotation * Quaternion.Inverse(rest[i]);
                    Assert.That(Vector3.SignedAngle(Vector3.forward, turned * Vector3.forward, Vector3.up), Is.GreaterThan(1), "SilkSpiral_" + i + " turns clockwise.");
                }
                motion.RestoreRestPose();
                Assert.That(Quaternion.Angle(Part(root, "SeedRing").localRotation, ring), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Condenser_IsFedSilkClockSeedsThatFloatInPappusUp()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Condenser));
            try
            {
                var anim = root.GetComponent<ProcessorMachineAnimation>(); var feed = anim.Feed;
                Assert.That(feed.name, Is.EqualTo("Feed"));
                var trail = feed.GetComponentsInChildren<ParticleSystem>(true).Single(p => p.name == "Feed trail");
                Assert.That(trail.GetComponent<ParticleSystemRenderer>().mesh.name, Is.EqualTo("DandelionSeedMesh"), "Loose parachutes peel off the incoming seeds.");
                anim.ResetImmediately();
                var upright = feed.localRotation;                       // the seeds as authored: pappus up
                float worst = 0; Vector3 start = Vector3.zero;
                for (int step = 0; step <= 40; step++)
                {
                    anim.SampleEditorPose(step * .05f, step / 40f * anim.ConsumeWindow.x);
                    if (step == 0) start = feed.position;
                    worst = Mathf.Max(worst, Quaternion.Angle(feed.localRotation, upright));
                }
                Assert.That(worst, Is.LessThan(1), "The seeds stay pappus-up on their way in instead of tumbling.");
                Assert.That(feed.position.y, Is.GreaterThan(start.y + .8f), "They rise into the rotor's wake.");
                var hub = Part(root, "Rotor").position;
                Assert.That(Vector3.Distance(feed.position, hub), Is.LessThan(.5f), "They arrive at the hub.");
                anim.SampleEditorPose(3, Mathf.Lerp(anim.ConsumeWindow.x, anim.ConsumeWindow.y, .95f));
                Assert.That(Vector3.Distance(feed.position, Part(root, "Orb").position), Is.LessThan(.3f), "They are drawn down the tower into the orb.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Condenser_WorkQuickensTheWindAnticipatesAndPopsOnCompletion()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Condenser));
            try
            {
                var motion = root.GetComponent<AmbientMotion>(); var orb = Part(root, "Orb"); var rest = orb.localScale;
                var ring = Part(root, "SeedRing"); var ringRest = ring.localRotation;
                motion.SampleEditorPose(1.5f, 0); float idle = Quaternion.Angle(ring.localRotation, ringRest);
                motion.SampleEditorPose(1.5f, 1); float working = Quaternion.Angle(ring.localRotation, ringRest);
                Assert.That(working, Is.GreaterThan(idle * 3), "The seed ring whirls faster while the machine works.");
                motion.SampleEditorPose(1, .6f, -1, .17f);
                Assert.That(orb.localScale.x, Is.LessThan(rest.x * .97f), "The orb squashes in anticipation as work starts.");
                motion.SampleEditorPose(1, 0, .12f);
                Assert.That(orb.localScale.x, Is.GreaterThan(rest.x * 1.03f), "The orb pops past rest when a block is made.");
                motion.SampleEditorPose(1, 0, AmbientMotion.KickDuration + .1f);
                Assert.That(orb.localScale.x, Is.EqualTo(rest.x).Within(.001f), "…and settles.");

                var anim = new SerializedObject(root.GetComponent<ProcessorMachineAnimation>());
                Assert.That(((Renderer)anim.FindProperty("heatSurfaces").GetArrayElementAtIndex(0).objectReferenceValue).name, Is.EqualTo("OrbGlow"), "The orb ring charges with progress.");
                Assert.That(anim.FindProperty("completionEffects").arraySize, Is.EqualTo(3), "Glints, a cloud puff and a ring of seeds.");
                Assert.That(anim.FindProperty("workEffects").arraySize, Is.EqualTo(3), "Silk intake, orb swirl and the seeds' trail.");
                var gust = (ParticleSystem)anim.FindProperty("completionEffects").GetArrayElementAtIndex(2).objectReferenceValue;
                Assert.That(gust.GetComponent<ParticleSystemRenderer>().mesh.name, Is.EqualTo("DandelionSeedMesh"), "The completion gust blows seeds out.");
                Assert.That(((AudioClip)anim.FindProperty("completionClip").objectReferenceValue).name, Is.EqualTo("SkyChime"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        private const float Frame = 1 / 60f;

        [Test]
        public void Condenser_MovesSmoothlyFrameToFrameThroughAWholeCycle()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Condenser));
            try
            {
                var anim = root.GetComponent<ProcessorMachineAnimation>(); var motion = root.GetComponent<AmbientMotion>();
                // The breathing silk must not jump; spinning parts must not change speed abruptly.
                var watched = new[] { "SilkSpiral_0", "SilkSpiral_1" }.Select(n => Part(root, n)).ToArray();
                var spinning = new[] { "Rotor", "SilkSpiral_0", "SeedRing", "OrbCloud", "ChannelClouds" }.Select(n => Part(root, n)).ToArray();
                var orb = Part(root, "Orb");
                anim.ResetImmediately();
                float worst = 0, worstScale = 0, worstSpeedChange = 0; string worstPart = "", worstSpinner = "";
                var last = watched.Select(t => t.localScale).ToArray(); float lastOrb = orb.localScale.x;
                var lastSpin = spinning.Select(t => t.localRotation).ToArray(); var lastStep = new float[spinning.Length];
                // Idle, then a 3.5 s recipe, completion, and the coast back to idle: activity ramps up and down.
                for (float t = 0; t < 8; t += Frame)
                {
                    if (t >= 1 && t - Frame < 1) anim.BeginWork();
                    if (t >= 1 && t < 4.5f) anim.SetProgress((t - 1) / 3.5f);
                    if (t >= 4.5f && t - Frame < 4.5f) { anim.SetProgress(1); anim.ConfirmCompletion(); }
                    anim.StepEditor(Frame); motion.StepEditor(Frame);
                    for (int i = 0; i < watched.Length; i++)
                    {
                        float step = (watched[i].localScale - last[i]).magnitude / last[i].magnitude; last[i] = watched[i].localScale;
                        // The first frame is the component's first pose, applied before anything is drawn, not motion.
                        if (t > Frame * 1.5f && step > worst) { worst = step; worstPart = watched[i].name + " at " + t.ToString("0.00") + " s"; }
                    }
                    for (int i = 0; i < spinning.Length; i++)
                    {
                        float step = Quaternion.Angle(spinning[i].localRotation, lastSpin[i]); lastSpin[i] = spinning[i].localRotation;
                        float change = Mathf.Abs(step - lastStep[i]); lastStep[i] = step;
                        if (t > Frame * 1.5f && change > worstSpeedChange) { worstSpeedChange = change; worstSpinner = spinning[i].name + " at " + t.ToString("0.00") + " s"; }
                    }
                    worstScale = Mathf.Max(worstScale, Mathf.Abs(orb.localScale.x / lastOrb - 1)); lastOrb = orb.localScale.x;
                }
                // A smooth breath of 3.5 % changes the scale by well under 1 % a frame; a phase jump would move it by up to 7 %.
                Assert.That(worst, Is.LessThan(.01f), "The silk breathes without jerking (worst: " + worstPart + ").");
                Assert.That(worstSpeedChange, Is.LessThan(1), "Spinning parts wind up and coast down smoothly (worst: " + worstSpinner + ").");
                Assert.That(worstScale, Is.LessThan(.012f), "The orb squashes and pops without snapping.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        // Cloud Dandelions, Silk Clock model (2026-11-15): flowers in picking order, each with its own seed clock.
        private static readonly string[] PickOrder = { "Stalk_2", "Stalk_1", "Bloom" };
        private const string SeedShader = "TowerOfBabel/Foliage/Dandelion Seeds";

        private static float Release(GameObject root, string flower)
        {
            var block = new MaterialPropertyBlock(); Part(root, "Seeds_" + flower).GetComponent<Renderer>().GetPropertyBlock(block);
            return block.GetFloat("_Release");
        }

        [Test]
        public void Dandelions_AreSilkClocksOnPaintedLeavesAndMixedGrass()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Node);
            var feedback = prefab.GetComponent<EssenceHarvestFeedback>();
            Assert.That(feedback.Style, Is.EqualTo(EssenceHarvestFeedback.EssenceStyle.SkyBloom));
            var list = new SerializedObject(feedback).FindProperty("flowers");
            Assert.That(Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue.name), Is.EqualTo(PickOrder));
            Assert.That(feedback.Body.name, Is.EqualTo(PickOrder[0]), "The body is the first flower picked, so it sinks first.");
            var container = Part(prefab, "SkyEssence");
            foreach (string flower in PickOrder)
            {
                var stalk = Part(prefab, flower);
                Assert.That(stalk.parent, Is.EqualTo(container), flower + " is its own stalk, so it can be picked on its own.");
                Assert.That(stalk.GetComponent<Renderer>().sharedMaterials.Select(m => m.name), Does.Contain("Workshop_DandelionGreen"), flower + " has a painted stem.");
                var seeds = Part(prefab, "Seeds_" + flower); Assert.That(seeds.parent, Is.EqualTo(stalk));
                var renderer = seeds.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterials.Length, Is.EqualTo(1)); Assert.That(renderer.sharedMaterial.shader.name, Is.EqualTo(SeedShader));
                // Every seed carries its umbrella centre: Blender (x, y, z) in UV1.xy + UV2.x, Unity object space (x, y, -z).
                var mesh = seeds.GetComponent<MeshFilter>().sharedMesh; var v = mesh.vertices;
                var uv0 = new System.Collections.Generic.List<Vector2>(); var uv1 = new System.Collections.Generic.List<Vector2>(); var uv2 = new System.Collections.Generic.List<Vector2>();
                mesh.GetUVs(0, uv0); mesh.GetUVs(1, uv1); mesh.GetUVs(2, uv2);
                Assert.That(uv1.Count, Is.EqualTo(v.Length)); Assert.That(uv2.Count, Is.EqualTo(v.Length)); Assert.That(mesh.colors.Length, Is.EqualTo(v.Length));
                int roots = 0; float farthest = 0;
                for (int i = 0; i < v.Length; i++)
                {
                    var pivot = new Vector3(uv1[i].x, uv1[i].y, -uv2[i].x);
                    farthest = Mathf.Max(farthest, (v[i] - pivot).magnitude);
                    if (uv0[i].x == 0 && (v[i] - pivot).magnitude < .02f) roots++;
                }
                Assert.That(roots, Is.GreaterThanOrEqualTo(80 * 10 * 3), flower + ": every bristle starts at its seed's pivot.");
                Assert.That(farthest, Is.LessThan(.25f), flower + ": no vertex is far from its own seed's pivot.");
            }
            // Hand-painted greens: the leaf region of the atlas mixes yellow-green, green and teal-green strokes.
            var green = Part(prefab, "Rosette").GetComponent<Renderer>().sharedMaterials.Single(m => m.name == "Workshop_DandelionGreen");
            Assert.That(green.GetTexture("_BaseMap").name, Is.EqualTo("DandelionGreens"));
            var atlas = new Texture2D(2, 2);
            try
            {
                Assert.That(atlas.LoadImage(System.IO.File.ReadAllBytes("Assets/_Project/Models/Resources/Textures/DandelionGreens.png")), Is.True);
                int yellow = 0, mid = 0, teal = 0, samples = 0;
                for (int y = (int)(atlas.height * .35f); y < atlas.height * .98f; y += 7)
                    for (int x = (int)(atlas.width * .15f); x < atlas.width; x += 7)
                    {
                        Color.RGBToHSV(atlas.GetPixel(x, y), out float h, out _, out _); samples++;
                        if (h < .25f) yellow++; else if (h < .37f) mid++; else teal++;
                    }
                foreach (var (band, count) in new[] { ("yellow-green", yellow), ("green", mid), ("teal-green", teal) })
                    Assert.That(count, Is.GreaterThan(samples * .03f), "The painted leaves include " + band + " strokes.");
            }
            finally { Object.DestroyImmediate(atlas); }
            // Grass in three sizes, every blade its own shade.
            var grass = Part(prefab, "Grass"); Assert.That(grass.GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo("Workshop_GrassBlade"));
            var blades = grass.GetComponent<MeshFilter>().sharedMesh; var gv = blades.vertices; var gc = blades.colors;
            var guv = new System.Collections.Generic.List<Vector2>(); blades.GetUVs(0, guv);
            var tips = Enumerable.Range(0, gv.Length).Where(i => guv[i].y > .99f).ToArray();
            var heights = tips.Select(i => -gv[i].z).ToArray();
            Assert.That(heights.Count(h => h < .1f), Is.GreaterThan(tips.Length * .2f), "Short blades.");
            Assert.That(heights.Count(h => h >= .1f && h < .2f), Is.GreaterThan(tips.Length * .15f), "Medium blades.");
            Assert.That(heights.Count(h => h >= .2f), Is.GreaterThan(tips.Length * .05f), "Tall blades.");
            Assert.That(tips.Select(i => (Mathf.Round(gc[i].r * 10), Mathf.Round(gc[i].g * 10), Mathf.Round(gc[i].b * 10))).Distinct().Count(), Is.GreaterThanOrEqualTo(8),
                "Blade tips come in many shades.");
            // Only the harvest feedback turns the stalks; ambient motion keeps the drifting seeds and sparkles.
            var ambient = prefab.GetComponent<AmbientMotion>();
            Assert.That(ambient.ChannelCount, Is.Zero, "No second component writes the flowers' rotation.");
            AssertAmbientRatesRise(ambient, 2);
            var drift = AmbientEffects(ambient).Single(e => e.name == "Drifting seeds");
            Assert.That(drift.GetComponent<ParticleSystemRenderer>().mesh.name, Is.EqualTo("DandelionSeedMesh"), "Loose seeds are parachute seeds.");
        }

        [Test]
        public void Dandelions_PickTheFlowersOneAfterAnother()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Node));
            try
            {
                var fb = root.GetComponent<EssenceHarvestFeedback>(); fb.ResetFeedback();
                var stalks = PickOrder.Select(n => Part(root, n)).ToArray(); var heads = PickOrder.Select(n => Part(root, "Seeds_" + n)).ToArray();
                var rest = stalks.Select(s => s.localScale.x).ToArray(); var restHeads = heads.Select(h => h.position).ToArray();
                var rotations = stalks.Select(s => s.localRotation).ToArray();
                Vector3 wind = root.transform.TransformDirection(new Vector3(1, 0, -.25f)).normalized;
                float flown = 1 + EssenceHarvestFeedback.FlowerFlight / EssenceHarvestFeedback.FlowerWave;

                fb.SampleEditorPose(2, .12f);
                Assert.That(Release(root, PickOrder[0]), Is.InRange(.1f, 1f), "The first flower's seeds are flying.");
                Assert.That(Release(root, PickOrder[1]), Is.LessThanOrEqualTo(0)); Assert.That(Release(root, PickOrder[2]), Is.LessThanOrEqualTo(0), "The others wait their turn.");
                Assert.That(Vector3.Dot(heads[0].position - restHeads[0], wind), Is.GreaterThan(.03f), "The picked flower bows into the breeze.");
                Assert.That(stalks[0].localScale.x, Is.EqualTo(rest[0]).Within(.001f), "It keeps its stalk while its seeds fly.");

                fb.SampleEditorPose(2, .36f);
                Assert.That(Release(root, PickOrder[0]), Is.GreaterThanOrEqualTo(1), "Every seed of the first flower has left.");
                Assert.That(Release(root, PickOrder[1]), Is.InRange(.1f, 1f), "Then the second flower's seeds fly.");
                Assert.That(Release(root, PickOrder[2]), Is.LessThanOrEqualTo(0));

                fb.SampleEditorPose(2, .62f);
                Assert.That(stalks[0].localScale.x, Is.LessThan(rest[0] * .15f), "The bare first stalk has sunk: it is picked.");
                Assert.That(Release(root, PickOrder[2]), Is.GreaterThan(0), "The tall centre flower is last.");

                fb.SampleEditorPose(2, 1);
                for (int i = 0; i < PickOrder.Length; i++)
                {
                    Assert.That(stalks[i].localScale.x, Is.LessThan(rest[i] * .1f), PickOrder[i] + " is picked by the end of the gather.");
                    Assert.That(Release(root, PickOrder[i]), Is.GreaterThanOrEqualTo(flown), PickOrder[i] + "'s seeds have all flown away.");
                }
                Assert.That(fb.PresentationScale, Is.LessThan(.05f));

                fb.SampleEditorPose(2, 0, false);
                for (int i = 0; i < PickOrder.Length; i++)
                {
                    Assert.That(Release(root, PickOrder[i]), Is.LessThanOrEqualTo(0)); Assert.That(stalks[i].localScale.x, Is.EqualTo(rest[i]).Within(.001f));
                }
                fb.SampleEditorPose(.6f, 0, false); var a = stalks.Select(s => s.localRotation).ToArray();
                fb.SampleEditorPose(3, 0, false);
                for (int i = 0; i < PickOrder.Length; i++)
                    Assert.That(Quaternion.Angle(stalks[i].localRotation, a[i]), Is.GreaterThan(.5f), PickOrder[i] + " nods in the breeze at rest.");
                fb.Deplete(); Assert.That(fb.Dormant, Is.True, "Seeds stop drifting while the essence is on cooldown.");
                fb.Respawn(); fb.ResetFeedback();
                for (int i = 0; i < PickOrder.Length; i++) Assert.That(Quaternion.Angle(stalks[i].localRotation, rotations[i]), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Dandelions_MoveSmoothlyThroughGatherHoldCooldownRespawnAndCancel()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Node));
            try
            {
                var fb = root.GetComponent<EssenceHarvestFeedback>(); var motion = root.GetComponent<AmbientMotion>();
                var container = Part(root, "SkyEssence").gameObject;
                var stalks = PickOrder.Select(n => Part(root, n)).ToArray(); var rest = stalks.Select(s => s.localScale.x).ToArray();
                var floaters = Enumerable.Range(0, 5).Select(i => Part(root, "Seed_" + i)).ToArray();
                fb.ResetFeedback();
                var lastRot = stalks.Select(s => s.localRotation).ToArray(); var lastScale = stalks.Select(s => s.localScale.x).ToArray();
                var lastRelease = PickOrder.Select(n => Release(root, n)).ToArray(); var lastFloat = floaters.Select(f => f.localPosition).ToArray();
                float worstTurn = 0, worstScale = 0, worstRelease = 0, worstFloat = 0; string where = "", whereFloat = "";
                bool rewoundBeforeCooldown = false, wasVisible = true;
                float flown = 1 + EssenceHarvestFeedback.FlowerFlight / EssenceHarvestFeedback.FlowerWave;
                // A 2.8 s gather, its completion held for a network round trip until cooldown hides the essence, the respawn,
                // and then a gather cancelled half-way that has to ease back.
                for (float t = 0; t < 10.5f; t += Frame)
                {
                    bool At(float s) => t >= s && t - Frame < s;
                    if (At(.5f)) fb.BeginWork();
                    if (t >= .5f && t < 3.3f) fb.SetProgress((t - .5f) / 2.8f);
                    if (At(3.3f)) { fb.SetProgress(1); fb.StopWork(); }
                    if (At(3.45f)) { fb.Deplete(); container.SetActive(false); }
                    if (At(4.5f)) { container.SetActive(true); fb.Respawn(); }
                    if (At(6)) fb.BeginWork();
                    if (t >= 6 && t < 7.4f) fb.SetProgress((t - 6) / 2.8f);
                    if (At(7.4f)) fb.StopWork();
                    fb.StepEditor(Frame); motion.StepEditor(Frame);
                    // Measured only while the essence was on screen in both frames (cooldown hides it, respawn shows it).
                    bool visible = container.activeSelf && wasVisible; wasVisible = container.activeSelf;
                    for (int i = 0; i < stalks.Length; i++)
                    {
                        float turn = Quaternion.Angle(stalks[i].localRotation, lastRot[i]), scale = Mathf.Abs(stalks[i].localScale.x - lastScale[i]) / rest[i];
                        // Release values below 0 (all seeds home) or past the last landing (all gone) look the same.
                        float release = Release(root, PickOrder[i]), step = Mathf.Abs(Mathf.Clamp(release, 0, flown) - Mathf.Clamp(lastRelease[i], 0, flown));
                        if (t > 3.3f && t < 3.45f && (release < lastRelease[i] - 1e-4f || stalks[i].localScale.x > lastScale[i] + 1e-4f)) rewoundBeforeCooldown = true;
                        if (visible && turn > worstTurn) { worstTurn = turn; where = PickOrder[i] + " turned at " + t.ToString("0.00") + " s"; }
                        if (visible) { worstScale = Mathf.Max(worstScale, scale); worstRelease = Mathf.Max(worstRelease, step); }
                        lastRot[i] = stalks[i].localRotation; lastScale[i] = stalks[i].localScale.x; lastRelease[i] = release;
                    }
                    for (int i = 0; i < floaters.Length; i++)
                    {
                        float glide = (floaters[i].localPosition - lastFloat[i]).magnitude;
                        if (visible && glide > worstFloat) { worstFloat = glide; whereFloat = floaters[i].name + " at " + t.ToString("0.00") + " s"; }
                        lastFloat[i] = floaters[i].localPosition;
                    }
                }
                Assert.That(worstTurn, Is.LessThan(.8f), "Flowers bow, sink and nod without jerking (worst: " + where + ").");
                Assert.That(worstScale, Is.LessThan(.07f), "Stalks sink, regrow and rise after a cancel smoothly.");
                Assert.That(worstRelease, Is.LessThan(.08f), "Seeds leave, and drift back after a cancel, a little each frame.");
                Assert.That(worstFloat, Is.LessThan(.05f), "Loose seeds glide away and back (worst: " + whereFloat + ").");
                Assert.That(rewoundBeforeCooldown, Is.False, "A finished gather holds its picked pose until the essence hides.");
                Assert.That(Release(root, PickOrder[0]), Is.LessThan(.05f), "The cancelled gather has eased back.");
                Assert.That(stalks[0].localScale.x, Is.EqualTo(rest[0]).Within(.03f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
