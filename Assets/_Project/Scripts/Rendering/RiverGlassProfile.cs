using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower of Babel/River glass profile")]
    public sealed class RiverGlassProfile : ScriptableObject
    {
        [HideInInspector] public int presentationVersion;
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
        [Min(.001f)] public float opticalThickness = .14f;
        [Min(.01f)] public float rippleScale = .46f;
        [Range(0, 1)] public float rippleStrength = .24f;
        [Min(0)] public float flowSpeed = .045f;
        [Range(0, .02f)] public float refraction = .003f;
        [Range(0, 1)] public float reflections = .42f;
        public Color glassTint = new(.55f,.86f,.94f);
        [Range(.005f,.04f)] public float glassThickness = .02f;
        [Range(.02f,.08f)] public float waterTop = .04f;
        [Range(.1f,.19f)] public float waterBottom = .18f;
        [Range(.01f,.12f)] public float rimWidth = .045f;
        [Min(2)] public float jointSpacing = 12;
        [Range(0,2)] public float waterHighlights = .7f;
        [Range(0,1)] public float glassReflections = .32f;
        [Min(1)] public float refractionDistance = 24;
        [Min(1)] public float detailDistance = 42;
        [Range(1,3)] public int detailedFloorLimit = 2;
        [Range(90,140)] public float projectionAngle = 120;
        public Color projectionTint = new(.88f, .96f, 1);
        [Min(0)] public float daylightIntensity = 28;
        [Min(0)] public float nightIntensity = 36;
        [Min(.1f)] public float projectionRange = 8;
        [Min(1)] public float selectionDistance = 24;
        [Min(.1f)] public float patchSeparation = 5;
        public Vector4 Enclosure => new(Mathf.Clamp(waterTop,.02f,.08f), Mathf.Clamp(waterBottom,waterTop+.02f,.19f),
            Mathf.Clamp(glassThickness,.005f,.04f), Mathf.Clamp(glassThickness,.005f,.04f));

        public void Apply(Material material)
        {
            material.SetTexture("_RippleNormal", ripples);
            material.SetTexture("_CausticsTex", caustics);
            material.SetTexture("_Environment", environment);
            material.SetVector("_RiverLayout", layout);
            material.SetColor("_Absorption", absorption);
            material.SetColor("_WaterColor", waterTint);
            material.SetVector("_RiverOptics", new Vector4(Enclosure.y-Enclosure.x, refraction, reflections, rippleStrength));
            material.SetVector("_RiverMotion", new Vector4(rippleScale, flowSpeed, waterHighlights, detailDistance));
            material.SetColor("_GlassTint",glassTint);
            material.SetVector("_RiverShell",new Vector4(rimWidth,jointSpacing,glassReflections,glassThickness));
            material.SetVector("_RiverEnclosure",Enclosure);
        }

        public static float WaterPath(float thickness, float normalViewCosine) =>
            Mathf.Max(.001f, thickness) / Mathf.Max(.15f, Mathf.Abs(normalViewCosine));
    }
}
