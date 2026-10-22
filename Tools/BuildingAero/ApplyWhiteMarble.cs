// Unity MCP eval_file authoring snippet. The supplied marble image is used directly.
// The see-through glass experiment was rejected; retain the opaque aquatic finish.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)
    throw new InvalidOperationException("Use this project in Edit Mode");
const string dir="Assets/_Project/Models/Buildings/FrutigerAero";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
foreach(var filename in new[]{"Aero_AquaticFlow.png","Aero_AquaticRipple_Normal.png","Aero_WaterCaustics.png"}){
    var waterImporter=(TextureImporter)AssetImporter.GetAtPath(dir+"/"+filename);
    if(waterImporter==null)throw new Exception("Missing aquatic field: "+filename);
    waterImporter.textureType=filename.Contains("Normal")?TextureImporterType.NormalMap:TextureImporterType.Default;
    waterImporter.sRGBTexture=false;waterImporter.convertToNormalmap=false;waterImporter.flipGreenChannel=false;
    waterImporter.wrapMode=TextureWrapMode.Repeat;waterImporter.filterMode=FilterMode.Trilinear;
    waterImporter.mipmapEnabled=true;waterImporter.anisoLevel=4;waterImporter.maxTextureSize=512;
    waterImporter.textureCompression=TextureImporterCompression.CompressedHQ;waterImporter.SaveAndReimport();
}
foreach(var name in new[]{"Arch","Floor_Tile","Pillar","Step_Tile"}){
    foreach(var suffix in new[]{"Surface","Normal","AO"}){
        var bakedPath=dir+"/BakedAero/AeroLP_"+name+"_"+suffix+".png";
        var baked=(TextureImporter)AssetImporter.GetAtPath(bakedPath);
        if(baked==null)throw new Exception("Missing baked texture: "+bakedPath);
        baked.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
        baked.sRGBTexture=suffix=="Surface";baked.convertToNormalmap=false;baked.flipGreenChannel=false;
        baked.wrapMode=TextureWrapMode.Clamp;baked.filterMode=FilterMode.Trilinear;
        baked.mipmapEnabled=true;baked.anisoLevel=4;baked.maxTextureSize=name=="Pillar"?2048:1024;
        baked.textureCompression=TextureImporterCompression.CompressedHQ;baked.SaveAndReimport();
    }
}
var importer=(TextureImporter)AssetImporter.GetAtPath(dir+"/Aero_WhiteMarble.png");
importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;
importer.wrapMode=TextureWrapMode.Mirror;importer.filterMode=FilterMode.Trilinear;
importer.mipmapEnabled=true;importer.anisoLevel=4;importer.maxTextureSize=2048;
importer.npotScale=TextureImporterNPOTScale.None;
importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.SaveAndReimport();
var normalImporter=(TextureImporter)AssetImporter.GetAtPath(dir+"/Aero_WhiteMarble_Normal.png");
if(normalImporter!=null){
    normalImporter.textureType=TextureImporterType.NormalMap;normalImporter.sRGBTexture=false;
    normalImporter.convertToNormalmap=false;normalImporter.flipGreenChannel=false;
    normalImporter.wrapMode=TextureWrapMode.Mirror;normalImporter.filterMode=FilterMode.Trilinear;
    normalImporter.mipmapEnabled=true;normalImporter.anisoLevel=4;normalImporter.maxTextureSize=2048;
    normalImporter.npotScale=TextureImporterNPOTScale.None;
    normalImporter.textureCompression=TextureImporterCompression.CompressedHQ;normalImporter.SaveAndReimport();
}
var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_WhiteMarble.png");
var shader=Shader.Find("TowerOfBabel/Buildings/Aquatic Finish");
if(shader==null)throw new Exception("Marble shader not imported");
var names=new[]{"Arch","Floor_Tile","Pillar","Step_Tile"};
var smoothness=new[]{.82f,.88f,.80f,.85f};
var waterScale=new[]{.85f,.45f,.65f,.55f};
var ripple=new[]{.035f,.025f,.025f,.025f};
var interior=new[]{.45f,.25f,.30f,.30f};
var caustics=new[]{.4f,.25f,.25f,.28f};
var results=new List<object>();
for(int i=0;i<names.Length;i++){
    string name=names[i];string path=dir+"/Aero_WhiteMarble_"+name+".mat";
    var material=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(material==null){material=new Material(shader);AssetDatabase.CreateAsset(material,path);}
    material.shader=shader;material.shaderKeywords=Array.Empty<string>();material.enableInstancing=true;
    material.SetFloat("_BorderGlass",0);material.DisableKeyword("_AERO_BORDER_GLASS");
    material.SetTexture("_MarbleMap",texture);
    material.SetTexture("_NormalMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_WhiteMarble_Normal.png"));
    material.SetFloat("_NormalStrength",.12f);
    material.SetTexture("_BakedSurface",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/BakedAero/AeroLP_"+name+"_Surface.png"));
    material.SetTexture("_BakedNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/BakedAero/AeroLP_"+name+"_Normal.png"));
    material.SetTexture("_BakedAO",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/BakedAero/AeroLP_"+name+"_AO.png"));
    material.SetFloat("_BakedStrength",.65f);material.SetFloat("_AOStrength",.10f);
    material.SetFloat("_PearlCoat",.55f);material.SetFloat("_AeroGloss",0);
    material.SetFloat("_UseGeometryColors",name=="Arch"?0:1);
    material.SetColor("_GlazeColor",new Color(.26f,.74f,.90f,1));
    material.SetTexture("_WaterLightMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_WaterCaustics.png"));
    material.SetFloat("_WaterLightStrength",.12f);
    material.SetTexture("_WaterField",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_AquaticFlow.png"));
    material.SetTexture("_WaterRippleNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_AquaticRipple_Normal.png"));
    material.SetFloat("_ModelKind",i);material.SetFloat("_WaterScale",waterScale[i]);
    material.SetFloat("_WaterNormalStrength",ripple[i]);material.SetFloat("_InteriorStrength",interior[i]);
    material.SetFloat("_InnerCausticStrength",caustics[i]);
    material.SetFloat("_InnerDepth",i==0?.06f:.035f);material.SetFloat("_FlowSpeed",.075f);
    material.SetFloat("_FlowDistortion",.9f);material.SetFloat("_InstanceVariation",1);
    material.SetFloat("_GlassBorderWidth",.11f);material.SetFloat("_GlassBorderStrength",.7f);
    material.SetFloat("_GlassEdgeStrength",.15f);material.SetFloat("_WaterDetailDistance",85);
    material.SetFloat("_GlazeReflection",.25f);
    material.SetFloat("_MotionTime",-1);
    material.SetColor("_WaterDeepColor",new Color(.08f,.48f,.70f,1));
    material.SetColor("_WaterLightColor",new Color(.53f,.88f,.98f,1));
    material.SetTexture("_PaletteMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_Palette.png"));
    material.SetColor("_BaseColor",Color.white);material.SetColor("_DetailTint",new Color(.72f,.83f,.95f,1));
    material.SetFloat("_VeinStrength",.10f);material.SetFloat("_DetailStrength",.08f);
    material.SetFloat("_MarbleScale",.28f);material.SetFloat("_TextureAspect",(float)texture.width/texture.height);
    material.SetFloat("_Smoothness",smoothness[i]);material.SetFloat("_PatternSeed",i*11.73f+.37f);
    material.SetFloat("_Cull",2);material.renderQueue=2000;EditorUtility.SetDirty(material);
    var model=(ModelImporter)AssetImporter.GetAtPath(dir+"/"+name+".fbx");
    model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Aero_BuildingAtlas"),material);
    model.SaveAndReimport();
    var prefabPath="Assets/_Project/Prefabs/Buildings/"+name+".prefab";
    var root=PrefabUtility.LoadPrefabContents(prefabPath);
    try{
        root.GetComponent<MeshRenderer>().sharedMaterials=new[]{material};
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        results.Add(new{name,material=path,smoothness=smoothness[i],submeshes=root.GetComponent<MeshFilter>().sharedMesh.subMeshCount,
            children=root.transform.childCount,shader=shader.name,material.renderQueue});
    }finally{PrefabUtility.UnloadPrefabContents(root);}
}
AssetDatabase.SaveAssets();
var report=new{variants=results,texture=new{texture.width,texture.height},
    messages=ShaderUtil.GetShaderMessages(shader).Select(m=>new{m.message,severity=m.severity.ToString(),m.line}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AeroFlowMaterialsInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
