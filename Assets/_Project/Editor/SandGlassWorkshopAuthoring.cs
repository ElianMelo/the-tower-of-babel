using System;
using System.IO;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class SandGlassWorkshopAuthoring
{
    public const string Models = "Assets/_Project/Models/Resources/";
    public const string SandPrefab = "Assets/_Project/Prefabs/Resources/Sand.prefab";
    public const string GlassPrefab = "Assets/_Project/Prefabs/Processor/GlassFurnace.prefab";
    private const string Data = "Assets/_Project/Resources/Definitions/";
    private const string Effects = "Assets/_Project/Audio/Resources/";
    private static readonly string[] MaterialNames = { "Sand", "SandLight", "Pearl", "Azure", "Steel", "Heat", "Glass", "Chip" };

    [MenuItem("Tower of Babel/Resources/Apply sand, glass and harvest feedback")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty) throw new InvalidOperationException("Open the saved Tower scene.");
        Directory.CreateDirectory(Effects); AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateMaterials();
        foreach (string model in new[] { "SandPile", "GlassFurnace", "RockChip", "SpawnerBrick" })
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Models + model + ".fbx");
            importer.globalScale = 1; importer.useFileScale = true; importer.bakeAxisConversion = true;
            importer.importAnimation = false; importer.importCameras = importer.importLights = importer.addCollider = false;
            importer.isReadable = false; importer.importNormals = ModelImporterNormals.Import;
            foreach (string name in MaterialNames)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Workshop_" + name), Mat(name));
            importer.SaveAndReimport();
        }
        foreach (string name in new[] { "RockStroke", "RockBreak", "SandStroke", "SandBreak", "Respawn", "KilnMotor", "GlassChime" }) MakeSound(name);
        var sand = Definition("Sand", ResourceType.Sand, 2, 2.6f, 6);
        var glass = Definition("Glass", ResourceType.Glass, 1, 3, 0);
        var collection = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>(Data + "ResourceDefinitions.asset");
        var sc = new SerializedObject(collection); var entries = sc.FindProperty("definitions");
        foreach (var definition in new[] { sand, glass })
        {
            bool found = false;
            for (int i = 0; i < entries.arraySize; i++) if (entries.GetArrayElementAtIndex(i).objectReferenceValue == definition) found = true;
            if (!found) { int i = entries.arraySize++; entries.GetArrayElementAtIndex(i).objectReferenceValue = definition; }
        }
        sc.ApplyModifiedPropertiesWithoutUndo();
        var stone = PrefabUtility.LoadPrefabContents(ResourceWorkshopAuthoring.StonePrefab);
        int layer = stone.transform.Find("Visuals").gameObject.layer;
        try { AddHarvest(stone, stone.transform.Find("Visuals"), false); PrefabUtility.SaveAsPrefabAsset(stone, ResourceWorkshopAuthoring.StonePrefab); }
        finally { PrefabUtility.UnloadPrefabContents(stone); }
        var node = new GameObject("Sand");
        try
        {
            var model = Model("SandPile", node.transform); model.name = "Visuals";
            foreach (var t in node.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
            var collider = node.AddComponent<BoxCollider>(); collider.center = new Vector3(0, .35f, 0); collider.size = new Vector3(2.5f, .75f, 2.18f);
            var so = new SerializedObject(node.AddComponent<Resource>());
            so.FindProperty("definition").objectReferenceValue = sand; so.FindProperty("visuals").objectReferenceValue = model;
            so.FindProperty("nodeId").ulongValue = 2001; so.ApplyModifiedPropertiesWithoutUndo();
            AddHarvest(node, model.transform, true); PrefabUtility.SaveAsPrefabAsset(node, SandPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(node); }
        var kiln = new GameObject("GlassFurnace");
        try
        {
            var model = Model("GlassFurnace", kiln.transform); model.name = "Visuals";
            foreach (var t in kiln.GetComponentsInChildren<Transform>()) t.gameObject.layer = layer;
            var collider = kiln.AddComponent<BoxCollider>(); collider.center = new Vector3(0, 1.15f, -.08f); collider.size = new Vector3(2.6f, 2.3f, 2.3f);
            var so = new SerializedObject(kiln.AddComponent<ResourceProcessor>());
            so.FindProperty("sourceResource").objectReferenceValue = sand; so.FindProperty("targetResource").objectReferenceValue = glass;
            so.FindProperty("sourceAmount").intValue = 2; so.FindProperty("targetAmount").intValue = 1;
            so.FindProperty("interactionDuration").floatValue = 3.5f;
            so.FindProperty("processorId").stringValue = "tower-glass-kiln-01"; so.ApplyModifiedPropertiesWithoutUndo();
            so = new SerializedObject(kiln.AddComponent<GlassFurnaceAnimation>());
            foreach (var pair in new[] { ("bellows", "Bellows"), ("flywheel", "Flywheel"), ("lid", "Lid"), ("sandFeed", "SandFeed"), ("glassOutput", "GlassOutput"), ("moltenPool", "MoltenPool") })
                so.FindProperty(pair.Item1).objectReferenceValue = Find(model, pair.Item2);
            var heat = so.FindProperty("heatSurfaces"); heat.arraySize = 3;
            for (int i = 0; i < 3; i++) heat.GetArrayElementAtIndex(i).objectReferenceValue = Find(model, "StatusLight_" + i).GetComponent<Renderer>();
            so.FindProperty("embers").objectReferenceValue = Particles(kiln.transform, "Kiln embers", new Vector3(.43f, 2.48f, .3f), Mat("Heat"), ChipMesh(), new Color(1, .48f, .05f), .025f, .06f, -.05f, .65f, .4f, null, 5);
            so.FindProperty("steam").objectReferenceValue = Particles(kiln.transform, "Heat vapour", new Vector3(.43f, 2.48f, .3f), DustMaterial(), null, new Color(.84f, .92f, 1, .2f), .12f, .28f, -.06f, 1, .3f, null, 4);
            so.FindProperty("glassSparkles").objectReferenceValue = Particles(kiln.transform, "Glass glints", new Vector3(0, .65f, -1.03f), Mat("Glass"), ChipMesh(), new Color(.55f, .95f, 1), .015f, .045f, .15f, .7f, .8f, null);
            var motor = SoundSource(kiln, "Kiln motor"); motor.clip = Clip("KilnMotor"); motor.loop = true; motor.volume = 0;
            so.FindProperty("motor").objectReferenceValue = motor; so.FindProperty("accents").objectReferenceValue = SoundSource(kiln, "Glass chime");
            so.FindProperty("completionClip").objectReferenceValue = Clip("GlassChime"); so.ApplyModifiedPropertiesWithoutUndo();
            foreach (string part in new[] { "SandFeed", "GlassOutput", "MoltenPool" }) Find(model, part).gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(kiln, GlassPrefab);
        }
        finally { UnityEngine.Object.DestroyImmediate(kiln); }
        Place(SandPrefab, "Sand", new Vector3(-2.4f, .03f, -101.5f));
        Place(GlassPrefab, "GlassFurnace", new Vector3(1.2f, .03f, -101.5f));
        RenderIcon("Sand", AssetDatabase.LoadAssetAtPath<GameObject>(Models + "SandPile.fbx"));
        RenderIcon("Glass", AssetDatabase.LoadAssetAtPath<GameObject>(GlassPrefab), "GlassOutput");
        foreach (string name in new[] { "Sand", "Glass" })
        {
            var so = new SerializedObject(AssetDatabase.LoadAssetAtPath<ResourceDefinition>(Data + name + ".asset"));
            so.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Models + "Textures/" + name + "-Icon.png"); so.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }
    private static Transform Find(GameObject root, string name) => root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
    private static GameObject Model(string name, Transform parent) => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx"), parent);
    private static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>(Models + "Materials/Workshop_" + name + ".mat");
    private static Mesh ChipMesh() => AssetDatabase.LoadAllAssetsAtPath(Models + "RockChip.fbx").OfType<Mesh>().Single();
    private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Effects + name + ".wav");
    private static AudioSource SoundSource(GameObject parent, string name)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var audio = go.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 1;
        audio.minDistance = 2; audio.maxDistance = 22; audio.rolloffMode = AudioRolloffMode.Linear; audio.dopplerLevel = 0; return audio;
    }
    private static ResourceDefinition Definition(string name, ResourceType type, int amount, float duration, float cooldown)
    {
        string path = Data + name + ".asset"; var d = AssetDatabase.LoadAssetAtPath<ResourceDefinition>(path);
        if (d == null) { d = ScriptableObject.CreateInstance<ResourceDefinition>(); AssetDatabase.CreateAsset(d, path); }
        var so = new SerializedObject(d); so.FindProperty("resourceId").stringValue = name.ToLowerInvariant();
        so.FindProperty("resourceType").enumValueIndex = (int)type; so.FindProperty("displayName").stringValue = name;
        so.FindProperty("amountGathered").intValue = amount; so.FindProperty("interactionDuration").floatValue = duration;
        so.FindProperty("respawnCooldown").floatValue = cooldown; so.ApplyModifiedPropertiesWithoutUndo(); return d;
    }
    private static void Place(string path, string name, Vector3 position)
    {
        var existing = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(t => t.parent == null && t.name == name);
        if (existing != null) return;
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path)); go.name = name; go.transform.position = position;
    }
    private static void AddHarvest(GameObject root, Transform visuals, bool sand)
    {
        var previous = root.transform.Find("Harvest feedback"); if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        var group = new GameObject("Harvest feedback"); group.transform.SetParent(root.transform, false);
        float minY = visuals.GetComponentsInChildren<Renderer>(true).Min(r => r.bounds.min.y) - root.transform.position.y;
        group.transform.localPosition = Vector3.up * minY;
        var marker = new GameObject("Spawner marker"); marker.transform.SetParent(group.transform, false);
        var brick = AssetDatabase.LoadAllAssetsAtPath(Models + "SpawnerBrick.fbx").OfType<Mesh>().Single();
        for (int i = 0; i < (sand ? 0 : 4); i++)
        {
            var piece = new GameObject("Brick " + i, typeof(MeshFilter), typeof(MeshRenderer)); piece.transform.SetParent(marker.transform, false);
            float angle = (i * 91 + 17) * Mathf.Deg2Rad;
            piece.transform.localPosition = new Vector3(Mathf.Cos(angle) * .3f, .015f, Mathf.Sin(angle) * .26f);
            piece.transform.localRotation = Quaternion.Euler(0, i * 47 + 13, 0);
            piece.GetComponent<MeshFilter>().sharedMesh = brick; piece.GetComponent<MeshRenderer>().sharedMaterial = Mat("Chip");
        }
        marker.SetActive(false);
        var chips = Particles(group.transform, "3D fragments", new Vector3(0, sand ? .26f : .4f, 0), Mat(sand ? "Sand" : "Chip"), ChipMesh(), sand ? new Color(.88f, .67f, .3f) : new Color(.65f, .7f, .73f), sand ? .025f : .055f, sand ? .06f : .14f, .8f, 1, sand ? 1.4f : 2.2f, group.transform);
        var dust = Particles(group.transform, "Gather dust", new Vector3(0, sand ? .3f : .35f, 0), DustMaterial(), null, sand ? new Color(.86f, .67f, .35f, .65f) : new Color(.7f, .74f, .77f, .4f), .14f, sand ? .5f : .32f, -.02f, .9f, .6f, null);
        var audio = SoundSource(group, "Gather sounds");
        if (root.GetComponent<AeroInteractionMotion>() == null) root.AddComponent<AeroInteractionMotion>();
        var feedback = root.GetComponent<ResourceHarvestFeedback>() ?? root.AddComponent<ResourceHarvestFeedback>(); var so = new SerializedObject(feedback);
        so.FindProperty("sand").boolValue = sand; so.FindProperty("visualRoot").objectReferenceValue = visuals;
        so.FindProperty("cooldownMarker").objectReferenceValue = sand ? null : marker;
        so.FindProperty("chips").objectReferenceValue = chips; so.FindProperty("dust").objectReferenceValue = dust;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("strokeClip").objectReferenceValue = Clip(sand ? "SandStroke" : "RockStroke");
        so.FindProperty("breakClip").objectReferenceValue = Clip(sand ? "SandBreak" : "RockBreak");
        so.FindProperty("respawnClip").objectReferenceValue = Clip("Respawn"); so.ApplyModifiedPropertiesWithoutUndo();
    }
    public static ParticleSystem Particles(Transform parent, string name, Vector3 position, Material material, Mesh mesh, Color color, float minSize, float maxSize, float gravity, float lifetime, float speed, Transform ground, float rate = 0)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = rate > 0; main.duration = 1.25f;
        main.maxParticles = mesh == null ? 64 : 48; main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .5f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize); main.startColor = color;
        main.startRotation3D = mesh != null; main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
        main.gravityModifier = gravity; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.PauseAndCatchup;
        var emission = ps.emission; emission.enabled = true; emission.rateOverTime = rate;
        var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 48; shape.radius = .18f;
        var size = ps.sizeOverLifetime; size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, .5f), new Keyframe(.16f, 1), new Keyframe(1, 0)));
        var col = ps.colorOverLifetime; col.enabled = true; var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .08f), new GradientAlphaKey(.75f, .5f), new GradientAlphaKey(0, 1) }); col.color = gradient;
        var rotation = ps.rotationOverLifetime; rotation.enabled = mesh != null; rotation.separateAxes = true; rotation.x = 3; rotation.y = 2; rotation.z = 4;
        if (ground != null)
        {
            var collision = ps.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.Planes;
            collision.SetPlane(0, ground); collision.bounce = .28f; collision.dampen = .5f; collision.lifetimeLoss = .1f;
        }
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.renderMode = mesh != null ? ParticleSystemRenderMode.Mesh : ParticleSystemRenderMode.Billboard;
        if (mesh != null) { renderer.mesh = mesh; renderer.enableGPUInstancing = true; }
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return ps;
    }
    private static void CreateMaterials()
    {
        Color[] colors = { new(.76f,.53f,.24f), new(.84f,.63f,.32f), new(.86f,.93f,.98f), new(.025f,.3f,.66f), new(.12f,.19f,.23f), new(1,.24f,.025f), new(.19f,.79f,.9f), new(.44f,.49f,.54f) };
        var grain = new Texture2D(256, 256, TextureFormat.RGBA32, true);
        var random = new System.Random(719);
        for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
        {
            float ripple = Mathf.Sin(y * .45f + Mathf.Sin(x * .037f) * 2) * .025f;
            float value = .89f + (float)random.NextDouble() * .16f + ripple;
            grain.SetPixel(x, y, new Color(value, value, value, 1));
        }
        grain.Apply(); string grainPath = Models + "Textures/Sand-Grain.png";
        File.WriteAllBytes(grainPath, grain.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(grain); AssetDatabase.ImportAsset(grainPath);
        for (int i = 0; i < MaterialNames.Length; i++)
        {
            string path = Models + "Materials/Workshop_" + MaterialNames[i] + ".mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", colors[i]); mat.SetFloat("_Smoothness", i < 2 || i == 7 ? .12f : i == 6 ? .95f : .6f);
            if (i < 2) mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(grainPath));
            mat.SetFloat("_Metallic", i == 4 ? .65f : i == 3 ? .4f : i == 6 ? .22f : .03f); mat.enableInstancing = true;
            if (i == 5 || i == 6) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", colors[i] * (i == 5 ? .9f : .14f)); }
            if (i == 6)
            {
                mat.SetColor("_BaseColor", new Color(.32f, .84f, .94f, .64f));
                mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_ZWrite", 0);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
        }
        DustMaterial();
    }
    private static Material DustMaterial()
    {
        string path = Models + "Materials/Workshop_Dust.mat"; var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
        {
            float radius = new Vector2((x - 31.5f) / 31.5f, (y - 31.5f) / 31.5f).magnitude;
            texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 2) * (.7f + .3f * Mathf.PerlinNoise(x * .2f, y * .2f))));
        }
        texture.Apply(); string texPath = Models + "Textures/Gather-Dust.png"; File.WriteAllBytes(texPath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(texPath);
        mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
        mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0); mat.SetFloat("_Cull", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000;
        AssetDatabase.CreateAsset(mat, path); return mat;
    }
    private static void MakeSound(string name)
    {
        string path = Effects + name + ".wav"; if (File.Exists(path)) return;
        int rate = 22050; float duration = name == "KilnMotor" ? 1 : name.Contains("Break") ? .5f : name == "GlassChime" ? .8f : .22f;
        int length = Mathf.RoundToInt(rate * duration); var random = new System.Random(71 + name.Length); float low = 0;
        using (var file = new BinaryWriter(File.Create(path)))
        {
            file.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); file.Write(36 + length * 2); file.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            file.Write(16); file.Write((short)1); file.Write((short)1); file.Write(rate); file.Write(rate * 2); file.Write((short)2); file.Write((short)16);
            file.Write(System.Text.Encoding.ASCII.GetBytes("data")); file.Write(length * 2);
            for (int i = 0; i < length; i++)
            {
                float t = (float)i / rate, n = (float)random.NextDouble() * 2 - 1; low = Mathf.Lerp(low, n, .16f); float value;
                if (name == "KilnMotor") value = (Mathf.Sin(t * Mathf.PI * 2 * 65) * .19f + Mathf.Sin(t * Mathf.PI * 2 * 130) * .06f) * (.85f + .15f * Mathf.Sin(t * Mathf.PI * 2 * 4));
                else if (name == "GlassChime" || name == "Respawn") value = (Mathf.Sin(t * Mathf.PI * 2 * 880) + .3f * Mathf.Sin(t * Mathf.PI * 2 * 1320) + .15f * Mathf.Sin(t * Mathf.PI * 2 * 2200)) * Mathf.Exp(-t * 7) * .22f;
                else if (name.StartsWith("Sand", StringComparison.Ordinal)) value = (n * .4f + low * .6f) * Mathf.Sin(Mathf.PI * t / duration) * Mathf.Exp(-t * 4) * .6f;
                else value = (low * .75f + Mathf.Sin(t * Mathf.PI * 2 * (105 - t * 80)) * .3f) * Mathf.Exp(-t * (name.Contains("Break") ? 9 : 30));
                value *= Mathf.Clamp01(t * 800); file.Write((short)(Mathf.Clamp(value, -.95f, .95f) * short.MaxValue));
            }
        }
        AssetDatabase.ImportAsset(path); var importer = (AudioImporter)AssetImporter.GetAtPath(path); importer.forceToMono = true;
        var settings = importer.defaultSampleSettings; settings.loadType = AudioClipLoadType.DecompressOnLoad; settings.compressionFormat = AudioCompressionFormat.PCM;
        importer.defaultSampleSettings = settings; importer.SaveAndReimport();
    }
    private static void RenderIcon(string name, GameObject prefab, string part = null)
    {
        var scene = EditorSceneManager.NewPreviewScene(); var root = UnityEngine.Object.Instantiate(prefab);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = Vector3.zero; root.SetActive(true);
        GameObject target = root;
        if (part != null)
        {
            target = Find(root, part).gameObject; target.SetActive(true);
            foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.enabled = r.transform == target.transform || r.transform.IsChildOf(target.transform);
        }
        var renderers = target.GetComponentsInChildren<Renderer>(); var bounds = renderers[0].bounds; foreach (var r in renderers) bounds.Encapsulate(r.bounds);
        var cameraGo = new GameObject("Icon camera"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGo, scene);
        var camera = cameraGo.AddComponent<Camera>(); camera.scene = scene; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear;
        camera.orthographic = true; camera.orthographicSize = bounds.extents.magnitude * .85f;
        camera.transform.position = bounds.center + new Vector3(3, 2.5f, -4); camera.transform.LookAt(bounds.center);
        var lightGo = new GameObject("Icon light"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGo, scene);
        var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.3f; light.transform.rotation = Quaternion.Euler(45, -35, 0);
        var rt = new RenderTexture(128, 128, 24); var old = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false); texture.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); texture.Apply();
            string path = Models + "Textures/" + name + "-Icon.png"; File.WriteAllBytes(path, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path); var importer = (TextureImporter)AssetImporter.GetAtPath(path); importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 128; importer.SaveAndReimport();
        }
        finally { camera.targetTexture = null; RenderTexture.active = old; UnityEngine.Object.DestroyImmediate(rt); EditorSceneManager.ClosePreviewScene(scene); }
    }
}
