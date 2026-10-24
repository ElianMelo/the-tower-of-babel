using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower of Babel/River glass profile")]
    public sealed class RiverGlassProfile : ScriptableObject
    {
        public static bool SupportsProjection => GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline &&
            pipeline.supportsAdditionalLightShadows;
        public static void ConfigureProjectionLight(Light light) =>
            JsonUtility.FromJsonOverwrite("{\"m_AdditionalLightsShadowResolutionTier\":0}", light.GetUniversalAdditionalLightData());
        public Texture2D ripples;
        public Texture2D caustics;
        public Cubemap environment;
        public Vector4 layout = new(96, 3, 6, 1.3f);
        public Color absorption = new(.9f, .22f, .09f);
        public Color waterTint = new(.18f, .65f, .76f);
        [Min(.001f)] public float opticalThickness = .2f;
        [Min(.01f)] public float rippleScale = .46f;
        [Range(0, 1)] public float rippleStrength = .06f;
        [Min(0)] public float flowSpeed = .045f;
        [Range(0, .02f)] public float refraction = .003f;
        [Range(0, 1)] public float reflections = .42f;
        public Color projectionTint = new(.32f, .82f, 1);
        [Min(0)] public float daylightIntensity = 28;
        [Min(0)] public float nightIntensity = 36;
        [Min(.1f)] public float projectionRange = 10;
        [Min(1)] public float selectionDistance = 42;
        [Min(.1f)] public float patchSeparation = 5;

        public void Apply(Material material)
        {
            material.SetTexture("_RippleNormal", ripples);
            material.SetTexture("_CausticsTex", caustics);
            material.SetTexture("_Environment", environment);
            material.SetVector("_RiverLayout", layout);
            material.SetColor("_Absorption", absorption);
            material.SetColor("_WaterColor", waterTint);
            material.SetVector("_RiverOptics", new Vector4(opticalThickness, refraction, reflections, rippleStrength));
            material.SetVector("_RiverMotion", new Vector4(rippleScale, flowSpeed, 0, 0));
        }

        public static float WaterPath(float thickness, float normalViewCosine) =>
            Mathf.Max(.001f, thickness) / Mathf.Max(.15f, Mathf.Abs(normalViewCosine));
    }
}
