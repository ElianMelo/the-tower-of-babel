using System;
using System.IO;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit Mode review captures of the glass stair step in a disposable preview scene: a flight of
/// completed steps over a floor with coloured objects beneath (to show see-through), or all
/// eleven construction stages. Output goes to the disposable screenshots folder.
/// </summary>
public static class GlassStepPreview
{
    public const string Output = "_Screenshots-and-previews-to-delete/Buildings/GlassStep/";

    public static object Capture(string view = "flight")
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        Directory.CreateDirectory(Output);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlassStepAuthoring.PrefabPath);
        var building = prefab.GetComponent<Building>();
        var material = prefab.GetComponent<MeshRenderer>().sharedMaterial;
        Vector3 scale = prefab.transform.localScale;
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(1800, 1200, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        Material[] temporary = Array.Empty<Material>();
        try
        {
            var cameraObject = new GameObject("Review camera"); Move(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.55f, .76f, .93f);
            camera.fieldOfView = 32; camera.farClipPlane = 200; camera.targetTexture = target;
            Light(scene, "Sun", Quaternion.Euler(48, -35, 0), 1.6f, new Color(1, .97f, .92f));
            Light(scene, "Sky fill", Quaternion.Euler(20, 150, 0), .5f, new Color(.7f, .88f, 1));
            var white = AssetDatabase.LoadAssetAtPath<Material>(BubbleTubePillarAuthoring.Art + "Review_White.mat");
            var floor = Primitive(scene, PrimitiveType.Plane, white, Vector3.zero, new Vector3(4, 1, 4));
            floor.name = "Floor";

            if (view == "stages")
            {
                for (int i = 0; i <= 10; i++)
                    Piece(scene, building.GetStageMesh((byte)i), material, new Vector3((i % 6 - 3) * 4.6f, .6f, i < 6 ? 2.5f : -1.5f), scale);
                camera.transform.position = new Vector3(-.4f, 17, -20);
                camera.transform.LookAt(new Vector3(-.4f, 0, 1));
            }
            else
            {
                // Coloured objects under the flight show what the glass lets through.
                var blue = new Material(white) { color = new Color(.1f, .35f, .85f) };
                var green = new Material(white) { color = new Color(.25f, .7f, .3f) };
                Primitive(scene, PrimitiveType.Cube, blue, new Vector3(-1, .5f, 2.5f), Vector3.one);
                Primitive(scene, PrimitiveType.Sphere, green, new Vector3(1.2f, .6f, 5f), Vector3.one * 1.2f);
                // Thin stripes on the floor make refraction and edge distortion readable.
                var stripe = new Material(white) { color = new Color(.12f, .2f, .35f) };
                for (int s = -6; s <= 6; s++)
                    Primitive(scene, PrimitiveType.Cube, stripe, new Vector3(s * .7f, .005f, 4), new Vector3(.08f, .01f, 16));
                for (int i = 0; i < 9; i++)
                    Piece(scene, building.GetStageMesh(10), material, new Vector3(-2, .5f + i * .45f, i * 1.25f), scale);
                if (view == "edge")
                {
                    camera.fieldOfView = 26;
                    camera.transform.position = new Vector3(2.2f, 1.25f, -2.2f);
                    camera.transform.LookAt(new Vector3(.4f, .6f, .6f));
                }
                else
                {
                    camera.transform.position = view == "above" ? new Vector3(5, 11, -4) : new Vector3(9, 4.5f, -6.5f);
                    camera.transform.LookAt(new Vector3(0, 1.8f, 4.5f));
                }
                temporary = new[] { blue, green, stripe };
            }
            camera.Render();
            RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            string path = Output + "GlassStep-" + view + ".png";
            File.WriteAllBytes(path, pixels.EncodeToPNG());
            return new { path, shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Length };
        }
        finally
        {
            foreach (var m in temporary) UnityEngine.Object.DestroyImmediate(m);
            ShaderUtil.allowAsyncCompilation = asynchronous; RenderTexture.active = oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Move(GameObject go, Scene scene) => SceneManager.MoveGameObjectToScene(go, scene);

    private static void Light(Scene scene, string name, Quaternion rotation, float intensity, Color color)
    {
        var go = new GameObject(name); Move(go, scene);
        var light = go.AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = intensity; light.color = color; light.shadows = LightShadows.Soft;
        go.transform.rotation = rotation;
    }

    private static GameObject Primitive(Scene scene, PrimitiveType type, Material material, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type); Move(go, scene);
        go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = material;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }

    private static void Piece(Scene scene, Mesh mesh, Material material, Vector3 position, Vector3 scale)
    {
        var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); Move(go, scene);
        go.transform.position = position; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }
}
