using System.Collections;
using System.Collections.Generic;
using FishNet.Connection;
using FishNet;
using FishNet.Object;
using FishNet.Transporting;
using TowerOfBabel.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Networking.Resources
{
    [DisallowMultipleComponent]
    public sealed class NetworkResourceService : NetworkBehaviour
    {
        private sealed class ActiveGather
        {
            public ulong NodeId;
            public float Duration;
            public int Production;
            public Coroutine Routine;
        }

        public static NetworkResourceService Instance { get; private set; }

        [SerializeField] private PlayerResourceWallet localWallet;
        [SerializeField, Min(1)] private int capacityPerResource = 50;
        [SerializeField, Min(0.1f)] private float maximumInteractionDistance = 5.5f;

        private readonly Dictionary<ulong, Resource> nodes = new();
        private readonly Dictionary<int, ActiveGather> activeGathers = new();
        private ServerPlayerResourceStore serverResources;
        private Resource localActiveResource;

        private void Awake()
        {
            Instance = this;
            serverResources = new ServerPlayerResourceStore(capacityPerResource);
            RebuildNodeLookup();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RebuildNodeLookup();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            activeGathers.Clear();
            base.OnStopServer();
        }

        public bool RequestGatherStart(Resource resource, Vector3 playerPosition)
        {
            if (!InstanceFinder.IsClientStarted || resource == null || localActiveResource != null)
                return false;

            localActiveResource = resource;
            RequestGatherStartServerRpc(resource.NodeId, playerPosition);
            return true;
        }

        public void RequestGatherCancel(Resource resource)
        {
            if (resource == null)
                return;

            if (InstanceFinder.IsClientStarted)
                RequestGatherCancelServerRpc(resource.NodeId);

            if (localActiveResource == resource)
                localActiveResource = null;
        }

        public void NotifyLocalInteractionFinished(Resource resource)
        {
            if (localActiveResource == resource)
                localActiveResource = null;
        }

        public int GetLocalAmount(ResourceType resourceType)
        {
            return localWallet != null ? localWallet.GetAmount(resourceType) : 0;
        }

        public bool ServerHasAtLeast(NetworkConnection connection, ResourceType resourceType, int amount)
        {
            return IsServerStarted && connection != null && amount > 0
                && serverResources.HasAtLeast(connection.ClientId, resourceType, amount);
        }

        public bool TryConsumeServer(NetworkConnection connection, ResourceType resourceType,
            int amount, out int authoritativeAmount)
        {
            authoritativeAmount = 0;
            if (!IsServerStarted || connection == null)
                return false;

            return serverResources.TryConsume(connection.ClientId, resourceType, amount,
                out authoritativeAmount);
        }

        public void SendAuthoritativeAmount(NetworkConnection connection, ResourceType resourceType,
            int amount)
        {
            if (IsServerStarted && connection != null)
                UpdateWalletTargetRpc(connection, resourceType, amount);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestGatherStartServerRpc(ulong nodeId, Vector3 claimedPlayerPosition, NetworkConnection sender = null)
        {
            if (sender == null || activeGathers.ContainsKey(sender.ClientId)
                || !nodes.TryGetValue(nodeId, out Resource node)
                || !node.ServerCanGather
                || Vector3.Distance(claimedPlayerPosition, node.transform.position) > maximumInteractionDistance
                || serverResources.GetAmount(sender.ClientId, node.Definition.ResourceType) >= capacityPerResource)
            {
                RejectGatherTargetRpc(sender, nodeId);
                return;
            }

            NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
            ActiveGather gather = new ActiveGather
            {
                NodeId = nodeId,
                Duration = upgrades != null
                    ? upgrades.GetServerActionDuration(sender, UpgradeJob.Gather,
                        node.Definition.InteractionDuration)
                    : node.Definition.InteractionDuration,
                Production = upgrades != null
                    ? upgrades.GetServerProduction(sender, UpgradeJob.Gather,
                        node.Definition.AmountGathered)
                    : node.Definition.AmountGathered
            };
            gather.Routine = StartCoroutine(CompleteGatherAfterDelay(sender, node, gather));
            activeGathers.Add(sender.ClientId, gather);
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestGatherCancelServerRpc(ulong nodeId, NetworkConnection sender = null)
        {
            if (sender == null || !activeGathers.TryGetValue(sender.ClientId, out ActiveGather gather) || gather.NodeId != nodeId)
                return;

            StopCoroutine(gather.Routine);
            activeGathers.Remove(sender.ClientId);
        }

        private IEnumerator CompleteGatherAfterDelay(NetworkConnection connection, Resource node, ActiveGather gather)
        {
            yield return new WaitForSeconds(gather.Duration);
            activeGathers.Remove(connection.ClientId);

            if (!node.ServerCanGather
                || !serverResources.TryAdd(connection.ClientId, node.Definition.ResourceType,
                    gather.Production, out int amount))
            {
                RejectGatherTargetRpc(connection, node.NodeId);
                yield break;
            }

            node.BeginAuthoritativeCooldown(node.Definition.RespawnCooldown);
            SetNodeCooldownObserversRpc(node.NodeId, node.Definition.RespawnCooldown);
            UpdateWalletTargetRpc(connection, node.Definition.ResourceType, amount);
            NetworkUpgradeService.Instance?.ServerGrantActionExperience(connection, UpgradeJob.Gather);
        }

        [TargetRpc]
        private void RejectGatherTargetRpc(NetworkConnection connection, ulong nodeId)
        {
            HandleGatherRejected(nodeId);
        }

        private void HandleGatherRejected(ulong nodeId)
        {
            if (localActiveResource == null || localActiveResource.NodeId != nodeId)
                return;

            Resource rejectedResource = localActiveResource;
            // Rejection skips the cancel RPC, so release the local gather here.
            localActiveResource = null;
            rejectedResource.RejectByServer();
        }

        [TargetRpc]
        private void UpdateWalletTargetRpc(NetworkConnection connection, ResourceType resourceType, int amount)
        {
            localWallet?.SetAuthoritativeAmount(resourceType, amount);
        }

        [ObserversRpc(ExcludeServer = true)]
        private void SetNodeCooldownObserversRpc(ulong nodeId, float duration)
        {
            if (nodes.TryGetValue(nodeId, out Resource node))
                node.BeginAuthoritativeCooldown(duration);
        }

        private void RebuildNodeLookup()
        {
            nodes.Clear();
            foreach (Resource node in FindObjectsByType<Resource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (node.NodeId != 0 && !nodes.ContainsKey(node.NodeId))
                    nodes.Add(node.NodeId, node);
            }
        }

        private void HandleRemoteConnectionState(NetworkConnection connection, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState != RemoteConnectionState.Stopped)
                return;

            if (activeGathers.Remove(connection.ClientId, out ActiveGather gather) && gather.Routine != null)
                StopCoroutine(gather.Routine);
            serverResources.RemovePlayer(connection.ClientId);
        }
    }
}
