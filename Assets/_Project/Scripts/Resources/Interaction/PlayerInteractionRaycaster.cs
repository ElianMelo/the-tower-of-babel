using UnityEngine;
using UnityEngine.UI;

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

        [Header("UI")]
        [SerializeField] private Text objectNameText;
        [SerializeField] private string promptFormat = "{0}";

        public GameObject CurrentTarget { get; private set; }

        private void Awake()
        {
            if (interactionCamera == null)
                interactionCamera = GetComponentInChildren<Camera>(true);

            if (interactionCamera == null)
                interactionCamera = Camera.main;

            if (objectNameText == null)
                objectNameText = CreateDefaultHud();

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
            CurrentTarget = target;
            if (objectNameText == null)
                return;

            InteractableName namedTarget = target.GetComponentInParent<InteractableName>();
            string targetName = namedTarget != null ? namedTarget.DisplayName : target.name;
            objectNameText.text = string.Format(promptFormat, targetName);
            objectNameText.enabled = true;
        }

        private void ClearTarget()
        {
            CurrentTarget = null;
            if (objectNameText == null)
                return;

            objectNameText.text = string.Empty;
            objectNameText.enabled = false;
        }

        private static Text CreateDefaultHud()
        {
            GameObject canvasObject = new GameObject("Interaction HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            GameObject textObject = new GameObject("Target Name", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(canvasObject.transform, false);

            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 28f);
            rect.sizeDelta = new Vector2(600f, 60f);

            Text text = textObject.GetComponent<Text>();
            text.font = UnityEngine.Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 28;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
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
