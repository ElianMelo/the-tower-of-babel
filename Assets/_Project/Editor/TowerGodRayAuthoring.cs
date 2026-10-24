#if UNITY_EDITOR
using System.Linq;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class TowerGodRayAuthoring
{
    public const string SettingsPath = "Assets/_Project/Environment/Lighting/TowerInteriorRays.asset";
    [MenuItem("Tower/Lighting/Configure dynamic side rays")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying || EditorSceneManager.GetActiveScene().path != "Assets/_Project/Scenes/Tower.unity")
            throw new System.InvalidOperationException("Configure the Tower scene in Edit Mode.");
        var settings = AssetDatabase.LoadAssetAtPath<AeroRaySettings>(SettingsPath);
        if (settings == null) { settings = ScriptableObject.CreateInstance<AeroRaySettings>(); AssetDatabase.CreateAsset(settings, SettingsPath); }
        var atmosphere = Object.FindFirstObjectByType<AeroTowerAtmosphere>();
        var bridge = atmosphere.GetComponent<TowerAtmosphereBridge>();
        if (bridge == null) bridge = atmosphere.gameObject.AddComponent<TowerAtmosphereBridge>();
        bridge.enabled = false;
        bridge.chunks = Object.FindFirstObjectByType<ChunkManager>();
        bridge.near = Object.FindFirstObjectByType<NearTowerAssetPool>();
        bridge.atmosphere = atmosphere; bridge.enabled = true;
        atmosphere.raySettings = settings;
        if (atmosphere.beams != null) Object.DestroyImmediate(atmosphere.beams.gameObject);
        atmosphere.beams = null;
        foreach (var light in atmosphere.bounceLights)
        {
            light.name = "Anchored arch fill"; light.enabled = false;
            light.shadows = LightShadows.Soft; light.shadowCustomResolution = 512;
            var data = light.GetUniversalAdditionalLightData();
            var serialized = new SerializedObject(data);
            serialized.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(light); EditorUtility.SetDirty(data);
        }
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
        var feature = renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();
        feature.raySettings = settings; feature.settings.distance = 32; feature.settings.samples = 16;
        feature.SetActive(true); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(atmosphere); EditorUtility.SetDirty(bridge);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(atmosphere.gameObject.scene);
        EditorSceneManager.SaveScene(atmosphere.gameObject.scene);
    }
}
#endif
