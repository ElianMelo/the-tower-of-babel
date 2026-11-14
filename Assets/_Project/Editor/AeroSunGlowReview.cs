#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Renders the soft sun from the Tower scene, the camera turned towards the sun (or moon) at each time of day:
/// rows are clear, cloudy and rain, columns times of day. Writes a contact sheet under
/// _Screenshots-and-previews-to-delete/SunGlow. Edit Mode only; the scene is restored afterwards.
/// </summary>
public static class AeroSunGlowReview
{
    public const string OutputFolder = "_Screenshots-and-previews-to-delete/SunGlow";
    public static readonly float[] DefaultTimes = { .32f, .5f, .6875f, .74f, .02f };
    const string ClearClouds = "Assets/_Project/Environment/Clouds/Presets/Clear Frutiger Dream.asset";
    const string RainClouds = "Assets/_Project/Environment/Clouds/Presets/Rain Grey Drizzle Overcast.asset";

    sealed class NoStore : IAeroSettingsStore
    {
        public int GetInt(string key, int fallback) => fallback;
        public void SetInt(string key, int value) { }
    }

    [MenuItem("Tower of Babel/Rendering/Capture soft sun review")]
    public static string CaptureDefault() => Capture("SunGlow", DefaultTimes, null);

    /// <param name="times">Times of day (0 midnight, .5 noon) as columns.</param>
    /// <param name="weathers">Rows; null for clear, cloudy and rain.</param>
    /// <param name="facingSun">True turns the camera towards the light, false away from it.</param>
    /// <param name="sunBelowCentre">Degrees the camera looks below the light, so the ground shows under it.</param>
    /// <param name="yawFromSun">Degrees the camera turns away from the light sideways, to see the sun towards the screen's edge.</param>
    public static string Capture(string sheetName, float[] times, AeroWeatherKind[] weathers, AeroSeason season = AeroSeason.Summer,
        Vector3? position = null, bool facingSun = true, float sunBelowCentre = 9, float maxPitch = 35, int width = 480, int height = 270,
        int settleFrames = 12, float yawFromSun = 0)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture in Edit Mode.");
        var director = Object.FindFirstObjectByType<AeroAtmosphereDirector>();
        if (director == null || director.library == null || director.dayNight == null) throw new InvalidOperationException("Open the Tower scene with the atmosphere director.");
        weathers ??= (AeroWeatherKind[])Enum.GetValues(typeof(AeroWeatherKind));
        var cycle = director.dayNight; var clouds = director.clouds; var wetness = director.wetness;
        var rain = cycle.GetComponent<AeroRainController>();
        var oldPreview = clouds != null ? clouds.previewPreset : null; float oldTime = cycle.timeOfDay; var oldWet = wetness != null ? wetness.WetnessOverride : null;
        var oldStore = director.Store;
        var clearClouds = AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(ClearClouds);
        var rainClouds = AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(RainClouds);

        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var cameraObject = new GameObject("Aero sun glow review camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.targetTexture = target; camera.fieldOfView = 70; camera.farClipPlane = 65536;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        var sheet = new Texture2D(width * times.Length, height * weathers.Length, TextureFormat.RGB24, false);
        var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        bool asyncCompile = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var legend = new StringBuilder($"Columns (time of day): {string.Join(", ", times)}\nCamera {(facingSun ? "facing" : "turned away from")} the light, season {season}\n\n");
        try
        {
            director.Store = new NoStore();
            for (int row = 0; row < weathers.Length; row++)
            {
                var weather = weathers[row];
                legend.Append($"Row {row + 1}: {weather}\n");
                for (int column = 0; column < times.Length; column++)
                {
                    director.SetSeason(season, false, true);
                    director.SetWeatherOverride(weather, true);
                    if (clouds != null && weather != AeroWeatherKind.Cloudy) clouds.previewPreset = weather == AeroWeatherKind.Rain ? rainClouds : clearClouds;
                    float overcast = weather == AeroWeatherKind.Rain && rain != null && rain.profile != null ? rain.profile.overcast : 0;
                    if (rain != null && rain.profile != null)
                    { cycle.overcastSunScale = rain.profile.sunScale; cycle.overcastSkyScale = rain.profile.skyScale; cycle.overcastFogScale = rain.profile.fogScale; }
                    cycle.overcast = overcast;
                    if (wetness != null) wetness.OverrideWetness(weather == AeroWeatherKind.Rain ? 1 : 0);
                    cycle.SetTime(times[column]);
                    director.Tick(0);
                    clouds?.Tick(0);
                    director.Tick(0);

                    Vector3 toward = RenderSettings.sun != null ? -RenderSettings.sun.transform.forward : Vector3.forward;
                    Vector3 flat = new Vector3(toward.x, 0, toward.z);
                    if (flat.sqrMagnitude < 1e-6f) flat = Vector3.forward;
                    if (!facingSun) flat = -flat;
                    float elevation = Mathf.Asin(Mathf.Clamp(toward.y, -1, 1)) * Mathf.Rad2Deg;
                    float pitch = facingSun ? Mathf.Clamp(elevation - sunBelowCentre, -5, maxPitch) : 4;
                    var rotation = Quaternion.LookRotation(Quaternion.AngleAxis(yawFromSun, Vector3.up) * flat.normalized) * Quaternion.Euler(-pitch, 0, 0);
                    cameraObject.transform.SetPositionAndRotation(position ?? new Vector3(-260, 45, -330), rotation);

                    for (int i = 0; i < settleFrames; i++) camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, width, height), 0, 0); frame.Apply();
                    sheet.SetPixels(column * width, (weathers.Length - 1 - row) * height, width, height, frame.GetPixels());
                    var glow = cycle.SunGlow;
                    legend.Append($"  t={times[column]:0.###}: light elevation {elevation:0.#}°, low sun {glow.lowSun:0.00}, " +
                                  $"core/glow/halo {glow.strength.x:0.##}/{glow.strength.y:0.##}/{glow.strength.z:0.##}, halo radius {glow.shape.w:0.#}°, " +
                                  $"haze {glow.haze.y * glow.haze.w:0.00}, sky tint {glow.spread.z:0.00}, rays {glow.rays.x:0.00}, bloom scatter {director.BloomScatter:0.00}\n");
                }
            }
            sheet.Apply();
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, sheetName + ".png").Replace('\\', '/');
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Path.Combine(OutputFolder, sheetName + ".txt"), legend.ToString());
            return path;
        }
        finally
        {
            RenderTexture.active = active;
            ShaderUtil.allowAsyncCompilation = asyncCompile;
            director.Shutdown(); director.Store = oldStore;
            if (clouds != null) { clouds.previewPreset = oldPreview; clouds.Tick(0); }
            if (wetness != null) wetness.OverrideWetness(oldWet);
            cycle.overcast = 0; cycle.grade = AeroLightGrade.Identity; cycle.SetTime(oldTime);
            Object.DestroyImmediate(cameraObject); target.Release(); Object.DestroyImmediate(target);
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(frame);
        }
    }
}
#endif
