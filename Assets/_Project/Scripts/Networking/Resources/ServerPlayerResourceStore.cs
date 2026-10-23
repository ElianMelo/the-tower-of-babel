using System.Collections.Generic;
using TowerOfBabel.Resources;

namespace TowerOfBabel.Networking.Resources
{
    public sealed class ServerPlayerResourceStore
    {
        private readonly Dictionary<int, Dictionary<ResourceType, int>> resourcesByPlayer = new();
        private readonly Dictionary<int, int> capacities = new();

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

        public int GetCapacity(int playerId) => capacities.TryGetValue(playerId, out int capacity) ? capacity : CapacityPerResource;
        public void SetCapacity(int playerId, int capacity)
        {
            capacities[playerId] = System.Math.Clamp(capacity, 1, 1000000);
            if (resourcesByPlayer.TryGetValue(playerId, out var amounts))
                foreach (var type in new List<ResourceType>(amounts.Keys)) amounts[type] = System.Math.Min(amounts[type], GetCapacity(playerId));
        }
        public void SetAmount(int playerId, ResourceType type, int amount)
        {
            if (!resourcesByPlayer.TryGetValue(playerId, out var amounts)) resourcesByPlayer[playerId] = amounts = new();
            amounts[type] = System.Math.Clamp(amount, 0, GetCapacity(playerId));
        }

        public bool TryAdd(int playerId, ResourceType resourceType, int amount, out int authoritativeAmount)
        {
            int current = GetAmount(playerId, resourceType);
            if (amount <= 0 || current >= GetCapacity(playerId))
            {
                authoritativeAmount = current;
                return false;
            }

            authoritativeAmount = (int)System.Math.Min(GetCapacity(playerId), (long)current + amount);
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

        public void RemovePlayer(int playerId) { resourcesByPlayer.Remove(playerId); capacities.Remove(playerId); }

        public bool CanExchange(int playerId, ResourceType source, int cost, ResourceType target, int production)
        {
            if (cost <= 0 || production <= 0 || !HasAtLeast(playerId, source, cost))
                return false;

            long targetAfter = (long)GetAmount(playerId, target) + production;
            if (source == target)
                targetAfter -= cost;
            return targetAfter <= GetCapacity(playerId);
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
