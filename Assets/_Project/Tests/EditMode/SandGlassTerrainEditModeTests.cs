using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class SandGlassTerrainEditModeTests
    {
        private const string SandPath = "Assets/_Project/Prefabs/Resources/Sand.prefab";
        private const string KilnPath = "Assets/_Project/Prefabs/Processor/GlassFurnace.prefab";
        [Test]
        public void ResourceDefinitions_PreserveExistingIdsAndExposeEveryResourceWithAnIcon()
        {
            Assert.That((byte)ResourceType.Stone, Is.Zero); Assert.That((byte)ResourceType.ProcessedStone, Is.EqualTo(1));
            var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>("Assets/_Project/Resources/Definitions/ResourceDefinitions.asset");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
            {
                var definition = collection.Definitions.Single(d => d.ResourceType == type);
                Assert.That(definition.Icon, Is.Not.Null, type.ToString());
                Assert.That(definition.ResourceId, Is.Not.Empty);
            }
            var store = new ServerPlayerResourceStore(50); store.SetAmount(1, ResourceType.Sand, 4);
            Assert.That(store.TryExchange(1, ResourceType.Sand, 2, ResourceType.Glass, 1, out int sand, out int glass), Is.True);
            Assert.That(sand, Is.EqualTo(2)); Assert.That(glass, Is.EqualTo(1));
            Assert.That(store.GetAmount(1, ResourceType.Stone), Is.Zero);
        }
        [Test]
        public void ResourcePrefabs_HaveFixedCollidersCappedParticleSystemsAndCorrectKilnRecipe()
        {
            foreach (string path in new[] { SandPath, KilnPath, "Assets/_Project/Prefabs/Resources/Stone.prefab" })
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1), path);
                foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    Assert.That(ps.main.playOnAwake, Is.False, path);
                    Assert.That(ps.main.maxParticles, Is.LessThanOrEqualTo(64));
                    Assert.That(ps.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World));
                    if (ps.collision.enabled) Assert.That(ps.collision.type, Is.EqualTo(ParticleSystemCollisionType.Planes));
                }
                Assert.That(prefab.GetComponentsInChildren<AudioSource>(true).All(a => !a.playOnAwake && a.spatialBlend == 1), Is.True);
            }
            var kiln = AssetDatabase.LoadAssetAtPath<GameObject>(KilnPath).GetComponent<ResourceProcessor>();
            Assert.That(kiln.SourceResource.ResourceType, Is.EqualTo(ResourceType.Sand)); Assert.That(kiln.SourceAmount, Is.EqualTo(2));
            Assert.That(kiln.TargetResource.ResourceType, Is.EqualTo(ResourceType.Glass)); Assert.That(kiln.TargetAmount, Is.EqualTo(1));
            Assert.That(kiln.GetComponent<ProcessorMachineAnimation>().Process, Is.EqualTo(ProcessorMachineAnimation.ProcessKind.Melt));
            var sandNode = AssetDatabase.LoadAssetAtPath<GameObject>(SandPath).GetComponent<Resource>();
            Assert.That(sandNode.NodeId, Is.Not.Zero); Assert.That(sandNode.Definition.AmountGathered, Is.EqualTo(2));
        }
        [Test]
        public void SandGathering_ShrinksPresentationAndCancellationRestoresWithoutMovingCollider()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SandPath));
            try
            {
                var resource = root.GetComponent<Resource>(); var feedback = root.GetComponent<ResourceHarvestFeedback>();
                var collider = root.GetComponent<Collider>(); var before = collider.bounds;
                var source = root.GetComponentInChildren<MeshRenderer>(); var sourceScale = source.transform.localScale;
                resource.BeginInteraction(null); resource.UpdateInteraction(.8f);
                root.GetComponent<AeroInteractionMotion>().SampleEditorPose(1.8f, .8f);
                Assert.That(feedback.PresentationScale, Is.LessThan(.4f));
                Assert.That(source.forceRenderingOff, Is.True);
                Assert.That(source.transform.localScale, Is.EqualTo(sourceScale));
                Assert.That(collider.bounds.center, Is.EqualTo(before.center)); Assert.That(collider.bounds.size, Is.EqualTo(before.size));
                resource.CancelInteraction(); root.GetComponent<AeroInteractionMotion>().Stop(true);
                Assert.That(source.forceRenderingOff, Is.False); Assert.That(feedback.PresentationScale, Is.EqualTo(1));
                Assert.That(feedback.IsDepleted, Is.False); Assert.That(feedback.CompletionCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void KilnPresentation_ConsumesFeedBeforeOutputAndPreservesCollider()
        {
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(KilnPath));
            try
            {
                var motion = root.GetComponent<ProcessorMachineAnimation>(); var before = root.GetComponent<Collider>().bounds;
                var parts = root.GetComponentsInChildren<Transform>(true);
                var feed = motion.Feed; var pool = parts.Single(t => t.name == "MeltPool");
                motion.BeginWork();
                motion.SampleEditorPose(.4f, .1f); Assert.That(feed.gameObject.activeSelf, Is.True); Assert.That(motion.IsOutputVisible, Is.False);
                motion.SampleEditorPose(2.5f, .9f); Assert.That(feed.gameObject.activeSelf, Is.False); Assert.That(pool.gameObject.activeSelf, Is.True);
                Assert.That(motion.IsOutputVisible, Is.False);
                motion.SampleEditorPose(3.5f, 1, .2f); Assert.That(motion.IsOutputVisible, Is.True);
                motion.SampleEditorPose(5, 1, 1.4f); Assert.That(motion.IsOutputVisible, Is.False);
                motion.ResetImmediately(); Assert.That(feed.gameObject.activeSelf || pool.gameObject.activeSelf || motion.IsOutputVisible, Is.False);
                Assert.That(root.GetComponent<Collider>().bounds, Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void Landscape_UsesLightweightDataAndDistinctEditorRuntimeBudgets()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity", OpenSceneMode.Additive);
            try
            {
                var landscape = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AeroTowerTerrain>(true)).Single();
                var terrain = landscape.GetComponent<Terrain>(); var data = terrain.terrainData; var quality = terrain.GetComponent<TerrainPresentationQuality>();
                Assert.That(data.heightmapResolution, Is.LessThanOrEqualTo(513)); Assert.That(data.alphamapResolution, Is.LessThanOrEqualTo(256));
                Assert.That(data.detailResolution, Is.LessThanOrEqualTo(512)); Assert.That(data.detailScatterMode, Is.EqualTo(DetailScatterMode.InstanceCountMode));
                Assert.That(data.size, Is.EqualTo(landscape.SourceTerrainData.size)); Assert.That(terrain.drawInstanced, Is.True);
                long total = 0;
                foreach (int count in data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, 0)) { Assert.That(count, Is.InRange(0, 4)); total += count; }
                Assert.That(total, Is.LessThan(800000));
                Assert.That(quality.EditorGrassDensity, Is.LessThan(quality.RuntimeGrassDensity / 4));
                quality.ApplyQuality(true); Assert.That(terrain.detailObjectDistance, Is.EqualTo(95)); Assert.That(terrain.detailObjectDensity, Is.EqualTo(.55f));
                quality.ApplyQuality(false); Assert.That(terrain.detailObjectDistance, Is.EqualTo(35)); Assert.That(terrain.detailObjectDensity, Is.EqualTo(.08f));
                Assert.That(terrain.GetComponent<TerrainCollider>().terrainData, Is.SameAs(data));
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
