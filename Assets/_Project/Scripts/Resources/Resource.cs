using System.Collections;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;

namespace TowerOfBabel.Resources
{
    [DisallowMultipleComponent]
    public sealed class Resource : MonoBehaviour, IInteractable
    {
        private static readonly Color AvailableColor = Color.blue;
        private static readonly Color CooldownColor = Color.red;

        [SerializeField] private ResourceDefinition definition;
        [SerializeField] private GameObject visuals;

        private Vector3 visualsStartPosition;
        private bool isCoolingDown;

        public string ObjectName => gameObject.name;
        public string DetailText => definition != null ? definition.DisplayName : "Undefined Resource";
        public Color DetailColor => isCoolingDown ? CooldownColor : AvailableColor;
        public string PromptText => CanInteract ? "Press 'E'" : "Unavailable";
        public float Duration => definition != null ? definition.InteractionDuration : 3f;
        public bool CanInteract => enabled && gameObject.activeInHierarchy && definition != null && !isCoolingDown;

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

            PlayerResourceWallet wallet = interactor.GetComponent<PlayerResourceWallet>();
            if (wallet != null)
                wallet.Add(definition.ResourceType, definition.AmountGathered);

            StartCoroutine(CooldownRoutine());
        }

        private IEnumerator CooldownRoutine()
        {
            isCoolingDown = true;
            visuals.SetActive(false);
            yield return new WaitForSeconds(definition.RespawnCooldown);
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
