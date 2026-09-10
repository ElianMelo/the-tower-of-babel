using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ResourceProcessorEditModeTests
    {
        [Test]
        public void StoneProcessorPrefab_HasRecipeAndResolvableInteractionCollider()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Processor/StoneProcessor.prefab");
            ResourceProcessor processor = prefab.GetComponent<ResourceProcessor>();
            Assert.That(processor, Is.Not.Null);
            Assert.That(processor.SourceResource.ResourceType, Is.EqualTo(ResourceType.Stone));
            Assert.That(processor.SourceAmount, Is.EqualTo(1));
            Assert.That(processor.TargetResource.ResourceType, Is.EqualTo(ResourceType.ProcessedStone));
            Assert.That(processor.TargetAmount, Is.EqualTo(1));
            Assert.That(processor.InteractionDuration, Is.EqualTo(2f));
            Collider collider = prefab.GetComponentInChildren<Collider>();
            Assert.That(collider.enabled && !collider.isTrigger, Is.True);
            Assert.That(collider.GetComponentInParent<ResourceProcessor>(true), Is.SameAs(processor));
            Assert.That(processor, Is.InstanceOf<IInteractable>());
            Assert.That(processor, Is.InstanceOf<IServerInteractionCompletion>());
        }

        [TestCase(0, 0, 1, 1, false, 0, 0)]
        [TestCase(1, 0, 1, 1, true, 0, 1)]
        [TestCase(3, 4, 2, 3, true, 1, 7)]
        [TestCase(1, 0, 2, 1, false, 1, 0)]
        [TestCase(3, 49, 1, 2, false, 3, 49)]
        [TestCase(3, 50, 1, 1, false, 3, 50)]
        [TestCase(3, 0, 0, 1, false, 3, 0)]
        [TestCase(3, 0, 1, 0, false, 3, 0)]
        [TestCase(3, 1, 1, int.MaxValue, false, 3, 1)]
        public void Exchange_IsAtomicAndRequiresFullOutputCapacity(int source, int target, int cost,
            int production, bool expected, int expectedSource, int expectedTarget)
        {
            ServerPlayerResourceStore store = new(50);
            store.TryAdd(7, ResourceType.Stone, source, out _);
            store.TryAdd(7, ResourceType.ProcessedStone, target, out _);
            Assert.That(store.CanExchange(7, ResourceType.Stone, cost, ResourceType.ProcessedStone, production), Is.EqualTo(expected));
            Assert.That(store.GetAmount(7, ResourceType.Stone), Is.EqualTo(source), "Validation must not reserve or consume materials.");
            Assert.That(store.TryExchange(7, ResourceType.Stone, cost, ResourceType.ProcessedStone, production,
                out int sourceAfter, out int targetAfter), Is.EqualTo(expected));
            Assert.That(sourceAfter, Is.EqualTo(expectedSource));
            Assert.That(targetAfter, Is.EqualTo(expectedTarget));
            Assert.That(store.GetAmount(7, ResourceType.Stone), Is.EqualTo(expectedSource));
            Assert.That(store.GetAmount(7, ResourceType.ProcessedStone), Is.EqualTo(expectedTarget));
        }

        [Test]
        public void Exchange_RechecksBalancesAndKeepsPlayersIndependent()
        {
            ServerPlayerResourceStore store = new(50);
            store.TryAdd(1, ResourceType.Stone, 2, out _);
            store.TryAdd(2, ResourceType.Stone, 2, out _);
            Assert.That(store.CanExchange(1, ResourceType.Stone, 2, ResourceType.ProcessedStone, 1), Is.True);
            store.TryConsume(1, ResourceType.Stone, 1, out _);
            Assert.That(store.TryExchange(1, ResourceType.Stone, 2, ResourceType.ProcessedStone, 1, out _, out _), Is.False);
            Assert.That(store.TryExchange(2, ResourceType.Stone, 2, ResourceType.ProcessedStone, 1, out _, out _), Is.True);
            Assert.That(store.GetAmount(1, ResourceType.Stone), Is.EqualTo(1));
            Assert.That(store.GetAmount(1, ResourceType.ProcessedStone), Is.Zero);
            Assert.That(store.GetAmount(2, ResourceType.Stone), Is.Zero);
            Assert.That(store.GetAmount(2, ResourceType.ProcessedStone), Is.EqualTo(1));
        }

        [Test]
        public void Exchange_SameResourceUsesNetAmountAtCapacity()
        {
            ServerPlayerResourceStore store = new(50);
            store.TryAdd(1, ResourceType.Stone, 50, out _);
            Assert.That(store.TryExchange(1, ResourceType.Stone, 3, ResourceType.Stone, 2,
                out int source, out int target), Is.True);
            Assert.That(source, Is.EqualTo(49));
            Assert.That(target, Is.EqualTo(49));
        }

        [Test]
        public void Processor_UsesOnlyProcessUpgradesAndClampsCostAndDuration()
        {
            GameObject serviceRoot = new("Upgrade test service");
            GameObject root = new("Processor");
            UpgradeTreeConfig config = ScriptableObject.CreateInstance<UpgradeTreeConfig>();
            try
            {
                NetworkUpgradeService upgrades = serviceRoot.AddComponent<NetworkUpgradeService>();
                Invoke(upgrades, "Awake");
                Set(upgrades, "config", config);
                Set(config, "boards", new List<UpgradeBoardData>
                {
                    new(UpgradeJob.Process, new List<UpgradeData>
                    {
                        new("speed", "Speed", 3, 3, UpgradeEffectType.Efficiency, 0.5f),
                        new("cost", "Cost", 3, 4, UpgradeEffectType.Cost, -2),
                        new("output", "Output", 3, 5, UpgradeEffectType.Production, 3)
                    }),
                    new(UpgradeJob.Gather, new List<UpgradeData>
                    {
                        new("gather", "Gather", 3, 3, UpgradeEffectType.Production, 100)
                    })
                });
                PlayerUpgradeProgress progress = Get<PlayerUpgradeProgress>(upgrades, "localProgress");
                progress.Get(UpgradeJob.Process).ApplySnapshot(new(UpgradeJob.Process, 5, 0, 0, new[] { "speed", "cost", "output" }));
                progress.Get(UpgradeJob.Gather).ApplySnapshot(new(UpgradeJob.Gather, 5, 0, 0, new[] { "gather" }));
                ResourceProcessor processor = root.AddComponent<ResourceProcessor>();
                Set(processor, "sourceAmount", 4);
                Assert.That(processor.Duration, Is.EqualTo(1.5f));
                Assert.That(processor.EffectiveSourceAmount, Is.EqualTo(2));
                Assert.That(processor.EffectiveTargetAmount, Is.EqualTo(4));
                Set(processor, "sourceAmount", 1);
                Set(processor, "interactionDuration", 0.2f);
                Assert.That(processor.EffectiveSourceAmount, Is.EqualTo(1));
                Assert.That(processor.Duration, Is.EqualTo(0.1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(serviceRoot);
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void DuplicatedProcessors_HaveDistinctStableSceneIds()
        {
            GameObject first = new("Processor");
            GameObject second = new("Processor");
            try
            {
                ResourceProcessor a = first.AddComponent<ResourceProcessor>();
                ResourceProcessor b = second.AddComponent<ResourceProcessor>();
                string id = a.ProcessorId;
                Assert.That(b.ProcessorId, Is.Not.EqualTo(id));
                second.transform.SetSiblingIndex(0);
                Assert.That(a.ProcessorId, Is.EqualTo(id));
                Assert.That(a.ServerCanProcess, Is.False, "Unconfigured recipes must be unavailable.");
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(target, null);
    }
}
