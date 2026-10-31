using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class EssenceWorkshopEditModeTests
    {
        private const string Definitions = "Assets/_Project/Resources/Definitions/ResourceDefinitions.asset";
        private static readonly (ResourceType Essence, ResourceType Block, string Node, string Container, string Base)[] Families =
        {
            (ResourceType.RiverEssence, ResourceType.RiverBlock, "RiverEssence", "RiverEssence", "PoolBed"),
            (ResourceType.WaterEssence, ResourceType.WaterBlock, "WaterEssence", "WaterEssence", "MossBed"),
            (ResourceType.SkyEssence, ResourceType.SkyBlock, "SkyEssence", "SkyEssence", "SkyStones"),
        };
        private static int[] FamilyIndices => Enumerable.Range(0, Families.Length).ToArray();
        private static string NodePath(int i) => "Assets/_Project/Prefabs/Resources/" + Families[i].Node + ".prefab";
        private static readonly string[] MachineMaterials = { "Workshop_Pearl", "Workshop_Azure", "Workshop_Steel" };

        [Test]
        public void ResourceTypes_AppendNewMaterialsWithoutRenumberingSavedOrNetworkedValues()
        {
            Assert.That((byte)ResourceType.Stone, Is.EqualTo(0)); Assert.That((byte)ResourceType.ProcessedStone, Is.EqualTo(1));
            Assert.That((byte)ResourceType.Sand, Is.EqualTo(2)); Assert.That((byte)ResourceType.Glass, Is.EqualTo(3));
            Assert.That((byte)ResourceType.RiverEssence, Is.EqualTo(4)); Assert.That((byte)ResourceType.RiverBlock, Is.EqualTo(5));
            Assert.That((byte)ResourceType.WaterEssence, Is.EqualTo(6)); Assert.That((byte)ResourceType.WaterBlock, Is.EqualTo(7));
            Assert.That((byte)ResourceType.SkyEssence, Is.EqualTo(8)); Assert.That((byte)ResourceType.SkyBlock, Is.EqualTo(9));
            Assert.That(ResourceTypeNames.Display(ResourceType.RiverBlock), Is.EqualTo("River Block"));
            Assert.That(ResourceTypeNames.Display(ResourceType.ProcessedStone), Is.EqualTo("Processed Stone"));
            Assert.That(ResourceTypeNames.Display(ResourceType.Glass), Is.EqualTo("Glass"));
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                Assert.That(ResourceTypeNames.Display(type), Does.Not.Match("[a-z][A-Z]"), "Build costs must not show raw enum names: " + type);
        }

        [Test]
        public void Definitions_EveryNewMaterialHasIconUniqueIdAndGatherTuning()
        {
            var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Definitions);
            Assert.That(collection.Definitions.Select(d => d.ResourceId).Distinct().Count(), Is.EqualTo(collection.Definitions.Count));
            Assert.That(collection.Definitions.Select(d => d.ResourceType).Distinct().Count(), Is.EqualTo(collection.Definitions.Count));
            foreach (var family in Families)
            {
                var essence = collection.Definitions.Single(d => d.ResourceType == family.Essence);
                var block = collection.Definitions.Single(d => d.ResourceType == family.Block);
                foreach (var definition in new[] { essence, block })
                {
                    Assert.That(definition.Icon, Is.Not.Null, definition.name);
                    Assert.That(definition.Icon.texture.width, Is.EqualTo(128));
                    Assert.That(definition.DisplayName, Is.EqualTo(ResourceTypeNames.Display(definition.ResourceType)));
                }
                Assert.That(essence.AmountGathered, Is.EqualTo(2)); Assert.That(essence.InteractionDuration, Is.EqualTo(2.8f));
                Assert.That(essence.RespawnCooldown, Is.EqualTo(6f));
            }
        }

        [Test]
        public void Buildings_ConsumeTheirAssignedProcessedMaterial()
        {
            foreach (var (prefab, type) in new[] { ("Step_Tile", ResourceType.Glass), ("GlassWaterFloor", ResourceType.RiverBlock),
                ("Pillar", ResourceType.WaterBlock), ("Floor_Tile", ResourceType.ProcessedStone), ("Arch", ResourceType.SkyBlock) })
            {
                var building = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/" + prefab + ".prefab").GetComponent<Building>();
                Assert.That(building.ResourceType, Is.EqualTo(type), prefab);
                Assert.That(building.ResourceCostPerStage, Is.EqualTo(1), prefab);
            }
        }

        [Test]
        public void NodePrefabs_AreGroundedNaturalNodesWithOneFixedColliderAndALastingBase()
        {
            var ids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs/Resources" })
                .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<Resource>())
                .Where(r => r != null).Select(r => r.NodeId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "Resource node ids must be unique.");
            for (int i = 0; i < Families.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NodePath(i));
                var resource = prefab.GetComponent<Resource>();
                Assert.That(resource.Definition.ResourceType, Is.EqualTo(Families[i].Essence));
                Assert.That(resource.NodeId, Is.Not.Zero);
                Assert.That(resource.Definition.ShakeStrength, Is.Zero, "Grounded nodes do not weave, like the sand pile.");
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
                var feedback = prefab.GetComponent<EssenceHarvestFeedback>();
                Assert.That(feedback, Is.Not.Null); Assert.That(feedback.Body, Is.Not.Null);
                Assert.That(feedback.FloaterCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(new SerializedObject(feedback).FindProperty("strokeClip").objectReferenceValue, Is.Not.Null);
                // The essence container hides on cooldown; the natural base stays, like the stone's bricks.
                var visuals = ((GameObject)new SerializedObject(resource).FindProperty("visuals").objectReferenceValue).transform;
                Assert.That(visuals.name, Is.EqualTo(Families[i].Container)); Assert.That(feedback.Body.IsChildOf(visuals), Is.True);
                var floaters = new SerializedObject(feedback).FindProperty("floaters");
                for (int f = 0; f < floaters.arraySize; f++)
                    Assert.That(((Transform)floaters.GetArrayElementAtIndex(f).objectReferenceValue).IsChildOf(visuals), Is.True);
                var lasting = prefab.GetComponentsInChildren<Transform>(true).Single(t => t.name == Families[i].Base);
                Assert.That(lasting.IsChildOf(visuals), Is.False); Assert.That(lasting.GetComponent<Renderer>(), Is.Not.Null);
                Assert.That(prefab.GetComponent<Collider>().transform.IsChildOf(visuals), Is.False);
                // Natural and low like the stone and sand: no machine parts, the body sits on the ground.
                var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.SelectMany(r => r.sharedMaterials).Any(m => m != null && MachineMaterials.Contains(m.name)), Is.False);
                var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                Assert.That(bounds.size.y, Is.LessThan(1.3f)); Assert.That(bounds.size.x, Is.InRange(1.4f, 2.6f));
                Assert.That(feedback.Body.position.y - prefab.transform.position.y, Is.LessThan(.05f), "The body pivots on the ground.");
                AssertEffects(prefab);
            }
        }

        [Test]
        public void EssenceGathering_ShrinksTheAnchoredBodyLiftsLoosePiecesAndCancelRestores([ValueSource(nameof(FamilyIndices))] int family)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(NodePath(family)));
            try
            {
                var resource = root.GetComponent<Resource>(); var feedback = root.GetComponent<EssenceHarvestFeedback>();
                var collider = root.GetComponent<Collider>(); var bounds = collider.bounds;
                var body = feedback.Body.GetComponent<Renderer>(); float bodyBottom = body.bounds.min.y, bodyTop = body.bounds.max.y;
                var so = new SerializedObject(feedback).FindProperty("floaters");
                var floaters = Enumerable.Range(0, so.arraySize).Select(i => (Transform)so.GetArrayElementAtIndex(i).objectReferenceValue).ToArray();
                float floatHeight = floaters.Average(f => f.position.y);
                resource.BeginInteraction(null); resource.UpdateInteraction(.85f);
                Assert.That(feedback.IsWorking, Is.True);
                Assert.That(feedback.PresentationScale, Is.LessThan(.35f)); Assert.That(feedback.WidthScale, Is.LessThan(.75f));
                // Anchored on the ground: the base never lifts (puffs that dip below ground only rise toward it).
                Assert.That(body.bounds.min.y, Is.LessThanOrEqualTo(Mathf.Max(bodyBottom, root.transform.position.y) + .04f), "The body stays anchored to the ground.");
                Assert.That(body.bounds.max.y, Is.LessThan(bodyTop - .1f), "The body sinks as it is gathered.");
                Assert.That(floaters.Average(f => f.position.y), Is.GreaterThan(floatHeight + .15f), "Loose pieces lift away.");
                Assert.That(collider.bounds.center, Is.EqualTo(bounds.center)); Assert.That(collider.bounds.size, Is.EqualTo(bounds.size));
                resource.CancelInteraction(); root.GetComponent<AeroInteractionMotion>().Stop(true);
                Assert.That(feedback.IsWorking, Is.False); Assert.That(feedback.PresentationScale, Is.EqualTo(1).Within(.1f));
                Assert.That(feedback.FloaterLift, Is.Zero); Assert.That(feedback.CompletionCount, Is.Zero);
                feedback.Deplete(); feedback.Deplete();
                Assert.That(feedback.IsDepleted, Is.True); Assert.That(feedback.CompletionCount, Is.EqualTo(1), "Duplicate notifications must not replay the burst.");
                feedback.Respawn(); Assert.That(feedback.IsDepleted, Is.False);
                feedback.ResetFeedback(); Assert.That(body.bounds.max.y, Is.EqualTo(bodyTop).Within(.0001f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void ServerStoreAndCheats_SupportEveryNewMaterial()
        {
            var store = new ServerPlayerResourceStore(50);
            foreach (var family in Families)
            {
                store.SetAmount(1, family.Essence, 4);
                Assert.That(store.TryExchange(1, family.Essence, 2, family.Block, 1, out int source, out int target), Is.True);
                Assert.That(source, Is.EqualTo(2)); Assert.That(target, Is.EqualTo(1));
            }
            Assert.That(store.GetAmount(1, ResourceType.Glass), Is.Zero);
            foreach (string name in new[] { "AddRiverEssence", "AddRiverBlock", "AddWaterEssence", "AddWaterBlock", "AddSkyEssence", "AddSkyBlock" })
                Assert.That(Enum.IsDefined(typeof(CheatCommand), name), Is.True, name);
            Assert.That((int)CheatCommand.AddGlass, Is.EqualTo(17), "Existing cheat command values stay stable.");
        }

        private static void AssertEffects(GameObject prefab)
        {
            foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
            {
                Assert.That(ps.main.playOnAwake, Is.False, prefab.name);
                Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(64), prefab.name);
                Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
            }
            Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).All(a => !a.playOnAwake && a.spatialBlend == 1), Is.True, prefab.name);
        }
    }
}
