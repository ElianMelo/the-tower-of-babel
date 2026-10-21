using System.Linq;
using TMPro;
using TowerOfBabel.Interface;
using UnityEditor;
using UnityEngine;

public static partial class FrutigerAeroBuilder
{
    private static void ConfigureGeneratedActionIcons()
    {
        foreach(string action in new[]{"Gather","Process","Build"})
        {
            string path=Root+"/Art/"+action+"Small.png";
            AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
            importer.alphaIsTransparency=true; importer.mipmapEnabled=false; importer.maxTextureSize=64;
            importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
            string prefab=Root+"/Prefabs/"+action+"ActionIcon.prefab";
            if(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)==null)
            {
                var icon=Container(action+"ActionIcon",null); icon.sizeDelta=new(64,64);
                icon.gameObject.AddComponent<UnityEngine.UI.Image>(); Save(icon,action+"ActionIcon");
            }
            EditPrefab(prefab,icon=>
            {
                var image=icon.GetComponent<UnityEngine.UI.Image>();
                image.sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path); image.preserveAspect=true;
                image.color=Color.white; image.raycastTarget=false;
            });
        }
    }

    private static void ConfigureActionArtwork(Transform holder,string action)
    {
        var vector=holder.GetComponent<AeroIcon>(); if(vector!=null) vector.enabled=false;
        var artwork=holder.Find("Artwork") as RectTransform;
        if(artwork==null) { artwork=Inst(action+"ActionIcon",holder); artwork.name="Artwork"; }
        Stretch(artwork,0,0,0,0);
    }

    private static void ConfigureActionPrompt(RectTransform root)
    {
        var ui=root.GetComponent<InteractionUI>();
        var visuals=(RectTransform)root.Find("Visuals");
        visuals.sizeDelta=new(720,182);
        if(visuals.GetComponent<AeroFloat>()==null) visuals.gameObject.AddComponent<AeroFloat>();
        At((RectTransform)visuals.Find("ObjectName"),108,19,350,36);
        At((RectTransform)visuals.Find("Detail"),108,60,350,44);
        At((RectTransform)visuals.Find("Prompt"),26,126,440,30);
        At((RectTransform)visuals.Find("Progress"),26,123,668,51);
        var orb=visuals.Find("ActionIcon");
        var old=orb.Find("ResourceIcon"); if(old!=null) old.gameObject.SetActive(false);
        var process=orb.Find("ProcessIcon") as RectTransform;
        if(process==null) { process=Inst("CubeIcon",orb); process.name="ProcessIcon"; Stretch(process,12,12,12,12); }
        var build=orb.Find("BuildIcon") as RectTransform;
        if(build==null) { build=Inst("TowerIcon",orb); build.name="BuildIcon"; Stretch(build,12,12,12,12); }
        process.GetComponent<AeroIcon>().Kind=AeroIconKind.Gear;
        process.GetComponent<AeroIcon>().color=Green; build.GetComponent<AeroIcon>().color=Green;
        ConfigureActionArtwork(orb.Find("PickaxeIcon"),"Gather");
        ConfigureActionArtwork(process,"Process"); ConfigureActionArtwork(build,"Build");
        process.gameObject.SetActive(false); build.gameObject.SetActive(false);

        string path=Root+"/Prefabs/InteractionResourceSlot.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)==null)
        {
            var slot=Container("InteractionResourceSlot",null); slot.sizeDelta=new(84,98);
            var caption=LabelAt("Caption",slot,"Output",13,Muted,0,0,84,18); caption.alignment=TextAlignmentOptions.Center;
            var image=Inst("ResourceSprite",slot); image.name="Icon"; At(image,18,20,48,48);
            var amount=LabelAt("Amount",slot,"1",18,Ink,0,70,84,24,FontStyles.Bold); amount.alignment=TextAlignmentOptions.Center;
            Save(slot,"InteractionResourceSlot");
        }
        var summary=visuals.Find("ResourceSummary") as RectTransform;
        if(summary==null) summary=Container("ResourceSummary",visuals);
        Anchor(summary,new(1,1),new(1,1),new(1,1),new(-18,-15),new(218,98));
        var source=summary.Find("Source") as RectTransform;
        if(source==null) { source=Inst("InteractionResourceSlot",summary); source.name="Source"; }
        At(source,0,0,84,98); source.Find("Caption").GetComponent<TMP_Text>().text="Source";
        var arrow=summary.Find("RecipeArrow") as RectTransform;
        if(arrow==null) { arrow=Inst("ArrowIcon",summary); arrow.name="RecipeArrow"; }
        At(arrow,91,40,24,24); arrow.GetComponent<AeroIcon>().color=Blue;
        var result=summary.Find("Result") as RectTransform;
        if(result==null) { result=Inst("InteractionResourceSlot",summary); result.name="Result"; }
        At(result,126,0,84,98);
        Set(ui,"resourceIcon",result.Find("Icon").GetComponent<UnityEngine.UI.Image>());
        Set(ui,"sourceIcon",source.Find("Icon").GetComponent<UnityEngine.UI.Image>());
        Set(ui,"sourceAmount",source.Find("Amount").GetComponent<TMP_Text>());
        Set(ui,"resultAmount",result.Find("Amount").GetComponent<TMP_Text>());
        Set(ui,"resultCaption",result.Find("Caption").GetComponent<TMP_Text>());
        Set(ui,"sourceSlot",source.gameObject); Set(ui,"resourceSummary",summary.gameObject); Set(ui,"recipeArrow",arrow.gameObject);
        Set(ui,"fallbackIcon",orb.Find("PickaxeIcon").gameObject); Set(ui,"processAction",process.gameObject); Set(ui,"buildAction",build.gameObject);
        Set(ui,"definitions",AssetDatabase.LoadAssetAtPath<TowerOfBabel.Resources.ResourceDefinitionCollection>("Assets/_Project/Resources/Definitions/ResourceDefinitions.asset"));
        summary.gameObject.SetActive(false);
    }

    private static void ConfigurePanelSurfaceFeedback()
    {
        var paths=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs","Assets/_Project/Prefabs/Interface"})
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>AssetDatabase.GetDependencies(p).Length).ToArray();
        foreach(var path in paths)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset.GetComponentsInChildren<AeroGlassGeometry>(true).Length==0) continue;
            EditPrefab(path,root=>
            {
                foreach(var geometry in root.GetComponentsInChildren<AeroGlassGeometry>(true))
                {
                    var graphic=geometry.GetComponent<UnityEngine.UI.Graphic>();
                    var material=graphic.material;
                    var hover=geometry.GetComponent<AeroHoverFeedback>();
                    bool glass=geometry.enabled && material!=null && material.shader.name=="TowerOfBabel/UI/Liquid Glass" && !material.name.Contains("Shadow");
                    if(!glass) { if(hover!=null) hover.enabled=false; continue; }
                    bool interactive=geometry.GetComponent<UnityEngine.UI.Selectable>()!=null;
                    SetFloat(geometry,"hoverFill",interactive ? 1 : 0);
                    SetFloat(geometry,"borderMotion",interactive ? .015f : .18f);
                    SetFloat(geometry,"surfaceFlow",interactive ? 0 : .8f);
                    if(hover==null) hover=geometry.gameObject.AddComponent<AeroHoverFeedback>();
                    hover.enabled=true; Set(hover,"surface",geometry);
                    var serialized=new SerializedObject(hover); serialized.FindProperty("trackScreenPointer").boolValue=!interactive; serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                // Each surface has one writer; legacy scroll controllers yield to its own tracker.
                foreach(var hover in root.GetComponentsInChildren<AeroHoverFeedback>(true))
                    if(hover.Surface!=null && hover.Surface.gameObject!=hover.gameObject && hover.Surface.GetComponent<AeroHoverFeedback>()!=null) hover.enabled=false;
            });
        }
    }
}
