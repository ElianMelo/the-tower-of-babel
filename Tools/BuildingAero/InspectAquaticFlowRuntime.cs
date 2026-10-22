// Read-only live-scene inspection, invoked through Unity MCP after authoring is complete.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || !EditorApplication.isPlaying ||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Tower.unity")
    throw new InvalidOperationException("Inspect this project's live Tower scene");
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var far=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.InstancedFarChunkRenderer>();
var batches=(System.Collections.IDictionary)far.GetType().GetField("batches",flags).GetValue(far);
Func<Material,object> describe=m=>new{m.name,shader=m.shader.name,m.enableInstancing,m.renderQueue,
    field=m.GetTexture("_WaterField")?.name,ripple=m.GetTexture("_WaterRippleNormal")?.name,
    caustics=m.GetTexture("_WaterLightMap")?.name,clock=m.GetFloat("_MotionTime"),variation=m.GetFloat("_InstanceVariation"),detailDistance=m.GetFloat("_WaterDetailDistance")};
var rows=new List<object>();
foreach(System.Collections.DictionaryEntry pair in batches){
    var b=pair.Value;var type=b.GetType();
    var mesh=(Mesh)type.GetField("mesh",flags).GetValue(b);
    var material=(Material)type.GetField("material",flags).GetValue(b);
    rows.Add(new{mesh=mesh.name,triangles=mesh.triangles.Length/3,material=describe(material)});
}
var near=UnityEngine.Object.FindObjectsByType<TowerOfBabel.Buildings.Building>(FindObjectsSortMode.None);
var colliders=near.Select(x=>x.GetComponent<MeshCollider>()).Where(x=>x!=null).ToArray();
var arch=colliders.First(x=>x.sharedMesh.name.Contains("Arch"));
Func<Vector3,bool> hit=p=>arch.Raycast(new Ray(arch.transform.TransformPoint(p),arch.transform.TransformDirection(Vector3.forward)),out _,4);
var shader=Shader.Find("TowerOfBabel/Buildings/Aquatic Finish");
var report=new{scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,frame=Time.frameCount,time=Time.time,
    nearbyPieces=near.Length,distantInstances=far.LoadedInstanceCount,batchCount=batches.Count,batches=rows,
    nearbyMaterials=near.Select(x=>x.GetComponent<MeshRenderer>().sharedMaterial).Distinct().Select(describe).ToArray(),
    archColliders=colliders.Length,allArchBoundsHaveVolume=colliders.All(c=>c.bounds.size.x>0&&c.bounds.size.y>0&&c.bounds.size.z>0),
    passageHit=hit(new Vector3(.981f,.5f,-2)),upperPanelHit=hit(new Vector3(.981f,1.9f,-2)),leftLegHit=hit(new Vector3(-.1f,.5f,-2)),
    shaderMessages=ShaderUtil.GetShaderMessages(shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AeroFlowTowerRuntime.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return new{report.nearbyPieces,report.distantInstances,report.batchCount,report.archColliders,report.passageHit,report.upperPanelHit,report.leftLegHit,report.shaderMessages};
