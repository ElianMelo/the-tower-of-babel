// Edit Mode review captures for the Aero shading pass (no Play Mode).
// Run: unity command script run --file Tools/AeroShading/CaptureAeroShading.cs --entry CaptureAeroShading.Run --args '["after"]'
// Output: _Screenshots-and-previews-to-delete/aero-shading/<label>/*.png
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class CaptureAeroShading
{
    const int Width = 1280, Height = 720, Layer = 31;
    sealed class Source : IRiverGlassDrawSource
    {
        public readonly List<RiverGlassDraw> draws = new();
        public void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> destination) => destination.AddRange(draws);
    }

    public static string Run(string label)
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets") throw new InvalidOperationException("Wrong project");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only");
        string output = Path.Combine("_Screenshots-and-previews-to-delete", "aero-shading", label);
        Directory.CreateDirectory(output);
        var original = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewPreviewScene();
        var made = new List<Object>();
        var source = new Source();
        bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var feature = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset").rendererFeatures.OfType<RiverGlassRendererFeature>().Single();
        float oldClock = feature.previewTime, oldDay = Shader.GetGlobalFloat("_AeroDaylight");
        var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.DefaultHDR) { antiAliasing = 1 };
        var readback = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        var written = new List<string>();
        AeroWetnessController wet = null;
        try
        {
            GameObject Spawn(string name, Mesh mesh, Material material, Vector3 position, Vector3 scale)
            {
                var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); go.layer = Layer;
                go.transform.position = position; go.transform.localScale = scale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
                return go;
            }
            Material Mat(string path) => AssetDatabase.LoadAssetAtPath<Material>(path);
            Mesh Primitive(PrimitiveType type) { var g = GameObject.CreatePrimitive(type); var m = g.GetComponent<MeshFilter>().sharedMesh; Object.DestroyImmediate(g); return m; }

            var camGo = new GameObject("Aero review camera"); SceneManager.MoveGameObjectToScene(camGo, scene);
            var camera = camGo.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene; camera.targetTexture = target;
            camera.cullingMask = 1 << Layer; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .78f, 1f);
            camera.allowHDR = true; camera.fieldOfView = 55; camera.nearClipPlane = .05f; camera.farClipPlane = 400;
            var cameraData = camera.GetUniversalAdditionalCameraData(); cameraData.renderPostProcessing = false; cameraData.volumeLayerMask = 1 << Layer;
            var sunGo = new GameObject("Sun"); SceneManager.MoveGameObjectToScene(sunGo, scene);
            var sun = sunGo.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 2.2f; sun.color = new Color(1, .97f, .9f);
            sun.shadows = LightShadows.Soft; sun.transform.rotation = Quaternion.Euler(52, -35, 0);
            Shader.SetGlobalFloat("_AeroDaylight", 1);

            // Wetness driver, preview clock fixed.
            var wetGo = new GameObject("Wetness"); SceneManager.MoveGameObjectToScene(wetGo, scene);
            wet = wetGo.AddComponent<AeroWetnessController>();
            wet.profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>("Assets/_Project/Environment/Lighting/AeroWetness.asset");
            wet.previewTime = 3.2f; wet.OverrideWetness(.18f); wet.Tick(0);

            // River: one floor of glass-water tiles over a marble floor 6 m below.
            var tile = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            var marble = Mat("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat");
            var glassFloor = new Material(Mat("Assets/_Project/Environment/Lighting/GlassWaterFloor.mat")); made.Add(glassFloor);
            glassFloor.SetFloat("_RiverStage", 10); glassFloor.SetFloat("_RiverFloorIndex", 0); glassFloor.SetFloat("_RadialFloor", 0);
            var matrices = new List<Matrix4x4>();
            for (int x = 3; x < 11; x++) for (int z = -3; z < 3; z++)
            {
                var p = new Vector3(x * 4 + 2, 6, z * 4 + 2);
                Spawn("River tile", tile, glassFloor, p, Vector3.one);
                matrices.Add(Matrix4x4.Translate(p));
                Spawn("Lower marble", tile, marble, new Vector3(p.x, 0, p.z), Vector3.one);
            }
            var draw = new RiverGlassDraw { mesh = tile, matrices = matrices.ToArray(), count = matrices.Count, floor = 1, height = 6, radial = 0 };
            draw.Configure(); source.draws.Add(draw);
            RiverGlassRenderRegistry.Register(source);
            // Things to see through the water: a jelly lamp, pearl and steel on the floor below.
            var sphere = Primitive(PrimitiveType.Sphere); var cube = Primitive(PrimitiveType.Cube); var cylinder = Primitive(PrimitiveType.Cylinder);
            Spawn("Jelly below", sphere, Mat("Assets/_Project/Environment/Lighting/AquaLampBlue.mat"), new Vector3(26, 1.2f, 0), Vector3.one * 2);
            Spawn("Glow below", cube, Mat("Assets/_Project/Environment/Lighting/AquaLampGlow.mat"), new Vector3(30, .8f, .5f), Vector3.one * 1.4f);
            Spawn("Red marker below", cube, Mat("Assets/_Project/Models/Resources/Materials/Workshop_Heat.mat"), new Vector3(22, .6f, -.4f), new Vector3(1.2f, 1.2f, 1.2f));
            // Columns holding the river floor, for scale.
            foreach (var x in new[] { 18f, 34f }) Spawn("Pillar", cylinder, Mat("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Pillar.mat"), new Vector3(x, 3, 4), new Vector3(1, 3, 1));

            // Material line-up on the upper deck, away from the channel.
            var lineup = new (string path, PrimitiveType shape)[]
            {
                ("Assets/_Project/Models/Resources/Materials/Workshop_Steel.mat", PrimitiveType.Cylinder),
                ("Assets/_Project/Models/Resources/Materials/Resource_Steel.mat", PrimitiveType.Sphere),
                ("Assets/_Project/Models/Resources/Materials/Workshop_Azure.mat", PrimitiveType.Sphere),
                ("Assets/_Project/Models/Resources/Materials/Workshop_Pearl.mat", PrimitiveType.Sphere),
                ("Assets/_Project/Environment/Lighting/AquaLampPearl.mat", PrimitiveType.Cube),
                ("Assets/_Project/Environment/Lighting/AquaLampBlue.mat", PrimitiveType.Sphere),
                ("Assets/_Project/Environment/Lighting/AquaLampGlow.mat", PrimitiveType.Cube),
                ("Assets/_Project/Models/Resources/Materials/Workshop_Glass.mat", PrimitiveType.Sphere),
                ("Assets/_Project/Models/Resources/Materials/Resource_Stone.mat", PrimitiveType.Cube),
                ("Assets/_Project/Environment/Foliage/AeroBushLeaf.mat", PrimitiveType.Sphere),
            };
            for (int i = 0; i < lineup.Length; i++)
                Spawn("Lineup " + i, Primitive(lineup[i].shape), Mat(lineup[i].path), new Vector3(14 + i * 2.2f, 6.9f, 9), Vector3.one * 1.6f);
            var bubble = new GameObject("Bubble"); SceneManager.MoveGameObjectToScene(bubble, scene); bubble.layer = Layer;
            bubble.transform.position = new Vector3(20, 9.5f, 7); bubble.transform.localScale = Vector3.one * 1.6f;
            bubble.AddComponent<MeshFilter>().sharedMesh = sphere; bubble.AddComponent<MeshRenderer>().sharedMaterial = Mat("Assets/_Project/UI/FrutigerAero/Materials/ExperienceWorldBubble.mat");

            var volumeGo = new GameObject("Review volume"); SceneManager.MoveGameObjectToScene(volumeGo, scene); volumeGo.layer = Layer;
            var volume = volumeGo.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100; volume.enabled = false;

            void Shot(string name, Vector3 position, Vector3 look, float time = 3.2f)
            {
                feature.previewTime = time; wet.previewTime = time; wet.Tick(0);
                camera.transform.position = position; camera.transform.LookAt(look);
                camera.Render(); camera.Render();
                Graphics.Blit(target, readback); RenderTexture.active = readback;
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0); pixels.Apply();
                string path = Path.Combine(output, name + ".png");
                File.WriteAllBytes(path, pixels.EncodeToPNG()); written.Add(path);
            }

            if (label.StartsWith("floor"))
            {
                // Player eye height over the marble deck, sun ahead for gloss.
                sun.transform.rotation = Quaternion.Euler(28, 160, 0);
                foreach (var (name, value, rain) in new[] { ("floor-dry", 0f, false), ("floor-dew", .22f, false), ("floor-rain", 1f, true) })
                {
                    wet.Raining = rain; wet.OverrideWetness(value); for (int i = 0; i < 40; i++) wet.Tick(.25f);
                    Shot(name, new Vector3(20, 7.7f, 6.5f), new Vector3(30, 6, 12));
                    Shot(name + "-down", new Vector3(16, 7.7f, 7), new Vector3(17.5f, 6, 9.5f));
                }
                return string.Join("\n", written);
            }
            Shot("river-above", new Vector3(23, 9.2f, -6.5f), new Vector3(27, 6, .5f));
            Shot("river-above-steep", new Vector3(26, 11, -2.5f), new Vector3(26.5f, 6, .3f));
            Shot("river-grazing", new Vector3(16, 7.1f, -1.6f), new Vector3(30, 5.8f, .4f));
            Shot("river-below", new Vector3(23.5f, 2.6f, -4.5f), new Vector3(27, 6, .3f));
            Shot("river-below-steep", new Vector3(26, 3.2f, -1.2f), new Vector3(26.4f, 6, .2f));
            Shot("materials-dew", new Vector3(24, 8.6f, 3.2f), new Vector3(24, 7, 9));
            wet.OverrideWetness(1); wet.Raining = true; for (int i = 0; i < 40; i++) wet.Tick(.25f);
            Shot("materials-rain", new Vector3(24, 8.6f, 3.2f), new Vector3(24, 7, 9));
            Shot("river-above-rain", new Vector3(23, 9.2f, -6.5f), new Vector3(27, 6, .5f));
            wet.Raining = false; wet.OverrideWetness(0); wet.Tick(10);
            Shot("materials-dry", new Vector3(24, 8.6f, 3.2f), new Vector3(24, 7, 9));
            wet.OverrideWetness(.18f); wet.Tick(0);

            cameraData.renderPostProcessing = true; volume.enabled = true;
            foreach (var (look, path) in new[] { ("restrained", AeroShadingAuthoring.RestrainedPath), ("bright", AeroShadingAuthoring.BrightPath), ("y2k", AeroShadingAuthoring.Y2KPath) })
            {
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                Shot("post-" + look + "-materials", new Vector3(24, 8.6f, 3.2f), new Vector3(24, 7, 9));
                Shot("post-" + look + "-river", new Vector3(23, 9.2f, -6.5f), new Vector3(27, 6, .5f));
            }
            return string.Join("\n", written);
        }
        finally
        {
            RiverGlassRenderRegistry.Unregister(source);
            feature.previewTime = oldClock; Shader.SetGlobalFloat("_AeroDaylight", oldDay);
            if (wet != null) Object.DestroyImmediate(wet);
            RenderTexture.active = oldActive;
            EditorSceneManager.ClosePreviewScene(scene); SceneManager.SetActiveScene(original);
            foreach (var o in made) Object.DestroyImmediate(o);
            target.Release(); Object.DestroyImmediate(target); readback.Release(); Object.DestroyImmediate(readback); Object.DestroyImmediate(pixels);
            ShaderUtil.allowAsyncCompilation = oldAsync;
            // Restore the scene's own wetness driver if present.
            var sceneWet = Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).FirstOrDefault();
            if (sceneWet != null) { sceneWet.enabled = false; sceneWet.enabled = true; }
        }
    }
}
