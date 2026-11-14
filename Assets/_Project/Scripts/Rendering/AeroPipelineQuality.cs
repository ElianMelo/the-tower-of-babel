using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Applies the game's shadow and render-scale settings to the active URP asset and puts the asset back as it was
    /// when the game stops. The top shadow quality is the asset exactly as saved; nothing is written while the
    /// requested values already match.
    /// </summary>
    public static class AeroPipelineQuality
    {
        public const int ShadowsOff = 0, ShadowsLow = 1, ShadowsMedium = 2, ShadowsHigh = 3;

        private static UniversalRenderPipelineAsset asset;
        private static float savedDistance, savedScale;
        private static int savedResolution, savedCascades;
        private static readonly Dictionary<UniversalAdditionalLightData, SoftShadowQuality> savedLights = new();

        /// <summary>The active pipeline asset's own shadow distance (before any setting is applied).</summary>
        public static float SceneShadowDistance => Capture() ? savedDistance : 120f;
        public static bool HasPipeline => Capture();
        /// <summary>The values in effect on the active pipeline asset right now.</summary>
        public static float CurrentShadowDistance => Capture() ? asset.shadowDistance : 0f;
        public static float CurrentRenderScale => Capture() ? asset.renderScale : 1f;
        public static int CurrentShadowCascades => Capture() ? asset.shadowCascadeCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { asset = null; savedLights.Clear(); }

        private static bool Capture()
        {
            var current = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (current == asset) return asset != null;
            // A different pipeline became active: the previous one goes back to its own values first.
            Restore();
            asset = current;
            if (asset == null) return false;
            savedDistance = asset.shadowDistance; savedScale = asset.renderScale;
            savedResolution = asset.mainLightShadowmapResolution; savedCascades = asset.shadowCascadeCount;
            return true;
        }

        /// <summary>
        /// Shadow quality 0 to 3 (off, low, medium, high as saved), shadow distance in metres and render scale.
        /// </summary>
        public static void Apply(int shadowQuality, float shadowDistance, float renderScale)
        {
            if (!Capture()) return;
            shadowQuality = Mathf.Clamp(shadowQuality, ShadowsOff, ShadowsHigh);
            int resolution = shadowQuality >= ShadowsHigh ? savedResolution : Mathf.Min(savedResolution, shadowQuality == ShadowsMedium ? 2048 : 1024);
            int cascades = shadowQuality >= ShadowsHigh ? savedCascades : Mathf.Min(savedCascades, shadowQuality == ShadowsMedium ? 2 : 1);
            float distance = shadowQuality == ShadowsOff ? 0f : Mathf.Max(0f, shadowDistance);
            if (!Mathf.Approximately(asset.shadowDistance, distance)) asset.shadowDistance = distance;
            if (asset.mainLightShadowmapResolution != resolution) asset.mainLightShadowmapResolution = resolution;
            if (asset.shadowCascadeCount != cascades) asset.shadowCascadeCount = cascades;
            renderScale = Mathf.Clamp(renderScale, .25f, 2f);
            if (!Mathf.Approximately(asset.renderScale, renderScale)) asset.renderScale = renderScale;

            // Softness of the sun and moon shadows: as authored at the top quality, cheaper filters below it.
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (light.type != LightType.Directional || !light.TryGetComponent(out UniversalAdditionalLightData data)) continue;
                if (!savedLights.ContainsKey(data)) savedLights.Add(data, data.softShadowQuality);
                SoftShadowQuality wanted = shadowQuality >= ShadowsHigh ? savedLights[data]
                    : shadowQuality == ShadowsMedium ? SoftShadowQuality.Medium : SoftShadowQuality.Low;
                if (data.softShadowQuality != wanted) data.softShadowQuality = wanted;
            }
        }

        /// <summary>Puts the pipeline asset and the lights back as they were saved.</summary>
        public static void Restore()
        {
            if (asset != null)
            {
                if (!Mathf.Approximately(asset.shadowDistance, savedDistance)) asset.shadowDistance = savedDistance;
                if (asset.mainLightShadowmapResolution != savedResolution) asset.mainLightShadowmapResolution = savedResolution;
                if (asset.shadowCascadeCount != savedCascades) asset.shadowCascadeCount = savedCascades;
                if (!Mathf.Approximately(asset.renderScale, savedScale)) asset.renderScale = savedScale;
            }
            foreach (var pair in savedLights)
                if (pair.Key != null && pair.Key.softShadowQuality != pair.Value) pair.Key.softShadowQuality = pair.Value;
            savedLights.Clear();
            asset = null;
        }
    }
}
