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

/// <summary>Edit Mode review stills of the essence nodes and the processor family, without entering Play Mode.</summary>
public static class ResourceStationsPreview
{
    public const string Output = "_Screenshots-and-previews-to-delete/ResourceStations/";

    public static object Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode previews only.");
        Directory.CreateDirectory(Output);
        var scene = EditorSceneManager.NewPreviewScene();
        var oldActive = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        const int width = 1800, height = 760;
        var rt = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        try
        {
            var camera = Create("Review camera").AddComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.66f, .81f, .9f);
            camera.orthographic = true; camera.nearClipPlane = .03f; camera.farClipPlane = 200;
            var key = Create("Sun").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.25f;
            key.transform.rotation = Quaternion.Euler(40, -35, 0); key.shadows = LightShadows.Soft; key.shadowStrength = .6f;
            var fill = Create("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .5f;
            fill.transform.rotation = Quaternion.Euler(35, 135, 0); fill.color = new Color(.8f, .94f, 1);
            var probe = Create("Reflections").AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size = Vector3.one * 120;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(6, -.06f, 10); ground.transform.localScale = new Vector3(60, .1f, 60);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            GameObject Place(string path, Vector3 position)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                go.transform.position = position; return go;
            }
            void Save(string file, Vector3 focus, float size)
            {
                camera.orthographicSize = size; camera.transform.position = focus + new Vector3(2.4f, 3.4f, -9f); camera.transform.LookAt(focus);
                camera.targetTexture = rt; camera.Render(); camera.Render(); RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply(); File.WriteAllBytes(Output + file, pixels.EncodeToPNG());
            }

            // Essences: the stone and sand references sit alongside for comparison.
            var references = new[] { Place(ResourceWorkshopAuthoring.StonePrefab, new Vector3(-3, 1.5f, 0)), Place(SandGlassWorkshopAuthoring.SandPrefab, new Vector3(0, .02f, 0)) };
            var nodes = EssenceWorkshopAuthoring.Families.Select((f, i) => Place(f.NodePrefab, new Vector3(3.2f + i * 3.1f, .02f, 0))).ToArray();
            var feedback = nodes.Select(n => n.GetComponent<EssenceHarvestFeedback>()).ToArray();
            var essenceFocus = new Vector3(4.4f, .5f, 0);
            foreach (var f in feedback) f.SampleEditorPose(1.1f, 0, false);
            Save("Essences-Idle.png", essenceFocus, 3.2f);
            foreach (var n in nodes) n.GetComponent<Resource>().BeginInteraction(null);
            foreach (var f in feedback) f.SampleEditorPose(1.9f, .7f);
            // While gathering the node renders through the interaction proxies; advance them to the new pose.
            foreach (var n in nodes) n.GetComponent<AeroInteractionMotion>().SampleEditorPose(1.9f, .7f);
            foreach (var ps in nodes.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.2f, false, false, false);
            Save("Essences-Gathering.png", essenceFocus, 3.2f);
            foreach (var n in nodes) n.GetComponent<AeroInteractionMotion>().Stop(true);
            for (int i = 0; i < nodes.Length; i++)
            {
                feedback[i].Deplete();
                nodes[i].GetComponentsInChildren<Transform>(true).Single(t => t != nodes[i].transform && t.name == EssenceWorkshopAuthoring.Families[i].Container).gameObject.SetActive(false);
            }
            foreach (var ps in nodes.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.15f, false, false, false);
            Save("Essences-Depleted.png", essenceFocus, 3.2f);
            foreach (var go in nodes.Concat(references)) UnityEngine.Object.DestroyImmediate(go);

            // Processors.
            var machines = ProcessorFamilyAuthoring.Machines.Select((m, i) => Place(m.Path, new Vector3(i * 2.8f, .02f, 20))).ToArray();
            var anims = machines.Select(m => m.GetComponent<ProcessorMachineAnimation>()).ToArray();
            var focus = new Vector3(5.6f, 1.2f, 20);
            void Machines(float seconds, float progress, float completion = -1)
            {
                foreach (var a in anims) a.SampleEditorPose(seconds, progress, completion);
                foreach (var ps in machines.SelectMany(g => g.GetComponentsInChildren<ParticleSystem>())) ps.Simulate(.12f, false, false, false);
            }
            foreach (var a in anims) a.ResetImmediately();
            Save("Processors-Idle.png", focus, 2.9f);
            foreach (var a in anims) a.BeginWork();
            Machines(.4f, .14f); Save("Processors-Entering.png", focus, 2.9f);
            Machines(1.1f, .4f); Save("Processors-Working-Early.png", focus, 2.9f);
            Machines(2.4f, .8f); Save("Processors-Working-Late.png", focus, 2.9f);
            Machines(3.4f, 1, .35f); Save("Processors-Complete.png", focus, 2.9f);
            return new { output = Output, playing = EditorApplication.isPlaying, sceneDirty = SceneManager.GetActiveScene().isDirty };
        }
        finally
        {
            RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async;
            EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
