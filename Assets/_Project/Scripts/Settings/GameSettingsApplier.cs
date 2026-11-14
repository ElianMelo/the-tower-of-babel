using TowerOfBabel.Rendering;
using TowerOfBabel.World;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.Settings
{
    /// <summary>
    /// Applies the player's settings to the running scene: shadows and render scale on the pipeline, terrain detail,
    /// the camera, the far tower's culling, the render feature tiers, sound levels and resolution. It measures the
    /// scene's own values first (the High preset keeps them) and puts everything back when it is disabled.
    /// Runs after Cinemachine has written the camera each frame, and before the cheat menu's field-of-view override.
    /// </summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(9000)]
    public sealed class GameSettingsApplier : MonoBehaviour
    {
        public static GameSettingsApplier Instance { get; private set; }

        private GraphicsBaseline baseline = GraphicsBaseline.Default;
        private bool measured, applied;
        private Terrain[] terrains = System.Array.Empty<Terrain>();
        private float[] terrainPixelError, terrainBaseMap, terrainGrassDistance, terrainGrassDensity;
        private InstancedFarChunkRenderer far;
        private float farDetailDistance, farLodScale;
        private bool farOcclusion;
        private TowerLavaLamps lamps;
        private Camera cameraInUse;
        private float cameraFar, cameraNear, cameraFov;
        private float listenerVolume = 1f;

        /// <summary>The scene's own graphics values, measured before anything was applied.</summary>
        public GraphicsBaseline Baseline { get { Measure(); return baseline; } }

        private void OnEnable()
        {
            if (Instance != null && Instance != this) { enabled = false; return; }
            Instance = this;
            GameSettings.Changed += Apply;
        }

        private void Start()
        {
            Measure();
            GameSettingsData data = GameSettings.Current;
            // A preset follows the scene: High is always the scene as it is authored now.
            if (!data.graphicsInitialized) GraphicsPresets.Apply(data, GraphicsQuality.High, baseline);
            else if (data.quality != GraphicsQuality.Custom) GraphicsPresets.Apply(data, data.quality, baseline);
            Apply();
            ApplyVideo();
        }

        private void OnDisable()
        {
            if (Instance != this) return;
            GameSettings.Changed -= Apply;
            Restore();
            Instance = null;
        }

        /// <summary>Reads the scene's own values once, before any setting touches them.</summary>
        public void Measure()
        {
            if (measured) return;
            measured = true;
            baseline = GraphicsBaseline.Default;
            if (AeroPipelineQuality.HasPipeline) baseline.shadowDistance = AeroPipelineQuality.SceneShadowDistance;
            terrains = Terrain.activeTerrains ?? System.Array.Empty<Terrain>();
            terrainPixelError = new float[terrains.Length]; terrainBaseMap = new float[terrains.Length];
            terrainGrassDistance = new float[terrains.Length]; terrainGrassDensity = new float[terrains.Length];
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                // The presentation component sets the runtime view when it enables; its values are the scene's intent.
                var presentation = terrain.GetComponent<TerrainPresentationQuality>();
                if (presentation != null && Application.isPlaying) presentation.ApplyQuality(true);
                terrainPixelError[i] = terrain.heightmapPixelError; terrainBaseMap[i] = terrain.basemapDistance;
                terrainGrassDistance[i] = terrain.detailObjectDistance; terrainGrassDensity[i] = terrain.detailObjectDensity;
            }
            if (terrains.Length > 0)
            {
                baseline.terrainPixelError = terrainPixelError[0]; baseline.baseMapDistance = terrainBaseMap[0];
                baseline.grassDistance = terrainGrassDistance[0]; baseline.grassDensity = terrainGrassDensity[0];
            }
            far = FindFirstObjectByType<InstancedFarChunkRenderer>();
            if (far != null) { farDetailDistance = far.DetailDrawDistance; farLodScale = far.LodDistanceScale; }
            farOcclusion = InstancedFarChunkRenderer.OcclusionCulling;
            lamps = FindFirstObjectByType<TowerLavaLamps>();
            if (lamps != null) baseline.lampLights = lamps.MaxLights;
            cameraInUse = Camera.main;
            if (cameraInUse != null)
            {
                cameraFar = cameraInUse.farClipPlane;
                // The ocean widens the main camera's far plane when it renders: that is the far plane the scene really has.
                bool ocean = FindFirstObjectByType<AeroOceanSurface>() != null;
                baseline.cameraFar = ocean ? Mathf.Max(cameraFar, AeroOceanSurface.MinimumFarClip) : cameraFar;
                cameraNear = baseline.cameraNear = cameraInUse.nearClipPlane;
                cameraFov = baseline.cameraFov = cameraInUse.fieldOfView;
            }
            listenerVolume = AudioListener.volume;
        }

        /// <summary>Applies the current settings. Called at start and whenever a setting changes.</summary>
        public void Apply()
        {
            if (!isActiveAndEnabled) return;
            Measure();
            applied = true;
            GameSettingsData data = GameSettings.Current;
            AeroPipelineQuality.Apply((int)data.shadowQuality, data.shadowDistance, data.renderScale);
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;
                Set(terrain.heightmapPixelError, data.terrainPixelError, v => terrain.heightmapPixelError = v);
                Set(terrain.basemapDistance, data.baseMapDistance, v => terrain.basemapDistance = v);
                Set(terrain.detailObjectDistance, data.grassDistance, v => terrain.detailObjectDistance = v);
                Set(terrain.detailObjectDensity, data.grassDensity, v => terrain.detailObjectDensity = v);
            }
            if (far == null) far = FindFirstObjectByType<InstancedFarChunkRenderer>();
            if (far != null)
            {
                far.DetailDrawDistance = data.towerDetailDistance;
                far.LodDistanceScale = data.towerLodScale;
            }
            InstancedFarChunkRenderer.OcclusionCulling = data.occlusionCulling;
            AeroRenderQuality.SunShafts = data.sunShafts;
            AeroRenderQuality.Clouds = data.clouds;
            AeroRenderQuality.LiveReflections = data.liveReflections;
            AeroRenderQuality.ReducedRiverRefraction = !data.riverRefraction;
            if (lamps == null) lamps = FindFirstObjectByType<TowerLavaLamps>();
            if (lamps != null) lamps.LightBudget = data.lampLights;
            AudioListener.volume = data.masterVolume;
            AeroAudio.Music = data.musicVolume; AeroAudio.Ui = data.uiVolume; AeroAudio.Vfx = data.vfxVolume;
            ApplyCamera();
        }

        private static void Set(float current, float wanted, System.Action<float> assign)
        {
            if (!Mathf.Approximately(current, wanted)) assign(wanted);
        }

        /// <summary>
        /// The game renders as fast as it can: no frame-rate target. The networking library sets one (500) whenever a
        /// session starts, so it is cleared again here. Vertical sync stays as the project's quality settings have it.
        /// </summary>
        public static bool UncappedFrameRate = true;

        // Cinemachine writes the camera from its lens every frame; the settings are put on top of it afterwards.
        private void LateUpdate()
        {
            if (UncappedFrameRate && Application.targetFrameRate != -1) Application.targetFrameRate = -1;
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (!applied) return;
            if (cameraInUse == null)
            {
                cameraInUse = Camera.main;
                if (cameraInUse == null) return;
                cameraFar = cameraInUse.farClipPlane; cameraNear = cameraInUse.nearClipPlane; cameraFov = cameraInUse.fieldOfView;
            }
            GameSettingsData data = GameSettings.Current;
            float near = Mathf.Min(data.cameraNear, data.cameraFar - 1f);
            // The ocean sets the main camera's far plane again when it renders; it is told the player's choice.
            AeroOceanSurface.PlayerFarClip = data.cameraFar;
            if (!Mathf.Approximately(cameraInUse.farClipPlane, data.cameraFar)) cameraInUse.farClipPlane = data.cameraFar;
            if (!Mathf.Approximately(cameraInUse.nearClipPlane, near)) cameraInUse.nearClipPlane = near;
            if (!Mathf.Approximately(cameraInUse.fieldOfView, data.cameraFov)) cameraInUse.fieldOfView = data.cameraFov;
        }

        /// <summary>
        /// Sets the resolution and display mode. The Editor's Game view has its own size; this only acts in a build.
        /// </summary>
        public void ApplyVideo()
        {
            if (Application.isEditor) return;
            GameSettingsData data = GameSettings.Current;
            FullScreenMode mode = data.displayMode switch
            {
                DisplayMode.Fullscreen => FullScreenMode.ExclusiveFullScreen,
                DisplayMode.Windowed => FullScreenMode.Windowed,
                _ => FullScreenMode.FullScreenWindow
            };
            int width = data.resolutionWidth, height = data.resolutionHeight;
            if (width <= 0 || height <= 0)
            {
                // No choice saved: keep the size, change only the mode when it differs.
                if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
                return;
            }
            if (Screen.width != width || Screen.height != height || Screen.fullScreenMode != mode)
                Screen.SetResolution(width, height, mode);
        }

        private void Restore()
        {
            if (!measured) return;
            AeroPipelineQuality.Restore();
            for (int i = 0; i < terrains.Length; i++)
            {
                Terrain terrain = terrains[i];
                if (terrain == null) continue;
                terrain.heightmapPixelError = terrainPixelError[i]; terrain.basemapDistance = terrainBaseMap[i];
                terrain.detailObjectDistance = terrainGrassDistance[i]; terrain.detailObjectDensity = terrainGrassDensity[i];
            }
            if (far != null) { far.DetailDrawDistance = farDetailDistance; far.LodDistanceScale = farLodScale; }
            InstancedFarChunkRenderer.OcclusionCulling = farOcclusion;
            AeroRenderQuality.Reset();
            AeroAudio.Reset();
            if (lamps != null) lamps.LightBudget = baseline.lampLights;
            AudioListener.volume = listenerVolume;
            AeroOceanSurface.PlayerFarClip = 0;
            if (cameraInUse != null)
            {
                cameraInUse.farClipPlane = cameraFar; cameraInUse.nearClipPlane = cameraNear; cameraInUse.fieldOfView = cameraFov;
            }
            measured = applied = false;
        }
    }
}
