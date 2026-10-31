using System;
using System.IO;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class SandGlassWorkshopPreview
{
    public static object Capture(bool frames = true)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode previews only.");
        const string output = "Documentation/FrutigerAero/Resources/Previews/";
        Directory.CreateDirectory(output); Directory.CreateDirectory("Temp/SandGlassWork");
        var scene = EditorSceneManager.NewPreviewScene();
        var oldActive = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var rt = new RenderTexture(1280, 800, 24); var pixels = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        void Save(Camera camera, string path)
        {
            camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
            pixels.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); pixels.Apply(); File.WriteAllBytes(path, pixels.EncodeToPNG());
        }
        try
        {
            var camera = Create("Workshop review camera").AddComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.66f, .81f, .9f);
            camera.orthographic = true; camera.orthographicSize = 2.75f; camera.nearClipPlane = .03f; camera.farClipPlane = 100;
            camera.transform.position = new Vector3(4.6f, 3.7f, -8); camera.transform.LookAt(new Vector3(0, .8f, 0));
            var key = Create("Soft sunlight").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.25f;
            key.transform.rotation = Quaternion.Euler(40, -35, 0); key.shadows = LightShadows.Soft; key.shadowStrength = .65f;
            var fill = Create("Cool fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .5f;
            fill.transform.rotation = Quaternion.Euler(35, 135, 0); fill.color = new Color(.8f, .94f, 1);
            var probe = Create("Studio reflections").AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size = Vector3.one * 40;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(0, -.06f, 1); ground.transform.localScale = new Vector3(30, .1f, 30);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            GameObject Instance(string path, Vector3 position)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene); go.transform.position = position; return go;
            }
            var rock = Instance(ResourceWorkshopAuthoring.StonePrefab, new Vector3(-2.6f, 1.5f, 0));
            var sand = Instance(SandGlassWorkshopAuthoring.SandPrefab, new Vector3(0, .02f, 0));
            var kiln = Instance(SandGlassWorkshopAuthoring.GlassPrefab, new Vector3(2.8f, .02f, 0));
            var motion = kiln.GetComponent<ProcessorMachineAnimation>();
            motion.ResetImmediately(); Save(camera, output + "Sand-Glass-Idle.png");
            foreach (var node in new[] { rock, sand }) node.GetComponent<Resource>().BeginInteraction(null);
            void Sample(float t)
            {
                float progress = Mathf.Clamp01(t / 2.8f);
                foreach (var node in new[] { rock, sand })
                {
                    if (t < 2.8f) node.GetComponent<ResourceHarvestFeedback>().SampleEditorPose(t, progress);
                }
                motion.SampleEditorPose(t, progress, t >= 2.8f ? t - 2.8f : -1);
            }
            Sample(1.5f);
            foreach (var ps in new[] { rock, sand }.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.12f, false, false, false);
            Save(camera, output + "Sand-Glass-Working.png");
            foreach (var node in new[] { rock, sand })
            {
                node.GetComponent<AeroInteractionMotion>().Stop(true);
                node.GetComponent<ResourceHarvestFeedback>().Deplete(); node.transform.Find("Visuals").gameObject.SetActive(false);
            }
            Sample(3.05f);
            foreach (var ps in new[] { rock, sand }.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.15f, false, false, false);
            Save(camera, output + "Sand-Glass-Complete.png");
            if (frames)
            {
                foreach (var node in new[] { rock, sand })
                {
                    node.GetComponent<ResourceHarvestFeedback>().ResetFeedback(); node.transform.Find("Visuals").gameObject.SetActive(true);
                    node.GetComponent<Resource>().BeginInteraction(null);
                }
                bool broken = false;
                for (int i = 0; i < 84; i++)
                {
                    float t = i / 20f;
                    if (t >= 2.8f && !broken)
                    {
                        broken = true;
                        foreach (var node in new[] { rock, sand })
                        {
                            node.GetComponent<AeroInteractionMotion>().Stop(true); node.GetComponent<ResourceHarvestFeedback>().Deplete();
                            node.transform.Find("Visuals").gameObject.SetActive(false);
                        }
                        foreach (var ps in kiln.GetComponentsInChildren<ParticleSystem>()) ps.Emit(ps.name == "Glass glints" ? 18 : 6);
                    }
                    Sample(t);
                    foreach (var ps in new[] { rock, sand, kiln }.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.05f, false, false, false);
                    Save(camera, $"Temp/SandGlassWork/frame-{i:000}.png");
                }
            }
            return new { preview = output + "Sand-Glass-Working.png", playing = EditorApplication.isPlaying, sceneDirty = SceneManager.GetActiveScene().isDirty };
        }
        finally
        {
            RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async;
            EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
