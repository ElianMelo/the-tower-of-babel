#if UNITY_EDITOR
using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Authors the Frutiger Aero shading pass: global wetness profile, the Restrained post profile,
/// HDR grading on PC and the Tower scene wiring. Bright Aero and Y2K were rejected on 2026-11-07;
/// time/weather/season grading now comes from AeroAtmospherePresetAuthoring.
/// Idempotent; see Documentation/FrutigerAero/Lighting/POST_PROFILES.md and WETNESS.md.
/// </summary>
public static class AeroShadingAuthoring
{
    public const string WetnessPath = "Assets/_Project/Environment/Lighting/AeroWetness.asset";
    public const string RestrainedPath = "Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset";
    public const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
    const string Drops = "Assets/MoveRotateResize/DYNAMIC- Falling Rain/Rain/Textures/rain_drops_01.tga";
    const string Streaks = "Assets/MoveRotateResize/DYNAMIC- Falling Rain/Rain/Textures/rain_streaks_01.png";
    const string Ripples = "Assets/MoveRotateResize/DYNAMIC- RainWetness/Shaders/Decal Shaders/Puddles/rain_ripples.png";

    static void Guard()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's shading in Edit Mode.");
    }

    [MenuItem("Tower of Babel/Rendering/Apply Aero shading, wetness and post profile")]
    public static string Apply()
    {
        Guard();
        var wetness = ConfigureWetness();
        ConfigureCaustics();
        ConfigurePostProfiles();
        ConfigurePipeline();
        string materials = AeroMaterialUpgrade.Apply();
        string scene = ConfigureScene(wetness);
        AssetDatabase.SaveAssets();
        return materials + " " + scene;
    }

    public static AeroWetnessProfile ConfigureWetness()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>(WetnessPath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<AeroWetnessProfile>();
            AssetDatabase.CreateAsset(profile, WetnessPath);
        }
        profile.drops = AssetDatabase.LoadAssetAtPath<Texture2D>(Drops);
        profile.streaks = AssetDatabase.LoadAssetAtPath<Texture2D>(Streaks);
        profile.ripples = AssetDatabase.LoadAssetAtPath<Texture2D>(Ripples);
        EditorUtility.SetDirty(profile);
        return profile;
    }

    /// <summary>
    /// One caustic source for every water light: the Crest net (MIT). Lake surface, lamp
    /// cookie, nearby-water light feature, river floor and river profile all sample it.
    /// </summary>
    public static Texture2D ConfigureCaustics()
    {
        string path = AeroIslandAtmosphereAuthoring.CausticPath;
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        bool dirty = importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.filterMode != FilterMode.Trilinear ||
            importer.anisoLevel != 4 || !importer.sRGBTexture || importer.textureCompression != TextureImporterCompression.CompressedHQ;
        if (dirty)
        {
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4; importer.sRGBTexture = true; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        var caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(path) ?? throw new InvalidOperationException("Missing " + path);
        foreach (var materialPath in new[] { AeroWaterLightAuthoring.WaterPath, AeroAtmosphereAuthoring.CookiePath, RiverGlassAuthoring.GlassPath })
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null || !material.HasProperty("_CausticsTex")) continue;
            material.SetTexture("_CausticsTex", caustics); EditorUtility.SetDirty(material);
        }
        foreach (var name in new[] { "PC_Renderer", "Mobile_Renderer" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{name}.asset");
            foreach (var feature in renderer.rendererFeatures.OfType<AeroLightRendererFeature>())
            {
                AeroCausticsAuthoring.ConfigureWaterLight(feature, AeroCausticsAuthoring.LampFlipbook);
            }
        }
        return caustics;
    }

    static T Override<T>(VolumeProfile profile) where T : VolumeComponent
    {
        if (!profile.TryGet<T>(out var component))
        {
            component = profile.Add<T>(false);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
        }
        component.active = true;
        return component;
    }

    public struct Look
    {
        public float threshold, bloom, scatter, clamp; public Color bloomTint; public bool highQuality;
        public float exposure, contrast, saturation; public Color filter;
        public float temperature, tint; public Vector4 lift, gamma, gain;
        public bool split; public Color splitShadows, splitHighlights; public float splitBalance;
        public float vignette; public Color vignetteColor;
        public float aberration, grain, lensFlare;
        public bool keepFlare; // Restrained keeps the accepted screen-space flare untouched.
    }

    public static readonly Look Restrained = new()
    {
        threshold = .95f, bloom = .55f, scatter = .68f, clamp = 12, bloomTint = new Color(.92f, .97f, 1), highQuality = false,
        exposure = 0, contrast = 6, saturation = 12, filter = Color.white,
        temperature = -6, tint = 2, lift = new Vector4(.97f, 1, 1.04f, 0), gamma = new Vector4(1, 1, 1, 0), gain = new Vector4(.99f, 1, 1.02f, .02f),
        vignette = .14f, vignetteColor = new Color(.05f, .15f, .35f), aberration = 0, grain = 0, lensFlare = .035f, keepFlare = true
    };

    public static void Write(VolumeProfile profile, Look look)
    {
        var bloom = Override<Bloom>(profile);
        bloom.threshold.Override(look.threshold); bloom.intensity.Override(look.bloom); bloom.scatter.Override(look.scatter);
        bloom.clamp.Override(look.clamp); bloom.tint.Override(look.bloomTint); bloom.highQualityFiltering.Override(look.highQuality);
        // Neutral keeps cool whites clean; ACES greys and darkens them.
        Override<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        var color = Override<ColorAdjustments>(profile);
        color.postExposure.Override(look.exposure); color.contrast.Override(look.contrast);
        color.saturation.Override(look.saturation); color.colorFilter.Override(look.filter);
        var white = Override<WhiteBalance>(profile);
        white.temperature.Override(look.temperature); white.tint.Override(look.tint);
        var lgg = Override<LiftGammaGain>(profile);
        lgg.lift.Override(look.lift); lgg.gamma.Override(look.gamma); lgg.gain.Override(look.gain);
        if (look.split)
        {
            var split = Override<SplitToning>(profile);
            split.shadows.Override(look.splitShadows); split.highlights.Override(look.splitHighlights); split.balance.Override(look.splitBalance);
        }
        else if (profile.TryGet<SplitToning>(out var existingSplit)) existingSplit.active = false;
        var vignette = Override<Vignette>(profile);
        vignette.intensity.Override(look.vignette); vignette.color.Override(look.vignetteColor); vignette.smoothness.Override(.45f);
        if (look.aberration > 0) Override<ChromaticAberration>(profile).intensity.Override(look.aberration);
        else if (profile.TryGet<ChromaticAberration>(out var existingAberration)) existingAberration.active = false;
        if (look.grain > 0)
        {
            var grain = Override<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(look.grain); grain.response.Override(.8f);
        }
        else if (profile.TryGet<FilmGrain>(out var existingGrain)) existingGrain.active = false;
        if (!look.keepFlare) Override<ScreenSpaceLensFlare>(profile).intensity.Override(look.lensFlare);
        if (profile.TryGet<MotionBlur>(out var blur)) blur.active = false;
        EditorUtility.SetDirty(profile);
        foreach (var component in profile.components) EditorUtility.SetDirty(component);
    }

    public static void ConfigurePostProfiles()
    {
        var restrained = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RestrainedPath)
            ?? throw new InvalidOperationException("Missing accepted atmosphere profile " + RestrainedPath);
        // Keep the accepted screen-space flare streak settings; only grading and bloom move.
        Write(restrained, Restrained);
    }

    public static void ConfigurePipeline()
    {
        // HDR grading keeps bright whites from clipping before tonemapping (PC only).
        var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var serialized = new SerializedObject(asset);
        serialized.FindProperty("m_ColorGradingMode").intValue = (int)ColorGradingMode.HighDynamicRange;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    public static string ConfigureScene(AeroWetnessProfile wetness)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "Open the Tower scene to wire the controllers.";
        var atmosphere = UnityEngine.Object.FindObjectsByType<AeroDayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (atmosphere == null) return "No day/night cycle in the Tower scene.";
        var controller = atmosphere.GetComponent<AeroWetnessController>() ?? atmosphere.gameObject.AddComponent<AeroWetnessController>();
        controller.profile = wetness; controller.dayNight = atmosphere; controller.tower = atmosphere.GetComponent<AeroTowerAtmosphere>();
        EditorUtility.SetDirty(controller);
        var volume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(v => v.isGlobal && v.gameObject.scene == scene);
        if (volume != null)
        {
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RestrainedPath);
            EditorUtility.SetDirty(volume);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Tower scene wired (wetness controller, Restrained global volume).";
    }
}
#endif
