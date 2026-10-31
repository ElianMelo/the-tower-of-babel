using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>Edit Mode capture of a completed deck with pillar shadows, built from saved tower data in a disposable scene.</summary>
public static class FloorFinishReview
{
    public const string Folder = "_Screenshots-and-previews-to-delete/floor-finish";

    public static object Capture(string name, int floor = 1, bool side = false)
    {
        var original = SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            original.path != "Assets/_Project/Scenes/Tower.unity" || original.isDirty) throw new InvalidOperationException("Use saved Tower in Edit Mode.");
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var sourceSun = RenderSettings.sun;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var oldTarget = RenderTexture.active; var rt = new RenderTexture(1440, 900, 24, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(1440, 900, TextureFormat.RGB24, false);
        var materials = new Dictionary<TowerAssetType, Material>(); var meshes = new RadialFloorMeshCache();
        bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        GameObject Create(string n, params Type[] types) { var go = new GameObject(n, types); go.layer = 31; SceneManager.MoveGameObjectToScene(go, scene); return go; }
        try
        {
            SceneManager.SetActiveScene(scene);
            var sun = Create("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            sun.transform.rotation = sourceSun.transform.rotation; sun.color = sourceSun.color; sun.intensity = sourceSun.intensity;
            sun.shadows = LightShadows.Soft; sun.cullingMask = 1 << 31; RenderSettings.sun = sun;
            var camera = Create("Deck review").AddComponent<Camera>(); camera.scene = scene; camera.enabled = false;
            camera.targetTexture = rt; camera.allowHDR = true; camera.nearClipPlane = .05f; camera.farClipPlane = 400; camera.fieldOfView = 70;
            camera.cullingMask = 1 << 31;
            float wall = generator.GetWallRadius(floor), y = floor * manager.FloorHeight;
            float a = 200 * Mathf.Deg2Rad, b = 214 * Mathf.Deg2Rad;
            Vector3 P(float ang, float r, float h) => new(generator.center.x + Mathf.Cos(ang) * r, h, generator.center.y + Mathf.Sin(ang) * r);
            camera.transform.position = P(a, wall - 6, y + 2.2f); camera.transform.LookAt(P(b, wall - 3, y));
            // Side view: just outside the deck edge at slab height, looking along the tile edges.
            if (side) { camera.transform.position = P(a, generator.GetFloorOuterRadius(floor) + 1.2f, y + .35f); camera.transform.LookAt(P(a + 3 * Mathf.Deg2Rad, generator.GetFloorOuterRadius(floor), y + .1f)); camera.fieldOfView = 40; }
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            var volume = Create("Atmosphere").AddComponent<Volume>(); volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
            int count = 0;
            foreach (var asset in manager.CachedChunks.SelectMany(x => x.Assets))
            {
                if (asset.Position.y < y - 1 || asset.Position.y > y + manager.FloorHeight + 1 ||
                    Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), new Vector2(camera.transform.position.x, camera.transform.position.z)) > 60) continue;
                var prefab = manager.AssetPrefabs.GetPrefab(asset.AssetType);
                var mesh = prefab.GetComponent<Building>().GetStageMesh(10);
                if (!materials.TryGetValue(asset.AssetType, out var material))
                {
                    material = new Material(prefab.GetComponent<Renderer>().sharedMaterial); materials.Add(asset.AssetType, material);
                    if (asset.AssetType.IsFloor()) TowerFloorLayout.ConfigureMaterial(material, true, manager.FloorCenter, true);
                }
                if (asset.AssetType.IsFloor())
                    mesh = meshes.Get(mesh, Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), manager.FloorCenter), asset.Scale);
                var go = Create(asset.AssetType.ToString(), typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetPositionAndRotation(asset.Position, asset.Rotation); go.transform.localScale = asset.Scale;
                go.GetComponent<MeshFilter>().sharedMesh = mesh; go.GetComponent<Renderer>().sharedMaterial = material; count++;
            }
            for (int i = 0; i < 4; i++) camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); pixels.Apply();
            System.IO.Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.png"; System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG());
            return new { path, assets = count };
        }
        finally
        {
            RenderSettings.sun = sourceSun; RenderTexture.active = oldTarget; ShaderUtil.allowAsyncCompilation = async;
            SceneManager.SetActiveScene(original); EditorSceneManager.CloseScene(scene, true); meshes.Dispose();
            foreach (var material in materials.Values) UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
