using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Iron stone is gathered from its own node, smelted into iron ingots, and the stone node wears the reworked model.
    /// The lava lamps that the ingots build are covered by TowerLavaLampEditModeTests.
    /// </summary>
    public sealed class IronWorkshopEditModeTests
    {
        private const string Definitions = "Assets/_Project/Resources/Definitions/ResourceDefinitions.asset";
        private const string StonePath = "Assets/_Project/Prefabs/Resources/Stone.prefab";
        private const string NodePath = "Assets/_Project/Prefabs/Resources/IronStone.prefab";
        private const string SmelterPath = "Assets/_Project/Prefabs/Processor/IronSmelter.prefab";
        private const string ScenePath = "Assets/_Project/Scenes/Tower.unity";

        private static string[] Materials(Transform visuals) => visuals.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name).ToArray();

        [Test]
        public void IronTypes_AreAppendedWithReadableNames()
        {
            Assert.That((byte)ResourceType.IronStone, Is.EqualTo(12));
            Assert.That((byte)ResourceType.IronIngot, Is.EqualTo(13));
            Assert.That((byte)ResourceType.LivingGrass, Is.EqualTo(11), "Earlier values never shift.");
            Assert.That(ResourceTypeNames.Display(ResourceType.IronStone), Is.EqualTo("Iron Stone"));
            Assert.That(ResourceTypeNames.Display(ResourceType.IronIngot), Is.EqualTo("Iron Ingot"));
        }

        [Test]
        public void Definitions_HaveIconsAndGatherTuning()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Definitions);
            var stone = collection.Definitions.Single(d => d.ResourceType == ResourceType.IronStone);
            var ingot = collection.Definitions.Single(d => d.ResourceType == ResourceType.IronIngot);
            foreach (var definition in new[] { stone, ingot })
            {
                Assert.That(definition.Icon, Is.Not.Null, definition.name);
                Assert.That(definition.Icon.texture.width, Is.EqualTo(128));
                Assert.That(definition.DisplayName, Is.EqualTo(ResourceTypeNames.Display(definition.ResourceType)));
            }
            Assert.That(stone.AmountGathered, Is.EqualTo(1)); Assert.That(stone.InteractionDuration, Is.EqualTo(2.2f));
            Assert.That(stone.RespawnCooldown, Is.EqualTo(5f)); Assert.That(stone.ShakeStrength, Is.GreaterThan(0), "It weaves like the stone.");
        }

        [Test]
        public void StoneNode_WearsTheReworkedModel_OnTheGround()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(StonePath);
            Assert.That(prefab.GetComponent<Resource>().Definition.ResourceType, Is.EqualTo(ResourceType.Stone));
            var visuals = prefab.transform.Find("Visuals");
            var mesh = visuals.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo("Assets/_Project/Models/Resources/StoneCluster.fbx"));
            Assert.That(Materials(visuals), Is.EqualTo(new[] { "Resource_Stone", "Workshop_Moss", "Workshop_Pearl" }), "Stone, moss on the tops, quartz shards.");
            Assert.That(mesh.bounds.size.y, Is.LessThan(mesh.bounds.size.x), "A low, wide cluster standing upright.");
            // The stone root stands 1.5 m above the ground, as in the Tower scene.
            float bottom = visuals.localPosition.y + mesh.bounds.min.y * visuals.localScale.y + prefab.transform.position.y;
            Assert.That(bottom, Is.EqualTo(.02f).Within(.01f));
            var sphere = visuals.GetComponent<SphereCollider>();
            Assert.That(sphere.radius, Is.GreaterThan(.4f), "The collider still covers the cluster.");
        }

        [Test]
        public void IronStoneNode_IsGroundedOre_AndGathersLikeTheStone()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodePath);
            var resource = prefab.GetComponent<Resource>();
            Assert.That(resource.Definition.ResourceType, Is.EqualTo(ResourceType.IronStone));
            Assert.That(resource.NodeId, Is.EqualTo(4001));
            var ids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Resources" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<Resource>())
                .Where(r => r != null).Select(r => r.NodeId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "Resource node ids must be unique.");
            Assert.That(prefab.transform.position, Is.EqualTo(Vector3.zero), "The node's root stands on the ground.");
            var visuals = prefab.transform.Find("Visuals");
            var mesh = visuals.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo("Assets/_Project/Models/Resources/IronStoneCluster.fbx"));
            Assert.That(Materials(visuals), Is.EqualTo(new[] { "Resource_IronRock", "Workshop_Rust", "Workshop_IronOre" }));
            Assert.That(visuals.localPosition.y + mesh.bounds.min.y * visuals.localScale.y, Is.EqualTo(.02f).Within(.01f));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));

            var feedback = new SerializedObject(prefab.GetComponent<ResourceHarvestFeedback>());
            Assert.That(feedback.FindProperty("sand").boolValue, Is.False, "Rock feedback: it weaves and throws chips.");
            Assert.That((Transform)feedback.FindProperty("visualRoot").objectReferenceValue, Is.SameAs(visuals));
            var marker = (GameObject)feedback.FindProperty("cooldownMarker").objectReferenceValue;
            Assert.That(marker.activeSelf, Is.False);
            Assert.That(marker.GetComponentsInChildren<MeshRenderer>(true).All(r => r.sharedMaterial.name == "Workshop_IronOre"), "Ore bricks stay during cooldown.");
            Assert.That(feedback.FindProperty("strokeClip").objectReferenceValue, Is.Not.Null);
            var chips = prefab.GetComponentsInChildren<ParticleSystemRenderer>(true).Single(r => r.renderMode == ParticleSystemRenderMode.Mesh);
            Assert.That(chips.sharedMaterial.name, Is.EqualTo("Workshop_Rust"));
            AssertEffects(prefab);
        }

        [Test]
        public void Smelter_MeltsTwoIronStoneIntoOneIngot()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmelterPath);
            var processor = prefab.GetComponent<ResourceProcessor>();
            Assert.That(processor.SourceResource.ResourceType, Is.EqualTo(ResourceType.IronStone));
            Assert.That(processor.TargetResource.ResourceType, Is.EqualTo(ResourceType.IronIngot));
            Assert.That(processor.SourceAmount, Is.EqualTo(2)); Assert.That(processor.TargetAmount, Is.EqualTo(1));
            Assert.That(processor.InteractionDuration, Is.EqualTo(4f));
            Assert.That(new SerializedObject(processor).FindProperty("processorId").stringValue, Is.EqualTo("tower-iron-smelter-01"));
            var anim = prefab.GetComponent<ProcessorMachineAnimation>();
            Assert.That(anim.Process, Is.EqualTo(ProcessorMachineAnimation.ProcessKind.Smelt));
            Assert.That((anim.SpinCount, anim.TravelCount, anim.PulseCount, anim.FillCount, anim.DialCount), Is.EqualTo((1, 1, 0, 2, 1)),
                "Cooling fan, stamp, melt and mould, temperature dial.");
            Assert.That(anim.Output.GetComponentInChildren<MeshFilter>(true).sharedMesh.name, Does.Contain("Ingot"));
            var so = new SerializedObject(anim);
            var heat = so.FindProperty("heatSurfaces");
            Assert.That(heat.arraySize, Is.EqualTo(1));
            Assert.That(((Renderer)heat.GetArrayElementAtIndex(0).objectReferenceValue).name, Is.EqualTo("HeatCoil"), "The coils glow as it heats.");
            Assert.That(so.FindProperty("strikeClip").objectReferenceValue.name, Is.EqualTo("IronClang"));
            AssertEffects(prefab);
        }

        [Test]
        public void SmelterCycle_MeltsInTheSightGlass_ThenCastsAndStamps()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SmelterPath));
            try
            {
                Transform Part(string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t != root.transform && t.name == name);
                var processor = root.GetComponent<ResourceProcessor>(); var anim = root.GetComponent<ProcessorMachineAnimation>();
                float stampRest = Part("Stamp").position.y;
                processor.BeginInteraction(null);
                for (int step = 1; step <= 12; step++) anim.SampleEditorPose(step * .08f, step / 12f * .6f);
                Assert.That(Part("MeltColumn").gameObject.activeSelf, Is.True, "Molten iron rises in the sight glass.");
                float melted = Part("MeltColumn").localScale.y;
                for (int step = 1; step <= 12; step++) anim.SampleEditorPose(1 + step * .08f, .6f + step / 12f * .25f);
                Assert.That(Part("MouldFill").gameObject.activeSelf, Is.True, "It runs down into the mould.");
                Assert.That(Part("MeltColumn").localScale.y, Is.LessThan(melted), "The sight glass drains as the mould fills.");
                // The single stroke lands 18% into the stamp's window (.86 to .97), then lifts again.
                for (int step = 1; step <= 6; step++) anim.SampleEditorPose(2 + step * .08f, .85f + step / 6f * (.86f + .11f * .18f - .85f));
                Assert.That(Part("Stamp").position.y, Is.LessThan(stampRest - .2f), "The stamp comes down on the cast.");
                for (int step = 1; step <= 6; step++) anim.SampleEditorPose(2.5f + step * .08f, .88f + step / 6f * .08f);
                Assert.That(anim.StrikeCount, Is.EqualTo(1), "One ringing strike.");
                processor.CancelInteraction();
                Assert.That(anim.IsOutputVisible, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void ServerStoreAndCheats_SupportIron()
        {
            var store = new ServerPlayerResourceStore(50);
            store.SetAmount(1, ResourceType.IronStone, 4);
            Assert.That(store.TryExchange(1, ResourceType.IronStone, 2, ResourceType.IronIngot, 1, out int source, out int target), Is.True);
            Assert.That(source, Is.EqualTo(2)); Assert.That(target, Is.EqualTo(1));
            foreach (string name in new[] { "AddIronStone", "AddIronIngot" })
                Assert.That(Enum.IsDefined(typeof(CheatCommand), name), Is.True, name);
            Assert.That((int)CheatCommand.BuildFloorDetail, Is.EqualTo(27), "Existing cheat command values stay stable.");
            Assert.That((int)CheatCommand.AddIronStone, Is.EqualTo(28));
        }

        [Test]
        public void TowerScene_HasOneIronStoneAndOneSmelter_OnTheGroundInTheWorkshopRow()
        {
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !(loaded.IsValid() && loaded.isLoaded);
            var scene = opened ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : loaded;
            try
            {
                var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
                var nodes = all.Where(t => t.GetComponent<Resource>() != null && t.GetComponent<Resource>().Definition.ResourceType == ResourceType.IronStone).ToArray();
                var smelters = all.Where(t => t.GetComponent<ResourceProcessor>() != null && t.GetComponent<ResourceProcessor>().TargetResource.ResourceType == ResourceType.IronIngot).ToArray();
                Assert.That(nodes.Length, Is.EqualTo(1)); Assert.That(smelters.Length, Is.EqualTo(1));
                var grass = all.Single(t => t.name == "Grass" && t.GetComponent<Resource>() != null);
                var planter = all.Single(t => t.name == "GrassPlanter" && t.GetComponent<ResourceProcessor>() != null);
                Assert.That(nodes[0].parent, Is.SameAs(grass.parent), "Grouped with the other resource nodes.");
                Assert.That(smelters[0].parent, Is.SameAs(planter.parent), "Grouped with the other processors.");
                foreach (var t in nodes.Concat(smelters))
                {
                    Assert.That(t.position.y, Is.LessThan(.4f), t.name + " stands on the ground.");
                    Assert.That(t.position.z, Is.EqualTo(grass.position.z).Within(.01f), t.name + " continues the workshop row.");
                    Assert.That(t.position.x, Is.GreaterThan(planter.position.x + 2), t.name + " sits past the planter.");
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static void AssertEffects(GameObject prefab)
        {
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(ps.main.playOnAwake, Is.False, prefab.name);
                Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(64), prefab.name);
            }
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).All(a => !a.playOnAwake && a.spatialBlend == 1), Is.True, prefab.name);
        }
    }
}
