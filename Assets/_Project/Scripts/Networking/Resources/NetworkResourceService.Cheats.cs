using System;
using FishNet.Connection;
using FishNet.Object;
using TowerOfBabel.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Networking.Resources
{
    public sealed partial class NetworkResourceService
    {
        public int ServerCapacity(NetworkConnection player) => serverResources.GetCapacity(player.ClientId);
        public void ServerCheatCapacity(NetworkConnection player, int amount)
        {
            if (!IsServerStarted || player == null) return;
            serverResources.SetCapacity(player.ClientId, amount);
            CapacityTargetRpc(player, serverResources.GetCapacity(player.ClientId));
            foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) UpdateWalletTargetRpc(player, type, serverResources.GetAmount(player.ClientId, type));
        }
        [TargetRpc] private void CapacityTargetRpc(NetworkConnection player, int capacity) { localCapacity = capacity; }
        public bool ServerCheatResource(NetworkConnection player, ResourceType type, int amount)
        {
            if (!IsServerStarted || player == null || !serverResources.TryAdd(player.ClientId, type, amount, out int balance)) return false;
            UpdateWalletTargetRpc(player, type, balance); return true;
        }
        public void ServerCheatSetResource(NetworkConnection player, ResourceType type, int amount)
        {
            if (!IsServerStarted || player == null) return;
            serverResources.SetAmount(player.ClientId, type, amount);
            UpdateWalletTargetRpc(player, type, serverResources.GetAmount(player.ClientId, type));
        }
        public bool ServerCheatResetNodes()
        {
            if (!IsServerStarted) return false;
            foreach (var node in nodes.Values) node.ResetAuthoritativeCooldown();
            ResetNodesObserversRpc(); return true;
        }
        [ObserversRpc(ExcludeServer = true)] private void ResetNodesObserversRpc()
        { foreach (var node in nodes.Values) node.ResetAuthoritativeCooldown(); }
        public bool ServerCheatGather(NetworkConnection player, ulong nodeId)
        {
            if (!IsServerStarted || player == null || !nodes.TryGetValue(nodeId, out var node)) return false;
            var upgrades = NetworkUpgradeService.Instance;
            int amount = upgrades != null ? upgrades.GetServerProduction(player, UpgradeJob.Gather, node.Definition.AmountGathered) : node.Definition.AmountGathered;
            if (!ServerCheatResource(player, node.Definition.ResourceType, amount)) return false;
            upgrades?.ServerGrantActionExperience(player, UpgradeJob.Gather, worldPosition: node.FeedbackPosition, actionLabel: "Instant gather");
            return true;
        }
        public bool ServerCheatProcess(NetworkConnection player, string processorId)
        {
            if (!IsServerStarted || player == null || string.IsNullOrEmpty(processorId) || !processors.TryGetValue(processorId, out var processor)) return false;
            var upgrades = NetworkUpgradeService.Instance;
            int cost = upgrades != null ? upgrades.GetServerActionCost(player, UpgradeJob.Process, processor.SourceAmount) : processor.SourceAmount;
            int production = upgrades != null ? upgrades.GetServerProduction(player, UpgradeJob.Process, processor.TargetAmount) : processor.TargetAmount;
            if (!serverResources.TryExchange(player.ClientId, processor.SourceResource.ResourceType, cost, processor.TargetResource.ResourceType, production, out int source, out int target)) return false;
            UpdateWalletTargetRpc(player, processor.SourceResource.ResourceType, source);
            UpdateWalletTargetRpc(player, processor.TargetResource.ResourceType, target);
            upgrades?.ServerGrantActionExperience(player, UpgradeJob.Process, worldPosition: processor.FeedbackPosition, actionLabel: "Instant process");
            return true;
        }
    }
}
