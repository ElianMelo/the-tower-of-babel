using System;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    [DisallowMultipleComponent]
    public sealed class ResourceProcessor : MonoBehaviour, IInteractable, IServerAuthoritativeInteractable, IServerInteractionCompletion
    {
        [Header("Recipe")]
        [SerializeField] private ResourceDefinition sourceResource;
        [SerializeField, Min(1)] private int sourceAmount = 1;
        [SerializeField] private ResourceDefinition targetResource;
        [SerializeField, Min(1)] private int targetAmount = 1;
        [SerializeField, Min(0.1f)] private float interactionDuration = 2f;

        [Tooltip("Optional unique ID. When empty, uses the scene and hierarchy path. Set explicitly for runtime-spawned processors.")]
        [SerializeField] private string processorId;
        private string resolvedId;

        public ResourceDefinition SourceResource => sourceResource;
        public ResourceDefinition TargetResource => targetResource;
        public int SourceAmount => sourceAmount;
        public int TargetAmount => targetAmount;
        public float InteractionDuration => interactionDuration;
        public string ProcessorId => resolvedId ??= ResolveId();
        public bool ServerCanProcess => isActiveAndEnabled && sourceResource != null && targetResource != null
            && sourceAmount > 0 && targetAmount > 0 && interactionDuration > 0f
            && !float.IsNaN(interactionDuration) && !float.IsInfinity(interactionDuration);
        public int EffectiveSourceAmount => NetworkUpgradeService.Instance != null
            ? NetworkUpgradeService.Instance.GetLocalActionCost(UpgradeJob.Process, sourceAmount) : sourceAmount;
        public int EffectiveTargetAmount => NetworkUpgradeService.Instance != null
            ? NetworkUpgradeService.Instance.GetLocalProduction(UpgradeJob.Process, targetAmount) : targetAmount;
        public float Duration => NetworkUpgradeService.Instance != null
            ? NetworkUpgradeService.Instance.GetLocalActionDuration(UpgradeJob.Process, interactionDuration) : interactionDuration;
        public string ObjectName => gameObject.name;
        public string DetailText => sourceResource != null && targetResource != null
            ? $"{EffectiveSourceAmount} {sourceResource.DisplayName} → {EffectiveTargetAmount} {targetResource.DisplayName}"
            : "Undefined recipe";
        public Color DetailColor => CanInteract ? Color.blue : Color.red;
        public string PromptText => !ServerCanProcess ? "Unavailable"
            : !HasLocalMaterials ? $"Need {EffectiveSourceAmount} {sourceResource.DisplayName}"
            : !HasLocalCapacity ? "Inventory full" : "Press 'E'";
        public bool CanInteract => ServerCanProcess && HasLocalMaterials && HasLocalCapacity;
        public event Action ServerRejected;
        public event Action ServerCompleted;

        private bool HasLocalMaterials => NetworkResourceService.Instance != null && sourceResource != null
            && NetworkResourceService.Instance.GetLocalAmount(sourceResource.ResourceType) >= EffectiveSourceAmount;
        private bool HasLocalCapacity => NetworkResourceService.Instance != null && targetResource != null
            && NetworkResourceService.Instance.CanExchangeLocal(sourceResource.ResourceType, EffectiveSourceAmount,
                targetResource.ResourceType, EffectiveTargetAmount);

        public void BeginInteraction(GameObject interactor) { }
        public void UpdateInteraction(float normalizedProgress) { }
        public void CancelInteraction() { }
        public void CompleteInteraction(GameObject interactor) { }
        public bool RequestServerStart(GameObject interactor) => interactor != null && CanInteract
            && NetworkResourceService.Instance.RequestProcessStart(this, interactor.transform.position);
        public void RequestServerCancel() => NetworkResourceService.Instance?.RequestProcessCancel(this);
        public void RejectByServer() => ServerRejected?.Invoke();
        public void CompleteByServer() => ServerCompleted?.Invoke();

        private string ResolveId()
        {
            if (!string.IsNullOrWhiteSpace(processorId))
                return processorId;
            string path = "";
            for (Transform current = transform; current != null; current = current.parent)
                path = $"/{current.GetSiblingIndex()}:{current.name}" + path;
            return gameObject.scene.path + path;
        }
    }
}
