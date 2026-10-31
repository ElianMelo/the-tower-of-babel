using System;
using System.IO;
using TowerOfBabel.Buildings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Edit Mode review captures of the Aurora Arch in a disposable preview scene: a stretch of the tower
/// ring (bubble tube pillars and arches placed exactly like the generator places them), or all eleven
/// construction stages. Output goes to the disposable _Screenshots-and-previews-to-delete folder.
/// </summary>
public static class AuroraArchPreview
{
    public const string Output = "_Screenshots-and-previews-to-delete/Buildings/AuroraArch/";
    private const float RingRadius = 96, PillarAngle = 1.25f, ArchHeight = 3.85f;

    public static object Capture(bool stages = false, float motionTime = 4f, string suffix = "", int frames = 1,
        float frameStep = .1f, bool night = false, float distance = 8.5f)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode only.");
        Directory.CreateDirectory(Output);
        var arch = AssetDatabase.LoadAssetAtPath<GameObject>(AuroraArchAuthoring.PrefabPath);
        var pillar = AssetDatabase.LoadAssetAtPath<GameObject>(BubbleTubePillarAuthoring.PrefabPath);
        var archBuilding = arch.GetComponent<Building>();
        var material = arch.GetComponent<MeshRenderer>().sharedMaterial;
        var pillarMaterial = pillar.GetComponent<MeshRenderer>().sharedMaterial;
        float previousTime = material.GetFloat("_MotionTime"), previousPillarTime = pillarMaterial.GetFloat("_MotionTime");
        float previousDaylight = Shader.GetGlobalFloat("_AeroDaylight");
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        var scene = EditorSceneManager.NewPreviewScene();
        var target = new RenderTexture(2400, 1100, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        var oldActive = RenderTexture.active;
        try
        {
            Shader.SetGlobalFloat("_AeroDaylight", night ? 0 : 1);
            var cameraObject = new GameObject("Review camera"); Move(cameraObject, scene);
            var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = night ? new Color(.02f, .05f, .14f) : new Color(.55f, .76f, .93f);
            camera.fieldOfView = 30; camera.farClipPlane = 400; camera.targetTexture = target;
            Light(scene, "Sun", Quaternion.Euler(38, -35, 0), night ? .12f : 1.6f, night ? new Color(.6f, .7f, 1) : new Color(1, .97f, .92f));
            Light(scene, "Sky fill", Quaternion.Euler(20, 150, 0), night ? .08f : .55f, new Color(.7f, .88f, 1));

            Vector2 centre = new Vector2(material.GetVector("_TowerCenter").x, material.GetVector("_TowerCenter").z);
            Vector3 focus;
            if (stages)
            {
                // Eleven stages side by side on one bay each, following the ring so the aurora stays continuous.
                for (int i = 0; i <= 10; i++) Bay(scene, centre, i - 5, archBuilding.GetStageMesh((byte)i), material, null, null);
                focus = Ring(centre, 0, ArchHeight + 1.1f);
                distance = Mathf.Max(distance, 30);
            }
            else
            {
                for (int i = -3; i <= 3; i++)
                    Bay(scene, centre, i, archBuilding.GetStageMesh(10), material, pillar.GetComponent<Building>().GetStageMesh(10), pillarMaterial);
                focus = Ring(centre, .5f, ArchHeight + 1.15f);
            }
            Vector3 outward = new Vector3(focus.x - centre.x, 0, focus.z - centre.y).normalized;
            camera.transform.position = focus + outward * distance + Vector3.up * .35f;
            camera.transform.LookAt(focus);

            string last = null;
            for (int f = 0; f < frames; f++)
            {
                material.SetFloat("_MotionTime", motionTime + f * frameStep);
                pillarMaterial.SetFloat("_MotionTime", motionTime + f * frameStep);
                camera.Render();
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                last = Output + (stages ? "AuroraArch-Stages" : "AuroraArch-Ring") + (night ? "-Night" : "") + suffix +
                       (frames > 1 ? "-" + f.ToString("000") : "") + ".png";
                File.WriteAllBytes(last, pixels.EncodeToPNG());
            }
            return new { path = last, frames, shaderMessages = ShaderUtil.GetShaderMessages(material.shader).Length };
        }
        finally
        {
            material.SetFloat("_MotionTime", previousTime); pillarMaterial.SetFloat("_MotionTime", previousPillarTime);
            Shader.SetGlobalFloat("_AeroDaylight", previousDaylight);
            ShaderUtil.allowAsyncCompilation = asynchronous; RenderTexture.active = oldActive;
            target.Release(); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    // Ring point of bay `index` (bay 0 faces the camera), matching TowerGenerator's layout and rotation.
    private static Vector3 Ring(Vector2 centre, float index, float height)
    {
        float angle = (-90 + index * PillarAngle) * Mathf.Deg2Rad;
        return new Vector3(centre.x + Mathf.Cos(angle) * RingRadius, height, centre.y + Mathf.Sin(angle) * RingRadius);
    }

    private static void Bay(UnityEngine.SceneManagement.Scene scene, Vector2 centre, int index, Mesh archMesh, Material archMaterial,
        Mesh pillarMesh, Material pillarMaterial)
    {
        Vector3 a = Ring(centre, index, 0), b = Ring(centre, index + 1, 0);
        Vector3 dir = (b - a).normalized;
        float yAngle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg - 90f;
        Place(scene, archMesh, archMaterial, a + Vector3.up * ArchHeight, Quaternion.Euler(0, yAngle, 0));
        if (pillarMesh == null) return;
        Place(scene, pillarMesh, pillarMaterial, a, Quaternion.identity);
        if (index == 3) Place(scene, pillarMesh, pillarMaterial, b, Quaternion.identity);
    }

    private static void Place(UnityEngine.SceneManagement.Scene scene, Mesh mesh, Material material, Vector3 position, Quaternion rotation)
    {
        var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer)); Move(go, scene);
        go.transform.SetPositionAndRotation(position, rotation);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.On;
    }

    private static void Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    private static void Light(UnityEngine.SceneManagement.Scene scene, string name, Quaternion rotation, float intensity, Color color)
    {
        var go = new GameObject(name); Move(go, scene);
        var light = go.AddComponent<Light>(); light.type = LightType.Directional;
        light.intensity = intensity; light.color = color; light.shadows = LightShadows.Soft;
        go.transform.rotation = rotation;
    }
}
