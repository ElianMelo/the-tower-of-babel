using System;
using System.Collections.Generic;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Players
{
    /// <summary>Client player registry, stable observer priority list, and remote-view pool.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayersManager : MonoBehaviour
    {
        public const int MaximumPriorityPlayers = 100;
        public const int DefaultMaximumSearchDistance = 20;
        public const float NetworkStateInterval = 0.1f;

        [Header("References")]
        [SerializeField] private ChunkManager chunkManager;
        [SerializeField] private Transform localPlayer;
        [SerializeField] private RemotePlayerView remotePlayerPrefab;
        [SerializeField] private Transform remoteVisualParent;

        [Header("Priority")]
        [SerializeField, Range(1, MaximumPriorityPlayers)] private int priorityCapacity = MaximumPriorityPlayers;
        [SerializeField, Range(0, DefaultMaximumSearchDistance)] private int maximumSearchDistance = DefaultMaximumSearchDistance;
        [SerializeField] private Vector2 vacancySearchIntervalSeconds = new(2f, 5f);
        [SerializeField, Min(1)] private int observerAnchorDistanceChunks = 10;
        [SerializeField, Min(1f)] private float fullReevaluationMinutes = 30f;

        [Header("Presentation")]
        [SerializeField, Min(0f)] private float interpolationDelaySeconds = NetworkStateInterval;
        [SerializeField, Range(0, MaximumPriorityPlayers)] private int prewarmVisualCount = 8;

        private readonly Dictionary<uint, PlayerInstance> playersById = new();
        private readonly List<PlayerInstance> players = new();
        private readonly List<PlayerInstance> priorityPlayers = new();
        private readonly HashSet<uint> priorityPlayerIds = new();
        private readonly Dictionary<uint, RemotePlayerView> activeViews = new();
        private readonly Stack<RemotePlayerView> pooledViews = new();
        private readonly List<PlayerInstance> candidateBuffer = new();

        private PlayerInstance localPlayerInstance;
        private PlayerAnimationState localAnimationState;
        private ChunkKey observerAnchorChunk;
        private bool hasObserverAnchor;
        private bool warnedMissingPrefab;
        private bool prioritySearchRequested;
        private float nextLocalSnapshotTime;
        private float nextVacancySearchTime;
        private float nextFullReevaluationTime;

        public PlayerInstance LocalPlayerInstance => localPlayerInstance;
        public IReadOnlyList<PlayerInstance> Players => players;
        public IReadOnlyList<PlayerInstance> PriorityPlayers => priorityPlayers;
        public int PriorityCount => priorityPlayers.Count;
        public int AvailablePrioritySeats => Mathf.Max(0, priorityCapacity - priorityPlayers.Count);
        public int PooledVisualCount => pooledViews.Count;

        public event Action<PlayerInstance> PlayerRegistered;
        public event Action<PlayerInstance> PlayerUnregistered;
        public event Action PriorityListChanged;
        public event Action<PlayerStateSnapshot> LocalSnapshotCreated;

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#else
            ResolveReferences();
#endif
        }

        private void Start()
        {
            PrewarmViews();
            float now = Time.unscaledTime;
            ScheduleVacancySearch(now);
            nextFullReevaluationTime = now + fullReevaluationMinutes * 60f;
        }

        private void Update()
        {
            float now = Time.unscaledTime;
            if (localPlayerInstance != null && localPlayer != null && now >= nextLocalSnapshotTime)
            {
                nextLocalSnapshotTime = now + NetworkStateInterval;
                PlayerStateSnapshot snapshot = localPlayerInstance.CreateNextLocalSnapshot(
                    localPlayer.position, localPlayer.rotation, localAnimationState);
                chunkManager.RefreshTrackedPlayer(localPlayerInstance);
                LocalSnapshotCreated?.Invoke(snapshot);
            }

            if (localPlayerInstance != null && localPlayerInstance.HasChunk)
            {
                ValidatePriorityEnvelope();
                bool anchorExpired = hasObserverAnchor &&
                    ChunkGrid.ManhattanDistance(observerAnchorChunk, localPlayerInstance.CurrentChunk) >= observerAnchorDistanceChunks;
                bool timeExpired = now >= nextFullReevaluationTime;
                if (anchorExpired || timeExpired)
                    ReevaluatePriorityList();
                else if (AvailablePrioritySeats > 0 &&
                         (prioritySearchRequested || now >= nextVacancySearchTime))
                {
                    prioritySearchRequested = false;
                    FillAvailableSeats();
                    ScheduleVacancySearch(now);
                }
            }

            double renderTime = Time.unscaledTimeAsDouble;
            for (int i = 0; i < priorityPlayers.Count; i++)
            {
                if (activeViews.TryGetValue(priorityPlayers[i].PlayerId, out RemotePlayerView view))
                    view.Render(renderTime);
            }
        }

        private void OnDestroy()
        {
            ClearAllPlayers();
            while (pooledViews.Count > 0)
            {
                RemotePlayerView view = pooledViews.Pop();
                if (view != null)
                {
                    if (Application.isPlaying)
                        Destroy(view.gameObject);
                    else
                        DestroyImmediate(view.gameObject);
                }
            }
        }

        public PlayerInstance RegisterLocalPlayer(uint playerId)
        {
            ResolveReferences();
            if (localPlayer == null)
                throw new InvalidOperationException("PlayersManager requires the scene Player transform.");

            if (localPlayerInstance != null && localPlayerInstance.PlayerId != playerId)
                UnregisterPlayer(localPlayerInstance.PlayerId);

            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
            {
                instance = new PlayerInstance(playerId, localPlayer.position, localPlayer.rotation,
                    localAnimationState, isLocal: true);
                AddPlayer(instance);
            }
            else
            {
                instance.IsLocal = true;
            }

            localPlayerInstance = instance;
            chunkManager.RefreshTrackedPlayer(instance);
            observerAnchorChunk = instance.CurrentChunk;
            hasObserverAnchor = true;
            nextLocalSnapshotTime = 0f;
            prioritySearchRequested = false;
            FillAvailableSeats();
            return instance;
        }

        public PlayerInstance RegisterPlayer(PlayerStateSnapshot initialSnapshot, bool isFriend = false)
        {
            if (playersById.TryGetValue(initialSnapshot.PlayerId, out PlayerInstance existing))
            {
                existing.SetFriend(isFriend);
                if (existing.ApplySnapshot(initialSnapshot))
                    chunkManager.RefreshTrackedPlayer(existing);
                return existing;
            }

            PlayerInstance instance = new(initialSnapshot.PlayerId, initialSnapshot.Position,
                initialSnapshot.Rotation, initialSnapshot.AnimationState, isFriend);
            instance.ApplySnapshot(initialSnapshot);
            AddPlayer(instance);
            return instance;
        }

        public bool ApplyPlayerSnapshot(PlayerStateSnapshot snapshot)
        {
            if (!playersById.TryGetValue(snapshot.PlayerId, out PlayerInstance instance))
            {
                RegisterPlayer(snapshot);
                return true;
            }

            if (!instance.ApplySnapshot(snapshot))
                return false;
            chunkManager.RefreshTrackedPlayer(instance);
            return true;
        }

        public bool UnregisterPlayer(uint playerId)
        {
            if (!playersById.Remove(playerId, out PlayerInstance instance))
                return false;

            bool priorityChanged = RemoveFromPriority(instance);
            players.Remove(instance);
            chunkManager.UntrackPlayer(instance);
            instance.MarkDisconnected();
            if (ReferenceEquals(localPlayerInstance, instance))
            {
                localPlayerInstance = null;
                hasObserverAnchor = false;
            }
            PlayerUnregistered?.Invoke(instance);
            if (priorityChanged)
                PriorityListChanged?.Invoke();
            ScheduleVacancySearch(Time.unscaledTime);
            return true;
        }

        public bool TryGetPlayer(uint playerId, out PlayerInstance instance) =>
            playersById.TryGetValue(playerId, out instance);

        public bool SetPlayerFriend(uint playerId, bool isFriend)
        {
            if (!playersById.TryGetValue(playerId, out PlayerInstance instance))
                return false;
            instance.SetFriend(isFriend);

            bool changed = false;
            if (isFriend && !instance.IsLocal && !priorityPlayerIds.Contains(playerId))
            {
                if (AvailablePrioritySeats == 0)
                {
                    PlayerInstance removable = FindLastPrioritizedNonFriend();
                    if (removable != null)
                        changed |= RemoveFromPriority(removable);
                }
                if (AvailablePrioritySeats > 0)
                    changed |= AddToPriority(instance);
            }
            if (changed)
                PriorityListChanged?.Invoke();
            return true;
        }

        public void SetLocalAnimationState(PlayerAnimationState state) => localAnimationState = state;

        public void FillPrioritySeatsNow()
        {
            FillAvailableSeats();
            ScheduleVacancySearch(Time.unscaledTime);
        }

        public void CopyPriorityPlayerIds(List<uint> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));
            results.Clear();
            for (int i = 0; i < priorityPlayers.Count; i++)
                results.Add(priorityPlayers[i].PlayerId);
        }

        public void ClearAllPlayers()
        {
            for (int i = priorityPlayers.Count - 1; i >= 0; i--)
                RemoveFromPriority(priorityPlayers[i]);
            foreach (PlayerInstance instance in players)
                instance.MarkDisconnected();

            playersById.Clear();
            players.Clear();
            chunkManager?.ClearTrackedPlayers();
            localPlayerInstance = null;
            hasObserverAnchor = false;
        }

        private void AddPlayer(PlayerInstance instance)
        {
            playersById.Add(instance.PlayerId, instance);
            players.Add(instance);
            chunkManager.TrackPlayer(instance);
            PlayerRegistered?.Invoke(instance);

            // Roster RPCs may arrive after RegisterLocalPlayer already performed its first
            // search. Defer one search until Update so the complete RPC roster is indexed
            // before we build and send the observer subscription.
            if (!instance.IsLocal && AvailablePrioritySeats > 0)
                prioritySearchRequested = true;
        }

        private bool FillAvailableSeats(bool notifyChange = true)
        {
            if (localPlayerInstance == null || !localPlayerInstance.HasChunk || AvailablePrioritySeats == 0)
                return false;

            bool changed = false;
            for (int i = 0; i < players.Count && AvailablePrioritySeats > 0; i++)
            {
                PlayerInstance instance = players[i];
                if (instance.IsFriend && !instance.IsLocal && !priorityPlayerIds.Contains(instance.PlayerId))
                    changed |= AddToPriority(instance);
            }

            if (AvailablePrioritySeats > 0)
            {
                chunkManager.GetNearestPlayers(localPlayerInstance, -1, maximumSearchDistance, candidateBuffer);
                for (int i = 0; i < candidateBuffer.Count && AvailablePrioritySeats > 0; i++)
                {
                    PlayerInstance candidate = candidateBuffer[i];
                    if (!priorityPlayerIds.Contains(candidate.PlayerId))
                        changed |= AddToPriority(candidate);
                }
            }

            if (changed && notifyChange)
                PriorityListChanged?.Invoke();
            return changed;
        }

        private void ValidatePriorityEnvelope()
        {
            bool changed = false;
            for (int i = priorityPlayers.Count - 1; i >= 0; i--)
            {
                PlayerInstance instance = priorityPlayers[i];
                if (!instance.IsConnected || (!instance.IsFriend &&
                    ChunkGrid.ChebyshevDistance(localPlayerInstance.CurrentChunk, instance.CurrentChunk) > maximumSearchDistance))
                    changed |= RemoveFromPriority(instance);
            }
            if (changed)
            {
                PriorityListChanged?.Invoke();
                ScheduleVacancySearch(Time.unscaledTime);
            }
        }

        private void ReevaluatePriorityList()
        {
            bool changed = false;
            for (int i = priorityPlayers.Count - 1; i >= 0; i--)
            {
                if (!priorityPlayers[i].IsFriend)
                    changed |= RemoveFromPriority(priorityPlayers[i]);
            }
            observerAnchorChunk = localPlayerInstance.CurrentChunk;
            hasObserverAnchor = true;
            nextFullReevaluationTime = Time.unscaledTime + fullReevaluationMinutes * 60f;
            changed |= FillAvailableSeats(false);
            if (changed)
                PriorityListChanged?.Invoke();
        }

        private bool AddToPriority(PlayerInstance instance)
        {
            if (instance == null || instance.IsLocal || !instance.IsConnected || AvailablePrioritySeats == 0
                || !priorityPlayerIds.Add(instance.PlayerId))
                return false;

            priorityPlayers.Add(instance);
            RemotePlayerView view = AcquireView();
            if (view != null)
            {
                view.gameObject.SetActive(true);
                view.Bind(instance, interpolationDelaySeconds);
                activeViews.Add(instance.PlayerId, view);
            }
            return true;
        }

        private bool RemoveFromPriority(PlayerInstance instance)
        {
            if (instance == null || !priorityPlayerIds.Remove(instance.PlayerId))
                return false;
            priorityPlayers.Remove(instance);

            if (activeViews.Remove(instance.PlayerId, out RemotePlayerView view))
                ReleaseView(view);
            return true;
        }

        private PlayerInstance FindLastPrioritizedNonFriend()
        {
            for (int i = priorityPlayers.Count - 1; i >= 0; i--)
            {
                if (!priorityPlayers[i].IsFriend)
                    return priorityPlayers[i];
            }
            return null;
        }

        private RemotePlayerView AcquireView()
        {
            while (pooledViews.Count > 0)
            {
                RemotePlayerView pooled = pooledViews.Pop();
                if (pooled != null)
                    return pooled;
            }
            if (remotePlayerPrefab != null)
                return Instantiate(remotePlayerPrefab, remoteVisualParent);

            if (!warnedMissingPrefab)
            {
                warnedMissingPrefab = true;
                Debug.LogWarning("PlayersManager has no Remote Player Prefab; priority data will work without visuals.", this);
            }
            return null;
        }

        private void ReleaseView(RemotePlayerView view)
        {
            if (view == null)
                return;
            view.Unbind();
            view.gameObject.SetActive(false);
            view.transform.SetParent(remoteVisualParent, false);
            pooledViews.Push(view);
        }

        private void PrewarmViews()
        {
            if (remotePlayerPrefab == null)
                return;
            for (int i = pooledViews.Count; i < prewarmVisualCount; i++)
            {
                RemotePlayerView view = Instantiate(remotePlayerPrefab, remoteVisualParent);
                view.gameObject.SetActive(false);
                pooledViews.Push(view);
            }
        }

        private void ResolveReferences()
        {
            if (chunkManager == null)
                chunkManager = FindFirstObjectByType<ChunkManager>();
            if (localPlayer == null)
            {
                GameObject playerObject = GameObject.Find("Player");
                if (playerObject != null)
                    localPlayer = playerObject.transform;
            }
            if (remoteVisualParent == null)
                remoteVisualParent = transform;
        }

        private void ScheduleVacancySearch(float now)
        {
            float minimum = Mathf.Max(0.1f, Mathf.Min(vacancySearchIntervalSeconds.x, vacancySearchIntervalSeconds.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(vacancySearchIntervalSeconds.x, vacancySearchIntervalSeconds.y));
            nextVacancySearchTime = now + UnityEngine.Random.Range(minimum, maximum);
        }

        private void OnValidate()
        {
            priorityCapacity = Mathf.Clamp(priorityCapacity, 1, MaximumPriorityPlayers);
            maximumSearchDistance = Mathf.Clamp(maximumSearchDistance, 0, DefaultMaximumSearchDistance);
            observerAnchorDistanceChunks = Mathf.Max(1, observerAnchorDistanceChunks);
            fullReevaluationMinutes = Mathf.Max(1f, fullReevaluationMinutes);
            interpolationDelaySeconds = Mathf.Max(0f, interpolationDelaySeconds);
        }
    }
}
