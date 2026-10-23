using UnityEngine;

namespace TowerOfBabel.Interface
{
    /// <summary>Reversible unscaled reveals; closing releases hit testing immediately.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CanvasGroup))]
    public sealed class AeroPanelMotion : MonoBehaviour
    {
        [SerializeField] private bool animateTransform = true;
        [SerializeField] private bool playSounds = true;
        [SerializeField] private bool allowInteraction = true;
        [SerializeField, Min(0)] private float siblingStagger;
        [SerializeField, Min(.05f)] private float revealDuration = .24f;
        [SerializeField, Min(.05f)] private float dismissDuration = .14f;
        private CanvasGroup group;
        private RectTransform rect;
        private Vector2 restPosition;
        private Vector3 restScale;
        private float amount, delay;
        private bool initialized;
        public bool IsOpen { get; private set; }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true; group = GetComponent<CanvasGroup>(); rect = (RectTransform)transform;
            restPosition = rect.anchoredPosition; restScale = rect.localScale;
        }

        private void OnEnable()
        {
            Initialize(); IsOpen = true; amount = 0;
            delay = Mathf.Min(6, transform.GetSiblingIndex()) * siblingStagger;
            Apply();
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        private void Advance(float dt)
        {
            if (delay > 0 && IsOpen) { delay -= dt; return; }
            amount = Mathf.MoveTowards(amount, IsOpen ? 1 : 0, dt / (IsOpen ? revealDuration : dismissDuration));
            Apply();
            if (!IsOpen && amount <= 0) gameObject.SetActive(false);
        }

        private void Apply()
        {
            float eased = 1 - Mathf.Pow(1 - amount, 3);
            group.alpha = eased;
            group.blocksRaycasts = group.interactable = IsOpen && allowInteraction && amount > .12f;
            if (!animateTransform) return;
            rect.anchoredPosition = restPosition + Vector2.down * (10 * (1 - eased));
            rect.localScale = restScale * Mathf.Lerp(.985f, 1, eased);
        }

        private void OnDisable()
        {
            IsOpen = false;
            if (!initialized) return;
            if (animateTransform) { rect.anchoredPosition = restPosition; rect.localScale = restScale; }
            group.alpha = 1; group.blocksRaycasts = group.interactable = allowInteraction;
        }

        public static bool IsVisible(GameObject root)
            => root != null && root.activeSelf && (!Application.isPlaying || root.GetComponent<AeroPanelMotion>() is not { } motion || motion.IsOpen);

        public static void SetVisible(GameObject root, bool visible)
        {
            if (root == null) return;
            var motion = root.GetComponent<AeroPanelMotion>();
            if (motion == null || !Application.isPlaying) { root.SetActive(visible); return; }
            motion.Initialize();
            if (visible)
            {
                bool wasOpen = root.activeSelf && motion.IsOpen;
                if (!root.activeSelf) root.SetActive(true);
                motion.IsOpen = true; motion.delay = 0;
                if (!wasOpen && motion.playSounds) AeroUIAudio.Play(AeroUICue.Open);
            }
            else
            {
                if (!root.activeSelf) return;
                bool wasOpen = motion.IsOpen;
                motion.IsOpen = false; motion.group.blocksRaycasts = motion.group.interactable = false;
                if (wasOpen && motion.playSounds) AeroUIAudio.Play(AeroUICue.Close);
                if (motion.amount <= 0) root.SetActive(false);
            }
        }

#if UNITY_EDITOR
        public void SampleEditorReveal(float visibility)
        {
            if (Application.isPlaying) return;
            Initialize(); IsOpen = true; amount = Mathf.Clamp01(visibility); Apply();
        }
#endif
    }
}
