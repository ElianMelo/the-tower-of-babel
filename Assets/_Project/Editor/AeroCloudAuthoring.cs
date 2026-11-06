#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Authors the volumetric clouds: bakes the tiling noise, creates the preset / tier / library / quality assets,
/// adds the renderer feature to PC_Renderer, gives the Tower scene a clean procedural sky and puts an
/// AeroVolumetricClouds next to the day/night cycle. Existing presets are kept (they are for hand tuning);
/// use "Reset cloud presets" to restore the shipped values. See Documentation/FrutigerAero/Lighting/VOLUMETRIC_CLOUDS.md.
/// </summary>
public static class AeroCloudAuthoring
{
    public const string Root = "Assets/_Project/Environment/Clouds";
    public const string PresetFolder = Root + "/Presets";
    public const string TierFolder = Root + "/Tiers";
    public const string NoiseFolder = Root + "/Noise";
    public const string LibraryPath = Root + "/AeroCloudWeather.asset";
    public const string QualityPath = Root + "/AeroCloudQuality.asset";
    public const string ShapePath = NoiseFolder + "/AeroCloudShape128.asset";
    public const string DetailPath = NoiseFolder + "/AeroCloudDetail32.asset";
    public const string CoveragePath = NoiseFolder + "/AeroCloudCoverage256.asset";
    public const string SkyPath = Root + "/AeroClearSky.mat";
    public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    public const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
    public const string ShaderName = "Hidden/TowerOfBabel/Volumetric Clouds";
    const int NoiseSeed = 20261105;

    [MenuItem("Tower of Babel/Environment/Clouds/Author volumetric clouds")]
    public static string Apply()
    {
        Guard();
        var library = CreateAssets(false);
        string feature = ConfigureRenderer();
        return $"{feature} {ConfigureScene(library)}";
    }

    [MenuItem("Tower of Babel/Environment/Clouds/Reset cloud presets to shipped values")]
    public static string ResetPresets()
    {
        Guard();
        CreateAssets(true);
        return "Cloud presets, tiers and library reset.";
    }

    [MenuItem("Tower of Babel/Environment/Clouds/Rebake cloud noise")]
    public static string RebakeNoise()
    {
        Guard();
        BakeNoise(true);
        return "Cloud noise rebaked.";
    }

