// Unity MCP eval_file: captures the saved kit and temporary identical-instance studies.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets")
    throw new InvalidOperationException("Wrong project");
if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")
    throw new InvalidOperationException("Open the building review scene first");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
const string matDir="Assets/_Project/Models/Buildings/FrutigerAero";
var names=new[]{"Arch","Floor_Tile","Pillar","Step_Tile"};
var materials=names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(matDir+"/Aero_WaterGlass_"+n+".mat")).ToArray();
var clocks=materials.Select(m=>m.GetFloat("_MotionTime")).ToArray();
var arch=materials[0];var layers=arch.GetFloat("_LayerCount");
var position=camera.transform.position;var rotation=camera.transform.rotation;
var size=camera.orthographicSize;var target=camera.targetTexture;var active=RenderTexture.active;
var rt=new RenderTexture(1200,900,24);var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
const string output="Documentation/FrutigerAero/Buildings/Previews";
const string frames="Temp/BuildingAero/Glass/LayeredFrames";
System.IO.Directory.CreateDirectory(output);System.IO.Directory.CreateDirectory(frames);
var temporary=new List<GameObject>();
Action<float> clock=t=>{foreach(var m in materials)m.SetFloat("_MotionTime",t);};
Action<string> capture=path=>{
    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1200,900),0,0);texture.Apply();
    System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
};
try{
    clock(0);
    camera.transform.position=new Vector3(6,2.65f,-2.8f);
    camera.transform.LookAt(new Vector3(4.15f,1.2f,2.1f));camera.orthographicSize=1.65f;
    capture(output+"/LayeredWater-Arch.png");
    arch.SetFloat("_LayerCount",2);capture(output+"/LayeredWater-TwoLayers.png");
    arch.SetFloat("_LayerCount",layers);
    for(int i=0;i<32;i++){clock(i*.25f);capture(frames+"/Water-"+i.ToString("D2")+".png");}
    clock(0);
    camera.transform.position=new Vector3(4,3,-4);camera.transform.LookAt(new Vector3(1,2.1f,2.1f));
    camera.orthographicSize=2.5f;capture(output+"/LayeredWater-Pillar.png");
    camera.transform.position=new Vector3(6,3,-6);camera.transform.LookAt(new Vector3(2.6f,.31f,-2.25f));
    camera.orthographicSize=2.4f;capture(output+"/LayeredWater-Floor.png");
    camera.transform.position=new Vector3(9,2.7f,-6);camera.transform.LookAt(new Vector3(7.3f,.31f,-2.25f));
    camera.orthographicSize=2.2f;capture(output+"/LayeredWater-Step.png");
    camera.transform.position=new Vector3(15,11.3f,-23);camera.transform.LookAt(new Vector3(1.25f,2.3f,-.3f));
    camera.orthographicSize=6.3f;capture(output+"/LayeredWater-Kit.png");
    for(int i=0;i<3;i++){
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);temporary.Add(go);
        go.name="Temporary same-material arch "+i;go.transform.position=new Vector3(20+i*2.7f,.11f,0);
    }
    camera.transform.position=new Vector3(23.68f,3.4f,-14);
    camera.transform.LookAt(new Vector3(23.68f,1.2f,0));camera.orthographicSize=3.2f;
    capture(output+"/LayeredWater-InstanceVariation.png");
}finally{
    foreach(var go in temporary)UnityEngine.Object.DestroyImmediate(go);
    for(int i=0;i<materials.Length;i++)materials[i].SetFloat("_MotionTime",clocks[i]);
    arch.SetFloat("_LayerCount",layers);
    camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
    camera.targetTexture=target;RenderTexture.active=active;
    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
}
return new{frames=32,clocks=materials.Select(m=>m.GetFloat("_MotionTime")).ToArray(),
    messages=ShaderUtil.GetShaderMessages(arch.shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
