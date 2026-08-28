using System;
using System.Collections.Generic;
using NaughtyAttributes;
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

    /// <summary>
    /// Client-side high-detail tower streaming and remote-player visual prioritization.
    /// The dedicated server does not execute this component.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ChunkManager : MonoBehaviour
    {
        public const int NearChunkRadius = 1;
        public const int NearChunkSlotCount = 27;
        public const int DefaultPlayerVisualBudget = 100;

        [Header("Tower Chunk Cache")]
        [Tooltip("Every descendant of this object is assigned to a chunk from its world position.")]
        [SerializeField] private GameObject towerRoot;
        [SerializeField, Min(0.01f)] private float chunkSizeMeters = ChunkGrid.DefaultChunkSizeMeters;
        [SerializeField, Min(0.01f)] private float floorHeight = 6f;
        [SerializeField] private List<ChunkSceneCache> cachedChunks = new();

        [Header("Local Player")]
        [Tooltip("Defaults to the active scene object named Player when left empty.")]
        [SerializeField] private Transform player;

        [Header("Remote Player Visuals")]
        [SerializeField, Range(0, DefaultPlayerVisualBudget)]
        private int playerVisualBudget = DefaultPlayerVisualBudget;

        private readonly Dictionary<ChunkKey, ChunkSceneCache> chunkLookup = new();
        private readonly HashSet<ChunkKey> loadedChunks = new();
        private readonly HashSet<ChunkKey> desiredChunks = new();
        private readonly Dictionary<ulong, PlayerInstance> playersById = new();
        private readonly List<PlayerInstance> playerInstances = new();
        private readonly List<PlayerInstance> prioritizedPlayers = new();
        private readonly List<PlayerInstance> queryBuffer = new();

        private PlayerInstance localPlayerInstance;
        private ChunkKey currentChunk;
        private bool hasCurrentChunk;
        private bool visibilityInitialized;
        private bool priorityDirty = true;

        public float ChunkSizeMeters => chunkSizeMeters;
        public float FloorHeight => floorHeight;
        public Transform PlayerTransform => player;
        public PlayerInstance LocalPlayerInstance => localPlayerInstance;
        public ChunkKey CurrentChunk => currentChunk;
        public bool HasCurrentChunk => hasCurrentChunk;
        public IReadOnlyList<ChunkSceneCache> CachedChunks => cachedChunks;
        public IReadOnlyList<PlayerInstance> PlayerInstances => playerInstances;
        public IReadOnlyList<PlayerInstance> PrioritizedPlayers => prioritizedPlayers;

        public event Action<ChunkKey, ChunkKey> CurrentChunkChanged;
        public event Action PriorityListChanged;

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

            localPlayerInstance?.UpdateState(player.position, player.rotation);
            RefreshFromPlayer(false);

            if (priorityDirty)
                RefreshPlayerPriorities(player.position);
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

        public ChunkKey WorldToChunk(Vector3 worldPosition)
        {
            return ChunkGrid.WorldToChunk(worldPosition, chunkSizeMeters, floorHeight);
        }

        public int CalculateChunkDistance(Vector3 firstWorldPosition, Vector3 secondWorldPosition)
        {
            return ChunkGrid.ChebyshevDistance(WorldToChunk(firstWorldPosition), WorldToChunk(secondWorldPosition));
        }

        public int CalculateChunkDistance(ChunkKey first, ChunkKey second)
        {
            return ChunkGrid.ChebyshevDistance(first, second);
        }

        public bool IsChunkLoaded(ChunkKey key)
        {
            return loadedChunks.Contains(key);
        }

        public void RefreshTowerVisibility()
        {
            if (player == null)
                ResolveScenePlayer();

            if (player != null)
                RefreshFromPlayer(true);
        }

        public PlayerInstance RegisterPlayer(
            ulong playerId,
            Vector3 position,
            Quaternion rotation,
            GameObject visualRoot = null,
            bool isFriend = false)
        {
            if (playersById.TryGetValue(playerId, out PlayerInstance existing))
            {
                ChunkKey previousChunk = WorldToChunk(existing.Position);
                bool friendChanged = existing.IsFriend != isFriend;
                existing.SetFriend(isFriend);
                existing.UpdateState(position, rotation);
                if (visualRoot != null && existing.VisualRoot != visualRoot)
                    existing.BindVisual(visualRoot);

                if (friendChanged || previousChunk != WorldToChunk(position))
                    priorityDirty = true;
                return existing;
            }

            PlayerInstance instance = new(playerId, position, rotation, isFriend);
            if (visualRoot != null)
                instance.BindVisual(visualRoot);

            playersById.Add(playerId, instance);
            playerInstances.Add(instance);
            priorityDirty = true;
            return instance;
        }

        public bool UpdatePlayerState(ulong playerId, Vector3 position, Quaternion rotation)
        {
            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
                return false;

            ChunkKey previousChunk = WorldToChunk(instance.Position);
            instance.UpdateState(position, rotation);
            if (previousChunk != WorldToChunk(position))
                priorityDirty = true;
            return true;
        }

        public bool BindPlayerVisual(ulong playerId, GameObject visualRoot)
        {
            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
                return false;

            instance.BindVisual(visualRoot);
            return true;
        }

        public bool SetPlayerFriend(ulong playerId, bool isFriend)
        {
            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
                return false;
            if (instance.IsFriend == isFriend)
                return true;

            instance.SetFriend(isFriend);
            priorityDirty = true;
            return true;
        }

        public bool TryGetPlayer(ulong playerId, out PlayerInstance instance)
        {
            return playersById.TryGetValue(playerId, out instance);
        }

        public bool UnregisterPlayer(ulong playerId, bool disableVisual = true)
        {
            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
                return false;

            playersById.Remove(playerId);
            playerInstances.Remove(instance);
            prioritizedPlayers.Remove(instance);
            instance.UnbindVisual(disableVisual);
            priorityDirty = true;
            return true;
        }

        public void ClearPlayers(bool disableVisuals = true)
        {
            for (int i = 0; i < playerInstances.Count; i++)
                playerInstances[i].UnbindVisual(disableVisuals);

            playersById.Clear();
            playerInstances.Clear();
            prioritizedPlayers.Clear();
            queryBuffer.Clear();
            priorityDirty = false;
            PriorityListChanged?.Invoke();
        }

        public void RefreshPlayerPriorities(Vector3 contextPosition)
        {
            queryBuffer.Clear();
            queryBuffer.AddRange(playerInstances);
            queryBuffer.Sort((first, second) => ComparePlayerPriority(first, second, contextPosition));

            prioritizedPlayers.Clear();
            int targetCount = Mathf.Min(Mathf.Clamp(playerVisualBudget, 0, DefaultPlayerVisualBudget), queryBuffer.Count);
            for (int i = 0; i < queryBuffer.Count; i++)
            {
                PlayerInstance instance = queryBuffer[i];
                bool prioritized = i < targetCount;
                instance.SetVisualPriority(prioritized);
                if (prioritized)
                    prioritizedPlayers.Add(instance);
            }

            priorityDirty = false;
            PriorityListChanged?.Invoke();
        }

        public PlayerInstance GetNearestPlayer(Vector3 contextPosition, bool prioritizedOnly = false)
        {
            queryBuffer.Clear();
            AddQueryCandidates(null, prioritizedOnly, queryBuffer);
            int selectedIndex = FindRandomNearestIndex(queryBuffer, WorldToChunk(contextPosition));
            return selectedIndex >= 0 ? queryBuffer[selectedIndex] : null;
        }

        public int GetNearestPlayers(
            PlayerInstance context,
            int maximumCount,
            List<PlayerInstance> results,
            bool prioritizedOnly = false)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            if (maximumCount <= 0)
                return 0;

            queryBuffer.Clear();
            AddQueryCandidates(context, prioritizedOnly, queryBuffer);
            int resultCount = Mathf.Min(maximumCount, queryBuffer.Count);
            for (int i = 0; i < resultCount; i++)
            {
                int selectedIndex = FindRandomNearestIndex(queryBuffer, WorldToChunk(context.Position));
                PlayerInstance selected = queryBuffer[selectedIndex];
                results.Add(selected);

                int lastIndex = queryBuffer.Count - 1;
                queryBuffer[selectedIndex] = queryBuffer[lastIndex];
                queryBuffer.RemoveAt(lastIndex);
            }
            return resultCount;
        }

        public int GetPlayersInChunk(ChunkKey key, List<PlayerInstance> results, bool prioritizedOnly = false)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            IReadOnlyList<PlayerInstance> source = prioritizedOnly ? prioritizedPlayers : playerInstances;
            for (int i = 0; i < source.Count; i++)
            {
                PlayerInstance instance = source[i];
                if (WorldToChunk(instance.Position) == key)
                    results.Add(instance);
            }

            return results.Count;
        }

        public int GetPlayersWithinChunkDistance(
            ChunkKey center,
            int maximumDistance,
            List<PlayerInstance> results,
            bool prioritizedOnly = false)
        {
            if (maximumDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumDistance));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            IReadOnlyList<PlayerInstance> source = prioritizedOnly ? prioritizedPlayers : playerInstances;
            for (int i = 0; i < source.Count; i++)
            {
                PlayerInstance instance = source[i];
                if (CalculateChunkDistance(center, WorldToChunk(instance.Position)) <= maximumDistance)
                    results.Add(instance);
            }

            return results.Count;
        }

        private void ResolveScenePlayer()
        {
            if (player == null)
            {
                GameObject playerObject = GameObject.Find("Player");
                if (playerObject != null)
                    player = playerObject.transform;
            }

            if (player != null && (localPlayerInstance == null || localPlayerInstance.VisualRoot != player.gameObject))
                localPlayerInstance = PlayerInstance.CreateLocal(player);
        }

        private void RefreshFromPlayer(bool force)
        {
            if (player == null)
                return;

            ChunkKey nextChunk = WorldToChunk(player.position);
            if (!force && hasCurrentChunk && nextChunk == currentChunk)
                return;

            ChunkKey previousChunk = currentChunk;
            currentChunk = nextChunk;
            bool hadPreviousChunk = hasCurrentChunk;
            hasCurrentChunk = true;

            ApplyTowerVisibility(currentChunk, force);
            priorityDirty = true;

            if (!hadPreviousChunk || previousChunk != currentChunk)
                CurrentChunkChanged?.Invoke(previousChunk, currentChunk);
        }

        private void ApplyTowerVisibility(ChunkKey center, bool force)
        {
            ChunkGrid.GetNeighborhood(center, NearChunkRadius, desiredChunks);

            if (!visibilityInitialized || force)
            {
                for (int i = 0; i < cachedChunks.Count; i++)
                {
                    ChunkSceneCache chunk = cachedChunks[i];
                    SetChunkObjectsActive(chunk, desiredChunks.Contains(chunk.Key));
                }
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

        private void AddQueryCandidates(PlayerInstance excluded, bool prioritizedOnly, List<PlayerInstance> results)
        {
            IReadOnlyList<PlayerInstance> source = prioritizedOnly ? prioritizedPlayers : playerInstances;
            for (int i = 0; i < source.Count; i++)
            {
                PlayerInstance instance = source[i];
                if (instance != excluded)
                    results.Add(instance);
            }
        }

        private int ComparePlayerPriority(PlayerInstance first, PlayerInstance second, Vector3 contextPosition)
        {
            int friendComparison = second.IsFriend.CompareTo(first.IsFriend);
            if (friendComparison != 0)
                return friendComparison;

            ChunkKey contextChunk = WorldToChunk(contextPosition);
            int firstChunkDistance = CalculateChunkDistance(contextChunk, WorldToChunk(first.Position));
            int secondChunkDistance = CalculateChunkDistance(contextChunk, WorldToChunk(second.Position));
            int chunkComparison = firstChunkDistance.CompareTo(secondChunkDistance);
            if (chunkComparison != 0)
                return chunkComparison;

            float firstPhysicalDistance = (first.Position - contextPosition).sqrMagnitude;
            float secondPhysicalDistance = (second.Position - contextPosition).sqrMagnitude;
            int physicalComparison = firstPhysicalDistance.CompareTo(secondPhysicalDistance);
            return physicalComparison != 0
                ? physicalComparison
                : first.PlayerId.CompareTo(second.PlayerId);
        }

        private int FindRandomNearestIndex(List<PlayerInstance> candidates, ChunkKey contextChunk)
        {
            int selectedIndex = -1;
            int nearestChunkDistance = int.MaxValue;
            int equalDistanceCount = 0;

            for (int i = 0; i < candidates.Count; i++)
            {
                int chunkDistance = CalculateChunkDistance(contextChunk, WorldToChunk(candidates[i].Position));
                if (chunkDistance < nearestChunkDistance)
                {
                    nearestChunkDistance = chunkDistance;
                    selectedIndex = i;
                    equalDistanceCount = 1;
                }
                else if (chunkDistance == nearestChunkDistance)
                {
                    equalDistanceCount++;
                    if (UnityEngine.Random.Range(0, equalDistanceCount) == 0)
                        selectedIndex = i;
                }
            }

            return selectedIndex;
        }

        private void OnValidate()
        {
            chunkSizeMeters = Mathf.Max(0.01f, chunkSizeMeters);
            floorHeight = Mathf.Max(0.01f, floorHeight);
            playerVisualBudget = Mathf.Clamp(playerVisualBudget, 0, DefaultPlayerVisualBudget);
            priorityDirty = true;
        }

        private void OnDrawGizmosSelected()
        {
            if (chunkSizeMeters <= 0f || floorHeight <= 0f)
                return;

            Vector3 position = player != null ? player.position : transform.position;
            ChunkKey center = WorldToChunk(position);
            Gizmos.color = new Color(0.2f, 0.75f, 1f, 0.35f);

            for (int floor = center.FloorIndex - NearChunkRadius; floor <= center.FloorIndex + NearChunkRadius; floor++)
            {
                for (int x = center.X - NearChunkRadius; x <= center.X + NearChunkRadius; x++)
                {
                    for (int z = center.Z - NearChunkRadius; z <= center.Z + NearChunkRadius; z++)
                    {
                        Bounds bounds = ChunkGrid.GetWorldBounds(new ChunkKey(floor, x, z), chunkSizeMeters, floorHeight);
                        Gizmos.DrawWireCube(bounds.center, bounds.size);
                    }
                }
            }
        }
    }
}
