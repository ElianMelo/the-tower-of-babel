using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower of Babel/Building finish profile")]
    public sealed class AeroBuildingFinishProfile : ScriptableObject
    {
        public enum ReflectionQuality { EnvironmentOnly, BalancedPC }
        public ReflectionQuality quality = ReflectionQuality.BalancedPC;
        [Header("Pearl marble")]
        [Range(0, 1)] public float reflections = .7f;
        [Range(0, 1)] public float polish = .65f;
        [Range(0, .08f)] public float roughnessVariation = .025f;
        [Header("Aquatic accents")]
        [Range(0, 1)] public float inlayDepth = .55f;
        [Range(0, .3f)] public float iridescence = .075f;
        [Range(0, 2)] public float glints = .8f;
        [Range(0, 1)] public float edgeLight = .14f;
        [Header("Shared environment")]
        public Cubemap dayEnvironment, nightEnvironment;
        [ColorUsage(false, true)] public Color dayTint = Color.white;
        [ColorUsage(false, true)] public Color nightTint = new(.14f, .21f, .32f);
        [Header("Bounded live reflection")]
        [Range(.25f, .5f)] public float resolutionScale = .5f;
        [Range(128, 1024)] public int maximumResolution = 1024;
        [Range(8, 32)] public float range = 32;
        [Min(.05f)] public float transitionSeconds = .35f;
        [Tooltip("Renderer containing only opaque geometry and sky; no recursive effects.")]
        public int reflectionRendererIndex = -1;
        public UniversalRenderPipelineAsset livePipeline;
        public bool SupportsLive => quality == ReflectionQuality.BalancedPC && !Application.isMobilePlatform &&
            QualitySettings.realtimeReflectionProbes && livePipeline != null && GraphicsSettings.currentRenderPipeline == livePipeline;

        public void Apply()
        {
            Shader.SetGlobalFloat("_AeroFinishEnabled", 1);
            Shader.SetGlobalVector("_AeroFinishSurface", new Vector4(reflections, polish, roughnessVariation,
                SupportsLive ? 1 : .5f));
            Shader.SetGlobalVector("_AeroFinishOptics", new Vector4(inlayDepth, iridescence, glints, edgeLight));
            var day = dayEnvironment != null ? dayEnvironment : nightEnvironment;
            var night = nightEnvironment != null ? nightEnvironment : day;
            Shader.SetGlobalFloat("_AeroFinishEnvironmentReady", day != null ? 1 : 0);
            Shader.SetGlobalFloat("_AeroFinishEnvironmentShared", day == night ? 1 : 0);
            if (day != null) Shader.SetGlobalTexture("_AeroFinishDay", day);
            if (night != null) Shader.SetGlobalTexture("_AeroFinishNight", night);
            Shader.SetGlobalColor("_AeroFinishDayTint", dayTint);
            Shader.SetGlobalColor("_AeroFinishNightTint", nightTint);
        }
    }
}
