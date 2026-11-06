#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// Renders every cloud preset (or chosen ones) at several times of day into one contact sheet under
/// _Screenshots-and-previews-to-delete/Clouds. Edit Mode only; the scene is left as it was.
/// </summary>
public static class AeroCloudReview
{
    public const string OutputFolder = "_Screenshots-and-previews-to-delete/Clouds";

    [MenuItem("Tower of Babel/Environment/Clouds/Capture preset contact sheet")]
    public static string CaptureAll() => Capture(null, new[] { .32f, .5f, .74f, .02f }, "AllPresets");

    /// <param name="presetNames">Preset asset names, or null for every preset.</param>
    /// <param name="times">Times of day (0 midnight, .5 noon) as columns.</param>
    public static string Capture(string[] presetNames, float[] times, string sheetName, int width = 480, int height = 270,
        Vector3? position = null, Vector3? euler = null, int settleFrames = 16)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Capture in Edit Mode.");
        var clouds = Object.FindFirstObjectByType<AeroVolumetricClouds>();
        if (clouds == null || clouds.dayNight == null) throw new InvalidOperationException("Open the Tower scene with authored clouds.");
        var presets = AssetDatabase.FindAssets("t:AeroCloudPreset", new[] { AeroCloudAuthoring.PresetFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p => presetNames == null || presetNames.Contains(p.name)).OrderBy(p => p.weather).ThenBy(p => p.name).ToArray();

        var cycle = clouds.dayNight;
        var oldPreview = clouds.previewPreset; float oldTime = cycle.timeOfDay;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
        var cameraObject = new GameObject("Aero cloud review camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.targetTexture = target; camera.fieldOfView = 70; camera.farClipPlane = 65536;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
        cameraObject.transform.SetPositionAndRotation(position ?? new Vector3(-260, 45, -330), Quaternion.Euler(euler ?? new Vector3(-14, 38, 0)));
        var sheet = new Texture2D(width * times.Length, height * presets.Length, TextureFormat.RGB24, false);
        var frame = new Texture2D(width, height, TextureFormat.RGB24, false);
        var active = RenderTexture.active;
        // Compile shaders synchronously: with async compilation a capture right after a shader edit still renders
        // the previous variant (or nothing), so a review would judge stale code.
        bool asyncCompile = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try
        {
            for (int row = 0; row < presets.Length; row++)
            for (int column = 0; column < times.Length; column++)
            {
                clouds.previewPreset = presets[row];
                cycle.ApplyTime(times[column]);
                clouds.Tick(0);
                for (int i = 0; i < settleFrames; i++) camera.Render(); // settle the temporal history (new jitter each render)
                RenderTexture.active = target;
                frame.ReadPixels(new Rect(0, 0, width, height), 0, 0); frame.Apply();
                sheet.SetPixels(column * width, (presets.Length - 1 - row) * height, width, height, frame.GetPixels());
            }
            sheet.Apply();
            Directory.CreateDirectory(OutputFolder);
            string path = Path.Combine(OutputFolder, sheetName + ".png").Replace('\\', '/');
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            File.WriteAllText(Path.Combine(OutputFolder, sheetName + ".txt"),
                "Rows: " + string.Join(", ", presets.Select(p => p.name)) + "\nColumns (time of day): " + string.Join(", ", times));
            return path;
        }
        finally
        {
            RenderTexture.active = active;
            ShaderUtil.allowAsyncCompilation = asyncCompile;
            clouds.previewPreset = oldPreview; cycle.ApplyTime(oldTime); clouds.Tick(0);
            Object.DestroyImmediate(cameraObject); target.Release(); Object.DestroyImmediate(target);
            Object.DestroyImmediate(sheet); Object.DestroyImmediate(frame);
        }
    }
}
#endif
