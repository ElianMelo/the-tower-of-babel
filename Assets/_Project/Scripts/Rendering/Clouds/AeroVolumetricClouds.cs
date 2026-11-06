using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Drives the volumetric cloud layer: rolls randomized clear-day skies from the weather library, blends to a
    /// heavy rain preset as the falling rain fades in, and lights the clouds from the day/night cycle (sun by day,
    /// boosted moonlight by night, sky ambient, horizon haze). Visual only and local to each client; the rain itself
    /// is synced by NetworkWeatherService. Rendered by <see cref="AeroVolumetricCloudsRendererFeature"/>.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroVolumetricClouds : MonoBehaviour
    {
        private static readonly List<AeroVolumetricClouds> Sources = new();

        public AeroCloudWeatherLibrary library;
        public AeroDayNightCycle dayNight;
        [Tooltip("Rain amount source; clouds blend to the rain preset as rain fades in.")]
        public AeroWetnessController wetness;
        [Tooltip("Shows only this preset (ignores weather and randomization). For tuning presets live.")]
        public AeroCloudPreset previewPreset;
        [Tooltip("Random seed for clear-day rolls; 0 picks a new seed each session.")]
        public int seed;

        private System.Random random;
        private AeroCloudLook clearFrom, clearTo, rainLook;
        private float clearBlend = 1, rerollTimer, lastDaylight = -1;
        private bool initialized, rainActive;

        /// <summary>The blended look being rendered this frame.</summary>
        public AeroCloudLook Look { get; private set; } = AeroCloudLook.Default;
        public AeroCloudWeatherLibrary.Roll ClearRoll { get; private set; }
        public AeroCloudWeatherLibrary.Roll RainRoll { get; private set; }
        public float RainBlend { get; private set; }
        public float ClearTransition => clearBlend;
        public float SecondsToReroll => rerollTimer;
        /// <summary>Accumulated drift of the shapes (x, z metres) and the boiling offset (metres).</summary>
        public Vector3 WindOffset { get; private set; }
        /// <summary>Unit vector toward the light that lights the clouds (sun or moon).</summary>
        public Vector3 LightDirection { get; private set; } = Vector3.up;
        /// <summary>Linear light colour on the clouds, including intensity, tint and night boost.</summary>
        public Color LightColor { get; private set; }
        public Color AmbientTop { get; private set; }
        public Color AmbientBottom { get; private set; }
        public Color FogColor { get; private set; }
        public bool LitByMoon { get; private set; }
        public float PlanetRadius => (library != null ? library.planetRadiusKm : 1200) * 1000;
        public bool Visible => isActiveAndEnabled && Look.coverage > .001f && Look.density > .001f;
        public string Description => previewPreset != null ? $"Preview: {previewPreset.name}" :
            RainBlend > .5f ? $"Rain: {RainRoll.Description}" : ClearRoll.Description;

        /// <summary>The clouds for this camera's scene; preview cameras never pick up the gameplay sky.</summary>
        public static AeroVolumetricClouds ForCamera(Camera camera)
        {
            if (camera == null) return null;
            var scene = camera.scene.IsValid() ? camera.scene : camera.gameObject.scene;
            if (camera.cameraType == CameraType.SceneView) scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            for (int i = Sources.Count - 1; i >= 0; i--)
                if (Sources[i] != null && Sources[i].isActiveAndEnabled && Sources[i].gameObject.scene == scene) return Sources[i];
            if (!camera.scene.IsValid())
                for (int i = Sources.Count - 1; i >= 0; i--)
                    if (Sources[i] != null && Sources[i].isActiveAndEnabled &&
                        Sources[i].gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return Sources[i];
            return null;
        }

        private void OnEnable()
        {
            if (!Sources.Contains(this)) Sources.Add(this);
            if (dayNight == null) dayNight = GetComponent<AeroDayNightCycle>();
            if (wetness == null) wetness = GetComponent<AeroWetnessController>();
            initialized = false;
            Tick(0);
        }

        private void OnDisable()
        {
            Sources.Remove(this);
            if (dayNight != null) dayNight.cloudShade = 0;
        }

        private void Update() => Tick(Application.isPlaying ? Time.deltaTime : 0);

        /// <summary>Re-seeds and rolls a fresh clear sky immediately (debug menu, tests).</summary>
        public void Reseed(int newSeed)
        {
            seed = newSeed; initialized = false; Tick(0);
        }

        /// <summary>Blends to a newly rolled clear-day sky over the library's transition time.</summary>
        public void RerollClearSky(bool instant = false)
        {
            if (library == null) return;
            EnsureInitialized();
            var current = CurrentClear();
            ClearRoll = library.RollClearDay(random);
            clearFrom = current; clearTo = ClearRoll.look;
            clearBlend = instant || library.transitionSeconds <= 0 ? 1 : 0;
            rerollTimer = library.NextRerollSeconds(random);
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
            random = new System.Random(seed != 0 ? seed : System.Environment.TickCount);
            if (library == null) { clearFrom = clearTo = rainLook = AeroCloudLook.Default; return; }
            ClearRoll = library.RollClearDay(random);
            clearFrom = clearTo = ClearRoll.look; clearBlend = 1;
            RainRoll = library.RollRain(random); rainLook = RainRoll.look;
            rerollTimer = library.NextRerollSeconds(random);
            rainActive = false; lastDaylight = -1;
        }

        private AeroCloudLook CurrentClear() => AeroCloudLook.Lerp(clearFrom, clearTo, Mathf.SmoothStep(0, 1, clearBlend));

        /// <summary>Advances weather blending, wind and lighting. Public for tests.</summary>
        public void Tick(float dt)
        {
            EnsureInitialized();
            dt = Mathf.Max(0, dt);
            float daylight = dayNight != null ? dayNight.Daylight : 1;
            AeroCloudLook look;
            if (previewPreset != null)
            {
                look = previewPreset.look.Sanitized(); RainBlend = 0;
            }
            else if (library == null)
            {
                look = AeroCloudLook.Default; RainBlend = 0;
            }
            else
            {
                float rain = wetness != null ? wetness.RainAmount : 0;
                bool raining = rain > .001f;
                if (raining && !rainActive) { RainRoll = library.RollRain(random); rainLook = RainRoll.look; }
                // Advance the running blend first, so a sky rolled this tick starts from zero.
                if (clearBlend < 1) clearBlend = library.transitionSeconds <= 0 ? 1 : Mathf.MoveTowards(clearBlend, 1, dt / library.transitionSeconds);
                if (!raining && rainActive) RerollClearSky();
                rainActive = raining;
                if (!raining && dt > 0)
                {
                    rerollTimer -= dt;
                    if (rerollTimer <= 0) RerollClearSky();
                    else if (library.rerollAtDawn && lastDaylight >= 0 && lastDaylight < .5f && daylight >= .5f) RerollClearSky();
                }
                RainBlend = library.RainBlend(rain);
                look = AeroCloudLook.Lerp(CurrentClear(), rainLook, RainBlend);
            }
            lastDaylight = daylight;
            Look = look;

            var wind = look.WindVector;
            var offset = WindOffset + new Vector3(wind.x, wind.y, look.evolution) * dt;
            // Days of drift stay well inside float precision; past 1000 km the pattern resets once.
            if (Mathf.Abs(offset.x) > 1e6f || Mathf.Abs(offset.y) > 1e6f || Mathf.Abs(offset.z) > 1e6f) offset = Vector3.zero;
            WindOffset = offset;

            UpdateLighting(look, daylight);
            if (dayNight != null) dayNight.cloudShade = look.sunOcclusion * Mathf.Clamp01(look.coverage * 1.4f);
        }

        private void UpdateLighting(in AeroCloudLook look, float daylight)
        {
            Light sun = dayNight != null ? dayNight.sun : null, moon = dayNight != null ? dayNight.moon : null;
            var profile = dayNight != null ? dayNight.profile : null;
            Color sunColor = Color.black, moonColor = Color.black;
            if (sun != null)
            {
                // Clouds sit above the overcast that dims the ground, so they get the unscaled sun.
                float intensity = profile != null ? profile.sunIntensity * daylight : sun.intensity;
                sunColor = sun.color.linear * intensity;
            }
            if (moon != null)
            {
                float intensity = profile != null ? profile.moonIntensity * (1 - daylight) : moon.intensity;
                moonColor = moon.color.linear * intensity * (library != null ? library.moonLightBoost : 1);
            }
            if (sun == null && moon == null && RenderSettings.sun != null)
                sunColor = RenderSettings.sun.color.linear * RenderSettings.sun.intensity;
            LitByMoon = moon != null && moonColor.grayscale > sunColor.grayscale;
            Light source = LitByMoon ? moon : sun != null ? sun : RenderSettings.sun;
            LightDirection = source != null ? -source.transform.forward : Vector3.up;
            Color light = LitByMoon ? moonColor : sunColor;
            LightColor = light * look.litTint.linear * look.sunLight;

            float nightBoost = Mathf.Lerp(library != null ? library.nightAmbientBoost : 1, 1, daylight);
            Color tint = look.shadowTint.linear * look.ambientLight * nightBoost * (1 - look.darkness * .55f);
            AmbientTop = RenderSettings.ambientSkyColor.linear * tint;
            AmbientBottom = RenderSettings.ambientEquatorColor.linear * tint * (1 - look.darkness * .35f);
            FogColor = RenderSettings.fogColor.linear;
        }
    }
}
