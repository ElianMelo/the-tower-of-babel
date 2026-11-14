using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TowerOfBabel.Cheats;
using TowerOfBabel.Settings;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Builds the settings menu prefab and installs it in the Tower scene beside the developer controls. The same step
/// removes the in-game debug console object from the scene: its key (left of 1) now shows and hides the mouse cursor.
/// </summary>
public static class SettingsMenuAuthoring
{
    public const string PrefabPath = "Assets/_Project/UI/FrutigerAero/Prefabs/Screens/SettingsMenu.prefab";
    public const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";
    public const string ConsoleObjectName = "IngameDebugConsole";

    [MenuItem("Tower of Babel/UI/Install settings menu")]
    public static void ApplyFromMenu() => Debug.Log(Apply());

    public static string Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity") throw new InvalidOperationException("Open Tower.");
        var report = new List<string>();

        var root = new GameObject("SettingsMenu", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler),
            typeof(UnityEngine.UI.GraphicRaycaster));
        root.SetActive(false);
        var menu = root.AddComponent<SettingsMenuUI>();
        root.AddComponent<GameSettingsApplier>();
        var data = new SerializedObject(menu);
        data.FindProperty("regularFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
        data.FindProperty("boldFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.BoldPath);
        data.FindProperty("glassMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/UI/FrutigerAero/Materials/FrostedGlass.mat");
        data.FindProperty("buttonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/GlassButton.prefab");
        data.FindProperty("inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        data.ApplyModifiedPropertiesWithoutUndo();
        root.SetActive(true);
        // The view is built at run time; the prefab holds only the configured components.
        foreach (Transform child in root.transform.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject);
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        report.Add("Saved " + PrefabPath);

        var existing = UnityEngine.Object.FindObjectsByType<SettingsMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(m => m.gameObject.scene == scene).ToArray();
        if (existing.Length == 0)
        {
            var cheats = UnityEngine.Object.FindObjectsByType<CheatMenuUI>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(m => m.gameObject.scene == scene);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            if (cheats != null && cheats.transform.parent != null) instance.transform.SetParent(cheats.transform.parent, false);
            report.Add("Installed SettingsMenu" + (instance.transform.parent != null ? " under " + instance.transform.parent.name : " at the scene root"));
        }
        else report.Add("SettingsMenu already in the scene");

        int removed = 0;
        foreach (GameObject candidate in scene.GetRootGameObjects())
        {
            if (candidate.name != ConsoleObjectName) continue;
            UnityEngine.Object.DestroyImmediate(candidate);
            removed++;
        }
        report.Add(removed > 0 ? $"Removed {removed} {ConsoleObjectName} object(s)" : "No " + ConsoleObjectName + " object in the scene");

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return string.Join("\n", report);
    }
}
