using System;
using System.Collections;
using System.Linq;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources;
using TowerOfBabel.Upgrades;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class CheatMenuPlayModeTests
    {
        private CheatMenuUI menu;
        private NetworkUpgradeService upgrades;
        private NetworkResourceService resources;
        private Keyboard keyboard, previousKeyboard;
        private UnityEngine.SceneManagement.Scene tower;
        private FishNet.Managing.NetworkManager manager;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1;
#if UNITY_EDITOR
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
#endif
            yield return null;
            menu = UnityEngine.Object.FindFirstObjectByType<CheatMenuUI>();
            Assert.That(menu, Is.Not.Null);
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
            manager = UnityEngine.Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
            yield return Wait(() => InstanceFinder.IsClientStarted && NetworkUpgradeService.Instance != null
                && NetworkUpgradeService.Instance.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized
                && NetworkResourceService.Instance.GetComponent<FishNet.Object.NetworkObject>().IsClientInitialized, "Host did not connect");
            upgrades = NetworkUpgradeService.Instance; resources = NetworkResourceService.Instance;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            if (menu != null) menu.Hide();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            keyboard = previousKeyboard = null;
            if (manager != null)
            {
                manager.ClientManager.StopConnection();
                manager.ServerManager.StopConnection(true);
                UnityEngine.Object.Destroy(manager.gameObject);
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }

        [UnityTest]
        public IEnumerator F3AndEscape_ToggleTheMenuWhilePausedAndRestoreCursorAndControls()
        {
            previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(menu.IsVisible, Is.True);
            Assert.That(Cursor.visible, Is.True); Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            var controls = UnityEngine.Object.FindFirstObjectByType<PlayerControlStateMachine>();
            Assert.That(controls.CanScanConstruction, Is.False);
            Time.timeScale = 0;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(menu.IsVisible, Is.False);
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.Locked));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F3)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(menu.IsVisible, Is.True);
            menu.SelectCategory("Stats"); menu.Search("construction");
            Assert.That(menu.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true).Length, Is.EqualTo(1));
            menu.Hide(); Assert.That(controls.CanScanConstruction, Is.True);
        }

        [UnityTest]
        public IEnumerator MenuActions_UpdateServerProgressCapacityResourcesAndConstruction()
        {
            var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            menu.Show(); menu.SetAmount(1000);
            menu.ActionButtons["Level up"].onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalSnapshot(UpgradeJob.Gather).Level == 1, "Level-up was not confirmed");
            menu.ActionButtons["Gain XP"].onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalSnapshot(UpgradeJob.Gather).Level > 1, "XP was not confirmed");
            menu.ActionButtons["Increase bag capacity"].onClick.Invoke();
            yield return Wait(() => resources.LocalCapacity == 1050, "Bag capacity was not confirmed");
            menu.SelectCategory("Inventory"); menu.ActionButtons["Fill bag"].onClick.Invoke();
            yield return Wait(() => resources.GetLocalAmount(ResourceType.Stone) == 1050 && resources.GetLocalAmount(ResourceType.ProcessedStone) == 1050, "Bag did not fill");
            menu.ActionButtons["Clear bag"].onClick.Invoke();
            yield return Wait(() => resources.GetLocalAmount(ResourceType.Stone) == 0 && resources.GetLocalAmount(ResourceType.ProcessedStone) == 0, "Bag did not clear");
            menu.SetAmount(10); menu.ActionButtons["Gain sand"].onClick.Invoke(); menu.ActionButtons["Gain glass"].onClick.Invoke();
            yield return Wait(() => resources.GetLocalAmount(ResourceType.Sand) == 10 && resources.GetLocalAmount(ResourceType.Glass) == 10, "New resource cheats failed");
            Assert.That(upgrades.RequestCheatStat(CheatStat.BuildSpeed, 10), Is.True);
            Assert.That(upgrades.RequestCheatStat(CheatStat.GatherProduction, 5), Is.True);
            Assert.That(upgrades.RequestCheatStat(CheatStat.WalkSpeed, 3), Is.True);
            Assert.That(upgrades.RequestCheatStat(CheatStat.CameraFov, 90), Is.True);
            yield return Wait(() => upgrades.GetLocalCheatStat(CheatStat.BuildSpeed) == 10 && upgrades.GetLocalCheatStat(CheatStat.WalkSpeed) == 3, "Stats did not synchronize");
            yield return Wait(() => Mathf.Abs(Camera.main.fieldOfView - 90) < .1f, "Cinemachine overrode the field-of-view cheat");
            Assert.That(upgrades.GetLocalActionDuration(UpgradeJob.Build, 3), Is.EqualTo(.3f).Within(.001f));
            Assert.That(upgrades.GetServerActionDuration(InstanceFinder.ClientManager.Connection, UpgradeJob.Build, 3), Is.EqualTo(.3f).Within(.001f));
            var node = UnityEngine.Object.FindObjectsByType<Resource>(FindObjectsSortMode.None).First(n => n.Definition != null && n.Definition.ResourceType == ResourceType.Stone && n.NodeId != 0);
            upgrades.RequestCheatCommand(CheatCommand.InstantGather, node: node.NodeId);
            yield return Wait(() => resources.GetLocalAmount(ResourceType.Stone) >= 5, "Instant gather failed");
            var processor = UnityEngine.Object.FindObjectsByType<ResourceProcessor>(FindObjectsSortMode.None).First(p => p.SourceResource.ResourceType == ResourceType.Stone);
            upgrades.RequestCheatCommand(CheatCommand.InstantProcess, processor: processor.ProcessorId);
            yield return Wait(() => resources.GetLocalAmount(ResourceType.ProcessedStone) >= 1, "Instant process failed");
            var first = chunks.CachedChunks.First(c => c.Key.FloorIndex == 0);
            upgrades.RequestCheatCommand(CheatCommand.BuildAsset, key: first.Key, localIndex: 0);
            yield return Wait(() => chunks.TryGetAsset(first.Key, 0, out var asset) && asset.Stage == 10, "Instant asset failed");
            menu.SelectCategory("Construction"); menu.SetFloor(1); menu.ActionButtons["Instantly build floor"].onClick.Invoke();
            yield return Wait(() => chunks.IsFloorComplete(0), "Instant floor failed");
            Assert.That(chunks.ActiveConstructionFloor, Is.EqualTo(1));
            menu.ActionButtons["Reset floor"].onClick.Invoke();
            yield return Wait(() => chunks.ActiveConstructionFloor == 0 && !chunks.IsFloorComplete(0), "Floor reset failed");
            Assert.That(chunks.IsFloorVisible(1), Is.False);
            menu.SelectCategory("Progression"); menu.ActionButtons["Unlock all upgrades"].onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalSnapshot(UpgradeJob.Build).Level == 50, "Unlock all failed");
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Build).PurchasedUpgradeIds.Length, Is.EqualTo(upgrades.Config.GetUpgrades(UpgradeJob.Build).Count));
            menu.SelectCategory("Stats"); menu.ActionButtons["Reset every stat"].onClick.Invoke();
            yield return Wait(() => resources.LocalCapacity == 50 && upgrades.GetLocalCheatStat(CheatStat.WalkSpeed) == 1, "Stat reset failed");
            menu.SelectCategory("Progression"); menu.ActionButtons["Reset all progression"].onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalSnapshot(UpgradeJob.Build).Level == 0 && upgrades.GetLocalSnapshot(UpgradeJob.Gather).Level == 0, "Progression reset failed");
        }

        private static IEnumerator Wait(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
