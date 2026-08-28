using System.Collections;
using System;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    [DisallowMultipleComponent]
    public sealed class Resource : MonoBehaviour, IInteractable, IServerAuthoritativeInteractable
    {
        private static readonly Color AvailableColor = Color.blue;
        private static readonly Color CooldownColor = Color.red;

        [SerializeField] private ResourceDefinition definition;
        [SerializeField] private GameObject visuals;
        [SerializeField] private ulong nodeId;

        private Vector3 visualsStartPosition;
        private bool isCoolingDown;

        public string ObjectName => gameObject.name;
        public string DetailText => definition != null ? definition.DisplayName : "Undefined Resource";
        public Color DetailColor => isCoolingDown ? CooldownColor : AvailableColor;
        public string PromptText => CanInteract ? "Press 'E'" : "Unavailable";
        public float Duration => definition != null ? definition.InteractionDuration : 3f;
        public bool CanInteract => enabled && gameObject.activeInHierarchy && definition != null && !isCoolingDown;
        public ulong NodeId => nodeId;
        public ResourceDefinition Definition => definition;
        public bool ServerCanGather => CanInteract;
        public event Action ServerRejected;

        private void Awake()
        {
            if (visuals == null)
                visuals = gameObject;

            visualsStartPosition = visuals.transform.localPosition;
        }

        public void BeginInteraction(GameObject interactor)
        {
            visualsStartPosition = visuals.transform.localPosition;
        }

        public void UpdateInteraction(float normalizedProgress)
        {
            if (definition == null || visuals == null)
                return;

            float phase = Time.time * definition.ShakeSpeed;
            Vector3 offset = new Vector3(Mathf.Sin(phase), 0f, Mathf.Cos(phase * 0.83f));
            visuals.transform.localPosition = visualsStartPosition + offset * definition.ShakeStrength;
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
            isCoolingDown = true;
            visuals.SetActive(false);
            yield return new WaitForSeconds(duration);
            visuals.SetActive(true);
            isCoolingDown = false;
        }

        private void RestoreVisualPosition()
        {
            if (visuals != null)
                visuals.transform.localPosition = visualsStartPosition;
        }

        private void OnDisable()
        {
            RestoreVisualPosition();
        }
    }
}
