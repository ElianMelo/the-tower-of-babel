using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    public sealed class PlayerResourceWallet : MonoBehaviour
    {
        private readonly Dictionary<ResourceType, int> amounts = new();

        public int GetAmount(ResourceType resourceType)
        {
            return amounts.TryGetValue(resourceType, out int amount) ? amount : 0;
        }

        public void Add(ResourceType resourceType, int amount)
        {
            if (amount <= 0)
                return;

            amounts[resourceType] = GetAmount(resourceType) + amount;
        }
    }
}
