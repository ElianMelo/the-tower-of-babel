using System;
using System.Linq;
using TMPro;
using TowerOfBabel.Interface;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Updates existing source prefabs without rebuilding their layout or event bindings.</summary>
public static class AeroUIJuiceAuthoring
{
    private const string Root = FrutigerAeroBuilder.Root + "/Prefabs/";

    [MenuItem("Tower of Babel/UI/Apply fluid UI feedback")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        foreach (string screen in new[] { "Inventory", "Upgrade", "AnimationEmote", "Interaction", "ServerStatus", "Information" })
            Edit(Root + "Screens/" + screen + ".prefab", root =>
            {
                var visual = root.transform.Find("Visuals");
                if (visual == null) return;
                bool modal = screen == "Inventory" || screen == "Upgrade" || screen == "AnimationEmote";
                Motion(visual, screen == "Inventory" || screen == "Upgrade", modal, modal);
                if (screen == "Upgrade")
                    foreach (string board in new[] { "GatherBoard", "ProcessorBoard", "BuilderBoard" })
                        Motion(visual.Find("BoardWell/Viewport/" + board), false, false, true);
            });
        Edit(Root + "Screens/HUD.prefab", root =>
        {
            foreach (Transform row in root.transform.Find("MenuLauncher/MenuList")) Motion(row, false, false, true, .025f);
        });
        Edit("Assets/_Project/Prefabs/Interface/OptionButton.prefab", root => Motion(root.transform, false, false, true, .025f));
        Edit("Assets/_Project/Prefabs/Interface/ResourceInventory.prefab", root =>
        {
            Motion(root.transform, false, false, false, .03f);
            var amount = root.transform.Find("Amount").GetComponent<TMP_Text>();
            var delta = root.transform.Find("AmountChange")?.GetComponent<TMP_Text>();
            if (delta == null)
            {
                delta = UnityEngine.Object.Instantiate(amount, root.transform); delta.name = "AmountChange";
                delta.fontSize = 16; delta.enableAutoSizing = false; delta.alignment = TextAlignmentOptions.Right;
                delta.raycastTarget = false; delta.color = new Color(.08f,.52f,.27f,1); delta.text = ""; delta.alpha = 0;
            }
            delta.rectTransform.anchorMin = delta.rectTransform.anchorMax = new Vector2(0,1);
            delta.rectTransform.pivot = new Vector2(0,1); delta.rectTransform.sizeDelta = new Vector2(100,24);
            delta.rectTransform.anchoredPosition = new Vector2(320,-44);
            var value = root.GetComponent<AeroValueFeedback>() ?? root.AddComponent<AeroValueFeedback>();
            Set(value, "amountText", amount); Set(value, "deltaText", delta);
            Set(value, "icon", root.transform.Find("IconWell/ResourceIcon")); Set(value, "surface", root.GetComponent<AeroGlassGeometry>());
        });
        Edit(FrutigerAeroBuilder.InterfacePath, root =>
        {
            if (root.GetComponent<AeroUIAudio>() == null) root.AddComponent<AeroUIAudio>();
            var audio = root.GetComponent<AudioSource>(); audio.playOnAwake = false; audio.spatialBlend = 0; audio.loop = false;
        });
        // Resolve icon references in source prefabs first, so nested instances inherit the same feedback.
        var paths = AssetDatabase.FindAssets("t:Prefab", new[] { Root.TrimEnd('/'), "Assets/_Project/Prefabs/Interface" })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => AssetDatabase.GetDependencies(p).Length).ToArray();
        foreach (var path in paths)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponentsInChildren<AeroButtonJuice>(true).Length == 0) continue;
            Edit(path, root =>
            {
                foreach (var button in root.GetComponentsInChildren<AeroButtonJuice>(true))
                {
                    var icon = button.transform.Cast<Transform>().FirstOrDefault(t => t.name.EndsWith("Icon", StringComparison.Ordinal));
                    Set(button, "icon", icon);
                }
            });
        }
        AssetDatabase.SaveAssets();
    }

    private static void Motion(Transform target, bool transformMotion, bool sound, bool interactive, float stagger = 0)
    {
        if (target == null) throw new InvalidOperationException("Missing UI motion target.");
        var motion = target.GetComponent<AeroPanelMotion>() ?? target.gameObject.AddComponent<AeroPanelMotion>();
        var serialized = new SerializedObject(motion);
        serialized.FindProperty("animateTransform").boolValue = transformMotion;
        serialized.FindProperty("playSounds").boolValue = sound;
        serialized.FindProperty("allowInteraction").boolValue = interactive;
        serialized.FindProperty("siblingStagger").floatValue = stagger;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        // Edit Mode remains an ordinary, visible authoring surface.
        var group = target.GetComponent<CanvasGroup>(); group.alpha = 1; group.interactable = group.blocksRaycasts = interactive;
    }

    private static void Set(UnityEngine.Object component, string property, UnityEngine.Object value)
    {
        var serialized = new SerializedObject(component); serialized.FindProperty(property).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
