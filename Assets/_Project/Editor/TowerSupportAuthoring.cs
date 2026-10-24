using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerSupportAuthoring
{
    [MenuItem("Tower of Babel/Buildings/Repair floor supports and ground bushes")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode
            || scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved Tower scene in this project's Edit Mode.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var all = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
        var floors = all.Where(a => a.AssetType.IsFloor()).GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / generator.heighIncrease));
        var pillars = all.Where(a => a.AssetType == TowerAssetType.Pillar).GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / generator.heighIncrease)).ToDictionary(g => g.Key, g => g.ToArray());
        float meshTop = generator.floorTileObject.GetComponent<Building>().GetStageMesh(10).bounds.max.y;
        float footprint = generator.PillarSupportRadius - .05f;
        int extended = 0, pads = 0, bridges = 0, removedSupports = 0, unsupportedBefore = 0, unsupportedAfter = 0;
        var repaired = new List<ChunkAssetData>();
        foreach (var group in floors)
        {
            float wall = generator.GetWallRadius(group.Key), previousWall = generator.GetWallRadius(Mathf.Max(0, group.Key-1));
            var oldSupports = group.Where(a=>TowerFloorSupport.IsPillarSupport(a,generator.center,generator.PillarSupportRadius,wall,previousWall)).ToArray();
            removedSupports += oldSupports.Length;
            var list = group.Except(oldSupports).ToList(); var before = new TowerFloorSupport(list, generator.center, meshTop);
            if (!pillars.TryGetValue(group.Key, out var levelPillars)) levelPillars = Array.Empty<ChunkAssetData>();
            foreach (var pillar in levelPillars) if (!before.SupportsDisk(new Vector2(pillar.Position.x, pillar.Position.z), footprint, out _)) unsupportedBefore++;
            float oldRadius = list.Max(a => Vector2.Distance(new Vector2(a.Position.x, a.Position.z), generator.center));
            float targetRadius = Mathf.Max(oldRadius, generator.GetFloorOuterRadius(group.Key));
            float extension = targetRadius - oldRadius;
            for (int i = 0; i < list.Count && extension > .0001f; i++)
            {
                var asset = list[i]; var delta = new Vector2(asset.Position.x, asset.Position.z) - generator.center;
                if (Mathf.Abs(delta.magnitude - oldRadius) > .002f) continue;
                var p = generator.center + delta.normalized * targetRadius;
                var scale = asset.Scale; scale.x += extension / TowerFloorLayout.TemplateSize; scale.z *= targetRadius / oldRadius;
                list[i] = new ChunkAssetData(asset.LocalIndex, asset.AssetType, new Vector3(p.x, asset.Position.y, p.y), asset.Rotation, scale, asset.Stage); extended++;
            }
            var support = new TowerFloorSupport(list, generator.center, meshTop);
            byte padStage = group.All(a => a.Stage == 10) ? (byte)10 : (byte)0;
            foreach (var pillar in levelPillars)
            {
                var point = new Vector2(pillar.Position.x, pillar.Position.z);
                if (support.SupportsDisk(point, footprint, out _)) continue;
                var sector = TowerFloorLayout.SupportPad(generator.center, list[0].Position.y, point, generator.PillarSupportRadius, list[0].Scale.y);
                if (wall < previousWall - .01f)
                {
                    if (!support.TryFindInnerConnection(point,generator.PillarSupportRadius,out float inner))
                        throw new InvalidOperationException($"No inward deck connection on floor {group.Key}, pillar {point}.");
                    sector=TowerFloorLayout.SupportBridge(generator.center,list[0].Position.y,point,generator.PillarSupportRadius,inner,list[0].Scale.y);
                    bridges++;
                }
                byte stage=padStage;
                foreach(var old in oldSupports) if(Vector3.Distance(old.Position,sector.Position)<.01f) {stage=old.Stage;break;}
                var pad = new ChunkAssetData(0, TowerAssetType.Floor, sector.Position, sector.Rotation, sector.Scale, stage);
                list.Add(pad); support.Add(pad); pads++;
            }
            foreach (var pillar in levelPillars) if (!support.SupportsDisk(new Vector2(pillar.Position.x, pillar.Position.z), footprint, out _)) unsupportedAfter++;
            repaired.AddRange(list);
        }
        if (unsupportedAfter > 0) throw new InvalidOperationException($"{unsupportedAfter} pillars still lack floor support; scene data was not changed.");
        manager.ReplaceCachedFloors(repaired);
        var scatter = generator.GetComponent<TowerBushScatter>(); scatter.Generate(generator);
        EditorUtility.SetDirty(manager); EditorUtility.SetDirty(scatter);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        const string reportPath = "Documentation/FrutigerAero/Buildings/TowerSupportInspection.json";
        var prior = System.IO.File.Exists(reportPath) ? Newtonsoft.Json.Linq.JObject.Parse(System.IO.File.ReadAllText(reportPath)) : new Newtonsoft.Json.Linq.JObject();
        int priorExtended = (int?)(prior["totalExtended"] ?? prior["extended"]) ?? 0;
        var report = new { extended, pads, bridges, removedSupports, removedOuterSupports=removedSupports-pads, totalExtended = priorExtended + extended, totalPads = pads, unsupportedBefore, unsupportedAfter, bushes = scatter.Placements.Count, constructionAssets = manager.CachedAssetCount,
            nonFloorAssetsPreserved = all.Count(a => !a.AssetType.IsFloor()), playing = EditorApplication.isPlaying, sceneDirty = scene.isDirty };
        System.IO.File.WriteAllText(reportPath, Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
