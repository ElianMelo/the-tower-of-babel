#if UNITY_EDITOR
using System;
using System.Linq;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class RiverGlassAuthoring
{
    public const string ProfilePath = "Assets/_Project/Environment/Lighting/RiverGlass.asset";
    public const string GlassPath = "Assets/_Project/Environment/Lighting/GlassWaterFloor.mat";
    public static RiverGlassProfile ConfigureAssets()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's river in Edit Mode.");
        var profile = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<RiverGlassProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        profile.caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(AeroIslandAtmosphereAuthoring.CausticPath);
        profile.ripples = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Stylized Water 3/Materials/Textures/Normals/SmoothWaves.png");
        profile.environment = glass.GetTexture("_Environment") as Cubemap;
        profile.layout = glass.GetVector("_RiverLayout");
        glass.SetFloat("_RiverStage", 10); glass.SetFloat("_RiverFloorIndex", -1); glass.enableInstancing = true;
        EditorUtility.SetDirty(glass); EditorUtility.SetDirty(profile);
        foreach (var name in new[] { "PC_Renderer", "Mobile_Renderer" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{name}.asset");
            var feature = renderer.rendererFeatures.OfType<RiverGlassRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RiverGlassRendererFeature>();
                feature.name = "Transparent river glass pools";
                AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
            }
            feature.profile = profile; feature.surfaceShader = Shader.Find("TowerOfBabel/Buildings/Transparent river glass");
            feature.reducedRefraction = name == "Mobile_Renderer"; feature.Create();
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
        return profile;
    }

    public static void ConfigureScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity") return;
        var manager = UnityEngine.Object.FindObjectsByType<ChunkManager>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (manager == null) return;
        var light = manager.GetComponent<RiverGlassLighting>() ?? manager.gameObject.AddComponent<RiverGlassLighting>();
        light.profile = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>(ProfilePath);
        light.cookieShader = Shader.Find("Hidden/TowerOfBabel/River caustic cookie");
        light.enabled = false; light.enabled = true;
        EditorUtility.SetDirty(light); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tower of Babel/Environment/Apply transparent river glass pools")]
    public static string Apply()
    {
        ConfigureAssets(); ConfigureScene();
        return "Transparent river, shared profile, PC/mobile composition and pooled shadowed water lights configured.";
    }
}
#endif
