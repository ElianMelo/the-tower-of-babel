using UnityEngine;

namespace TowerOfBabel.Resources
{
    [CreateAssetMenu(fileName = "ResourceDefinition", menuName = "Tower of Babel/Resources/Resource Definition")]
    public sealed class ResourceDefinition : ScriptableObject
    {
        [SerializeField] private string resourceId = "stone";
        [SerializeField] private ResourceType resourceType = ResourceType.Stone;
        [SerializeField] private string displayName = "Stone";
        [SerializeField] private Sprite icon;
        [SerializeField, Min(1)] private int amountGathered = 1;
        [SerializeField, Min(0.1f)] private float interactionDuration = 3f;
        [SerializeField, Min(0f)] private float respawnCooldown = 5f;
        [SerializeField, Min(0f)] private float shakeStrength = 0.05f;
        [SerializeField, Min(0f)] private float shakeSpeed = 24f;

        public string ResourceId => resourceId;
        public ResourceType ResourceType => resourceType;
        public string DisplayName => displayName;
        public Sprite Icon => icon;
        public int AmountGathered => amountGathered;
        public float InteractionDuration => interactionDuration;
        public float RespawnCooldown => respawnCooldown;
        public float ShakeStrength => shakeStrength;
        public float ShakeSpeed => shakeSpeed;
    }
}
