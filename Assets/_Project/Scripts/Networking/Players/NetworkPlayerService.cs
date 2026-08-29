using System;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using TowerOfBabel.Players;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Networking.Players
{
    /// <summary>
    /// One global FishNet bridge for player presence and batched state. Player avatars are
    /// not NetworkObjects; clients create pooled presentation views from PlayersManager.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkPlayerService : NetworkBehaviour
    {
        private sealed class ObserverSubscription
        {
            public uint[] PlayerIds = Array.Empty<uint>();
            public PlayerStateSnapshot[] StateBuffer = Array.Empty<PlayerStateSnapshot>();
        }

        [SerializeField] private PlayersManager playersManager;
        [SerializeField, Min(0.01f)] private float chunkSizeMeters = ChunkGrid.DefaultChunkSizeMeters;
        [SerializeField, Min(0.01f)] private float floorHeight = 6f;
        [SerializeField, Min(1f)] private float stateSendRateHz = 10f;

        private readonly ServerPlayerRegistry serverRegistry = new();
        private readonly Dictionary<int, ObserverSubscription> observerSubscriptions = new();
        private readonly List<PlayerStateSnapshot> rosterBuffer = new();
        private readonly List<uint> localPriorityIds = new();
        private float nextServerBatchTime;
        private uint serverBatchSequence;

        private void Awake()
        {
            if (playersManager == null)
                playersManager = FindFirstObjectByType<PlayersManager>();
        }

        private void Update()
        {
            if (!IsServerStarted || Time.unscaledTime < nextServerBatchTime)
                return;

            nextServerBatchTime = Time.unscaledTime + 1f / Mathf.Max(1f, stateSendRateHz);
            SendObservedStateBatches();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
            nextServerBatchTime = Time.unscaledTime;
        }

        public override void OnStopServer()
        {
            ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            observerSubscriptions.Clear();
            serverRegistry.Clear();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (playersManager == null)
                playersManager = FindFirstObjectByType<PlayersManager>();
            if (playersManager == null)
                return;

            playersManager.LocalSnapshotCreated += HandleLocalSnapshotCreated;
            playersManager.PriorityListChanged += HandlePriorityListChanged;

            NetworkConnection connection = ClientManager.Connection;
            if (connection != null && connection.ClientId >= 0)
            {
                playersManager.RegisterLocalPlayer((uint)connection.ClientId);
                RequestRosterServerRpc();
            }
        }

        public override void OnStopClient()
        {
            if (playersManager != null)
            {
                playersManager.LocalSnapshotCreated -= HandleLocalSnapshotCreated;
                playersManager.PriorityListChanged -= HandlePriorityListChanged;
                playersManager.ClearAllPlayers();
            }
            base.OnStopClient();
        }

        private void HandleRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (connection == null || connection.ClientId < 0)
                return;

            uint playerId = (uint)connection.ClientId;
            if (args.ConnectionState == RemoteConnectionState.Started)
            {
                PlayerInstance instance = serverRegistry.Register(playerId, Vector3.zero, Quaternion.identity);
                observerSubscriptions[connection.ClientId] = new ObserverSubscription();

                // Existing observers learn about the new player immediately. The joining
                // client requests its roster from OnStartClient, after this NetworkObject
                // is guaranteed to be spawned and observable on that client.
                PlayerJoinedObserversRpc(instance.CurrentSnapshot);
            }
            else if (args.ConnectionState == RemoteConnectionState.Stopped)
            {
                observerSubscriptions.Remove(connection.ClientId);
                if (serverRegistry.Unregister(playerId))
                    PlayerLeftObserversRpc(playerId);
            }
        }

        private void HandleLocalSnapshotCreated(PlayerStateSnapshot snapshot)
        {
            if (IsClientStarted)
                SubmitLocalStateServerRpc(snapshot);
        }

        private void HandlePriorityListChanged()
        {
            if (!IsClientStarted || playersManager == null)
                return;

            playersManager.CopyPriorityPlayerIds(localPriorityIds);
            SetObservedPlayersServerRpc(localPriorityIds.ToArray());
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestRosterServerRpc(NetworkConnection sender = null)
        {
            if (sender == null || sender.ClientId < 0)
                return;

            serverRegistry.CopySnapshots(rosterBuffer);
            SendRosterTargetRpc(sender, rosterBuffer.ToArray());
        }

        [ServerRpc(RequireOwnership = false)]
        private void SubmitLocalStateServerRpc(PlayerStateSnapshot snapshot, NetworkConnection sender = null)
        {
            if (sender == null || sender.ClientId < 0)
                return;

            uint authenticatedPlayerId = (uint)sender.ClientId;
            ChunkKey previousChunk = default;
            bool hadPrevious = serverRegistry.TryGet(authenticatedPlayerId, out PlayerInstance previousInstance);
            if (hadPrevious)
                previousChunk = ChunkGrid.WorldToChunk(previousInstance.Position, chunkSizeMeters, floorHeight);

            if (!serverRegistry.ApplySnapshot(authenticatedPlayerId, snapshot, out PlayerInstance instance))
                return;

            ChunkKey currentChunk = ChunkGrid.WorldToChunk(instance.Position, chunkSizeMeters, floorHeight);
            if (!hadPrevious || previousChunk != currentChunk)
                PlayerChunkChangedObserversRpc(instance.CurrentSnapshot);
        }

        [ServerRpc(RequireOwnership = false)]
        private void SetObservedPlayersServerRpc(uint[] requestedPlayerIds, NetworkConnection sender = null)
        {
            if (sender == null || sender.ClientId < 0)
                return;

            int requestedCount = requestedPlayerIds?.Length ?? 0;
            int maximum = Mathf.Min(requestedCount, PlayersManager.MaximumPriorityPlayers);
            List<uint> validated = new(maximum);
            uint senderId = (uint)sender.ClientId;
            for (int i = 0; i < maximum; i++)
            {
                uint playerId = requestedPlayerIds[i];
                if (playerId == senderId || validated.Contains(playerId) || !serverRegistry.TryGet(playerId, out _))
                    continue;
                validated.Add(playerId);
            }

            ObserverSubscription subscription = new()
            {
                PlayerIds = validated.ToArray(),
                StateBuffer = new PlayerStateSnapshot[validated.Count]
            };
            observerSubscriptions[sender.ClientId] = subscription;
        }

        private void SendObservedStateBatches()
        {
            serverBatchSequence++;
            foreach (NetworkConnection connection in ServerManager.Clients.Values)
            {
                if (connection == null || !observerSubscriptions.TryGetValue(connection.ClientId, out ObserverSubscription subscription))
                    continue;

                int count = 0;
                for (int i = 0; i < subscription.PlayerIds.Length; i++)
                {
                    if (serverRegistry.TryGet(subscription.PlayerIds[i], out PlayerInstance instance))
                        subscription.StateBuffer[count++] = instance.CurrentSnapshot;
                }
                if (count > 0)
                    SendStateBatchTargetRpc(connection, serverBatchSequence, subscription.StateBuffer, count);
            }
        }

        [TargetRpc]
        private void SendRosterTargetRpc(NetworkConnection connection, PlayerStateSnapshot[] snapshots)
        {
            if (playersManager == null || snapshots == null)
                return;
            for (int i = 0; i < snapshots.Length; i++)
                playersManager.RegisterPlayer(snapshots[i]);
        }

        [ObserversRpc]
        private void PlayerJoinedObserversRpc(PlayerStateSnapshot snapshot)
        {
            playersManager?.RegisterPlayer(snapshot);
        }

        [ObserversRpc]
        private void PlayerLeftObserversRpc(uint playerId)
        {
            playersManager?.UnregisterPlayer(playerId);
        }

        [ObserversRpc]
        private void PlayerChunkChangedObserversRpc(PlayerStateSnapshot snapshot)
        {
            playersManager?.ApplyPlayerSnapshot(snapshot);
        }

        [TargetRpc]
        private void SendStateBatchTargetRpc(NetworkConnection connection, uint batchSequence,
            PlayerStateSnapshot[] snapshots, int count)
        {
            if (playersManager == null || snapshots == null)
                return;
            int validCount = Mathf.Min(count, snapshots.Length);
            for (int i = 0; i < validCount; i++)
                playersManager.ApplyPlayerSnapshot(snapshots[i]);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            chunkSizeMeters = Mathf.Max(0.01f, chunkSizeMeters);
            floorHeight = Mathf.Max(0.01f, floorHeight);
            stateSendRateHz = Mathf.Max(1f, stateSendRateHz);
        }
    }
}
