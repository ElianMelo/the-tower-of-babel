using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Players;
using TowerOfBabel.World.Chunks;
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
        public void CacheChunks_IncludesEveryDescendantAndUsesWorldPosition()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();
            GameObject towerRoot = new("TowerRoot");
            towerRoot.transform.position = new Vector3(64f, 6f, 0f);

            GameObject firstAsset = new("FirstAsset");
            firstAsset.transform.SetParent(towerRoot.transform);
            firstAsset.transform.position = new Vector3(63.9f, 5.9f, 0f);
            GameObject nestedAsset = new("NestedAsset");
            nestedAsset.transform.SetParent(firstAsset.transform);
            nestedAsset.transform.position = new Vector3(96f, 12f, -0.01f);
            nestedAsset.SetActive(false);

            SetField(manager, "towerRoot", towerRoot);
            manager.CacheChunks();

            Assert.That(manager.CachedChunks, Has.Count.EqualTo(2));
            Assert.That(manager.CachedChunks[0].Key, Is.EqualTo(new ChunkKey(0, 1, 0)));
            Assert.That(manager.CachedChunks[0].GameObjects[0], Is.SameAs(firstAsset));
            Assert.That(manager.CachedChunks[1].Key, Is.EqualTo(new ChunkKey(2, 3, -1)));
            Assert.That(manager.CachedChunks[1].GameObjects[0], Is.SameAs(nestedAsset));

            Object.DestroyImmediate(towerRoot);
            Object.DestroyImmediate(managerObject);
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
    }
}
