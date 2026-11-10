using System;
using System.Globalization;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Applies the atmosphere presets every frame: reads the time of day, the weather (rain amount and cloud
    /// coverage, or a forced weather) and the chosen season, composes the preset blend, and drives a runtime global
    /// volume layered over the Restrained profile, the day/night cycle's sun, ambient and fog, and the seasonal
    /// floating particles. Visual only and local; time and rain stay synced by the weather service.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-50)]
    public sealed class AeroAtmosphereDirector : MonoBehaviour
    {
        public const string SeasonKey = "Aero.Season";
        public static AeroAtmosphereDirector Instance { get; private set; }
        private static readonly ProfilerMarker TickMarker = new("Aero.Atmosphere.Tick");

        public AeroAtmospherePresetLibrary library;
        public AeroDayNightCycle dayNight;
        public AeroWetnessController wetness;
        public AeroVolumetricClouds clouds;
        [Tooltip("Optional: camera under a built roof stops falling particles.")]
        public AeroTowerAtmosphere tower;
        [Tooltip("The scene's global Restrained volume; the preset grade sits one priority above it.")]
        public Volume baseVolume;
        public Shader particleShader;
        [Tooltip("Cloud preset shown while Cloudy is forced from the graphics window.")]
        public AeroCloudPreset cloudyClouds;
        public AeroSeason defaultSeason = AeroSeason.Summer;
        [Min(0)] public float weatherBlendSeconds = 4;
        [Min(0)] public float seasonBlendSeconds = 3;
        [Tooltip("Cloud coverage range that reads as clear (below) to cloudy (above) in automatic weather.")]
        public Vector2 cloudyCoverage = new(.4f, .65f);
        public bool particlesEnabled = true;
        [Tooltip("Time of day used when there is no day/night cycle (previews and tests).")]
        [Range(0, 1)] public float fallbackTimeOfDay = .5f;

        private AeroSeason season, fromSeason;
        private float seasonBlend = 1;
        private AeroWeatherKind? weatherOverride;
        private readonly float[] weights = { 1, 0, 0 };
        private bool initialized, weightsPrimed;
        private IAeroSettingsStore store;
        private GameObject gradeObject;
        private VolumeProfile gradeProfile;
        private ColorAdjustments color;
        private WhiteBalance white;
        private Bloom bloom;
        private Vignette vignette;
        private SplitToning split;
        private AeroAmbientParticles particles;

        public IAeroSettingsStore Store { get => store ??= new PlayerPrefsAeroSettingsStore(); set { store = value; initialized = false; } }
        public AeroSeason Season => season;
        public float SeasonBlend => seasonBlend;
        public AeroWeatherKind? WeatherOverride => weatherOverride;
        public float[] WeatherWeights => (float[])weights.Clone();
        private AeroAtmospherePresetLibrary.Frame current;
        private readonly float[] targetWeights = new float[AeroAtmospherePresetLibrary.WeatherCount];
        /// <summary>The look applied this frame. Its particle array is reused every frame; copy it to keep it.</summary>
        public AeroAtmospherePresetLibrary.Frame Current => current;
        public Volume GradeVolume => gradeObject != null ? gradeObject.GetComponent<Volume>() : null;
        public AeroAmbientParticles Particles => particles;
        public float TimeOfDay => dayNight != null ? dayNight.timeOfDay : fallbackTimeOfDay;
        public AeroTimeSlot NearestSlot => library != null ? library.NearestSlot(TimeOfDay) : AeroTimeSlot.MidDay;
        public AeroWeatherKind DominantWeather => Current.weather;
        public int CurrentIndex => AeroAtmospherePresetLibrary.IndexOf(NearestSlot, DominantWeather, season);
        public string Description => AeroAtmospherePresetLibrary.Describe(NearestSlot, DominantWeather, season) +
                                     (weatherOverride.HasValue ? "" : "  (auto weather)");

        private void OnEnable()
        {
            Instance = this;
            initialized = false;
        }

        private void OnDisable() => Shutdown();

        /// <summary>Removes the grade volume and particles and hands lighting back to the day/night cycle.</summary>
        public void Shutdown()
        {
            if (Instance == this) Instance = null;
            Teardown();
        }

        private void Update()
        {
            if (!Application.isPlaying) return;
            var cam = Camera.main;
            Tick(Time.deltaTime, cam != null ? cam.transform : null);
        }

        /// <summary>Creates the grade volume and particles and loads the saved season. Safe to call repeatedly.</summary>
        public void EnsureInitialized()
        {
            if (initialized && gradeObject != null) return;
            initialized = true;
            Instance = this;
            if (dayNight == null) dayNight = GetComponent<AeroDayNightCycle>();
            if (wetness == null) wetness = GetComponent<AeroWetnessController>();
            if (clouds == null) clouds = GetComponent<AeroVolumetricClouds>();
            if (tower == null) tower = GetComponent<AeroTowerAtmosphere>();
            int saved = Store.GetInt(SeasonKey, (int)defaultSeason);
            season = fromSeason = Enum.IsDefined(typeof(AeroSeason), saved) ? (AeroSeason)saved : defaultSeason;
            seasonBlend = 1;
            if (gradeObject == null) BuildGrade();
            if (particles == null && particleShader != null) particles = new AeroAmbientParticles(transform, particleShader);
        }

        private void BuildGrade()
        {
            gradeObject = new GameObject("Aero atmosphere grade") { hideFlags = HideFlags.DontSave };
            gradeObject.transform.SetParent(transform, false);
            var volume = gradeObject.AddComponent<Volume>();
            volume.isGlobal = true; volume.weight = 1;
            volume.priority = (baseVolume != null ? baseVolume.priority : 0) + 1;
            gradeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            gradeProfile.name = "Aero atmosphere grade (runtime)"; gradeProfile.hideFlags = HideFlags.DontSave;
            color = gradeProfile.Add<ColorAdjustments>(true);
            white = gradeProfile.Add<WhiteBalance>(true);
            bloom = gradeProfile.Add<Bloom>(false);
            bloom.threshold.overrideState = bloom.intensity.overrideState = bloom.tint.overrideState = true;
            vignette = gradeProfile.Add<Vignette>(false);
            vignette.intensity.overrideState = true;
            split = gradeProfile.Add<SplitToning>(true);
            // Hue shift stays with the base profile.
            color.hueShift.overrideState = false;
            volume.sharedProfile = gradeProfile;
        }

        private void Teardown()
        {
            if (dayNight != null) dayNight.grade = AeroLightGrade.Identity;
            if (clouds != null && cloudyClouds != null && clouds.previewPreset == cloudyClouds) clouds.previewPreset = null;
            particles?.Destroy(); particles = null;
            if (gradeObject != null) Release(gradeObject);
            if (gradeProfile != null) Release(gradeProfile);
            gradeObject = null; gradeProfile = null; initialized = false; weightsPrimed = false;
        }

        private static void Release(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>Picks the season; persisted locally. Crossfades unless <paramref name="instant"/>.</summary>
        public void SetSeason(AeroSeason value, bool persist = true, bool instant = false)
        {
            EnsureInitialized();
            if (value != season || instant)
            {
                fromSeason = instant ? value : season;
                season = value;
                seasonBlend = instant || seasonBlendSeconds <= 0 || fromSeason == season ? 1 : 0;
            }
            if (persist) Store.SetInt(SeasonKey, (int)value);
        }

        public AeroSeason CycleSeason(int step = 1)
        {
            var next = (AeroSeason)((((int)season + step) % AeroAtmospherePresetLibrary.SeasonCount + AeroAtmospherePresetLibrary.SeasonCount) % AeroAtmospherePresetLibrary.SeasonCount);
            SetSeason(next);
            return next;
        }

        /// <summary>
        /// Forces a weather look (null returns to the live weather). Cloudy also shows the cloudy cloud preset on this
        /// screen; rain itself is synced, so the caller requests it from the weather service.
        /// </summary>
        public void SetWeatherOverride(AeroWeatherKind? value, bool instant = false)
        {
            EnsureInitialized();
            weatherOverride = value;
            if (clouds != null && cloudyClouds != null)
            {
                if (value == AeroWeatherKind.Cloudy) clouds.previewPreset = cloudyClouds;
                else if (clouds.previewPreset == cloudyClouds) clouds.previewPreset = null;
                clouds.Tick(0);
            }
            if (instant) { TargetWeights(weights); weightsPrimed = true; }
        }

        /// <summary>Clear / cloudy / rain weights the look is heading to (forced, or from rain amount and cloud cover).</summary>
        public void TargetWeights(float[] target)
        {
            Array.Clear(target, 0, target.Length);
            if (weatherOverride.HasValue) { target[(int)weatherOverride.Value] = 1; return; }
            float rain = wetness != null ? Mathf.Clamp01(wetness.RainAmount) : 0;
            float coverage = clouds != null && clouds.isActiveAndEnabled ? clouds.Look.coverage : 0;
            float cloudy = (1 - rain) * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(cloudyCoverage.x, cloudyCoverage.y, coverage));
            target[(int)AeroWeatherKind.Rain] = rain;
            target[(int)AeroWeatherKind.Cloudy] = cloudy;
            target[(int)AeroWeatherKind.Clear] = Mathf.Max(0, 1 - rain - cloudy);
        }

        /// <summary>Advances blends and applies the look. Public for tests and the review window.</summary>
        public void Tick(float dt, Transform view = null)
        {
            if (library == null) return;
            using var _ = TickMarker.Auto();
            EnsureInitialized();
            dt = Mathf.Max(0, dt);
            var target = targetWeights;
            TargetWeights(target);
            float step = !weightsPrimed || weatherBlendSeconds <= 0 ? 1 : dt / weatherBlendSeconds;
            for (int i = 0; i < weights.Length; i++) weights[i] = Mathf.MoveTowards(weights[i], target[i], step);
            weightsPrimed = true;
            if (seasonBlend < 1) seasonBlend = seasonBlendSeconds <= 0 ? 1 : Mathf.MoveTowards(seasonBlend, 1, dt / seasonBlendSeconds);
            if (seasonBlend >= 1) fromSeason = season;

            library.Compose(TimeOfDay, weights, fromSeason, season, seasonBlend, ref current);
            var frame = current;
            ApplyVolume(frame.look);
            if (dayNight != null)
            {
                dayNight.grade = AeroLightGrade.From(frame.look);
                // In Play Mode the cycle applies it in its own Update, which runs after this one.
                if (!Application.isPlaying && dayNight.profile != null && dayNight.sun != null) dayNight.ApplyTime(dayNight.timeOfDay);
            }
            if (particles != null)
            {
                Vector3 position = view != null ? view.position : transform.position, forward = view != null ? view.forward : Vector3.forward;
                bool sheltered = tower != null && tower.IsUnderRoof(position);
                particles.Apply(frame.particles, position, forward, sheltered, particlesEnabled && Application.isPlaying);
            }
        }

        private void ApplyVolume(in AeroAtmosphereLook look)
        {
            if (gradeProfile == null) return;
            color.postExposure.Override(look.exposure); color.contrast.Override(look.contrast);
            color.saturation.Override(look.saturation); color.colorFilter.Override(look.colorFilter);
            white.temperature.Override(look.temperature); white.tint.Override(look.tint);
            bloom.intensity.Override(look.bloomIntensity); bloom.threshold.Override(look.bloomThreshold); bloom.tint.Override(look.bloomTint);
            vignette.intensity.Override(look.vignette);
            split.shadows.Override(look.splitShadows); split.highlights.Override(look.splitHighlights); split.balance.Override(look.splitBalance);
        }

        /// <summary>Multi-line summary of a look for the graphics window.</summary>
        public static string Summarize(in AeroAtmospherePresetLibrary.Frame frame)
        {
            var l = frame.look; var c = CultureInfo.InvariantCulture;
            string warmth = l.Warmth > 6 ? "warm" : l.Warmth < -10 ? "cool" : "neutral";
            string particles = "none";
            if (frame.particles != null)
            {
                float best = 0;
                foreach (var p in frame.particles) if (p.density > best) { best = p.density; particles = AeroAtmosphereNames.Of(p.kind) + (p.Glows ? " (glow)" : "") + " " + p.density.ToString("0.00", c); }
            }
            return string.Format(c,
                "Grade  exp {0:+0.00;-0.00}  con {1:0}  sat {2:0}  temp {3:0}  tint {4:0}  ({5})\n" +
                "Bloom {6:0.00}  vignette {7:0.00}  sun x{8:0.00}  ambient x{9:0.00}  shadows {10:0.00}\n" +
                "Fog x{11:0.00} #{12}   Particles: {13}",
                l.exposure, l.contrast, l.saturation, l.temperature, l.tint, warmth,
                l.bloomIntensity, l.vignette, l.sunScale, l.ambientScale, l.shadowStrength,
                l.fogDensityScale, ColorUtility.ToHtmlStringRGB(l.fogColor), particles);
        }
    }
}
