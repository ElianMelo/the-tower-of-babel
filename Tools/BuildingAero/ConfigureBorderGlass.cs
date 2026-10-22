// REJECTED EXPERIMENT: only run if the user explicitly requests see-through glass again.
// Unity MCP authoring: install the tagged glass pass on this project's two URP renderers.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)
    throw new InvalidOperationException("Use this project in Edit Mode");
var renderers=new List<object>();
foreach(var path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"}){
    var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
    if(data==null)throw new Exception("Missing project renderer: "+path);
    var feature=data.rendererFeatures.OfType<TowerOfBabel.Rendering.BuildingGlassRendererFeature>().FirstOrDefault();
    if(feature==null){
        feature=ScriptableObject.CreateInstance<TowerOfBabel.Rendering.BuildingGlassRendererFeature>();
        feature.name="Building Glass Borders";
        AssetDatabase.AddObjectToAsset(feature,data);data.rendererFeatures.Add(feature);
        var so=new SerializedObject(data);var map=so.FindProperty("m_RendererFeatureMap");
        map.arraySize=data.rendererFeatures.Count;
        for(int i=0;i<data.rendererFeatures.Count;i++){
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i],out string guid,out long localId);
            map.GetArrayElementAtIndex(i).longValue=localId;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    feature.SetActive(true);feature.Create();data.SetDirty();EditorUtility.SetDirty(data);EditorUtility.SetDirty(feature);
    renderers.Add(new{path,features=data.rendererFeatures.Select(f=>new{f.name,type=f.GetType().Name}).ToArray()});
}
var names=new[]{"Arch","Floor_Tile","Pillar","Step_Tile"};
var transmission=new[]{.86f,.91f,.87f,.92f};var thickness=new[]{.12f,.085f,.10f,.065f};
var materials=new List<object>();
for(int i=0;i<names.Length;i++){
    var m=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_"+names[i]+".mat");
    m.SetFloat("_BorderGlass",1);m.EnableKeyword("_AERO_BORDER_GLASS");
    m.SetFloat("_GlassTransmission",transmission[i]);m.SetFloat("_GlassThickness",thickness[i]);
    m.SetFloat("_GlassRefraction",.8f);m.SetFloat("_GlassIOR",1.45f);
    m.SetColor("_GlassTint",new Color(.68f,.93f,1,1));
    m.SetFloat("_GlassBorderWidth",.12f);m.SetFloat("_GlassBorderStrength",1);
    m.SetFloat("_MotionTime",-1);EditorUtility.SetDirty(m);
    materials.Add(new{name=names[i],m.enableInstancing,transmission=transmission[i],thickness=thickness[i],glassPass=m.FindPass("GlassBorders"),borderGlass=m.GetFloat("_BorderGlass"),keyword=m.IsKeywordEnabled("_AERO_BORDER_GLASS")});
}
AssetDatabase.SaveAssets();
var report=new{renderers,materials,shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/Buildings/Aquatic Finish")).Select(m=>new{m.message,m.line,severity=m.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/BorderGlassConfiguration.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
