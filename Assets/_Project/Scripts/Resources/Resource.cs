using System.Collections;
using System;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    [DisallowMultipleComponent]
    public sealed class Resource : MonoBehaviour, IInteractable, IServerAuthoritativeInteractable, IInteractionAnimation, IInteractionResource
    {
        private static readonly Color AvailableColor = Color.blue;
        private static readonly Color CooldownColor = Color.red;

        [SerializeField] private ResourceDefinition definition;
        [SerializeField] private GameObject visuals;
        [SerializeField] private ulong nodeId;

        private AeroInteractionMotion interactionMotion;
        private bool isCoolingDown;

        public string ObjectName => gameObject.name;
        public bool UsesGatheringAnimation => true;
        public string DetailText => definition != null ? definition.DisplayName : "Undefined Resource";
        public Color DetailColor => isCoolingDown ? CooldownColor : AvailableColor;
        public string PromptText => CanInteract ? "Press 'E'" : "Unavailable";
        public float Duration
        {
            get
            {
                float duration = definition != null ? definition.InteractionDuration : 3f;
                NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
                return upgrades != null
                    ? upgrades.GetLocalActionDuration(UpgradeJob.Gather, duration)
                    : duration;
            }
        }
        public bool CanInteract => enabled && gameObject.activeInHierarchy && definition != null && !isCoolingDown;
        public ulong NodeId => nodeId;
        public ResourceDefinition Definition => definition;
        public Vector3 FeedbackPosition
        {
            get
            {
                var filter=visuals!=null ? visuals.GetComponent<MeshFilter>() : null;
                if (filter==null || filter.sharedMesh==null) return transform.position;
                var bounds=filter.sharedMesh.bounds;
                return filter.transform.TransformPoint(bounds.center+Vector3.up*bounds.extents.y*.6f);
            }
        }
        public ResourceType InteractionResourceType => definition!=null ? definition.ResourceType : ResourceType.Stone;
        public InteractionResourceData InteractionResources => definition!=null ? InteractionResourceData.Gather(InteractionResourceType,
            NetworkUpgradeService.Instance!=null ? NetworkUpgradeService.Instance.GetLocalProduction(UpgradeJob.Gather,definition.AmountGathered) : definition.AmountGathered) : default;
        public bool ServerCanGather => CanInteract;
        public event Action ServerRejected;

        private void Awake()
        {
            if (visuals == null)
                visuals = gameObject;
        }

        public void BeginInteraction(GameObject interactor)
        {
            if (definition == null || visuals == null) return;
            if (interactionMotion == null)
                interactionMotion = GetComponent<AeroInteractionMotion>() ?? gameObject.AddComponent<AeroInteractionMotion>();
            interactionMotion.Begin(visuals.transform, definition.ShakeStrength, definition.ShakeSpeed);
        }

        public void UpdateInteraction(float normalizedProgress)
        {
            interactionMotion?.SetProgress(normalizedProgress);
        }

        public void CancelInteraction()
        {
            RestoreVisualPosition();
        }

        public void CompleteInteraction(GameObject interactor)
        {
            RestoreVisualPosition();
            NetworkResourceService.Instance?.NotifyLocalInteractionFinished(this);
        }

        public bool RequestServerStart(GameObject interactor)
        {
            return NetworkResourceService.Instance != null
                && NetworkResourceService.Instance.RequestGatherStart(this, interactor.transform.position);
        }

        public void RequestServerCancel()
        {
            NetworkResourceService.Instance?.RequestGatherCancel(this);
        }

        public void RejectByServer()
        {
            ServerRejected?.Invoke();
        }

        public void BeginAuthoritativeCooldown(float duration)
        {
            StopAllCoroutines();
            StartCoroutine(CooldownRoutine(duration));
        }

        private IEnumerator CooldownRoutine(float duration)
        {
            interactionMotion?.Stop(true);
            isCoolingDown = true;
            visuals.SetActive(false);
            yield return new WaitForSeconds(duration);
            visuals.SetActive(true);
            isCoolingDown = false;
        }

        private void RestoreVisualPosition()
        {
            interactionMotion?.Stop();
        }

        private void OnDisable()
        {
            interactionMotion?.Stop(true);
        }
    }
}
