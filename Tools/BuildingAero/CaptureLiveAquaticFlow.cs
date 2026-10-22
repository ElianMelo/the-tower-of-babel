// Live-clock render inspection; must run only after implementation is complete.
if(!EditorApplication.isPlaying||Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")throw new Exception("Use this project's live review scene");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Arch.mat");
if(material.GetFloat("_MotionTime")>=0)throw new Exception("Preview clock must be off for live verification");
var index=SessionState.GetInt("TowerAeroFlowLiveFrame",0);
var position=camera.transform.position;var rotation=camera.transform.rotation;var zoom=camera.orthographicSize;
var target=camera.targetTexture;var active=RenderTexture.active;
var rt=new RenderTexture(1000,800,24);var texture=new Texture2D(1000,800,TextureFormat.RGB24,false);
try{
    camera.transform.position=new Vector3(6,2.65f,-2.8f);camera.transform.LookAt(new Vector3(4.15f,1.2f,2.1f));camera.orthographicSize=1.65f;
    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1000,800),0,0);texture.Apply();
    System.IO.File.WriteAllBytes("Documentation/FrutigerAero/Buildings/Previews/AeroFlow-Live-"+index+".png",texture.EncodeToPNG());
}finally{
    camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=zoom;camera.targetTexture=target;RenderTexture.active=active;
    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
}
var r=new{index,frame=Time.frameCount,time=Time.time,clock=material.GetFloat("_MotionTime"),shader=material.shader.name};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/AeroFlowLiveFrame"+index+".json",Newtonsoft.Json.JsonConvert.SerializeObject(r,Newtonsoft.Json.Formatting.Indented));
SessionState.SetInt("TowerAeroFlowLiveFrame",index+1);return r;
