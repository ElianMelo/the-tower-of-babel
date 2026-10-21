// Unity MCP eval_file authoring capture. Does not save scene camera changes.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets")
    throw new InvalidOperationException("Wrong project");
if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")
    throw new InvalidOperationException("Open the building review scene first");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_BuildingAtlas.mat");
var originalClock=material.GetFloat("_MotionTime");
var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;
var originalSize=camera.orthographicSize;var originalTarget=camera.targetTexture;
var originalActive=RenderTexture.active;
var rt=new RenderTexture(1000,800,24);var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);
const string output="Documentation/FrutigerAero/Buildings/Previews";
const string frames="Temp/BuildingAero/Glass/InnerFrames";
System.IO.Directory.CreateDirectory(output);System.IO.Directory.CreateDirectory(frames);
Action<string> capture=(path)=>{
    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();
    System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());
};
try {
    material.SetFloat("_MotionTime",0);
    camera.transform.position=new Vector3(6,2.65f,-2.8f);
    camera.transform.LookAt(new Vector3(4.15f,1.2f,2.1f));camera.orthographicSize=1.65f;
    capture(output+"/WaterGlass-Inner-Arch.png");
    for(int i=0;i<32;i++){
        material.SetFloat("_MotionTime",i*.25f);
        capture(frames+"/Water-"+i.ToString("D2")+".png");
    }
    material.SetFloat("_MotionTime",0);
    camera.transform.position=new Vector3(4,3,-4);camera.transform.LookAt(new Vector3(1,2.1f,2.1f));
    camera.orthographicSize=2.5f;capture(output+"/WaterGlass-Inner-Pillar.png");
    camera.transform.position=new Vector3(6,3,-6);camera.transform.LookAt(new Vector3(2.6f,.31f,-2.25f));
    camera.orthographicSize=2.4f;capture(output+"/WaterGlass-Inner-Floor.png");
    camera.transform.position=new Vector3(15,11.3f,-23);camera.transform.LookAt(new Vector3(1.25f,2.3f,-.3f));
    camera.orthographicSize=6.3f;capture(output+"/WaterGlass-Inner-Kit.png");
}finally{
    material.SetFloat("_MotionTime",originalClock);
    camera.transform.SetPositionAndRotation(originalPosition,originalRotation);camera.orthographicSize=originalSize;
    camera.targetTexture=originalTarget;RenderTexture.active=originalActive;
    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
}
return new{frames=32,previewClock=material.GetFloat("_MotionTime"),
    messages=ShaderUtil.GetShaderMessages(material.shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
