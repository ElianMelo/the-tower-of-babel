using System;
using System.Linq;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Authoring-only weave review on disposable copies, without Play Mode or network actions.</summary>
public static class AeroInteractionMotionPreview
{
    public static object Capture()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var original = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var selection = Selection.objects;
        var active = RenderTexture.active;
        bool asynchronous = ShaderUtil.allowAsyncCompilation;
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(960, 640, 24);
        var pixels = new Texture2D(960, 640, TextureFormat.RGB24, false);
        var records = new System.Collections.Generic.List<object>();
        System.IO.Directory.CreateDirectory("Temp/InteractionWeave");
        System.IO.Directory.CreateDirectory("Documentation/FrutigerAero/Previews");
        try
        {
            ShaderUtil.allowAsyncCompilation = false;
            var cameraObject = new GameObject("Weave review camera", typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject, scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene;
            camera.targetTexture = target; camera.orthographic = true; camera.orthographicSize = 3;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.26f,.48f,.63f);
            camera.transform.position = new Vector3(7,5,-11); camera.transform.LookAt(new Vector3(0,1.3f,0));
            var lightObject = new GameObject("Review light", typeof(Light));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightObject, scene);
            var light = lightObject.GetComponent<Light>(); light.type = LightType.Directional; light.intensity = 2;
            light.transform.rotation = Quaternion.Euler(40,-35,0);
            var stone = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/Stone.prefab"),scene);
            var pillar = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab"),scene);
            stone.transform.position = new Vector3(-1.9f,1.3f,0);
            stone.transform.localScale = Vector3.one * .7f;
            pillar.transform.SetPositionAndRotation(new Vector3(1.1f,0,0),Quaternion.identity);
            pillar.transform.localScale = Vector3.one;
            var resource = stone.GetComponent<TowerOfBabel.Resources.Resource>();
            var building = pillar.GetComponent<TowerOfBabel.Buildings.Building>();
            var roots = new[] { stone, pillar };
            var initialRoots = roots.Select(x=>x.transform.localToWorldMatrix).ToArray();
            var colliders = roots.SelectMany(x=>x.GetComponentsInChildren<Collider>(true)).ToArray();
            var initialColliders = colliders.Select(x=>x.transform.localToWorldMatrix).ToArray();
            resource.BeginInteraction(null); building.BeginInteraction(null);
            var motions = roots.Select(x=>x.GetComponent<AeroInteractionMotion>()).ToArray();
            for (int frame=0;frame<84;frame++)
            {
                float seconds = frame / 24f;
                foreach (var motion in motions) motion.SampleEditorPose(seconds, frame/83f);
                for (int warm=0;warm<(frame==0 ? 3 : 1);warm++) camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0,0,960,640),0,0); pixels.Apply();
                System.IO.File.WriteAllBytes($"Temp/InteractionWeave/frame-{frame:000}.png",pixels.EncodeToPNG());
                if (frame==12 || frame==36 || frame==60)
                {
                    System.IO.File.WriteAllBytes($"Documentation/FrutigerAero/Previews/Interaction-Weave-{frame:000}.png",pixels.EncodeToPNG());
                    records.Add(new { seconds,
                        rootsUnchanged=roots.Select((x,i)=>x.transform.localToWorldMatrix==initialRoots[i]).ToArray(),
                        collidersUnchanged=colliders.Select((x,i)=>x.transform.localToWorldMatrix==initialColliders[i]).ToArray(),
                        poses=scene.GetRootGameObjects()
                            .Where(x=>x.name=="AeroInteractionPresentation").Select(x=>new {position=x.transform.GetChild(0).localPosition.ToString("F4"),rotation=x.transform.GetChild(0).localEulerAngles.ToString("F3")}).ToArray() });
                }
            }
            foreach (var motion in motions) motion.Stop(true);
            var report = new {samples=records, sourceRenderersRestored=roots.SelectMany(x=>x.GetComponentsInChildren<MeshRenderer>(true)).All(x=>!x.forceRenderingOff),
                scene=original.path, original.isDirty, playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/InteractionWeaveInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            RenderTexture.active=active; ShaderUtil.allowAsyncCompilation=asynchronous;
            EditorSceneManager.ClosePreviewScene(scene); Selection.objects=selection;
            UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
