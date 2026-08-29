using System;
using System.Collections.Generic;
using NaughtyAttributes;
using TowerOfBabel.Players;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public sealed class ChunkSceneCache
    {
        [SerializeField] private ChunkKey key;
        [SerializeField] private List<GameObject> gameObjects = new();

        public ChunkKey Key => key;
        public IReadOnlyList<GameObject> GameObjects => gameObjects;

        internal ChunkSceneCache(ChunkKey key, List<GameObject> gameObjects)
        {
            this.key = key;
            this.gameObjects = gameObjects;
        }
    }

    /// <summary>Owns tower visibility and the chunk-keyed spatial index.</summary>
    [DisallowMultipleComponent]
    public sealed class ChunkManager : MonoBehaviour
    {
        public const int NearChunkRadius = 1;
        public const int NearChunkSlotCount = 27;

        [Header("Tower Chunk Cache")]
        [Tooltip("Every descendant of this object is assigned to a chunk from its world position.")]
        [SerializeField] private GameObject towerRoot;
        [SerializeField, Min(0.01f)] private float chunkSizeMeters = ChunkGrid.DefaultChunkSizeMeters;
        [SerializeField, Min(0.01f)] private float floorHeight = 6f;
        [SerializeField] private List<ChunkSceneCache> cachedChunks = new();

        [Header("Local Player")]
        [Tooltip("Defaults to the active scene object named Player when left empty.")]
        [SerializeField] private Transform player;

        private readonly Dictionary<ChunkKey, ChunkSceneCache> chunkLookup = new();
        private readonly HashSet<ChunkKey> loadedChunks = new();
        private readonly HashSet<ChunkKey> desiredChunks = new();
        private readonly Dictionary<ChunkKey, HashSet<PlayerInstance>> playersByChunk = new();
        private readonly Dictionary<uint, PlayerInstance> trackedPlayers = new();

        private ChunkKey currentChunk;
        private bool hasCurrentChunk;
        private bool visibilityInitialized;

        public float ChunkSizeMeters => chunkSizeMeters;
        public float FloorHeight => floorHeight;
        public Transform PlayerTransform => player;
        public ChunkKey CurrentChunk => currentChunk;
        public bool HasCurrentChunk => hasCurrentChunk;
        public IReadOnlyList<ChunkSceneCache> CachedChunks => cachedChunks;
        public int TrackedPlayerCount => trackedPlayers.Count;

        public event Action<ChunkKey, ChunkKey> CurrentChunkChanged;

        private void OnEnable()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#else
            if (!Application.isPlaying)
                return;
            RebuildChunkLookup();
            ResolveScenePlayer();
            RefreshFromPlayer(true);
#endif
        }

        private void Update()
        {
            if (player == null)
            {
                ResolveScenePlayer();
                if (player == null)
                    return;
            }
            RefreshFromPlayer(false);
        }

        [Button]
        public void CacheChunks()
        {
            cachedChunks.Clear();
            if (towerRoot == null)
            {
                Debug.LogWarning("ChunkManager cannot cache chunks without a Tower Root.", this);
                RebuildChunkLookup();
                return;
            }

            Dictionary<ChunkKey, List<GameObject>> groupedObjects = new();
            Transform[] descendants = towerRoot.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant == towerRoot.transform)
                    continue;

                ChunkKey key = WorldToChunk(descendant.position);
                if (!groupedObjects.TryGetValue(key, out List<GameObject> objectsInChunk))
                {
                    objectsInChunk = new List<GameObject>();
                    groupedObjects.Add(key, objectsInChunk);
                }
                objectsInChunk.Add(descendant.gameObject);
            }

            List<ChunkKey> keys = new(groupedObjects.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                ChunkKey key = keys[i];
                cachedChunks.Add(new ChunkSceneCache(key, groupedObjects[key]));
            }

            RebuildChunkLookup();
            visibilityInitialized = false;
            if (Application.isPlaying && hasCurrentChunk)
                ApplyTowerVisibility(currentChunk, true);

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (gameObject.scene.IsValid())
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
            Debug.Log($"Cached {descendants.Length - 1} tower descendants into {cachedChunks.Count} chunks.", this);
        }

        public ChunkKey WorldToChunk(Vector3 worldPosition) =>
            ChunkGrid.WorldToChunk(worldPosition, chunkSizeMeters, floorHeight);

        public int CalculateChunkDistance(Vector3 first, Vector3 second) =>
            ChunkGrid.ChebyshevDistance(WorldToChunk(first), WorldToChunk(second));

        public int CalculateChunkDistance(ChunkKey first, ChunkKey second) =>
            ChunkGrid.ChebyshevDistance(first, second);

        public bool IsChunkLoaded(ChunkKey key) => loadedChunks.Contains(key);

        public void RefreshTowerVisibility()
        {
            if (player == null)
                ResolveScenePlayer();
            if (player != null)
                RefreshFromPlayer(true);
        }

        public void TrackPlayer(PlayerInstance instance)
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (trackedPlayers.TryGetValue(instance.PlayerId, out PlayerInstance previous))
                UntrackPlayer(previous);

            trackedPlayers.Add(instance.PlayerId, instance);
            MoveTrackedPlayer(instance, WorldToChunk(instance.Position));
        }

        public bool RefreshTrackedPlayer(PlayerInstance instance)
        {
            if (instance == null || !trackedPlayers.TryGetValue(instance.PlayerId, out PlayerInstance tracked)
                || !ReferenceEquals(instance, tracked))
                return false;

            ChunkKey nextChunk = WorldToChunk(instance.Position);
            if (instance.HasChunk && instance.CurrentChunk == nextChunk)
                return false;
            MoveTrackedPlayer(instance, nextChunk);
            return true;
        }

        public bool UntrackPlayer(PlayerInstance instance)
        {
            if (instance == null || !trackedPlayers.Remove(instance.PlayerId))
                return false;

            RemoveFromCurrentBucket(instance);
            instance.ClearChunk();
            return true;
        }

        public int GetPlayersInChunk(ChunkKey key, List<PlayerInstance> results, uint? excludedPlayerId = null)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            if (!playersByChunk.TryGetValue(key, out HashSet<PlayerInstance> bucket))
                return 0;

            foreach (PlayerInstance instance in bucket)
            {
                if (instance.IsConnected && (!excludedPlayerId.HasValue || instance.PlayerId != excludedPlayerId.Value))
                    results.Add(instance);
            }
            return results.Count;
        }

        public int GetPlayersWithinChunkDistance(ChunkKey center, int maximumDistance,
            List<PlayerInstance> results, uint? excludedPlayerId = null)
        {
            if (maximumDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            foreach (KeyValuePair<ChunkKey, HashSet<PlayerInstance>> pair in playersByChunk)
            {
                if (CalculateChunkDistance(center, pair.Key) > maximumDistance)
                    continue;
                foreach (PlayerInstance instance in pair.Value)
                {
                    if (instance.IsConnected && (!excludedPlayerId.HasValue || instance.PlayerId != excludedPlayerId.Value))
                        results.Add(instance);
                }
            }
            return results.Count;
        }

        public int GetNearestPlayers(PlayerInstance context, int maximumCount,
            int maximumChunkDistance, List<PlayerInstance> results)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!context.HasChunk)
                throw new InvalidOperationException("The context player must be tracked before querying nearby players.");

            GetPlayersWithinChunkDistance(context.CurrentChunk, maximumChunkDistance, results, context.PlayerId);
            results.Sort((first, second) =>
            {
                int firstDistance = CalculateChunkDistance(context.CurrentChunk, first.CurrentChunk);
                int secondDistance = CalculateChunkDistance(context.CurrentChunk, second.CurrentChunk);
                return firstDistance.CompareTo(secondDistance);
            });

            // Distance is intentionally chunk-only. Randomize each equal-distance band so
            // players sharing the context chunk are never selected by physical proximity.
            int bandStart = 0;
            while (bandStart < results.Count)
            {
                int bandDistance = CalculateChunkDistance(context.CurrentChunk, results[bandStart].CurrentChunk);
                int bandEnd = bandStart + 1;
                while (bandEnd < results.Count &&
                       CalculateChunkDistance(context.CurrentChunk, results[bandEnd].CurrentChunk) == bandDistance)
                    bandEnd++;
                ShuffleRange(results, bandStart, bandEnd);
                bandStart = bandEnd;
            }

            if (maximumCount >= 0 && results.Count > maximumCount)
                results.RemoveRange(maximumCount, results.Count - maximumCount);
            return results.Count;
        }

        public void ClearTrackedPlayers()
        {
            foreach (PlayerInstance instance in trackedPlayers.Values)
                instance.ClearChunk();
            trackedPlayers.Clear();
            playersByChunk.Clear();
        }

        private static void ShuffleRange(List<PlayerInstance> instances, int startInclusive, int endExclusive)
        {
            for (int i = endExclusive - 1; i > startInclusive; i--)
            {
                int swapIndex = UnityEngine.Random.Range(startInclusive, i + 1);
                PlayerInstance temporary = instances[i];
                instances[i] = instances[swapIndex];
                instances[swapIndex] = temporary;
            }
        }

        private void MoveTrackedPlayer(PlayerInstance instance, ChunkKey nextChunk)
        {
            RemoveFromCurrentBucket(instance);
            if (!playersByChunk.TryGetValue(nextChunk, out HashSet<PlayerInstance> nextBucket))
            {
                nextBucket = new HashSet<PlayerInstance>();
                playersByChunk.Add(nextChunk, nextBucket);
            }
            nextBucket.Add(instance);
            instance.SetChunk(nextChunk);
        }

        private void RemoveFromCurrentBucket(PlayerInstance instance)
        {
            if (!instance.HasChunk || !playersByChunk.TryGetValue(instance.CurrentChunk, out HashSet<PlayerInstance> bucket))
                return;
            bucket.Remove(instance);
            if (bucket.Count == 0)
                playersByChunk.Remove(instance.CurrentChunk);
        }

        private void ResolveScenePlayer()
        {
            if (player != null)
                return;
            GameObject playerObject = GameObject.Find("Player");
            if (playerObject != null)
                player = playerObject.transform;
        }

        private void RefreshFromPlayer(bool force)
        {
            if (player == null)
                return;
            ChunkKey nextChunk = WorldToChunk(player.position);
            if (!force && hasCurrentChunk && nextChunk == currentChunk)
                return;

            ChunkKey previousChunk = currentChunk;
            bool hadPreviousChunk = hasCurrentChunk;
            currentChunk = nextChunk;
            hasCurrentChunk = true;
            ApplyTowerVisibility(currentChunk, force);
            if (!hadPreviousChunk || previousChunk != currentChunk)
                CurrentChunkChanged?.Invoke(previousChunk, currentChunk);
        }

        private void ApplyTowerVisibility(ChunkKey center, bool force)
        {
            ChunkGrid.GetNeighborhood(center, NearChunkRadius, desiredChunks);
            if (!visibilityInitialized || force)
            {
                for (int i = 0; i < cachedChunks.Count; i++)
                    SetChunkObjectsActive(cachedChunks[i], desiredChunks.Contains(cachedChunks[i].Key));
            }
            else
            {
                foreach (ChunkKey loaded in loadedChunks)
                {
                    if (!desiredChunks.Contains(loaded) && chunkLookup.TryGetValue(loaded, out ChunkSceneCache chunk))
                        SetChunkObjectsActive(chunk, false);
                }
                foreach (ChunkKey desired in desiredChunks)
                {
                    if (!loadedChunks.Contains(desired) && chunkLookup.TryGetValue(desired, out ChunkSceneCache chunk))
                        SetChunkObjectsActive(chunk, true);
                }
            }

            loadedChunks.Clear();
            foreach (ChunkKey desired in desiredChunks)
            {
                if (chunkLookup.ContainsKey(desired))
                    loadedChunks.Add(desired);
            }
            visibilityInitialized = true;
        }

        private void RebuildChunkLookup()
        {
            chunkLookup.Clear();
            for (int i = 0; i < cachedChunks.Count; i++)
            {
                ChunkSceneCache chunk = cachedChunks[i];
                if (chunk != null)
                    chunkLookup[chunk.Key] = chunk;
            }
        }

        private static void SetChunkObjectsActive(ChunkSceneCache chunk, bool active)
        {
            IReadOnlyList<GameObject> objects = chunk.GameObjects;
            for (int i = 0; i < objects.Count; i++)
            {
                GameObject target = objects[i];
                if (target != null && target.activeSelf != active)
                    target.SetActive(active);
            }
        }

        private void OnValidate()
        {
            chunkSizeMeters = Mathf.Max(0.01f, chunkSizeMeters);
            floorHeight = Mathf.Max(0.01f, floorHeight);
        }

        private void OnDrawGizmosSelected()
        {
            if (chunkSizeMeters <= 0f || floorHeight <= 0f)
                return;
            Vector3 position = player != null ? player.position : transform.position;
            ChunkKey center = WorldToChunk(position);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);
            for (int floor = center.FloorIndex - NearChunkRadius; floor <= center.FloorIndex + NearChunkRadius; floor++)
            for (int x = center.X - NearChunkRadius; x <= center.X + NearChunkRadius; x++)
            for (int z = center.Z - NearChunkRadius; z <= center.Z + NearChunkRadius; z++)
            {
                Bounds bounds = ChunkGrid.GetWorldBounds(new ChunkKey(floor, x, z), chunkSizeMeters, floorHeight);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }
    }
}