    static void Guard()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's clouds in Edit Mode.");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
    }

    public static AeroCloudWeatherLibrary CreateAssets(bool reset)
    {
        EnsureFolder(PresetFolder); EnsureFolder(TierFolder); EnsureFolder(NoiseFolder);
        BakeNoise(false);
        var quality = LoadOrCreate<AeroCloudQuality>(QualityPath, out bool newQuality);
        if (newQuality || reset) { var d = ScriptableObject.CreateInstance<AeroCloudQuality>(); EditorUtility.CopySerialized(d, quality); UnityEngine.Object.DestroyImmediate(d); quality.name = Path.GetFileNameWithoutExtension(QualityPath); }
        EditorUtility.SetDirty(quality);

        var presets = new Dictionary<string, (AeroCloudPreset preset, float weight)>();
        foreach (var def in PresetDefinitions())
        {
            var preset = LoadOrCreate<AeroCloudPreset>($"{PresetFolder}/{def.name}.asset", out bool created);
            if (created || reset)
            {
                preset.weather = def.weather; preset.notes = def.notes;
                var look = AeroCloudLook.Default; def.edit(ref look); preset.look = look.Sanitized();
                EditorUtility.SetDirty(preset);
            }
            presets[def.name] = (preset, def.weight);
        }

        var amounts = new List<AeroCloudAmountTier>();
        foreach (var (name, weight, coverage, size) in new[]
                 {
                     ("Amount Small", 1f, new Vector2(.14f, .26f), new Vector2(.65f, .85f)),
                     ("Amount Medium", 1.4f, new Vector2(.3f, .42f), new Vector2(.9f, 1.1f)),
                     ("Amount Large", 1f, new Vector2(.46f, .62f), new Vector2(1.15f, 1.45f)),
                 })
        {
            var tier = LoadOrCreate<AeroCloudAmountTier>($"{TierFolder}/{name}.asset", out bool created);
            if (created || reset) { tier.weight = weight; tier.coverage = coverage; tier.sizeMultiplier = size; EditorUtility.SetDirty(tier); }
            amounts.Add(tier);
        }
        var densities = new List<AeroCloudDensityTier>();
        foreach (var (name, weight, density, erosion, softness, ambient) in new[]
                 {
                     ("Density Soft", 1.2f, new Vector2(.35f, .55f), new Vector2(.1f, .25f), new Vector2(.1f, .2f), new Vector2(1.1f, 1.2f)),
                     ("Density Medium", 1.4f, new Vector2(.65f, .85f), new Vector2(-.05f, .05f), new Vector2(-.03f, .03f), new Vector2(1, 1)),
                     ("Density Dense", .9f, new Vector2(.95f, 1.2f), new Vector2(-.2f, -.08f), new Vector2(-.08f, -.02f), new Vector2(.85f, .95f)),
                 })
        {
            var tier = LoadOrCreate<AeroCloudDensityTier>($"{TierFolder}/{name}.asset", out bool created);
            if (created || reset)
            {
                tier.weight = weight; tier.densityMultiplier = density; tier.erosionOffset = erosion;
                tier.softnessOffset = softness; tier.ambientMultiplier = ambient; EditorUtility.SetDirty(tier);
            }
            densities.Add(tier);
        }

        var library = LoadOrCreate<AeroCloudWeatherLibrary>(LibraryPath, out bool newLibrary);
        if (newLibrary || reset)
        {
            var d = ScriptableObject.CreateInstance<AeroCloudWeatherLibrary>();
            EditorUtility.CopySerialized(d, library); UnityEngine.Object.DestroyImmediate(d);
            library.name = Path.GetFileNameWithoutExtension(LibraryPath);
            library.clearDay = presets.Values.Where(p => p.preset.weather == AeroCloudWeather.ClearDay)
                .Select(p => new AeroCloudWeatherLibrary.Entry { preset = p.preset, weight = p.weight }).ToList();
            library.rain = presets.Values.Where(p => p.preset.weather != AeroCloudWeather.ClearDay)
                .Select(p => new AeroCloudWeatherLibrary.Entry { preset = p.preset, weight = p.weight }).ToList();
            library.amountTiers = amounts; library.densityTiers = densities;
            EditorUtility.SetDirty(library);
        }
        CreateSky();
        AssetDatabase.SaveAssets();
        return library;
    }

    static T LoadOrCreate<T>(string path, out bool created) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        created = asset == null;
        if (created) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }

    public static void BakeNoise(bool force)
    {
        EnsureFolder(NoiseFolder);
        if (force || AssetDatabase.LoadAssetAtPath<Texture3D>(ShapePath) == null)
            Save(AeroCloudNoise.CreateTexture3D("AeroCloudShape128", AeroCloudNoise.DefaultShapeSize, AeroCloudNoise.Shape(AeroCloudNoise.DefaultShapeSize, NoiseSeed)), ShapePath);
        if (force || AssetDatabase.LoadAssetAtPath<Texture3D>(DetailPath) == null)
            Save(AeroCloudNoise.CreateTexture3D("AeroCloudDetail32", AeroCloudNoise.DefaultDetailSize, AeroCloudNoise.Detail(AeroCloudNoise.DefaultDetailSize, NoiseSeed + 1)), DetailPath);
        if (force || AssetDatabase.LoadAssetAtPath<Texture2D>(CoveragePath) == null)
            Save(AeroCloudNoise.CreateCoverageTexture("AeroCloudCoverage256", AeroCloudNoise.DefaultCoverageSize, AeroCloudNoise.Coverage(AeroCloudNoise.DefaultCoverageSize, NoiseSeed + 2)), CoveragePath);
    }

    static void Save(Texture texture, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Texture>(path);
        if (existing != null && existing.GetType() == texture.GetType()) { EditorUtility.CopySerialized(texture, existing); UnityEngine.Object.DestroyImmediate(texture); EditorUtility.SetDirty(existing); }
        else AssetDatabase.CreateAsset(texture, path);
    }

    static Material CreateSky()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        if (sky != null) return sky;
        sky = new Material(Shader.Find("Skybox/Procedural")) { name = "AeroClearSky" };
        // A clean, saturated Frutiger blue with a soft sun; the clouds are drawn on top by the renderer feature.
        sky.SetFloat("_SunDisk", 2); sky.SetFloat("_SunSize", .035f); sky.SetFloat("_SunSizeConvergence", 6);
        sky.SetFloat("_AtmosphereThickness", .78f);
        sky.SetColor("_SkyTint", new Color(.38f, .56f, .86f));
        sky.SetColor("_GroundColor", new Color(.33f, .42f, .52f));
        sky.SetFloat("_Exposure", 1.15f);
        AssetDatabase.CreateAsset(sky, SkyPath);
        return sky;
    }

    public static string ConfigureRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var feature = renderer.rendererFeatures.OfType<AeroVolumetricCloudsRendererFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<AeroVolumetricCloudsRendererFeature>();
            feature.name = "Aero volumetric clouds";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.cloudShader = Shader.Find(ShaderName);
        feature.quality = AssetDatabase.LoadAssetAtPath<AeroCloudQuality>(QualityPath);
        feature.shapeNoise = AssetDatabase.LoadAssetAtPath<Texture3D>(ShapePath);
        feature.detailNoise = AssetDatabase.LoadAssetAtPath<Texture3D>(DetailPath);
        feature.coverageMap = AssetDatabase.LoadAssetAtPath<Texture2D>(CoveragePath);
        feature.SetActive(true); feature.Create();
        var serialized = new SerializedObject(renderer);
        var map = serialized.FindProperty("m_RendererFeatureMap");
        map.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long id);
            map.GetArrayElementAtIndex(i).longValue = id;
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer); renderer.SetDirty();
        AssetDatabase.SaveAssets();
        return "Renderer feature ready.";
    }

    public static string ConfigureScene(AeroCloudWeatherLibrary library)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "Open the Tower scene to wire the clouds.";
        var atmosphere = UnityEngine.Object.FindObjectsByType<AeroDayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (atmosphere == null) return "No day/night cycle in the Tower scene.";
        var clouds = atmosphere.GetComponent<AeroVolumetricClouds>() ?? atmosphere.gameObject.AddComponent<AeroVolumetricClouds>();
        clouds.library = library; clouds.dayNight = atmosphere; clouds.wetness = atmosphere.GetComponent<AeroWetnessController>();
        EditorUtility.SetDirty(clouds);
        RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Tower scene wired (clouds on the day/night object, clear procedural sky).";
    }

    public struct PresetDefinition
    {
        public string name, notes;
        public AeroCloudWeather weather;
        public float weight;
        public Edit edit;
    }

    public delegate void Edit(ref AeroCloudLook l);

    static PresetDefinition P(string name, AeroCloudWeather weather, float weight, string notes, Edit edit)
        => new() { name = name, weather = weather, weight = weight, notes = notes, edit = edit };

    /// <summary>The shipped presets. Clear-day coverage/size/density are further varied by the tiers.</summary>
    public static IEnumerable<PresetDefinition> PresetDefinitions()
    {
        yield return P("Clear Fair Weather Cumulus", AeroCloudWeather.ClearDay, 1.6f, "Classic sunny-day cumulus: flat bases, domed tops.",
            (ref AeroCloudLook l) => { l.cloudSize = 0.75f; l.heightVariation = 220; l.baseAltitude = 150; l.coverage = .38f; });
        yield return P("Clear Cotton Puffs", AeroCloudWeather.ClearDay, 1.2f, "Small popcorn cumulus, crisp and bright.",
            (ref AeroCloudLook l) => { l.heightVariation = 180; l.coverage = .32f; l.density = .9f; l.cloudSize = .55f; l.cloudType = .42f; l.erosion = .45f; l.edgeSoftness = .3f; l.baseAltitude = 140; l.thickness = 260; });
        yield return P("Clear Towering Cumulus", AeroCloudWeather.ClearDay, .7f, "Tall congestus towers with deep shading.",
            (ref AeroCloudLook l) => { l.heightVariation = 250; l.coverage = .3f; l.density = 1.15f; l.cloudSize = 1.1f; l.cloudType = .78f; l.baseAltitude = 160; l.thickness = 1100; l.shear = 300; l.powder = .6f; l.sunOcclusion = .12f; });
        // Review 2026-11-06: the old fleece (thin torn sheet, erosion .7) read as floating lines. Altocumulus is a
        // high field of small, rounded cloudlets: billowy (low erosion), a little thicker, little shear.
        yield return P("Clear Altocumulus Fleece", AeroCloudWeather.ClearDay, .8f, "Altocumulus: a high field of small, rounded cloudlets with shaded undersides, like fleece.",
            (ref AeroCloudLook l) => { l.heightVariation = 40; l.coverage = .45f; l.density = .85f; l.cloudSize = .3f; l.cloudType = .24f; l.erosion = .35f; l.edgeSoftness = .25f; l.baseAltitude = 900; l.thickness = 300; l.windSpeed = 12; l.evolution = 1.2f; l.shear = 25; l.sunLight = 1.05f; l.ambientLight = 1.15f; l.litTint = new Color(1, .98f, .95f); l.shadowTint = new Color(.72f, .84f, 1); l.powder = .4f; l.silverLining = .7f; l.multiScatter = .55f; l.hazeDistance = 11000; l.hazeStrength = .55f; l.fadeDistance = 18000; l.sunOcclusion = .04f; });
        yield return P("Clear Stratocumulus Rolls", AeroCloudWeather.ClearDay, .7f, "Broad, low rolls with blue gaps.",
            (ref AeroCloudLook l) => { l.heightVariation = 120; l.coverage = .6f; l.density = .75f; l.cloudSize = 1.3f; l.cloudType = .3f; l.erosion = .4f; l.edgeSoftness = .35f; l.baseAltitude = 120; l.thickness = 300; l.sunOcclusion = .12f; });
        // Review 2026-11-06: crisper billowy tops (more erosion, firmer edges) and sky-blue shaded bellies.
        yield return P("Clear Frutiger Dream", AeroCloudWeather.ClearDay, 1.4f, "Bright Frutiger Aero cumulus: crisp billowy tops, sky-blue shaded bellies.",
            (ref AeroCloudLook l) => { l.heightVariation = 220; l.baseAltitude = 150; l.coverage = .35f; l.density = .85f; l.cloudSize = 0.9f; l.cloudType = .52f; l.erosion = .5f; l.edgeSoftness = .28f; l.litTint = new Color(1, .97f, .92f); l.shadowTint = new Color(.66f, .8f, 1); l.ambientLight = 1.2f; l.sunLight = 1.1f; l.silverLining = .75f; l.powder = .45f; l.hazeStrength = .35f; });
        yield return P("Clear Thin High Veil", AeroCloudWeather.ClearDay, .5f, "Faint high veil; soft light, almost no shade.",
            (ref AeroCloudLook l) => { l.heightVariation = 350; l.coverage = .55f; l.density = .8f; l.cloudSize = 2.4f; l.cloudType = .05f; l.erosion = .7f; l.edgeSoftness = .8f; l.baseAltitude = 900; l.thickness = 250; l.ambientLight = 1.3f; l.powder = 0; l.sunOcclusion = .04f; });
        yield return P("Clear Scattered Wisps", AeroCloudWeather.ClearDay, .9f, "Sparse, torn wisps drifting quickly.",
            (ref AeroCloudLook l) => { l.heightVariation = 280; l.coverage = .22f; l.density = .9f; l.cloudSize = .8f; l.cloudType = .35f; l.erosion = .8f; l.edgeSoftness = .45f; l.baseAltitude = 170; l.thickness = 300; l.windSpeed = 12; l.sunOcclusion = .03f; });
        yield return P("Clear Big Sky Banks", AeroCloudWeather.ClearDay, .8f, "Large cumulus banks with wide open sky between.",
            (ref AeroCloudLook l) => { l.heightVariation = 180; l.coverage = .33f; l.cloudSize = 1.8f; l.cloudType = .55f; l.baseAltitude = 140; l.thickness = 600; });
        yield return P("Clear Golden Afternoon", AeroCloudWeather.ClearDay, .8f, "Warm-lit cumulus with lavender shadows.",
            (ref AeroCloudLook l) => { l.cloudSize = 0.8f; l.heightVariation = 220; l.baseAltitude = 150; l.coverage = .4f; l.density = .75f; l.litTint = new Color(1, .93f, .82f); l.shadowTint = new Color(.78f, .8f, .95f); });

        yield return P("Rain Heavy Nimbostratus", AeroCloudWeather.Rain, 3, "Low, thick, dark grey ceiling for heavy rain.",
            (ref AeroCloudLook l) => { l.heightVariation = 50; l.coverage = .97f; l.density = 2.8f; l.cloudSize = 1.6f; l.cloudType = .35f; l.erosion = .25f; l.edgeSoftness = .5f; l.baseAltitude = 100; l.thickness = 900; l.windSpeed = 14; l.evolution = 3; l.sunLight = .55f; l.ambientLight = .55f; l.litTint = new Color(.78f, .8f, .85f); l.shadowTint = new Color(.42f, .46f, .55f); l.darkness = .85f; l.powder = .7f; l.silverLining = .35f; l.multiScatter = .35f; l.hazeDistance = 9000; l.hazeStrength = .8f; l.fadeDistance = 22000; l.sunOcclusion = .35f; });
        yield return P("Rain Thunderstorm Anvil", AeroCloudWeather.Storm, 1.5f, "Towering cumulonimbus with near-black bases.",
            (ref AeroCloudLook l) => { l.heightVariation = 80; l.coverage = .85f; l.density = 3; l.cloudSize = 2; l.cloudType = 1; l.erosion = .35f; l.edgeSoftness = .35f; l.baseAltitude = 115; l.thickness = 2600; l.windSpeed = 18; l.evolution = 4; l.shear = 900; l.sunLight = .7f; l.ambientLight = .45f; l.litTint = new Color(.9f, .9f, .95f); l.shadowTint = new Color(.36f, .38f, .48f); l.darkness = 1; l.powder = .8f; l.silverLining = .4f; l.multiScatter = .3f; l.hazeDistance = 10000; l.hazeStrength = .75f; l.fadeDistance = 26000; l.sunOcclusion = .45f; });
        yield return P("Rain Dark Squall", AeroCloudWeather.Storm, 1.5f, "Fast, ragged, dark squall line.",
            (ref AeroCloudLook l) => { l.heightVariation = 120; l.coverage = .92f; l.density = 2.9f; l.cloudSize = 1.1f; l.cloudType = .6f; l.erosion = .5f; l.edgeSoftness = .3f; l.baseAltitude = 105; l.thickness = 1100; l.windSpeed = 22; l.evolution = 5; l.shear = 500; l.sunLight = .5f; l.ambientLight = .5f; l.litTint = new Color(.8f, .82f, .86f); l.shadowTint = new Color(.38f, .42f, .5f); l.darkness = .95f; l.powder = .75f; l.silverLining = .3f; l.multiScatter = .3f; l.hazeDistance = 8000; l.hazeStrength = .85f; l.fadeDistance = 20000; l.sunOcclusion = .4f; });
        yield return P("Rain Grey Drizzle Overcast", AeroCloudWeather.Overcast, 1, "Even grey overcast for lighter, steady rain.",
            (ref AeroCloudLook l) => { l.heightVariation = 40; l.coverage = .9f; l.density = 2; l.cloudSize = 1.3f; l.cloudType = .15f; l.erosion = .4f; l.edgeSoftness = .6f; l.baseAltitude = 110; l.thickness = 550; l.windSpeed = 10; l.sunLight = .7f; l.ambientLight = .75f; l.litTint = new Color(.88f, .9f, .92f); l.shadowTint = new Color(.6f, .64f, .72f); l.darkness = .55f; l.powder = .5f; l.silverLining = .45f; l.hazeDistance = 10000; l.hazeStrength = .75f; l.fadeDistance = 24000; l.sunOcclusion = .25f; });
    }
}
#endif
