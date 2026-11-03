using System;
using System.Collections.Generic;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit Mode review of the tower lava lamps: rebuilds a real section of the cached tower around one lamp in a
/// disposable preview scene and renders day and night shots. Nothing in the Tower scene changes.
/// </summary>
public static class LavaLampPreview
{
    public const string Output = "_Screenshots-and-previews-to-delete/LavaLamps/";

    public static object Capture(int floor = 4)
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var lamps = UnityEngine.Object.FindFirstObjectByType<TowerLavaLamps>() ?? throw new InvalidOperationException("Apply the tower lava lamps first.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var chunks = lamps.Chunks;
        var cycle = UnityEngine.Object.FindFirstObjectByType<AeroDayNightCycle>();
        float originalTime = cycle != null ? cycle.timeOfDay : 0;
        float originalDaylight = Shader.GetGlobalFloat("_AeroDaylight");
        var placements = lamps.Placements.Where(p => p.floor == floor).ToList();
        var focus = placements[placements.Count / 2];
        var original = SceneManager.GetActiveScene();
        if (original.path != "Assets/_Project/Scenes/Tower.unity" || original.isDirty) throw new InvalidOperationException("Use the saved Tower scene.");
        var skybox = RenderSettings.skybox;
        var reflection = ReflectionProbe.defaultTexture as Cubemap;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        const int Layer = 31;
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var previous = RenderTexture.active;
        const int width = 1600, height = 1000;
        var target = new RenderTexture(width, height, 24, RenderTextureFormat.DefaultHDR);
        var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        var floorMeshes = new RadialFloorMeshCache();
        var floorMaterial = new Material(generator.floorTileObject.GetComponent<Renderer>().sharedMaterial);
        TowerFloorLayout.ConfigureMaterial(floorMaterial, true, generator.center, true);
        System.IO.Directory.CreateDirectory(Output);
        var fluid = AssetDatabase.LoadAssetAtPath<Material>(LavaLampAuthoring.FluidPath);
        GameObject Create(string name) { var go = new GameObject(name); go.layer = Layer; SceneManager.MoveGameObjectToScene(go, scene); return go; }
        var shots = new List<string>();
        try
        {
            SceneManager.SetActiveScene(scene);
            RenderSettings.skybox = skybox;
            if (reflection != null) { RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom; RenderSettings.customReflectionTexture = reflection; }
            var camera = Create("Lava lamp review camera").AddComponent<Camera>();
            camera.enabled = false; camera.cullingMask = 1 << Layer;
            camera.targetTexture = target; camera.clearFlags = CameraClearFlags.SolidColor;
            camera.nearClipPlane = .03f; camera.farClipPlane = 600; camera.allowHDR = true;
            var data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = true;
            var sun = Create("Review sun").AddComponent<Light>(); sun.type = LightType.Directional; sun.shadows = LightShadows.Soft;
            sun.cullingMask = 1 << Layer; RenderSettings.sun = sun;

            int pieces = 0;
            Vector3 center3 = new(generator.center.x, focus.Position.y, generator.center.y);
            foreach (var asset in chunks.CachedChunks.SelectMany(c => c.Assets))
            {
                if (new Vector2(asset.Position.x - focus.Position.x, asset.Position.z - focus.Position.z).sqrMagnitude > 24 * 24) continue;
                int level = Mathf.FloorToInt((asset.Position.y + .001f) / generator.heighIncrease);
                if (level < floor - 1 || level > floor + 1 || asset.AssetType.IsDetail()) continue;
                var prefab = generator.GetModelPrefab(asset.AssetType);
                if (prefab == null) continue;
                var mesh = prefab.GetComponent<Building>()?.GetStageMesh(10) ?? prefab.GetComponent<MeshFilter>().sharedMesh;
                var material = prefab.GetComponent<Renderer>().sharedMaterial;
                if (asset.AssetType == TowerAssetType.Floor)
                {
                    float radius = Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), generator.center);
                    mesh = floorMeshes.Get(mesh, radius, asset.Scale); material = floorMaterial;
                }
                var go = Create(asset.AssetType.ToString());
                go.transform.SetPositionAndRotation(asset.Position, asset.Rotation); go.transform.localScale = asset.Scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
                pieces++;
            }
            var placed = new List<AeroProjectionLamp>();
            foreach (var p in lamps.Placements.Where(p => Mathf.Abs(p.floor - floor) <= 1 && (p.Position - focus.Position).sqrMagnitude < 26 * 26))
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(lamps.LampPrefab, scene);
                go.transform.SetPositionAndRotation(p.Position, p.mount.rotation);
                foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                foreach (var l in go.GetComponentsInChildren<Light>(true)) l.cullingMask = 1 << Layer;
                var lamp = go.GetComponent<AeroProjectionLamp>(); lamp.phase = placed.Count * 1.7f; placed.Add(lamp);
            }

            var outward = (focus.Position - center3).normalized;
            var tangent = Vector3.Cross(Vector3.up, outward);
            var lampCenter = focus.mount.MultiplyPoint3x4(LavaLampAuthoring.LampOrigin + Vector3.up * .5f);

            void Light(bool night, float clock)
            {
                // The cycle writes ambient, fog and reflection strength into the active (review) scene.
                if (cycle != null) { var keep = cycle.sun; cycle.sun = sun; cycle.ApplyTime(night ? .02f : .32f); cycle.sun = keep; }
                camera.clearFlags = night ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
                Shader.SetGlobalFloat("_AeroDaylight", night ? 0 : 1);
                if (night) { sun.intensity = .12f; sun.color = new Color(.55f, .65f, 1f); }
                sun.transform.rotation = night ? Quaternion.Euler(35, 120, 0) : Quaternion.Euler(28, 330, 0); sun.enabled = true;
                camera.backgroundColor = night ? new Color(.012f, .02f, .055f) : new Color(.55f, .74f, .9f);
                fluid.SetFloat("_FlowTime", clock);
                AeroProjectionLamp.RefreshBudget(camera.transform.position);
                foreach (var lamp in placed) lamp.Sample(clock);
            }
            void Save(string name)
            {
                camera.Render(); camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                System.IO.File.WriteAllBytes(Output + name, pixels.EncodeToPNG()); shots.Add(Output + name);
            }
            void Shot(string name, Vector3 eye, Vector3 look, float fov, bool night, float clock = 6)
            {
                camera.fieldOfView = fov; camera.transform.position = eye; camera.transform.LookAt(look);
                Light(night, clock); Save(name);
            }
            Shot("Warmup.png", lampCenter + outward * 2, lampCenter, 40, false);
            Vector3 close = lampCenter + outward * 1.45f + tangent * .95f - Vector3.up * .25f;
            Shot("Sconce-Day.png", close, lampCenter + Vector3.up * .12f, 42, false);
            Shot("Sconce-Night.png", close, lampCenter + Vector3.up * .12f, 42, true);
            Vector3 glass = lampCenter + outward * .62f + tangent * .2f;
            for (int i = 0; i < 3; i++) Shot($"Fluid-Night-{i}.png", glass, lampCenter - Vector3.up * .05f, 38, true, 4 + i * 5);
            Vector3 facade = focus.Position + outward * 15 + Vector3.up * 3.2f;
            Shot("Facade-Day.png", facade, focus.Position + Vector3.up * 3.6f, 55, false);
            Shot("Facade-Night.png", facade, focus.Position + Vector3.up * 3.6f, 55, true);
            Vector3 under = focus.Position + outward * 2.2f + tangent * 1.6f + Vector3.up * 1.4f;
            Shot("Projection-Night.png", under, focus.Position + Vector3.up * 5.4f, 70, true);
            return new { floor, pieces, lamps = placed.Count, shots };
        }
        finally
        {
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(scene, true);
            if (cycle != null) cycle.ApplyTime(originalTime);
            Shader.SetGlobalFloat("_AeroDaylight", originalDaylight);
            if (fluid != null) fluid.SetFloat("_FlowTime", -1);
            RenderTexture.active = previous; ShaderUtil.allowAsyncCompilation = asynchronous;
            floorMeshes.Dispose();
            UnityEngine.Object.DestroyImmediate(floorMaterial); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            AeroProjectionLamp.RefreshBudget(Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }
}
