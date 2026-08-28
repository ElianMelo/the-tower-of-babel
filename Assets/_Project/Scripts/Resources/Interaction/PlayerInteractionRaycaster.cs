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
        [SerializeField] private MonoBehaviour[] controlsToLock;

        public GameObject CurrentTarget { get; private set; }

        private IInteractable currentInteractable;
        private MonoBehaviour currentInteractableBehaviour;
        private IInteractable activeInteraction;
        private MonoBehaviour activeInteractionBehaviour;
        private IPlayerControlLock[] controlLocks;
        private float interactionElapsed;

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponentInChildren<Camera>(true);

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            MonoBehaviour[] controlSources = controlsToLock != null && controlsToLock.Length > 0
                ? controlsToLock
                : GetComponentsInChildren<MonoBehaviour>(true);

            controlLocks = controlSources
                .OfType<IPlayerControlLock>()
                .ToArray();

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
                return;

            CurrentTarget = target;
            currentInteractable = interactable;
            currentInteractableBehaviour = interactableBehaviour;

            if (interactable == null)
            {
                InterfaceManager.Instance?.HideInteraction();
                return;
            }

            InterfaceManager.Instance?.ShowInteraction(
                string.Format(promptFormat, interactable.ObjectName),
                interactable.DetailText,
                interactable.DetailColor,
                interactable.PromptText);
        }

        private void ClearTarget()
        {
            if (CurrentTarget == null)
                return;

            CurrentTarget = null;
            currentInteractable = null;
            currentInteractableBehaviour = null;
            InterfaceManager.Instance?.HideInteraction();
        }

        private void OnDisable()
        {
            if (activeInteraction != null)
                CancelCurrentInteraction();
            else
                ClearTarget();
        }

        public bool TryBeginCurrentInteraction()
        {
            if (activeInteraction != null || currentInteractable == null || !currentInteractable.CanInteract)
                return false;

            activeInteraction = currentInteractable;
            activeInteractionBehaviour = currentInteractableBehaviour;
            interactionElapsed = 0f;
            SetControlsLocked(true);
            activeInteraction.BeginInteraction(gameObject);
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
            ClearTarget();
        }

        public void CancelCurrentInteraction()
        {
            if (activeInteraction == null)
                return;

            activeInteraction?.CancelInteraction();
            FinishInteraction();
            ClearTarget();
        }

        private void FinishInteraction()
        {
            activeInteraction = null;
            activeInteractionBehaviour = null;
            interactionElapsed = 0f;
            SetControlsLocked(false);
            InterfaceManager.Instance?.HideInteractionProgress();
        }

        private void SetControlsLocked(bool locked)
        {
            foreach (IPlayerControlLock controlLock in controlLocks)
                controlLock.SetControlLocked(locked);
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
