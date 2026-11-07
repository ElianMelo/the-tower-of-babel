#if UNITY_EDITOR
using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Authors the atmosphere presets (time of day x weather x season) and wires the director into the Tower scene.
/// Idempotent: an existing library keeps its tuned values unless reset. See Documentation/FrutigerAero/Lighting/ATMOSPHERE_PRESETS.md.
/// </summary>
public static class AeroAtmospherePresetAuthoring
{
    public const string LibraryPath = "Assets/_Project/Environment/Lighting/AeroAtmospherePresets.asset";
    public const string ShaderPath = "Assets/_Project/Shaders/AeroAmbientParticles.shader";
    public const string CloudyCloudsPath = "Assets/_Project/Environment/Clouds/Presets/Clear Stratocumulus Rolls.asset";
    public const string ScenePath = "Assets/_Project/Scenes/Tower.unity";

    static void Guard()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's atmosphere presets in Edit Mode.");
    }

    [MenuItem("Tower of Babel/Rendering/Apply atmosphere presets")]
    public static string Apply() => Apply(false);

    [MenuItem("Tower of Babel/Rendering/Reset atmosphere presets to defaults")]
    public static string ResetAndApply() => Apply(true);

    public static string Apply(bool resetValues)
    {
        Guard();
        var library = ConfigureLibrary(resetValues);
        string scene = ConfigureScene(library);
        AssetDatabase.SaveAssets();
        return scene;
    }

    public static AeroAtmospherePresetLibrary ConfigureLibrary(bool resetValues)
    {
        var library = AssetDatabase.LoadAssetAtPath<AeroAtmospherePresetLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<AeroAtmospherePresetLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        else if (resetValues) library.ResetToDefaults();
        EditorUtility.SetDirty(library);
        return library;
    }

    public static string ConfigureScene(AeroAtmospherePresetLibrary library)
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != ScenePath) return "Open the Tower scene to wire the atmosphere director.";
        var cycle = UnityEngine.Object.FindObjectsByType<AeroDayNightCycle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(x => x.gameObject.scene == scene);
        if (cycle == null) return "No day/night cycle in the Tower scene.";
        var director = cycle.GetComponent<AeroAtmosphereDirector>() ?? cycle.gameObject.AddComponent<AeroAtmosphereDirector>();
        director.library = library;
        director.dayNight = cycle;
        director.wetness = cycle.GetComponent<AeroWetnessController>();
        director.clouds = cycle.GetComponent<AeroVolumetricClouds>();
        director.tower = cycle.GetComponent<AeroTowerAtmosphere>();
        director.particleShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
        director.cloudyClouds = AssetDatabase.LoadAssetAtPath<AeroCloudPreset>(CloudyCloudsPath);
        var volume = UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(v => v.isGlobal && v.gameObject.scene == scene);
        director.baseVolume = volume;
        EditorUtility.SetDirty(director);
        if (volume != null)
        {
            // Bright Aero and Y2K were rejected (2026-11-07); the switcher and its looks are retired.
            foreach (var old in volume.GetComponents<MonoBehaviour>().Where(m => m != null && m.GetType().Name == "AeroPostProfileSwitcher").ToArray())
                UnityEngine.Object.DestroyImmediate(old, true);
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(volume.gameObject);
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AeroShadingAuthoring.RestrainedPath);
            EditorUtility.SetDirty(volume);
        }
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return "Tower scene wired (atmosphere director on " + cycle.name + ").";
    }
}
#endif
