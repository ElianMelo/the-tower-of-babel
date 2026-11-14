using System;
using UnityEngine;

namespace TowerOfBabel.Settings
{
    public enum GraphicsQuality { Low = 0, Medium = 1, High = 2, Custom = 3 }
    public enum ShadowQuality { Off = 0, Low = 1, Medium = 2, High = 3 }
    public enum DisplayMode { Fullscreen = 0, Borderless = 1, Windowed = 2 }

    /// <summary>Everything the settings menu edits. Saved as JSON; unknown fields keep their defaults.</summary>
    [Serializable]
    public sealed class GameSettingsData
    {
        public const int CurrentVersion = 1;
        public int version = CurrentVersion;

        // Video. Zero size: keep the resolution the game started with.
        public int resolutionWidth, resolutionHeight;
        public DisplayMode displayMode = DisplayMode.Borderless;

        // Graphics. graphicsInitialized stays false until the first preset is filled in from the scene's own values.
        public bool graphicsInitialized;
        public GraphicsQuality quality = GraphicsQuality.High;
        public ShadowQuality shadowQuality = ShadowQuality.High;
        public float shadowDistance = 120;
        public float terrainPixelError = 8;
        public float baseMapDistance = 1000;
        public float grassDistance = 95;
        public float grassDensity = .55f;
        public float cameraFar = 65536;
        public float cameraNear = .3f;
        public float cameraFov = 60;
        public float renderScale = 1;
        // Introduced with the far tower's GPU culling and the render feature tiers (performance pass 2026-11-12).
        public float towerDetailDistance = 170;
        public float towerLodScale = 1;
        public bool occlusionCulling = true;
        public bool liveReflections = true;
        public int sunShafts = 2;
        public int clouds = 2;
        public bool riverRefraction = true;
        public int lampLights = 96;

        // Audio, 0 to 1.
        public float masterVolume = 1, musicVolume = 1, uiVolume = 1, vfxVolume = 1;

        // Gameplay.
        public float lookSensitivity = 1;
        public bool invertX, invertY;

        // Controls: Input System binding overrides as JSON.
        public string bindingOverrides = "";

        public GameSettingsData Clone() => JsonUtility.FromJson<GameSettingsData>(JsonUtility.ToJson(this));
    }

    /// <summary>The scene's own graphics values: what the High preset keeps.</summary>
    [Serializable]
    public struct GraphicsBaseline
    {
        public float shadowDistance, terrainPixelError, baseMapDistance, grassDistance, grassDensity, cameraFar, cameraNear, cameraFov;
        public int lampLights;

        /// <summary>The Tower scene's values, used when no scene has been measured.</summary>
        public static GraphicsBaseline Default => new()
        {
            shadowDistance = 120, terrainPixelError = 8, baseMapDistance = 1000, grassDistance = 95, grassDensity = .55f,
            cameraFar = 65536, cameraNear = .3f, cameraFov = 60, lampLights = 96
        };
    }

    /// <summary>
    /// The three graphics presets. High makes no compromise: it is the scene exactly as authored, and far tower detail
    /// (vines, lava lamps) reaches about as far as it did when whole pages drew within 140 m. Medium trades what is
    /// hard to notice in motion. Low goes for the highest frame rate and accepts a clearly simpler picture.
    /// </summary>
    public static class GraphicsPresets
    {
        public const float HighDetailDistance = 170, MediumDetailDistance = 120, LowDetailDistance = 60;

