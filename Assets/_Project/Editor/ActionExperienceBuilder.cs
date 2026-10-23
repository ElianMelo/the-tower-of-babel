using System;
using TMPro;
using TowerOfBabel.Interface;
using UnityEditor;
using UnityEngine;

/// <summary>Authors only the action-reward prefabs and adds one nested screen to the existing interface.</summary>
public static class ActionExperienceBuilder
{
    private const string Root = FrutigerAeroBuilder.Root;
    public const string ScreenPath = Root + "/Prefabs/Screens/ActionExperience.prefab";
    private static readonly Color Ink = new(.035f, .22f, .34f, 1);

    [MenuItem("Tower of Babel/UI/Build Action Experience Feedback")]
    public static void Build()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use the Tower of Babel Editor in Edit Mode.");
        var shader = Shader.Find("TowerOfBabel/UI/Experience Bubble");
        if (shader == null) throw new InvalidOperationException("Import the experience bubble shader first.");
        var bubbleMaterial = Material("ExperienceBubble", shader);
        var popMaterial = Material("ExperiencePop", shader);
        popMaterial.SetFloat("_RingOnly", 1);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.BoldPath);
        var captionMaterial = Material("ExperienceCaption", font.material.shader);
        captionMaterial.CopyPropertiesFromMaterial(font.material);
        captionMaterial.SetColor("_OutlineColor", new Color(.02f, .16f, .27f, .9f));
        captionMaterial.SetFloat("_OutlineWidth", .22f);
        captionMaterial.EnableKeyword("OUTLINE_ON");
        EditorUtility.SetDirty(captionMaterial);
        EditorUtility.SetDirty(popMaterial);

        SaveParticle("ExperienceBubble", bubbleMaterial, 40);
        SaveParticle("ExperiencePop", popMaterial, 64);
        var caption = Rect("ExperienceFloatingText", null, new(760, 64));
        var captionText = caption.gameObject.AddComponent<TextMeshProUGUI>();
        captionText.font = font; captionText.fontSharedMaterial = captionMaterial;
        captionText.fontSize = 24; captionText.alignment = TextAlignmentOptions.Center;
        captionText.textWrappingMode = TextWrappingModes.NoWrap;
        captionText.enableAutoSizing = true; captionText.fontSizeMin = 14; captionText.fontSizeMax = 24;
        captionText.raycastTarget = false;
        captionText.text = "+1 XP · Gathered 1 Stone";
        Save(caption, Root + "/Prefabs/ExperienceFloatingText.prefab");

        var bar = Instance("GlassPanel", null);
        bar.name = "ExperienceBar";
        bar.sizeDelta = new(560, 104);
        bar.GetComponent<AeroGlassGeometry>().CornerRadius = 25;
        bar.GetComponent<UnityEngine.UI.Image>().material =
            AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/PearlGlass.mat");
        var shade = Instance("GlassPanel", bar);
        shade.name = "DropShadow"; Stretch(shade);
        shade.GetComponent<AeroGlassGeometry>().CornerRadius = 25;
        shade.GetComponent<UnityEngine.UI.Image>().material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/GlassShadow.mat");
        shade.SetAsFirstSibling();
        var icon = Rect("ActionIcon", bar, new(40, 40));
        Place(icon, new(0, 1), new(0, 1), new(0, 1), new(18, -13), new(40, 40));
        icon.gameObject.AddComponent<UnityEngine.UI.Image>().raycastTarget = false;
        var profession = Label("Profession", bar, "Gather   ·   Level 0", 21, Ink);
        Place(profession.rectTransform, new(0, 1), new(1, 1), new(0, 1), new(70, -13), new(-225, 28));
        var count = Label("Experience", bar, "0 / 10 XP", 19, Ink);
        count.alignment = TextAlignmentOptions.Right;
        Place(count.rectTransform, new(1, 1), new(1, 1), new(1, 1), new(-20, -13), new(150, 28));
        var action = Label("Action", bar, "Complete an action to earn experience", 16, new(.14f, .37f, .47f));
        action.enableAutoSizing = true; action.fontSizeMin = 12; action.fontSizeMax = 16;
        Place(action.rectTransform, new(0, 1), new(1, 1), new(0, 1), new(70, -42), new(-90, 24));

        var track = Instance("GlassPanel", bar);
        track.name = "Track";
        Place(track, new(0, 0), new(1, 0), new(.5f, 0), new(0, 16), new(-40, 14));
        track.GetComponent<AeroGlassGeometry>().CornerRadius = 7;
        track.GetComponent<UnityEngine.UI.Image>().material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/GlassShadow.mat");
        var shadow = Fill("ShadowFill", track, new(.20f, .63f, .76f, .6f));
        var fill = Fill("Fill", track, new(.23f, .86f, .46f, 1));
        fill.rectTransform.offsetMin = new(2, 4);
        var shine = Fill("Shine", track, new(.89f, 1, 1, .65f));
        shine.rectTransform.anchorMin = new(0, .58f); shine.rectTransform.anchorMax = Vector2.one;
        shine.rectTransform.offsetMin = new(2, 0); shine.rectTransform.offsetMax = new(-2, -2);
        var glow = Instance("GlassPanel", bar);
        glow.name = "ArrivalGlow"; Stretch(glow);
        glow.GetComponent<UnityEngine.UI.Image>().material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/PearlGlass.mat");
        glow.GetComponent<UnityEngine.UI.Image>().color = Color.clear;
        glow.GetComponent<AeroGlassGeometry>().CornerRadius = 25;
        // Keep text above the flash.
        icon.SetAsLastSibling(); profession.transform.SetAsLastSibling(); count.transform.SetAsLastSibling(); action.transform.SetAsLastSibling();
        bar.gameObject.AddComponent<CanvasGroup>();
        Save(bar, Root + "/Prefabs/ExperienceBar.prefab");

        var screen = Rect("ActionExperience", null, Vector2.zero); Stretch(screen);
        var ui = screen.gameObject.AddComponent<ActionExperienceUI>();
        SetBurstCounts(ui);
        var visibility = screen.gameObject.AddComponent<CanvasGroup>();
        visibility.interactable = visibility.blocksRaycasts = false;
        var barInstance = Instance("ExperienceBar", screen);
        Place(barInstance, Vector2.zero, Vector2.zero, Vector2.zero, new(24, 28), new(560, 104));
        var effects = Rect("Particles", screen, Vector2.zero); Stretch(effects);
        var source = screen.gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 0; source.volume = .7f;
        Set(ui, "effectsLayer", effects); Set(ui, "bar", barInstance);
        Set(ui, "barVisibility", barInstance.GetComponent<CanvasGroup>());
        Set(ui, "track", barInstance.Find("Track"));
        Set(ui, "fill", barInstance.Find("Track/Fill").GetComponent<UnityEngine.UI.Image>());
        Set(ui, "shadowFill", barInstance.Find("Track/ShadowFill").GetComponent<UnityEngine.UI.Image>());
        Set(ui, "fillShine", barInstance.Find("Track/Shine").GetComponent<UnityEngine.UI.Image>());
        Set(ui, "arrivalGlow", barInstance.Find("ArrivalGlow").GetComponent<UnityEngine.UI.Image>());
        Set(ui, "professionText", barInstance.Find("Profession").GetComponent<TMP_Text>());
        Set(ui, "experienceText", barInstance.Find("Experience").GetComponent<TMP_Text>());
        Set(ui, "actionText", barInstance.Find("Action").GetComponent<TMP_Text>());
        Set(ui, "actionIcon", barInstance.Find("ActionIcon").GetComponent<UnityEngine.UI.Image>());
        foreach (string job in new[] { "gather", "process", "build" })
            Set(ui, job + "Icon", AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/" + char.ToUpperInvariant(job[0]) + job.Substring(1) + "Small.png"));
        Set(ui, "worldBubblePrefab", CreateWorldBubble());
        Set(ui, "popPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/ExperiencePop.prefab").GetComponent<UnityEngine.UI.Image>());
        Set(ui, "floatingTextPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/ExperienceFloatingText.prefab").GetComponent<TMP_Text>());
        Set(ui, "audioSource", source);
        barInstance.GetComponent<CanvasGroup>().alpha = 0;
        Save(screen, ScreenPath);
        AttachToInterface();
        AssetDatabase.SaveAssets();
        Debug.Log("Action experience feedback authored and attached to the existing interface prefab.");
    }

    public static void AttachToInterface()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ScreenPath);
        if (asset == null) return;
        var root = PrefabUtility.LoadPrefabContents(FrutigerAeroBuilder.InterfacePath);
        try
        {
            if (root.transform.Find("ActionExperience") == null)
            {
                var screen = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
                Stretch((RectTransform)screen.transform);
            }
            PrefabUtility.SaveAsPrefabAsset(root, FrutigerAeroBuilder.InterfacePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    [MenuItem("Tower of Babel/UI/Apply Experience Bubble Counts")]
    public static void ApplyBurstCounts()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use the Tower of Babel Editor in Edit Mode.");
        var screen = PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            SetBurstCounts(screen.GetComponent<ActionExperienceUI>());
            PrefabUtility.SaveAsPrefabAsset(screen, ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); }
    }

    private static void SetBurstCounts(ActionExperienceUI ui)
    {
        var data = new SerializedObject(ui);
        data.FindProperty("bubbleCount").intValue = 35;
        data.FindProperty("levelUpBubbleMultiplier").intValue = 10;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void ApplyWorldBubbles()
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        var particle=CreateWorldBubble();
        var screen=PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            var ui=screen.GetComponent<ActionExperienceUI>();
            SetBurstCounts(ui); Set(ui,"worldBubblePrefab",particle);
            PrefabUtility.SaveAsPrefabAsset(screen,ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); }
        AssetDatabase.SaveAssets();
    }

    private static MeshRenderer CreateWorldBubble()
    {
        const string meshPath=Root+"/Art/ExperienceBubbleSphere.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh==null)
        {
            const int segments=20,rings=12;
            var vertices=new System.Collections.Generic.List<Vector3>();
            var normals=new System.Collections.Generic.List<Vector3>();
            var triangles=new System.Collections.Generic.List<int>();
            for (int y=0; y<=rings; y++) for (int x=0; x<=segments; x++)
            {
                float latitude=Mathf.PI*y/rings,longitude=2*Mathf.PI*x/segments;
                var normal=new Vector3(Mathf.Sin(latitude)*Mathf.Cos(longitude),Mathf.Cos(latitude),Mathf.Sin(latitude)*Mathf.Sin(longitude));
                normals.Add(normal); vertices.Add(normal*.5f);
                if (y==rings || x==segments) continue;
                int a=y*(segments+1)+x,b=a+segments+1;
                triangles.Add(a); triangles.Add(a+1); triangles.Add(b);
                triangles.Add(a+1); triangles.Add(b+1); triangles.Add(b);
            }
            mesh=new Mesh {name="Experience bubble sphere"}; mesh.SetVertices(vertices); mesh.SetNormals(normals);
            mesh.SetTriangles(triangles,0); mesh.RecalculateBounds(); AssetDatabase.CreateAsset(mesh,meshPath);
        }
        var shader=Shader.Find("TowerOfBabel/World/Experience Bubble");
        if (shader==null) throw new InvalidOperationException("Import the world bubble shader first.");
        var material=Material("ExperienceWorldBubble",shader); material.enableInstancing=true; EditorUtility.SetDirty(material);
        var go=new GameObject("ExperienceWorldBubble",typeof(MeshFilter),typeof(MeshRenderer));
        try
        {
            go.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            renderer.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;
            return PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/ExperienceWorldBubble.prefab").GetComponent<MeshRenderer>();
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [MenuItem("Tower of Babel/UI/Apply Compact Reward And Interaction Layout")]
    public static void ApplyCompactLayout()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" ||
            EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use the Tower of Babel Editor in Edit Mode.");
        var bar = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/ExperienceBar.prefab");
        try
        {
            ((RectTransform)bar.transform).sizeDelta = new(560, 104);
            Stretch((RectTransform)bar.transform.Find("DropShadow"));
            bar.transform.Find("DropShadow").GetComponent<AeroGlassGeometry>().CornerRadius = 25;
            Stretch((RectTransform)bar.transform.Find("ArrivalGlow"));
            bar.transform.Find("ArrivalGlow").GetComponent<AeroGlassGeometry>().CornerRadius = 25;
            PrefabUtility.SaveAsPrefabAsset(bar, Root + "/Prefabs/ExperienceBar.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(bar); }
        var screen = PrefabUtility.LoadPrefabContents(ScreenPath);
        try
        {
            Place((RectTransform)screen.transform.Find("ExperienceBar"), Vector2.zero, Vector2.zero,
                Vector2.zero, new(24, 28), new(560, 104));
            PrefabUtility.SaveAsPrefabAsset(screen, ScreenPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(screen); }
        var interaction = PrefabUtility.LoadPrefabContents(Root + "/Prefabs/Screens/Interaction.prefab");
        try
        {
            FrutigerAeroBuilder.ConfigureCompactActionPrompt((RectTransform)interaction.transform);
            PrefabUtility.SaveAsPrefabAsset(interaction, Root + "/Prefabs/Screens/Interaction.prefab");
        }
        finally { PrefabUtility.UnloadPrefabContents(interaction); }
    }

    private static UnityEngine.UI.Image Fill(string name, Transform parent, Color color)
    {
        var rect = Rect(name, parent, Vector2.zero); Stretch(rect, 2, 2, 2, 2);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Root + "/Art/ProgressPixel.png");
        image.type = UnityEngine.UI.Image.Type.Filled; image.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        image.fillOrigin = 0; image.fillAmount = 0; image.color = color; image.raycastTarget = false;
        return image;
    }
    private static void SaveParticle(string name, Material material, float size)
    {
        var rect = Rect(name, null, Vector2.one * size);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.material = material; image.raycastTarget = false;
        Save(rect, Root + "/Prefabs/" + name + ".prefab");
    }
    private static Material Material(string name, Shader shader)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        return material;
    }
    private static TMP_Text Label(string name, Transform parent, string text, float size, Color color)
    {
        var rect = Instance("Label", parent); rect.name = name;
        var label = rect.GetComponent<TMP_Text>();
        label.text = text; label.fontSize = size; label.color = color;
        label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        return label;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false); rect.sizeDelta = size;
        return rect;
    }
    private static RectTransform Instance(string name, Transform parent)
        => (RectTransform)((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/" + name + ".prefab"), parent)).transform;
    private static void Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
    {
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.pivot = new(.5f, .5f);
        rect.offsetMin = new(left, bottom); rect.offsetMax = new(-right, -top);
    }
    private static void Place(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot, Vector2 position, Vector2 size)
    { rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position; }
    private static void Save(RectTransform rect, string path)
    { PrefabUtility.SaveAsPrefabAsset(rect.gameObject, path); UnityEngine.Object.DestroyImmediate(rect.gameObject); }
    private static void Set(UnityEngine.Object target, string name, UnityEngine.Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(name).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
