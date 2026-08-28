using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
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
            ChunkKey diagonal = new(6, -2, 7);

            Assert.That(ChunkGrid.ChebyshevDistance(center, diagonal), Is.EqualTo(1));
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
            Assert.That(manager.CachedChunks[0].GameObjects, Has.Count.EqualTo(1));
            Assert.That(manager.CachedChunks[0].GameObjects[0], Is.SameAs(firstAsset));
            Assert.That(manager.CachedChunks[1].Key, Is.EqualTo(new ChunkKey(2, 3, -1)));
            Assert.That(manager.CachedChunks[1].GameObjects[0], Is.SameAs(nestedAsset));

            Object.DestroyImmediate(towerRoot);
            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void NearestPlayer_UsesChunkDistanceAndRandomizesTies()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();

            PlayerInstance context = manager.RegisterPlayer(1, new Vector3(31f, 0f, 31f), Quaternion.identity);
            manager.RegisterPlayer(2, new Vector3(32.01f, 0f, 0f), Quaternion.identity);
            PlayerInstance sameChunkNear = manager.RegisterPlayer(3, new Vector3(2f, 0f, 0f), Quaternion.identity);
            PlayerInstance sameChunkFar = manager.RegisterPlayer(4, new Vector3(30f, 0f, 30f), Quaternion.identity);

            Random.State previousRandomState = Random.state;
            Random.InitState(1729);
            HashSet<PlayerInstance> selectedPlayers = new();
            for (int i = 0; i < 32; i++)
                selectedPlayers.Add(manager.GetNearestPlayer(Vector3.zero));
            Random.state = previousRandomState;

            Assert.That(selectedPlayers, Is.EquivalentTo(new[] { context, sameChunkNear, sameChunkFar }));

            List<PlayerInstance> results = new();
            manager.GetNearestPlayers(context, 3, results);
            Assert.That(results.GetRange(0, 2), Is.EquivalentTo(new[] { sameChunkNear, sameChunkFar }));
            Assert.That(results[2].PlayerId, Is.EqualTo(2));

            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void PriorityList_PutsFriendsBeforeCloserNonFriends()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();

            manager.RegisterPlayer(10, new Vector3(1f, 0f, 0f), Quaternion.identity);
            PlayerInstance friend = manager.RegisterPlayer(20, new Vector3(1000f, 0f, 0f), Quaternion.identity, null, true);
            manager.RefreshPlayerPriorities(Vector3.zero);

            Assert.That(manager.PrioritizedPlayers[0], Is.SameAs(friend));

            Object.DestroyImmediate(managerObject);
        }

        [Test]
        public void RemoteVisual_IsEnabledOnlyWhilePrioritized()
        {
            GameObject managerObject = new("ChunkManagerTest");
            managerObject.SetActive(false);
            ChunkManager manager = managerObject.AddComponent<ChunkManager>();
            GameObject visual = new("RemoteVisual");

            PlayerInstance instance = manager.RegisterPlayer(42, Vector3.one, Quaternion.identity, visual);
            Assert.That(visual.activeSelf, Is.False);

            manager.RefreshPlayerPriorities(Vector3.zero);
            Assert.That(instance.IsVisualPrioritized, Is.True);
            Assert.That(visual.activeSelf, Is.True);

            manager.UnregisterPlayer(42);
            Assert.That(visual.activeSelf, Is.False);

            Object.DestroyImmediate(visual);
            Object.DestroyImmediate(managerObject);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
            field.SetValue(target, value);
        }
    }
}
