// Authoring visual comparison and collider/renderer inspection. No test runner or Play Mode.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)throw new Exception("Use this project in Edit Mode");
var originalScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();if(originalScene.isDirty)throw new Exception("Save current edits before review");
string originalPath=originalScene.path;
const string dir="Assets/_Project/Models/Buildings/FrutigerAero";
AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
var imp=(ModelImporter)AssetImporter.GetAtPath(dir+"/Review_PreTiling_Floor.fbx");
imp.bakeAxisConversion=true;imp.isReadable=true;imp.importAnimation=false;imp.SaveAndReimport();
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,UnityEditor.SceneManagement.NewSceneMode.Single);
var oldMesh=AssetDatabase.LoadAllAssetsAtPath(dir+"/Review_PreTiling_Floor.fbx").OfType<Mesh>().Single();
var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
var mesh=prefab.GetComponent<MeshFilter>().sharedMesh;var source=prefab.GetComponent<MeshRenderer>().sharedMaterial;
var material=new Material(source){enableInstancing=true};material.SetFloat("_MotionTime",4);
var cpuMaterial=new Material(material){enableInstancing=true};
var cache=new TowerOfBabel.World.Tower.RadialFloorMeshCache();
var oldRows=Newtonsoft.Json.Linq.JArray.Parse(System.IO.File.ReadAllText("Temp/BuildingAero/RadialFloor/Floors-Before.json"));
var oldMatrices=oldRows.Where(x=>(float)x["position"][1]<1).Select(x=>Matrix4x4.TRS(
    new Vector3((float)x["position"][0],(float)x["position"][1],(float)x["position"][2]),
    new Quaternion((float)x["rotation"][0],(float)x["rotation"][1],(float)x["rotation"][2],(float)x["rotation"][3]),
    new Vector3((float)x["scale"][0],(float)x["scale"][1],(float)x["scale"][2]))).ToArray();
