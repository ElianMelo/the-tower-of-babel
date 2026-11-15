using UnityEngine;
using System.Collections.Generic;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;

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

        [Header("Construction scan")]
        [SerializeField, Min(0.1f)] private float constructionScanRadius = 15f;
        public const float ConstructionScanDuration = 2f;
        private readonly List<Building> scannedBuildings = new();
        private ChunkManager constructionChunks;
        private float scanExpiresAt;

        public GameObject CurrentTarget { get; private set; }

        private IInteractable currentInteractable;
        private MonoBehaviour currentInteractableBehaviour;
        private IInteractionPresentation currentPresentation;
        private IInteractable activeInteraction;
        private MonoBehaviour activeInteractionBehaviour;
        private float interactionElapsed;
        private float interactionDuration;
        private IServerInteractionCompletion serverCompletion;
        private IServerAuthoritativeInteractable authoritativeInteractable;
        private bool serverRejected;
        private InteractionMovementCancel movementCancel;
        private readonly RaycastHit[] targetHits = new RaycastHit[16];

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponentInChildren<Camera>(true);

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            if (playerStateMachine == null)
                playerStateMachine = GetComponent<PlayerControlStateMachine>();
            ClearTarget();
        }

        private void OnEnable()
        {
            if (playerStateMachine != null)
                playerStateMachine.InteractionInterrupted += HandleConnectionInterruptedInteraction;
        }

        private void Update()
        {
            RefreshConstructionScan();
            if (TowerOfBabel.Settings.GameInput.ScanPressed)
                ScanNearbyConstruction();
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

            if (TryFindTarget(interactionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out GameObject target))
                SetTarget(target);
            else
                ClearTarget();

            if (currentInteractable != null && currentInteractable.CanInteract && TowerOfBabel.Settings.GameInput.InteractPressed)
                TryBeginCurrentInteraction();
        }

        /// <summary>
        /// The nearest object along the crosshair ray within reach of the player, skipping the player's own body. A
        /// third-person camera's ray starts where it passes the player's eyes, so the reach matches first person.
        /// </summary>
        public bool TryFindTarget(Ray cameraRay, out GameObject target)
        {
            target = null;
            PlayerFollower follower = PlayerFollower.For(transform);
            Ray ray = follower != null && follower.CameraDistance > 0f
                ? PlayerCameraZoom.StartNear(cameraRay, follower.Pivot)
                : cameraRay;
            int count = Physics.RaycastNonAlloc(ray, targetHits, interactionDistance, interactionMask, triggerInteraction);
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider hitCollider = targetHits[i].collider;
                if (hitCollider == null || targetHits[i].distance >= nearest || hitCollider.transform.IsChildOf(transform))
                    continue;
                nearest = targetHits[i].distance;
                target = hitCollider.gameObject;
            }

            return target != null;
        }

        public bool ScanNearbyConstruction()
        {
            if (playerStateMachine == null || !playerStateMachine.CanScanConstruction)
                return false;
            ClearConstructionScan();
            if (constructionChunks == null)
                constructionChunks = FindFirstObjectByType<ChunkManager>();
            if (constructionChunks == null) return false;
            constructionChunks.GetNearbyConstruction(transform.position, constructionScanRadius, scannedBuildings);
            scanExpiresAt = Time.unscaledTime + ConstructionScanDuration;
            foreach (Building building in scannedBuildings) building.SetScanHighlighted(true);
            return true;
        }

        private void RefreshConstructionScan()
        {
            if (Time.unscaledTime >= scanExpiresAt || playerStateMachine == null ||
                !playerStateMachine.CanScanConstruction)
            {
                ClearConstructionScan();
                return;
            }
            for (int i = scannedBuildings.Count - 1; i >= 0; i--)
            {
                Building building = scannedBuildings[i];
                if (building != null && building.IsConstructionAvailable) continue;
                if (building != null) building.SetScanHighlighted(false);
                scannedBuildings.RemoveAt(i);
            }
        }

        private void ClearConstructionScan()
        {
            foreach (Building building in scannedBuildings)
                if (building != null) building.SetScanHighlighted(false);
            scannedBuildings.Clear();
        }

        private void SetTarget(GameObject target)
        {
            // The same object under the crosshair as last frame, with its interactable still alive: nothing to look up.
            // (The lookup used to run every frame and allocated an array, a delegate and an enumerator each time.)
            if (CurrentTarget == target && (currentInteractable == null || currentInteractableBehaviour != null))
            {
                RefreshCurrentPresentation();
                return;
            }

            IInteractable interactable = target.GetComponentInParent<IInteractable>(true);
            MonoBehaviour interactableBehaviour = interactable as MonoBehaviour;
            if (interactableBehaviour == null) interactable = null;

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
                currentInteractable.PromptText,
                (currentInteractable as IInteractionResource)?.InteractionResources ?? default);
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
            ClearConstructionScan();
            if (activeInteraction != null)
                CancelCurrentInteraction();
            else
                ClearTarget();

            if (playerStateMachine != null)
                playerStateMachine.InteractionInterrupted -= HandleConnectionInterruptedInteraction;
        }

        public bool TryBeginCurrentInteraction()
        {
            if (activeInteraction != null || currentInteractable == null || !currentInteractable.CanInteract)
                return false;

            bool gatheringAnimation = currentInteractable is IInteractionAnimation animation && animation.UsesGatheringAnimation;
            if (playerStateMachine == null || !playerStateMachine.BeginInteraction(gatheringAnimation))
                return false;

            activeInteraction = currentInteractable;
            activeInteractionBehaviour = currentInteractableBehaviour;
            interactionElapsed = 0f;
            interactionDuration = activeInteraction.Duration;
            movementCancel.Begin(TowerOfBabel.Settings.GameInput.Move);
            activeInteraction.BeginInteraction(gameObject);
            serverCompletion = activeInteraction as IServerInteractionCompletion;
            if (serverCompletion != null)
                serverCompletion.ServerCompleted += HandleServerCompleted;
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

            if (TowerOfBabel.Settings.GameInput.InteractPressed ||
                MovementCancelsInteraction(TowerOfBabel.Settings.GameInput.Move, TowerOfBabel.Settings.GameInput.JumpPressed))
            {
                CancelCurrentInteraction();
                return;
            }

            interactionElapsed += Time.deltaTime;
            float normalizedProgress = Mathf.Clamp01(interactionElapsed / Mathf.Max(0.01f, interactionDuration));
            activeInteraction.UpdateInteraction(normalizedProgress);
            InterfaceManager.Instance?.SetInteractionProgress(normalizedProgress);

            if (normalizedProgress >= 1f && serverCompletion == null)
                CompleteInteraction();
        }

        /// <summary>
        /// Moving or jumping cancels the active action; looking around does not. Input typed into an open menu is
        /// ignored, and keys still held when the menu closes count as already held.
        /// </summary>
        public bool MovementCancelsInteraction(Vector2 move, bool jumpPressed)
        {
            if (activeInteraction == null)
                return false;
            if (playerStateMachine != null && playerStateMachine.IsModalInputLocked)
            {
                movementCancel.Begin(move);
                return false;
            }

            return movementCancel.ShouldCancel(move, jumpPressed);
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
            if (serverCompletion != null)
                serverCompletion.ServerCompleted -= HandleServerCompleted;
            serverCompletion = null;
            if (authoritativeInteractable != null)
                authoritativeInteractable.ServerRejected -= HandleServerRejected;
            authoritativeInteractable = null;
            serverRejected = false;
            activeInteraction = null;
            activeInteractionBehaviour = null;
            interactionElapsed = 0f;
            playerStateMachine?.EndInteraction();
            InterfaceManager.Instance?.HideInteractionProgress();
        }

        private void HandleServerRejected()
        {
            serverRejected = true;
            CancelCurrentInteraction();
        }

        private void HandleServerCompleted()
        {
            if (activeInteraction != null)
                CompleteInteraction();
        }

        private void HandleConnectionInterruptedInteraction()
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
