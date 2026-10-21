// Dedicated review-scene lighting, never the gameplay scene's environment.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying||
    UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/BuildingAeroReview.unity")throw new Exception("Open this project's review scene in Edit Mode");
const string dir="Assets/_Project/Models/Buildings/FrutigerAero/";
var floor=AssetDatabase.LoadAssetAtPath<Material>(dir+"Review_White.mat");floor.SetColor("_BaseColor",new Color(.60f,.72f,.78f));floor.SetFloat("_Smoothness",.7f);EditorUtility.SetDirty(floor);
var plinth=AssetDatabase.LoadAssetAtPath<Material>(dir+"Review_Blue.mat");plinth.SetColor("_BaseColor",new Color(.15f,.36f,.45f));plinth.SetFloat("_Smoothness",.7f);EditorUtility.SetDirty(plinth);
foreach(var backdrop in new[]{floor,plinth}){backdrop.SetFloat("_EnvironmentReflections",0);backdrop.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");}
RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
RenderSettings.ambientSkyColor=new Color(.28f,.42f,.45f);RenderSettings.ambientEquatorColor=new Color(.16f,.25f,.28f);RenderSettings.ambientGroundColor=new Color(.10f,.14f,.17f);
foreach(var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None)){
    if(light.name=="Key — White"){light.intensity=1.4f;light.color=Color.white;light.shadowStrength=.55f;}
    if(light.name=="Fill — Blue"){light.intensity=.35f;light.color=new Color(.72f,.94f,1);}
}
var softbox=AssetDatabase.LoadAssetAtPath<Material>(dir+"Review_Softbox.mat");
if(softbox==null){softbox=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(softbox,dir+"Review_Softbox.mat");}
softbox.SetColor("_BaseColor",new Color(8f,8.5f,9f));EditorUtility.SetDirty(softbox);
Action<string,Vector3,Vector3> panel=(name,pos,scale)=>{
    var go=GameObject.Find(name);if(go==null)go=GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name=name;go.transform.position=pos;go.transform.localScale=scale;
    go.layer=31;
    GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ReflectionProbeStatic);
    go.GetComponent<Renderer>().sharedMaterial=softbox;go.GetComponent<Renderer>().shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
    var collider=go.GetComponent<Collider>();if(collider!=null)UnityEngine.Object.DestroyImmediate(collider);
};
panel("Studio ceiling reflection",new Vector3(0,14,0),new Vector3(35,.1f,30));
panel("Studio left reflection",new Vector3(-16,5,0),new Vector3(.1f,8,18));
panel("Studio front reflection",new Vector3(-2,2,-12),new Vector3(3,10,.1f));
panel("Studio right reflection",new Vector3(9,3,-10),new Vector3(2,8,.1f));
var camera=Camera.allCameras.First(x=>x.name=="Aero Review Camera");camera.backgroundColor=new Color(.72f,.88f,.93f);
camera.cullingMask&=~(1<<31);
var probe=UnityEngine.Object.FindFirstObjectByType<ReflectionProbe>();probe.backgroundColor=new Color(.025f,.09f,.15f);probe.intensity=1;
probe.resolution=256;probe.cullingMask=-1;
probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Baked;
var reflectionPath=dir+"Review_AquaticReflection.exr";
var asyncCompilation=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
bool baked;
try{baked=Lightmapping.BakeReflectionProbe(probe,reflectionPath);}finally{ShaderUtil.allowAsyncCompilation=asyncCompilation;}
AssetDatabase.ImportAsset(reflectionPath,ImportAssetOptions.ForceSynchronousImport);
var reflection=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);
if(!baked||reflection==null)throw new Exception("Review reflection bake failed");
probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Custom;probe.customBakedTexture=reflection;
RenderSettings.defaultReflectionMode=UnityEngine.Rendering.DefaultReflectionMode.Custom;RenderSettings.customReflection=reflection;
AssetDatabase.SaveAssets();UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
return new{baked,reflection=reflection.name,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path};
