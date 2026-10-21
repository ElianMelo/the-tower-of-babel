using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>Installs the supplied Frutiger faces in authored and dynamically created UI.</summary>
public static class FrutigerFontBuilder
{
    public const string RegularPath = "Assets/_Project/Fonts/Frutiger SDF.asset";
    public const string BoldPath = "Assets/_Project/Fonts/Frutiger Bold SDF.asset";
    private const string Folder = "Assets/_Project/Fonts/";
    private static readonly string[] UiRoots = { "Assets/_Project", "Assets/Plugins/IngameDebugConsole" };

    [MenuItem("Tower of Babel/UI/Apply Frutiger Fonts")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save scene changes before applying fonts.");

        var regular = Create("Frutiger", RegularPath, false);
        var bold = Create("Frutiger_bold", BoldPath, false);
        var regularFallback = Create("Frutiger", Folder + "Frutiger Dynamic SDF.asset", true);
        var boldFallback = Create("Frutiger_bold", Folder + "Frutiger Bold Dynamic SDF.asset", true);
        ConfigureWeights(regular, regular, bold);
        ConfigureWeights(bold, bold, bold);
        ConfigureWeights(regularFallback, regularFallback, boldFallback);
        ConfigureWeights(boldFallback, boldFallback, boldFallback);
        regular.fallbackFontAssetTable = new() { regularFallback };
        bold.fallbackFontAssetTable = new() { boldFallback };
        EditorUtility.SetDirty(regular);
        EditorUtility.SetDirty(bold);
        TMP_Settings.defaultFontAsset = regular;
        TMP_Settings.fallbackFontAssets = new() { regularFallback };
        EditorUtility.SetDirty(TMP_Settings.instance);

        // Edit shared label sources before their parents, retaining nested prefab inheritance.
        var paths = AssetDatabase.FindAssets("t:Prefab", UiRoots).Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(p => AssetDatabase.GetDependencies(p).Length).ToArray();
        int changed = 0;
        foreach (var path in paths)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!NeedsFont(asset, regular)) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Replace(root, regular);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                changed++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        foreach (var root in scene.GetRootGameObjects()) Replace(root, regular);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"Applied Frutiger fonts: {changed} prefab sources updated; scene and TMP defaults saved.");
    }

    private static TMP_FontAsset Create(string source, string path, bool dynamic)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (existing != null) return existing;
        var sourceFont = AssetDatabase.LoadAssetAtPath<Font>(Folder + source + ".ttf");
        if (sourceFont == null) throw new InvalidOperationException("Missing supplied font: " + source);
        var font = TMP_FontAsset.CreateFontAsset(sourceFont, 80, 8, GlyphRenderMode.SDF16,
            dynamic ? 1024 : 2048, dynamic ? 1024 : 2048, AtlasPopulationMode.Dynamic, dynamic);
        if (font == null) throw new InvalidOperationException("Could not create Frutiger font asset.");
        font.name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (!dynamic)
        {
            // Bake Latin text, accents, punctuation and available UI symbols into the main atlas.
            var characters = Enumerable.Range(32, 95).Concat(Enumerable.Range(160, 432))
                .Concat(Enumerable.Range(8192, 112)).Concat(Enumerable.Range(8592, 112))
                .Concat(new[] { 9633, 9632, 9679, 9711, 10003, 10005 }).Select(c => (uint)c).ToArray();
            font.TryAddCharacters(characters, out _, true);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
        }
        font.material.name = font.name + " Material";
        font.atlasTexture.name = font.name + " Atlas";
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(font.material, font);
        foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
        var serialized = new SerializedObject(font);
        serialized.FindProperty("m_ClearDynamicDataOnBuild").boolValue = dynamic;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(font);
        return font;
    }

    private static void ConfigureWeights(TMP_FontAsset font, TMP_FontAsset regular, TMP_FontAsset bold)
    {
        var weights = font.fontWeightTable;
        weights[4].regularTypeface = regular;
        weights[6].regularTypeface = bold;
        weights[7].regularTypeface = bold;
        weights[8].regularTypeface = bold;
        weights[9].regularTypeface = bold;
        EditorUtility.SetDirty(font);
    }

    private static bool NeedsFont(GameObject root, TMP_FontAsset font) =>
        root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.font != font || t.fontSharedMaterial != font.material) ||
        root.GetComponentsInChildren<TMP_InputField>(true).Any(t => t.fontAsset != font);

    private static void Replace(GameObject root, TMP_FontAsset font)
    {
        foreach (var field in root.GetComponentsInChildren<TMP_InputField>(true))
        {
            if (field.fontAsset == font) continue;
            field.fontAsset = font;
            EditorUtility.SetDirty(field);
            if (PrefabUtility.IsPartOfPrefabInstance(field)) PrefabUtility.RecordPrefabInstancePropertyModifications(field);
        }
        foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
        {
            if (text.font == font && text.fontSharedMaterial == font.material) continue;
            text.font = font;
            text.fontSharedMaterial = font.material;
            EditorUtility.SetDirty(text);
            if (PrefabUtility.IsPartOfPrefabInstance(text)) PrefabUtility.RecordPrefabInstancePropertyModifications(text);
        }
    }
}