        public static void Apply(GameSettingsData data, GraphicsQuality quality, GraphicsBaseline scene)
        {
            if (quality == GraphicsQuality.Custom) return;
            data.quality = quality;
            data.graphicsInitialized = true;
            // The field of view and the near plane are a matter of taste and comfort, not of quality: presets keep them.
            switch (quality)
            {
                case GraphicsQuality.High:
                    data.shadowQuality = ShadowQuality.High; data.shadowDistance = scene.shadowDistance;
                    data.terrainPixelError = scene.terrainPixelError; data.baseMapDistance = scene.baseMapDistance;
                    data.grassDistance = scene.grassDistance; data.grassDensity = scene.grassDensity;
                    data.cameraFar = scene.cameraFar; data.renderScale = 1;
                    data.towerDetailDistance = HighDetailDistance; data.towerLodScale = 1; data.occlusionCulling = true;
                    data.liveReflections = true; data.sunShafts = 2; data.clouds = 2; data.riverRefraction = true;
                    data.lampLights = scene.lampLights;
                    break;
                case GraphicsQuality.Medium:
                    data.shadowQuality = ShadowQuality.Medium; data.shadowDistance = Mathf.Min(scene.shadowDistance, 80);
                    data.terrainPixelError = Mathf.Max(scene.terrainPixelError, 14); data.baseMapDistance = Mathf.Min(scene.baseMapDistance, 400);
                    data.grassDistance = Mathf.Min(scene.grassDistance, 65); data.grassDensity = scene.grassDensity * .7f;
                    data.cameraFar = Mathf.Min(scene.cameraFar, 6000); data.renderScale = 1;
                    data.towerDetailDistance = MediumDetailDistance; data.towerLodScale = .7f; data.occlusionCulling = true;
                    data.liveReflections = true; data.sunShafts = 1; data.clouds = 1; data.riverRefraction = true;
                    data.lampLights = Mathf.Min(scene.lampLights, 40);
                    break;
                default:
                    data.shadowQuality = ShadowQuality.Low; data.shadowDistance = Mathf.Min(scene.shadowDistance, 40);
                    data.terrainPixelError = Mathf.Max(scene.terrainPixelError, 30); data.baseMapDistance = Mathf.Min(scene.baseMapDistance, 120);
                    data.grassDistance = Mathf.Min(scene.grassDistance, 30); data.grassDensity = scene.grassDensity * .3f;
                    data.cameraFar = Mathf.Min(scene.cameraFar, 1500); data.renderScale = .75f;
                    data.towerDetailDistance = LowDetailDistance; data.towerLodScale = .35f; data.occlusionCulling = true;
                    data.liveReflections = false; data.sunShafts = 0; data.clouds = 0; data.riverRefraction = false;
                    data.lampLights = Mathf.Min(scene.lampLights, 12);
                    break;
            }
        }

        /// <summary>The preset whose values <paramref name="data"/> currently holds, or Custom.</summary>
        public static GraphicsQuality Detect(GameSettingsData data, GraphicsBaseline scene)
        {
            var probe = new GameSettingsData();
            for (var quality = GraphicsQuality.Low; quality <= GraphicsQuality.High; quality++)
            {
                Apply(probe, quality, scene);
                if (probe.shadowQuality == data.shadowQuality && Same(probe.shadowDistance, data.shadowDistance) &&
                    Same(probe.terrainPixelError, data.terrainPixelError) && Same(probe.baseMapDistance, data.baseMapDistance) &&
                    Same(probe.grassDistance, data.grassDistance) && Same(probe.grassDensity, data.grassDensity) &&
                    Same(probe.cameraFar, data.cameraFar) && Same(probe.renderScale, data.renderScale) &&
                    Same(probe.towerDetailDistance, data.towerDetailDistance) && Same(probe.towerLodScale, data.towerLodScale) &&
                    probe.occlusionCulling == data.occlusionCulling && probe.liveReflections == data.liveReflections &&
                    probe.sunShafts == data.sunShafts && probe.clouds == data.clouds && probe.riverRefraction == data.riverRefraction &&
                    probe.lampLights == data.lampLights)
                    return quality;
            }
            return GraphicsQuality.Custom;
        }

        private static bool Same(float a, float b) => Mathf.Abs(a - b) <= Mathf.Max(.001f, Mathf.Abs(a) * .001f);
    }

    /// <summary>Where the settings are kept between sessions. Tests replace it with an in-memory store.</summary>
    public interface IGameSettingsStore
    {
        string Load();
        void Save(string json);
    }

    public sealed class PlayerPrefsSettingsStore : IGameSettingsStore
    {
        public const string Key = "TowerOfBabel.Settings";
        public string Load() => PlayerPrefs.GetString(Key, "");
        public void Save(string json) { PlayerPrefs.SetString(Key, json); PlayerPrefs.Save(); }
    }

    public sealed class MemorySettingsStore : IGameSettingsStore
    {
        public string Json = "";
        public string Load() => Json;
        public void Save(string json) => Json = json;
    }

    /// <summary>The player's settings: loaded once, changed by the settings menu, read by the systems they steer.</summary>
    public static class GameSettings
    {
        private static IGameSettingsStore store = new PlayerPrefsSettingsStore();
        private static GameSettingsData current;

        /// <summary>Raised after any value changed (and when the settings are reloaded).</summary>
        public static event Action Changed;

        public static GameSettingsData Current => current ??= Read();

