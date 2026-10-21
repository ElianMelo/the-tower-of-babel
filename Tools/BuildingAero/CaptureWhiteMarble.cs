// Unity MCP authoring capture. Restores camera settings; does not save scene edits.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")
    throw new InvalidOperationException("Open this project's building review scene");
var camera=Camera.allCameras.First(c=>c.name=="Aero Review Camera");
var position=camera.transform.position;var rotation=camera.transform.rotation;
var size=camera.orthographicSize;var target=camera.targetTexture;var active=RenderTexture.active;
var orthographic=camera.orthographic;var fieldOfView=camera.fieldOfView;
var asyncCompilation=ShaderUtil.allowAsyncCompilation;
ShaderUtil.allowAsyncCompilation=false;
var rt=new RenderTexture(1400,1050,24);var texture=new Texture2D(1400,1050,TextureFormat.RGB24,false);
const string output="Documentation/FrutigerAero/Buildings/Previews";
System.IO.Directory.CreateDirectory(output);
Action<string,Vector3,Vector3,float> capture=(name,pos,look,zoom)=>{
    camera.transform.position=pos;camera.transform.LookAt(look);camera.orthographicSize=zoom;
    camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
    texture.ReadPixels(new Rect(0,0,1400,1050),0,0);texture.Apply();
    System.IO.File.WriteAllBytes(output+"/AquaticArchitecture-"+name+".png",texture.EncodeToPNG());
};
try{
    capture("Kit",new Vector3(15,11.3f,-23),new Vector3(1.25f,2.3f,-.3f),6.3f);
    capture("Arch",new Vector3(6,2.65f,-2.8f),new Vector3(4.15f,1.2f,2.1f),1.65f);
    var archMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Arch.mat");
    var strength=archMaterial.GetFloat("_NormalStrength");
    var bakedStrength=archMaterial.GetFloat("_BakedStrength");
    try{archMaterial.SetFloat("_NormalStrength",0);archMaterial.SetFloat("_BakedStrength",0);
        capture("Arch-NoNormal",new Vector3(6,2.65f,-2.8f),new Vector3(4.15f,1.2f,2.1f),1.65f);
    }finally{archMaterial.SetFloat("_NormalStrength",strength);archMaterial.SetFloat("_BakedStrength",bakedStrength);}
    capture("Pillar",new Vector3(4,3,-4),new Vector3(1,2.1f,2.1f),2.5f);
    capture("Capital",new Vector3(2.3f,4.4f,-.5f),new Vector3(1,3.8f,2.1f),.72f);
    capture("Floor",new Vector3(6,3,-6),new Vector3(2.6f,.31f,-2.25f),2.4f);
    capture("Step",new Vector3(9,2.7f,-6),new Vector3(7.3f,.31f,-2.25f),2.2f);
    camera.orthographic=false;camera.fieldOfView=45;
    capture("Assembly",new Vector3(1,6.8f,-8),new Vector3(-4,3.15f,.4f),6);
}finally{
    ShaderUtil.allowAsyncCompilation=asyncCompilation;
    camera.transform.SetPositionAndRotation(position,rotation);camera.orthographicSize=size;
    camera.orthographic=orthographic;camera.fieldOfView=fieldOfView;
    camera.targetTexture=target;RenderTexture.active=active;
    UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);
}
var shader=Shader.Find("TowerOfBabel/Buildings/White Marble");
return new{shader=shader.name,messages=ShaderUtil.GetShaderMessages(shader).Select(x=>new{x.message,severity=x.severity.ToString()}).ToArray()};
