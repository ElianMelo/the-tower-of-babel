#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Renders all 60 atmosphere presets (rows: season x weather, columns: time of day) from the Tower scene into one
/// contact sheet under _Screenshots-and-previews-to-delete/AtmospherePresets. Edit Mode only; the scene is restored.
/// Floating particles only run in Play Mode, so they are not in the sheet; the summary text lists them.
/// </summary>
public static class AeroAtmospherePresetReview
{
    public const string OutputFolder = "_Screenshots-and-previews-to-delete/AtmospherePresets";
    const string ClearClouds = "Assets/_Project/Environment/Clouds/Presets/Clear Frutiger Dream.asset";
    const string RainClouds = "Assets/_Project/Environment/Clouds/Presets/Rain Grey Drizzle Overcast.asset";

    sealed class NoStore : IAeroSettingsStore
    {
        public int GetInt(string key, int fallback) => fallback;
        public void SetInt(string key, int value) { }
    }

    [MenuItem("Tower of Babel/Rendering/Capture atmosphere preset contact sheet")]
    public static string CaptureAll() => Capture("AllPresets");

    public static string Capture(string sheetName, int width = 320, int height = 180, Vector3? position = null, Vector3? euler = null,
        AeroSeason[] seasons = null, int settleFrames = 12)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture in Edit Mode.");
        var director = Object.FindFirstObjectByType<AeroAtmosphereDirector>();
        if (director == null || director.library == null || director.dayNight == null) throw new InvalidOperationException("Open the Tower scene with the atmosphere director.");
        seasons ??= (AeroSeason[])Enum.GetValues(typeof(AeroSeason));
        var weathers = (AeroWeatherKind[])Enum.GetValues(typeof(AeroWeatherKind));
        var times = (AeroTimeSlot[])Enum.GetValues(typeof(AeroTimeSlot));
        var cycle = director.dayNight; var clouds = director.clouds; var wetness = director.wetness;
        var rain = cycle.GetComponent<AeroRainController>();
        var oldPreview = clouds != null ? clouds.previewPreset : null; float oldTime = cycle.timeOfDay; var oldWet = wetness != null ? wetness.WetnessOverride : null;
        var oldStore = director.Store;
        var clearClouds = AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(ClearClouds);
        var rainClouds = AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(RainClouds);

        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var cameraObject = new GameObject("Aero atmosphere review camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.targetTexture = target; camera.fieldOfView = 70; camera.farClipPlane = 65536;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        cameraObject.transform.SetPositionAndRotation(position ?? new Vector3(-260, 45, -330), Quaternion.Euler(euler ?? new Vector3(-14, 38, 0)));
        int rows = seasons.Length * weathers.Length;
        var sheet = new Texture2D(width * times.Length, height * rows, TextureFormat.RGB24, false);
        var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        bool asyncCompile = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var legend = new StringBuilder("Columns: " + string.Join(", ", times.Select(AeroAtmosphereNames.Of)) + "\n\n");
        try
        {
            director.Store = new NoStore();
            int row = 0;
            foreach (var season in seasons)
            foreach (var weather in weathers)
            {
                legend.Append($"Row {row + 1}: {AeroAtmosphereNames.Of(season)} · {weather}\n");
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
                    cycle.SetTime(director.library.TimeOf(times[column]));
                    director.Tick(0);
                    clouds?.Tick(0);
                    director.Tick(0);
                    for (int i = 0; i < settleFrames; i++) camera.Render();
                    RenderTexture.active = target;
                    frame.ReadPixels(new Rect(0, 0, width, height), 0, 0); frame.Apply();
                    sheet.SetPixels(column * width, (rows - 1 - row) * height, width, height, frame.GetPixels());
                    legend.Append($"  {AeroAtmosphereNames.Of(times[column])}: {AeroAtmosphereDirector.Summarize(director.Current).Replace('\n', ' ')}\n");
                }
                row++;
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