        public static IGameSettingsStore Store
        {
            get => store;
            set { store = value ?? new PlayerPrefsSettingsStore(); current = null; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { current = null; Changed = null; }

        private static GameSettingsData Read()
        {
            GameSettingsData data = null;
            try
            {
                string json = store.Load();
                if (!string.IsNullOrEmpty(json)) data = JsonUtility.FromJson<GameSettingsData>(json);
            }
            catch (Exception exception) { Debug.LogWarning("Saved settings could not be read; using defaults. " + exception.Message); }
            data ??= new GameSettingsData();
            Sanitize(data);
            return data;
        }

        /// <summary>Keeps every value inside the range the menu offers; a hand-edited or damaged save cannot break the game.</summary>
        public static void Sanitize(GameSettingsData data)
        {
            data.version = GameSettingsData.CurrentVersion;
            data.resolutionWidth = Mathf.Max(0, data.resolutionWidth); data.resolutionHeight = Mathf.Max(0, data.resolutionHeight);
            data.displayMode = (DisplayMode)Mathf.Clamp((int)data.displayMode, 0, 2);
            data.quality = (GraphicsQuality)Mathf.Clamp((int)data.quality, 0, 3);
            data.shadowQuality = (ShadowQuality)Mathf.Clamp((int)data.shadowQuality, 0, 3);
            data.shadowDistance = Clamp(data.shadowDistance, Ranges.ShadowDistance, 120);
            data.terrainPixelError = Clamp(data.terrainPixelError, Ranges.TerrainPixelError, 8);
            data.baseMapDistance = Clamp(data.baseMapDistance, Ranges.BaseMapDistance, 1000);
            data.grassDistance = Clamp(data.grassDistance, Ranges.GrassDistance, 95);
            data.grassDensity = Clamp(data.grassDensity, Ranges.GrassDensity, .55f);
            data.cameraFar = Clamp(data.cameraFar, Ranges.CameraFar, 65536);
            data.cameraNear = Clamp(data.cameraNear, Ranges.CameraNear, .3f);
            data.cameraFov = Clamp(data.cameraFov, Ranges.CameraFov, 60);
            data.renderScale = Clamp(data.renderScale, Ranges.RenderScale, 1);
            data.towerDetailDistance = Clamp(data.towerDetailDistance, Ranges.TowerDetailDistance, GraphicsPresets.HighDetailDistance);
            data.towerLodScale = Clamp(data.towerLodScale, Ranges.TowerLodScale, 1);
            data.sunShafts = Mathf.Clamp(data.sunShafts, 0, 2); data.clouds = Mathf.Clamp(data.clouds, 0, 2);
            data.lampLights = Mathf.Clamp(data.lampLights, 0, TowerOfBabel.World.Tower.TowerLavaLamps.MaximumLights);
            data.masterVolume = Clamp(data.masterVolume, Ranges.Volume, 1); data.musicVolume = Clamp(data.musicVolume, Ranges.Volume, 1);
            data.uiVolume = Clamp(data.uiVolume, Ranges.Volume, 1); data.vfxVolume = Clamp(data.vfxVolume, Ranges.Volume, 1);
            data.lookSensitivity = Clamp(data.lookSensitivity, Ranges.LookSensitivity, 1);
            data.bindingOverrides ??= "";
        }

        private static float Clamp(float value, Vector2 range, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, range.x, range.y);

        /// <summary>Writes the settings to the store and tells every listener.</summary>
        public static void Save()
        {
            Sanitize(Current);
            try { store.Save(JsonUtility.ToJson(Current)); }
            catch (Exception exception) { Debug.LogWarning("Settings could not be saved. " + exception.Message); }
            Changed?.Invoke();
        }

        /// <summary>Tells every listener without writing (while a slider is being dragged).</summary>
        public static void NotifyChanged() => Changed?.Invoke();

        /// <summary>Slider limits, shared by the menu and the sanitizer.</summary>
        public static class Ranges
        {
            public static readonly Vector2 ShadowDistance = new(0, 300), TerrainPixelError = new(1, 60), BaseMapDistance = new(0, 2000),
                GrassDistance = new(0, 250), GrassDensity = new(0, 1), CameraFar = new(300, 70000), CameraNear = new(.05f, 1.5f),
                CameraFov = new(45, 110), RenderScale = new(.5f, 1), TowerDetailDistance = new(30, 400), TowerLodScale = new(.2f, 1),
                Volume = new(0, 1), LookSensitivity = new(.1f, 4);
        }
    }
}
