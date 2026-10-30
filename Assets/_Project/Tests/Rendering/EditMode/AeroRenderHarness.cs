using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>Preview-scene renderer for shader pixel tests (Edit Mode only).</summary>
    public sealed class AeroRenderHarness : IDisposable
    {
        public const int Layer = 31;
        public readonly Scene Scene;
        public readonly Camera Camera;
        public readonly Light Sun;
        public readonly int Size;
        private readonly Scene original;
        private readonly RenderTexture target;
        private readonly Texture2D pixels;
        private readonly RenderTexture oldActive;
        private readonly bool oldAsync;
        private readonly List<Object> owned = new();
        private readonly float oldDay;
        private readonly Light oldSun;
        private readonly float oldFinish;

        public AeroRenderHarness(int size = 128)
        {
            Size = size;
            original = SceneManager.GetActiveScene();
            Scene = EditorSceneManager.NewPreviewScene();
            oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            oldActive = RenderTexture.active;
            oldDay = Shader.GetGlobalFloat("_AeroDaylight"); Shader.SetGlobalFloat("_AeroDaylight", 1);
            target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGBHalf);
            pixels = new Texture2D(size, size, TextureFormat.RGBAFloat, false);
            var go = new GameObject("Aero test camera"); SceneManager.MoveGameObjectToScene(go, Scene);
            Camera = go.AddComponent<Camera>(); Camera.enabled = false; Camera.scene = Scene; Camera.targetTexture = target;
            Camera.cullingMask = 1 << Layer; Camera.clearFlags = CameraClearFlags.SolidColor; Camera.backgroundColor = new Color(.2f, .25f, .3f);
            Camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var sunGo = new GameObject("Aero test sun"); SceneManager.MoveGameObjectToScene(sunGo, Scene);
            Sun = sunGo.AddComponent<Light>(); Sun.type = LightType.Directional; Sun.intensity = 1.5f; Sun.shadows = LightShadows.None;
            Sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            // Earlier tests can leave RenderSettings.sun on a light outside this scene; make ours the main light.
            oldSun = RenderSettings.sun; RenderSettings.sun = Sun;
            // The open scene's shared reflection capture can be mid-rebuild after a domain reload;
            // shader pixel tests use URP's own probes instead so metals never read a stale capture.
            oldFinish = Shader.GetGlobalFloat("_AeroFinishEnabled"); Shader.SetGlobalFloat("_AeroFinishEnabled", 0);
        }

        public T Own<T>(T item) where T : Object { owned.Add(item); return item; }

        public GameObject Spawn(PrimitiveType type, Material material, Vector3 position, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type); go.layer = Layer;
            SceneManager.MoveGameObjectToScene(go, Scene);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = position; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public GameObject Spawn(Mesh mesh, Material material, Vector3 position)
        {
            var go = new GameObject("Aero test mesh") { layer = Layer };
            SceneManager.MoveGameObjectToScene(go, Scene);
            go.transform.position = position;
            go.AddComponent<MeshFilter>().sharedMesh = mesh; go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        public Color[] Render()
        {
            Camera.Render(); Camera.Render();
            RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); pixels.Apply();
            return pixels.GetPixels();
        }

        public Color Pixel(Color[] image, int x, int y) => image[Mathf.Clamp(y, 0, Size - 1) * Size + Mathf.Clamp(x, 0, Size - 1)];

        public static float Saturation(Color c)
        {
            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b)), min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            return max <= 1e-4f ? 0 : (max - min) / max;
        }

        public void Dispose()
        {
            Shader.SetGlobalFloat("_AeroDaylight", oldDay);
            RenderSettings.sun = oldSun;
            Shader.SetGlobalFloat("_AeroFinishEnabled", oldFinish);
            RenderTexture.active = oldActive;
            EditorSceneManager.ClosePreviewScene(Scene); SceneManager.SetActiveScene(original);
            foreach (var o in owned) if (o != null) Object.DestroyImmediate(o);
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            ShaderUtil.allowAsyncCompilation = oldAsync;
        }
    }
}
