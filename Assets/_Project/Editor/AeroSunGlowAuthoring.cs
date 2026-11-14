using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Applies the soft sun glow (Documentation/FrutigerAero/Lighting/SUN_GLOW.md): switches the clear sky material to the
/// soft sun shader in place (keeping its GUID and colours), adds the distant-haze pass to the PC renderer, makes the
/// sun flare's halo faint and light-coloured, and saves the glow settings on the day/night profile.
/// </summary>
public static class AeroSunGlowAuthoring
{
    public const string SkyShaderName = "Tower of Babel/Sky/Sun Glow Sky";
    public const string HazeShaderName = "Hidden/TowerOfBabel/Sun Haze";
    public const string RaysShaderName = "Hidden/TowerOfBabel/Sun Rays";

    [MenuItem("Tower of Babel/Environment/Apply soft sun glow")]
    public static string Apply()
    {
        string sky = UpgradeSky();
        string renderer = ConfigureRenderer();
        string flare = ConfigureFlare();
        string profile = SaveProfile();
        AssetDatabase.SaveAssets();
        return string.Join("\n", sky, renderer, flare, profile);
    }

    /// <summary>Switches AeroClearSky.mat to the soft sun shader; the shared property names keep its sky colours.</summary>
    public static string UpgradeSky()
    {
        var sky = AssetDatabase.LoadAssetAtPath<Material>(AeroCloudAuthoring.SkyPath);
        var shader = Shader.Find(SkyShaderName);
        if (sky == null || shader == null) return "Sky material or soft sun shader missing.";
        if (sky.shader != shader)
        {
            sky.shader = shader;
            sky.shaderKeywords = System.Array.Empty<string>();
        }
        // The Procedural sun disk settings no longer apply; drop them so the material only shows what it uses.
        var serialized = new SerializedObject(sky);
        var floats = serialized.FindProperty("m_SavedProperties.m_Floats");
        for (int i = floats.arraySize - 1; i >= 0; i--)
        {
            string name = floats.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
            if (name is "_SunDisk" or "_SunSize" or "_SunSizeConvergence") floats.DeleteArrayElementAtIndex(i);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(sky);
        return "Sky uses the soft sun.";
    }

    /// <summary>Adds the distant-haze pass to the PC renderer (not the mobile or reflection renderers).</summary>
    public static string ConfigureRenderer()
    {
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroCloudAuthoring.RendererPath);
        var feature = renderer.rendererFeatures.OfType<AeroSunHazeRendererFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<AeroSunHazeRendererFeature>();
            feature.name = "Aero sun haze";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.hazeShader = Shader.Find(HazeShaderName);
        feature.SetActive(true); feature.Create();
        // Outdoor god rays ride on the existing sun shaft feature (same graphics setting).
        var shafts = renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().FirstOrDefault();
        if (shafts != null) { shafts.raysShader = Shader.Find(RaysShaderName); shafts.Create(); EditorUtility.SetDirty(shafts); }
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
        return "Sun haze pass and outdoor god rays on the PC renderer.";
    }

    public static string ConfigureFlare()
    {
        var data = AssetDatabase.LoadAssetAtPath<LensFlareDataSRP>(AeroIslandAtmosphereAuthoring.SunFlarePath);
        var halo = AssetDatabase.LoadAssetAtPath<Texture2D>(AeroIslandAtmosphereAuthoring.SunFlareHaloPath);
        if (data == null || halo == null) return "Sun flare assets missing.";
        AeroIslandAtmosphereAuthoring.ConfigureSunFlareData(data, halo);
        return "Sun flare halo faint and light-coloured.";
    }

    /// <summary>Writes the tuned glow settings into AeroDayNight.asset.</summary>
    public static string SaveProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>(AeroIslandAtmosphereAuthoring.ProfilePath);
        if (profile == null) return "Day/night profile missing.";
        profile.sunGlow = AeroSunGlowSettings.Sun();
        profile.moonGlow = AeroSunGlowSettings.Moon();
        EditorUtility.SetDirty(profile);
        return "Glow settings saved on the day/night profile.";
    }
}
