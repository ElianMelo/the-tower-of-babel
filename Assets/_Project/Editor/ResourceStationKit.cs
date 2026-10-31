using System;
using System.IO;
using System.Linq;
using TowerOfBabel.Resources;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared editor helpers for resource nodes and processors: models, materials, sounds, icons, placement.</summary>
public static class ResourceStationKit
{
    public const string Models = "Assets/_Project/Models/Resources/";
    public const string Effects = "Assets/_Project/Audio/Resources/";
    public const string TowerScene = "Assets/_Project/Scenes/Tower.unity";
    public const float RowZ = -101.5f;
    public static readonly string[] WorkshopMaterials =
        { "Pearl", "Azure", "Steel", "Glass", "Heat", "Sand", "Chip", "RiverGlass", "RiverGlow", "WaterGlass", "WaterFoam", "SkyGlass", "SkyGlow", "Cloud", "RiverStone", "Moss", "ClearGlass", "Brick" };
    private static readonly string[] OwnMaterials = { "RiverGlass", "RiverGlow", "WaterGlass", "WaterFoam", "SkyGlass", "SkyGlow", "Cloud", "RiverStone", "Moss", "ClearGlass", "Brick" };

    public static void RequireCleanTowerScene()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != TowerScene || scene.isDirty) throw new InvalidOperationException("Open the saved Tower scene.");
    }

    public static void SaveTowerScene()
    {
        AssetDatabase.SaveAssets();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    public static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
    public static Mesh Mesh(string fbx) => AssetDatabase.LoadAllAssetsAtPath(Models + fbx + ".fbx").OfType<Mesh>().First();
    public static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_" + name + ".mat");
    public static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Effects + name + ".wav");
    public static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t != root.transform && Clean(t.name) == name);
    public static Transform TryFind(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t != root.transform && Clean(t.name) == name);
    /// <summary>Blender names are global, so repeated part names arrive as "Halo.001"; strip that suffix.</summary>
    public static string Clean(string name) => System.Text.RegularExpressions.Regex.Replace(name, @"\.\d{3}$", "");

    public static GameObject Instance(string model, Transform parent, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(model), parent); go.name = name;
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t != go.transform) t.name = Clean(t.name);
        return go;
    }

    public static void ImportModel(string model)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Models + model + ".fbx");
        if (importer == null) throw new InvalidOperationException("Missing model " + model + ". Run the Blender build scripts first.");
        importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
        importer.importAnimation = false; importer.importCameras = importer.importLights = importer.addCollider = false;
        importer.isReadable = false; importer.importNormals = ModelImporterNormals.Import;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        foreach (string name in WorkshopMaterials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Workshop_" + name), Mat(name));
        foreach (string name in new[] { "Stone", "Refined" })
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Resource_" + name),
                AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Resource_" + name + ".mat"));
        importer.SaveAndReimport();
    }

    public static AudioSource SoundSource(GameObject parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var audio = go.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1;
        audio.minDistance = 2; audio.maxDistance = 22; audio.rolloffMode = AudioRolloffMode.Linear; audio.dopplerLevel = 0; return audio;
    }

    public static ParticleSystem Particles(Transform parent, string name, Vector3 position, Material material, Mesh mesh, Color color,
        float minSize, float maxSize, float gravity, float lifetime, float speed, float rate = 0, float sphere = 0)
    {
        var ps = SandGlassWorkshopAuthoring.Particles(parent, name, position, material, mesh, color, minSize, maxSize, gravity, lifetime, speed, null, rate);
        if (sphere > 0) { var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = sphere; }
        return ps;
    }

    /// <summary>Ground height under a point, ignoring resource nodes and processors.</summary>
    public static float GroundY(float x, float z)
    {
        var hits = Physics.RaycastAll(new Vector3(x, 40, z), Vector3.down, 80)
            .Where(h => h.collider.GetComponentInParent<ResourceProcessor>() == null && h.collider.GetComponentInParent<Resource>() == null)
            .OrderBy(h => h.distance);
        return hits.Any() ? hits.First().point.y + .02f : .03f;
    }

    public static Transform SceneRoot(string name) => UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .FirstOrDefault(t => t.parent == null && t.name == name);

    /// <summary>Adds a missing scene instance, or regrounds an existing one, keeping its x and z.</summary>
    public static void PlaceOrGround(string prefabPath, string name, float x, float z = RowZ)
    {
        var existing = SceneRoot(name);
        if (existing == null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath));
            go.name = name; existing = go.transform; existing.position = new Vector3(x, 0, z);
        }
        var p = existing.position; existing.position = new Vector3(p.x, GroundY(p.x, p.z), p.z);
    }

    /// <summary>Box collider around every renderer, in the root's space (root at the origin, unrotated).</summary>
    public static BoxCollider FitBox(GameObject root, float inset = .9f, float minHeight = .5f)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is ParticleSystemRenderer)).ToArray();
        var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var collider = root.AddComponent<BoxCollider>();
        var size = new Vector3(bounds.size.x * inset, Mathf.Max(minHeight, bounds.size.y), bounds.size.z * inset);
        collider.center = new Vector3(bounds.center.x, size.y * .5f + bounds.min.y, bounds.center.z) - root.transform.position;
        collider.size = size;
        return collider;
    }

    public static void CreateMaterials()
    {
        foreach (string name in OwnMaterials)
        {
            string path = Models + "Materials/Workshop_" + name + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            bool glass = name.EndsWith("Glass"), glow = name.EndsWith("Glow") || name == "WaterFoam";
            Color color = name switch
            {
                "RiverGlass" => new Color(.16f, .82f, .7f, .58f), "RiverGlow" => new Color(.4f, 1, .8f),
                "WaterGlass" => new Color(.12f, .45f, .95f, .66f), "ClearGlass" => new Color(.9f, .97f, 1, .16f), "Brick" => new Color(.78f, .45f, .32f), "WaterFoam" => new Color(.9f, .97f, 1),
                "SkyGlass" => new Color(.55f, .78f, 1, .56f), "SkyGlow" => new Color(.78f, .82f, 1),
                "RiverStone" => new Color(.6f, .66f, .7f), "Moss" => new Color(.36f, .6f, .46f),
                _ => new Color(.97f, .98f, 1)
            };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", glass ? .96f : glow ? .8f : name == "RiverStone" ? .55f : name == "Moss" || name == "Brick" ? .25f : .4f);
            mat.SetFloat("_Metallic", glass ? .12f : 0);
            mat.enableInstancing = true;
            if (glow)
            {
                mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                mat.SetColor("_EmissionColor", name == "WaterFoam" ? new Color(.12f, .16f, .2f) : color * .9f);
            }
            if (glass)
            {
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            EditorUtility.SetDirty(mat);
        }
        AssetDatabase.SaveAssets();
    }

    public static void MakeSound(string name)
    {
        string path = Effects + name + ".wav"; if (File.Exists(path)) return;
        Directory.CreateDirectory(Effects);
        int rate = 22050;
        float duration = name switch { "CondenserHum" => 1, "BlockChime" => .9f, "EssenceBurst" => .6f, "SkyStroke" => .35f, "RiverStroke" => .3f, _ => .2f };
        int length = Mathf.RoundToInt(rate * duration); var random = new System.Random(311 + name.Length * 7); float low = 0, band = 0;
        using (var file = new BinaryWriter(File.Create(path)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + length * 2); file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16); file.Write((short)1); file.Write((short)1); file.Write(rate); file.Write(rate * 2); file.Write((short)2); file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(length * 2);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate, n = (float)random.NextDouble() * 2 - 1;
                low = Mathf.Lerp(low, n, .08f); band = Mathf.Lerp(band, n - low, .3f);
                float tau = Mathf.PI * 2, value;
                switch (name)
                {
                    case "RiverStroke": value = (Mathf.Sin(tau * (320 + 180 * t / duration) * t + Mathf.Sin(tau * 23 * t) * 2) * .35f + low * .5f) * Mathf.Sin(Mathf.PI * t / duration) * .55f; break;
                    case "WaterStroke": value = Mathf.Sin(tau * (240 * t + 1400 * t * t)) * Mathf.Exp(-t * 16) * .5f; break;
                    case "SkyStroke": value = band * Mathf.Sin(Mathf.PI * t / duration) * .9f + Mathf.Sin(tau * 1180 * t) * Mathf.Exp(-t * 9) * .05f; break;
                    case "EssenceBurst": value = (low * .6f * Mathf.Exp(-t * 7) + (Mathf.Sin(tau * 990 * t) + .4f * Mathf.Sin(tau * 1485 * t)) * Mathf.Exp(-t * 6) * .18f) * .8f; break;
                    // Whole-hertz partials repeat exactly every second, so the one-second hum loops without a click.
                    case "CondenserHum": value = (Mathf.Sin(tau * 90 * t) * .16f + Mathf.Sin(tau * 180 * t) * .07f + Mathf.Sin(tau * 541 * t) * .02f) * (.85f + .15f * Mathf.Sin(tau * 3 * t)); break;
                    default: value = (Mathf.Sin(tau * 660 * t) + .45f * Mathf.Sin(tau * 990 * t) + .2f * Mathf.Sin(tau * 1320 * t)) * Mathf.Exp(-t * 5) * .2f; break;
                }
                if (name != "CondenserHum") value *= Mathf.Clamp01(t * 600);
                file.Write((short)(Mathf.Clamp(value, -.95f, .95f) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path); var importer = (AudioImporter)AssetImporter.GetAtPath(path); importer.forceToMono = true;
        var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings; importer.SaveAndReimport();
    }

    /// <summary>128 px transparent icon of the model, or of one named part of it.</summary>
    public static Sprite RenderIcon(string name, GameObject prefab, string part = null)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var root = UnityEngine.Object.Instantiate(prefab);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = Vector3.zero; root.SetActive(true);
        GameObject target = part != null ? Find(root, part).gameObject : root;
        target.SetActive(true);
        foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = r.transform == target.transform || r.transform.IsChildOf(target.transform);
        var renderers = target.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var cameraGo = new GameObject("Icon camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
        var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.orthographic = true; camera.orthographicSize = bounds.extents.magnitude * .78f;
        camera.transform.position = bounds.center + new Vector3(3, 2.8f, -4); camera.transform.LookAt(bounds.center);
        var lightGo = new GameObject("Icon light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.4f; light.transform.rotation = Quaternion.Euler(45, -35, 0);
        var fillGo = new GameObject("Icon fill"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(fillGo, scene);
        var fill = fillGo.AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .6f; fill.color = new Color(.75f, .88f, 1); fill.transform.rotation = Quaternion.Euler(20, 150, 0);
        var rt = new RenderTexture(128, 128, 24); var old = RenderTexture.active;
        string path = Models + "Textures/" + name + "-Icon.png";
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); texture.Apply();
            // Transparent glass writes little alpha; lift coloured pixels so the icon stays readable on dark panels.
            var pixels = texture.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                var p = pixels[i]; float lum = Mathf.Max(p.r, Mathf.Max(p.g, p.b));
                if (lum > .02f) p.a = Mathf.Max(p.a, Mathf.Clamp01(.55f + lum * .5f));
                pixels[i] = p;
            }
            texture.SetPixels(pixels); texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
        }
        finally { camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); EditorSceneManager.ClosePreviewScene(scene); }
        AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 128; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
