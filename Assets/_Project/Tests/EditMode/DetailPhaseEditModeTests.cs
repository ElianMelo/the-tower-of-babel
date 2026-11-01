using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    /// <summary>Each floor builds its structure first; the detail (bushes) unlocks after, then the next floor.</summary>
    public sealed class DetailPhaseEditModeTests
    {
        private static readonly ChunkKey Ground = new(0, 0, 0);
        private const int FloorIndex = 0, PillarIndex = 1, BushIndex = 2;

        private GameObject root;
        private ChunkManager manager;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Detail phase test");
            root.SetActive(false);
            manager = root.AddComponent<ChunkManager>();
            var tower = new GameObject("Authoring");
            tower.transform.SetParent(root.transform);
            // Sorted by type inside the chunk: Floor 0, Pillar 1, Bush 2.
            AddAsset(tower.transform, Vector3.zero, TowerAssetType.Floor);
            AddAsset(tower.transform, new Vector3(1, 0, 0), TowerAssetType.Pillar);
            AddAsset(tower.transform, new Vector3(2, .5f, 0), TowerAssetType.Bush);
            AddAsset(tower.transform, new Vector3(0, 6, 0), TowerAssetType.Stair);
            Field("towerRoot").SetValue(manager, tower);
            manager.CacheChunks();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Bush_IsADetailAssetAndEverythingElseIsStructure()
        {
            Assert.That(TowerAssetType.Bush.Phase(), Is.EqualTo(TowerConstructionPhase.Detail));
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                if (type != TowerAssetType.Bush) Assert.That(type.Phase(), Is.EqualTo(TowerConstructionPhase.Structure), type.ToString());
            Assert.That(manager.TryGetAsset(Ground, BushIndex, out var bush) && bush.AssetType == TowerAssetType.Bush);
        }

        [Test]
        public void Detail_UnlocksAfterStructure_AndNextFloorAfterDetail()
        {
            Assert.That(manager.GetFloorPhase(0), Is.EqualTo(TowerConstructionPhase.Structure));
            Assert.That(manager.CanConstructAsset(Ground, BushIndex), Is.False, "Detail is withheld while structure is unfinished.");
            Assert.That(manager.TryAdvanceAssetStage(Ground, BushIndex, out _), Is.False);

            manager.SetAssetStage(Ground, FloorIndex, 10);
            Assert.That(manager.CanConstructAsset(Ground, BushIndex), Is.False);
            manager.SetAssetStage(Ground, PillarIndex, 10);

            Assert.That(manager.IsStructureComplete(0), Is.True);
            Assert.That(manager.GetFloorPhase(0), Is.EqualTo(TowerConstructionPhase.Detail));
            Assert.That(manager.CanConstructAsset(Ground, BushIndex), Is.True);
            Assert.That(manager.IsFloorComplete(0), Is.False, "The floor is not done until its detail is.");
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            Assert.That(manager.IsFloorVisible(1), Is.False);

            for (int i = 0; i < 10; i++) Assert.That(manager.TryAdvanceAssetStage(Ground, BushIndex, out _), Is.True);
            Assert.That(manager.IsFloorComplete(0), Is.True);
            Assert.That(manager.ActiveConstructionFloor, Is.EqualTo(1));
            Assert.That(manager.IsFloorVisible(1), Is.True);
        }

        [Test]
        public void DetailRelease_RaisesOnceEachWay_AndRollbackWithdrawsDetail()
        {
            var releases = new List<int>();
            manager.DetailReleaseChanged += releases.Add;
            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(releases, Is.EqualTo(new[] { 0 }));

            manager.SetAssetStage(Ground, BushIndex, 4);
            manager.SetAssetStage(Ground, PillarIndex, 9);
            Assert.That(releases, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(manager.CanConstructAsset(Ground, BushIndex), Is.False);
            Assert.That(manager.GetFloorPhase(0), Is.EqualTo(TowerConstructionPhase.Structure));
        }

        [Test]
        public void SetConstructionStage_ReleasesDetailForWholeFloorSnapshots()
        {
            var releases = new List<int>();
            manager.DetailReleaseChanged += releases.Add;
            manager.SetConstructionStage(0, 10);
            Assert.That(releases, Is.EqualTo(new[] { 0 }));
            Assert.That(manager.IsFloorComplete(0), Is.True);
            manager.SetConstructionStage(0, 0);
            Assert.That(releases, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(manager.CanConstructAsset(Ground, BushIndex), Is.False);
        }

        [Test]
        public void NearPool_ShowsDetailOnlyAfterRelease_AndHidesItAgainOnRollback()
        {
            var pool = root.AddComponent<NearTowerAssetPool>();
            root.AddComponent<RecordingFarChunkRenderer>();
            var prefab = new GameObject("Detail model");
            prefab.transform.SetParent(root.transform);
            prefab.SetActive(false);
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                manager.AssetPrefabs.SetPrefab(type, prefab);
            var player = new GameObject("Detail player");
            player.transform.SetParent(root.transform);
            Field("player").SetValue(manager, player.transform);

            manager.RefreshTowerVisibility();
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(2), "Floor and pillar only; the bush is withheld.");
            Assert.That(pool.GetCapacity(TowerAssetType.Bush), Is.Zero);

            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(3), "Structure complete releases the bush.");
            Assert.That(pool.ActiveAssets.Count(a => a.GetComponent<TowerAsset>().AssetType == TowerAssetType.Bush), Is.EqualTo(1));

            manager.SetAssetStage(Ground, PillarIndex, 9);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(2));

            manager.SetAssetStage(Ground, PillarIndex, 10);
            manager.SetAssetStage(Ground, BushIndex, 10);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(4), "Completing the detail unlocks the next floor.");
        }

        [Test]
        public void FarSnapshots_LeaveOutBuiltDetailUntilReleased()
        {
            var far = root.AddComponent<SnapshotRecorder>();
            var player = new GameObject("Far player");
            player.transform.SetParent(root.transform);
            player.transform.position = new Vector3(500, 0, 500);
            Field("player").SetValue(manager, player.transform);
            manager.RefreshTowerVisibility();

            manager.SetAssetStage(Ground, BushIndex, 6);
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Bush), Is.False);
            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Bush), Is.True);
        }

        [Test]
        public void BoundBush_RefreshesItsPromptWhenDetailIsReleased()
        {
            var model = new GameObject("Bush model");
            var building = model.AddComponent<Building>();
            try
            {
                building.Bind(manager, Ground, BushIndex, 0);
                Assert.That(building.ShouldShowInteraction, Is.False);
                int changes = 0;
                building.InteractionPresentationChanged += () => changes++;
                manager.SetAssetStage(Ground, FloorIndex, 10);
                manager.SetAssetStage(Ground, PillarIndex, 10);
                Assert.That(building.ShouldShowInteraction, Is.True);
                Assert.That(changes, Is.GreaterThanOrEqualTo(1));
            }
            finally { Object.DestroyImmediate(model); }
        }

        [Test]
        public void Generator_PlacesBushesBesidePillars_OnTheDeck_Deterministically()
        {
            var setup = CreateGeneratorScene(out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                var bushes = generator.BuildDetailBushes(assets);
                Assert.That(bushes.Count, Is.EqualTo(pillars.Length / generator.detailBushPillarInterval));
                foreach (var bush in bushes)
                {
                    Assert.That(bush.AssetType, Is.EqualTo(TowerAssetType.Bush));
                    var point = new Vector2(bush.Position.x, bush.Position.z);
                    float nearest = pillars.Min(p => Vector2.Distance(point, new Vector2(p.x, p.z)));
                    Assert.That(nearest, Is.InRange(generator.PillarSupportRadius, 3f), "Next to a pillar, never inside it.");
                    // Floor deck surface is y = 0.2 + 0.5 * 0.2; the cube's base sits on it.
                    Assert.That(bush.Position.y - .5f * bush.Scale.y, Is.EqualTo(.3f - TowerBushScatter.RootEmbedDepth).Within(.001f));
                    Assert.That(bush.Scale.x, Is.InRange(.85f, 1.15f));
                }
                Assert.That(bushes.Select(b => b.Position.x).Distinct().Count(), Is.EqualTo(bushes.Count));
                var again = generator.BuildDetailBushes(assets);
                Assert.That(again.Select(b => b.Position), Is.EqualTo(bushes.Select(b => b.Position)));
            }
            finally { Object.DestroyImmediate(setup); }
        }

        [Test]
        public void Generator_KeepsBushesOffLowStairs()
        {
            var setup = CreateGeneratorScene(out var assets, out _);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.transform.SetParent(setup.transform);
                generator.stairStepObject = step;
                // Low steps all around the pillar ring block both sides of every pillar.
                for (int i = 0; i < 360; i += 2)
                for (float r = 12; r <= 18; r += .5f)
                {
                    float a = i * Mathf.Deg2Rad;
                    assets.Add(new ChunkAssetData(0, TowerAssetType.Stair, new Vector3(Mathf.Cos(a) * r, .3f, Mathf.Sin(a) * r), Quaternion.identity, Vector3.one));
                }
                Assert.That(generator.BuildDetailBushes(assets), Is.Empty);
            }
            finally { Object.DestroyImmediate(setup); }
        }

        // A full 360 degree deck from radius 10 to 20 at y 0.2 with twelve pillars on radius 15.
        private static GameObject CreateGeneratorScene(out List<ChunkAssetData> assets, out Vector3[] pillars)
        {
            var setup = new GameObject("Detail generator");
            var generator = setup.AddComponent<TowerGenerator>();
            generator.heighIncrease = 6;
            generator.center = Vector2.zero;
            generator.detailBushPillarInterval = 3;
            var bush = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bush.transform.SetParent(setup.transform);
            Object.DestroyImmediate(bush.GetComponent<Collider>());
            generator.detailBushObject = bush;
            var tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.transform.SetParent(setup.transform);
            generator.floorTileObject = tile;

            assets = new List<ChunkAssetData>();
            const float outer = 20, depth = 10; const int tiles = 16;
            float sweep = Mathf.PI * 2 / tiles;
            for (int i = 0; i < tiles; i++)
            {
                float a = i * sweep;
                assets.Add(new ChunkAssetData(0, TowerAssetType.Floor, new Vector3(Mathf.Cos(a) * outer, .2f, Mathf.Sin(a) * outer),
                    Quaternion.identity, new Vector3(depth / TowerFloorLayout.TemplateSize, .2f, sweep * outer / TowerFloorLayout.TemplateSize)));
            }
            pillars = Enumerable.Range(0, 12).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI / 6) * 15, 0, Mathf.Sin(i * Mathf.PI / 6) * 15)).ToArray();
            foreach (var pillar in pillars)
                assets.Add(new ChunkAssetData(0, TowerAssetType.Pillar, pillar, Quaternion.identity, Vector3.one));
            return setup;
        }

        private static FieldInfo Field(string name) =>
            typeof(ChunkManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static void AddAsset(Transform parent, Vector3 position, TowerAssetType type)
        {
            var asset = new GameObject(type.ToString());
            asset.transform.SetParent(parent);
            asset.transform.position = position;
            asset.AddComponent<TowerAsset>().SetAssetType(type);
        }
    }

    public sealed class SnapshotRecorder : MonoBehaviour, IFarChunkRenderer
    {
        public readonly Dictionary<ChunkKey, ChunkAssetData[]> Last = new();
        public void LoadChunk(in FarChunkSnapshot snapshot) => Last[snapshot.Key] = snapshot.Assets.ToArray();
        public void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot) { }
        public void RemoveChunk(ChunkKey key) { }
        public void SetVisible(bool visible) { }
    }
}
