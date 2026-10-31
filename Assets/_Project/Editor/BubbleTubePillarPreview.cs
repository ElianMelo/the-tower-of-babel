using System;
using System.IO;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Edit Mode review captures of the Bubble Tube pillar in a disposable preview scene: all eleven
/// construction stages, and a row of completed instances showing per-instance bubbles/motion.
/// Output goes to the disposable _Screenshots-and-previews-to-delete folder.
/// </summary>
public static class BubbleTubePillarPreview
{
    public const string Output = "_Screenshots-and-previews-to-delete/Buildings/BubbleTubePillar/";

    public static object Capture(bool stages, float motionTime = 2.5f, string suffix = "", int frames = 1, float frameStep = .1f, bool closeUp = false)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        Directory.CreateDirectory(Output);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BubbleTubePillarAuthoring.PrefabPath);
        var building = prefab.GetComponent<Building>();
        var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
        float previousTime = material.GetFloat("_MotionTime");
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(2400, 1100, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        try
        {
            var cameraObject = new GameObject("Review camera"); SceneManager(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .76f, .93f);
            camera.fieldOfView = 30; camera.farClipPlane = 200; camera.targetTexture = target;
            Light(scene, "Sun", Quaternion.Euler(38, -35, 0), 1.6f, new Color(1, .97f, .92f));
            Light(scene, "Sky fill", Quaternion.Euler(20, 150, 0), .55f, new Color(.7f, .88f, 1));
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); SceneManager(floor, scene);
            floor.transform.localScale = new Vector3(12, 1, 6);
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(BubbleTubePillarAuthoring.Art + "Review_White.mat");
            UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());

            // Stages 0-10 in one row, or six completed pillars at scattered origins (own seeds).
            if (stages)
            {
                for (int i = 0; i <= 10; i++) Pillar(scene, building.GetStageMesh((byte)i), material, new Vector3((i - 5) * 1.2f, 0, 0));
                camera.transform.position = new Vector3(0, 2.6f, -13.5f);
            }
            else if (closeUp)
            {
                // Three pillars filling the frame: glass distortion and bubble detail.
                for (int i = 0; i < 3; i++) Pillar(scene, building.GetStageMesh(10), material, new Vector3((i - 1) * .95f, 0, 0));
                camera.transform.position = new Vector3(0, 2.3f, -3.3f);
            }
            else
            {
                for (int i = 0; i < 6; i++) Pillar(scene, building.GetStageMesh(10), material, new Vector3((i - 2.5f) * 1.3f, 0, 0));
                camera.transform.position = new Vector3(0, 2.4f, -9.2f);
            }
            camera.transform.LookAt(new Vector3(0, 2, 0));

            string last = null;
            for (int f = 0; f < frames; f++)
            {
                material.SetFloat("_MotionTime", motionTime + f * frameStep);
                camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                last = Output + (stages ? "BubbleTube-Stages" : closeUp ? "BubbleTube-CloseUp" : "BubbleTube-Instances") + suffix + (frames > 1 ? "-" + f.ToString("000") : "") + ".png";
                File.WriteAllBytes(last, pixels.EncodeToPNG());
            }
            return new { path = last, frames, shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Length };
        }
        finally
        {
            material.SetFloat("_MotionTime", previousTime);
            ShaderUtil.allowAsyncCompilation = asynchronous; RenderTexture.active = oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void SceneManager(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    private static void Light(UnityEngine.SceneManagement.Scene scene, string name, Quaternion rotation, float intensity, Color color)
    {
        var go = new GameObject(name); SceneManager(go, scene);
        var light = go.AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = intensity; light.color = color; light.shadows = LightShadows.Soft;
        go.transform.rotation = rotation;
    }

    private static void Pillar(UnityEngine.SceneManagement.Scene scene, Mesh mesh, Material material, Vector3 position)
    {
        var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); SceneManager(go, scene);
        go.transform.position = position;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }
}
