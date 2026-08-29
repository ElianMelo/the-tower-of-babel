using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Players;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class ChunkManagerEditModeTests
    {
        [Test]
        public void WorldToChunk_UsesFloorDivisionForNegativeCoordinates()
        {
            ChunkKey key = ChunkGrid.WorldToChunk(new Vector3(-0.01f, -0.01f, -32.01f), 32f, 6f);
            Assert.That(key, Is.EqualTo(new ChunkKey(-1, -1, -2)));
            Assert.That(ChunkGrid.WorldToChunk(new Vector3(32f, 6f, 64f), 32f, 6f),
                Is.EqualTo(new ChunkKey(1, 1, 2)));
        }

        [Test]
        public void ChebyshevDistance_CountsDiagonalAdjacentChunkAsOne()
        {
            ChunkKey center = new(5, -3, 8);
            Assert.That(ChunkGrid.ChebyshevDistance(center, new ChunkKey(6, -2, 7)), Is.EqualTo(1));
            Assert.That(ChunkGrid.ChebyshevDistance(center, new ChunkKey(9, -2, 6)), Is.EqualTo(4));
        }

        [Test]
        public void Neighborhood_WithRadiusOneContainsExactlyTwentySevenUniqueChunks()
        {
            HashSet<ChunkKey> neighborhood = new();
            ChunkGrid.GetNeighborhood(new ChunkKey(4, -2, 7), 1, neighborhood);
            Assert.That(neighborhood, Has.Count.EqualTo(ChunkManager.NearChunkSlotCount));
            Assert.That(neighborhood, Does.Contain(new ChunkKey(3, -3, 6)));
            Assert.That(neighborhood, Does.Contain(new ChunkKey(5, -1, 8)));
        }

        [Test]
        public void CacheChunks_StoresPureDeterministicDataThatSurvivesAuthoringObjectDestruction()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();
            GameObject towerRoot = new("TowerRoot");
            towerRoot.transform.position = new Vector3(64f, 6f, 0f);

            GameObject firstAsset = new("FirstAsset");
            firstAsset.transform.SetParent(towerRoot.transform);
            firstAsset.transform.position = new Vector3(63.9f, 5.9f, 0f);
            firstAsset.AddComponent<TowerAsset>().SetAssetType(TowerAssetType.Floor);
            GameObject nestedAsset = new("NestedAsset");
            nestedAsset.transform.SetParent(firstAsset.transform);
            nestedAsset.transform.position = new Vector3(96f, 12f, -0.01f);
            nestedAsset.SetActive(false);
            nestedAsset.AddComponent<TowerAsset>().SetAssetType(TowerAssetType.Arch);

            SetField(manager, "towerRoot", towerRoot);
            manager.CacheChunks();

            Assert.That(manager.CachedChunks, Has.Count.EqualTo(2));
            Assert.That(manager.CachedChunks[0].Key, Is.EqualTo(new ChunkKey(0, 1, 0)));
            Assert.That(manager.CachedChunks[0].Assets[0].AssetType, Is.EqualTo(TowerAssetType.Floor));
            Assert.That(manager.CachedChunks[0].Assets[0].LocalIndex, Is.Zero);
            Assert.That(manager.CachedChunks[0].Assets[0].Stage, Is.EqualTo(ChunkAssetData.CompletedStage));
            Assert.That(manager.CachedChunks[0].Assets[0].Position, Is.EqualTo(firstAsset.transform.position));
            Assert.That(manager.CachedChunks[1].Key, Is.EqualTo(new ChunkKey(2, 3, -1)));
            Assert.That(manager.CachedChunks[1].Assets[0].AssetType, Is.EqualTo(TowerAssetType.Arch));

            Vector3 cachedPosition = manager.CachedChunks[1].Assets[0].Position;
            Object.DestroyImmediate(towerRoot);

            Assert.That(manager.CachedChunks[1].Assets[0].Position, Is.EqualTo(cachedPosition));

            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void CacheChunks_AssignsDeterministicTypeAndPositionOrderedLocalIndices()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();
            GameObject towerRoot = new("TowerRoot");
            CreateTypedAsset("Arch", towerRoot.transform, new Vector3(1f, 0f, 0f), TowerAssetType.Arch);
            CreateTypedAsset("FloorB", towerRoot.transform, new Vector3(2f, 0f, 0f), TowerAssetType.Floor);
            CreateTypedAsset("FloorA", towerRoot.transform, new Vector3(1f, 0f, 0f), TowerAssetType.Floor);
            SetField(manager, "towerRoot", towerRoot);

            manager.CacheChunks();

            IReadOnlyList<ChunkAssetData> assets = manager.CachedChunks[0].Assets;
            Assert.That(assets, Has.Count.EqualTo(3));
            Assert.That(assets[0].AssetType, Is.EqualTo(TowerAssetType.Floor));
            Assert.That(assets[0].Position.x, Is.EqualTo(1f));
            Assert.That(assets[0].LocalIndex, Is.Zero);
            Assert.That(assets[1].Position.x, Is.EqualTo(2f));
            Assert.That(assets[1].LocalIndex, Is.EqualTo(1));
            Assert.That(assets[2].AssetType, Is.EqualTo(TowerAssetType.Arch));
            Assert.That(assets[2].LocalIndex, Is.EqualTo(2));

            Object.DestroyImmediate(towerRoot);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void ConfigureTower_CachesDataThenDestroysTemporaryAuthoringAssets()
        {
            GameObject root = new("ConfigureTowerTest");
            root.SetActive(false);
            GameObject towerParent = new("TowerParent");
            towerParent.transform.SetParent(root.transform);
            GameObject modelPrefab = new("TowerModel");
            modelPrefab.SetActive(false);

            TowerGenerator generator = root.AddComponent<TowerGenerator>();
            generator.parent = towerParent.transform;
            generator.pillarObject = modelPrefab;
            generator.archObject = modelPrefab;
            generator.floorTileObject = modelPrefab;
            generator.stairStepObject = modelPrefab;
            generator.radius = 2f;
            generator.pillarAngle = 90f;
            generator.tileAngle = 90f;
            generator.tileDecreaseRate = 1f;
            generator.levels = 1;
            generator.floorHeight = 2f;
            generator.stairHeight = 0.5f;

            ChunkManager manager = root.AddComponent<ChunkManager>();
            SetField(manager, "towerRoot", towerParent);
            ConfigureTower configureTower = root.AddComponent<ConfigureTower>();
            SetField(configureTower, "towerGenerator", generator);
            SetField(configureTower, "chunkManager", manager);

            configureTower.RunConfiguration();
            for (int i = 0; i < 16 && configureTower.IsRunning; i++)
                InvokePrivate(configureTower, "AdvanceConfiguration");

            Assert.That(configureTower.IsRunning, Is.False);
            Assert.That(towerParent.transform.childCount, Is.Zero);
            Assert.That(manager.CachedChunks.Count, Is.GreaterThan(0));
            Assert.That(manager.AssetPrefabs.IsComplete, Is.True);
            Assert.That(root.GetComponent<NearTowerAssetPool>(), Is.Not.Null);
            Assert.That(root.GetComponent<InstancedFarChunkRenderer>(), Is.Not.Null);

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(modelPrefab);
        }

        [Test]
        public void TowerVisibility_ImmediatelySwitchesChunkDataBetweenNearPoolAndFarRenderer()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();
            RecordingFarChunkRenderer renderer = managerObject.AddComponent<RecordingFarChunkRenderer>();
            NearTowerAssetPool nearPool = managerObject.AddComponent<NearTowerAssetPool>();
            GameObject towerRoot = new("TowerRoot");
            GameObject poolPrefab = CreateTypedAsset("PoolPrefab", null, Vector3.zero, TowerAssetType.Floor);
            poolPrefab.SetActive(false);
            ConfigureAllPrefabs(manager, poolPrefab);

            GameObject firstAsset = CreateTypedAsset("First", towerRoot.transform, Vector3.zero, TowerAssetType.Floor);
            GameObject secondAsset = CreateTypedAsset("Second", towerRoot.transform,
                new Vector3(64f, 0f, 0f), TowerAssetType.Pillar);
            SetField(manager, "towerRoot", towerRoot);
            SetField(manager, "farChunkRendererComponent", renderer);
            SetField(manager, "nearAssetPool", nearPool);
            manager.CacheChunks();
            Object.DestroyImmediate(towerRoot);

            InvokePrivate(manager, "ResolveFarChunkRenderer");
            InvokePrivate(manager, "ApplyTowerVisibility", new ChunkKey(0, 0, 0), true);

            Assert.That(nearPool.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(nearPool.ActiveAssets[0].transform.position, Is.EqualTo(Vector3.zero));
            Assert.That(renderer.LoadedChunks, Has.No.Member(new ChunkKey(0, 0, 0)));
            Assert.That(renderer.LoadedChunks, Has.Member(new ChunkKey(0, 2, 0)));

            InvokePrivate(manager, "ApplyTowerVisibility", new ChunkKey(0, 2, 0), false);

            Assert.That(nearPool.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(nearPool.ActiveAssets[0].transform.position, Is.EqualTo(new Vector3(64f, 0f, 0f)));
            Assert.That(renderer.LoadedChunks, Has.Member(new ChunkKey(0, 0, 0)));
            Assert.That(renderer.LoadedChunks, Has.No.Member(new ChunkKey(0, 2, 0)));

            Assert.That(manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 0), Is.True);
            Assert.That(nearPool.ActiveInstanceCount, Is.Zero);
            Assert.That(manager.SetAssetStage(new ChunkKey(0, 2, 0), 0, 10), Is.True);
            Assert.That(nearPool.ActiveInstanceCount, Is.EqualTo(1));
            Assert.That(nearPool.GetCapacity(TowerAssetType.Pillar), Is.GreaterThanOrEqualTo(1));

            Object.DestroyImmediate(managerObject);
            Object.DestroyImmediate(poolPrefab);
        }

        [Test]
        public void InstancedFarRenderer_LoadsAndRemovesTypedChunkInstances()
        {
            GameObject rendererObject = new("FarRendererTest");
            rendererObject.SetActive(false);
            InstancedFarChunkRenderer renderer = rendererObject.AddComponent<InstancedFarChunkRenderer>();
            GameObject modelPrefab = new("FloorModel");
            Mesh mesh = new();
            modelPrefab.AddComponent<MeshFilter>().sharedMesh = mesh;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Hidden/InternalErrorShader");
            Material material = new(shader);
            modelPrefab.AddComponent<MeshRenderer>().sharedMaterial = material;
            TowerAssetRenderModel model = new();
            SetField(model, "assetType", TowerAssetType.Floor);
            SetField(model, "prefab", modelPrefab);
            SetField(renderer, "models", new List<TowerAssetRenderModel> { model });

            ChunkKey key = new(0, 4, -2);
            List<ChunkAssetData> assets = new(1025);
            for (int i = 0; i < 1024; i++)
                assets.Add(new ChunkAssetData(i, TowerAssetType.Floor, Vector3.right * i,
                    Quaternion.identity, Vector3.one));
            assets.Add(new ChunkAssetData(1024, TowerAssetType.Floor, Vector3.zero,
                Quaternion.identity, Vector3.one, 0));
            FarChunkSnapshot snapshot = new(key, assets);
            renderer.LoadChunk(in snapshot);

            Assert.That(renderer.LoadedChunkCount, Is.EqualTo(1));
            Assert.That(renderer.LoadedInstanceCount, Is.EqualTo(1024));
            Assert.DoesNotThrow(() => InvokePrivate(renderer, "LateUpdate"));

            renderer.RemoveChunk(key);

            Assert.That(renderer.LoadedChunkCount, Is.Zero);
            Assert.That(renderer.LoadedInstanceCount, Is.Zero);
            Object.DestroyImmediate(rendererObject);
            Object.DestroyImmediate(modelPrefab);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(mesh);
        }

        [Test]
        public void SpatialIndex_UsesChunkDistanceAndRandomizesNearestTies()
        {
            ChunkManager manager = CreateInactiveChunkManager(out GameObject managerObject);
            PlayerInstance context = new(1, new Vector3(31f, 0f, 31f), Quaternion.identity);
            PlayerInstance sameChunkNear = new(2, new Vector3(2f, 0f, 0f), Quaternion.identity);
            PlayerInstance sameChunkFar = new(3, new Vector3(30f, 0f, 30f), Quaternion.identity);
            PlayerInstance adjacent = new(4, new Vector3(32.01f, 0f, 0f), Quaternion.identity);
            manager.TrackPlayer(context);
            manager.TrackPlayer(sameChunkNear);
            manager.TrackPlayer(sameChunkFar);
            manager.TrackPlayer(adjacent);

            List<PlayerInstance> results = new();
            manager.GetNearestPlayers(context, 3, 20, results);

            Assert.That(results.GetRange(0, 2), Is.EquivalentTo(new[] { sameChunkNear, sameChunkFar }));
            Assert.That(results[2], Is.SameAs(adjacent));
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void SpatialIndex_CutsSearchPastTwentyChunks()
        {
            ChunkManager manager = CreateInactiveChunkManager(out GameObject managerObject);
            PlayerInstance context = new(1, Vector3.zero, Quaternion.identity);
            PlayerInstance atTwenty = new(2, new Vector3(20f * 32f, 0f, 0f), Quaternion.identity);
            PlayerInstance pastTwenty = new(3, new Vector3(21f * 32f, 0f, 0f), Quaternion.identity);
            manager.TrackPlayer(context);
            manager.TrackPlayer(atTwenty);
            manager.TrackPlayer(pastTwenty);

            List<PlayerInstance> results = new();
            manager.GetPlayersWithinChunkDistance(context.CurrentChunk, 20, results, context.PlayerId);

            Assert.That(results, Is.EquivalentTo(new[] { atTwenty }));
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void PlayerInstance_RejectsStaleSnapshots()
        {
            PlayerInstance instance = new(7, Vector3.zero, Quaternion.identity);
            Assert.That(instance.ApplySnapshot(new PlayerStateSnapshot(7, 2, Vector3.one,
                Quaternion.identity, PlayerAnimationState.Walk)), Is.True);
            Assert.That(instance.ApplySnapshot(new PlayerStateSnapshot(7, 1, Vector3.zero,
                Quaternion.identity, PlayerAnimationState.Idle)), Is.False);
            Assert.That(instance.Position, Is.EqualTo(Vector3.one));
            Assert.That(instance.AnimationState, Is.EqualTo(PlayerAnimationState.Walk));
        }

        [Test]
        public void PlayersManager_PrioritizesFriendsAndUsesTwentyChunkCutoff()
        {
            CreateManagers(out GameObject root, out PlayersManager playersManager, out _);
            SetField(playersManager, "priorityCapacity", 2);
            playersManager.RegisterLocalPlayer(1);
            PlayerInstance nearby = playersManager.RegisterPlayer(Snapshot(2, new Vector3(32f, 0f, 0f)));
            PlayerInstance farFriend = playersManager.RegisterPlayer(Snapshot(3, new Vector3(30f * 32f, 0f, 0f)), true);
            playersManager.RegisterPlayer(Snapshot(4, new Vector3(21f * 32f, 0f, 0f)));

            playersManager.FillPrioritySeatsNow();

            Assert.That(playersManager.PriorityPlayers, Is.EquivalentTo(new[] { nearby, farFriend }));
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PlayersManager_ReturnsReleasedVisualToPool()
        {
            CreateManagers(out GameObject root, out PlayersManager playersManager, out _);
            SetField(playersManager, "priorityCapacity", 1);
            GameObject prefabObject = new("RemotePlayerPrefab");
            prefabObject.SetActive(false);
            RemotePlayerView prefab = prefabObject.AddComponent<RemotePlayerView>();
            SetField(playersManager, "remotePlayerPrefab", prefab);

            playersManager.RegisterLocalPlayer(1);
            playersManager.RegisterPlayer(Snapshot(2, Vector3.one));
            playersManager.FillPrioritySeatsNow();
            Assert.That(playersManager.PriorityCount, Is.EqualTo(1));

            playersManager.UnregisterPlayer(2);
            Assert.That(playersManager.PriorityCount, Is.Zero);
            Assert.That(playersManager.PooledVisualCount, Is.EqualTo(1));

            Object.DestroyImmediate(prefabObject);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void PlayersManager_RemoteRegistrationRequestsSearchAfterInitialLocalSearch()
        {
            CreateManagers(out GameObject root, out PlayersManager playersManager, out _);
            playersManager.RegisterLocalPlayer(1);
            PlayerInstance remote = playersManager.RegisterPlayer(Snapshot(2, Vector3.one));

            Assert.That(playersManager.PriorityCount, Is.Zero,
                "Roster entries should be batched until the next manager update.");
            InvokePrivate(playersManager, "Update");

            Assert.That(playersManager.PriorityPlayers, Is.EquivalentTo(new[] { remote }));
            Object.DestroyImmediate(root);
        }

        private static PlayerStateSnapshot Snapshot(uint id, Vector3 position) =>
            new(id, 1, position, Quaternion.identity, PlayerAnimationState.Idle);

        private static GameObject CreateTypedAsset(string name, Transform parent, Vector3 position,
            TowerAssetType assetType)
        {
            GameObject asset = new(name);
            asset.transform.SetParent(parent);
            asset.transform.position = position;
            asset.AddComponent<TowerAsset>().SetAssetType(assetType);
            return asset;
        }

        private static void ConfigureAllPrefabs(ChunkManager manager, GameObject prefab)
        {
            manager.AssetPrefabs.SetPrefab(TowerAssetType.Floor, prefab);
            manager.AssetPrefabs.SetPrefab(TowerAssetType.Stair, prefab);
            manager.AssetPrefabs.SetPrefab(TowerAssetType.Pillar, prefab);
            manager.AssetPrefabs.SetPrefab(TowerAssetType.Arch, prefab);
        }

        private static ChunkManager CreateInactiveChunkManager(out GameObject root)
        {
            root = new GameObject("ChunkManagerTest");
            root.SetActive(false);
            return root.AddComponent<ChunkManager>();
        }

        private static void CreateManagers(out GameObject root, out PlayersManager playersManager,
            out ChunkManager chunkManager)
        {
            root = new GameObject("PlayersManagerTest");
            root.SetActive(false);
            chunkManager = root.AddComponent<ChunkManager>();
            playersManager = root.AddComponent<PlayersManager>();
            GameObject localPlayer = new("Player");
            localPlayer.transform.SetParent(root.transform);
            SetField(playersManager, "chunkManager", chunkManager);
            SetField(playersManager, "localPlayer", localPlayer.transform);
            SetField(playersManager, "prewarmVisualCount", 0);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {methodName}");
            method.Invoke(target, null);
        }

        private static void InvokePrivate(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, $"Missing method {methodName}");
            method.Invoke(target, arguments);
        }
    }

    public sealed class RecordingFarChunkRenderer : MonoBehaviour, IFarChunkRenderer
    {
        public readonly HashSet<ChunkKey> LoadedChunks = new();

        public void LoadChunk(in FarChunkSnapshot snapshot) => LoadedChunks.Add(snapshot.Key);

        public void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot)
        {
        }

        public void RemoveChunk(ChunkKey key) => LoadedChunks.Remove(key);

        public void SetVisible(bool visible)
        {
        }
    }
}
