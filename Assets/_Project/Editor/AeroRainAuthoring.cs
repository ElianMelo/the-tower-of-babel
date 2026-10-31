#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using FishNet.Object;
using TowerOfBabel.Networking.Weather;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Authors falling rain and synced weather: URP particle materials from the DYNAMIC Falling Rain textures,
/// the AeroRain profile with the DYNAMIC rain loop, an AeroRainController next to the wetness driver and a
/// NetworkWeatherService on the scene's network authority. Idempotent; see Documentation/FrutigerAero/Lighting/RAIN.md.
/// </summary>
public static class AeroRainAuthoring
{
    public const string ProfilePath = "Assets/_Project/Environment/Lighting/AeroRain.asset";
    public const string DropMaterialPath = "Assets/_Project/Environment/Lighting/AeroRainDrop.mat";
    public const string SplashMaterialPath = "Assets/_Project/Environment/Lighting/AeroRainSplash.mat";
    public const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
    const string DropTexture = "Assets/MoveRotateResize/DYNAMIC- Falling Rain/Rain/Textures/vxf_rain_rotated_Basecolor_01.tif";
    const string SplashTexture = "Assets/MoveRotateResize/DYNAMIC- Falling Rain/Rain/Textures/rain_splash_02.tif";
    const string RainLoop = "Assets/MoveRotateResize/DYNAMIC- RainWetness/Audio/audio_rain_main.wav";
    const string ParticleShader = "Universal Render Pipeline/Particles/Unlit";

    [MenuItem("Tools/Tower of Babel/Author Rain and Weather")]
    public static string Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's rain in Edit Mode.");
        var profile = CreateProfile();
        return "Rain profile ready. " + ConfigureScene(profile);
    }

    public static AeroRainProfile CreateProfile()
    {
        var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(ProfilePath);
        if (profile == null) { profile = ScriptableObject.CreateInstance<AeroRainProfile>(); AssetDatabase.CreateAsset(profile, ProfilePath); }
        profile.dropMaterial = ParticleMaterial(DropMaterialPath, DropTexture);
        // The streak fills only the middle ~12% of the package texture (x .443-.559); crop to it so a
        // particle's width is the drop's width (otherwise heavy rain renders as millimetre slivers).
        profile.dropMaterial.SetTextureScale("_BaseMap", new Vector2(.14f, .95f));
        profile.dropMaterial.SetTextureOffset("_BaseMap", new Vector2(.43f, .03f));
        profile.splashMaterial = ParticleMaterial(SplashMaterialPath, SplashTexture);
        profile.rainLoop = AssetDatabase.LoadAssetAtPath<AudioClip>(RainLoop);
        HeavyRain(profile);
        profile.collidesWith = ~(LayerMask.GetMask("Ignore Raycast", "UI", "Water", "TransparentFX"));
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>Heavy rain preset (user feedback 2026-10-31: the first pass was too subtle).</summary>
    public static void HeavyRain(AeroRainProfile p)
    {
        var d = ScriptableObject.CreateInstance<AeroRainProfile>();
        try
        {
            p.dropsPerSecond = d.dropsPerSecond; p.maxDrops = d.maxDrops; p.maxSplashes = d.maxSplashes; p.areaRadius = d.areaRadius;
            p.dropSpeed = d.dropSpeed; p.dropWidth = d.dropWidth; p.dropLength = d.dropLength;
            p.splashChance = d.splashChance; p.splashSize = d.splashSize;
            p.dayTint = d.dayTint; p.nightTint = d.nightTint; p.splashDayTint = d.splashDayTint; p.splashNightTint = d.splashNightTint;
            p.outsideVolume = d.outsideVolume; p.shelteredVolume = d.shelteredVolume;
        }
        finally { UnityEngine.Object.DestroyImmediate(d); }
    }

    static Material ParticleMaterial(string path, string texturePath)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find(ParticleShader)); AssetDatabase.CreateAsset(material, path); }
        material.shader = Shader.Find(ParticleShader);
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        material.SetTexture("_BaseMap", texture);
        material.SetColor("_BaseColor", Color.white);
        // Transparent, alpha blended, both sides, soft against geometry, no depth write.
        material.SetFloat("_Surface", 1); material.SetFloat("_Blend", 0); material.SetFloat("_Cull", 0);
        material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0); material.SetFloat("_ColorMode", 0);
        material.SetFloat("_SoftParticlesEnabled", 1); material.SetVector("_SoftParticleFadeParams", new Vector4(0, 1.5f, 0, 0));
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.EnableKeyword("_SOFTPARTICLES_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON"); material.DisableKeyword("_ALPHAMODULATE_ON");
        material.SetOverrideTag("RenderType", "Transparent");
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static string ConfigureScene(AeroRainProfile profile)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "Open the Tower scene to wire rain and weather.";
        var atmosphere = UnityEngine.Object.FindObjectsByType<AeroDayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (atmosphere == null) return "No day/night cycle in the Tower scene.";
        var wetness = atmosphere.GetComponent<AeroWetnessController>();
        var rain = atmosphere.GetComponent<AeroRainController>() ?? atmosphere.gameObject.AddComponent<AeroRainController>();
        rain.profile = profile; rain.wetness = wetness; rain.dayNight = atmosphere; rain.tower = atmosphere.GetComponent<AeroTowerAtmosphere>();
        EditorUtility.SetDirty(rain);

        var authority = UnityEngine.Object.FindObjectsByType<NetworkObject>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(n => n.gameObject.scene == scene && n.GetComponent<TowerOfBabel.Networking.Upgrades.NetworkUpgradeService>() != null);
        if (authority == null) return "Rain wired; no network authority object found for the weather service.";
        var weather = authority.GetComponent<NetworkWeatherService>() ?? authority.gameObject.AddComponent<NetworkWeatherService>();
        var serialized = new SerializedObject(weather);
        serialized.FindProperty("dayNight").objectReferenceValue = atmosphere;
        serialized.FindProperty("wetness").objectReferenceValue = wetness;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weather);
        typeof(NetworkObject).GetMethod("ReserializeEditorSetValues", BindingFlags.Instance | BindingFlags.NonPublic)?
            .Invoke(authority, new object[] { true, false });
        EditorUtility.SetDirty(authority);

        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Tower scene wired (rain controller, network weather service).";
    }
}
#endif
