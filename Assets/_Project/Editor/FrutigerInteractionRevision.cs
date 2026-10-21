using System;
using System.Linq;
using TMPro;
using TowerOfBabel;
using TowerOfBabel.Interface;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

public static partial class FrutigerAeroBuilder
{
    [MenuItem("Tower of Babel/UI/Apply Interactive Aero Revision")]
    public static void ApplyInteractiveRevision()
    {
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project's Editor in Edit Mode.");
        LoadStyle();
        ConfigureResourceArtwork();
        CreateInteractivePrimitives();
        EditScreen("Inventory",root=>
        {
            var bar=root.Find("Visuals/Titlebar");
            var old=bar.Find("BagIcon"); if(old!=null) old.gameObject.SetActive(false);
            var icon=bar.Find("InventoryIcon") as RectTransform;
            if(icon==null) { icon=Inst("InventoryIcon",bar); icon.name="InventoryIcon"; }
            At(icon,12,7,60,60);
            At((RectTransform)bar.Find("Title"),80,12,340,48);
            var list=root.Find("Visuals/ResourceScroll");
            var hover=list.GetComponent<AeroHoverFeedback>(); if(hover==null) hover=list.gameObject.AddComponent<AeroHoverFeedback>();
            Set(hover,"surface",root.Find("Visuals/WindowGlass").GetComponent<AeroGlassGeometry>());
        });
        EditScreen("Upgrade",ConfigureExplorableUpgrade);
        EditScreen("AnimationEmote",ConfigureCenteredEmotes);
        EditScreen("Interaction",ConfigureResourcePrompt);
        EditScreen("HUD",HighlightShortcuts);
        EditPrefab("Assets/_Project/Prefabs/Interface/UpgradeButton.prefab",node=>
        {
            ((RectTransform)node.transform).sizeDelta=UpgradeBoardLayout.NodeSize;
            Set(node.GetComponent<UpgradeButton>(),"hoverEffect",null);
            Set(node.GetComponent<UpgradeButton>(),"hoverEffectImage",null);
            var oldHover=node.transform.Find("Hover"); if(oldHover!=null) oldHover.gameObject.SetActive(false);
            var label=node.transform.Find("Label").GetComponent<TMP_Text>();
            label.enableAutoSizing=false; label.fontSize=20;
            Stretch(label.rectTransform,9,9,8,27);
            var state=node.transform.Find("State").GetComponent<TMP_Text>(); state.fontSize=13;
            Anchor(state.rectTransform,new(0,0),new(1,0),new(.5f,0),new(0,5),new(-18,18));
        });
        EditPrefab("Assets/_Project/Prefabs/Interface/OptionButton.prefab",node=>
        {
            node.GetComponent<UnityEngine.UI.Image>().material=Mat("SolidEmoteGlass",new(.56f,.89f,1,1),1,.6f,0);
            Set(node.GetComponent<OptionButton>(),"hoverEffect",null);
            var oldHover=node.transform.Find("Hover"); if(oldHover!=null) oldHover.gameObject.SetActive(false);
            var label=node.GetComponentInChildren<TMP_Text>(); label.color=Ink; label.fontSize=18;
        });
        EditPrefab("Assets/_Project/Prefabs/Interface/ResourceInventory.prefab",node=>
        {
            var cube=node.transform.Find("IconWell/CubeIcon"); if(cube!=null) cube.gameObject.SetActive(false);
            Stretch((RectTransform)node.transform.Find("IconWell/ResourceIcon"),3,3,3,3);
        });
        // Add feedback to sources, so every nested instance and runtime entry inherits it.
        var prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Project/UI/FrutigerAero","Assets/_Project/Prefabs/Interface"})
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>AssetDatabase.GetDependencies(p).Length).ToArray();
        foreach(var path in prefabs)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(!asset.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.GetComponent<AeroButtonJuice>()==null) &&
               !asset.GetComponentsInChildren<UnityEngine.UI.Selectable>(true).Any(s=>s.GetComponent<AeroHoverFeedback>()==null)) continue;
            EditPrefab(path,root=>
            {
                foreach(var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    if(button.GetComponent<AeroButtonJuice>()==null) button.gameObject.AddComponent<AeroButtonJuice>();
                foreach(var control in root.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
                {
                    var hover=control.GetComponent<AeroHoverFeedback>(); if(hover==null) hover=control.gameObject.AddComponent<AeroHoverFeedback>();
                    Set(hover,"surface",control.targetGraphic!=null ? control.targetGraphic.GetComponent<AeroGlassGeometry>() : control.GetComponent<AeroGlassGeometry>());
                }
            });
        }
        ConfigurePanelSurfaceFeedback();
        AssetDatabase.SaveAssets();
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    private static void CreateInteractivePrimitives()
    {
        string imagePath=Root+"/Art/InventorySmall.png";
        AssetDatabase.ImportAsset(imagePath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(imagePath);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=64;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/InventoryIcon.prefab")==null)
        {
            var icon=Container("InventoryIcon",null);
            var image=icon.gameObject.AddComponent<UnityEngine.UI.Image>(); image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(imagePath);
            image.preserveAspect=true; image.raycastTarget=false; icon.sizeDelta=new(64,64); Save(icon,"InventoryIcon");
        }
        EditPrefab(Root+"/Prefabs/InventoryIcon.prefab",icon=>
        {
            var image=icon.GetComponent<UnityEngine.UI.Image>(); image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(imagePath); image.preserveAspect=true;
        });
        ConfigureGeneratedUpgradeIcon();
        ConfigureGeneratedActionIcons();
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/EmoteCenter.prefab")==null)
        {
            var center=Button("EmoteCenter",null,""); center.sizeDelta=new(56,56);
            center.GetComponent<AeroGlassGeometry>().CornerRadius=28;
            center.GetComponent<UnityEngine.UI.Image>().material=blue;
            var close=Inst("CloseIcon",center); Stretch(close,18,18,18,18); close.GetComponent<AeroIcon>().color=Color.white;
            Save(center,"EmoteCenter");
        }
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/UpgradeTooltip.prefab")==null)
        {
            var pane=Panel("UpgradeTooltip",null,Mat("TooltipGlass",new(.88f,.98f,1,.98f),1,.45f,0),22);
            pane.sizeDelta=new(420,320); pane.GetComponent<UnityEngine.UI.Image>().raycastTarget=false;
            var view=pane.gameObject.AddComponent<UpgradeTooltip>();
            var group=pane.gameObject.AddComponent<CanvasGroup>(); group.alpha=0; group.blocksRaycasts=false; group.interactable=false;
            var title=LabelAt("Title",pane,"Upgrade",26,Ink,22,16,376,38,FontStyles.Bold);
            var body=LabelAt("Description",pane,"",19,Ink,22,62,376,198); body.alignment=TextAlignmentOptions.TopLeft;
            var status=LabelAt("Status",pane,"",16,Blue,22,270,376,32,FontStyles.Bold);
            Set(view,"group",group); Set(view,"title",title); Set(view,"description",body); Set(view,"status",status);
            Save(pane,"UpgradeTooltip");
        }
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/ResourceSprite.prefab")==null)
        {
            var sprite=Container("ResourceSprite",null); sprite.sizeDelta=new(64,64);
            var image=sprite.gameObject.AddComponent<UnityEngine.UI.Image>(); image.raycastTarget=false; image.preserveAspect=true;
            Save(sprite,"ResourceSprite");
        }
    }

    private static void ConfigureGeneratedUpgradeIcon()
    {
        string path=Root+"/Art/UpgradesSmall.png";
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=64;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        string prefabPath=Root+"/Prefabs/UpgradesIcon.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath)==null)
        {
            var icon=Container("UpgradesIcon",null); icon.gameObject.AddComponent<UnityEngine.UI.Image>();
            icon.sizeDelta=new(76,76); Save(icon,"UpgradesIcon");
        }
        EditPrefab(prefabPath,icon=>
        {
            var image=icon.GetComponent<UnityEngine.UI.Image>(); image.material=null; image.color=Color.white;
            image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path); image.preserveAspect=true; image.raycastTarget=false;
            var geometry=icon.GetComponent<AeroGlassGeometry>(); if(geometry!=null) geometry.enabled=false;
            foreach(Transform child in icon.transform) child.gameObject.SetActive(false);
        });
    }

    private static void ConfigureExplorableUpgrade(RectTransform root)
    {
        var ui=root.GetComponent<UpgradeUI>(); var visuals=root.Find("Visuals");
        visuals.Find("Titlebar/Title").GetComponent<TMP_Text>().text="Upgrades";
        visuals.Find("Titlebar/Subtitle").GetComponent<TMP_Text>().text="Discover your next skill. Shape your path.";
        var oldIcon=visuals.Find("Titlebar/TitleIcon"); if(oldIcon!=null) oldIcon.gameObject.SetActive(false);
        var icon=visuals.Find("Titlebar/UpgradesIcon") as RectTransform;
        if(icon==null) icon=Inst("UpgradesIcon",visuals.Find("Titlebar")); At(icon,16,17,68,68);
        var rail=visuals.Find("SkillRail");
        var art=rail.Find("Landscape"); if(art!=null) UnityEngine.Object.DestroyImmediate(art.gameObject);
        At((RectTransform)rail.Find("Choose"),24,30,252,32);
        var choose=rail.Find("Choose").GetComponent<TMP_Text>(); choose.text="CHOOSE YOUR PATH"; choose.fontSize=19;
        string[] tabs={"GatherButton","ProcessorButton","BuilderButton"};
        for(int i=0;i<tabs.Length;i++)
        {
            var button=(RectTransform)rail.Find(tabs[i]); At(button,14,90+i*108,272,92);
            button.GetComponent<UnityEngine.UI.Image>().color=Color.white;
            var tab=button.GetComponent<UnityEngine.UI.Button>();
            var colors=tab.colors; colors.normalColor=Color.white; colors.selectedColor=Color.white;
            colors.highlightedColor=Color.white; tab.colors=colors;
            button.GetComponentInChildren<TMP_Text>().fontSize=28;
            button.GetComponentInChildren<TMP_Text>().fontStyle=FontStyles.Bold;
            foreach(var glyph in button.GetComponentsInChildren<AeroIcon>()) At(glyph.rectTransform,16,26,40,40);
            string[] actionNames={"Gather","Process","Build"};
            var actionGlyph=button.GetComponentInChildren<AeroIcon>(true);
            ConfigureActionArtwork(actionGlyph.transform,actionNames[i]);
            var outline=button.GetComponent<UnityEngine.UI.Outline>(); if(outline==null) outline=button.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectDistance=new(3,-3); outline.effectColor=new(.7f,1,.84f,1); outline.enabled=i==0;
        }
        var instructions=rail.Find("Instructions").GetComponent<TMP_Text>();
        instructions.text="Drag the tree to explore.\nHover a skill for details.\nUnlock adjacent skills.";
        At(instructions.rectTransform,24,430,252,110);
        var well=(RectTransform)visuals.Find("BoardWell");
        var viewport=well.Find("Viewport") as RectTransform;
        if(viewport==null)
        {
            viewport=Container("Viewport",well); Stretch(viewport,6,6,6,6);
            var maskImage=viewport.gameObject.AddComponent<UnityEngine.UI.Image>(); maskImage.color=Color.white;
            viewport.gameObject.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic=false;
        }
        var scroll=well.GetComponent<UnityEngine.UI.ScrollRect>(); if(scroll==null) scroll=well.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
        var hover=well.GetComponent<AeroHoverFeedback>(); if(hover==null) hover=well.gameObject.AddComponent<AeroHoverFeedback>();
        Set(hover,"surface",well.GetComponent<AeroGlassGeometry>());
        scroll.viewport=viewport; scroll.horizontal=true; scroll.vertical=true;
        scroll.movementType=UnityEngine.UI.ScrollRect.MovementType.Clamped; scroll.inertia=true; scroll.decelerationRate=.08f; scroll.scrollSensitivity=55;
        foreach(string name in new[]{"GatherBoard","ProcessorBoard","BuilderBoard"})
        {
            var board=(well.Find(name)??viewport.Find(name)) as RectTransform;
            board.SetParent(viewport,false);
            Anchor(board,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),Vector2.zero,UpgradeBoardLayout.TreeSize);
            board.gameObject.SetActive(name=="GatherBoard");
        }
        scroll.content=(RectTransform)viewport.Find("GatherBoard");
        var tooltip=visuals.Find("UpgradeTooltip") as RectTransform;
        if(tooltip==null) tooltip=Inst("UpgradeTooltip",visuals);
        Anchor(tooltip,new(.5f,.5f),new(.5f,.5f),new(0,1),Vector2.zero,new(420,320));
        Set(ui,"treeScroll",scroll); Set(ui,"tooltip",tooltip.GetComponent<UpgradeTooltip>());
        var pan=well.GetComponent<UpgradeTreePan>(); if(pan==null) pan=well.gameObject.AddComponent<UpgradeTreePan>(); Set(pan,"tooltip",tooltip.GetComponent<UpgradeTooltip>());
        visuals.Find("BoardHint").GetComponent<TMP_Text>().text="Drag to explore. Hover a skill for details.";
        visuals.Find("Legend").GetComponent<TMP_Text>().text="BLUE  Available    GREEN  Owned    Hidden skills reveal as you grow";
    }

    private static void ConfigureCenteredEmotes(RectTransform root)
    {
        var ui=root.GetComponent<AnimationEmoteUI>(); var visuals=(RectTransform)root.Find("Visuals");
        Anchor(visuals,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),Vector2.zero,new(500,420));
        var titlebar=visuals.Find("Titlebar"); if(titlebar!=null) UnityEngine.Object.DestroyImmediate(titlebar.gameObject);
        var options=(RectTransform)visuals.Find("Options"); Stretch(options);
        foreach(string name in new[]{"Hint","EmoteOrb"})
        { var old=options.Find(name); if(old!=null) UnityEngine.Object.DestroyImmediate(old.gameObject); }
        var center=options.Find("CenterClose") as RectTransform;
        if(center==null)
        {
            center=Inst("EmoteCenter",options); center.name="CenterClose";
            UnityEventTools.AddPersistentListener(center.GetComponent<UnityEngine.UI.Button>().onClick,ui.Hide);
        }
        Anchor(center,new(.5f,.5f),new(.5f,.5f),new(.5f,.5f),Vector2.zero,new(56,56));
    }

    private static void ConfigureResourcePrompt(RectTransform root)
    {
        ConfigureActionPrompt(root);
    }

    private static void HighlightShortcuts(RectTransform root)
    {
        var launcher=root.Find("MenuLauncher");
        launcher.Find("MenusButton/Label").GetComponent<TMP_Text>().text="<color=#C8FF78><b>M</b></color>   Menus";
        foreach(var item in new[]{("Upgrades","Upgrades","Tab"),("Inventory","Inventory","I"),("Emotes","Emotes","R")})
        {
            var label=launcher.Find("MenuList/"+item.Item1+"/Label").GetComponent<TMP_Text>();
            label.text=item.Item2+"  <color=#C8FF78><b>"+item.Item3+"</b></color>";
        }
        var upgrade=launcher.Find("MenuList/Upgrades");
        var icon=upgrade.Find("UpgradesIcon") as RectTransform;
        if(icon==null) icon=Inst("UpgradesIcon",upgrade);
        At(icon,10,7,36,36);
        var upgradeLabel=upgrade.Find("Label").GetComponent<TMP_Text>();
        Stretch(upgradeLabel.rectTransform,52,10,4,4); upgradeLabel.alignment=TextAlignmentOptions.MidlineLeft;
        var inventory=launcher.Find("MenuList/Inventory");
        var bag=inventory.Find("InventoryIcon") as RectTransform;
        if(bag==null) bag=Inst("InventoryIcon",inventory);
        At(bag,10,7,36,36);
        var inventoryLabel=inventory.Find("Label").GetComponent<TMP_Text>();
        Stretch(inventoryLabel.rectTransform,52,10,4,4); inventoryLabel.alignment=TextAlignmentOptions.MidlineLeft;
    }

    private static void EditPrefab(string path,Action<GameObject> edit)
    {
        var root=PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
