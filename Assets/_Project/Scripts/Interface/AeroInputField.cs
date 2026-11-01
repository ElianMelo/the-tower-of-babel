using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Interface
{
    /// <summary>
    /// Frutiger Aero text field: a rounded glass well with a leading glyph, an aqua caret and
    /// a blue-green underline that lights up on hover and focus.
    /// </summary>
    [DisallowMultipleComponent, RequireComponent(typeof(TMP_InputField))]
    public sealed class AeroInputField : MonoBehaviour
    {
        public static readonly Color Ink = new(.055f, .20f, .29f, 1);
        public static readonly Color Well = new(.93f, .985f, 1f, .96f);
        public static readonly Color Accent = new(.16f, .72f, .95f, 1);
        public static readonly Color AccentFocus = new(.30f, .88f, .55f, 1);
        public const float IdleAccent = .25f, HoverAccent = .6f;

        [SerializeField] private TMP_InputField input;
        [SerializeField] private Image accent;
        private bool hovered;
        public TMP_InputField Input => input;
        public Image AccentBar => accent;
        /// <summary>Current underline strength: idle, hover or focused (1).</summary>
        public float AccentStrength => accent != null ? accent.color.a : 0;

        public static TMP_InputField Create(Transform parent, string placeholder, string value, float width, bool numeric,
            TMP_FontAsset font, Material glass = null, string glyph = null, float fontSize = 17)
        {
            var root = NewRect(placeholder, parent);
            var image = root.gameObject.AddComponent<Image>(); image.color = Well;
            if (glass != null) image.material = glass;
            root.gameObject.AddComponent<AeroGlassGeometry>().CornerRadius = 12;
            var size = root.gameObject.AddComponent<LayoutElement>(); size.preferredWidth = size.minWidth = width; size.preferredHeight = 38;

            float left = 12;
            if (!string.IsNullOrEmpty(glyph))
            {
                var icon = NewText(glyph, root, font, fontSize, new Color(Accent.r, Accent.g, Accent.b, .85f));
                icon.alignment = TextAlignmentOptions.Center;
                var r = icon.rectTransform; r.anchorMin = new Vector2(0, 0); r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, .5f);
                r.anchoredPosition = new Vector2(8, 0); r.sizeDelta = new Vector2(24, 0); left = 36;
            }
            var viewport = NewRect("Text area", root); Fill(viewport, left, 12, 4, 6); viewport.gameObject.AddComponent<RectMask2D>();
            var text = NewText(value, viewport, font, fontSize, Ink); Fill(text.rectTransform);
            var hint = NewText(placeholder, viewport, font, fontSize, new Color(.25f, .45f, .52f, .6f)); Fill(hint.rectTransform);
            hint.fontStyle = FontStyles.Italic;

            var bar = NewRect("Accent", root); bar.anchorMin = new Vector2(0, 0); bar.anchorMax = new Vector2(1, 0); bar.pivot = new Vector2(.5f, 0);
            bar.offsetMin = new Vector2(10, 3); bar.offsetMax = new Vector2(-10, 5);
            var barImage = bar.gameObject.AddComponent<Image>(); barImage.raycastTarget = false; barImage.color = WithAlpha(Accent, IdleAccent);

            var field = root.gameObject.AddComponent<TMP_InputField>();
            field.textViewport = viewport; field.textComponent = text; field.placeholder = hint; field.targetGraphic = image;
            field.contentType = numeric ? TMP_InputField.ContentType.DecimalNumber : TMP_InputField.ContentType.Standard;
            field.characterLimit = numeric ? 12 : 80;
            field.customCaretColor = true; field.caretColor = Accent; field.caretWidth = 2; field.caretBlinkRate = .9f;
            field.selectionColor = new Color(.45f, .85f, 1f, .45f);
            field.transition = Selectable.Transition.None;
            field.text = value;

            var aero = root.gameObject.AddComponent<AeroInputField>(); aero.input = field; aero.accent = barImage;
            root.gameObject.AddComponent<AeroHoverFeedback>();
            return field;
        }

        private void Awake() { if (input == null) input = GetComponent<TMP_InputField>(); }

        private void Update()
        {
            if (accent == null || input == null) return;
            bool focused = input.isFocused;
            hovered = IsPointerOver();
            float target = focused ? 1 : hovered ? HoverAccent : IdleAccent;
            var color = Color.Lerp(Accent, AccentFocus, focused ? 1 : 0);
            float alpha = Application.isPlaying ? Mathf.MoveTowards(accent.color.a, target, Time.unscaledDeltaTime / .12f) : target;
            accent.color = WithAlpha(color, alpha);
        }

        private bool IsPointerOver()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null) return hovered;
            return RectTransformUtility.RectangleContainsScreenPoint((RectTransform)transform, mouse.position.ReadValue(), null);
        }

        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        private static RectTransform NewRect(string name, Transform parent)
        {
            var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform; rect.SetParent(parent, false); return rect;
        }
        private static void Fill(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top);
        }
        private static TextMeshProUGUI NewText(string value, Transform parent, TMP_FontAsset font, float size, Color color)
        {
            var text = NewRect("Text", parent).gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size; text.text = value; text.color = color; text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Ellipsis;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }
    }
}