int oldTriangles=oldMesh.triangles.Length/3,newTriangles=mesh.triangles.Length/3;
var camera=Camera.main;camera.orthographic=true;camera.orthographicSize=108;camera.farClipPlane=1000;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.37f,.54f,.62f);
var light=UnityEngine.Object.FindFirstObjectByType<Light>();light.transform.rotation=Quaternion.Euler(52,-30,0);light.intensity=.7f;light.shadows=LightShadows.Soft;
RenderSettings.skybox=null;RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
RenderSettings.ambientSkyColor=new Color(.68f,.78f,.84f);RenderSettings.ambientEquatorColor=new Color(.55f,.65f,.70f);RenderSettings.ambientGroundColor=new Color(.32f,.42f,.47f);
var ambient=new UnityEngine.Rendering.SphericalHarmonicsL2();ambient.AddAmbientLight(new Color(.12f,.15f,.18f));RenderSettings.ambientProbe=ambient;RenderSettings.reflectionIntensity=.15f;
var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="Review backdrop";ground.transform.position=new Vector3(0,-.3f,0);ground.transform.localScale=new Vector3(24,1,24);
var groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));groundMaterial.SetColor("_BaseColor",new Color(.11f,.23f,.30f));groundMaterial.SetFloat("_Smoothness",.1f);ground.GetComponent<MeshRenderer>().sharedMaterial=groundMaterial;
var rt=new RenderTexture(1600,1200,24);var texture=new Texture2D(1600,1200,TextureFormat.RGB24,false);var priorRT=RenderTexture.active;
bool async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
Mesh drawnMesh=mesh;Material drawnMaterial=material;Matrix4x4[] matrices=Array.Empty<Matrix4x4>();
Action<UnityEngine.Rendering.ScriptableRenderContext,Camera> draw=(context,c)=>{
    if(c!=camera)return;
    var rp=new RenderParams(drawnMaterial){camera=c,worldBounds=new Bounds(new Vector3(0,.1f,0),new Vector3(220,2,220)),shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On,receiveShadows=true,lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off};
    for(int start=0;start<matrices.Length;start+=1023)Graphics.RenderMeshInstanced(rp,drawnMesh,0,matrices,Mathf.Min(1023,matrices.Length-start),start);
};
Action<string,Vector3,Vector3,float> capture=(name,pos,look,size)=>{
    camera.transform.position=pos;camera.transform.LookAt(look);camera.orthographicSize=size;camera.targetTexture=rt;
    for(int warm=0;warm<8;warm++)camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1600,1200),0,0);texture.Apply();
    System.IO.File.WriteAllBytes("Documentation/FrutigerAero/Buildings/Previews/RadialFloor-"+name+".png",texture.EncodeToPNG());
};
UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering+=draw;
var inspections=new List<object>();
try{
    drawnMesh=oldMesh;matrices=oldMatrices;TowerOfBabel.World.Tower.TowerFloorLayout.ConfigureMaterial(material,false,Vector2.zero);
    material.SetFloat("_BakedStrength",0);material.SetFloat("_AOStrength",0);
    capture("Before",new Vector3(100,145,-140),new Vector3(0,.2f,0),105);
    capture("Before-Joins",new Vector3(100,17,-13),new Vector3(87,.2f,8),12);
    drawnMesh=mesh;var sectors=TowerOfBabel.World.Tower.TowerFloorLayout.Build(Vector2.zero,.2f,93,3.9f,96*2*Mathf.Deg2Rad).ToArray();
    matrices=sectors.Select(s=>Matrix4x4.TRS(s.Position,s.Rotation,s.Scale)).ToArray();TowerOfBabel.World.Tower.TowerFloorLayout.ConfigureMaterial(material,true,Vector2.zero);
    material.SetFloat("_BakedStrength",source.GetFloat("_BakedStrength"));material.SetFloat("_AOStrength",source.GetFloat("_AOStrength"));
    capture("After",new Vector3(100,145,-140),new Vector3(0,.2f,0),105);
    capture("After-Joins",new Vector3(100,17,-13),new Vector3(87,.2f,8),12);
    capture("Top",new Vector3(0,180,0),new Vector3(0,.2f,0),98);
    capture("Center",new Vector3(10,15,-12),new Vector3(0,.2f,0),10);
    matrices=Array.Empty<Matrix4x4>();
    foreach(float radius in new[]{93f,30f,3.3f}){
        var s=TowerOfBabel.World.Tower.TowerFloorLayout.Build(Vector2.zero,.2f,radius,3.9f,96*2*Mathf.Deg2Rad).First();
        var bent=cache.Get(mesh,s.Radius,s.Scale);var go=new GameObject("Sector collision inspection");go.transform.SetPositionAndRotation(s.Position,s.Rotation);go.transform.localScale=s.Scale;
        var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=bent;
        var p=go.transform.TransformPoint(TowerOfBabel.World.Tower.TowerFloorLayout.Bend(new Vector3(2,0,-2),s.Radius,s.Scale));
        Physics.SyncTransforms();bool hit=collider.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var result,4);
        inspections.Add(new{radius,hit,hitY=hit?result.point.y:-999f,volume=collider.bounds.size.ToString(),bentTriangles=bent.triangles.Length/3});
        UnityEngine.Object.DestroyImmediate(go);
    }
    var sample=TowerOfBabel.World.Tower.TowerFloorLayout.Build(Vector2.zero,.2f,8,3.9f,96*2*Mathf.Deg2Rad).First();
    matrices=new[]{Matrix4x4.TRS(sample.Position,sample.Rotation,sample.Scale)};
    material.SetFloat("_GlassBorderStrength",0);
    capture("GPU-Sector",new Vector3(12,6,-2),new Vector3(6,.1f,1.3f),3.2f);
    drawnMesh=cache.Get(mesh,sample.Radius,sample.Scale);drawnMaterial=cpuMaterial;
    TowerOfBabel.World.Tower.TowerFloorLayout.ConfigureMaterial(cpuMaterial,true,Vector2.zero,true);cpuMaterial.SetFloat("_GlassBorderStrength",0);
    capture("CPU-Sector",new Vector3(12,6,-2),new Vector3(6,.1f,1.3f),3.2f);
}finally{
    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering-=draw;camera.targetTexture=null;RenderTexture.active=priorRT;ShaderUtil.allowAsyncCompilation=async;
    cache.Dispose();foreach(var obj in new UnityEngine.Object[]{material,cpuMaterial,groundMaterial,rt,texture})UnityEngine.Object.DestroyImmediate(obj);
    UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalPath,UnityEditor.SceneManagement.OpenSceneMode.Single);
}
var report=new{triangles=newTriangles,oldTriangles,oldFloorInstances=oldMatrices.Length,collision=inspections,
    compilationFailed=EditorUtility.scriptCompilationFailed,shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/Buildings/Aquatic Finish")).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray(),
    scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,playing=EditorApplication.isPlaying};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/RadialFloorRenderInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
