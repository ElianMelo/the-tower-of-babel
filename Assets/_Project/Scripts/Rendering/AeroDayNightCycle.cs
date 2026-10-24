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
        public float Daylight => AeroDayNightProfile.DaylightAt(timeOfDay);
        private Material originalSky, ownedSky;
        private bool initialized;

        private void OnEnable() { initialized = false; }
        private void Update()
        {
            if (gameObject.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene()) return;
            if (profile == null || sun == null) return;
            if (!initialized)
            {
                if (Application.isPlaying) timeOfDay = profile.startTime;
                originalSky = RenderSettings.skybox;
                if (originalSky != null && Application.isPlaying)
                {
                    ownedSky = new Material(originalSky) { hideFlags = HideFlags.HideAndDontSave };
                    RenderSettings.skybox = ownedSky;
                }
                initialized = true;
            }
            if (Application.isPlaying && profile.advanceTime) timeOfDay = Mathf.Repeat(timeOfDay + Time.deltaTime / Mathf.Max(60, profile.cycleSeconds), 1);
            ApplyTime(timeOfDay);
        }

        public void ApplyTime(float normalizedTime)
        {
            if (profile == null || sun == null) return;
            timeOfDay = Mathf.Repeat(normalizedTime, 1);
            float day = Daylight;
            float elevation = Mathf.Sin((timeOfDay - .25f) * Mathf.PI * 2);
            sun.transform.rotation = Quaternion.Euler(timeOfDay * 360 - 90, profile.sunAzimuth, 0);
            sun.color = Color.Lerp(profile.sunsetSun, profile.daySun, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.02f, .38f, elevation)));
            sun.intensity = profile.sunIntensity * day;
            sun.enabled = day > .001f;
            var flare = sun.GetComponent<LensFlareComponentSRP>();
            if (flare != null) flare.intensity = profile.sunFlareIntensity * day;
            if (moon != null)
            {
                moon.transform.rotation = Quaternion.Euler(timeOfDay * 360 + 90, profile.sunAzimuth + 25, 0);
                moon.color = profile.moonColor; moon.intensity = profile.moonIntensity * (1 - day); moon.enabled = day < .999f;
            }
            RenderSettings.sun = day > .1f || moon == null ? sun : moon;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            Color sky = Color.Lerp(profile.nightSky, profile.daySky, day);
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky * .62f;
            RenderSettings.ambientGroundColor = sky * .4f;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(sky * .45f);
            ambient.AddDirectionalLight(Vector3.up, sky, .35f);
            ambient.AddDirectionalLight(Vector3.down, sky * .4f, .2f);
            RenderSettings.ambientProbe = ambient;
            RenderSettings.reflectionIntensity = Mathf.Lerp(.12f, 1, day);
            RenderSettings.fogColor = Color.Lerp(profile.nightSky, new Color(.62f,.76f,.86f), day);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .0006f;
            if (ownedSky != null && ownedSky.HasProperty("_Exposure")) ownedSky.SetFloat("_Exposure", Mathf.Lerp(profile.nightExposure, profile.dayExposure, day));
            Shader.SetGlobalFloat("_AeroDaylight", day);
        }

        private void OnDisable()
        {
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
