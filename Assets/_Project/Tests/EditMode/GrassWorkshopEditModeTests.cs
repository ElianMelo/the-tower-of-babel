using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>Grass is gathered, grown into Living Grass at the planter, and Living Grass builds the detail vines.</summary>
    public sealed class GrassWorkshopEditModeTests
    {
        private const string Definitions = "Assets/_Project/Resources/Definitions/ResourceDefinitions.asset";
        private const string NodePath = "Assets/_Project/Prefabs/Resources/Grass.prefab";
        private const string PlanterPath = "Assets/_Project/Prefabs/Processor/GrassPlanter.prefab";
        private static readonly string[] VinePrefabs =
            Enumerable.Range(0, TowerAssetTypeExtensions.DetailCount).Select(i => "Detail_Vine_" + i).ToArray();

        [Test]
        public void GrassTypes_AreAppendedWithReadableNames()
        {
            Assert.That((byte)ResourceType.Grass, Is.EqualTo(10));
            Assert.That((byte)ResourceType.LivingGrass, Is.EqualTo(11));
            Assert.That(ResourceTypeNames.Display(ResourceType.Grass), Is.EqualTo("Grass"));
            Assert.That(ResourceTypeNames.Display(ResourceType.LivingGrass), Is.EqualTo("Living Grass"));
        }

        [Test]
        public void Definitions_HaveIconsAndGatherTuning()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Definitions);
            var grass = collection.Definitions.Single(d => d.ResourceType == ResourceType.Grass);
            var living = collection.Definitions.Single(d => d.ResourceType == ResourceType.LivingGrass);
            foreach (var definition in new[] { grass, living })
            {
                Assert.That(definition.Icon, Is.Not.Null, definition.name);
                Assert.That(definition.Icon.texture.width, Is.EqualTo(128));
                Assert.That(definition.DisplayName, Is.EqualTo(ResourceTypeNames.Display(definition.ResourceType)));
            }
            Assert.That(grass.AmountGathered, Is.EqualTo(2)); Assert.That(grass.InteractionDuration, Is.EqualTo(2.6f));
            Assert.That(grass.RespawnCooldown, Is.EqualTo(6f)); Assert.That(grass.ShakeStrength, Is.Zero);
        }

        [Test]
        public void GrassNode_IsAGroundedTuftThatLeavesStubbleDuringCooldown()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodePath);
            var resource = prefab.GetComponent<Resource>();
            Assert.That(resource.Definition.ResourceType, Is.EqualTo(ResourceType.Grass));
            Assert.That(resource.NodeId, Is.EqualTo(3004));
            var ids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Resources" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<Resource>())
                .Where(r => r != null).Select(r => r.NodeId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "Resource node ids must be unique.");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
            var feedback = new SerializedObject(prefab.GetComponent<ResourceHarvestFeedback>());
            var tuft = (Transform)feedback.FindProperty("visualRoot").objectReferenceValue;
            var stubble = (GameObject)feedback.FindProperty("cooldownMarker").objectReferenceValue;
            Assert.That(tuft.name, Is.EqualTo("GrassTuft"));
            Assert.That(((GameObject)new SerializedObject(resource).FindProperty("visuals").objectReferenceValue).transform, Is.SameAs(tuft));
            Assert.That(stubble.name, Is.EqualTo("Stubble")); Assert.That(stubble.activeSelf, Is.False);
            Assert.That(prefab.transform.Find("MeadowBed").IsChildOf(tuft), Is.False, "The mossy bed stays while the grass regrows.");
            Assert.That(feedback.FindProperty("strokeClip").objectReferenceValue, Is.Not.Null);
            var blades = tuft.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(blades.bounds.size.x, Is.InRange(1.2f, 1.9f)); Assert.That(blades.bounds.max.y, Is.InRange(.3f, .8f));
            Assert.That(blades.colors.Max(c => c.a), Is.EqualTo(1).Within(.01f), "Blade tips sway in the wind.");
            AssertEffects(prefab);
        }

        [Test]
        public void Planter_GrowsTwoGrassIntoOneLivingGrass()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlanterPath);
            var processor = prefab.GetComponent<ResourceProcessor>();
            Assert.That(processor.SourceResource.ResourceType, Is.EqualTo(ResourceType.Grass));
            Assert.That(processor.TargetResource.ResourceType, Is.EqualTo(ResourceType.LivingGrass));
            Assert.That(processor.SourceAmount, Is.EqualTo(2)); Assert.That(processor.TargetAmount, Is.EqualTo(1));
            Assert.That(processor.InteractionDuration, Is.EqualTo(3.5f));
            Assert.That(new SerializedObject(processor).FindProperty("processorId").stringValue, Is.EqualTo("tower-grass-planter-01"));
            Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
            var anim = prefab.GetComponent<ProcessorMachineAnimation>();
            Assert.That(anim.Process, Is.EqualTo(ProcessorMachineAnimation.ProcessKind.Grow));
            Assert.That(anim.Feed, Is.Not.Null); Assert.That(anim.IsFeedVisible, Is.False);
            Assert.That(anim.Output, Is.Not.Null); Assert.That(anim.IsOutputVisible, Is.False);
            Assert.That(anim.SpinCount, Is.EqualTo(1), "The mister spins.");
            var so = new SerializedObject(anim);
            Assert.That(so.FindProperty("statusLights").arraySize, Is.EqualTo(3));
            Assert.That(so.FindProperty("fills").arraySize, Is.EqualTo(1), "Sprouts fill the dome.");
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Dome"), Is.True);
            Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "BlockSocket"), Is.True);
            AssertEffects(prefab);
        }

        [Test]
        public void Vines_AreBuiltFromLivingGrassAndGrowEachStage()
        {
            var types = VinePrefabs.Select(p => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/" + p + ".prefab")).ToArray();
            for (int i = 0; i < types.Length; i++)
            {
                var prefab = types[i];
                Assert.That(prefab, Is.Not.Null, VinePrefabs[i]);
                var building = prefab.GetComponent<Building>();
                Assert.That(building.ResourceType, Is.EqualTo(ResourceType.LivingGrass), prefab.name);
                Assert.That(building.ResourceCostPerStage, Is.EqualTo(1));
                Assert.That(prefab.GetComponent<TowerAsset>().AssetType, Is.EqualTo(TowerAssetTypeExtensions.DetailTypes[i]));
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
                Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("TowerOfBabel/Foliage/Clinging Vine"));
                Assert.That(prefab.GetComponent<MeshRenderer>().sharedMaterial.enableInstancing, Is.True);
                int previous = 0;
                for (byte stage = 0; stage <= 10; stage++)
                {
                    var mesh = building.GetStageMesh(stage);
                    Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(previous), $"{prefab.name} stage {stage}");
                    previous = mesh.vertexCount;
                }
                Assert.That(building.GetStageMesh(0).bounds.size.magnitude, Is.LessThan(1.6f), "Stage 0 is a small sprout.");
                Assert.That(building.GetStageMesh(10).bounds.size.magnitude, Is.GreaterThan(building.GetStageMesh(0).bounds.size.magnitude * 2));
                Assert.That(building.GetStageMesh(10).triangles.Length / 3, Is.LessThan(8000), "Keep distant batches affordable.");
            }
            // Some shapes stay on the pillar, some cover only the arch, and the rest climb from one onto the other.
            bool OnPillar(Vector3 v) => Mathf.Abs(v.x) < .25f && v.y < 3.6f;
            var grown = types.Select(t => t.GetComponent<Building>().GetStageMesh(10)).ToArray();
            Assert.That(grown.Count(m => m.bounds.max.y < 4.05f), Is.GreaterThanOrEqualTo(1), "A pillar-only shape.");
            Assert.That(grown.Count(m => !m.vertices.Any(OnPillar) && m.bounds.min.y > 2.5f), Is.GreaterThanOrEqualTo(1), "An arch-only shape.");
            Assert.That(grown.Count(m => m.vertices.Any(OnPillar) && m.bounds.max.y > 4.6f), Is.GreaterThanOrEqualTo(4), "Shapes spanning pillar and arch.");
            Assert.That(grown.Max(m => m.bounds.size.x), Is.GreaterThan(4f), "The widest vines reach well past the neighbouring pillar.");
            foreach (string stale in new[] { "Detail_Bush", "Detail_Vine_Climbing", "Detail_Vine_Draped", "Detail_Vine_Dense" })
                Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/" + stale + ".prefab"), Is.Null, stale + " is superseded.");
            Assert.That(types.Select(t => t.GetComponent<Building>().GetStageMesh(10).vertexCount).Distinct().Count(), Is.EqualTo(types.Length), "Every shape grows differently.");
            Assert.That(types.Average(t => t.GetComponent<Building>().GetStageMesh(10).triangles.Length / 3), Is.LessThan(5000), "Thousands of vines must stay affordable on average.");
        }

        [Test]
        public void ServerStoreAndCheats_SupportGrass()
        {
            var store = new ServerPlayerResourceStore(50);
            store.SetAmount(1, ResourceType.Grass, 4);
            Assert.That(store.TryExchange(1, ResourceType.Grass, 2, ResourceType.LivingGrass, 1, out int source, out int target), Is.True);
            Assert.That(source, Is.EqualTo(2)); Assert.That(target, Is.EqualTo(1));
            foreach (string name in new[] { "AddGrass", "AddLivingGrass" })
                Assert.That(Enum.IsDefined(typeof(CheatCommand), name), Is.True, name);
            Assert.That((int)CheatCommand.AddGlass, Is.EqualTo(17), "Existing cheat command values stay stable.");
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
