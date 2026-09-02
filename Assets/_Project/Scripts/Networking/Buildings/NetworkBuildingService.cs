using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using TowerOfBabel.Buildings;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Networking.Buildings
{
    [DisallowMultipleComponent]
    public sealed class NetworkBuildingService : NetworkBehaviour
    {
        private sealed class ActiveBuild
        {
            public ChunkKey ChunkKey;
            public int LocalIndex;
            public Coroutine Routine;
        }

        public static NetworkBuildingService Instance { get; private set; }

        [SerializeField] private ChunkManager chunkManager;
        [SerializeField, Min(0.1f)] private float maximumInteractionDistance = 5.5f;

        private readonly Dictionary<int, ActiveBuild> activeBuilds = new();
        private Building localActiveBuilding;

        private void Awake()
        {
            Instance = this;
            ResolveChunkManager();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ResolveChunkManager();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            foreach (ActiveBuild build in activeBuilds.Values)
            {
                if (build.Routine != null)
                    StopCoroutine(build.Routine);
            }
            activeBuilds.Clear();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ResolveChunkManager();
            RequestStageSnapshotServerRpc();
        }

        public bool RequestBuildStart(Building building, Vector3 playerPosition)
        {
            if (!InstanceFinder.IsClientStarted || building == null || !building.IsBound ||
                localActiveBuilding != null)
                return false;

            localActiveBuilding = building;
            ChunkKey key = building.ChunkKey;
            RequestBuildStartServerRpc(key.FloorIndex, key.X, key.Z, building.LocalIndex,
                playerPosition);
            return true;
        }

        public void RequestBuildCancel(Building building)
        {
            if (building == null)
                return;

            if (InstanceFinder.IsClientStarted)
            {
                ChunkKey key = building.ChunkKey;
                RequestBuildCancelServerRpc(key.FloorIndex, key.X, key.Z, building.LocalIndex);
            }

            if (localActiveBuilding == building)
                localActiveBuilding = null;
        }

        public void NotifyLocalInteractionFinished(Building building)
        {
            if (localActiveBuilding == building)
                localActiveBuilding = null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestBuildStartServerRpc(int floor, int x, int z, int localIndex,
            Vector3 claimedPlayerPosition, NetworkConnection sender = null)
        {
            ChunkKey key = new(floor, x, z);
            if (sender == null || activeBuilds.ContainsKey(sender.ClientId) ||
                !TryGetBuildDefinition(key, localIndex, out ChunkAssetData asset, out Building definition) ||
                asset.Stage >= ChunkAssetData.CompletedStage ||
                Vector3.Distance(claimedPlayerPosition, asset.Position) > maximumInteractionDistance ||
                NetworkResourceService.Instance == null ||
                !NetworkResourceService.Instance.ServerHasAtLeast(sender, definition.ResourceType,
                    definition.ResourceCostPerStage))
            {
                RejectBuildTargetRpc(sender, floor, x, z, localIndex);
                return;
            }

            ActiveBuild build = new() { ChunkKey = key, LocalIndex = localIndex };
            build.Routine = StartCoroutine(CompleteBuildAfterDelay(sender, build,
                definition.InteractionDuration));
            activeBuilds.Add(sender.ClientId, build);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestBuildCancelServerRpc(int floor, int x, int z, int localIndex,
            NetworkConnection sender = null)
        {
            ChunkKey key = new(floor, x, z);
            if (sender == null || !activeBuilds.TryGetValue(sender.ClientId, out ActiveBuild build) ||
                build.ChunkKey != key || build.LocalIndex != localIndex)
                return;

            if (build.Routine != null)
                StopCoroutine(build.Routine);
            activeBuilds.Remove(sender.ClientId);
        }

        private IEnumerator CompleteBuildAfterDelay(NetworkConnection connection, ActiveBuild build,
            float duration)
        {
            yield return new WaitForSeconds(duration);
            activeBuilds.Remove(connection.ClientId);

            if (!TryGetBuildDefinition(build.ChunkKey, build.LocalIndex, out ChunkAssetData asset,
                    out Building definition) ||
                asset.Stage >= ChunkAssetData.CompletedStage ||
                NetworkResourceService.Instance == null ||
                !NetworkResourceService.Instance.ServerHasAtLeast(connection, definition.ResourceType,
                    definition.ResourceCostPerStage) ||
                !chunkManager.TryAdvanceAssetStage(build.ChunkKey, build.LocalIndex, out byte appliedStage))
            {
                RejectBuildTargetRpc(connection, build.ChunkKey.FloorIndex, build.ChunkKey.X,
                    build.ChunkKey.Z, build.LocalIndex);
                yield break;
            }

            if (!NetworkResourceService.Instance.TryConsumeServer(connection, definition.ResourceType,
                    definition.ResourceCostPerStage, out int authoritativeAmount))
            {
                chunkManager.SetAssetStage(build.ChunkKey, build.LocalIndex, asset.Stage);
                RejectBuildTargetRpc(connection, build.ChunkKey.FloorIndex, build.ChunkKey.X,
                    build.ChunkKey.Z, build.LocalIndex);
                yield break;
            }

            ApplyStageObserversRpc(build.ChunkKey.FloorIndex, build.ChunkKey.X, build.ChunkKey.Z,
                build.LocalIndex, appliedStage);
            NetworkResourceService.Instance.SendAuthoritativeAmount(connection, definition.ResourceType,
                authoritativeAmount);
        }

        private bool TryGetBuildDefinition(ChunkKey key, int localIndex, out ChunkAssetData asset,
            out Building definition)
        {
            asset = default;
            definition = null;
            if (chunkManager == null || !chunkManager.TryGetAsset(key, localIndex, out asset))
                return false;

            GameObject prefab = chunkManager.AssetPrefabs.GetPrefab(asset.AssetType);
            definition = prefab != null ? prefab.GetComponent<Building>() : null;
            return definition != null;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestStageSnapshotServerRpc(NetworkConnection sender = null)
        {
            if (sender == null || chunkManager == null)
                return;

            List<int> floors = new();
            List<int> xs = new();
            List<int> zs = new();
            List<int> localIndices = new();
            List<byte> stages = new();
            IReadOnlyList<ChunkSceneCache> chunks = chunkManager.CachedChunks;
            for (int chunkIndex = 0; chunkIndex < chunks.Count; chunkIndex++)
            {
                ChunkSceneCache chunk = chunks[chunkIndex];
                IReadOnlyList<ChunkAssetData> assets = chunk.Assets;
                for (int assetIndex = 0; assetIndex < assets.Count; assetIndex++)
                {
                    byte stage = assets[assetIndex].Stage;
                    if (stage == 0)
                        continue;
                    floors.Add(chunk.Key.FloorIndex);
                    xs.Add(chunk.Key.X);
                    zs.Add(chunk.Key.Z);
                    localIndices.Add(assetIndex);
                    stages.Add(stage);
                }
            }

            ApplyStageSnapshotTargetRpc(sender, floors.ToArray(), xs.ToArray(), zs.ToArray(),
                localIndices.ToArray(), stages.ToArray());
        }

        [TargetRpc]
        private void ApplyStageSnapshotTargetRpc(NetworkConnection connection, int[] floors, int[] xs,
            int[] zs, int[] localIndices, byte[] stages)
        {
            if (chunkManager == null || floors == null || xs == null || zs == null ||
                localIndices == null || stages == null)
                return;

            int count = Mathf.Min(floors.Length, xs.Length, zs.Length, localIndices.Length,
                stages.Length);
            for (int i = 0; i < count; i++)
                chunkManager.SetAssetStage(new ChunkKey(floors[i], xs[i], zs[i]), localIndices[i], stages[i]);
        }

        [TargetRpc]
        private void RejectBuildTargetRpc(NetworkConnection connection, int floor, int x, int z,
            int localIndex)
        {
            if (localActiveBuilding == null)
                return;
            ChunkKey key = new(floor, x, z);
            if (localActiveBuilding.ChunkKey == key && localActiveBuilding.LocalIndex == localIndex)
                localActiveBuilding.RejectByServer();
        }

        [ObserversRpc(ExcludeServer = true)]
        private void ApplyStageObserversRpc(int floor, int x, int z, int localIndex, byte stage)
        {
            ResolveChunkManager();
            chunkManager?.SetAssetStage(new ChunkKey(floor, x, z), localIndex, stage);
        }

        private void HandleRemoteConnectionState(NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (connection == null || args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            if (activeBuilds.Remove(connection.ClientId, out ActiveBuild build) && build.Routine != null)
                StopCoroutine(build.Routine);
        }

        private void ResolveChunkManager()
        {
            if (chunkManager == null)
                chunkManager = FindFirstObjectByType<ChunkManager>(FindObjectsInactive.Include);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            maximumInteractionDistance = Mathf.Max(0.1f, maximumInteractionDistance);
        }
    }
}
