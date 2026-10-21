using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using TowerOfBabel;
using TowerOfBabel.Interface;
using TowerOfBabel.Resources;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Authors real nested prefabs. The runtime only instantiates data-driven rows/nodes.</summary>
public static partial class FrutigerAeroBuilder
{
    public const string Root = "Assets/_Project/UI/FrutigerAero";
    public const string InterfacePath = Root + "/Prefabs/FrutigerInterface.prefab";
    private static readonly Color Ink = new(.035f,.22f,.34f,1);
    private static readonly Color Muted = new(.14f,.37f,.47f,1);
    private static readonly Color Blue = new(.06f,.58f,.91f,1);
    private static readonly Color Green = new(.13f,.67f,.34f,1);
    private static TMP_FontAsset font;
    private static Material glass, blue, green, white, shadow;
    private static Sprite landscape;

    [MenuItem("Tower of Babel/UI/Rebuild Frutiger Aero Interface")]
    public static void Build()
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets")
            throw new InvalidOperationException("Wrong project.");
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build in Edit Mode only.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity") throw new InvalidOperationException("Open Tower.unity first.");
        System.IO.Directory.CreateDirectory(Root + "/Prefabs");
        System.IO.Directory.CreateDirectory(Root + "/Materials");
        System.IO.Directory.CreateDirectory(Root + "/Prefabs/Screens");
        AssetDatabase.Refresh();
        font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FrutigerFontBuilder.RegularPath);
        var importer=(TextureImporter)AssetImporter.GetAtPath(Root+"/Art/AeroLandscape.png");
        importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;
        importer.mipmapEnabled=false;
        importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        landscape=AssetDatabase.LoadAssetAtPath<Sprite>(Root+"/Art/AeroLandscape.png");
        ConfigureResourceArtwork();
        glass=Mat("FrostedGlass",new(.63f,.88f,.98f,.87f),.72f,.36f,1);
        blue=Mat("AzureGlass",new(.035f,.55f,.86f,.95f),.86f,.65f,0);
        green=Mat("MeadowGlass",new(.2f,.78f,.35f,.96f),.92f,.7f,0);
        white=Mat("PearlGlass",new(.85f,.96f,1,.91f),.9f,.32f,.35f);
        shadow=Mat("GlassShadow",new(.015f,.18f,.28f,.19f),1,0,0);
        CreatePrimitives();
        CreateDataPrefabs();

        var old=GameObject.Find("CanvasInterfaceManager");
        if(old==null) throw new InvalidOperationException("Original interface not found.");
        var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
        var oldInventory=old.GetComponentInChildren<InventoryUI>(true);
        var oldInventoryData=new SerializedObject(oldInventory);
        var wallet=oldInventoryData.FindProperty("wallet").objectReferenceValue;
        var definitions=oldInventoryData.FindProperty("definitionCollection").objectReferenceValue;
        var root=NewRect("FrutigerInterface",null);
        root.gameObject.SetActive(false);
        var canvas=root.gameObject.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        canvas.additionalShaderChannels=AdditionalCanvasShaderChannels.TexCoord1|AdditionalCanvasShaderChannels.TexCoord2;
        var scaler=root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new(1920,1080);
        scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var manager=root.gameObject.AddComponent<InterfaceManager>();

        var hud=Container("HUD",root); Stretch(hud);
        BuildHud(hud);
        var interaction=BuildInteraction(root);
        var information=BuildInformation(root);
        var server=BuildServer(root);
        var inventory=BuildInventory(root,input,definitions);
        var upgrade=BuildUpgrade(root,input);
        var emotes=BuildEmotes(root,input);
        Set(manager,"interactionUI",interaction); Set(manager,"inventoryUI",inventory);
        Set(manager,"upgradeUI",upgrade); Set(manager,"serverStatusUI",server);
        Set(manager,"animationEmoteUI",emotes);
        Set(manager,"menuLauncher",hud.GetComponentInChildren<AeroDock>(true));
        foreach(Transform child in root.Cast<Transform>().ToArray())
            PrefabUtility.SaveAsPrefabAssetAndConnect(child.gameObject,Root+"/Prefabs/Screens/"+child.name+".prefab",InteractionMode.AutomatedAction);

        // Store the whole interface as a reusable prefab, with scene dependencies only on its instance.
        root.gameObject.SetActive(true);
        var asset=PrefabUtility.SaveAsPrefabAsset(root.gameObject,InterfacePath);
        UnityEngine.Object.DestroyImmediate(root.gameObject);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
        instance.name="CanvasInterfaceManager";
        var newInventory=instance.GetComponentInChildren<InventoryUI>(true);
        Set(newInventory,"wallet",wallet);
        PrefabUtility.RecordPrefabInstancePropertyModifications(newInventory);
        var oldComponents=old.GetComponentsInChildren<MonoBehaviour>(true);
        var replacements=instance.GetComponentsInChildren<MonoBehaviour>(true);
        foreach(var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(component==null || component.transform.IsChildOf(old.transform)) continue;
            var serialized=new SerializedObject(component); var property=serialized.GetIterator(); bool changed=false;
            while(property.NextVisible(true))
            {
                if(property.propertyType!=SerializedPropertyType.ObjectReference) continue;
                var source=property.objectReferenceValue as MonoBehaviour;
                if(source==null || !oldComponents.Contains(source)) continue;
                var replacement=replacements.FirstOrDefault(c=>c.GetType()==source.GetType());
                if(replacement!=null) { property.objectReferenceValue=replacement; changed=true; }
            }
            if(changed) serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Undo.DestroyObjectImmediate(old);
        ConfigureRendering();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject=instance;
        ApplyInteractiveRevision();
        Debug.Log("Frutiger Aero: replaced all six interface areas with nested prefab components.");
    }

    private static void CreatePrimitives()
    {
        var container=NewRect("Container",null); Save(container,"Container");
        var board=NewRect("UpgradeBoard",null);
        var lines=board.gameObject.AddComponent<AeroTreeConnections>(); lines.color=new(.15f,.64f,.83f,.5f); lines.raycastTarget=false;
        board.gameObject.AddComponent<UnityEngine.UI.RectMask2D>(); Save(board,"UpgradeBoard");
        var label=NewRect("Label",null); var text=label.gameObject.AddComponent<TextMeshProUGUI>();
        text.font=font; text.fontSize=22; text.color=Ink; text.raycastTarget=false;
        text.alignment=TextAlignmentOptions.MidlineLeft; text.textWrappingMode=TextWrappingModes.Normal;
        Save(label,"Label");
        var panel=NewRect("GlassPanel",null); panel.sizeDelta=new(300,160);
        var image=panel.gameObject.AddComponent<UnityEngine.UI.Image>(); image.material=glass; image.raycastTarget=false;
        panel.gameObject.AddComponent<AeroGlassGeometry>(); Save(panel,"GlassPanel");
        var button=Panel("GlassButton",null,blue,22); button.sizeDelta=new(230,54);
        button.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var click=button.gameObject.AddComponent<UnityEngine.UI.Button>(); click.targetGraphic=button.GetComponent<UnityEngine.UI.Image>();
        var colors=click.colors; colors.normalColor=Color.white; colors.highlightedColor=new(.8f,1,1,1);
        colors.selectedColor=new(.8f,1,1,1); colors.pressedColor=new(.5f,.84f,1,1); colors.disabledColor=new(1,1,1,.9f); colors.fadeDuration=.12f; click.colors=colors;
        var caption=Label("Label",button,"Button",22,Color.white); Stretch(caption.rectTransform,14,14,8,8); caption.alignment=TextAlignmentOptions.Center;
        Save(button,"GlassButton");
        var orb=Panel("GlassOrb",null,white,100); orb.sizeDelta=new(68,68); Save(orb,"GlassOrb");
        var art=NewRect("Landscape",null); art.sizeDelta=new(280,180);
        var artImage=art.gameObject.AddComponent<UnityEngine.UI.Image>(); artImage.sprite=landscape; artImage.raycastTarget=false;
        artImage.preserveAspect=false; Save(art,"Landscape");
        foreach(AeroIconKind kind in Enum.GetValues(typeof(AeroIconKind)))
        {
            var icon=NewRect(kind+"Icon",null); icon.sizeDelta=new(42,42);
            var glyph=icon.gameObject.AddComponent<AeroIcon>(); glyph.Kind=kind; glyph.color=Blue; glyph.raycastTarget=false;
            Save(icon,kind+"Icon");
        }
        var progress=Panel("ProgressTrack",null,white,10); progress.sizeDelta=new(240,12);
        var fill=Container("Fill",progress); Stretch(fill,3,3,3,3);
        var fillImage=fill.gameObject.AddComponent<UnityEngine.UI.Image>(); fillImage.color=new(.2f,.76f,.37f,1);
        fillImage.sprite=SolidSprite();
        fillImage.type=UnityEngine.UI.Image.Type.Filled; fillImage.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; fillImage.fillAmount=0; fillImage.raycastTarget=false;
        Save(progress,"ProgressTrack");
    }

    private static void CreateDataPrefabs()
    {
        var node=Button("UpgradeButton",null,"Efficiency\n-0.2s"); node.sizeDelta=new(150,66);
        node.GetComponent<AeroGlassGeometry>().CornerRadius=12;
        node.GetComponent<UnityEngine.UI.Image>().material=white;
        var label=node.GetComponentInChildren<TMP_Text>(); label.fontSize=16; label.fontSizeMin=12; label.fontSizeMax=17; label.enableAutoSizing=true;
        label.color=Ink; Stretch(label.rectTransform,4,4,3,22);
        var badge=Label("State",node,"LOCKED",10,Muted);
        Anchor(badge.rectTransform,new(0,0),new(1,0),new(.5f,0),new(0,3),new(-12,14)); badge.alignment=TextAlignmentOptions.Center;
        var hover=Panel("Hover",node,green,12); Stretch(hover,1,1,1,1); hover.SetAsFirstSibling(); hover.gameObject.SetActive(false);
        var view=node.gameObject.AddComponent<UpgradeButton>(); Set(view,"button",node.GetComponent<UnityEngine.UI.Button>());
        Set(view,"upgradeText",label); Set(view,"stateText",badge); Set(view,"stateSurface",node.GetComponent<UnityEngine.UI.Image>());
        Set(view,"hoverEffect",hover.gameObject); Set(view,"hoverEffectImage",hover.GetComponent<UnityEngine.UI.Image>());
        SetColor(view,"canBuy",new(.65f,.95f,1,.65f)); SetColor(view,"cannotBuy",new(.65f,.8f,.85f,.25f)); SetColor(view,"alreadyBrought",new(.65f,1,.7f,.55f));
        SaveAt(node,"Assets/_Project/Prefabs/Interface/UpgradeButton.prefab");
        StyleUpgradeNode();

        var option=Button("OptionButton",null,"Dance"); option.sizeDelta=new(190,64);
        var optionView=option.gameObject.AddComponent<OptionButton>();
        Set(optionView,"button",option.GetComponent<UnityEngine.UI.Button>()); Set(optionView,"optionText",option.GetComponentInChildren<TMP_Text>());
        var effect=Panel("Hover",option,green,22); Stretch(effect,2,2,2,2); effect.SetAsFirstSibling(); effect.gameObject.SetActive(false);
        Set(optionView,"hoverEffect",effect.gameObject); SaveAt(option,"Assets/_Project/Prefabs/Interface/OptionButton.prefab");

        var resource=Panel("ResourceInventory",null,white,18); resource.sizeDelta=new(600,94);
        var layout=resource.gameObject.AddComponent<UnityEngine.UI.LayoutElement>(); layout.preferredHeight=94; layout.minHeight=94;
        var orb=Orb("IconWell",resource,AeroIconKind.Cube,Blue); At(orb,24,14,66,66);
        var icon=Container("ResourceIcon",orb); Stretch(icon,10,10,10,10);
        var resourceIcon=icon.gameObject.AddComponent<UnityEngine.UI.Image>(); resourceIcon.preserveAspect=true; resourceIcon.raycastTarget=false;
        var name=Label("ResourceName",resource,"Stone",24,Ink); At(name.rectTransform,112,17,330,30);
        LabelAt("Description",resource,"Building material",16,Muted,112,51,330,24);
        var amount=Label("Amount",resource,"0",30,Blue); amount.alignment=TextAlignmentOptions.Right;
        Anchor(amount.rectTransform,new(1,.5f),new(1,.5f),new(1,.5f),new(-24,0),new(160,60));
        var row=resource.gameObject.AddComponent<ResourceInventoryDisplay>(); Set(row,"resourceIcon",resourceIcon); Set(row,"resourceName",name); Set(row,"resourceAmount",amount);
        SaveAt(resource,"Assets/_Project/Prefabs/Interface/ResourceInventory.prefab");
    }

    private static void BuildHud(RectTransform parent)
    {
        ConfigureMenuLauncher(parent);
    }

    private static InteractionUI BuildInteraction(RectTransform parent)
    {
        var controller=Container("Interaction",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<InteractionUI>();
        var visuals=Panel("Visuals",controller,glass,28);
        Anchor(visuals,new(.5f,0),new(.5f,0),new(.5f,0),new(0,118),new(540,172));
        var orb=Orb("ActionIcon",visuals,AeroIconKind.Pickaxe,Green); At(orb,22,23,64,64);
        var title=LabelAt("ObjectName",visuals,"Stone",26,Ink,108,19,395,36,FontStyles.Bold);
        var detail=LabelAt("Detail",visuals,"Ready to gather",17,Muted,108,60,395,28);
        var prompt=LabelAt("Prompt",visuals,"Press E to interact",17,Blue,26,112,350,30);
        var progress=Container("Progress",visuals); At(progress,26,112,488,51);
        var track=Inst("ProgressTrack",progress);
        Anchor(track,new(0,0),new(1,0),new(.5f,0),Vector2.zero,new(0,12));
        var percentage=Label("Percentage",progress,"0%",17,Blue);
        Anchor(percentage.rectTransform,new(1,1),new(1,1),new(1,1),Vector2.zero,new(90,30)); percentage.alignment=TextAlignmentOptions.Right;
        Set(ui,"visuals",visuals.gameObject); Set(ui,"objectNameText",title); Set(ui,"detailText",detail); Set(ui,"promptText",prompt);
        Set(ui,"progressVisuals",progress.gameObject); Set(ui,"progressFill",track.Find("Fill").GetComponent<UnityEngine.UI.Image>()); Set(ui,"progressText",percentage);
        visuals.gameObject.SetActive(false); return ui;
    }

    private static InformationUI BuildInformation(RectTransform parent)
    {
        var controller=Container("Information",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<InformationUI>();
        var visuals=Panel("Visuals",controller,white,20);
        Anchor(visuals,new(1,1),new(1,1),new(1,1),new(-30,-28),new(138,44));
        var text=Label("FPS",visuals,"FPS: --",17,Ink); Stretch(text.rectTransform,14,14,5,5); text.alignment=TextAlignmentOptions.Center;
        Set(ui,"visuals",visuals.gameObject); Set(ui,"fpsCounter",text); return ui;
    }

    private static ServerStatusUI BuildServer(RectTransform parent)
    {
        var controller=Container("ServerStatus",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<ServerStatusUI>();
        var visuals=Panel("Visuals",controller,white,22);
        Anchor(visuals,new(1,1),new(1,1),new(1,1),new(-182,-28),new(330,44));
        var icon=Inst("GlobeIcon",visuals); At(icon,10,8,28,28);
        var text=LabelAt("StatusText",visuals,"Not connected to server",17,Muted,50,6,270,32);
        Set(ui,"visuals",visuals.gameObject); Set(ui,"statusText",text);
        SetColor(ui,"disconnectedColor",Muted); SetColor(ui,"connectingColor",Blue); return ui;
    }

    private static InventoryUI BuildInventory(RectTransform parent,InputActionAsset input,UnityEngine.Object definitions)
    {
        var controller=Container("Inventory",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<InventoryUI>();
        ConfigureCompactInventory(controller,ui,input,definitions);
        return ui;
    }

    private static UpgradeUI BuildUpgrade(RectTransform parent,InputActionAsset input)
    {
        var controller=Container("Upgrade",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<UpgradeUI>();
        var visuals=Window(controller,"Grow your potential","Every skill is another step toward the sky.",ui.Hide);
        Set(ui,"visuals",visuals.gameObject); Set(ui,"inputActions",input);
        Set(ui,"prefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/UpgradeButton.prefab"));
        var rail=Panel("SkillRail",visuals,white,24); At(rail,28,128,300,0); Bottom(rail,28);
        var art=Inst("Landscape",rail); At(art,14,14,272,172);
        LabelAt("Choose",rail,"YOUR PATH",14,Muted,24,210,252,26,FontStyles.Bold);
        var gather=Button("GatherButton",rail,"Gather"); At(gather,20,254,260,64); AddButtonIcon(gather,AeroIconKind.Leaf);
        var process=Button("ProcessorButton",rail,"Processor"); At(process,20,332,260,64); AddButtonIcon(process,AeroIconKind.Cube);
        var build=Button("BuilderButton",rail,"Builder"); At(build,20,410,260,64); AddButtonIcon(build,AeroIconKind.Tower);
        Set(ui,"gatherButton",gather.GetComponent<UnityEngine.UI.Button>()); Set(ui,"processorButton",process.GetComponent<UnityEngine.UI.Button>()); Set(ui,"builderButton",build.GetComponent<UnityEngine.UI.Button>());
        LabelAt("Instructions",rail,"Start at the center.\nUnlock neighboring skills\nas you grow.",19,Muted,24,514,252,100);
        var tip=Panel("CapstoneHint",rail,green,18);
        Anchor(tip,new(.5f,0),new(.5f,0),new(.5f,0),new(0,24),new(252,68));
        var tipText=Label("Label",tip,"LEVEL 50\nA new horizon awaits",16,Color.white); Stretch(tipText.rectTransform,10,10,5,5); tipText.alignment=TextAlignmentOptions.Center;
        var title=LabelAt("TitleJob",visuals,"Gather",28,Ink,366,134,420,42,FontStyles.Bold); Set(ui,"jobTitle",title.gameObject);
        LabelAt("Level",visuals,"Lv 0 | Pts 0",19,Blue,366,181,480,32);
        var xp=Label("Experience",visuals,"XP 0/10",17,Muted);
        Anchor(xp.rectTransform,new(1,1),new(1,1),new(1,1),new(-34,-140),new(350,30)); xp.alignment=TextAlignmentOptions.Right;
        var track=Inst("ProgressTrack",visuals); Anchor(track,new(1,1),new(1,1),new(1,1),new(-34,-188),new(340,12));
        Set(ui,"experienceFill",track.Find("Fill").GetComponent<UnityEngine.UI.Image>());
        var boardWell=Panel("BoardWell",visuals,glass,22); Stretch(boardWell,354,28,230,95);
        foreach(string name in new[]{"GatherBoard","ProcessorBoard","BuilderBoard"})
        {
            var board=Inst("UpgradeBoard",boardWell); board.name=name; Stretch(board,5,5,5,5);
            Set(ui,name=="GatherBoard"?"gatherBoard":name=="ProcessorBoard"?"processorBoard":"builderBoard",board.gameObject);
            board.gameObject.SetActive(name=="GatherBoard");
        }
        var hint=Label("BoardHint",visuals,"Earn experience to unlock your next upgrade.",17,Muted);
        Anchor(hint.rectTransform,new(0,0),new(0,0),new(0,0),new(370,53),new(760,26)); Set(ui,"boardHint",hint);
        var legend=Label("Legend",visuals,"BLUE  Available    GREEN  Owned    FROST  Locked",14,Muted);
        Anchor(legend.rectTransform,new(1,0),new(1,0),new(1,0),new(-32,23),new(680,24)); legend.alignment=TextAlignmentOptions.Right;
        visuals.gameObject.SetActive(false); return ui;
    }

    private static AnimationEmoteUI BuildEmotes(RectTransform parent,InputActionAsset input)
    {
        var controller=Container("AnimationEmote",parent); Stretch(controller);
        var ui=controller.gameObject.AddComponent<AnimationEmoteUI>();
        ConfigureQuickEmotes(controller,ui,input);
        return ui;
    }

    private static RectTransform Window(RectTransform controller,string title,string subtitle,UnityAction close)
    {
        var visuals=Container("Visuals",controller); Stretch(visuals,90,90,100,100);
        var shade=Panel("Shadow",visuals,shadow,34); Stretch(shade,-7,-7,-12,-12);
        var pane=Panel("WindowGlass",visuals,glass,28); Stretch(pane); pane.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
        var header=Panel("Titlebar",visuals,white,25); Stretch(header,10,10,10,0);
        header.anchorMin=new(0,1); header.anchorMax=new(1,1); header.pivot=new(.5f,1); header.sizeDelta=new(-20,102); header.anchoredPosition=new(0,-10);
        var orb=Orb("TitleIcon",header,AeroIconKind.Globe,Green); At(orb,16,17,68,68);
        LabelAt("Title",header,title,36,Ink,108,13,1250,46,FontStyles.Bold);
        LabelAt("Subtitle",header,subtitle,18,Muted,110,62,1250,28);
        var x=Button("CloseButton",header,""); Anchor(x,new(1,.5f),new(1,.5f),new(1,.5f),new(-20,0),new(48,48));
        x.GetComponent<AeroGlassGeometry>().CornerRadius=24;
        var icon=Inst("CloseIcon",x); Stretch(icon,12,12,12,12); icon.GetComponent<AeroIcon>().color=Color.white;
        UnityEventTools.AddPersistentListener(x.GetComponent<UnityEngine.UI.Button>().onClick,close);
        var escape=visuals.gameObject.AddComponent<AeroWindow>();
        // UnityEventTools operates on the initialized event; keep the reference serialized in the prefab.
        var field=typeof(AeroWindow).GetField("close",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        UnityEventTools.AddPersistentListener((UnityEvent)field.GetValue(escape),close); EditorUtility.SetDirty(escape);
        return visuals;
    }

    private static void LabelFooter(RectTransform parent,string left,string right)
    {
        var a=Label("Footer",parent,left,17,Muted); Anchor(a.rectTransform,new(0,0),new(0,0),new(0,0),new(366,30),new(650,34));
        var b=Label("FooterHint",parent,right,16,Muted); Anchor(b.rectTransform,new(1,0),new(1,0),new(1,0),new(-36,30),new(600,34)); b.alignment=TextAlignmentOptions.Right;
    }

    private static void ConfigureRendering()
    {
        var pipeline=UniversalRenderPipeline.asset;
        if(pipeline==null) throw new InvalidOperationException("Frutiger glass requires the project's URP pipeline.");
        pipeline.supportsCameraOpaqueTexture=true; EditorUtility.SetDirty(pipeline);
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(camera.cameraType!=CameraType.Game) continue;
            var data=camera.GetComponent<UniversalAdditionalCameraData>();
            if(data==null) continue;
            data.requiresColorOption=CameraOverrideOption.On;
            data.renderPostProcessing=true; EditorUtility.SetDirty(data);
        }
        var volume=GameObject.Find("Global Volume").GetComponent<Volume>();
        string profilePath=Root+"/AeroAtmosphere.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
        if(profile==null)
        {
            profile=UnityEngine.Object.Instantiate(volume.sharedProfile); profile.name="AeroAtmosphere";
            profile.components=new List<VolumeComponent>();
            foreach(var source in volume.sharedProfile.components)
            { var copy=UnityEngine.Object.Instantiate(source); profile.components.Add(copy); }
            AssetDatabase.CreateAsset(profile,profilePath);
            foreach(var effect in profile.components) AssetDatabase.AddObjectToAsset(effect,profile);
        }
        if(!profile.TryGet<Bloom>(out var bloom)) { bloom=profile.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,profile); }
        bloom.intensity.Override(.18f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.6f);
        if(!profile.TryGet<ColorAdjustments>(out var colors)) { colors=profile.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(colors,profile); }
        colors.saturation.Override(8); colors.contrast.Override(3);
        volume.sharedProfile=profile; EditorUtility.SetDirty(volume); EditorUtility.SetDirty(profile);
    }

    private static Material Mat(string name,Color tint,float frost,float gloss,float scene)
    {
        string path=Root+"/Materials/"+name+".mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null) { material=new Material(Shader.Find("TowerOfBabel/UI/Liquid Glass")); AssetDatabase.CreateAsset(material,path); }
        material.SetColor("_Color",tint); material.SetFloat("_Frost",frost); material.SetFloat("_Shine",gloss);
        material.SetFloat("_SceneBlur",scene); material.SetFloat("_Blur",6); EditorUtility.SetDirty(material); return material;
    }
    private static Sprite SolidSprite()
    {
        string path=Root+"/Art/ProgressPixel.png";
        if(!System.IO.File.Exists(path))
        {
            var texture=new Texture2D(4,4); texture.SetPixels(Enumerable.Repeat(Color.white,16).ToArray()); texture.Apply();
            System.IO.File.WriteAllBytes(path,texture.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(texture); AssetDatabase.ImportAsset(path);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single; importer.mipmapEnabled=false; importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    private static void ConfigureResourceArtwork()
    {
        foreach(string name in new[]{"Stone","RefinedStone"})
        {
            string path=Root+"/Art/"+name+".png";
            if(!System.IO.File.Exists(path)) continue;
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=512;
            importer.textureCompression=TextureImporterCompression.CompressedHQ; importer.SaveAndReimport();
            var definition=AssetDatabase.LoadAssetAtPath<ResourceDefinition>("Assets/_Project/Resources/Definitions/"+(name=="Stone"?"Stone":"ProcessedStone")+".asset");
            Set(definition,"icon",AssetDatabase.LoadAssetAtPath<Sprite>(path));
            if(name=="RefinedStone")
            { var data=new SerializedObject(definition); data.FindProperty("displayName").stringValue="Processed stone"; data.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }
    private static RectTransform NewRect(string name,Transform parent)
    { var go=new GameObject(name,typeof(RectTransform)); var rect=(RectTransform)go.transform; rect.SetParent(parent,false); rect.sizeDelta=new(100,100); return rect; }
    private static RectTransform Inst(string name,Transform parent)
    { var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Prefabs/"+name+".prefab")); go.transform.SetParent(parent,false); return (RectTransform)go.transform; }
    private static RectTransform Container(string name,Transform parent) { var rect=Inst("Container",parent); rect.name=name; return rect; }
    private static RectTransform Panel(string name,Transform parent,Material material,float radius)
    { var rect=Inst("GlassPanel",parent); rect.name=name; rect.GetComponent<UnityEngine.UI.Image>().material=material; rect.GetComponent<AeroGlassGeometry>().CornerRadius=radius; return rect; }
    private static RectTransform Button(string name,Transform parent,string text)
    { var rect=Inst("GlassButton",parent); rect.name=name; rect.GetComponentInChildren<TMP_Text>().text=text; return rect; }
    private static TMP_Text Label(string name,Transform parent,string content,float size,Color color)
    { var rect=Inst("Label",parent); rect.name=name; var text=rect.GetComponent<TMP_Text>(); text.text=content; text.fontSize=size; text.color=color; return text; }
    private static TMP_Text LabelAt(string name,Transform parent,string content,float size,Color color,float x,float y,float width,float height,FontStyles style=FontStyles.Normal)
    { var text=Label(name,parent,content,size,color); text.fontStyle=style; At(text.rectTransform,x,y,width,height); return text; }
    private static RectTransform Orb(string name,Transform parent,AeroIconKind kind,Color color)
    { var rect=Inst("GlassOrb",parent); rect.name=name; var icon=Inst(kind+"Icon",rect); Stretch(icon,12,12,12,12); icon.GetComponent<AeroIcon>().color=color; return rect; }
    private static void AddButtonIcon(RectTransform button,AeroIconKind kind)
    { var icon=Inst(kind+"Icon",button); At(icon,14,12,40,40); icon.GetComponent<AeroIcon>().color=Color.white; var label=button.GetComponentInChildren<TMP_Text>(); Stretch(label.rectTransform,60,12,4,4); label.alignment=TextAlignmentOptions.MidlineLeft; }
    private static void Save(RectTransform rect,string name) => SaveAt(rect,Root+"/Prefabs/"+name+".prefab");
    private static void SaveAt(RectTransform rect,string path) { PrefabUtility.SaveAsPrefabAsset(rect.gameObject,path); UnityEngine.Object.DestroyImmediate(rect.gameObject); }
    private static void At(RectTransform rect,float x,float y,float width,float height)
    { Anchor(rect,new(0,1),new(0,1),new(0,1),new(x,-y),new(width,height)); }
    private static void Bottom(RectTransform rect,float bottom)
    { rect.anchorMin=new(0,0); rect.anchorMax=new(0,1); rect.offsetMin=new(rect.offsetMin.x,bottom); }
    private static void Stretch(RectTransform rect,float left=0,float right=0,float top=0,float bottom=0)
    { rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.pivot=new(.5f,.5f); rect.offsetMin=new(left,bottom); rect.offsetMax=new(-right,-top); rect.localScale=Vector3.one; }
    private static void Anchor(RectTransform r,Vector2 min,Vector2 max,Vector2 pivot,Vector2 position,Vector2 size)
    { r.anchorMin=min; r.anchorMax=max; r.pivot=pivot; r.sizeDelta=size; r.anchoredPosition=position; r.localScale=Vector3.one; }
    private static void Set(UnityEngine.Object target,string field,UnityEngine.Object value)
    { var s=new SerializedObject(target); var p=s.FindProperty(field); if(p==null) throw new InvalidOperationException(target.name+" missing "+field); p.objectReferenceValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetColor(UnityEngine.Object target,string field,Color value)
    { var s=new SerializedObject(target); s.FindProperty(field).colorValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
    private static void SetFloat(UnityEngine.Object target,string field,float value)
    { var s=new SerializedObject(target); s.FindProperty(field).floatValue=value; s.ApplyModifiedPropertiesWithoutUndo(); }
}
