using System;
using System.Linq;
using TMPro;
using TowerOfBabel.Cheats;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CheatMenuAuthoring
{
    public const string PrefabPath = "Assets/_Project/UI/FrutigerAero/Prefabs/Screens/CheatMenu.prefab";
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity") throw new InvalidOperationException("Open Tower.");
        var root = new GameObject("CheatMenu", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster), typeof(CheatMenuUI));
        var menu = root.GetComponent<CheatMenuUI>(); var data = new SerializedObject(menu);
        data.FindProperty("regularFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
        data.FindProperty("boldFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.BoldPath);
        data.FindProperty("glassMaterial").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/UI/FrutigerAero/Materials/FrostedGlass.mat");
        data.FindProperty("buttonPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/GlassButton.prefab");
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); UnityEngine.Object.DestroyImmediate(root);
        if (UnityEngine.Object.FindFirstObjectByType<CheatMenuUI>(FindObjectsInactive.Include) == null)
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (component != null && component.GetType().Name == "CheatUpgrades" && component.gameObject.scene == scene)
                UnityEngine.Object.DestroyImmediate(component);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
    }
    public static string Capture(string suffix = "", string category = "Quick actions", int width = 1600, int height = 1000)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        var preview = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        var menu = preview.GetComponent<CheatMenuUI>(); menu.BuildView(); menu.ShowPreview(); menu.SelectCategory(category);
        var camera = Camera.main; var canvas = preview.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
        var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c != canvas && !c.transform.IsChildOf(preview.transform)).ToArray();
        var enabled = canvases.Select(c => c.enabled).ToArray(); var oldTarget = camera.targetTexture; var active = RenderTexture.active;
        var target = new RenderTexture(width, height, 24); var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
        string path = "Documentation/FrutigerAero/Previews/Cheat-Menu" + suffix + ".png";
        try
        {
            foreach (var other in canvases) other.enabled = false;
            camera.targetTexture = target; Canvas.ForceUpdateCanvases();
            foreach (var rect in preview.GetComponentsInChildren<RectTransform>(true)) UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            for (int i = 0; i < 3; i++) camera.Render(); RenderTexture.active = target;
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            System.IO.File.WriteAllBytes(path, pixels.EncodeToPNG()); return path;
        }
        finally
        {
            for (int i = 0; i < canvases.Length; i++) canvases[i].enabled = enabled[i];
            camera.targetTexture = oldTarget; RenderTexture.active = active;
            UnityEngine.Object.DestroyImmediate(preview); UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
