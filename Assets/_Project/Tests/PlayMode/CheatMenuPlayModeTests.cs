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

        [UnityTest]
        public IEnumerator GodMode_FliesThroughCollisionsAndRestoresEveryPreviousColliderState()
        {
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var extra = new GameObject("Previously disabled collider"); extra.transform.SetParent(player.transform, false);
            extra.AddComponent<BoxCollider>().enabled = false;
            var colliders = player.GetComponentsInChildren<Collider>(true); var previous = colliders.Select(c => c.enabled).ToArray();
            var start = player.transform.position;
            menu.Show(); menu.ToggleControls["God mode"].isOn = true;
            Assert.That(menu.IsGodMode, Is.True); Assert.That(player.enabled, Is.False);
            Assert.That(colliders.All(c => !c.enabled), Is.True);
            menu.Hide(); Assert.That(menu.IsGodMode, Is.True);
            previousKeyboard = Keyboard.current; keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space));
            yield return null; yield return null; yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            Assert.That(player.transform.position.y, Is.GreaterThan(start.y + .05f));
            menu.Show(); menu.ToggleControls["God mode"].isOn = false;
            Assert.That(menu.IsGodMode, Is.False); Assert.That(player.enabled, Is.True);
            for (int i = 0; i < colliders.Length; i++) Assert.That(colliders[i].enabled, Is.EqualTo(previous[i]));
            UnityEngine.Object.Destroy(extra);
        }

        [UnityTest]
        public IEnumerator BuildFloor_AlwaysAdvancesTheNextUnfinishedFloorRegardlessOfPlayerOrSelection()
        {
            var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>(); var start = player.transform.position;
            menu.Show(); menu.SetFloor(45);
            menu.ActionButtons["Instantly build floor"].onClick.Invoke();
            yield return Wait(() => chunks.IsFloorComplete(0), "The first unfinished floor did not complete");
            menu.ActionButtons["Instantly build floor"].onClick.Invoke();
            yield return Wait(() => chunks.IsFloorComplete(1), "Repeated action rebuilt the standing floor instead of advancing");
            Assert.That(chunks.ActiveConstructionFloor, Is.EqualTo(2)); Assert.That(chunks.IsFloorComplete(44), Is.False);
            Assert.That(Mathf.Abs(player.transform.position.y - start.y), Is.LessThan(2));
        }

        [UnityTest]
        public IEnumerator AllClasses_AppliesProgressionAndProfessionStatsAndCanReturnToOneClass()
        {
            menu.Show(); menu.SelectAllProfessions(); menu.SetAmount(10);
            menu.ActionButtons["Level up"].onClick.Invoke();
            yield return Wait(() => Enum.GetValues(typeof(UpgradeJob)).Cast<UpgradeJob>().All(j => upgrades.GetLocalSnapshot(j).Level == 1), "ALL did not level every class");
            menu.ActionButtons["Gain XP"].onClick.Invoke();
            yield return Wait(() => Enum.GetValues(typeof(UpgradeJob)).Cast<UpgradeJob>().All(j => upgrades.GetLocalSnapshot(j).Experience > 0 || upgrades.GetLocalSnapshot(j).Level > 1), "ALL did not give XP to every class");
            menu.SelectCategory("Stats"); menu.Search("Construction speed");
            menu.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.GetComponentInChildren<TMPro.TMP_Text>().text == "5x").onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalCheatStat(CheatStat.GatherSpeed) == 5 && upgrades.GetLocalCheatStat(CheatStat.ProcessSpeed) == 5 && upgrades.GetLocalCheatStat(CheatStat.BuildSpeed) == 5, "ALL did not apply action speed across classes");
            menu.SelectCategory("Progression"); menu.ActionButtons["Reset selected profession"].onClick.Invoke();
            yield return Wait(() => Enum.GetValues(typeof(UpgradeJob)).Cast<UpgradeJob>().All(j => upgrades.GetLocalSnapshot(j).Level == 0), "ALL reset did not reset every class");
            menu.SelectProfession(UpgradeJob.Build); menu.ActionButtons["Level up"].onClick.Invoke();
            yield return Wait(() => upgrades.GetLocalSnapshot(UpgradeJob.Build).Level == 1, "Single-class selection failed");
            Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Gather).Level, Is.Zero); Assert.That(upgrades.GetLocalSnapshot(UpgradeJob.Process).Level, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InstantBuildToggle_OnlyBuildsOnInteractionCompletesAllStagesAndChargesNothing()
        {
            var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
            var pool = UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.NearTowerAssetPool>();
            var player = UnityEngine.Object.FindFirstObjectByType<PlayerController>();
            var raycaster = player.GetComponent<TowerOfBabel.Resources.Interaction.PlayerInteractionRaycaster>();
            menu.Show(); menu.ToggleControls["Instantly build asset"].isOn = true;
            yield return Wait(() => upgrades.GetLocalCheatStat(CheatStat.InstantBuild) == 1, "Instant-build toggle did not reach the server");
            yield return Wait(() => Enum.GetValues(typeof(ResourceType)).Cast<ResourceType>().All(type => resources.GetLocalAmount(type) == resources.LocalCapacity), "Instant build did not fill every resource to bag capacity");
            Assert.That(chunks.CachedChunks.SelectMany(c => c.Assets).All(a => a.Stage == 0), Is.True, "Enabling the toggle must not build the captured target");
            var candidate = chunks.CachedChunks.Where(c => c.Key.FloorIndex == 0)
                .SelectMany(c => c.Assets.Select(a => (key: c.Key, asset: a))).Where(p => p.asset.AssetType == TowerOfBabel.World.Tower.TowerAssetType.Pillar)
                .OrderBy(p => (p.asset.Position - player.transform.position).sqrMagnitude).First();
            var body = player.GetComponent<CharacterController>(); body.enabled = false;
            player.transform.position = candidate.asset.Position + Vector3.forward * 2 + Vector3.up; body.enabled = true;
            chunks.RefreshTowerVisibility();
            var building = pool.ActiveAssets.Select(a => a.GetComponent<TowerOfBabel.Buildings.Building>()).First(b => b != null && b.ChunkKey == candidate.key && b.LocalIndex == candidate.asset.LocalIndex);
            Assert.That(building.EffectiveResourceCost, Is.Zero); Assert.That(building.Duration, Is.LessThan(.02f)); Assert.That(building.CanInteract, Is.True);
            int balance = resources.GetLocalAmount(building.ResourceType);
            menu.Hide();
            typeof(TowerOfBabel.Resources.Interaction.PlayerInteractionRaycaster).GetMethod("SetTarget", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(raycaster, new object[] { building.gameObject });
            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True);
            yield return Wait(() => chunks.TryGetAsset(candidate.key, candidate.asset.LocalIndex, out var a) && a.Stage == 10, "Interacting did not finish the asset");
            Assert.That(resources.GetLocalAmount(building.ResourceType), Is.EqualTo(balance));
            Assert.That(chunks.CachedChunks.Where(c => c.Key.FloorIndex == 1).SelectMany(c => c.Assets).All(a => a.Stage == 0), Is.True);
            menu.Show(); menu.ToggleControls["Instantly build asset"].isOn = false;
            yield return Wait(() => upgrades.GetLocalCheatStat(CheatStat.InstantBuild) == 0, "Toggle did not disable");
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                Assert.That(resources.GetLocalAmount(type), Is.EqualTo(resources.LocalCapacity), "Disabling instant build must retain granted resources");
        }

        private static IEnumerator Wait(Func<bool> condition, string message)
        {
            float deadline = Time.realtimeSinceStartup + 12;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(condition(), Is.True, message);
        }
    }
}
