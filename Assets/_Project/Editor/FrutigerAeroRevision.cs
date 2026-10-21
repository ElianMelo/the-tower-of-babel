using System;
using System.Linq;
using TMPro;
using TowerOfBabel;
using TowerOfBabel.Interface;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public static partial class FrutigerAeroBuilder
{
    [MenuItem("Tower of Babel/UI/Apply Compact Menu Revision")]
    public static void ApplyCompactRevision()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets")
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        LoadStyle();
        var input=EnsureMenuInput();
        EditScreen("HUD",root => { ClearChildren(root); ConfigureMenuLauncher(root); });
        EditScreen("Inventory",root =>
        {
            var ui=root.GetComponent<InventoryUI>();
            var definitions=new SerializedObject(ui).FindProperty("definitionCollection").objectReferenceValue;
            ConfigureCompactInventory(root,ui,input,definitions);
        });
        EditScreen("AnimationEmote",root => ConfigureQuickEmotes(root,root.GetComponent<AnimationEmoteUI>(),input));
        StyleUpgradeNode();
        var prefab=PrefabUtility.LoadPrefabContents(InterfacePath);
        try
        {
            Set(prefab.GetComponent<InterfaceManager>(),"menuLauncher",prefab.GetComponentInChildren<AeroDock>(true));
            PrefabUtility.SaveAsPrefabAsset(prefab,InterfacePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        ApplyInteractiveRevision();
    }

    private static void LoadStyle()
    {
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
        glass=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/FrostedGlass.mat");
        white=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/PearlGlass.mat");
        blue=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/AzureGlass.mat");
        green=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/MeadowGlass.mat");
        shadow=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Materials/GlassShadow.mat");
    }

    private static InputActionAsset EnsureMenuInput()
    {
        const string path="Assets/InputSystem_Actions.inputactions";
        var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
        var map=input.FindActionMap("Player",true);
        if(map.FindAction("Menus",false)==null)
        {
            map.AddAction("Menus",InputActionType.Button,"<Keyboard>/m");
            System.IO.File.WriteAllText(path,input.ToJson());
            AssetDatabase.ImportAsset(path);
        }
        return AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
    }

    private static void ConfigureMenuLauncher(RectTransform parent)
    {
        var launcher=Container("MenuLauncher",parent);
        Anchor(launcher,new(1,0),new(1,0),new(1,0),new(-28,24),new(232,54));
        var actions=launcher.gameObject.AddComponent<AeroDock>();
        var toggle=Button("MenusButton",launcher,"M   Menus"); Stretch(toggle);
        toggle.GetComponent<AeroGlassGeometry>().CornerRadius=27;
        UnityEventTools.AddPersistentListener(toggle.GetComponent<UnityEngine.UI.Button>().onClick,actions.Toggle);
        var list=Panel("MenuList",launcher,glass,24);
        Anchor(list,new(1,1),new(1,1),new(1,0),new(0,12),new(274,204));
        list.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var group=list.gameObject.AddComponent<CanvasGroup>(); group.alpha=0; group.blocksRaycasts=false;
        var up=Button("Upgrades",list,"Upgrades          Tab"); At(up,12,14,250,50);
        var inv=Button("Inventory",list,"Inventory              I"); At(inv,12,77,250,50);
        var em=Button("Emotes",list,"Emotes                 R"); At(em,12,140,250,50);
        UnityEventTools.AddPersistentListener(up.GetComponent<UnityEngine.UI.Button>().onClick,actions.Upgrades);
        UnityEventTools.AddPersistentListener(inv.GetComponent<UnityEngine.UI.Button>().onClick,actions.Inventory);
        UnityEventTools.AddPersistentListener(em.GetComponent<UnityEngine.UI.Button>().onClick,actions.Emotes);
        Set(actions,"menuList",list.gameObject); Set(actions,"listGroup",group); Set(actions,"inputActions",EnsureMenuInput());
        list.gameObject.SetActive(false);
        var focus=Inst("SparkleIcon",parent);
        Anchor(focus,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),Vector2.zero,new(12,12)); focus.name="Focus";
        focus.GetComponent<AeroIcon>().color=new(1,1,1,.8f);
    }

    private static void ConfigureCompactInventory(RectTransform controller,InventoryUI ui,InputActionAsset input,UnityEngine.Object definitions)
    {
        var visuals=GetVisuals(controller); ClearChildren(visuals);
        Anchor(visuals,new(1,.5f),new(1,.5f),new(1,.5f),new(-28,0),new(540,390));
        var background=Panel("WindowGlass",visuals,glass,28); Stretch(background); background.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        MakeSmallHeader(visuals,"Inventory",AeroIconKind.Bag,ui.Hide,false);
        var scroll=Container("ResourceScroll",visuals); Stretch(scroll,16,16,96,48);
        var sr=scroll.gameObject.AddComponent<UnityEngine.UI.ScrollRect>(); sr.horizontal=false; sr.scrollSensitivity=32;
        sr.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped;
        var viewport=Container("Viewport",scroll); Stretch(viewport);
        viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
        viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        var content=Container("Resources",viewport);
        Anchor(content,new(0,1),new(1,1),new(.5f,1),Vector2.zero,Vector2.zero);
        var layout=content.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>(); layout.spacing=10;
        layout.padding=new RectOffset(4,4,4,4); layout.childControlHeight=true; layout.childControlWidth=true;
        layout.childForceExpandHeight=false; layout.childForceExpandWidth=true;
        content.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit=UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
        sr.viewport=viewport; sr.content=content;
        var hint=Label("Footer",visuals,"I / Esc   Close backpack",16,Muted);
        Anchor(hint.rectTransform,new(0,0),new(1,0),new(.5f,0),new(0,12),new(-48,26));
        Set(ui,"visuals",visuals.gameObject); Set(ui,"inputActions",input); Set(ui,"definitionCollection",definitions);
        Set(ui,"targetListResources",content);
        Set(ui,"resourcePrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/ResourceInventory.prefab"));
        WireEscape(visuals,ui.Hide); visuals.gameObject.SetActive(false);
        StyleResourceRow();
    }

    private static void ConfigureQuickEmotes(RectTransform controller,AnimationEmoteUI ui,InputActionAsset input)
    {
        var visuals=GetVisuals(controller); ClearChildren(visuals);
        Anchor(visuals,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),new(0,-70),new(500,420));
        MakeSmallHeader(visuals,"Emotes",AeroIconKind.Music,ui.Hide,true);
        var options=Container("Options",visuals); Stretch(options,0,0,60,20);
        var orb=Orb("EmoteOrb",options,AeroIconKind.Sparkle,Blue);
        Anchor(orb,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),Vector2.zero,new(56,56));
        orb.GetComponent<UnityEngine.UI.Image>().material=Mat("QuickEmoteGlass",new(.65f,.91f,1,.32f),.3f,.35f,.8f);
        var hint=Label("Hint",options,"Jump emotes\nR / Esc",15,Ink); hint.alignment=TextAlignmentOptions.Center;
        Anchor(hint.rectTransform,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),new(0,-51),new(130,42));
        Set(ui,"visuals",visuals.gameObject); Set(ui,"inputActions",input); Set(ui,"optionsContainer",options);
        SetFloat(ui,"radialRadius",130);
        Set(ui,"optionPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/OptionButton.prefab").GetComponent<OptionButton>());
        WireEscape(visuals,ui.Hide); visuals.gameObject.SetActive(false);
        string path="Assets/_Project/Prefabs/Interface/OptionButton.prefab";
        var option=PrefabUtility.LoadPrefabContents(path);
        try
        {
            ((RectTransform)option.transform).sizeDelta=new(136,44);
            option.GetComponent<UnityEngine.UI.Image>().material=Mat("QuickEmoteGlass",new(.65f,.91f,1,.32f),.3f,.35f,.8f);
            var label=option.GetComponentInChildren<TMP_Text>(); label.color=Ink; label.fontSize=18;
            PrefabUtility.SaveAsPrefabAsset(option,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(option); }
    }

    private static void MakeSmallHeader(RectTransform parent,string title,AeroIconKind kind,UnityAction close,bool floating)
    {
        var bar=Panel("Titlebar",parent,floating ? Mat("QuickEmoteGlass",new(.65f,.91f,1,.32f),.3f,.35f,.8f) : white,24);
        if(floating) Anchor(bar,new(.5f,1),new(.5f,1),new(.5f,1),Vector2.zero,new(252,46));
        else Anchor(bar,new(0,1),new(1,1),new(.5f,1),new(0,-8),new(-16,76));
        var icon=Inst(kind+"Icon",bar); At(icon,16,floating ? 10 : 20,28,28); icon.GetComponent<AeroIcon>().color=Green;
        LabelAt("Title",bar,title,floating ? 21 : 28,Ink,56,floating ? 4 : 12,floating ? 146 : 360,floating ? 38 : 48,FontStyles.Bold);
        var button=Button("CloseButton",bar,"");
        Anchor(button,new(1,.5f),new(1,.5f),new(1,.5f),new(-10,0),new(30,30));
        var x=Inst("CloseIcon",button); Stretch(x,7,7,7,7); x.GetComponent<AeroIcon>().color=Color.white;
        UnityEventTools.AddPersistentListener(button.GetComponent<UnityEngine.UI.Button>().onClick,close);
    }

    private static void StyleResourceRow()
    {
        const string path="Assets/_Project/Prefabs/Interface/ResourceInventory.prefab";
        var row=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var root=(RectTransform)row.transform; root.sizeDelta=new(500,86);
            var layout=row.GetComponent<UnityEngine.UI.LayoutElement>(); layout.minHeight=layout.preferredHeight=86;
            At((RectTransform)root.Find("IconWell"),16,12,60,60);
            var name=(RectTransform)root.Find("ResourceName"); At(name,90,12,350,28); name.GetComponent<TMP_Text>().fontSize=22;
            var amount=(RectTransform)root.Find("Amount"); At(amount,90,42,190,28);
            amount.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.MidlineLeft; amount.GetComponent<TMP_Text>().fontSize=25;
            if(root.Find("Description")!=null) UnityEngine.Object.DestroyImmediate(root.Find("Description").gameObject);
            PrefabUtility.SaveAsPrefabAsset(row,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(row); }
    }

    private static void StyleUpgradeNode()
    {
        const string path="Assets/_Project/Prefabs/Interface/UpgradeButton.prefab";
        var node=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view=node.GetComponent<UpgradeButton>();
            Set(view,"availableMaterial",Mat("AvailableUpgradeGlass",new(.02f,.38f,.86f,1),1,.55f,0));
            Set(view,"lockedMaterial",white); Set(view,"purchasedMaterial",green);
            var outline=node.GetComponent<UnityEngine.UI.Outline>(); if(outline==null) outline=node.AddComponent<UnityEngine.UI.Outline>();
            outline.effectDistance=new(2,-2); outline.effectColor=new(.65f,.96f,1,1); outline.enabled=false;
            Set(view,"availableOutline",outline);
            PrefabUtility.SaveAsPrefabAsset(node,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(node); }
    }

    private static RectTransform GetVisuals(RectTransform root)
    { return root.Find("Visuals") is RectTransform rect ? rect : Container("Visuals",root); }
    private static void ClearChildren(Transform parent)
    { foreach(Transform child in parent.Cast<Transform>().ToArray()) UnityEngine.Object.DestroyImmediate(child.gameObject); }
    private static void WireEscape(RectTransform root,UnityAction callback)
    {
        var window=root.GetComponent<AeroWindow>(); if(window==null) window=root.gameObject.AddComponent<AeroWindow>();
        var field=typeof(AeroWindow).GetField("close",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        var evt=new UnityEvent(); field.SetValue(window,evt); UnityEventTools.AddPersistentListener(evt,callback); EditorUtility.SetDirty(window);
    }
    private static void EditScreen(string name,Action<RectTransform> edit)
    {
        string path=Root+"/Prefabs/Screens/"+name+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try { edit((RectTransform)root.transform); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
