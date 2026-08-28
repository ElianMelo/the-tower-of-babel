using UnityEngine;

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

        public GameObject CurrentTarget { get; private set; }

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponentInChildren<Camera>(true);

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            ClearTarget();
        }

        private void Update()
        {
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
        }

        private void SetTarget(GameObject target)
        {
            if (CurrentTarget == target)
                return;

            CurrentTarget = target;
            InteractableName namedTarget = target.GetComponentInParent<InteractableName>();
            string targetName = namedTarget != null ? namedTarget.DisplayName : target.name;
            InterfaceManager.Instance?.ShowInteraction(string.Format(promptFormat, targetName));
        }

        private void ClearTarget()
        {
            if (CurrentTarget == null)
                return;

            CurrentTarget = null;
            InterfaceManager.Instance?.HideInteraction();
        }

        private void OnDisable()
        {
            ClearTarget();
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
