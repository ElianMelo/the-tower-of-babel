#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FishNet.Connection;
using FishNet.Managing;
using NUnit.Framework;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ResourceProcessorPlayModeTests
    {
        private NetworkManager manager;
        private NetworkResourceService service;
        private NetworkUpgradeService upgrades;
        private NetworkConnection connection;
        private ServerPlayerResourceStore store;
        private ResourceProcessor processor;
        private PlayerInteractionRaycaster raycaster;
        private PlayerControlStateMachine controls;
        private GameObject player;
        private Scene tower;
        private UpgradeTreeConfig testConfig;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Load the authored network services only through the Play Mode test runner.
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/_Project/Scenes/Tower.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            tower = SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            manager = Object.FindFirstObjectByType<NetworkManager>();
            service = NetworkResourceService.Instance;
            upgrades = NetworkUpgradeService.Instance;
            Assert.That(manager, Is.Not.Null);
            Assert.That(service, Is.Not.Null);
            Assert.That(manager.ServerManager.StartConnection(), Is.True);
            Assert.That(manager.ClientManager.StartConnection("localhost"), Is.True);
            yield return WaitFor(() => service.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized
                && upgrades.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized,
                "The host network services did not initialize.");
            connection = manager.ServerManager.Clients[manager.ClientManager.Connection.ClientId];
            store = Get<ServerPlayerResourceStore>(service, "serverResources");
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Processor/StoneProcessor.prefab");
            processor = Object.Instantiate(prefab).GetComponent<ResourceProcessor>();
            processor.transform.position = new Vector3(0, 1000, 0);
            Set(processor, "interactionDuration", 0.3f);
            player = new GameObject("Processor test player");
            player.transform.position = processor.transform.position;
            controls = player.AddComponent<PlayerControlStateMachine>();
            controls.SetConnected(true);
            raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            Seed(ResourceType.Stone, 10);
            Seed(ResourceType.ProcessedStone, 0);
            Invoke(service, "RebuildNodeLookup");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (raycaster != null)
                raycaster.CancelCurrentInteraction();
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
            }
            if (player != null) Object.Destroy(player);
            if (processor != null) Object.Destroy(processor.gameObject);
            if (testConfig != null) Object.Destroy(testConfig);
            if (manager != null) Object.Destroy(manager.gameObject);
            yield return null;
            if (tower.IsValid() && tower.isLoaded)
                yield return SceneManager.UnloadSceneAsync(tower);
        }

        [UnityTest]
        public IEnumerator HostProcessing_ExchangesOnCompletion_UpdatesWalletAndGrantsProcessExperience()
        {
            int completed = 0;
            processor.ServerCompleted += () => completed++;
            Begin();
            Assert.That(controls.CurrentState, Is.EqualTo(PlayerControlState.Interacting));
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.Stone), Is.EqualTo(10));
            Assert.That(service.GetLocalAmount(ResourceType.ProcessedStone), Is.Zero);
            yield return WaitFor(() => completed == 1, "Processor did not complete through the host RPC flow.");
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.Stone), Is.EqualTo(9));
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.ProcessedStone), Is.EqualTo(1));
            Assert.That(service.GetLocalAmount(ResourceType.Stone), Is.EqualTo(9));
            Assert.That(service.GetLocalAmount(ResourceType.ProcessedStone), Is.EqualTo(1));
            Assert.That(controls.CurrentState, Is.EqualTo(PlayerControlState.Moving));
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Process).Experience, Is.EqualTo(1));
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Gather).Experience, Is.Zero);
            Assert.That(processor.CanInteract, Is.True, "Processors have no shared cooldown.");
            yield return new WaitForSeconds(0.1f);
            Assert.That(completed, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Cancellation_DisablingAndDisconnecting_DoNotConsumeOrGrantExperience()
        {
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Server did not start processing.");
            raycaster.CancelCurrentInteraction();
            yield return WaitFor(() => ActiveCount == 0, "Cancellation did not reach the server.");
            AssertUnchanged();

            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Server did not restart processing.");
            processor.enabled = false;
            yield return WaitFor(() => ActiveCount == 0, "Disabled processor kept working.");
            yield return null;
            AssertUnchanged();
            processor.enabled = true;

            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Server did not restart processing after disabling.");
            controls.SetConnected(false);
            yield return WaitFor(() => ActiveCount == 0, "Disconnected interaction was not cancelled.");
            AssertUnchanged();
            Assert.That(controls.CurrentState, Is.EqualTo(PlayerControlState.Locked));
        }

        [UnityTest]
        public IEnumerator MissingMaterialsAndFullOutput_RejectStartAndRecheckAtCompletion()
        {
            Seed(ResourceType.Stone, 0);
            Focus();
            Assert.That(processor.CanInteract, Is.False);
            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.False);

            // Stale client inventory must not bypass server validation.
            Get<PlayerResourceWallet>(service, "localWallet").SetAuthoritativeAmount(ResourceType.Stone, 10);
            int rejected = 0;
            processor.ServerRejected += () => rejected++;
            Begin();
            yield return WaitFor(() => rejected == 1, "Server accepted missing source materials.");
            Assert.That(ActiveCount, Is.Zero);

            Seed(ResourceType.Stone, 10);
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Processing did not start.");
            store.TryConsume(connection.ClientId, ResourceType.Stone, 10, out _);
            yield return WaitFor(() => rejected == 2, "Completion did not recheck source materials.");
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.ProcessedStone), Is.Zero);

            Seed(ResourceType.Stone, 10);
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Processing did not restart.");
            store.TryAdd(connection.ClientId, ResourceType.ProcessedStone, 50, out _);
            yield return WaitFor(() => rejected == 3, "Completion did not recheck target capacity.");
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.Stone), Is.EqualTo(10));
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.ProcessedStone), Is.EqualTo(50));
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Process).Experience, Is.Zero);
            Seed(ResourceType.ProcessedStone, 50);
            Assert.That(processor.CanInteract, Is.False);
        }

        [UnityTest]
        public IEnumerator ServerProcessing_AppliesProcessUpgradesAndSnapshotsRecipeAtStart()
        {
            testConfig = ScriptableObject.CreateInstance<UpgradeTreeConfig>();
            Set(testConfig, "boards", new List<UpgradeBoardData>
            {
                new(UpgradeJob.Process, new List<UpgradeData>
                {
                    new("speed", "Speed", 3, 3, UpgradeEffectType.Efficiency, 0.5f),
                    new("cost", "Cost", 3, 4, UpgradeEffectType.Cost, -2),
                    new("output", "Output", 3, 5, UpgradeEffectType.Production, 3)
                })
            });
            Set(upgrades, "config", testConfig);
            UpgradeJobSnapshot snapshot = new(UpgradeJob.Process, 5, 0, 0, new[] { "speed", "cost", "output" });
            ((PlayerUpgradeProgress)Invoke(upgrades, "GetOrCreateServerProgress", connection)).Get(UpgradeJob.Process).ApplySnapshot(snapshot);
            Get<PlayerUpgradeProgress>(upgrades, "localProgress").Get(UpgradeJob.Process).ApplySnapshot(snapshot);
            Set(processor, "sourceAmount", 4);
            Set(processor, "interactionDuration", 0.8f);
            int completed = 0;
            processor.ServerCompleted += () => completed++;
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Upgraded processing did not start.");
            object active = Get<IDictionary>(service, "activeProcesses")[connection.ClientId];
            Assert.That((float)active.GetType().GetField("Duration").GetValue(active), Is.EqualTo(0.3f).Within(0.001f));
            // Changes during an action must not change the accepted exchange.
            Set(processor, "sourceAmount", 9);
            Set(processor, "targetAmount", 9);
            yield return WaitFor(() => completed == 1, "Upgraded processing did not finish.");
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.Stone), Is.EqualTo(8));
            Assert.That(service.GetLocalAmount(ResourceType.ProcessedStone), Is.EqualTo(4));
        }

        [UnityTest]
        public IEnumerator DelayedReply_DoesNotInterruptANewerRequestOnTheSameProcessor()
        {
            Set(processor, "interactionDuration", 0.5f);
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Initial processing did not start.");
            uint oldRequest = Get<uint>(service, "localProcessRequestId");
            raycaster.CancelCurrentInteraction();
            yield return WaitFor(() => ActiveCount == 0, "Initial processing did not cancel.");
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Next processing did not start.");
            Invoke(service, "HandleProcessFinished", processor.ProcessorId, oldRequest, false);
            Invoke(service, "HandleProcessFinished", processor.ProcessorId, oldRequest, true);
            Assert.That(controls.CurrentState, Is.EqualTo(PlayerControlState.Interacting));
            Assert.That(Get<ResourceProcessor>(service, "localActiveProcessor"), Is.SameAs(processor));
            yield return WaitFor(() => service.GetLocalAmount(ResourceType.ProcessedStone) == 1,
                "A stale reply interrupted the current action.");
            Assert.That(controls.CurrentState, Is.EqualTo(PlayerControlState.Moving));
        }

        [UnityTest]
        public IEnumerator SharedProcessor_AcceptsIndependentPlayersAndCancelsOnlyTheRequestedPlayer()
        {
            // Exercise two server connection identities on one processor; the host uses the full RPC path.
            NetworkConnection second = new() { ClientId = 12345 };
            store.TryAdd(second.ClientId, ResourceType.Stone, 5, out _);
            Set(processor, "interactionDuration", 0.5f);
            Begin();
            yield return WaitFor(() => ActiveCount == 1, "Host processing did not start.");
            Assert.That((bool)Invoke(service, "TryStartServerProcess", second, processor, processor.transform.position, 1u), Is.True);
            Assert.That(ActiveCount, Is.EqualTo(2));
            Assert.That((bool)Invoke(service, "TryStartServerProcess", second, processor, processor.transform.position, 2u), Is.False);
            Invoke(service, "CancelServerProcess", second.ClientId);
            Assert.That(ActiveCount, Is.EqualTo(1));
            yield return WaitFor(() => service.GetLocalAmount(ResourceType.ProcessedStone) == 1, "Cancelling another player cancelled the host.");
            Assert.That(store.GetAmount(second.ClientId, ResourceType.Stone), Is.EqualTo(5));
            Assert.That(store.GetAmount(second.ClientId, ResourceType.ProcessedStone), Is.Zero);
        }

        private int ActiveCount => Get<IDictionary>(service, "activeProcesses").Count;
        private void Seed(ResourceType type, int amount)
        {
            int current = store.GetAmount(connection.ClientId, type);
            if (current > 0) store.TryConsume(connection.ClientId, type, current, out _);
            if (amount > 0) store.TryAdd(connection.ClientId, type, amount, out _);
            Get<PlayerResourceWallet>(service, "localWallet").SetAuthoritativeAmount(type, amount);
        }
        private void Focus() => Invoke(raycaster, "SetTarget", processor.GetComponentInChildren<Collider>().gameObject);
        private void Begin() { Focus(); Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True); }
        private void AssertUnchanged()
        {
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.Stone), Is.EqualTo(10));
            Assert.That(store.GetAmount(connection.ClientId, ResourceType.ProcessedStone), Is.Zero);
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Process).Experience, Is.Zero);
        }
        private static IEnumerator WaitFor(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 10f;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static T Get<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    }
}
#endif
