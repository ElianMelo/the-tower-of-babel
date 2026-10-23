using System;
using System.Linq;
using TowerOfBabel.World;
using UnityEditor;
using UnityEngine;

/// <summary>Captures the real, baked landscape in Edit Mode and restores the scene camera afterward.</summary>
public static class AeroTowerTerrainPreview
{
    public static object Capture(string suffix = "")
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        var landscape = UnityEngine.Object.FindFirstObjectByType<AeroTowerTerrain>();
        var terrain = landscape.GetComponent<Terrain>();
        var data = terrain.terrainData;
        var camera = Camera.main;
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        float fieldOfView = camera.fieldOfView, far = camera.farClipPlane, size = camera.orthographicSize;
        bool orthographic = camera.orthographic, asynchronous = ShaderUtil.allowAsyncCompilation;
        var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(1600, 1000, 24);
        var pixels = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
        const string output = "Documentation/FrutigerAero/Previews";
        System.IO.Directory.CreateDirectory(output);
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            camera.targetTexture = target; camera.farClipPlane = 1800;
            Action<string, Vector3, Vector3, bool, float> capture = (name, from, to, ortho, zoom) =>
            {
                camera.orthographic = ortho;
                camera.fieldOfView = 60; camera.orthographicSize = zoom;
                camera.transform.position = from;
                camera.transform.rotation = ortho ? Quaternion.Euler(90, 0, 0) : Quaternion.LookRotation(to - from);
                for (int i = 0; i < 6; i++) camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1600, 1000), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(output + "/Terrain-" + name + suffix + ".png", pixels.EncodeToPNG());
            };
            Vector3 center = new(landscape.ClearingCenter.x, 0, landscape.ClearingCenter.y);
            capture("Overview", center + new Vector3(340, 250, -340), center + Vector3.up * 5, false, 260);
            capture("Clearing", center + new Vector3(0, 480, 0), center, true, 230);
            capture("Ground", center + new Vector3(0, 2, 0), center + new Vector3(-230, 20, 220), false, 260);
            capture("Mountains", center + new Vector3(-175, 52, -175), center + new Vector3(-320, 38, 190), false, 260);
            var lakes = landscape.Lakes;
            for (int i = 0; i < lakes.Length; i++)
            {
                Vector3 lake = new(center.x + lakes[i].offset.x, landscape.WaterElevation, center.z + lakes[i].offset.y);
                Vector3 approach = new Vector3(-lakes[i].offset.x,0,-lakes[i].offset.y).normalized * 65;
                capture("Lake-" + (i+1), lake + approach + Vector3.up * 32, lake, false, 100);
            }
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position, rotation);
            camera.fieldOfView = fieldOfView; camera.farClipPlane = far;
            camera.orthographic = orthographic; camera.orthographicSize = size;
            camera.targetTexture = oldTarget; RenderTexture.active = active;
            ShaderUtil.allowAsyncCompilation = asynchronous;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
        int remainingGrass = 0, cellsInClearing = 0, submergedCells = 0, submergedGrass = 0;
        int resolution = data.detailResolution;
        var heightfield = data.GetHeights(0,0,data.heightmapResolution,data.heightmapResolution);
        for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
        {
            var density = data.GetDetailLayer(0, 0, resolution, resolution, layer);
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 point = new(terrain.transform.position.x + (x + .5f) * data.size.x / resolution,
                    terrain.transform.position.z + (z + .5f) * data.size.z / resolution);
                int hx = Mathf.RoundToInt((x+.5f)/resolution*(data.heightmapResolution-1));
                int hz = Mathf.RoundToInt((z+.5f)/resolution*(data.heightmapResolution-1));
                if (heightfield[hz,hx]*data.size.y+terrain.transform.position.y <= landscape.WaterElevation)
                { submergedCells++; submergedGrass += density[z,x]; }
                if (Vector2.Distance(point, landscape.ClearingCenter) > landscape.ClearingRadius) continue;
                cellsInClearing++; remainingGrass += density[z, x];
            }
        }
        var collider = terrain.GetComponent<TerrainCollider>();
        var samples = new System.Collections.Generic.List<object>();
        foreach (float radius in new[] { 0f, 80f, 108f, 140f, 220f, 300f, 380f })
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;
            Vector3 p = new(landscape.ClearingCenter.x + Mathf.Cos(angle) * radius, 0,
                landscape.ClearingCenter.y + Mathf.Sin(angle) * radius);
            float height = terrain.SampleHeight(p) + terrain.transform.position.y;
            bool hit = collider.Raycast(new Ray(p + Vector3.up * 150, Vector3.down), out var collision, 500);
            samples.Add(new { radius, angle = i * 45, height, colliderHit = hit, colliderHeight = hit ? collision.point.y : -999 });
        }
        var lakeSamples = landscape.Lakes.Select(lake =>
        {
            Vector3 p = new(landscape.ClearingCenter.x+lake.offset.x,0,landscape.ClearingCenter.y+lake.offset.y);
            float height = terrain.SampleHeight(p)+terrain.transform.position.y;
            bool hit = collider.Raycast(new Ray(p+Vector3.up*150,Vector3.down),out var collision,500);
            return new { center=p.ToString(), radii=lake.radii.ToString(), terrainHeight=height, waterHeight=landscape.WaterElevation,
                depth=landscape.WaterElevation-height, colliderHit=hit, colliderHeight=hit ? collision.point.y : -999 };
        }).ToArray();
        var report = new
        {
            center = landscape.ClearingCenter.ToString("F3"), radius = landscape.ClearingRadius,
            cellsInClearing, remainingGrass, submergedCells, submergedGrass, samples, lakeSamples,
            clearingLayer = data.terrainLayers[0].name,
            terrain = AssetDatabase.GetAssetPath(data), original = AssetDatabase.GetAssetPath(landscape.SourceTerrainData),
            sourceHash = AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(landscape.SourceTerrainData)).ToString(),
            terrainColliderMatches = collider.terrainData == data,
            compileFailed = EditorUtility.scriptCompilationFailed, playing = EditorApplication.isPlaying,
            sceneDirty = UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty
        };
        System.IO.File.WriteAllText("Documentation/FrutigerAero/TerrainInspection.json", Newtonsoft.Json.JsonConvert.SerializeObject(report, Newtonsoft.Json.Formatting.Indented));
        return report;
    }
}
