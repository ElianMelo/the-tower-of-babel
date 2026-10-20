using UnityEngine;

namespace TowerOfBabel.Interface
{
    public sealed class AeroDock : MonoBehaviour
    {
        public void Upgrades() => InterfaceManager.Instance?.ToggleUpgrade();
        public void Inventory() => InterfaceManager.Instance?.ToggleInventory();
        public void Emotes() => InterfaceManager.Instance?.ToggleEmotes();
    }
}
