using System;
using System.Linq;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Applies the white deck tile thickness to the saved tower. GlassWater tiles are left unchanged. Safe to rerun.</summary>
public static class DeckThicknessAuthoring
{
    [MenuItem("Tower of Babel/Buildings/Apply deck tile thickness")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode
            || scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved Tower scene in this project's Edit Mode.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        float target = generator.floorTileObject.transform.localScale.y * TowerFloorLayout.DeckThicknessStretch;
        int changed = 0;
        var floors = manager.CachedChunks.SelectMany(c => c.Assets).Where(a => a.AssetType.IsFloor()).Select(a =>
        {
            if (a.AssetType != TowerAssetType.Floor || Mathf.Abs(a.Scale.y - target) < .0001f) return a;
            changed++;
            return new ChunkAssetData(a.LocalIndex, a.AssetType, a.Position, a.Rotation, new Vector3(a.Scale.x, target, a.Scale.z), a.Stage);
        }).ToList();
        manager.ReplaceCachedFloors(floors);
        EditorUtility.SetDirty(manager); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        return new { changed, thickness = target };
    }
}
