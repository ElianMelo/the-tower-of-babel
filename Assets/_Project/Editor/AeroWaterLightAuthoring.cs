using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class AeroWaterLightAuthoring
{
    public const string RendererPath = "Assets/Settings/PC_Renderer.asset";
    public const string WaterPath = "Assets/_Project/Environment/AeroLakeWater.mat";
    public const string CubePath = "Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr";
    public const string PatternPath = "Assets/_Project/Models/Buildings/FrutigerAero/Aero_WaterCaustics.png";

    [MenuItem("Tower of Babel/Environment/Apply nearby aquatic light")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Tower scene in this project's Edit Mode.");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        var feature = renderer.rendererFeatures.OfType<AeroLightRendererFeature>().FirstOrDefault();
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance<AeroLightRendererFeature>();
            feature.name = "Aero nearby water light";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
        }
        feature.lightShader = Shader.Find("Hidden/TowerOfBabel/Nearby Water Light");
        feature.caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(PatternPath);
        feature.environment = AssetDatabase.LoadAssetAtPath<Cubemap>(CubePath);
        feature.settings = new AeroLightRendererFeature.LightSettings();
        feature.SetActive(true); feature.Create();
        // Keep URP's recovery map in step with the renderer's new feature subasset.
        var serializedRenderer = new SerializedObject(renderer);
        var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        featureMap.arraySize = renderer.rendererFeatures.Count;
        for (int i = 0; i < renderer.rendererFeatures.Count; i++)
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long id);
            featureMap.GetArrayElementAtIndex(i).longValue = id;
        }
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer); renderer.SetDirty();

        var water = UnityEngine.Object.FindFirstObjectByType<StylizedWater3.WaterObject>();
        var material = AssetDatabase.LoadAssetAtPath<Material>(WaterPath);
        if (material == null)
        {
            material = new Material(water.material) { name = "Aero lake water" };
            AssetDatabase.CreateAsset(material, WaterPath);
        }
        material.SetFloat("_ScreenSpaceReflectionsEnabled", 1);
        material.SetFloat("_EnvironmentReflectionsOn", 1); material.SetFloat("_SpecularReflectionsOn", 1);
        material.SetFloat("_ReflectionStrength", .9f); material.SetFloat("_ReflectionFresnel", 4);
        material.SetFloat("_ReflectionDistortion", .075f); material.SetFloat("_ReflectionLighting", .35f);
        material.SetFloat("_SunReflectionStrength", 3); material.SetFloat("_SunReflectionSize", .65f);
        material.SetFloat("_CausticsBrightness", 2.1f); material.SetFloat("_CausticsTiling", .32f);
        material.SetFloat("_CausticsSpeed", .075f); material.SetFloat("_CausticsChromance", .35f);
        // Calm lake edges leave room for reflected hills and sky instead of an opaque foam ring.
        material.SetFloat("_IntersectionLength", .65f); material.SetFloat("_IntersectionFalloff", .45f);
        material.SetFloat("_IntersectionClipping", .25f); material.SetFloat("_FoamBaseAmount", .08f);
        material.SetFloat("_FoamStrength", .35f);
        material.EnableKeyword("_CAUSTICS"); material.EnableKeyword("_SCREENSPACEREFLECTIONSENABLED_ON");
        water.material = material; water.meshRenderer.sharedMaterial = material;
        EditorUtility.SetDirty(material); EditorUtility.SetDirty(water); EditorUtility.SetDirty(water.meshRenderer);
        var waterFeature = renderer.rendererFeatures.OfType<StylizedWater3.StylizedWaterRenderFeature>().Single();
        waterFeature.screenSpaceReflectionSettings.allow = true;
        // No always-on 4096-pixel height field: the water package still requests it automatically for height queries.
        waterFeature.heightPrePassSettings.enable = false;
        EditorUtility.SetDirty(waterFeature);

        ConfigureLakeRegions();

        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { scene = scene.path, nearLightDistance = feature.settings.distance,
            water = WaterPath, liveReflectionCameras = 0, extraLightRenderTargets = 0 };
    }

    public static void ConfigureLakeRegions()
    {
        var terrain = UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.AeroTowerTerrain>();
        if (terrain == null) throw new InvalidOperationException("Tower landscape is required for water light footprints.");
        var root = GameObject.Find("Lake light footprints") ?? new GameObject("Lake light footprints");
        var basins = terrain.Lakes;
        while (root.transform.childCount > basins.Length)
            UnityEngine.Object.DestroyImmediate(root.transform.GetChild(root.transform.childCount-1).gameObject);
        for (int i=0;i<basins.Length;i++)
        {
            var go = i < root.transform.childCount ? root.transform.GetChild(i).gameObject : new GameObject("Lake " + (i+1));
            go.transform.SetParent(root.transform, false);
            Vector2 center = terrain.ClearingCenter + basins[i].offset;
            // Terrain's basin formula rotates XZ coordinates in the opposite sense to Unity yaw.
            go.transform.SetPositionAndRotation(new Vector3(center.x, terrain.WaterElevation, center.y), Quaternion.Euler(0, -basins[i].rotation, 0));
            var region = go.GetComponent<AeroWaterLightRegion>() ?? go.AddComponent<AeroWaterLightRegion>();
            region.radii = basins[i].radii * 1.06f;
            region.shoreFade=4; region.height=3; region.depth=5;
            EditorUtility.SetDirty(region);
        }
    }
}
