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
            JsonUtility.FromJsonOverwrite("{\"m_AdditionalLightsShadowResolutionTier\":-1}", light.GetUniversalAdditionalLightData());
        public Texture2D ripples;
        public Texture2D caustics;
        [Header("Water surface pattern")]
        public Texture2D surfaceCaustics;
        [Range(0,1)] public float surfacePatternOpacity = .22f;
        [Tooltip("Extra texture mip filtering; softens the surface pattern without blurring objects through the glass.")]
        [Range(0,4)] public float surfacePatternBlur = 3;
        [Min(.01f)] public float surfacePatternScale = .16f;
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
        [Header("Water volume (presentation 12)")]
        public Color shallowColor = new(.25f, .83f, .94f);
        public Color deepColor = new(.04f, .5f, .82f);
        [Tooltip("Water path (m) at which the colour reaches the deep tint.")]
        [Min(.05f)] public float deepPath = .6f;
        public Color wallColor = new(.93f, .96f, 1f);
        public Color edgeTint = new(.35f, .82f, .74f);
        [Range(1, 1.6f)] public float waterIor = 1.33f;
        [Range(1, 2)] public float glassIor = 1.5f;
        [Tooltip("1 is physical refraction through the real water path; lower keeps the floor below readable.")]
        [Range(0, 2)] public float refractionStrength = .4f;
        [Min(.05f)] public float bedTileSize = .6f;
        [Range(0, 1)] public float bedEtchOpacity = .05f;
        [Range(0, 3)] public float bedCausticStrength = 1.6f;
        [Range(0, 3)] public float meniscusIntensity = 1.2f;
        [Range(.001f, .03f)] public float meniscusWidth = .008f;
        [Range(0, 1)] public float wallTint = .35f;
        [Range(0, 3)] public float inscatter = 1.6f;
        [Tooltip("Micro-bubble cells per metre on three depth layers; 0 disables.")]
        [Range(0, 6)] public float bubbleDensity = 2.2f;
        [Range(1, 24)] public int volumeSteps = 12;
        [Range(1, 24)] public int liteSteps = 6;
        [Header("Projected caustic patches")]
        [Range(1,4)] public int projectionPatchCount = 4;
        [Min(0)] public float projectionAheadDistance = 28;
        [Tooltip("Higher density makes each caustic branch smaller without enlarging its light footprint.")]
        [Range(1,5)] public float projectionPatternDensity = 2.5f;
        [Range(20,155)] public float projectionAngle = 90;
        [Tooltip("Contribution of the sparse bright focus points.")]
        [Range(0,1)] public float projectionHighlightOpacity = .95f;
        [Tooltip("Contribution of the faint flowing branches between bright points.")]
        [Range(0,1)] public float projectionDetailOpacity = .06f;
        public Color projectionTint = new(1, 1, .99f);
        [Tooltip("Peak reflected light during a burst, before the fade envelope.")]
        [Min(0)] public float daylightIntensity = 96;
        [Min(0)] public float nightIntensity = 64;
        public Vector2 projectionBurstDuration = new(6, 9);
        public Vector2 projectionQuietDuration = new(2, 4);
        [Range(1,2)] public int projectionMaxActivePatches = 2;
        public Vector2 projectionStartSpacing = new(1.2f, 2.5f);
        public Vector2 projectionGlobalQuietInterval = new(14, 20);
        public Vector2 projectionGlobalQuietDuration = new(1, 2);
        [Min(.1f)] public float projectionGlobalFade = 1.5f;
        [Min(.05f)] public float projectionBurstFade = 2.8f;
        [Min(.1f)] public float projectionRelocationFade = 1.6f;
        [Tooltip("Cutout translation in texture UV per second; projectors do not rotate.")]
        [Min(0)] public float projectionCutoutSpeed = .06f;
        [Min(0)] public float projectionCutoutWarp = .035f;
        [Range(0,.5f)] public float projectionFlowVariation = .15f;
        [Min(0)] public float projectionFlowSway = .035f;
        public Vector2 projectionDetailLifetime = new(6, 10);
        public Vector2 projectionHighlightLifetime = new(2, 4);
        [Min(.7f)] public float projectionHighlightFade = .7f;
        [Min(.1f)] public float projectionRange = 14;
        [Min(1)] public float selectionDistance = 42;
        [Min(.1f)] public float patchSeparation = 8;
        public Vector4 Enclosure => new(Mathf.Clamp(waterTop,.02f,.08f), Mathf.Clamp(waterBottom,waterTop+.02f,.19f),
            Mathf.Clamp(glassThickness,.005f,.04f), Mathf.Clamp(glassThickness,.005f,.04f));

        public void Apply(Material material)
        {
            material.SetTexture("_RippleNormal", ripples);
            material.SetTexture("_CausticsTex", surfaceCaustics != null ? surfaceCaustics : caustics);
            material.SetTexture("_Environment", environment);
            material.SetVector("_RiverLayout", layout);
            // Absorption is a physical coefficient per metre, not a colour: no gamma conversion.
            material.SetVector("_Absorption", new Vector4(absorption.r, absorption.g, absorption.b, 0));
            material.SetColor("_WaterColor", waterTint);
            material.SetVector("_RiverOptics", new Vector4(Enclosure.y-Enclosure.x, refraction, reflections, rippleStrength));
            material.SetVector("_RiverMotion", new Vector4(rippleScale, flowSpeed, waterHighlights, detailDistance));
            material.SetVector("_RiverPattern",new Vector4(surfacePatternScale,Mathf.Clamp01(surfacePatternOpacity),surfacePatternBlur,0));
            material.SetColor("_GlassTint",glassTint);
            material.SetVector("_RiverShell",new Vector4(rimWidth,jointSpacing,glassReflections,glassThickness));
            material.SetVector("_RiverEnclosure",Enclosure);
            material.SetColor("_ShallowColor",shallowColor);
            material.SetColor("_DeepColor",deepColor);
            material.SetColor("_WallColor",wallColor);
            material.SetColor("_EdgeTint",edgeTint);
            material.SetVector("_RiverVolume",new Vector4(waterIor,glassIor,refractionStrength,deepPath));
            material.SetVector("_RiverBed",new Vector4(bedTileSize,bedEtchOpacity,bedCausticStrength,meniscusIntensity));
            material.SetVector("_RiverDetail",new Vector4(bubbleDensity,wallTint,meniscusWidth,inscatter));
        }

        /// <summary>Volume march steps for a floor (lite budget on mobile).</summary>
        public int Steps(bool reduced) => Mathf.Clamp(reduced ? liteSteps : volumeSteps, 1, 24);

        public static float WaterPath(float thickness, float normalViewCosine) =>
            Mathf.Max(.001f, thickness) / Mathf.Max(.15f, Mathf.Abs(normalViewCosine));
    }
}
