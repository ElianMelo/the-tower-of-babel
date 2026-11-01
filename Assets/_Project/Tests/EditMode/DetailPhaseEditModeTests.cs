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
            AddAsset(tower.transform, new Vector3(2, .5f, 0), TowerAssetType.Vine0);
            AddAsset(tower.transform, new Vector3(0, 6, 0), TowerAssetType.Stair);
            Field("towerRoot").SetValue(manager, tower);
            manager.CacheChunks();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void Vines_AreDetailAssetsAndEverythingElseIsStructure()
        {
            Assert.That(TowerAssetType.Vine0.Phase(), Is.EqualTo(TowerConstructionPhase.Detail));
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                if (!type.IsDetail()) Assert.That(type.Phase(), Is.EqualTo(TowerConstructionPhase.Structure), type.ToString());
            Assert.That(manager.TryGetAsset(Ground, VineIndex, out var vine) && vine.AssetType == TowerAssetType.Vine0);
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
            Assert.That(pool.GetCapacity(TowerAssetType.Vine0), Is.Zero);

            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(pool.ActiveInstanceCount, Is.EqualTo(3), "Structure complete releases the vine.");
            Assert.That(pool.ActiveAssets.Count(a => a.GetComponent<TowerAsset>().AssetType == TowerAssetType.Vine0), Is.EqualTo(1));

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
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Vine0), Is.False);
            manager.SetAssetStage(Ground, FloorIndex, 10);
            manager.SetAssetStage(Ground, PillarIndex, 10);
            Assert.That(far.Last[Ground].Any(a => a.AssetType == TowerAssetType.Vine0), Is.True);
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
            Assert.That(TowerAssetTypeExtensions.DetailTypes.Length, Is.EqualTo(TowerAssetTypeExtensions.DetailCount));
            Assert.That(TowerAssetTypeExtensions.DetailTypes.Select(t => t.DetailIndex()), Is.EqualTo(Enumerable.Range(0, TowerAssetTypeExtensions.DetailCount)));
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
                Assert.That(type.IsDetail(), Is.EqualTo(System.Array.IndexOf(TowerAssetTypeExtensions.DetailTypes, type) >= 0), type.ToString());
            var set = new TowerAssetPrefabSet();
            var prefab = new GameObject("Shape 7");
            try
            {
                set.SetPrefab(TowerAssetType.Vine7, prefab);
                Assert.That(set.GetPrefab(TowerAssetType.Vine7), Is.SameAs(prefab));
                Assert.That(set.GetPrefab(TowerAssetType.Vine0), Is.Null);
            }
            finally { Object.DestroyImmediate(prefab); }
        }

        [Test]
        public void Generator_OutsideAndInsideAreSeparateVines_PlacedDeterministically()
        {
            var setup = CreateGeneratorScene(12, out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                generator.detailVineCoverage = 1; generator.detailVineClustering = 0;
                var vines = generator.BuildDetailVines(assets);
                Assert.That(vines.Count, Is.EqualTo(pillars.Length * 2), "Full coverage puts a vine on both faces of every pillar.");
                foreach (var vine in vines)
                {
                    Assert.That(vine.AssetType.IsDetail(), vine.AssetType.ToString());
                    Assert.That(pillars.Min(p => Vector3.Distance(p, vine.Position)), Is.LessThan(.001f), "Rooted on a pillar.");
                    Assert.That(Mathf.Abs(Vector3.Dot(vine.Rotation * Vector3.forward, Outward(vine))), Is.EqualTo(1).Within(.001f), "Faces the inside or the outside.");
                    Assert.That(Mathf.Abs(vine.Scale.x), Is.EqualTo(1)); Assert.That(vine.Scale.y, Is.EqualTo(1));
                }
                foreach (var pair in vines.GroupBy(v => v.Position))
                    Assert.That(pair.Select(v => Vector3.Dot(v.Rotation * Vector3.forward, Outward(v)) > 0), Is.EquivalentTo(new[] { true, false }),
                        "Each pillar has one outside and one inside vine, as separate building blocks.");
                Assert.That(vines.Select(v => v.AssetType).Distinct().Count(), Is.GreaterThan(1), "Shapes are mixed.");
                Assert.That(vines.Count(v => v.Scale.x < 0), Is.InRange(1, vines.Count - 1), "Some are mirrored.");
                generator.detailVineCoverage = .3f; generator.detailVineClustering = .75f;
                var partial = generator.BuildDetailVines(assets);
                Assert.That(partial.Select(v => (v.AssetType, v.Position, v.Rotation, v.Scale)),
                    Is.EqualTo(generator.BuildDetailVines(assets).Select(v => (v.AssetType, v.Position, v.Rotation, v.Scale))));
            }
            finally { Object.DestroyImmediate(setup); }
        }

        [Test]
        public void Generator_GathersVinesIntoPatchesWithBareStretches()
        {
            var setup = CreateGeneratorScene(360, out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                generator.detailVineCoverage = .25f; generator.detailVineClustering = 1;
                var outside = generator.BuildDetailVines(assets).Where(v => Vector3.Dot(v.Rotation * Vector3.forward, Outward(v)) > 0)
                    .Select(v => System.Array.FindIndex(pillars, p => Vector3.Distance(p, v.Position) < .001f)).ToHashSet();
                Assert.That(outside.Count, Is.InRange(360 * .12f, 360 * .45f), "Roughly the requested coverage.");
                int longestGap = 0, gap = 0, densest = 0;
                for (int i = 0; i < 360 * 2; i++)
                {
                    gap = outside.Contains(i % 360) ? 0 : gap + 1; longestGap = Mathf.Max(longestGap, gap);
                    densest = Mathf.Max(densest, Enumerable.Range(i, 12).Count(k => outside.Contains(k % 360)));
                }
                Assert.That(longestGap, Is.GreaterThanOrEqualTo(15), "Bare stretches between patches.");
                Assert.That(densest, Is.GreaterThanOrEqualTo(6), "Somewhere at least half of twelve neighbouring pillars carry a vine.");
            }
            finally { Object.DestroyImmediate(setup); }
        }

        [Test]
        public void Generator_SkipsOutsideFacesCrossedByStairs_ButInsideVinesRunAlongTheRamp()
        {
            var setup = CreateGeneratorScene(12, out var assets, out var pillars);
            try
            {
                var generator = setup.GetComponent<TowerGenerator>();
                generator.detailVineCoverage = 1; generator.detailVineClustering = 0;
                var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
                step.transform.SetParent(setup.transform);
                generator.stairStepObject = step;
                var stairs = new List<ChunkAssetData>(assets);
                foreach (var pillar in pillars)
                    for (float side = -1; side <= 1; side += 2)
                        stairs.Add(new ChunkAssetData(0, TowerAssetType.Stair, pillar + pillar.normalized * .6f * side + Vector3.up * 1.2f, Quaternion.LookRotation(pillar.normalized), Vector3.one));
                var vines = generator.BuildDetailVines(stairs);
                Assert.That(vines.Count, Is.EqualTo(pillars.Length), "Only the inside faces remain.");
                Assert.That(vines.All(v => Vector3.Dot(v.Rotation * Vector3.forward, Outward(v)) < 0), Is.True);
                // A step crossing only the vine's root does not block the outside face either.
                var rootSteps = new List<ChunkAssetData>(assets);
                foreach (var pillar in pillars)
                    rootSteps.Add(new ChunkAssetData(0, TowerAssetType.Stair, pillar + pillar.normalized * .6f + Vector3.up * .5f, Quaternion.LookRotation(pillar.normalized), new Vector3(1, .4f, 1)));
                Assert.That(generator.BuildDetailVines(rootSteps).Count, Is.EqualTo(pillars.Length * 2));
            }
            finally { Object.DestroyImmediate(setup); }
        }

        private static Vector3 Outward(ChunkAssetData vine) => new Vector3(vine.Position.x, 0, vine.Position.z).normalized;

        // Pillars on a 15 m ring; each vine shape is a unit cube 0.7 m in front of its pillar, from 0.7 to 1.7 m up.
        private static GameObject CreateGeneratorScene(int count, out List<ChunkAssetData> assets, out Vector3[] pillars)
        {
            var setup = new GameObject("Detail generator");
            var generator = setup.AddComponent<TowerGenerator>();
            generator.heighIncrease = 6;
            generator.center = Vector2.zero;
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
            Assert.That(generator.GetModelPrefab(TowerAssetType.Vine2), Is.SameAs(generator.detailVineObjects[2]));
            Assert.That(generator.GetModelPrefab(TowerAssetType.Vine5), Is.Null);
            assets = new List<ChunkAssetData>();
            pillars = Enumerable.Range(0, count).Select(i => new Vector3(Mathf.Cos(i * Mathf.PI * 2 / count) * 15, 0, Mathf.Sin(i * Mathf.PI * 2 / count) * 15)).ToArray();
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
