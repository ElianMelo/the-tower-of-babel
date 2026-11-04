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

        [Test]
        public void Condenser_KeepsItsPartsAndTurnsInTheBreezeAtRest()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Condenser);
            foreach (string part in new[] { "Rotor", "Orb", "OrbGlow", "OrbCloud", "CloudMass", "CloudRing", "WindSpiral_0", "WindSpiral_1", "Grass", "Daisy_0" })
                Assert.That(Part(prefab, part), Is.Not.Null, part);
            var spin = new SerializedObject(prefab.GetComponent<ProcessorMachineAnimation>()).FindProperty("spins").GetArrayElementAtIndex(0);
            Assert.That(((Transform)spin.FindPropertyRelative("part").objectReferenceValue).name, Is.EqualTo("Rotor"));
            Assert.That(spin.FindPropertyRelative("idleSpeed").floatValue, Is.GreaterThan(10), "The rotor never stops dead at rest.");
            Assert.That(Part(prefab, "Grass").GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo("Workshop_GrassBlade"), "The grass sways in the wind shader.");
            var ambient = prefab.GetComponent<AmbientMotion>();
            Assert.That(ambient, Is.Not.Null); Assert.That(ambient.ChannelCount, Is.GreaterThanOrEqualTo(12));
            AssertAmbientRatesRise(ambient, 3);

            var root = Object.Instantiate(prefab);
            try
            {
                var motion = root.GetComponent<AmbientMotion>();
                Quaternion ring = Part(root, "CloudRing").localRotation, daisy = Part(root, "Daisy_0").localRotation;
                motion.SampleEditorPose(2.5f, 0);
                Assert.That(Quaternion.Angle(Part(root, "CloudRing").localRotation, ring), Is.GreaterThan(10), "The cloud ring orbits the tower at rest.");
                float early = Quaternion.Angle(Part(root, "Daisy_0").localRotation, daisy);
                motion.SampleEditorPose(3.4f, 0);
                Assert.That(Quaternion.Angle(Part(root, "Daisy_0").localRotation, daisy), Is.Not.EqualTo(early).Within(.2f), "Daisies nod.");
                motion.RestoreRestPose();
                Assert.That(Quaternion.Angle(Part(root, "CloudRing").localRotation, ring), Is.LessThan(.01f));
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
                var ring = Part(root, "CloudRing"); var ringRest = ring.localRotation;
                motion.SampleEditorPose(1.5f, 0); float idle = Quaternion.Angle(ring.localRotation, ringRest);
                motion.SampleEditorPose(1.5f, 1); float working = Quaternion.Angle(ring.localRotation, ringRest);
                Assert.That(working, Is.GreaterThan(idle * 3), "The cloud ring whirls faster while the machine works.");
                motion.SampleEditorPose(1, .6f, -1, .17f);
                Assert.That(orb.localScale.x, Is.LessThan(rest.x * .97f), "The orb squashes in anticipation as work starts.");
                motion.SampleEditorPose(1, 0, .12f);
                Assert.That(orb.localScale.x, Is.GreaterThan(rest.x * 1.03f), "The orb pops past rest when a block is made.");
                motion.SampleEditorPose(1, 0, AmbientMotion.KickDuration + .1f);
                Assert.That(orb.localScale.x, Is.EqualTo(rest.x).Within(.001f), "…and settles.");

                var anim = new SerializedObject(root.GetComponent<ProcessorMachineAnimation>());
                Assert.That(((Renderer)anim.FindProperty("heatSurfaces").GetArrayElementAtIndex(0).objectReferenceValue).name, Is.EqualTo("OrbGlow"), "The orb ring charges with progress.");
                Assert.That(anim.FindProperty("completionEffects").arraySize, Is.EqualTo(3), "Glints, a cloud puff and a gust ring.");
                Assert.That(anim.FindProperty("workEffects").arraySize, Is.EqualTo(3), "Intake, orb swirl and the essence's cloud trail.");
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
                // Swaying parts must not jump; spinning parts must not change speed abruptly.
                var watched = new[] { "Daisy_0", "Daisy_3" }.Select(n => Part(root, n)).ToArray();
                var spinning = new[] { "Rotor", "WindSpiral_0", "CloudRing", "OrbCloud" }.Select(n => Part(root, n)).ToArray();
                var orb = Part(root, "Orb");
                anim.ResetImmediately();
                float worst = 0, worstScale = 0, worstSpeedChange = 0; string worstPart = "", worstSpinner = "";
                var last = watched.Select(t => t.localRotation).ToArray(); float lastOrb = orb.localScale.x;
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
                        float step = Quaternion.Angle(watched[i].localRotation, last[i]); last[i] = watched[i].localRotation;
                        if (step > worst) { worst = step; worstPart = watched[i].name + " at " + t.ToString("0.00") + " s"; }
                    }
                    for (int i = 0; i < spinning.Length; i++)
                    {
                        float step = Quaternion.Angle(spinning[i].localRotation, lastSpin[i]); lastSpin[i] = spinning[i].localRotation;
                        float change = Mathf.Abs(step - lastStep[i]); lastStep[i] = step;
                        if (t > Frame * 1.5f && change > worstSpeedChange) { worstSpeedChange = change; worstSpinner = spinning[i].name + " at " + t.ToString("0.00") + " s"; }
                    }
                    worstScale = Mathf.Max(worstScale, Mathf.Abs(orb.localScale.x / lastOrb - 1)); lastOrb = orb.localScale.x;
                }
                Assert.That(worst, Is.LessThan(1), "Daisies sway without jerking (worst: " + worstPart + ").");
                Assert.That(worstSpeedChange, Is.LessThan(1), "Spinning parts wind up and coast down smoothly (worst: " + worstSpinner + ").");
                Assert.That(worstScale, Is.LessThan(.012f), "The orb squashes and pops without snapping.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Dandelions_GatherAndCancelWithoutShaking()
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Node));
            try
            {
                var fb = root.GetComponent<EssenceHarvestFeedback>(); var motion = root.GetComponent<AmbientMotion>();
                var bloom = Part(root, "Bloom"); var stalk = Part(root, "Stalk_1");
                fb.ResetFeedback();
                Quaternion lastBloom = bloom.localRotation, lastStalk = stalk.localRotation; float lastScale = bloom.localScale.x;
                float worstTurn = 0, worstScale = 0;
                // Six pulls over the 2.8 s gather, then a cancelled gather that has to ease back, not snap.
                for (float t = 0; t < 6; t += Frame)
                {
                    if (t >= .5f && t - Frame < .5f) fb.BeginWork();
                    if (t >= .5f && t < 3.3f) fb.SetProgress((t - .5f) / 2.8f);
                    if (t >= 3.3f && t - Frame < 3.3f) fb.StopWork();
                    fb.StepEditor(Frame); motion.StepEditor(Frame);
                    worstTurn = Mathf.Max(worstTurn, Quaternion.Angle(bloom.localRotation, lastBloom), Quaternion.Angle(stalk.localRotation, lastStalk));
                    worstScale = Mathf.Max(worstScale, Mathf.Abs(bloom.localScale.x / lastScale - 1));
                    lastBloom = bloom.localRotation; lastStalk = stalk.localRotation; lastScale = bloom.localScale.x;
                }
                Assert.That(worstTurn, Is.LessThan(2), "Pulls bow the stalks like a gust; they never shiver or snap upright.");
                Assert.That(worstScale, Is.LessThan(.07f), "A cancelled gather regrows the stalks smoothly.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Dandelions_AreALivingMeadowThatNodsAndShedsSeeds()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Node);
            var feedback = prefab.GetComponent<EssenceHarvestFeedback>();
            Assert.That(feedback.Style, Is.EqualTo(EssenceHarvestFeedback.EssenceStyle.SkyBloom));
            Assert.That(feedback.Body.name, Is.EqualTo("Bloom"));
            foreach (string stalk in new[] { "Stalk_1", "Stalk_2" }) Assert.That(Part(prefab, stalk).parent, Is.EqualTo(feedback.Body), stalk + " sinks with the bloom.");
            Assert.That(Part(prefab, "Grass").GetComponent<Renderer>().sharedMaterial.name, Is.EqualTo("Workshop_GrassBlade"));
            var ambient = prefab.GetComponent<AmbientMotion>();
            Assert.That(ambient.ChannelCount, Is.EqualTo(2));
            AssertAmbientRatesRise(ambient, 2);
            Assert.That(AmbientEffects(ambient).Any(e => e.name == "Drifting seeds"), Is.True);

            var root = Object.Instantiate(prefab);
            try
            {
                var fb = root.GetComponent<EssenceHarvestFeedback>(); var motion = root.GetComponent<AmbientMotion>();
                var bloom = Part(root, "Bloom"); var stalk = Part(root, "Stalk_1");
                Quaternion bloomRest = bloom.localRotation;
                fb.SampleEditorPose(.6f, 0, false); var a = bloom.localRotation;
                fb.SampleEditorPose(3f, 0, false);
                Assert.That(Quaternion.Angle(bloom.localRotation, a), Is.GreaterThan(.5f), "The centre stalk nods in the breeze at rest.");
                motion.SampleEditorPose(1.2f, 0); var early = stalk.localRotation;
                motion.SampleEditorPose(2.3f, 0);
                Assert.That(Quaternion.Angle(stalk.localRotation, early), Is.GreaterThan(.3f), "Side stalks sway on their own rhythm.");
                Assert.That(fb.Dormant, Is.False);
                var restScale = bloom.localScale;
                fb.SampleEditorPose(2, .9f);
                var s = bloom.localScale;
                Assert.That(s.x / restScale.x, Is.EqualTo(s.y / restScale.y).Within(.02f), "Gathered stalks shrink whole instead of being squashed.");
                Assert.That(s.x / restScale.x, Is.LessThan(.65f));
                Assert.That(Quaternion.Angle(bloom.localRotation, bloomRest), Is.GreaterThan(15), "They lean downwind as the seeds blow away.");
                fb.Deplete();
                Assert.That(fb.Dormant, Is.True, "Seeds stop drifting while the essence is on cooldown.");
                fb.Respawn(); fb.ResetFeedback();
                Assert.That(Quaternion.Angle(bloom.localRotation, bloomRest), Is.LessThan(.01f));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
