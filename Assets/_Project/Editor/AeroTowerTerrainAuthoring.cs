using System;
using System.Linq;
using TowerOfBabel.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class AeroTowerTerrainAuthoring
{
    public const string Root = "Assets/_Project/Environment/FrutigerTerrain";

    [MenuItem("Tower of Babel/Terrain/Generate Frutiger Wave Mountains")]
    public static void Generate()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved Tower scene before generating terrain.");
        var terrain = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(t => t.terrainData != null && t.gameObject.scene == scene);
        var markers = Enumerable.Range(1, 4).Select(i => scene.GetRootGameObjects().Single(g => g.name == "Marker" + i).transform).ToArray();
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var component = terrain.GetComponent<AeroTowerTerrain>();
        TerrainData source = component != null ? component.SourceTerrainData : terrain.terrainData;
        var oldMaterial = terrain.materialTemplate;
        var oldData = terrain.terrainData;
        System.IO.Directory.CreateDirectory(Root);
        AssetDatabase.Refresh();
        string dataPath = Root + "/TowerAeroTerrain.asset";
        var generated = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        if (generated == null)
        {
            if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), dataPath))
                throw new InvalidOperationException("Could not create the project-owned terrain copy.");
            generated = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath);
        }
        var layers = new[]
        {
            Layer("TowerGreen", new Color(.34f,.65f,.16f), 10),
            Layer("AeroMeadow", new Color(.37f,.68f,.14f), 18),
            Layer("HighMeadow", new Color(.51f,.75f,.23f), 24)
        };
        generated.terrainLayers = layers;
        string materialPath = Root + "/AeroTerrain.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) throw new InvalidOperationException("URP Terrain Lit shader is unavailable.");
            material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath);
        }
        material.enableInstancing = true;
        if (component == null) component = Undo.AddComponent<AeroTowerTerrain>(terrain.gameObject);
        var data = new SerializedObject(component);
        data.FindProperty("targetTerrain").objectReferenceValue = terrain;
        data.FindProperty("sourceTerrainData").objectReferenceValue = source;
        data.FindProperty("generatedTerrainData").objectReferenceValue = generated;
        var ocean = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Single(r => r.gameObject.scene == scene && r.name == "StylizedWater_Desktop");
        data.FindProperty("oceanSurface").objectReferenceValue = ocean.transform;
        var references = data.FindProperty("boundaryMarkers"); references.arraySize = markers.Length;
        for (int i = 0; i < markers.Length; i++) references.GetArrayElementAtIndex(i).objectReferenceValue = markers[i];
        data.FindProperty("minimumTowerRadius").floatValue = generator != null ? generator.radius : 96;
        data.ApplyModifiedPropertiesWithoutUndo();
        try
        {
            component.Generate();
            terrain.materialTemplate = material;
            terrain.heightmapPixelError = 3;
            EditorUtility.SetDirty(generated); EditorUtility.SetDirty(component); EditorUtility.SetDirty(material);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain);
            PrefabUtility.RecordPrefabInstancePropertyModifications(terrain.GetComponent<TerrainCollider>());
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            Debug.Log($"Frutiger terrain generated: center {component.ClearingCenter}, grass-free radius {component.ClearingRadius:F1} m.");
        }
        catch
        {
            terrain.terrainData = oldData; terrain.materialTemplate = oldMaterial;
            terrain.GetComponent<TerrainCollider>().terrainData = oldData;
            throw;
        }
    }

    private static TerrainLayer Layer(string name, Color color, float tileSize)
    {
        string texturePath = Root + "/" + name + ".png";
        // These small, project-owned procedural tiles are regenerated with the authoring preset.
        {
            var texture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            var colors = new Color[128 * 128];
            for (int z = 0; z < 128; z++)
            for (int x = 0; x < 128; x++)
            {
                float u = x / 128f, v = z / 128f;
                float noise = Mathf.Lerp(Mathf.Lerp(Mathf.PerlinNoise(x * .055f, z * .055f),
                        Mathf.PerlinNoise((x - 128) * .055f, z * .055f), u),
                    Mathf.Lerp(Mathf.PerlinNoise(x * .055f, (z - 128) * .055f),
                        Mathf.PerlinNoise((x - 128) * .055f, (z - 128) * .055f), u), v);
                var pixel = color * (1 + (noise - .5f) * .04f);
                // Terrain Lit reads albedo alpha as smoothness; grass and soil should stay matte.
                pixel.a = .025f;
                colors[z * 128 + x] = pixel;
            }
            texture.SetPixels(colors); texture.Apply();
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(texturePath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
        }
        string path = Root + "/" + name + ".terrainlayer";
        var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
        if (layer == null) { layer = new TerrainLayer(); AssetDatabase.CreateAsset(layer, path); }
        layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        layer.tileSize = Vector2.one * tileSize; layer.smoothness = .025f; layer.metallic = 0;
        EditorUtility.SetDirty(layer);
        return layer;
    }
}
