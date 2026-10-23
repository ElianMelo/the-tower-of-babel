using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class TowerProgressionEditModeTests
    {
        private GameObject root;
        private ChunkManager manager;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Progression test");
            root.SetActive(false);
            manager = root.AddComponent<ChunkManager>();
            var tower = new GameObject("Authoring");
            tower.transform.SetParent(root.transform);
            AddAsset(tower.transform, Vector3.zero, TowerAssetType.Floor);
            AddAsset(tower.transform, new Vector3(64, 0, 0), TowerAssetType.Arch);
            AddAsset(tower.transform, new Vector3(0, 6, 0), TowerAssetType.Stair);
            AddAsset(tower.transform, new Vector3(0, 12, 0), TowerAssetType.Pillar);
            typeof(ChunkManager).GetField("towerRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, tower);
            manager.CacheChunks();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Floors_UnlockOnlyAfterEveryAssetAcrossAllChunksIsComplete()
        {
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            Assert.That(manager.TryAdvanceAssetStage(new ChunkKey(1, 0, 0), 0, out _), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 9);
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            Assert.That(manager.CanConstructAsset(new ChunkKey(1, 0, 0), 0), Is.False);
            Assert.That(manager.TryAdvanceAssetStage(new ChunkKey(0, 2, 0), 0, out _), Is.True);
            Assert.That(manager.ActiveConstructionFloor, Is.EqualTo(1));
            Assert.That(manager.CanConstructAsset(new ChunkKey(1, 0, 0), 0), Is.True);
            Assert.That(manager.CanConstructAsset(new ChunkKey(2, 0, 0), 0), Is.False);
        }

        [Test]
        public void SnapshotAndRollback_RecomputeTheFirstUnfinishedFloor()
        {
            int changes = 0;
            manager.ConstructionFloorChanged += () => changes++;
            manager.SetAssetStage(new ChunkKey(2, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 10);
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10);
            Assert.That(manager.ActiveConstructionFloor, Is.Null);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            Assert.That(changes, Is.EqualTo(2));
            Assert.That(manager.CanConstructAsset(new ChunkKey(0, 0, 0), 999), Is.False);
        }

        [Test]
        public void Visibility_HidesLaterFloorsEvenWhenSnapshotsContainBuiltAssets()
        {
            Assert.That(manager.IsFloorVisible(0), Is.True);
            Assert.That(manager.IsFloorVisible(1), Is.False);
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 5);
            manager.SetAssetStage(new ChunkKey(2, 0, 0), 0, 10);
            Assert.That(manager.IsFloorVisible(1), Is.False);
            Assert.That(manager.IsFloorVisible(2), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 9);
            Assert.That(manager.IsFloorVisible(1), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10);
            Assert.That(manager.IsFloorVisible(0), Is.True);
            Assert.That(manager.IsFloorVisible(1), Is.True);
            Assert.That(manager.IsFloorVisible(2), Is.False);
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 10);
            Assert.That(manager.ActiveConstructionFloor, Is.Null);
            Assert.That(manager.IsFloorVisible(2), Is.True);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            Assert.That(manager.IsFloorVisible(1), Is.False);
            Assert.That(manager.IsFloorVisible(2), Is.False);
        }

        [Test]
        public void Visibility_KeepsLockedChunksOutOfBothNearAndFarRendering()
        {
            var pool = root.AddComponent<NearTowerAssetPool>();
            var far = root.AddComponent<RecordingFarChunkRenderer>();
            var prefab = new GameObject("Visibility model");
            prefab.transform.SetParent(root.transform);
            prefab.SetActive(false);
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                manager.AssetPrefabs.SetPrefab(type, prefab);
            var player = new GameObject("Visibility player");
            player.transform.SetParent(root.transform);
            typeof(ChunkManager).GetField("player", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(manager, player.transform);

            manager.SetAssetStage(new ChunkKey(2, 0, 0), 0, 5);
            manager.RefreshTowerVisibility();
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(manager.IsChunkLoaded(new ChunkKey(1, 0, 0)), Is.False);
            Assert.That(far.LoadedChunks, Has.No.Member(new ChunkKey(2, 0, 0)));
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(2));
            Assert.That(manager.IsChunkLoaded(new ChunkKey(1, 0, 0)), Is.True);
            Assert.That(far.LoadedChunks, Has.No.Member(new ChunkKey(2, 0, 0)));
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 10);
            Assert.That(far.LoadedChunks, Has.Member(new ChunkKey(2, 0, 0)));
            player.transform.position = Vector3.up * 12;
            manager.RefreshTowerVisibility();
            Assert.That(manager.IsChunkLoaded(new ChunkKey(2, 0, 0)), Is.True);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            Assert.That(pool.ActiveInstanceCount, Is.Zero);
            Assert.That(manager.IsChunkLoaded(new ChunkKey(2, 0, 0)), Is.False);
            Assert.That(far.LoadedChunks, Has.No.Member(new ChunkKey(2, 0, 0)));
        }

        [Test]
        public void Bushes_AppearOnlyOnCompletedAndUnlockedFloors()
        {
            var scatter = root.AddComponent<TowerBushScatter>();
            typeof(TowerBushScatter).GetField("chunkManager", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(scatter, manager);
            Assert.That(scatter.CanShowFloor(0), Is.False);
            Assert.That(scatter.CanShowFloor(1), Is.False);
            Assert.That(manager.IsFloorComplete(999), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 9);
            Assert.That(scatter.CanShowFloor(0), Is.False, "Every asset across the floor must be solid.");
            manager.SetAssetStage(new ChunkKey(1, 0, 0), 0, 10);
            Assert.That(scatter.CanShowFloor(1), Is.False, "Completed snapshots must not reveal locked floors.");
            manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10);
            Assert.That(scatter.CanShowFloor(0), Is.True);
            Assert.That(scatter.CanShowFloor(1), Is.True);
            Assert.That(scatter.CanShowFloor(2), Is.False);
            manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 9);
            Assert.That(scatter.CanShowFloor(0), Is.False);
            Assert.That(scatter.CanShowFloor(1), Is.False);
        }

        [Test]
        public void BoundBuildings_RefreshTheirPresentationWhenTheFloorUnlocks()
        {
            var model = new GameObject("Upper floor model");
            model.transform.SetParent(root.transform);
            // Keep the presentation active while the data owner stays inactive.
            model.transform.SetParent(null);
            var building = model.AddComponent<Building>();
            try
            {
                building.Bind(manager, new ChunkKey(1, 0, 0), 0, 0);
                Assert.That(building.ShouldShowInteraction, Is.False);
                Assert.That(building.CanInteract, Is.False);
                int changes = 0;
                building.InteractionPresentationChanged += () => changes++;
                manager.SetAssetStage(new ChunkKey(0, 0, 0), 0, 10);
                manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10);
                Assert.That(building.ShouldShowInteraction, Is.True);
                Assert.That(changes, Is.EqualTo(1));
                building.Unbind();
                Assert.That(building.ShouldShowInteraction, Is.False);
            }
            finally { Object.DestroyImmediate(model); }
        }

        private static void AddAsset(Transform parent, Vector3 position, TowerAssetType type)
        {
            var asset = new GameObject(type.ToString());
            asset.transform.SetParent(parent);
            asset.transform.position = position;
            asset.AddComponent<TowerAsset>().SetAssetType(type);
        }
    }
}
