using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroDayNightCycle : MonoBehaviour
    {
        public AeroDayNightProfile profile;
        public Light sun;
        public Light moon;
        [Range(0, 1)] public float timeOfDay = .29f;
        [Tooltip("Daytime haze colour when no atmosphere preset is applied (see AeroAtmosphereDirector).")]
        public Color dayFogColor = new(.62f, .76f, .86f);
        public float Daylight => AeroDayNightProfile.DaylightAt(timeOfDay);
        /// <summary>Runtime freeze (debug menu / synced weather); the profile's advanceTime still applies.</summary>
        [System.NonSerialized] public bool paused;
        /// <summary>Rain clouds, 0 clear .. 1 full overcast. Set by AeroRainController.</summary>
        [System.NonSerialized] public float overcast;
        [System.NonSerialized] public float overcastSunScale = .45f, overcastSkyScale = .78f, overcastFogScale = 2.2f;
        /// <summary>Sunlight blocked by the volumetric clouds, 0..1. Set by AeroVolumetricClouds; dims only the sun.</summary>
        [System.NonSerialized] public float cloudShade;
        /// <summary>Atmosphere preset lighting (AeroAtmosphereDirector): sun and ambient tint, shadows and fog. Identity when none.</summary>
        [System.NonSerialized] public AeroLightGrade grade = AeroLightGrade.Identity;
        public float CycleSeconds => profile != null ? Mathf.Max(60, profile.cycleSeconds) : 1200;
        public bool Advancing => profile != null && profile.advanceTime && !paused;
        private Material originalSky, ownedSky;
        private bool initialized, timeAssigned;
        private float baseShadowStrength = -1;

        /// <summary>Sets the clock from an authority (network or debug); keeps it over the profile start time.</summary>
        public void SetTime(float normalizedTime)
        {
            timeOfDay = Mathf.Repeat(normalizedTime, 1); timeAssigned = true;
            if (profile != null && sun != null) ApplyTime(timeOfDay);
        }

        private void OnEnable() { initialized = false; AeroSunGlow.TrackCameras(true); }
        private void Update()
        {
            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
            if (profile == null || sun == null) return;
            if (!initialized)
            {
                if (Application.isPlaying && !timeAssigned) timeOfDay = profile.startTime;
                originalSky = RenderSettings.skybox;
                if (originalSky != null && Application.isPlaying)
                {
                    ownedSky = new Material(originalSky) { hideFlags = HideFlags.HideAndDontSave };
                    RenderSettings.skybox = ownedSky;
                }
                initialized = true;
            }
            if (Application.isPlaying && Advancing) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / CycleSeconds, 1);
            ApplyTime(timeOfDay);
        }

        public void ApplyTime(float normalizedTime)
        {
            if (profile == null || sun == null) return;
            timeOfDay = Mathf.Repeat(normalizedTime, 1);
            float day = Daylight;
            float cloud = Mathf.Clamp01(overcast);
            float elevation = Mathf.Sin((timeOfDay - .25f) * Mathf.PI * 2);
            sun.transform.rotation = Quaternion.Euler(timeOfDay * 360 - 90, profile.sunAzimuth, 0);
            sun.color = Color.Lerp(profile.sunsetSun, profile.daySun, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.02f, .38f, elevation))) * (grade.active ? grade.sunTint : Color.white);
            sun.intensity = profile.sunIntensity * day * Mathf.Lerp(1, overcastSunScale, cloud) * (1 - Mathf.Clamp01(cloudShade)) * (grade.active ? grade.sunScale : 1);
            if (grade.active)
            {
                if (baseShadowStrength < 0) baseShadowStrength = sun.shadowStrength;
                sun.shadowStrength = Mathf.Clamp01(grade.shadowStrength);
            }
            else if (baseShadowStrength >= 0) { sun.shadowStrength = baseShadowStrength; baseShadowStrength = -1; }
            sun.enabled = day > .001f;
            var flare = sun.GetComponent<LensFlareComponentSRP>();
            // The flare is drawn over the clouds, so it fades when they or rain clouds cover the sun.
            if (flare != null) flare.intensity = profile.sunFlareIntensity * day * (1 - Mathf.Clamp01(cloudShade)) * (1 - cloud);
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(timeOfDay * 360 + 90, profile.sunAzimuth + 25, 0);
                moon.color = profile.moonColor; moon.intensity = profile.moonIntensity * (1 - day); moon.enabled = day < .999f;
            }
            RenderSettings.sun = day > .1f || moon == null ? sun : moon;
            PushSunGlow(day, cloud);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color sky = Color.Lerp(profile.nightSky, profile.daySky, day);
            if (grade.active) sky *= grade.ambientTint * grade.ambientScale;
            if (cloud > 0) { float grey = sky.grayscale; sky = Color.Lerp(sky, new Color(grey, grey, grey * 1.04f), cloud * .35f) * Mathf.Lerp(1, overcastSkyScale, cloud); }
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky * .62f;
            RenderSettings.ambientGroundColor = sky * .4f;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(sky * .45f);
            ambient.AddDirectionalLight(Vector3.up, sky, .35f);
            ambient.AddDirectionalLight(Vector3.down, sky * .4f, .2f);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = Mathf.Lerp(.12f, 1, day);
            Color fog = grade.active ? grade.fogColor : Color.Lerp(profile.nightSky, dayFogColor, day);
            if (cloud > 0) { float grey = fog.grayscale; fog = Color.Lerp(fog, new Color(grey, grey, grey * 1.03f), cloud * .45f) * Mathf.Lerp(1, overcastSkyScale, cloud); }
            RenderSettings.fogColor = fog;
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .0006f * Mathf.Lerp(1, overcastFogScale, cloud) * (grade.active ? grade.fogDensityScale : 1);
            if (ownedSky != null && ownedSky.HasProperty("_Exposure")) ownedSky.SetFloat("_Exposure", Mathf.Lerp(profile.nightExposure, profile.dayExposure, day) * Mathf.Lerp(1, overcastSkyScale, cloud) * (grade.active ? grade.skyExposureScale : 1));
            Shader.SetGlobalFloat("_AeroDaylight", day);
        }

        /// <summary>
        /// The soft sun glow (sky, distant haze, cloud haze) follows the sun by day and the moon by night. The sun's glow
        /// fades out as daylight drops to the moon switch, and the moon's fades in after it, so nothing pops.
        /// </summary>
        private void PushSunGlow(float day, float cloud)
        {
            bool moonLit = RenderSettings.sun == moon && moon != null;
            Light source = moonLit ? moon : sun;
            float visibility = moonLit ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, 0, day)) : day * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.1f, .2f, day));
            var settings = moonLit ? profile.moonGlow : profile.sunGlow;
            float haze = grade.active ? grade.fogDensityScale : 1;
            Vector3 toward = -source.transform.forward;
            SunGlowEdge = AeroSunGlow.Evaluate(settings, toward, source.color, visibility, cloud, haze, cloudShade, 0);
            SunGlow = AeroSunGlow.Evaluate(settings, toward, source.color, visibility, cloud, haze, cloudShade, 1);
            AeroSunGlow.Push(SunGlowEdge, SunGlow);
        }

        /// <summary>The glow from the last <see cref="ApplyTime"/>, looked at straight on.</summary>
        public AeroSunGlowState SunGlow { get; private set; }
        /// <summary>The same with the sun at the edge of the screen: small and calm.</summary>
        public AeroSunGlowState SunGlowEdge { get; private set; }

        /// <summary>Bloom scatter for a time of day: wider when the sun (or moon) is low.</summary>
        public float BloomScatterAt(float normalizedTime)
        {
            if (profile == null) return .68f;
            float day = AeroDayNightProfile.DaylightAt(normalizedTime);
            bool moonLit = day <= .1f && moon != null;
            float elevation = Mathf.Sin((Mathf.Repeat(normalizedTime, 1) - .25f) * Mathf.PI * 2) * (moonLit ? -1 : 1);
            var settings = moonLit ? profile.moonGlow : profile.sunGlow;
            return AeroSunGlow.BloomScatter(settings, AeroSunGlow.LowSun(settings, elevation));
        }

        private void OnDisable()
        {
            AeroSunGlow.TrackCameras(false);
            if (ownedSky != null)
            {
                if (RenderSettings.skybox == ownedSky) RenderSettings.skybox = originalSky;
                if (Application.isPlaying) Destroy(ownedSky); else DestroyImmediate(ownedSky);
                ownedSky = null;
            }
            initialized = false;
        }
    }
}
