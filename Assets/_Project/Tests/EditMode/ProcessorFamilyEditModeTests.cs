using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Kind = TowerOfBabel.Resources.Interaction.ProcessorMachineAnimation.ProcessKind;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ProcessorFamilyEditModeTests
    {
        private static readonly (string Name, ResourceType Source, int SourceAmount, ResourceType Target, int TargetAmount, float Duration, string Id, Kind Process, string Output)[] Machines =
        {
            ("StoneProcessor", ResourceType.Stone, 1, ResourceType.ProcessedStone, 1, 2f, "", Kind.Crush, "Brick"),
            ("GlassFurnace", ResourceType.Sand, 2, ResourceType.Glass, 1, 3.5f, "tower-glass-kiln-01", Kind.Melt, "Slab"),
            ("RiverMill", ResourceType.RiverEssence, 2, ResourceType.RiverBlock, 1, 3.5f, "tower-river-mill-01", Kind.Mill, "Block"),
            ("WaterPress", ResourceType.WaterEssence, 2, ResourceType.WaterBlock, 1, 3.5f, "tower-water-press-01", Kind.Press, "Block"),
            ("SkySpinner", ResourceType.SkyEssence, 2, ResourceType.SkyBlock, 1, 3.5f, "tower-sky-spinner-01", Kind.Spin, "Block"),
        };
        private static int[] MachineIndices => Enumerable.Range(0, Machines.Length).ToArray();
        private static GameObject Prefab(int i) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Processor/" + Machines[i].Name + ".prefab");
        private static Transform Part(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t != root.transform && t.name == name);

        [Test]
        public void Prefabs_KeepRecipesAndShareTheFamilyBase()
        {
            for (int i = 0; i < Machines.Length; i++)
            {
                var m = Machines[i]; var prefab = Prefab(i);
                Assert.That(prefab, Is.Not.Null, m.Name);
                var processor = prefab.GetComponent<ResourceProcessor>();
                Assert.That(processor.SourceResource.ResourceType, Is.EqualTo(m.Source), m.Name); Assert.That(processor.SourceAmount, Is.EqualTo(m.SourceAmount));
                Assert.That(processor.TargetResource.ResourceType, Is.EqualTo(m.Target)); Assert.That(processor.TargetAmount, Is.EqualTo(m.TargetAmount));
                Assert.That(processor.InteractionDuration, Is.EqualTo(m.Duration));
                Assert.That(new SerializedObject(processor).FindProperty("processorId").stringValue, Is.EqualTo(m.Id));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1), m.Name);
                foreach (string part in new[] { "Base", "MachineBody", "StatusLight_0", "StatusLight_1", "StatusLight_2", "BlockSocket" })
                    Assert.That(Part(prefab, part), Is.Not.Null, m.Name + " has the family layout: " + part);
                var anim = prefab.GetComponent<ProcessorMachineAnimation>();
                Assert.That(anim.Process, Is.EqualTo(m.Process));
                Assert.That(anim.Feed.gameObject.activeSelf, Is.False, "Feed is hidden at idle.");
                Assert.That(anim.Output.gameObject.activeSelf, Is.False, "Output is hidden until confirmation.");
                Assert.That(anim.Output.GetComponentInChildren<MeshFilter>(true).sharedMesh.name, Does.Contain(m.Output));
                foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(ps.main.playOnAwake, Is.False); Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(64));
                    Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                }
                Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).All(a => !a.playOnAwake && a.spatialBlend == 1), Is.True);
            }
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Processor/RiverCondenser.prefab"), Is.Null, "Condensers were renamed.");
        }

        [Test]
        public void Bases_AreBuiltFromEachMachinesMaterialAndOutput()
        {
            var expected = new[]
            {
                new[] { "Resource_Stone", "Workshop_Steel" },        // quarry block, steel deck, chute
                new[] { "Workshop_Brick", "Workshop_Heat" },         // brick hearth with glowing vents, cooling rack
                new[] { "Workshop_RiverStone", "Workshop_RiverGlass" }, // riverbed and flume
                new[] { "Workshop_WaterGlass", "Workshop_ClearGlass" }, // hydraulic platform with a glass catch basin
                new[] { "Workshop_Cloud", "Workshop_SkyGlass" },     // cloud cushion with a hovering plate
            };
            var sets = MachineIndices.Select(i =>
            {
                var prefab = Prefab(i);
                var materials = Part(prefab, "Base").GetComponent<Renderer>().sharedMaterials.Select(m => m.name)
                    .Concat(Enumerable.Range(0, 3).SelectMany(l => Part(prefab, "StatusLight_" + l).GetComponent<Renderer>().sharedMaterials.Select(m => m.name)))
                    .Distinct().OrderBy(n => n).ToArray();
                foreach (string material in expected[i]) Assert.That(materials, Does.Contain(material), Machines[i].Name);
                return string.Join(",", materials);
            }).ToArray();
            Assert.That(sets.Distinct().Count(), Is.EqualTo(Machines.Length), "No two bases share the same materials.");
            var sockets = MachineIndices.Select(i => Part(Prefab(i), "BlockSocket").position - Prefab(i).transform.position).ToArray();
            for (int a = 0; a < sockets.Length; a++)
                for (int b = a + 1; b < sockets.Length; b++)
                    Assert.That(Vector3.Distance(sockets[a], sockets[b]), Is.GreaterThan(.02f), "Each base presents its output in its own place.");
        }

        [Test]
        public void Machines_LookAndMoveDifferently()
        {
            var anims = MachineIndices.Select(i => Prefab(i).GetComponent<ProcessorMachineAnimation>()).ToArray();
            Assert.That(anims.Select(a => a.Process).Distinct().Count(), Is.EqualTo(Machines.Length), "Each machine performs its own process.");
            var signatures = anims.Select(a => (a.SpinCount, a.TravelCount, a.PulseCount, a.FillCount, a.DialCount)).ToArray();
            Assert.That(signatures.Distinct().Count(), Is.EqualTo(Machines.Length), "Each machine uses its own mix of motions.");
            var sizes = MachineIndices.Select(i =>
            {
                var body = Part(Prefab(i), "MachineBody").GetComponent<MeshFilter>().sharedMesh.bounds.size;
                return new Vector3(body.x, Mathf.Max(body.y, body.z), Mathf.Min(body.y, body.z));
            }).ToArray();
            for (int a = 0; a < sizes.Length; a++)
                for (int b = a + 1; b < sizes.Length; b++)
                    Assert.That(Vector3.Distance(sizes[a], sizes[b]), Is.GreaterThan(.08f), $"{Machines[a].Name} and {Machines[b].Name} need different silhouettes.");
            var tallest = MachineIndices.OrderByDescending(i => sizes[i].y).First();
            Assert.That(Machines[tallest].Process, Is.EqualTo(Kind.Press), "The press is the tall machine.");
        }

        [Test]
        public void WorkCycle_RunsItsProcessAndRevealsOutputOnlyAfterConfirmation([ValueSource(nameof(MachineIndices))] int index)
        {
            var root = Object.Instantiate(Prefab(index));
            try
            {
                var processor = root.GetComponent<ResourceProcessor>(); var anim = root.GetComponent<ProcessorMachineAnimation>();
                var collider = root.GetComponent<Collider>(); var bounds = collider.bounds; var socket = anim.Output.position;
                // The input starts outside, on the player's side, and travels into the machine.
                processor.BeginInteraction(null); processor.UpdateInteraction(.01f);
                Assert.That(collider.bounds.Contains(anim.Feed.position), Is.False, "The input starts outside the machine.");
                Assert.That(anim.Feed.position.z, Is.LessThan(root.transform.position.z - .5f), "It starts on the tray (player) side.");
                processor.UpdateInteraction(anim.ConsumeWindow.x);
                Assert.That(collider.bounds.Contains(anim.Feed.position), Is.True, "It has entered the machine.");
                processor.UpdateInteraction(.2f);
                Assert.That(anim.IsWorking, Is.True); Assert.That(anim.IsFeedVisible, Is.True); Assert.That(anim.IsOutputVisible, Is.False);
                // Sweep the cycle so strikes and fills happen in order.
                for (int step = 1; step <= 40; step++) anim.SampleEditorPose(step * .073f, step / 40f * .95f);
                Assert.That(anim.IsFeedVisible, Is.False, "The input has been consumed by the end of the cycle.");
                Assert.That(anim.IsOutputVisible, Is.False);
                AssertProcess(root, Machines[index].Process, anim);
                Assert.That(collider.bounds.center, Is.EqualTo(bounds.center)); Assert.That(collider.bounds.size, Is.EqualTo(bounds.size));
                processor.CancelInteraction();
                Assert.That(anim.IsFeedVisible, Is.False); Assert.That(anim.IsOutputVisible, Is.False); Assert.That(anim.CompletionCount, Is.Zero);
                processor.BeginInteraction(null); processor.UpdateInteraction(1);
                processor.CompleteByServer(); processor.CompleteByServer();
                Assert.That(anim.IsOutputVisible, Is.True); Assert.That(anim.CompletionCount, Is.EqualTo(1));
                anim.SampleEditorPose(4, 1, .6f);
                Assert.That(anim.Output.position.y, Is.GreaterThan(socket.y), "The output lifts as it is presented.");
                Assert.That(Vector3.Dot(anim.Output.position - socket, -root.transform.forward), Is.GreaterThan(.1f), "The output slides toward the tray front.");
                anim.SampleEditorPose(5, 1, ProcessorMachineAnimation.OutputDuration + .1f);
                Assert.That(anim.IsOutputVisible, Is.False);
                anim.ResetImmediately();
                Assert.That(anim.IsFeedVisible || anim.IsOutputVisible, Is.False);
            }
            finally { Object.DestroyImmediate(root); }
        }

        private static void AssertProcess(GameObject root, Kind kind, ProcessorMachineAnimation anim)
        {
            Quaternion Rest(string part) => Part(Prefab(System.Array.FindIndex(Machines, m => m.Process == kind)), part).localRotation;
            float Turned(string part) => Quaternion.Angle(Part(root, part).localRotation, Rest(part));
            switch (kind)
            {
                case Kind.Crush:
                    Assert.That(anim.StrikeCount, Is.InRange(5, 6), "The hammer strikes repeatedly.");
                    Assert.That(Turned("Roller_0"), Is.GreaterThan(5)); Assert.That(Turned("Roller_1"), Is.GreaterThan(5));
                    break;
                case Kind.Melt:
                    Assert.That(Part(root, "MeltPool").gameObject.activeSelf, Is.True, "Molten glass glows in the porthole.");
                    Assert.That(Part(root, "Bellows").localScale, Is.Not.EqualTo(Part(Prefab(1), "Bellows").localScale), "The bellows breathe.");
                    float lid = Part(Prefab(1), "Lid").position.y - Prefab(1).transform.position.y;
                    Assert.That(Part(root, "Lid").position.y - root.transform.position.y, Is.GreaterThan(lid + .02f), "The hot lid lifts.");
                    break;
                case Kind.Mill:
                    Assert.That(Turned("WaterWheel"), Is.GreaterThan(5), "The water wheel turns.");
                    Assert.That(Turned("Gear_0"), Is.GreaterThan(5));
                    Assert.That(Part(root, "MouldFill").gameObject.activeSelf, Is.True, "The mould fills.");
                    break;
                case Kind.Press:
                    var surface = Part(root, "WaterSurface");
                    Assert.That(surface.gameObject.activeSelf, Is.True, "The water has a visible surface.");
                    float squeezed = surface.position.y;
                    anim.SampleEditorPose(1.6f, .46f);
                    Assert.That(surface.position.y, Is.GreaterThan(squeezed + .2f), "The surface rides on the water level and drops as it is pressed.");
                    anim.SampleEditorPose(2.9f, .95f);
                    float rest = Part(Prefab(3), "Piston").position.y - Prefab(3).transform.position.y;
                    Assert.That(Part(root, "Piston").position.y - root.transform.position.y, Is.LessThan(rest - .35f), "The piston presses down.");
                    Assert.That(Turned("Needle"), Is.GreaterThan(90), "The pressure gauge rises.");
                    Assert.That(Part(root, "WaterColumn").gameObject.activeSelf, Is.True);
                    break;
                case Kind.Spin:
                    Assert.That(Turned("Rotor"), Is.GreaterThan(5), "The rotor spins.");
                    Assert.That(Vector3.Angle(Part(root, "Rotor").TransformDirection(Part(Prefab(4), "Rotor").InverseTransformDirection(Vector3.up)), Vector3.up), Is.LessThan(1), "It spins about the vertical axis.");
                    Assert.That(Part(root, "CloudMass").gameObject.activeSelf, Is.True, "Cloud gathers on the spindle.");
                    break;
            }
        }

        [Test]
        public void TowerScene_PlacesEveryMachineOnTheGround()
        {
            var loaded = SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            bool opened = !(loaded.IsValid() && loaded.isLoaded);
            var scene = opened ? EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity", OpenSceneMode.Additive) : loaded;
            try
            {
                var roots = scene.GetRootGameObjects();
                foreach (var m in Machines)
                {
                    var instance = roots.Where(g => g.name == m.Name).ToArray();
                    Assert.That(instance.Length, Is.EqualTo(1), m.Name);
                    Assert.That(instance[0].GetComponent<ProcessorMachineAnimation>(), Is.Not.Null);
                    Assert.That(instance[0].transform.position.y, Is.LessThan(.4f), m.Name + " stands on the ground.");
                }
                foreach (string old in new[] { "RiverCondenser", "WaterCondenser", "SkyCondenser" })
                    Assert.That(roots.Any(g => g.name == old), Is.False, old + " was renamed.");
                foreach (string node in new[] { "RiverEssence", "WaterEssence", "SkyEssence" })
                    Assert.That(roots.Single(g => g.name == node).transform.position.y, Is.LessThan(.4f));
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
