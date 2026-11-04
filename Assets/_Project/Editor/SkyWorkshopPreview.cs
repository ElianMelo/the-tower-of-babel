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

/// <summary>
/// Edit Mode motion review of the Wind Condenser and the Cloud Dandelions, without entering Play Mode. It drives the
/// real runtime path (BeginWork, SetProgress, ConfirmCompletion / Deplete) and steps each component's update and the
/// particle systems frame by frame, writing numbered frames for a GIF and key-frame sheet.
/// Timeline (seconds): idle 0-1.6, work 1.6-5.1 (the 3.5 s recipe; the node's 2.8 s gather), completion, idle again.
/// </summary>
public static class SkyWorkshopPreview
{
    public const string Root = "_Screenshots-and-previews-to-delete/SkyWorkshop/";
    private const string Condenser = "Assets/_Project/Prefabs/Processor/WindCondenser.prefab";
    private const string Node = "Assets/_Project/Prefabs/Resources/SkyEssence.prefab";

    public static object Capture(string pass = "Pass1", float fps = 12, float length = 7.6f, int size = 720)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode previews only.");
        string output = Root + pass + "/"; Directory.CreateDirectory(output);
        foreach (var old in Directory.GetFiles(output, "*.png")) File.Delete(old);
        var scene = EditorSceneManager.NewPreviewScene();
        var oldActive = RenderTexture.active; bool async = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var rt = new RenderTexture(size, size, 24) { antiAliasing = 4 }; var pixels = new Texture2D(size, size, TextureFormat.RGB24, false);
        GameObject Create(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
        try
        {
            var camera = Create("Review camera").AddComponent<Camera>(); camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.62f, .8f, .95f);
            camera.fieldOfView = 30; camera.nearClipPlane = .05f; camera.farClipPlane = 200;
            var key = Create("Sun").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.3f;
            key.transform.rotation = Quaternion.Euler(42, -32, 0); key.shadows = LightShadows.Soft; key.shadowStrength = .55f;
            var fill = Create("Fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .55f;
            fill.transform.rotation = Quaternion.Euler(30, 140, 0); fill.color = new Color(.8f, .93f, 1);
            var probe = Create("Reflections").AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/_Project/Models/Buildings/FrutigerAero/Review_AquaticReflection.exr"); probe.size = Vector3.one * 120;
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground, scene);
            ground.transform.position = new Vector3(6, -.06f, 0); ground.transform.localScale = new Vector3(60, .1f, 60);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");

            GameObject Place(string path, Vector3 position)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                go.transform.position = position; return go;
            }
            var machine = Place(Condenser, new Vector3(0, .02f, 0));
            var node = Place(Node, new Vector3(14, .02f, 0));
            var anim = machine.GetComponent<ProcessorMachineAnimation>(); var machineAmbient = machine.GetComponent<AmbientMotion>();
            var processor = machine.GetComponent<ResourceProcessor>();
            var feedback = node.GetComponent<EssenceHarvestFeedback>(); var nodeAmbient = node.GetComponent<AmbientMotion>();
            var container = node.GetComponentsInChildren<Transform>(true).Single(t => t != node.transform && t.name == "SkyEssence").gameObject;
            var machineEffects = machine.GetComponentsInChildren<ParticleSystem>(true);
            var nodeEffects = node.GetComponentsInChildren<ParticleSystem>(true);
            anim.ResetImmediately(); feedback.ResetFeedback();

            void Save(string file, Vector3 focus, Vector3 direction, float distance)
            {
                camera.transform.position = focus + direction.normalized * distance; camera.transform.LookAt(focus);
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                pixels.ReadPixels(new Rect(0, 0, size, size), 0, 0); pixels.Apply(); File.WriteAllBytes(output + file, pixels.EncodeToPNG());
            }

            const float workStart = 1.6f, recipe = 3.5f, gather = 2.8f, respawnAt = 6.6f;
            float dt = 1 / fps; int frames = Mathf.RoundToInt(length * fps);
            bool machineWorking = false, machineDone = false, nodeWorking = false, nodeDone = false, nodeBack = false;
            for (int f = 0; f < frames; f++)
            {
                float t = f * dt;
                // Wind Condenser: the processor's own calls, as ResourceProcessor makes them.
                if (!machineWorking && !machineDone && t >= workStart) { anim.BeginWork(); machineWorking = true; }
                if (machineWorking)
                {
                    float p = Mathf.Clamp01((t - workStart) / recipe); anim.SetProgress(p);
                    if (p >= 1) { anim.ConfirmCompletion(); machineWorking = false; machineDone = true; }
                }
                anim.StepEditor(dt); machineAmbient.StepEditor(dt);
                machineAmbient.SimulateEffectsForPreview(0, machineAmbient.Activity);
                foreach (var ps in machineEffects) ps.Simulate(dt, false, false, false);

                // Cloud Dandelions: gather, deplete (the essence hides), then respawn.
                if (!nodeWorking && !nodeDone && t >= workStart) { feedback.BeginWork(); nodeWorking = true; }
                if (nodeWorking)
                {
                    float p = Mathf.Clamp01((t - workStart) / gather); feedback.SetProgress(p);
                    if (p >= 1) { feedback.StopWork(); feedback.Deplete(); container.SetActive(false); nodeWorking = false; nodeDone = true; }
                }
                if (nodeDone && !nodeBack && t >= respawnAt) { container.SetActive(true); feedback.Respawn(); nodeBack = true; }
                feedback.StepEditor(dt); nodeAmbient.StepEditor(dt);
                nodeAmbient.SimulateEffectsForPreview(0, nodeAmbient.Activity, feedback.Dormant);
                foreach (var ps in nodeEffects) ps.Simulate(dt, false, false, false);

                Save($"Condenser_{f:000}.png", machine.transform.position + new Vector3(0, 1.3f, -.25f), new Vector3(-.42f, .3f, -1), 6.7f);
                Save($"Node_{f:000}.png", node.transform.position + new Vector3(.05f, .58f, 0), new Vector3(-.42f, .42f, -1), 4.5f);
            }
            return new { output, frames, fps, playing = EditorApplication.isPlaying, sceneDirty = SceneManager.GetActiveScene().isDirty, processor = processor != null };
        }
        finally
        {
            RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = async;
            EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
