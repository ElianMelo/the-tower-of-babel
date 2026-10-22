// Authoring comparison renders; no test runner, scene save or Play Mode.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying||
 UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")throw new Exception("Use this project's review scene in Edit Mode");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
var pos=camera.transform.position;var rot=camera.transform.rotation;var zoom=camera.orthographicSize;
var ortho=camera.orthographic;var target=camera.targetTexture;var active=RenderTexture.active;
var async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Arch.mat");
var clock=material.GetFloat("_MotionTime");var glass=material.GetFloat("_BorderGlass");var keyword=material.IsKeywordEnabled("_AERO_BORDER_GLASS");
var rt=new RenderTexture(1000,800,24);var image=new Texture2D(1000,800,TextureFormat.RGB24,false);
var pattern=new Texture2D(16,8,TextureFormat.RGBA32,false);pattern.filterMode=FilterMode.Point;
var boardMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
boardMaterial.SetTexture("_BaseMap",pattern);
var board=GameObject.CreatePrimitive(PrimitiveType.Quad);board.name="Temporary glass transmission background";
board.transform.position=new Vector3(11.981f,1.86f,2.75f);board.transform.localScale=new Vector3(2.2f,.65f,1);
board.GetComponent<MeshRenderer>().sharedMaterial=boardMaterial;
board.GetComponent<MeshRenderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
var arch=(GameObject)PrefabUtility.InstantiatePrefab(prefab);arch.name="Temporary glass comparison";arch.transform.position=new Vector3(11,.11f,2.1f);
var renderer=arch.GetComponent<MeshRenderer>();var mesh=arch.GetComponent<MeshFilter>().sharedMesh;
const string output="Documentation/FrutigerAero/Buildings/Previews";
Action<int> paint=phase=>{for(int y=0;y<8;y++)for(int x=0;x<16;x++)pattern.SetPixel(x,y,((x/2+y/2+phase)%2==0)?new Color(.12f,.45f,.95f,1):Color.white);pattern.Apply();};
// The Editor's renderer batching can need several renders after a keyword change.
Action<string> capture=name=>{camera.targetTexture=rt;for(int warm=0;warm<6;warm++)camera.Render();RenderTexture.active=rt;
    image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();System.IO.File.WriteAllBytes(output+"/BorderGlass-"+name+".png",image.EncodeToPNG());};
Action<UnityEngine.Rendering.ScriptableRenderContext,Camera> draw=null;
object crop=null;
try{
    camera.orthographic=true;camera.orthographicSize=1.3f;
    camera.transform.position=new Vector3(11.981f,1.19f,-4);camera.transform.LookAt(new Vector3(11.981f,1.19f,2.1f));
    material.SetFloat("_MotionTime",4);material.SetFloat("_BorderGlass",1);material.EnableKeyword("_AERO_BORDER_GLASS");
    paint(0);capture("Transmission-A");paint(1);capture("Transmission-B");
    var a=camera.WorldToScreenPoint(new Vector3(11.4f,1.72f,1.9585f));var b=camera.WorldToScreenPoint(new Vector3(12.56f,2,1.9585f));
    crop=new{x=Mathf.CeilToInt(a.x),y=800-Mathf.FloorToInt(b.y),width=Mathf.FloorToInt(b.x-a.x),height=Mathf.FloorToInt(b.y-a.y)};
    material.DisableKeyword("_AERO_BORDER_GLASS");material.SetFloat("_BorderGlass",0);
    paint(0);capture("Opaque-Control-A");paint(1);capture("Opaque-Control-B");
    material.EnableKeyword("_AERO_BORDER_GLASS");material.SetFloat("_BorderGlass",1);paint(0);
    renderer.enabled=false;
    var matrices=new[]{arch.transform.localToWorldMatrix};
    var parameters=new UnityEngine.RenderParams(material){camera=camera,worldBounds=new Bounds(new Vector3(11.981f,1.19f,2.1f),new Vector3(2.5f,2.3f,.5f)),
        shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off,receiveShadows=true,reflectionProbeUsage=UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes};
    draw=(context,c)=>{if(c==camera)Graphics.RenderMeshInstanced(parameters,mesh,0,matrices);};
    UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering+=draw;
    capture("Instanced-Transmission");
    camera.transform.position=new Vector3(11.981f,1.19f,-100);
    camera.transform.LookAt(new Vector3(11.981f,1.19f,2.1f));
    capture("Instanced-DistantFallback");
}finally{
    if(draw!=null)UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering-=draw;
    material.SetFloat("_MotionTime",clock);material.SetFloat("_BorderGlass",glass);
    if(keyword)material.EnableKeyword("_AERO_BORDER_GLASS");else material.DisableKeyword("_AERO_BORDER_GLASS");
    camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=zoom;camera.orthographic=ortho;camera.targetTexture=target;RenderTexture.active=active;
    ShaderUtil.allowAsyncCompilation=async;
    foreach(var obj in new UnityEngine.Object[]{arch,board,boardMaterial,pattern,image,rt})UnityEngine.Object.DestroyImmediate(obj);
}
var report=new{crop,clockRestored=material.GetFloat("_MotionTime"),keywordRestored=material.IsKeywordEnabled("_AERO_BORDER_GLASS"),
    method="Fixed camera and material clock; two checkerboard backgrounds; opaque-shader control; Graphics.RenderMeshInstanced capture at the same transform",shaderMessages=ShaderUtil.GetShaderMessages(material.shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/BorderGlassCaptureInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
