using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// The sun's look at one sun height. Angles are in degrees from the centre of the sun.
    /// A bright inner circle with a soft edge, a warm glow around it, a wide faint halo, a warm band low in the sky on the
    /// sun's side, and the sun's colour spread over the whole sky. See Documentation/FrutigerAero/Lighting/SUN_GLOW.md.
    /// </summary>
    [Serializable]
    public struct AeroSunGlowShape
    {
        [Tooltip("Radius of the bright inner circle, degrees.")]
        [Range(.1f, 6)] public float coreRadius;
        [Tooltip("Width of the inner circle's soft edge, as a share of its radius.")]
        [Range(0, .9f)] public float coreSoftness;
        [Tooltip("Radius of the warm glow around the inner circle, degrees.")]
        [Range(.5f, 20)] public float glowRadius;
        [Tooltip("Radius of the wide halo, degrees.")]
        [Range(2, 60)] public float haloRadius;
        [Tooltip("HDR brightness of the inner circle; above the bloom threshold so it blooms.")]
        [Min(0)] public float coreIntensity;
        [Min(0)] public float glowIntensity;
        [Tooltip("Kept below the bloom threshold so the halo stays soft.")]
        [Min(0)] public float haloIntensity;
        [Tooltip("Warm band low in the sky on the sun's side.")]
        [Min(0)] public float horizonWash;
        [Tooltip("Colour of the halo and horizon band relative to the light colour.")]
        public Color haloTint;
        [Tooltip("How much the blue sky around the sun gives way to the glow, so a hazy low sun reads golden, not lavender.")]
        [Range(0, 1)] public float skyVeil;
        [Tooltip("Brightness of the sun's colour washed over the whole sky on its side.")]
        [Min(0)] public float skySpread;
        [Tooltip("How far the whole sky's colour moves towards the sun's (brightness kept), most on the sun's side.")]
        [Range(0, 1)] public float skyTint;
        [Tooltip("Strength of the outdoor light shafts in the air towards the sun (AeroSunShaftRendererFeature).")]
        [Min(0)] public float rays;

        public static AeroSunGlowShape Lerp(in AeroSunGlowShape a, in AeroSunGlowShape b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AeroSunGlowShape
            {
                coreRadius = Mathf.Lerp(a.coreRadius, b.coreRadius, t), coreSoftness = Mathf.Lerp(a.coreSoftness, b.coreSoftness, t),
                glowRadius = Mathf.Lerp(a.glowRadius, b.glowRadius, t), haloRadius = Mathf.Lerp(a.haloRadius, b.haloRadius, t),
                coreIntensity = Mathf.Lerp(a.coreIntensity, b.coreIntensity, t), glowIntensity = Mathf.Lerp(a.glowIntensity, b.glowIntensity, t),
                haloIntensity = Mathf.Lerp(a.haloIntensity, b.haloIntensity, t), horizonWash = Mathf.Lerp(a.horizonWash, b.horizonWash, t),
                haloTint = Color.Lerp(a.haloTint, b.haloTint, t), skyVeil = Mathf.Lerp(a.skyVeil, b.skyVeil, t),
                skySpread = Mathf.Lerp(a.skySpread, b.skySpread, t), skyTint = Mathf.Lerp(a.skyTint, b.skyTint, t), rays = Mathf.Lerp(a.rays, b.rays, t)
            };
        }
    }

    /// <summary>How the sun (or moon) glows in the sky and over distant ground, from high in the sky to low over the horizon.</summary>
    [Serializable]
    public sealed class AeroSunGlowSettings
    {
        [Tooltip("High sun, seen at the edge of the screen: a calm, small sun.")]
        public AeroSunGlowShape highSun;
        [Tooltip("Low sun, seen at the edge of the screen.")]
        public AeroSunGlowShape lowSun;
        [Tooltip("High sun, looked at straight on. The look blends towards the edge one as the sun moves to the screen's edge.")]
        public AeroSunGlowShape highSunFocused;
        [Tooltip("Low sun, looked at straight on: the full golden-hour glow.")]
        public AeroSunGlowShape lowSunFocused;
        [Tooltip("Sine of the sun's height where the look is fully low (x) and fully high (y). Wider than the sun colour's " +
                 "range so the 16:30 golden-hour preset, with the sun about 22 degrees up, still reads as low sun.")]
        public Vector2 lowSunRange = new(.08f, .8f);
        [Tooltip("How far the inner circle's colour moves from the light colour towards white.")]
        [Range(0, 1)] public float coreWhiteness = .45f;
        [Tooltip("Height of the warm horizon band, as the sine of the height above the horizon.")]
        [Range(.02f, 1)] public float washHeight = .22f;
        [Tooltip("How much the atmosphere preset's haze (fog density scale) strengthens and widens the halo.")]
        [Range(0, 1)] public float hazeResponse = .6f;
        [Tooltip("How tightly the sky spread gathers towards the sun: 1 covers the whole sun-side sky evenly, higher narrows it.")]
        [Range(.5f, 8)] public float spreadPower = 2.5f;

        [Header("Overcast (rain clouds)")]
        [Tooltip("Glow brightness under full overcast; the inner circle disappears.")]
        [Range(0, 1)] public float overcastGlow = .5f;
        [Tooltip("Halo brightness under full overcast.")]
        [Range(0, 1)] public float overcastHalo = .6f;
        [Range(1, 3)] public float overcastGlowWiden = 1.6f;
        [Range(1, 3)] public float overcastHaloWiden = 1.4f;
        [Tooltip("Light shaft strength under full overcast.")]
        [Range(0, 1)] public float overcastRays = .4f;

        [Header("Looking at the sun")]
        [Tooltip("Distance of the sun from the screen centre (0 centre, 1 edge) within which the focused look is full.")]
        [Range(0, .9f)] public float focusCentre = .1f;
        [Tooltip("Distance from the screen centre where the edge look is reached (1 is the edge of the screen).")]
        [Range(.2f, 2)] public float focusEdge = 1;

        [Header("Haze over distant ground (AeroSunHazeRendererFeature)")]
        [Tooltip("Haze per metre: distant ground on the sun's side fades into the halo.")]
        [Range(0, .01f)] public float hazeDensity = .003f;
        [Range(0, 1)] public float lowSunHaze = .85f;
        [Range(0, 1)] public float highSunHaze = .35f;
        [Tooltip("The haze thins above this height, metres.")]
        public float hazeBaseHeight;
        [Tooltip("How fast the haze thins with height above the base, per metre.")]
        [Range(0, .02f)] public float hazeHeightFalloff = .0025f;

        [Header("Outdoor light shafts (AeroSunShaftRendererFeature)")]
        [Tooltip("Share of the air without a shaft: higher leaves fewer, more separate beams.")]
        [Range(0, .95f)] public float shaftGaps = .6f;
        [Tooltip("Typical width of a shaft, metres.")]
        [Range(.5f, 30)] public float shaftWidth = 8;
        [Tooltip("Typical length of a shaft along the sunlight, metres.")]
        [Range(5, 300)] public float shaftLength = 120;
        [Tooltip("How far from the camera shafts are drawn, metres (the sun's shadows reach 120 m).")]
        [Range(10, 200)] public float shaftRange = 110;
        [Tooltip("Shafts thin out above the haze base at this rate, per metre.")]
        [Range(0, .1f)] public float shaftHeightFalloff = .02f;
        [Tooltip("How fast the shafts drift sideways, metres per second.")]
        [Range(0, 3)] public float shaftDrift = .35f;

        [Header("Bloom")]
        [Range(0, 1)] public float highSunBloomScatter = .68f;
        [Range(0, 1)] public float lowSunBloomScatter = .8f;

        /// <summary>
        /// Tuned to the golden-hour reference. Looked at straight on, a white-hot circle melts into a wide golden halo
        /// and warms the whole sky; at the edge of the screen the sun is small and calm.
        /// </summary>
        public static AeroSunGlowSettings Sun() => new()
        {
            highSun = new AeroSunGlowShape
            {
                coreRadius = .9f, coreSoftness = .75f, glowRadius = 3.5f, haloRadius = 11,
                coreIntensity = 3, glowIntensity = .8f, haloIntensity = .2f, horizonWash = 0, haloTint = new Color(1, .97f, .92f), skyVeil = .1f,
                skySpread = .03f, skyTint = 0, rays = .6f
            },
            lowSun = new AeroSunGlowShape
            {
                coreRadius = 1.6f, coreSoftness = .8f, glowRadius = 6.5f, haloRadius = 22,
                coreIntensity = 3, glowIntensity = 1.4f, haloIntensity = .55f, horizonWash = .4f, haloTint = new Color(1, .82f, .55f), skyVeil = .6f,
                skySpread = .18f, skyTint = .32f, rays = 1.4f
            },
            highSunFocused = new AeroSunGlowShape
            {
                coreRadius = 1.1f, coreSoftness = .8f, glowRadius = 5, haloRadius = 16,
                coreIntensity = 4, glowIntensity = 1.2f, haloIntensity = .3f, horizonWash = 0, haloTint = new Color(1, .97f, .92f), skyVeil = .15f,
                skySpread = .04f, skyTint = 0, rays = .6f
            },
            lowSunFocused = new AeroSunGlowShape
            {
                coreRadius = 2.6f, coreSoftness = .85f, glowRadius = 11, haloRadius = 36,
                coreIntensity = 3.5f, glowIntensity = 2.4f, haloIntensity = .9f, horizonWash = .55f, haloTint = new Color(1, .82f, .55f), skyVeil = .8f,
                skySpread = .35f, skyTint = .45f, rays = 1.4f
            }
        };

        /// <summary>A small cool glow around the moon, the same wherever it is on screen; no haze over the ground.</summary>
        public static AeroSunGlowSettings Moon()
        {
            var moon = MoonEdge();
            moon.highSunFocused = moon.highSun; moon.lowSunFocused = moon.lowSun;
            return moon;
        }

        private static AeroSunGlowSettings MoonEdge() => new()
        {
            highSun = new AeroSunGlowShape
            {
                coreRadius = .6f, coreSoftness = .5f, glowRadius = 2, haloRadius = 6,
                coreIntensity = 2, glowIntensity = .35f, haloIntensity = .08f, horizonWash = 0, haloTint = Color.white,
                skySpread = .02f, rays = .25f
            },
            lowSun = new AeroSunGlowShape
            {
                coreRadius = .7f, coreSoftness = .5f, glowRadius = 2.6f, haloRadius = 8,
                coreIntensity = 2, glowIntensity = .4f, haloIntensity = .1f, horizonWash = .05f, haloTint = Color.white,
                skySpread = .03f, rays = .25f
            },
            coreWhiteness = .45f, hazeResponse = .3f, lowSunHaze = 0, highSunHaze = 0, lowSunBloomScatter = .68f
        };
    }

    /// <summary>The glow for one frame, as pushed to the shaders.</summary>
    public struct AeroSunGlowState
    {
        /// <summary>Unit vector towards the light.</summary>
        public Vector3 direction;
        /// <summary>Linear light hue (largest channel 1) times visibility; alpha is the inner circle's whiteness.</summary>
        public Color color;
        /// <summary>Core radius, core softness, glow radius, halo radius (degrees).</summary>
        public Vector4 shape;
        /// <summary>Core, glow, halo and horizon band brightness.</summary>
        public Vector4 strength;
        /// <summary>Halo tint; alpha is the horizon band height.</summary>
        public Color tint;
        /// <summary>Haze per metre, amount, height falloff per metre, cloud fade.</summary>
        public Vector4 haze;
        /// <summary>Height the haze thins above, metres; also the sea level open water is hazed at.</summary>
        public float hazeBase;
        /// <summary>0..1: how much the blue sky gives way to the glow at the glow's centre.</summary>
        public float veil;
        /// <summary>Sky spread brightness, spread power, whole-sky tint amount, unused.</summary>
        public Vector4 spread;
        /// <summary>Outdoor light shafts: strength, gaps, 1/width, 1/length (per metre).</summary>
        public Vector4 rays;
        /// <summary>Outdoor light shafts: range (m), height falloff (per m), drift (m/s), haze base height (m).</summary>
        public Vector4 shafts;
        /// <summary>Screen distance (0 centre, 1 edge) where the focused look is full, and where the edge look is reached.</summary>
        public Vector2 focusRange;
        public float lowSun;
        public float bloomScatter;

        public bool HazeVisible => haze.x > 0 && haze.y * haze.w > .001f && (color.r + color.g + color.b) > .001f;
        public bool RaysVisible => rays.x > .001f && (color.r + color.g + color.b) > .001f;

        /// <summary>Blends two looks of the same frame (the sun at the screen's edge and looked at straight on).</summary>
        public static AeroSunGlowState Lerp(in AeroSunGlowState a, in AeroSunGlowState b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AeroSunGlowState
            {
                direction = b.direction, color = Color.Lerp(a.color, b.color, t),
                shape = Vector4.Lerp(a.shape, b.shape, t), strength = Vector4.Lerp(a.strength, b.strength, t),
                tint = Color.Lerp(a.tint, b.tint, t), haze = Vector4.Lerp(a.haze, b.haze, t), hazeBase = Mathf.Lerp(a.hazeBase, b.hazeBase, t),
                veil = Mathf.Lerp(a.veil, b.veil, t), spread = Vector4.Lerp(a.spread, b.spread, t), rays = Vector4.Lerp(a.rays, b.rays, t),
                shafts = Vector4.Lerp(a.shafts, b.shafts, t), focusRange = b.focusRange, lowSun = Mathf.Lerp(a.lowSun, b.lowSun, t),
                bloomScatter = Mathf.Lerp(a.bloomScatter, b.bloomScatter, t)
            };
        }
    }

    /// <summary>
    /// The soft sun glow model, shared by the sky (AeroSunGlowSky.shader), the haze over distant ground
    /// (AeroSunHaze.shader) and the cloud layer's far haze. <see cref="Radiance"/> mirrors AeroSunGlow.hlsl.
    /// </summary>
    public static class AeroSunGlow
    {
        public static readonly int DirectionId = Shader.PropertyToID("_AeroSunGlowDir");
        public static readonly int ColorId = Shader.PropertyToID("_AeroSunGlowColor");
        public static readonly int ShapeId = Shader.PropertyToID("_AeroSunGlowShape");
        public static readonly int StrengthId = Shader.PropertyToID("_AeroSunGlowStrength");
        public static readonly int TintId = Shader.PropertyToID("_AeroSunGlowTint");
        public static readonly int HazeId = Shader.PropertyToID("_AeroSunHaze");
        public static readonly int HazeBaseId = Shader.PropertyToID("_AeroSunHazeBase");
        public static readonly int VeilId = Shader.PropertyToID("_AeroSunGlowVeil");
        public static readonly int SpreadId = Shader.PropertyToID("_AeroSunGlowSpread");
        /// <summary>The state last pushed to the shaders (for the camera rendering now, when cameras are tracked).</summary>
        public static AeroSunGlowState Current { get; private set; }
        /// <summary>This frame's look with the sun at the edge of the screen, and looked at straight on.</summary>
        public static AeroSunGlowState Edge { get; private set; }
        public static AeroSunGlowState Focused { get; private set; }

        /// <summary>0 when the light is high (by <see cref="AeroSunGlowSettings.lowSunRange"/>), 1 when it is near the horizon.</summary>
        public static float LowSun(AeroSunGlowSettings settings, float elevationSine)
        {
            var range = settings != null ? settings.lowSunRange : new Vector2(.08f, .8f);
            return 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(range.x, range.y, elevationSine));
        }

        public static float BloomScatter(AeroSunGlowSettings settings, float lowSun)
            => settings == null ? .68f : Mathf.Lerp(settings.highSunBloomScatter, settings.lowSunBloomScatter, Mathf.Clamp01(lowSun));

        /// <param name="towardLight">Unit vector towards the sun or moon.</param>
        /// <param name="lightColor">The light's colour (gamma); only its hue is used.</param>
        /// <param name="visibility">0 hides the glow (night for the sun, day for the moon), 1 shows it fully.</param>
        /// <param name="overcast">Rain clouds, 0 clear .. 1 full overcast.</param>
        /// <param name="haze">The atmosphere preset's fog density scale (1 neutral).</param>
        /// <param name="cloudShade">Sunlight blocked by the volumetric clouds, 0..1; fades the ground haze.</param>
        /// <param name="focus">1 looking straight at the sun (the focused look), 0 with it at the edge of the screen.</param>
        public static AeroSunGlowState Evaluate(AeroSunGlowSettings settings, Vector3 towardLight, Color lightColor, float visibility,
            float overcast, float haze, float cloudShade, float focus = 1)
        {
            settings ??= AeroSunGlowSettings.Sun();
            towardLight = towardLight.sqrMagnitude > 1e-8f ? towardLight.normalized : Vector3.up;
            visibility = Mathf.Clamp01(visibility); overcast = Mathf.Clamp01(overcast);
            float low = LowSun(settings, towardLight.y);
            var s = AeroSunGlowShape.Lerp(settings.highSun, settings.lowSun, low);
            // Settings saved before the focused look existed have none: they keep the edge look everywhere.
            if (settings.lowSunFocused.glowRadius > 0 && settings.highSunFocused.glowRadius > 0)
                s = AeroSunGlowShape.Lerp(s, AeroSunGlowShape.Lerp(settings.highSunFocused, settings.lowSunFocused, low), focus);
            float hazeScale = Mathf.Max(.1f, Mathf.Lerp(1, haze, settings.hazeResponse));

            Color linear = lightColor.linear;
            float peak = Mathf.Max(linear.r, Mathf.Max(linear.g, linear.b));
            Color hue = peak > 1e-4f ? linear / peak : Color.black;
            hue *= visibility; hue.a = Mathf.Clamp01(settings.coreWhiteness);
            Color tint = s.haloTint.linear; tint.a = Mathf.Max(.02f, settings.washHeight);

            return new AeroSunGlowState
            {
                direction = towardLight,
                color = hue,
                shape = new Vector4(Mathf.Max(.05f, s.coreRadius), Mathf.Clamp(s.coreSoftness, 0, .95f),
                    Mathf.Max(.1f, s.glowRadius * Mathf.Lerp(1, settings.overcastGlowWiden, overcast)),
                    Mathf.Max(.5f, s.haloRadius * Mathf.Lerp(1, settings.overcastHaloWiden, overcast) * Mathf.Sqrt(hazeScale))),
                strength = new Vector4(Mathf.Max(0, s.coreIntensity) * (1 - overcast),
                    Mathf.Max(0, s.glowIntensity) * Mathf.Lerp(1, settings.overcastGlow, overcast),
                    Mathf.Max(0, s.haloIntensity) * hazeScale * Mathf.Lerp(1, settings.overcastHalo, overcast), Mathf.Max(0, s.horizonWash) * hazeScale),
                tint = tint,
                haze = new Vector4(settings.hazeDensity, Mathf.Lerp(settings.highSunHaze, settings.lowSunHaze, low) * visibility,
                    settings.hazeHeightFalloff, 1 - Mathf.Clamp01(cloudShade)),
                hazeBase = settings.hazeBaseHeight,
                veil = Mathf.Clamp01(s.skyVeil * Mathf.Lerp(1, hazeScale, .5f)) * visibility,
                spread = new Vector4(Mathf.Max(0, s.skySpread) * hazeScale, Mathf.Max(.1f, settings.spreadPower), Mathf.Clamp01(s.skyTint), 0),
                rays = new Vector4(Mathf.Max(0, s.rays) * Mathf.Lerp(1, settings.overcastRays, overcast) * visibility, Mathf.Clamp(settings.shaftGaps, 0, .95f),
                    1 / Mathf.Max(.1f, settings.shaftWidth), 1 / Mathf.Max(1, settings.shaftLength)),
                shafts = new Vector4(Mathf.Max(1, settings.shaftRange), Mathf.Max(0, settings.shaftHeightFalloff), Mathf.Max(0, settings.shaftDrift), settings.hazeBaseHeight),
                focusRange = new Vector2(Mathf.Clamp(settings.focusCentre, 0, .95f), Mathf.Max(settings.focusEdge, settings.focusCentre + .05f)),
                lowSun = low,
                bloomScatter = BloomScatter(settings, low)
            };
        }

        /// <summary>Pushes one look for every camera (reviews and tests).</summary>
        public static void Push(in AeroSunGlowState state) => Push(state, state);

        /// <summary>
        /// Pushes this frame's two looks. The focused one shows until a tracked camera blends them by where the sun is
        /// on its screen (see <see cref="TrackCameras"/>).
        /// </summary>
        public static void Push(in AeroSunGlowState edge, in AeroSunGlowState focused)
        {
            Edge = edge; Focused = focused;
            PushGlobals(focused);
        }

        private static void PushGlobals(in AeroSunGlowState state)
        {
            Current = state;
            var d = state.direction;
            Shader.SetGlobalVector(DirectionId, new Vector4(d.x, d.y, d.z, 1));
            Shader.SetGlobalVector(ColorId, state.color);
            Shader.SetGlobalVector(ShapeId, state.shape);
            Shader.SetGlobalVector(StrengthId, state.strength);
            Shader.SetGlobalVector(TintId, state.tint);
            Shader.SetGlobalVector(HazeId, state.haze);
            Shader.SetGlobalFloat(HazeBaseId, state.hazeBase);
            Shader.SetGlobalFloat(VeilId, state.veil);
            Shader.SetGlobalVector(SpreadId, state.spread);
        }

        // ---- Looking at the sun -----------------------------------------------------------------------------------

        /// <summary>
        /// 1 with the sun at the centre of the camera's screen, 0 at (or beyond) the screen's edge and behind the camera,
        /// smoothly between: how much of the focused look the camera sees.
        /// </summary>
        public static float ScreenFocus(Camera camera, in AeroSunGlowState state)
        {
            if (camera == null) return 1;
            Vector3 point = camera.WorldToViewportPoint(camera.transform.position + state.direction * Mathf.Max(1, camera.nearClipPlane * 10));
            if (point.z <= 0) return 0;
            var range = state.focusRange == Vector2.zero ? new Vector2(.1f, 1) : state.focusRange;
            float distance = new Vector2((point.x - .5f) * 2, (point.y - .5f) * 2).magnitude;
            return 1 - SmoothStep(range.x, range.y, distance);
        }

        private static int trackers;

        /// <summary>
        /// While tracked (by the day/night cycle), each game and Scene View camera blends the frame's two looks by where
        /// the sun is on its screen before it renders. Other cameras (reflections, probes) keep the last camera's look,
        /// so a reflection cubemap's faces all match.
        /// </summary>
        public static void TrackCameras(bool track)
        {
            if (track) { if (trackers++ == 0) RenderPipelineManager.beginCameraRendering += OnBeginCamera; }
            else if (trackers > 0 && --trackers == 0) RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView)) return;
            PushGlobals(AeroSunGlowState.Lerp(Edge, Focused, ScreenFocus(camera, Focused)));
        }

        // ---- CPU mirror of AeroSunGlow.hlsl ----------------------------------------------------------------------

        /// <summary>Glow added to the sky (linear HDR) looking along <paramref name="view"/>.</summary>
        public static Color Radiance(in AeroSunGlowState state, Vector3 view, bool includeCore = true)
        {
            view = view.normalized;
            Terms(state, view, out float core, out float glow, out float halo, out float wash, out float above, out float lowered);
            float spread = Spread(state, view);
            Color hue = state.color; hue.a = 1;
            float peak = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b));
            Color coreColor = Color.Lerp(hue, new Color(peak, peak, peak, 1), state.color.a);
            Color tint = state.tint; tint.a = 1;
            var s = state.strength;
            Color c = hue * (glow * s.y * lowered) + hue * tint * ((halo * s.z + wash * s.w + spread * state.spread.x) * lowered);
            if (includeCore) c += coreColor * (core * s.x * above);
            c.a = 1;
            return c;
        }

        /// <summary>The sky behind the glow is multiplied by this, so the blue gives way to the glow near a hazy sun.</summary>
        public static float SkyKeep(in AeroSunGlowState state, Vector3 view) => 1 - state.veil * Mask(state, view);

        /// <summary>The sun-side falloff of the sky spread, 1 facing the sun, 0 facing away.</summary>
        public static float Spread(in AeroSunGlowState state, Vector3 view)
            => Mathf.Pow(Mathf.Clamp01(.5f + .5f * Vector3.Dot(view.normalized, state.direction)), Mathf.Max(.1f, state.spread.y));

        /// <summary>
        /// How far the sky's colour moves towards the sun's colour (its brightness kept) looking along
        /// <paramref name="view"/>: most on the sun's side, still a little opposite it.
        /// </summary>
        public static float SkyTint(in AeroSunGlowState state, Vector3 view)
        {
            float peak = Mathf.Max(state.color.r, Mathf.Max(state.color.g, state.color.b));
            float broad = Mathf.Pow(Mathf.Clamp01(.5f + .5f * Vector3.Dot(view.normalized, state.direction)), 1.5f);
            return Mathf.Clamp01(state.spread.z * (.35f + .65f * broad) * Mathf.Clamp01(peak));
        }

        /// <summary>How strongly a view direction lies in the sun's glow, 0..1: where haze takes on the glow's colour.</summary>
        public static float Mask(in AeroSunGlowState state, Vector3 view)
        {
            Terms(state, view.normalized, out _, out float glow, out float halo, out _, out _, out _);
            return Mathf.Clamp01(glow + halo);
        }

        private static void Terms(in AeroSunGlowState state, Vector3 view, out float core, out float glow, out float halo,
            out float wash, out float above, out float lowered)
        {
            Vector3 sun = state.direction;
            float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(view, sun), -1, 1)) * Mathf.Rad2Deg;
            var shape = state.shape;
            float inner = shape.x * (1 - shape.y), outer = Mathf.Max(shape.x * (1 + shape.y), inner + 1e-3f);
            core = 1 - SmoothStep(inner, outer, angle);
            glow = Mathf.Exp(-(angle * angle) / Mathf.Max(shape.z * shape.z, 1e-4f));
            halo = Mathf.Pow(1 + angle * angle / Mathf.Max(shape.w * shape.w, 1e-4f), -1.5f);
            float flatDot = view.x * sun.x + view.z * sun.z;
            float flatLengths = (view.x * view.x + view.z * view.z) * (sun.x * sun.x + sun.z * sun.z);
            float side = Mathf.Clamp01(flatDot / Mathf.Sqrt(Mathf.Max(flatLengths, 1e-8f)) * .5f + .5f);
            wash = side * side * side * Mathf.Exp(-Mathf.Max(view.y, 0) / Mathf.Max(state.tint.a, .01f));
            above = SmoothStep(-.012f, .004f, view.y);
            lowered = Mathf.Lerp(.3f, 1, SmoothStep(-.12f, 0, view.y));
        }

        public static float Luminance(Color c) => .2126f * c.r + .7152f * c.g + .0722f * c.b;

        /// <summary>HLSL smoothstep (Mathf.SmoothStep interpolates between its first two arguments instead).</summary>
        public static float SmoothStep(float edge0, float edge1, float x)
        {
            float t = Mathf.Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3 - 2 * t);
        }
    }
}
