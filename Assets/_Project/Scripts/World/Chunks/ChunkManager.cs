using System;
using System.Collections.Generic;
using NaughtyAttributes;
using TowerOfBabel.Players;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public sealed class ChunkSceneCache
    {
        [SerializeField] private ChunkKey key;
        [SerializeField] private List<ChunkAssetData> assets = new();

        public ChunkKey Key => key;
        public IReadOnlyList<ChunkAssetData> Assets => assets;

        internal ChunkSceneCache(ChunkKey key, List<ChunkAssetData> assets)
        {
            this.key = key;
            this.assets = assets;
        }

        internal bool TrySetStage(int localIndex, byte stage)
        {
            if ((uint)localIndex >= (uint)assets.Count)
                return false;

            ChunkAssetData asset = assets[localIndex];
            asset.SetStage(stage);
            assets[localIndex] = asset;
            return true;
        }
    }

    /// <summary>Owns tower visibility and the chunk-keyed spatial index.</summary>
    [DisallowMultipleComponent]
    public sealed class ChunkManager : MonoBehaviour
    {
        public const int NearChunkRadius = 1;
        public const int NearChunkSlotCount = 27;

        [Header("Tower Chunk Cache")]
        [Tooltip("Temporary authoring assets under this object are scanned, cached as data, then destroyed.")]
        [SerializeField] private GameObject towerRoot;
        [SerializeField, Min(0.01f)] private float chunkSizeMeters = ChunkGrid.DefaultChunkSizeMeters;
        [SerializeField, Min(0.01f)] private float floorHeight = 6f;
        [SerializeField] private List<ChunkSceneCache> cachedChunks = new();
        [SerializeField] private TowerAssetPrefabSet assetPrefabs = new();
        [Tooltip("Component implementing IFarChunkRenderer. Defaults to one on this GameObject.")]
        [SerializeField] private MonoBehaviour farChunkRendererComponent;
        [Tooltip("Growable per-type pool used for the 3x3x3 near neighborhood.")]
        [SerializeField] private NearTowerAssetPool nearAssetPool;

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
        private IFarChunkRenderer farChunkRenderer;

        public float ChunkSizeMeters => chunkSizeMeters;
        public float FloorHeight => floorHeight;
        public Transform PlayerTransform => player;
        public GameObject TowerRoot => towerRoot;
        public ChunkKey CurrentChunk => currentChunk;
        public bool HasCurrentChunk => hasCurrentChunk;
        public IReadOnlyList<ChunkSceneCache> CachedChunks => cachedChunks;
        public int CachedAssetCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < cachedChunks.Count; i++)
                    count += cachedChunks[i]?.Assets.Count ?? 0;
                return count;
            }
        }
        public TowerAssetPrefabSet AssetPrefabs => assetPrefabs;
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
            ResolveFarChunkRenderer();
            ResolveNearAssetPool();
            farChunkRenderer?.SetVisible(true);
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

            Dictionary<ChunkKey, List<TowerAsset>> groupedAssets = new();
            TowerAsset[] towerAssets = towerRoot.GetComponentsInChildren<TowerAsset>(true);
            for (int i = 0; i < towerAssets.Length; i++)
            {
                TowerAsset asset = towerAssets[i];
                ChunkKey key = WorldToChunk(asset.transform.position);
                if (!groupedAssets.TryGetValue(key, out List<TowerAsset> assetsInChunk))
                {
                    assetsInChunk = new List<TowerAsset>();
                    groupedAssets.Add(key, assetsInChunk);
                }
                assetsInChunk.Add(asset);
            }

            List<ChunkKey> keys = new(groupedAssets.Keys);
            keys.Sort();
            for (int i = 0; i < keys.Count; i++)
            {
                ChunkKey key = keys[i];
                List<TowerAsset> assetsInChunk = groupedAssets[key];
                assetsInChunk.Sort(CompareTowerAssets);
                List<ChunkAssetData> assetData = new(assetsInChunk.Count);
                for (int assetIndex = 0; assetIndex < assetsInChunk.Count; assetIndex++)
                {
                    TowerAsset asset = assetsInChunk[assetIndex];
                    Transform assetTransform = asset.transform;
                    assetData.Add(new ChunkAssetData(
                        assetIndex,
                        asset.AssetType,
                        assetTransform.position,
                        assetTransform.rotation,
                        assetTransform.lossyScale));
                }
                cachedChunks.Add(new ChunkSceneCache(key, assetData));
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
            Debug.Log($"Cached {towerAssets.Length} typed tower assets into {cachedChunks.Count} chunks.", this);
        }

        public void ConfigureAssetPrefabs(TowerGenerator generator)
        {
            if (generator == null)
                throw new ArgumentNullException(nameof(generator));

            assetPrefabs.SetPrefab(TowerAssetType.Floor, generator.GetModelPrefab(TowerAssetType.Floor));
            assetPrefabs.SetPrefab(TowerAssetType.Stair, generator.GetModelPrefab(TowerAssetType.Stair));
            assetPrefabs.SetPrefab(TowerAssetType.Pillar, generator.GetModelPrefab(TowerAssetType.Pillar));
            assetPrefabs.SetPrefab(TowerAssetType.Arch, generator.GetModelPrefab(TowerAssetType.Arch));
        }

        public bool SetAssetStage(ChunkKey key, int localIndex, byte stage)
        {
            if (!chunkLookup.TryGetValue(key, out ChunkSceneCache chunk) || !chunk.TrySetStage(localIndex, stage))
                return false;

            if (loadedChunks.Contains(key))
                RefreshNearPool();
            else
            {
                SetFarChunkVisible(chunk, false);
                SetFarChunkVisible(chunk, true);
            }
            return true;
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
            ResolveFarChunkRenderer();
            ChunkGrid.GetNeighborhood(center, NearChunkRadius, desiredChunks);
            if (!visibilityInitialized || force)
            {
                for (int i = 0; i < cachedChunks.Count; i++)
                {
                    ChunkSceneCache chunk = cachedChunks[i];
                    bool isNear = desiredChunks.Contains(chunk.Key);
                    SetFarChunkVisible(chunk, !isNear);
                }
            }
            else
            {
                foreach (ChunkKey loaded in loadedChunks)
                {
                    if (!desiredChunks.Contains(loaded) && chunkLookup.TryGetValue(loaded, out ChunkSceneCache chunk))
                    {
                        SetFarChunkVisible(chunk, true);
                    }
                }
                foreach (ChunkKey desired in desiredChunks)
                {
                    if (!loadedChunks.Contains(desired) && chunkLookup.TryGetValue(desired, out ChunkSceneCache chunk))
                    {
                        SetFarChunkVisible(chunk, false);
                    }
                }
            }

            loadedChunks.Clear();
            foreach (ChunkKey desired in desiredChunks)
            {
                if (chunkLookup.ContainsKey(desired))
                    loadedChunks.Add(desired);
            }
            RefreshNearPool();
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

        private void ResolveFarChunkRenderer()
        {
            if (farChunkRenderer != null)
                return;

            if (farChunkRendererComponent != null)
            {
                farChunkRenderer = farChunkRendererComponent as IFarChunkRenderer;
                if (farChunkRenderer == null)
                    Debug.LogError($"{farChunkRendererComponent.GetType().Name} does not implement IFarChunkRenderer.", this);
                return;
            }

            MonoBehaviour[] components = GetComponents<MonoBehaviour>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] is IFarChunkRenderer renderer)
                {
                    farChunkRendererComponent = components[i];
                    farChunkRenderer = renderer;
                    return;
                }
            }
        }

        private void ResolveNearAssetPool()
        {
            if (nearAssetPool == null)
                nearAssetPool = GetComponent<NearTowerAssetPool>();
            if (nearAssetPool != null)
                nearAssetPool.Configure(assetPrefabs);
        }

        private void RefreshNearPool()
        {
            ResolveNearAssetPool();
            if (nearAssetPool == null)
                return;

            nearAssetPool.BeginUpdate();
            foreach (ChunkKey loaded in loadedChunks)
            {
                if (chunkLookup.TryGetValue(loaded, out ChunkSceneCache chunk))
                    nearAssetPool.AddChunk(chunk);
            }
            nearAssetPool.EndUpdate();
        }

        private void SetFarChunkVisible(ChunkSceneCache chunk, bool visible)
        {
            if (farChunkRenderer == null)
                return;

            if (visible)
            {
                FarChunkSnapshot snapshot = new(chunk.Key, chunk.Assets);
                farChunkRenderer.LoadChunk(in snapshot);
            }
            else
            {
                farChunkRenderer.RemoveChunk(chunk.Key);
            }
        }

        private static int CompareTowerAssets(TowerAsset first, TowerAsset second)
        {
            int comparison = first.AssetType.CompareTo(second.AssetType);
            if (comparison != 0)
                return comparison;

            comparison = CompareVector(first.transform.position, second.transform.position);
            if (comparison != 0)
                return comparison;

            comparison = CompareQuaternion(first.transform.rotation, second.transform.rotation);
            if (comparison != 0)
                return comparison;

            comparison = CompareVector(first.transform.lossyScale, second.transform.lossyScale);
            if (comparison != 0)
                return comparison;

            comparison = string.CompareOrdinal(first.name, second.name);
            return comparison != 0
                ? comparison
                : first.transform.GetSiblingIndex().CompareTo(second.transform.GetSiblingIndex());
        }

        private static int CompareVector(Vector3 first, Vector3 second)
        {
            int comparison = first.x.CompareTo(second.x);
            if (comparison != 0)
                return comparison;
            comparison = first.y.CompareTo(second.y);
            return comparison != 0 ? comparison : first.z.CompareTo(second.z);
        }

        private static int CompareQuaternion(Quaternion first, Quaternion second)
        {
            int comparison = first.x.CompareTo(second.x);
            if (comparison != 0)
                return comparison;
            comparison = first.y.CompareTo(second.y);
            if (comparison != 0)
                return comparison;
            comparison = first.z.CompareTo(second.z);
            return comparison != 0 ? comparison : first.w.CompareTo(second.w);
        }

        private void OnDisable()
        {
            farChunkRenderer?.SetVisible(false);
            nearAssetPool?.ClearVisibleAssets();
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
