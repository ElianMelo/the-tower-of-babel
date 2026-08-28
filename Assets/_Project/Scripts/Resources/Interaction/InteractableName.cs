using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Optional player-facing name override for a raycast target.</summary>
    public sealed class InteractableName : MonoBehaviour
    {
        [SerializeField] private string displayName;

        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? gameObject.name
            : displayName;
    }
}
