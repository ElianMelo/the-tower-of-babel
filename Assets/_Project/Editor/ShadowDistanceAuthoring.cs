using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Sun shadows used to stop at 50 m, so the tower's arches and pillars lost their shadows a short way off.
/// The PC pipeline now keeps them to 150 m, with cascades re-split so the near shadows stay as sharp as before
/// and a 4096 shadow map so the far cascades are not blocky.
/// </summary>
public static class ShadowDistanceAuthoring
{
    public const string PipelinePath = "Assets/Settings/PC_RPAsset.asset";
    public const float ShadowDistance = 150;
    public const int ShadowResolution = 4096;
    /// <summary>Cascade ends as fractions of the distance: 9 m, 27 m, 66 m, 150 m.</summary>
    public static readonly Vector3 CascadeSplits = new(.06f, .18f, .44f);

    [MenuItem("Tower of Babel/Environment/Apply shadow distance")]
    public static object Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run in this project's Editor, in Edit Mode.");
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        pipeline.shadowDistance = ShadowDistance;
        pipeline.shadowCascadeCount = 4;
        pipeline.cascade4Split = CascadeSplits;
        pipeline.mainLightShadowmapResolution = ShadowResolution;
        EditorUtility.SetDirty(pipeline);
        AssetDatabase.SaveAssets();
        return new { pipeline.shadowDistance, pipeline.shadowCascadeCount, pipeline.cascade4Split, pipeline.mainLightShadowmapResolution };
    }
}
