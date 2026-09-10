using System.Collections.Generic;
using TowerOfBabel.Resources;

namespace TowerOfBabel.Networking.Resources
{
    public sealed class ServerPlayerResourceStore
    {
        private readonly Dictionary<int, Dictionary<ResourceType, int>> resourcesByPlayer = new();

        public int CapacityPerResource { get; }

        public ServerPlayerResourceStore(int capacityPerResource)
        {
            CapacityPerResource = capacityPerResource;
        }

        public int GetAmount(int playerId, ResourceType resourceType)
        {
            return resourcesByPlayer.TryGetValue(playerId, out Dictionary<ResourceType, int> resources)
                && resources.TryGetValue(resourceType, out int amount)
                ? amount
                : 0;
        }

        public bool TryAdd(int playerId, ResourceType resourceType, int amount, out int authoritativeAmount)
        {
            int current = GetAmount(playerId, resourceType);
            if (amount <= 0 || current >= CapacityPerResource)
            {
                authoritativeAmount = current;
                return false;
            }

            authoritativeAmount = System.Math.Min(CapacityPerResource, current + amount);
            if (!resourcesByPlayer.TryGetValue(playerId, out Dictionary<ResourceType, int> resources))
            {
                resources = new Dictionary<ResourceType, int>();
                resourcesByPlayer.Add(playerId, resources);
            }

            resources[resourceType] = authoritativeAmount;
            return true;
        }

        public bool HasAtLeast(int playerId, ResourceType resourceType, int amount)
        {
            return amount >= 0 && GetAmount(playerId, resourceType) >= amount;
        }

        public bool TryConsume(int playerId, ResourceType resourceType, int amount, out int authoritativeAmount)
        {
            authoritativeAmount = GetAmount(playerId, resourceType);
            if (amount <= 0 || authoritativeAmount < amount)
                return false;

            authoritativeAmount -= amount;
            resourcesByPlayer[playerId][resourceType] = authoritativeAmount;
            return true;
        }

        public void RemovePlayer(int playerId) => resourcesByPlayer.Remove(playerId);

        public bool CanExchange(int playerId, ResourceType source, int cost, ResourceType target, int production)
        {
            if (cost <= 0 || production <= 0 || !HasAtLeast(playerId, source, cost))
                return false;

            long targetAfter = (long)GetAmount(playerId, target) + production;
            if (source == target)
                targetAfter -= cost;
            return targetAfter <= CapacityPerResource;
        }

        // Validate the entire exchange before changing either balance. Never truncate production.
        public bool TryExchange(int playerId, ResourceType source, int cost, ResourceType target,
            int production, out int sourceAmount, out int targetAmount)
        {
            sourceAmount = GetAmount(playerId, source);
            targetAmount = GetAmount(playerId, target);
            if (!CanExchange(playerId, source, cost, target, production))
                return false;

            Dictionary<ResourceType, int> resources = resourcesByPlayer[playerId];
            resources[source] = sourceAmount - cost;
            resources[target] = (int)((long)resources.GetValueOrDefault(target) + production);
            sourceAmount = resources[source];
            targetAmount = resources[target];
            return true;
        }
    }
}
