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
    /// <summary>Each floor builds its structure first; the detail (vines) unlocks after, then the next floor.</summary>
    public sealed class DetailPhaseEditModeTests
    {
        private static readonly ChunkKey Ground = new(0, 0, 0);
        private const int FloorIndex = 0, PillarIndex = 1, VineIndex = 2;

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
            // Sorted by type inside the chunk: Floor 0, Pillar 1, Vine 2.
            AddAsset(tower.transform, Vector3.zero, TowerAssetType.Floor);
            AddAsset(tower.transform, new Vector3(1, 0, 0), TowerAssetType.Pillar);
            AddAsset(tower.transform, new Vector3(2, .5f, 0), TowerAssetType.Vine);
            AddAsset(tower.transform, new Vector3(0, 6, 0), TowerAssetType.Stair);
            Field("towerRoot").SetValue(manager, tower);
            manager.CacheChunks();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Vines_AreDetailAssetsAndEverythingElseIsStructure()
        {
            Assert.That(TowerAssetType.Vine.Phase(), Is.EqualTo(TowerConstructionPhase.Detail));
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                if (!type.IsDetail()) Assert.That(type.Phase(), Is.EqualTo(TowerConstructionPhase.Structure), type.ToString());
            Assert.That(manager.TryGetAsset(Ground, VineIndex, out var vine) && vine.AssetType == TowerAssetType.Vine);
        }

        [Test]
        public void Detail_UnlocksAfterStructure_AndNextFloorAfterDetail()
        {
            Assert.That(manager.GetFloorPhase(0), Is.EqualTo(TowerConstructionPhase.Structure));
            Assert.That(manager.CanConstructAsset(Ground, VineIndex), Is.False, "Detail is withheld while structure is unfinished.");
            Assert.That(manager.TryAdvanceAssetStage(Ground, VineIndex, out _), Is.False);

            manager.SetAssetStage(Ground, FloorIndex, 10);
            Assert.That(manager.CanConstructAsset(Ground, VineIndex), Is.False);
            manager.SetAssetStage(Ground, PillarIndex, 10);

            Assert.That(manager.IsStructureComplete(0), Is.True);
            Assert.That(manager.GetFloorPhase(0), Is.EqualTo(TowerConstructionPhase.Detail));
            Assert.That(manager.CanConstructAsset(Ground, VineIndex), Is.True);
            Assert.That(manager.IsFloorComplete(0), Is.False, "The floor is not done until its detail is.");
            Assert.That(manager.ActiveConstructionFloor, Is.Zero);
            Assert.That(manager.IsFloorVisible(1), Is.False);

            for (int i = 0; i < 10; i++) Assert.That(manager.TryAdvanceAssetStage(Ground, VineIndex, out _), Is.True);
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

            manager.SetAssetStage(Ground, VineIndex, 4);
            manager.SetAssetStage(Ground, PillarIndex, 9);
            Assert.That(releases, Is.EqualTo(new[] { 0, 0 }));
            Assert.That(manager.CanConstructAsset(Ground, VineIndex), Is.False);
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
            Assert.That(manager.CanConstructAsset(Ground, VineIndex), Is.False);
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
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(2), "Floor and pillar only; the vine is withheld.");
            Assert.That(pool.GetCapacity(TowerAssetType.Vine), Is.Zero);

            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(3), "Structure complete releases the vine.");
            Assert.That(pool.ActiveAssets.Count(a => a.GetComponent<TowerAsset>().AssetType == TowerAssetType.Vine), Is.EqualTo(1));

            manager.SetAssetStage(Ground, PillarIndex, 9);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(2));

            manager.SetAssetStage(Ground, PillarIndex, 10);
            manager.SetAssetStage(Ground, VineIndex, 10);
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

            manager.SetAssetStage(Ground, VineIndex, 6);
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Vine), Is.False);
            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Vine), Is.True);
        }

        [Test]
        public void BoundVine_RefreshesItsPromptWhenDetailIsReleased()
        {
            var model = new GameObject("Vine model");
            var building = model.AddComponent<Building>();
            try
            {
                building.Bind(manager, Ground, VineIndex, 0);
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
        public void Vines_AreTheOnlyDetailTypes()
        {
            Assert.That(TowerAssetTypeExtensions.DetailTypes, Is.EqualTo(new[] { TowerAssetType.Vine, TowerAssetType.VineDraped, TowerAssetType.VineDense }));
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                Assert.That(type.IsDetail(), Is.EqualTo(System.Array.IndexOf(TowerAssetTypeExtensions.DetailTypes, type) >= 0), type.ToString());
        }

        [Test]
        public void Generator_PlacesVinesOnPillars_FacingInOrOut_Deterministically()
        {
            var setup = CreateGeneratorScene(out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                var vines = generator.BuildDetailVines(assets);
                Assert.That(vines.Count, Is.EqualTo(pillars.Length / generator.detailPillarInterval));
                foreach (var vine in vines)
                {
                    Assert.That(vine.AssetType.IsDetail(), vine.AssetType.ToString());
                    Assert.That(pillars.Min(p => Vector3.Distance(p, vine.Position)), Is.LessThan(.001f), "Rooted on a pillar.");
                    Vector3 outward = new Vector3(vine.Position.x, 0, vine.Position.z).normalized;
                    Assert.That(Mathf.Abs(Vector3.Dot(vine.Rotation * Vector3.forward, outward)), Is.EqualTo(1).Within(.001f), "Faces the inside or the outside.");
                    Assert.That(Mathf.Abs(vine.Scale.x), Is.EqualTo(1)); Assert.That(vine.Scale.y, Is.EqualTo(1));
                }
                var again = generator.BuildDetailVines(assets);
                Assert.That(again.Select(v => (v.AssetType, v.Position, v.Rotation, v.Scale)), Is.EqualTo(vines.Select(v => (v.AssetType, v.Position, v.Rotation, v.Scale))));
            }
            finally { Object.DestroyImmediate(setup); }
        }

        [Test]
        public void Generator_UsesTheOtherSideOrSkipsWhenStairsCrossTheVine()
        {
            var setup = CreateGeneratorScene(out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.transform.SetParent(setup.transform);
                generator.stairStepObject = step;
                // Steps that only reach the vine's root level don't block it: the vine grows from the step edge.
                var rootSteps = new List<ChunkAssetData>(assets);
                foreach (var pillar in pillars)
                    for (float side = -1; side <= 1; side += 2)
                        rootSteps.Add(new ChunkAssetData(0, TowerAssetType.Stair, pillar + pillar.normalized * .6f * side + Vector3.up * .5f, Quaternion.LookRotation(pillar.normalized), new Vector3(1, .4f, 1)));
                Assert.That(generator.BuildDetailVines(rootSteps).Count, Is.EqualTo(pillars.Length / generator.detailPillarInterval));
                // A step just inside every pillar forces the vines outside.
                var inner = new List<ChunkAssetData>(assets);
                foreach (var pillar in pillars)
                    inner.Add(new ChunkAssetData(0, TowerAssetType.Stair, pillar - pillar.normalized * .6f + Vector3.up * 2, Quaternion.LookRotation(pillar.normalized), Vector3.one));
                foreach (var vine in generator.BuildDetailVines(inner))
                    Assert.That(Vector3.Dot(vine.Rotation * Vector3.forward, new Vector3(vine.Position.x, 0, vine.Position.z)), Is.GreaterThan(0));
                // Steps on both sides leave no room at all.
                foreach (var pillar in pillars)
                    inner.Add(new ChunkAssetData(0, TowerAssetType.Stair, pillar + pillar.normalized * .6f + Vector3.up * 2, Quaternion.LookRotation(pillar.normalized), Vector3.one));
                Assert.That(generator.BuildDetailVines(inner), Is.Empty);
            }
            finally { Object.DestroyImmediate(setup); }
        }

        // Twelve pillars on a 15 m ring; each vine variant is a unit cube 0.7 m in front of its pillar, from 0.7 to 1.7 m up.
        private static GameObject CreateGeneratorScene(out List<ChunkAssetData> assets, out Vector3[] pillars)
        {
            var setup = new GameObject("Detail generator");
            var generator = setup.AddComponent<TowerGenerator>();
            generator.heighIncrease = 6;
            generator.center = Vector2.zero;
            generator.detailPillarInterval = 3;
            generator.detailVineObjects = new GameObject[3];
            for (int i = 0; i < 3; i++)
            {
                var vine = GameObject.CreatePrimitive(PrimitiveType.Cube);
                vine.transform.SetParent(setup.transform);
                Object.DestroyImmediate(vine.GetComponent<Collider>());
                // Like a real vine, the stand-in sits in front of the pillar face (local +Z).
                var filter = vine.GetComponent<MeshFilter>();
                var mesh = Object.Instantiate(filter.sharedMesh);
                mesh.vertices = mesh.vertices.Select(v => v + new Vector3(0, 1.2f, .7f)).ToArray();
                filter.sharedMesh = mesh;
                generator.detailVineObjects[i] = vine;
            }
            Assert.That(generator.GetModelPrefab(TowerAssetType.VineDense), Is.SameAs(generator.detailVineObjects[2]));
            assets = new List<ChunkAssetData>();
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
