// Run with Unity MCP eval_file. Dedicated review scene; never a build-settings change.
if(EditorApplication.isPlaying)throw new InvalidOperationException("Edit Mode required");
var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(current.isDirty)throw new InvalidOperationException("Save current scene before opening review");
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(
    UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
const string modelDir="Assets/_Project/Models/Buildings/FrutigerAero";
const string scenePath="Assets/_Project/Scenes/BuildingAeroReview.unity";
var shader=Shader.Find("Universal Render Pipeline/Lit");
Func<string,Color,float,Material> studioMat=(name,color,smooth)=>{
    var path=modelDir+"/"+name+".mat";
    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
    if(m==null){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}
    m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;
    EditorUtility.SetDirty(m);return m;
};
var white=studioMat("Review_White",new Color(.32f,.43f,.61f),.42f);
var blue=studioMat("Review_Blue",new Color(.055f,.23f,.51f),.6f);
Func<string,Vector3,Vector3,Material,GameObject> block=(name,pos,size,mat)=>{
    var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;
    go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;
    return go;
};
Func<string,Vector3,GameObject> piece=(name,pos)=>{
    var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/"+name+".prefab");
    var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
    go.transform.position=pos;go.name=name+" — Review";return go;
};
Action<string,Vector3,float> label=(text,pos,size)=>{
    var go=new GameObject("Label — "+text);go.transform.position=pos;
    var t=go.AddComponent<TextMesh>();t.text=text;t.fontSize=64;t.characterSize=size;
    t.anchor=TextAnchor.MiddleCenter;t.alignment=TextAlignment.Center;
    t.color=new Color(.08f,.28f,.55f);t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;
};

block("Studio Floor",new Vector3(0,-.22f,0),new Vector3(90,.2f,90),white);
block("Assembly Plinth",new Vector3(-4,.0f,.0f),new Vector3(4.6f,.22f,5.2f),blue);
piece("Floor_Tile",new Vector3(-6,.30f,2));
piece("Pillar",new Vector3(-5,.10f,0));
piece("Pillar",new Vector3(-3.0379943f,.10f,0));
piece("Pillar",new Vector3(-5,.10f,1.45f));
piece("Pillar",new Vector3(-3.0379943f,.10f,1.45f));
piece("Arch",new Vector3(-5,3.95f,0));
piece("Arch",new Vector3(-5,3.95f,1.45f));
piece("Floor_Tile",new Vector3(-6,6.30f,2));
label("ASSEMBLED BAY",new Vector3(-4,.07f,-2.63f),.045f);

block("Pillar Display",new Vector3(1.0f,.0f,2.1f),new Vector3(1.6f,.22f,1.6f),blue);
piece("Pillar",new Vector3(1,.11f,2.1f));
label("PILLAR",new Vector3(1,.05f,1.28f),.038f);
block("Arch Display",new Vector3(4.15f,.0f,2.1f),new Vector3(3.0f,.22f,1.4f),blue);
piece("Arch",new Vector3(3.17f,.11f,2.1f));
label("ARCH",new Vector3(4.15f,.05f,1.38f),.038f);
block("Floor Display",new Vector3(2.6f,.0f,-2.25f),new Vector3(4.2f,.22f,4.2f),blue);
piece("Floor_Tile",new Vector3(.6f,.31f,-.25f));
label("FLOOR TILE",new Vector3(2.6f,.05f,-4.38f),.04f);
block("Step Display",new Vector3(7.3f,.0f,-2.25f),new Vector3(4.2f,.22f,1.65f),blue);
piece("Step_Tile",new Vector3(5.3f,.31f,-1.55f));
label("STEP TILE",new Vector3(7.3f,.05f,-3.10f),.04f);

RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;
RenderSettings.ambientSkyColor=new Color(.32f,.42f,.62f);
RenderSettings.ambientEquatorColor=new Color(.20f,.26f,.38f);
RenderSettings.ambientGroundColor=new Color(.12f,.15f,.22f);
RenderSettings.ambientIntensity=1;RenderSettings.fog=false;
Action<string,Vector3,float,Color> sun=(name,rot,intensity,color)=>{
    var go=new GameObject(name);var l=go.AddComponent<Light>();l.type=LightType.Directional;
    go.transform.rotation=Quaternion.Euler(rot);l.intensity=intensity;l.color=color;
    l.shadows=LightShadows.Soft;l.shadowStrength=.65f;l.shadowBias=.025f;
};
sun("Key — White",new Vector3(48,-30,0),1.15f,new Color(1,.97f,.94f));
sun("Fill — Blue",new Vector3(25,155,0),.3f,new Color(.62f,.79f,1));
var probeGo=new GameObject("Studio Reflection");var probe=probeGo.AddComponent<ReflectionProbe>();
probeGo.transform.position=new Vector3(1,2,0);probe.size=new Vector3(40,20,30);
probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Realtime;
probe.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.ViaScripting;
probe.clearFlags=UnityEngine.Rendering.ReflectionProbeClearFlags.SolidColor;
probe.backgroundColor=new Color(.55f,.68f,.89f);probe.resolution=128;
probe.intensity=.8f;probe.RenderProbe();
var cameraGo=new GameObject("Aero Review Camera");cameraGo.tag="MainCamera";
var camera=cameraGo.AddComponent<Camera>();cameraGo.AddComponent<AudioListener>();
cameraGo.transform.position=new Vector3(15,11.3f,-23);
cameraGo.transform.LookAt(new Vector3(1.25f,2.3f,-.3f));
camera.orthographic=true;camera.orthographicSize=6.3f;
camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.73f,.83f,.96f);
camera.nearClipPlane=.1f;camera.farClipPlane=140;camera.allowHDR=true;
var data=cameraGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
data.renderPostProcessing=true;data.antialiasing=UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
var profilePath=modelDir+"/Review_VolumeProfile.asset";
var profile=AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
if(profile==null){profile=ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();AssetDatabase.CreateAsset(profile,profilePath);}
UnityEngine.Rendering.Universal.Tonemapping tonemap;
if(!profile.TryGet(out tonemap)){tonemap=profile.Add<UnityEngine.Rendering.Universal.Tonemapping>();AssetDatabase.AddObjectToAsset(tonemap,profile);}
tonemap.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);
var volumeGo=new GameObject("Review Color Management");var volume=volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
volume.isGlobal=true;volume.sharedProfile=profile;EditorUtility.SetDirty(profile);
if(SceneView.lastActiveSceneView!=null){
    SceneView.lastActiveSceneView.LookAtDirect(new Vector3(1,2,0),cameraGo.transform.rotation,15);
    SceneView.lastActiveSceneView.sceneLighting=true;
}
AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene,scenePath);
return new{scene=scenePath,objects=scene.rootCount,camera=camera.name};
