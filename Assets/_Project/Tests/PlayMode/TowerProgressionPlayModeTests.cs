using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.World.Tests
{
    public sealed class TowerProgressionPlayModeTests
    {
        private readonly List<GameObject> objects = new();
        private ChunkManager manager;
        private NearTowerAssetPool pool;
        private PlayerInteractionRaycaster raycaster;
        private PlayerControlStateMachine controls;
        private Building nearby;
        private TowerOfBabel.Outline nearbyOutline;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            var owner = Create("Progression owner");
            owner.SetActive(false);
            manager = owner.AddComponent<ChunkManager>();
            manager.enabled = false;
            pool = owner.AddComponent<NearTowerAssetPool>();
            SetField(manager, "nearAssetPool", pool);
            // Supply the same serialized cache used by the scene, without invoking authoring in Play Mode.
            SetField(manager, "cachedChunks", new List<ChunkSceneCache> {
                CreateChunk(new ChunkKey(0, 0, 0), Vector3.right * 2, Vector3.right * 25),
                CreateChunk(new ChunkKey(1, 0, 0), Vector3.up * 6)
            });

            var prefab = Create("Building model");
            prefab.SetActive(false);
            prefab.AddComponent<MeshRenderer>();
            prefab.AddComponent<MeshFilter>();
            prefab.AddComponent<Building>();
            var outline = prefab.AddComponent<TowerOfBabel.Outline>();
#if UNITY_EDITOR
            SetField(outline, "settings", UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/_Project/Scripts/Buildings/OutlineSettings.asset"));
#endif
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                manager.AssetPrefabs.SetPrefab(type, prefab);
            owner.SetActive(true);
            pool.Configure(manager.AssetPrefabs, manager);

            var player = Create("Scan player");
            controls = player.AddComponent<PlayerControlStateMachine>();
            controls.SetConnected(true);
            raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            SetField(raycaster, "constructionChunks", manager);
            SetField(manager, "player", player.transform);
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            manager.RefreshTowerVisibility();
            nearby = pool.ActiveAssets.First(a => a.transform.position == Vector3.right * 2).GetComponent<Building>();
            nearbyOutline = nearby.GetComponent<TowerOfBabel.Outline>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BushRendering_WaitsForEveryAssetOnItsFloorAndHidesAfterRollback()
        {
#if UNITY_EDITOR
            var scatter = manager.gameObject.AddComponent<TowerBushScatter>();
            var bush = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/Prefabs/Foliage/Bush_Small.prefab");
            Assert.That(bush, Is.Not.Null);
            SetField(scatter, "bushPrefabs", new[] { bush });
            SetField(scatter, "placements", new List<TowerBushScatter.Placement> {
                new() { variant = 0, floor = 0, position = Vector3.up * .2f,
                    scale = Vector3.one, footprint = .5f, height = .5f },
                new() { variant = 0, floor = 1, position = Vector3.up * 6.2f,
                    scale = Vector3.one, footprint = .5f, height = .5f }
            });
            scatter.RebuildBatches();
            var camera = Create("Bush render camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 3, -12);
            camera.transform.LookAt(new Vector3(0, 3, 0));
            Assert.That(scatter.BatchCount, Is.EqualTo(2));
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.Zero);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 1, 9);
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.Zero);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 1, 10);
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.EqualTo(1), "Only the completed floor receives bushes.");
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 10);
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.EqualTo(2));
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 9);
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.EqualTo(1));
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            scatter.Draw(camera);
            Assert.That(scatter.DrawnBatchCount, Is.Zero);
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator Floors_RevealOnlyAfterThePreviousFloorIsSolidAndComplete()
        {
            Assert.That(pool.ActiveAssets.Any(a => a.GetComponent<Building>().ChunkKey.FloorIndex == 1), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 1, 9);
            yield return null;
            Assert.That(pool.ActiveAssets.Any(a => a.GetComponent<Building>().ChunkKey.FloorIndex == 1), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 1, 10);
            yield return null;
            Building upper = pool.ActiveAssets.First(a => a.GetComponent<Building>().ChunkKey.FloorIndex == 1).GetComponent<Building>();
            Assert.That(upper.gameObject.activeInHierarchy, Is.True);
            Assert.That(upper.IsConstructionAvailable, Is.True);
            foreach (var asset in pool.ActiveAssets)
            {
                Building building = asset.GetComponent<Building>();
                if (building.ChunkKey.FloorIndex != 0) continue;
                Assert.That(building.CurrentStage, Is.EqualTo(10));
                Assert.That(building.IsComplete, Is.True);
                Assert.That(building.CanInteract, Is.False);
            }
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            yield return null;
            Assert.That(upper.gameObject.activeInHierarchy, Is.False);
            Assert.That(pool.ActiveAssets.Any(a => a.GetComponent<Building>().ChunkKey.FloorIndex == 1), Is.False);
        }

        [UnityTest]
        public IEnumerator Scan_OnlyHighlightsNearbyUnfinishedAssetsOnTheActiveFloorForTwoSeconds()
        {
            Assert.That(raycaster.ScanNearbyConstruction(), Is.True);
            Assert.That(nearbyOutline.IsVisible, Is.True);
            foreach (GameObject asset in pool.ActiveAssets)
                Assert.That(asset.GetComponent<TowerOfBabel.Outline>().IsVisible,
                    Is.EqualTo(asset == nearby.gameObject));
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(nearbyOutline.IsVisible, Is.True);
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(nearbyOutline.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Scan_RetriggerRefreshesExpiryAndHoverRemainsAfterExpiry()
        {
            raycaster.ScanNearbyConstruction();
            yield return new WaitForSecondsRealtime(1.1f);
            raycaster.ScanNearbyConstruction();
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(nearbyOutline.IsVisible, Is.True);
            nearby.SetInteractionFocused(true);
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(nearbyOutline.IsVisible, Is.True);
            nearby.SetInteractionFocused(false);
            Assert.That(nearbyOutline.IsVisible, Is.False);
        }

        [UnityTest]
        public IEnumerator Scan_ClearsOnCompletionPoolingAndDisconnectAndRespectsMenus()
        {
            controls.SetModalInputLocked(true);
            Assert.That(raycaster.ScanNearbyConstruction(), Is.False);
            controls.SetModalInputLocked(false);
            raycaster.ScanNearbyConstruction();
            manager.SetAssetStage(nearby.ChunkKey, nearby.LocalIndex, 10);
            Assert.That(nearbyOutline.IsVisible, Is.False);
            manager.SetAssetStage(nearby.ChunkKey, nearby.LocalIndex, 0);
            raycaster.ScanNearbyConstruction();
            Assert.That(nearbyOutline.IsVisible, Is.True);
            nearby.Unbind();
            nearby.Bind(manager, new ChunkKey(1, 0, 0), 0, 0);
            Assert.That(nearbyOutline.IsVisible, Is.False);
            nearby.Bind(manager, new ChunkKey(0, 0, 0), 0, 0);
            raycaster.ScanNearbyConstruction();
            controls.SetConnected(false);
            yield return null;
            Assert.That(nearbyOutline.IsVisible, Is.False);
            Assert.That(raycaster.ScanNearbyConstruction(), Is.False);
        }

        private GameObject Create(string name)
        {
            var item = new GameObject(name);
            objects.Add(item);
            return item;
        }

        private static ChunkSceneCache CreateChunk(ChunkKey key, params Vector3[] positions)
        {
            var assets = new List<ChunkAssetData>();
            for (int i = 0; i < positions.Length; i++)
                assets.Add(new ChunkAssetData(i, TowerAssetType.Floor, positions[i], Quaternion.identity, Vector3.one));
            return (ChunkSceneCache)System.Activator.CreateInstance(typeof(ChunkSceneCache),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { key, assets }, null);
        }

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
