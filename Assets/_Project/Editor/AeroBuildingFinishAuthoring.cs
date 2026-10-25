#if UNITY_EDITOR
using System;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class AeroBuildingFinishAuthoring
{
    public const string ProfilePath = "Assets/_Project/Environment/Lighting/BuildingFinish.asset";
    public const string RendererPath = "Assets/Settings/AeroReflection_Renderer.asset";

    public static object Configure()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Configure this project's building finish in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the clean Tower scene before authoring.");
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
        if (renderer == null)
        {
            renderer = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset"));
            renderer.rendererFeatures.Clear();
            renderer.renderingMode = RenderingMode.Forward;
            renderer.transparentLayerMask = 0;
            renderer.opaqueLayerMask = ~(1 << 5);
            renderer.name = "Aero opaque reflection";
            var copy = new SerializedObject(renderer); copy.FindProperty("m_RendererFeatureMap").ClearArray(); copy.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.CreateAsset(renderer, RendererPath);
            renderer.SetDirty(); EditorUtility.SetDirty(renderer);
        }
        var profile = AssetDatabase.LoadAssetAtPath<AeroBuildingFinishProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<AeroBuildingFinishProfile>();
            var river = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>(RiverGlassAuthoring.ProfilePath);
            profile.dayEnvironment = river.environment; profile.nightEnvironment = river.environment;
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
        var serialized = new SerializedObject(pipeline);
        var list = serialized.FindProperty("m_RendererDataList");
        int index = -1;
        for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == renderer) index = i;
        if (index < 0) { index = list.arraySize; list.InsertArrayElementAtIndex(index); list.GetArrayElementAtIndex(index).objectReferenceValue = renderer; }
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline);
        profile.reflectionRendererIndex = index; EditorUtility.SetDirty(profile);
        profile.livePipeline = pipeline;
        var atmosphere = UnityEngine.Object.FindFirstObjectByType<AeroTowerAtmosphere>();
        var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        if (atmosphere == null || chunks == null) throw new InvalidOperationException("Tower atmosphere and chunk data required.");
        var finish = atmosphere.GetComponent<AeroBuildingFinish>();
        if (finish == null) finish = Undo.AddComponent<AeroBuildingFinish>(atmosphere.gameObject);
        finish.profile = profile;
        var bridge = chunks.GetComponent<TowerBuildingFinishBridge>();
        if (bridge == null) bridge = Undo.AddComponent<TowerBuildingFinishBridge>(chunks.gameObject);
        bridge.chunks = chunks;
        EditorUtility.SetDirty(finish); EditorUtility.SetDirty(bridge);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { profile = ProfilePath, renderer = RendererPath, rendererIndex = index, scene = scene.path };
    }
}
#endif
