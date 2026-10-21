// Evaluate through Unity MCP in the project Editor, in Edit Mode.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets")
    throw new InvalidOperationException("Wrong Unity project");
if(EditorApplication.isPlaying)throw new InvalidOperationException("Apply the glass material in Edit Mode");
const string dir="Assets/_Project/Models/Buildings/FrutigerAero";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var imp=(TextureImporter)AssetImporter.GetAtPath(dir+"/Aero_WaterCaustics.png");
imp.sRGBTexture=false;imp.wrapMode=TextureWrapMode.Repeat;imp.filterMode=FilterMode.Bilinear;
imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.Uncompressed;
imp.maxTextureSize=512;imp.SaveAndReimport();
var material=AssetDatabase.LoadAssetAtPath<Material>(dir+"/Aero_BuildingAtlas.mat");
material.shader=Shader.Find("TowerOfBabel/Buildings/Water Glass");
material.shaderKeywords=Array.Empty<string>();material.enableInstancing=true;
material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_Palette.png"));
material.SetTexture("_CausticsMap",AssetDatabase.LoadAssetAtPath<Texture2D>(dir+"/Aero_WaterCaustics.png"));
material.SetColor("_BaseColor",new Color(.91f,.965f,1,1));
material.SetColor("_DetailTint",new Color(.48f,.75f,1,1));
material.SetColor("_WaterTint",new Color(.035f,.36f,1,1));
material.SetFloat("_DetailBlend",.32f);material.SetFloat("_CausticScale",.26f);
material.SetFloat("_CausticIntensity",1.15f);material.SetFloat("_FlowSpeed",.7f);
material.SetFloat("_WaterDepth",.22f);material.SetFloat("_Opacity",.36f);
material.SetFloat("_LayerCount",5);material.SetFloat("_LayerSpread",1.6f);
material.SetFloat("_WaterDensity",.9f);material.SetFloat("_RippleStrength",.09f);
material.SetFloat("_InstanceVariation",1);material.SetFloat("_PatternSeed",0);
material.SetFloat("_FarLayerDistance",70);
material.SetFloat("_EdgeOpacity",.78f);material.SetFloat("_Refraction",.009f);
material.SetFloat("_Frost",.25f);material.SetFloat("_Smoothness",.91f);
material.SetFloat("_ReflectionStrength",.6f);material.SetFloat("_MotionTime",-1);
material.SetFloat("_ZWrite",1);material.SetFloat("_Cull",2);material.SetFloat("_Surface",1);
material.renderQueue=3000;EditorUtility.SetDirty(material);
var variants=new List<object>();
var names=new[]{"Arch","Floor_Tile","Pillar","Step_Tile"};
var opacity=new[]{.23f,.50f,.34f,.40f};
var depth=new[]{.28f,.12f,.35f,.09f};
var layers=new[]{6,5,6,4};
var density=new[]{.8f,1.15f,1.05f,.75f};
var speed=new[]{.7f,.52f,.62f,.85f};
for(int i=0;i<names.Length;i++){
    string name=names[i];string matPath=dir+"/Aero_WaterGlass_"+name+".mat";
    var variant=AssetDatabase.LoadAssetAtPath<Material>(matPath);
    if(variant==null){variant=new Material(material);AssetDatabase.CreateAsset(variant,matPath);}
    variant.shader=material.shader;variant.CopyPropertiesFromMaterial(material);
    variant.name="Aero_WaterGlass_"+name;variant.enableInstancing=true;
    variant.SetFloat("_Opacity",opacity[i]);variant.SetFloat("_WaterDepth",depth[i]);
    variant.SetFloat("_LayerCount",layers[i]);variant.SetFloat("_WaterDensity",density[i]);
    variant.SetFloat("_FlowSpeed",speed[i]);variant.SetFloat("_PatternSeed",i*11.73f+.37f);
    variant.SetFloat("_LayerSpread",name=="Pillar"?2.1f:1.6f);
    variant.SetFloat("_EdgeOpacity",name=="Floor_Tile"?.86f:.78f);
    EditorUtility.SetDirty(variant);
    var importer=(ModelImporter)AssetImporter.GetAtPath(dir+"/"+name+".fbx");
    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Aero_BuildingAtlas"),variant);
    importer.SaveAndReimport();
    var prefabPath="Assets/_Project/Prefabs/Buildings/"+name+".prefab";
    var root=PrefabUtility.LoadPrefabContents(prefabPath);
    try{
        root.GetComponent<MeshRenderer>().sharedMaterials=new[]{variant};
        PrefabUtility.SaveAsPrefabAsset(root,prefabPath);
        variants.Add(new{name,material=matPath,opacity=opacity[i],transparency=1-opacity[i],
            waterLayers=layers[i],depth=depth[i],speed=speed[i],rootChildren=root.transform.childCount,
            submeshes=root.GetComponent<MeshFilter>().sharedMesh.subMeshCount});
    }finally{PrefabUtility.UnloadPrefabContents(root);}
}
AssetDatabase.SaveAssets();
var report=new{shader=material.shader.name,variants,
    errors=ShaderUtil.GetShaderMessages(material.shader).Select(m=>new{m.message,severity=m.severity.ToString(),m.line}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/LayeredWaterMaterialsInspection.json",
    Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
