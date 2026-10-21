// Read-only authoring inspection through Unity MCP. Does not run the test runner.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets")throw new Exception("Wrong project");
var rows=new List<object>();
foreach(var name in new[]{"Arch","Floor_Tile","Pillar","Step_Tile"}){
    var path="Assets/_Project/Prefabs/Buildings/"+name+".prefab";
    var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);
    var mesh=root.GetComponent<MeshFilter>().sharedMesh;var material=root.GetComponent<MeshRenderer>().sharedMaterial;
    var so=new SerializedObject(root.GetComponent<TowerOfBabel.Buildings.Building>());var stages=so.FindProperty("stageMeshes");
    bool matching=true;for(int i=0;i<stages.arraySize;i++)matching&=stages.GetArrayElementAtIndex(i).objectReferenceValue==mesh;
    rows.Add(new{name,guid=AssetDatabase.AssetPathToGUID(path),triangles=mesh.triangles.Length/3,vertices=mesh.vertexCount,
        submeshes=mesh.subMeshCount,uv0=mesh.uv.Length,uv1=mesh.uv2.Length,tangents=mesh.tangents.Length,normals=mesh.normals.Length,
        min=mesh.bounds.min.ToString("F6"),max=mesh.bounds.max.ToString("F6"),scale=root.transform.localScale.ToString(),
        children=root.transform.childCount,stages=stages.arraySize,matchingStageMeshes=matching,
        surface=material.GetTexture("_BakedSurface")?.name,normal=material.GetTexture("_BakedNormal")?.name,ao=material.GetTexture("_BakedAO")?.name,
        material.enableInstancing,material.renderQueue,collider=root.GetComponent<Collider>().GetType().Name});
}
var shader=Shader.Find("TowerOfBabel/Buildings/White Marble");
var report=new{models=rows,messages=ShaderUtil.GetShaderMessages(shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AquaticArchitectureInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
