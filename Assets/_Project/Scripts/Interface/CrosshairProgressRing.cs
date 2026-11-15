using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Interface
{
    /// <summary>
    /// Glossy aqua loading ring at the centre of the screen, where the crosshair aims, while gathering, processing or
    /// building runs. It fills clockwise from the top and pops in with a small overshoot. The artwork is generated
    /// once at runtime, so the ring needs no texture asset.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CrosshairProgressRing : MonoBehaviour
    {
        public const float DefaultSize = 60f;
        private const int TextureSize = 128;
        private const float OuterRadius = 0.47f;
        private const float InnerRadius = 0.33f;

        private static readonly Color Ink = new(.035f, .22f, .34f, 1f);
        private static readonly Color Azure = new(.06f, .58f, .91f, 1f);
        private static readonly Color Aqua = new(.55f, .93f, 1f, 1f);
        private static Sprite trackSprite, fillSprite, dotSprite;

        [SerializeField] private RectTransform body;
        [SerializeField] private Image fill;
        [SerializeField, Min(0.01f)] private float popDuration = .16f;

        private float shownAt;

        public bool IsVisible { get; private set; }
        public float Progress => fill != null ? fill.fillAmount : 0f;

        /// <summary>Builds a hidden ring centred on <paramref name="parent"/>.</summary>
        public static CrosshairProgressRing Create(Transform parent, float size = DefaultSize)
        {
            var root = new GameObject("CrosshairProgressRing", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);

            var ring = root.AddComponent<CrosshairProgressRing>();
            ring.body = (RectTransform)new GameObject("Body", typeof(RectTransform)).transform;
            ring.body.SetParent(rect, false);
            ring.body.anchorMin = Vector2.zero;
            ring.body.anchorMax = Vector2.one;
            ring.body.offsetMin = ring.body.offsetMax = Vector2.zero;
            NewImage("Track", ring.body, TrackSprite, Color.white);
            ring.fill = NewImage("Fill", ring.body, FillSprite, Color.white);
            ring.fill.type = Image.Type.Filled;
            ring.fill.fillMethod = Image.FillMethod.Radial360;
            ring.fill.fillOrigin = (int)Image.Origin360.Top;
            ring.fill.fillClockwise = true;
            ring.fill.fillAmount = 0f;
            Image dot = NewImage("Dot", ring.body, DotSprite, Color.white);
            dot.rectTransform.anchorMin = dot.rectTransform.anchorMax = new Vector2(.5f, .5f);
            dot.rectTransform.sizeDelta = new Vector2(size * .16f, size * .16f);
            root.SetActive(false);
            return ring;
        }

        /// <summary>Shows the ring filled to <paramref name="normalizedProgress"/> (0 to 1).</summary>
        public void SetProgress(float normalizedProgress)
        {
            if (!IsVisible)
            {
                IsVisible = true;
                shownAt = Time.unscaledTime;
                gameObject.SetActive(true);
                ApplyPop(0f);
            }

            if (fill != null)
                fill.fillAmount = Mathf.Clamp01(normalizedProgress);
        }

        public void Hide()
        {
            IsVisible = false;
            if (fill != null)
                fill.fillAmount = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            ApplyPop(Mathf.Clamp01((Time.unscaledTime - shownAt) / popDuration));
        }

        private void ApplyPop(float t)
        {
            if (body == null)
                return;
            // Ease-out-back: grows past full size, then settles.
            const float overshoot = 1.7f;
            float u = t - 1f;
            float eased = 1f + (overshoot + 1f) * u * u * u + overshoot * u * u;
            body.localScale = Vector3.one * Mathf.LerpUnclamped(.72f, 1f, eased);
        }

        private static Image NewImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        // A translucent pearl band with ink rims, readable over both the white tower and the bright sky.
        private static Sprite TrackSprite => trackSprite != null ? trackSprite : trackSprite = Bake("Crosshair ring track", (r, y) =>
        {
            float band = Band(r, OuterRadius + .025f, InnerRadius - .025f);
            float rim = 1f - Mathf.Clamp01(Mathf.Min(r - (InnerRadius - .025f), OuterRadius + .025f - r) / .035f);
            Color color = Color.Lerp(new Color(.9f, .97f, 1f), Ink, rim * .85f);
            color.a = band * Mathf.Lerp(.42f, .62f, rim);
            return color;
        });

        // Azure at the bottom brightening to aqua at the top, with a white gloss along the upper outer edge.
        private static Sprite FillSprite => fillSprite != null ? fillSprite : fillSprite = Bake("Crosshair ring fill", (r, y) =>
        {
            float band = Band(r, OuterRadius, InnerRadius);
            Color color = Color.Lerp(Azure, Aqua, Mathf.SmoothStep(0f, 1f, y * .5f + .5f));
            float across = Mathf.InverseLerp(InnerRadius, OuterRadius, r);
            float gloss = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.45f, .85f, across)) * Mathf.Clamp01(y * 1.4f + .2f) * .55f;
            color = Color.Lerp(color, Color.white, gloss);
            color.a = band;
            return color;
        });

        // A small pearl bead marking the exact aim point.
        private static Sprite DotSprite => dotSprite != null ? dotSprite : dotSprite = Bake("Crosshair ring dot", (r, y) =>
        {
            float disc = Band(r, .44f, -1f);
            float rim = Mathf.Clamp01((r - .3f) / .14f);
            Color color = Color.Lerp(Color.white, Ink, rim * .7f);
            color.a = disc * .95f;
            return color;
        });

        // Anti-aliased coverage of the band between two radii (in texture-width units), one texel soft.
        private static float Band(float r, float outer, float inner)
        {
            float texel = 1f / TextureSize;
            return Mathf.Clamp01((outer - r) / texel + .5f) * Mathf.Clamp01((r - inner) / texel + .5f);
        }

        /// <summary>Paints a square sprite from a function of radius and height (both relative to the centre).</summary>
        private static Sprite Bake(string name, System.Func<float, float, Color> paint)
        {
            var texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                float u = (x + .5f) / TextureSize - .5f;
                float v = (y + .5f) / TextureSize - .5f;
                float radius = Mathf.Sqrt(u * u + v * v);
                pixels[y * TextureSize + x] = paint(radius, v / .5f);
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, TextureSize, TextureSize), new Vector2(.5f, .5f), 100f);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
