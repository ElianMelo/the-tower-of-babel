// Restore the finish preceding the rejected see-through glass revision.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)
    throw new InvalidOperationException("Use this project in Edit Mode");
var renderers=new List<object>();
foreach(var path in new[]{"Assets/Settings/PC_Renderer.asset","Assets/Settings/Mobile_Renderer.asset"}){
    var data=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRendererData>(path);
    var removed=data.rendererFeatures.Where(f=>f!=null&&f.GetType().FullName=="TowerOfBabel.Rendering.BuildingGlassRendererFeature").ToArray();
    foreach(var feature in removed){data.rendererFeatures.Remove(feature);UnityEngine.Object.DestroyImmediate(feature,true);}
    var so=new SerializedObject(data);var map=so.FindProperty("m_RendererFeatureMap");map.arraySize=data.rendererFeatures.Count;
    for(int i=0;i<data.rendererFeatures.Count;i++){
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(data.rendererFeatures[i],out string guid,out long localId);
        map.GetArrayElementAtIndex(i).longValue=localId;
    }
    so.ApplyModifiedPropertiesWithoutUndo();data.SetDirty();EditorUtility.SetDirty(data);
    renderers.Add(new{path,features=data.rendererFeatures.Select(f=>f.GetType().Name).ToArray(),removed=removed.Length});
}
var materials=new List<object>();
foreach(var name in new[]{"Arch","Floor_Tile","Pillar","Step_Tile"}){
    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_"+name+".mat");
    material.SetFloat("_BorderGlass",0);material.DisableKeyword("_AERO_BORDER_GLASS");
    material.SetFloat("_GlassBorderWidth",.11f);material.SetFloat("_GlassBorderStrength",.7f);
    material.SetFloat("_MotionTime",-1);EditorUtility.SetDirty(material);
    materials.Add(new{name,shader=material.shader.name,seeThrough=material.GetFloat("_BorderGlass"),keyword=material.IsKeywordEnabled("_AERO_BORDER_GLASS"),
        borderWidth=material.GetFloat("_GlassBorderWidth"),borderStrength=material.GetFloat("_GlassBorderStrength"),clock=material.GetFloat("_MotionTime"),material.enableInstancing});
}
AssetDatabase.SaveAssets();
var report=new{restored="Opaque aquatic finish with layered moving water; see-through glass rejected by user",renderers,materials,
    compilationFailed=EditorUtility.scriptCompilationFailed,
    shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/Buildings/Aquatic Finish")).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AquaticFinishRestored.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
