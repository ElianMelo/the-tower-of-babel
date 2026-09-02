using UnityEngine;
using System.Linq;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Displays the name of the object at the center of the local player's view.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerInteractionRaycaster : MonoBehaviour
    {
        [Header("Raycast")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.1f)] private float interactionDistance = 5f;
        [SerializeField] private LayerMask interactionMask = ~0;
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField] private string promptFormat = "{0}";
        [SerializeField] private PlayerControlStateMachine playerStateMachine;

        public GameObject CurrentTarget { get; private set; }

        private IInteractable currentInteractable;
        private MonoBehaviour currentInteractableBehaviour;
        private IInteractionPresentation currentPresentation;
        private IInteractable activeInteraction;
        private MonoBehaviour activeInteractionBehaviour;
        private float interactionElapsed;
        private IServerAuthoritativeInteractable authoritativeInteractable;
        private bool serverRejected;

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponentInChildren<Camera>(true);

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            if (playerStateMachine == null)
                playerStateMachine = GetComponent<PlayerControlStateMachine>();
            if (playerStateMachine != null)
                playerStateMachine.GatheringInterrupted += HandleConnectionInterruptedGathering;

            ClearTarget();
        }

        private void Update()
        {
            if (activeInteraction != null)
            {
                UpdateActiveInteraction();
                return;
            }

            if (interactionCamera == null)
            {
                ClearTarget();
                return;
            }

            Ray ray = interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
            if (Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionMask, triggerInteraction))
                SetTarget(hit.collider.gameObject);
            else
                ClearTarget();

            if (currentInteractable != null && currentInteractable.CanInteract && Input.GetKeyDown(KeyCode.E))
                TryBeginCurrentInteraction();
        }

        private void SetTarget(GameObject target)
        {
            MonoBehaviour interactableBehaviour = target.GetComponentsInParent<MonoBehaviour>(true)
                .FirstOrDefault(component => component is IInteractable);
            IInteractable interactable = interactableBehaviour as IInteractable;

            if (CurrentTarget == target && ReferenceEquals(currentInteractable, interactable))
            {
                RefreshCurrentPresentation();
                return;
            }

            ReleaseCurrentPresentation();

            CurrentTarget = target;
            currentInteractable = interactable;
            currentInteractableBehaviour = interactableBehaviour;
            currentPresentation = interactable as IInteractionPresentation;
            if (currentPresentation != null)
                currentPresentation.InteractionPresentationChanged += HandleInteractionPresentationChanged;

            RefreshCurrentPresentation();
        }

        private void RefreshCurrentPresentation()
        {
            if (currentInteractable == null ||
                currentInteractableBehaviour == null ||
                !currentInteractableBehaviour.isActiveAndEnabled ||
                currentPresentation != null && !currentPresentation.ShouldShowInteraction)
            {
                currentPresentation?.SetInteractionFocused(false);
                InterfaceManager.Instance?.HideInteraction();
                return;
            }

            currentPresentation?.SetInteractionFocused(true);
            InterfaceManager.Instance?.ShowInteraction(
                string.Format(promptFormat, currentInteractable.ObjectName),
                currentInteractable.DetailText,
                currentInteractable.DetailColor,
                currentInteractable.PromptText);
        }

        private void ClearTarget()
        {
            if (CurrentTarget == null && currentInteractable == null && currentPresentation == null)
                return;

            ReleaseCurrentPresentation();
            CurrentTarget = null;
            currentInteractable = null;
            currentInteractableBehaviour = null;
            InterfaceManager.Instance?.HideInteraction();
        }

        private void ReleaseCurrentPresentation()
        {
            if (currentPresentation == null)
                return;

            currentPresentation.InteractionPresentationChanged -= HandleInteractionPresentationChanged;
            currentPresentation.SetInteractionFocused(false);
            currentPresentation = null;
        }

        private void HandleInteractionPresentationChanged()
        {
            RefreshCurrentPresentation();
        }

        private void OnDisable()
        {
            if (activeInteraction != null)
                CancelCurrentInteraction();
            else
                ClearTarget();

            if (playerStateMachine != null)
                playerStateMachine.GatheringInterrupted -= HandleConnectionInterruptedGathering;
        }

        public bool TryBeginCurrentInteraction()
        {
            if (activeInteraction != null || currentInteractable == null || !currentInteractable.CanInteract)
                return false;

            if (playerStateMachine == null || !playerStateMachine.BeginGathering())
                return false;

            activeInteraction = currentInteractable;
            activeInteractionBehaviour = currentInteractableBehaviour;
            interactionElapsed = 0f;
            activeInteraction.BeginInteraction(gameObject);
            authoritativeInteractable = activeInteraction as IServerAuthoritativeInteractable;
            if (authoritativeInteractable != null)
            {
                authoritativeInteractable.ServerRejected += HandleServerRejected;
                if (!authoritativeInteractable.RequestServerStart(gameObject))
                {
                    HandleServerRejected();
                    return false;
                }
            }
            InterfaceManager.Instance?.SetInteractionProgress(0f);
            return true;
        }

        private void UpdateActiveInteraction()
        {
            if (activeInteractionBehaviour == null || !activeInteractionBehaviour.isActiveAndEnabled)
            {
                CancelCurrentInteraction();
                return;
            }

            if (Input.GetKeyDown(KeyCode.E))
            {
                CancelCurrentInteraction();
                return;
            }

            interactionElapsed += Time.deltaTime;
            float normalizedProgress = Mathf.Clamp01(interactionElapsed / Mathf.Max(0.01f, activeInteraction.Duration));
            activeInteraction.UpdateInteraction(normalizedProgress);
            InterfaceManager.Instance?.SetInteractionProgress(normalizedProgress);

            if (normalizedProgress >= 1f)
                CompleteInteraction();
        }

        private void CompleteInteraction()
        {
            activeInteraction.CompleteInteraction(gameObject);
            FinishInteraction();
            RefreshCurrentPresentation();
        }

        public void CancelCurrentInteraction()
        {
            if (activeInteraction == null)
                return;

            if (!serverRejected)
                authoritativeInteractable?.RequestServerCancel();
            activeInteraction?.CancelInteraction();
            FinishInteraction();
            ClearTarget();
        }

        private void FinishInteraction()
        {
            if (authoritativeInteractable != null)
                authoritativeInteractable.ServerRejected -= HandleServerRejected;
            authoritativeInteractable = null;
            serverRejected = false;
            activeInteraction = null;
            activeInteractionBehaviour = null;
            interactionElapsed = 0f;
            playerStateMachine?.EndGathering();
            InterfaceManager.Instance?.HideInteractionProgress();
        }

        private void HandleServerRejected()
        {
            serverRejected = true;
            CancelCurrentInteraction();
        }

        private void HandleConnectionInterruptedGathering()
        {
            if (activeInteraction != null)
                CancelCurrentInteraction();
        }

        private void OnDrawGizmosSelected()
        {
            Camera sourceCamera = interactionCamera != null ? interactionCamera : GetComponentInChildren<Camera>(true);
            if (sourceCamera == null)
                return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(sourceCamera.transform.position, sourceCamera.transform.forward * interactionDistance);
        }
    }
}
