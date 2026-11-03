using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sun shadows used to stop at 50 m, so the tower's arches and pillars lost their shadows a short way off.
/// The PC pipeline now keeps them to 120 m (150 m with four cascades cost ~5 ms of GPU per frame in the tower-facing
/// benchmark), with three cascades split so the near shadows stay as sharp as before.
/// Performance pass (2026-11-03): a 2048 sun shadow map, and a 1024 atlas for spot/point light shadows; every shadowed
/// local light uses the 256 px low tier (lava lamp, aqua lamp, river patches near the camera, arch fill lights).
/// </summary>
public static class ShadowDistanceAuthoring
{
    public const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
    public const float ShadowDistance = 120;
    public const int Cascades = 3;
    public const int ShadowResolution = 2048;
    public const int AdditionalShadowAtlas = 1024;
    /// <summary>Cascade ends as fractions of the distance: 8.4 m, 30 m, 120 m.</summary>
    public static readonly Vector2 CascadeSplits = new(.07f, .25f);

    [MenuItem("Tower of Babel/Environment/Apply shadow distance")]
    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in this project's Editor, in Edit Mode.");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        pipeline.shadowDistance = ShadowDistance;
        pipeline.shadowCascadeCount = Cascades;
        pipeline.cascade3Split = CascadeSplits;
        pipeline.mainLightShadowmapResolution = ShadowResolution;
        pipeline.additionalLightsShadowmapResolution = AdditionalShadowAtlas;
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        return new { pipeline.shadowDistance, pipeline.shadowCascadeCount, pipeline.cascade3Split, pipeline.mainLightShadowmapResolution, pipeline.additionalLightsShadowmapResolution };
    }
}
