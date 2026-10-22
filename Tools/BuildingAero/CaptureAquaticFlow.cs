// Authoring renders for motion/instance comparison; no test runner or scene save.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")throw new Exception("Open this project's review scene");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
var materials=new[]{"Arch","Floor_Tile","Pillar","Step_Tile"}.Select(n=>AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_"+n+".mat")).ToArray();
var clocks=materials.Select(m=>m.GetFloat("_MotionTime")).ToArray();
var arch=materials[0];var variation=arch.GetFloat("_InstanceVariation");var veins=arch.GetFloat("_VeinStrength");
var pos=camera.transform.position;var rot=camera.transform.rotation;var zoom=camera.orthographicSize;
var ortho=camera.orthographic;var target=camera.targetTexture;var active=RenderTexture.active;
var async=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
var rt=new RenderTexture(1000,800,24);var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);
const string output="Documentation/FrutigerAero/Buildings/Previews";
const string frames="Temp/BuildingAero/AeroFlow/Frames";System.IO.Directory.CreateDirectory(frames);
Action<string> capture=path=>{camera.targetTexture=rt;camera.Render();camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());};
var created=new List<GameObject>();object crop=null;
try{
    camera.orthographic=true;camera.transform.position=new Vector3(6,2.65f,-2.8f);
    camera.transform.LookAt(new Vector3(4.15f,1.2f,2.1f));camera.orthographicSize=1.65f;
    for(int i=0;i<32;i++){
        foreach(var material in materials)material.SetFloat("_MotionTime",3+i*.16f);
        capture(frames+"/Flow-"+i.ToString("D2")+".png");
    }
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
    foreach(float x in new[]{11f,15f}){
        var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.name="Temporary flow comparison";
        go.transform.position=new Vector3(x,.11f,2.1f);created.Add(go);
    }
    arch.SetFloat("_MotionTime",4);arch.SetFloat("_VeinStrength",0);
    for(int mode=0;mode<2;mode++){
        arch.SetFloat("_InstanceVariation",mode==0?1:0);
        for(int i=0;i<created.Count;i++){
            var origin=created[i].transform.position;
            camera.transform.position=origin+new Vector3(.981f,1.08f,-6);
            camera.transform.LookAt(origin+new Vector3(.981f,1.08f,0));camera.orthographicSize=1.3f;
            capture(frames+"/Instance-"+(mode==0?"Varied":"Control")+"-"+i+".png");
            var a=camera.WorldToScreenPoint(origin+new Vector3(.4f,1.61f,-.1415f));
            var b=camera.WorldToScreenPoint(origin+new Vector3(1.56f,1.89f,-.1415f));
            crop=new{x=Mathf.CeilToInt(a.x),y=800-Mathf.FloorToInt(b.y),width=Mathf.FloorToInt(b.x-a.x),height=Mathf.FloorToInt(b.y-a.y)};
        }
    }
    arch.SetFloat("_InstanceVariation",1);
    camera.transform.position=new Vector3(13.72f,1.3f,-7);camera.transform.LookAt(new Vector3(13.72f,1.3f,2.1f));camera.orthographicSize=2.5f;
    capture(output+"/AeroFlow-InstanceVariation.png");
}finally{
    for(int i=0;i<materials.Length;i++)materials[i].SetFloat("_MotionTime",clocks[i]);
    arch.SetFloat("_InstanceVariation",variation);arch.SetFloat("_VeinStrength",veins);
    foreach(var go in created)UnityEngine.Object.DestroyImmediate(go);
    camera.transform.SetPositionAndRotation(pos,rot);camera.orthographicSize=zoom;camera.orthographic=ortho;
    camera.targetTexture=target;RenderTexture.active=active;ShaderUtil.allowAsyncCompilation=async;
    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
}
var report=new{frames=32,secondsPerFrame=.16f,previewStartTime=3,clockRestored=materials.Select(m=>m.GetFloat("_MotionTime")).ToArray(),
    instanceComparison="Two copies, same shared material; constant clock and camera-relative pose; marble veins disabled only for comparison",crop};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AeroFlowCaptureInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return report;
