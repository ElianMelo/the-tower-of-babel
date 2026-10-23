using TMPro;
using UnityEngine;

namespace TowerOfBabel.Interface
{
    /// <summary>Presentation-only interpolation of authoritative inventory amounts.</summary>
    [DisallowMultipleComponent]
    public sealed class AeroValueFeedback : MonoBehaviour
    {
        [SerializeField] private TMP_Text amountText;
        [SerializeField] private TMP_Text deltaText;
        [SerializeField] private RectTransform icon;
        [SerializeField] private AeroGlassGeometry surface;
        private int target;
        private float displayed, start, elapsed = 1;
        private Vector2 iconRest, deltaRest;
        private bool initialized;

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            if (icon != null) iconRest = icon.anchoredPosition;
            if (deltaText != null) { deltaRest = deltaText.rectTransform.anchoredPosition; deltaText.alpha = 0; }
        }

        public void SetAmount(int amount, bool immediate = false)
        {
            Initialize(); amount = Mathf.Max(0, amount);
            int difference = amount - target;
            if (immediate || !Application.isPlaying || !isActiveAndEnabled)
            {
                target = amount; displayed = amount; elapsed = 1; Apply(1); return;
            }
            if (difference == 0) return;
            start = displayed; target = amount; elapsed = 0;
            if (deltaText != null)
            {
                deltaText.text = difference.ToString("+0;-0;0");
                deltaText.color = difference > 0 ? new Color(.08f,.52f,.27f,1) : new Color(.1f,.4f,.62f,1);
            }
            AeroUIAudio.Play(AeroUICue.Change);
            Apply(0);
        }

        private void Update()
        {
            if (elapsed >= 1) return;
            elapsed = Mathf.Min(1, elapsed + Time.unscaledDeltaTime / .7f);
            Apply(elapsed);
        }

        private void Apply(float t)
        {
            float count = Mathf.Clamp01(t / .6f);
            displayed = Mathf.Lerp(start, target, 1 - Mathf.Pow(1 - count, 3));
            if (amountText != null) amountText.text = Mathf.RoundToInt(displayed).ToString();
            float lift = Mathf.Sin(t * Mathf.PI);
            if (icon != null) icon.anchoredPosition = iconRest + Vector2.up * (lift * 3);
            surface?.SetFeedback((1 - t) * .55f, new Vector2(.85f,.5f));
            if (deltaText != null)
            {
                deltaText.alpha = (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.35f, 1, t))) * Mathf.Clamp01(t * 12);
                deltaText.rectTransform.anchoredPosition = deltaRest + Vector2.up * (12 * t);
            }
        }

        private void OnDisable()
        {
            if (!initialized) return;
            elapsed = 1; displayed = target; Apply(1);
        }

#if UNITY_EDITOR
        public void SampleEditorChange(int before, int after, float seconds)
        {
            if (Application.isPlaying) return;
            Initialize(); start = before; target = after;
            if (deltaText != null) deltaText.text = (after - before).ToString("+0;-0;0");
            Apply(Mathf.Clamp01(seconds / .7f));
        }
#endif
    }
}
