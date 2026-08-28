using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    public sealed class PlayerResourceWallet : MonoBehaviour
    {
        private readonly Dictionary<ResourceType, int> amounts = new();

        public event Action<ResourceType, int> ResourceAmountChanged;

        public int GetAmount(ResourceType resourceType)
        {
            return amounts.TryGetValue(resourceType, out int amount) ? amount : 0;
        }

        public void Add(ResourceType resourceType, int amount)
        {
            if (amount <= 0)
                return;

            int newAmount = GetAmount(resourceType) + amount;
            amounts[resourceType] = newAmount;
            ResourceAmountChanged?.Invoke(resourceType, newAmount);
        }

        public void SetAuthoritativeAmount(ResourceType resourceType, int amount)
        {
            int sanitizedAmount = Mathf.Max(0, amount);
            amounts[resourceType] = sanitizedAmount;
            ResourceAmountChanged?.Invoke(resourceType, sanitizedAmount);
        }
    }
}
