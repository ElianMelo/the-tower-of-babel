if(UnityEngine.Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    throw new System.InvalidOperationException("Review this project in Edit Mode.");
if(AeroCameraTurnReview.Status.StartsWith("Turning review ") && AeroCameraTurnReview.Status!="Turning review complete")
    throw new System.InvalidOperationException("Wait for the camera sweep to finish.");
var original=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(original.path!="Assets/_Project/Scenes/Tower.unity")throw new System.InvalidOperationException("Open Tower first.");
var source=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.Rendering.AeroTowerAtmosphere>();
var originalFlare=source.cycle.sun.GetComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
bool originalFlareEnabled=originalFlare.enabled;
var sky=UnityEngine.RenderSettings.skybox;
var active=UnityEngine.RenderTexture.active;
float daylight=UnityEngine.Shader.GetGlobalFloat("_AeroDaylight");
bool async=UnityEditor.ShaderUtil.allowAsyncCompilation;
var scene=UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Additive);
UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
var target=new UnityEngine.RenderTexture(1280,720,24,UnityEngine.RenderTextureFormat.ARGBHalf);
var pixels=new UnityEngine.Texture2D(1280,720,UnityEngine.TextureFormat.RGB24,false);
var material=new UnityEngine.Material(UnityEngine.Shader.Find("Universal Render Pipeline/Lit"));
string folder="Documentation/FrutigerAero/Lighting/CameraTurn";
System.IO.Directory.CreateDirectory(folder);
var observations=new System.Collections.Generic.List<object>();
try
{
    originalFlare.enabled=false;UnityEditor.ShaderUtil.allowAsyncCompilation=false;
    UnityEngine.RenderSettings.skybox=sky;UnityEngine.RenderSettings.fog=false;
    UnityEngine.RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
    UnityEngine.RenderSettings.ambientLight=new UnityEngine.Color(.15f,.2f,.25f);
    var sun=new UnityEngine.GameObject("Review sunlight",typeof(UnityEngine.Light)).GetComponent<UnityEngine.Light>();
    sun.gameObject.layer=31;
    sun.type=UnityEngine.LightType.Directional;sun.intensity=source.cycle.sun.intensity;sun.color=source.cycle.sun.color;sun.cullingMask=1<<31;
    sun.transform.rotation=source.cycle.sun.transform.rotation;UnityEngine.RenderSettings.sun=sun;
    var flare=sun.gameObject.AddComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
    flare.lensFlareData=originalFlare.lensFlareData;flare.intensity=.85f;flare.useOcclusion=true;flare.occlusionRadius=.02f;
    flare.sampleCount=32;flare.allowOffScreen=false;flare.radialScreenAttenuationCurve=originalFlare.radialScreenAttenuationCurve;
    var camera=new UnityEngine.GameObject("Atmosphere review camera",typeof(UnityEngine.Camera)).GetComponent<UnityEngine.Camera>();
    camera.enabled=false;camera.scene=scene;camera.targetTexture=target;camera.cullingMask=1<<31;camera.nearClipPlane=.05f;camera.farClipPlane=2000;camera.fieldOfView=68;camera.allowHDR=true;
    camera.clearFlags=UnityEngine.CameraClearFlags.Skybox;
    var data=UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(camera);data.renderPostProcessing=true;data.requiresDepthTexture=true;data.volumeLayerMask=1<<31;
    var volumeRoot=new UnityEngine.GameObject("Review grading",typeof(UnityEngine.Rendering.Volume));volumeRoot.layer=31;
    var volume=volumeRoot.GetComponent<UnityEngine.Rendering.Volume>();volume.isGlobal=true;
    volume.sharedProfile=UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
    void Save(string name)
    {
        UnityEngine.Shader.SetGlobalFloat("_AeroDaylight",1);
        for(int i=0;i<4;i++)
        {
            UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera,new UnityEngine.Rendering.Universal.UniversalRenderPipeline.SingleCameraRequest{destination=target});
            UnityEngine.RenderTexture.active=target;pixels.ReadPixels(new UnityEngine.Rect(0,0,1,1),0,0,false);
        }
        pixels.ReadPixels(new UnityEngine.Rect(0,0,1280,720),0,0,false);pixels.Apply();
        System.IO.File.WriteAllBytes(folder+"/"+name+".png",pixels.EncodeToPNG());
        observations.Add(new{name,flareEnabled=flare.enabled,flareOcclusion=flare.useOcclusion});
    }
    camera.transform.position=new UnityEngine.Vector3(0,300,0);
    var direction=-sun.transform.forward;camera.transform.rotation=UnityEngine.Quaternion.LookRotation(direction,UnityEngine.Vector3.up);
    flare.enabled=false;Save("Sun-Look-Before");flare.enabled=true;Save("Sun-Look-After");
    var look=camera.transform.rotation;camera.transform.rotation=look*UnityEngine.Quaternion.Euler(0,65,0);Save("Sun-Away");camera.transform.rotation=look;
    var blocker=UnityEngine.GameObject.CreatePrimitive(UnityEngine.PrimitiveType.Cube);blocker.name="Opaque sun blocker";blocker.layer=31;
    blocker.transform.position=camera.transform.position+direction*5;blocker.transform.localScale=UnityEngine.Vector3.one*3;blocker.GetComponent<UnityEngine.Renderer>().sharedMaterial=material;
    Save("Sun-Occluded");blocker.SetActive(false);
    flare.enabled=false;sun.intensity=.12f;camera.clearFlags=UnityEngine.CameraClearFlags.SolidColor;camera.backgroundColor=new UnityEngine.Color(.015f,.03f,.06f);
    camera.transform.position=new UnityEngine.Vector3(0,3,-16);camera.transform.LookAt(new UnityEngine.Vector3(0,2,0));
    var particles=UnityEngine.Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>("Assets/_Project/Prefabs/Lighting/AeroFireflies.prefab"));
    foreach(var child in particles.GetComponentsInChildren<UnityEngine.Transform>(true))child.gameObject.layer=31;
    particles.transform.position=new UnityEngine.Vector3(0,2,0);
    var ps=particles.GetComponent<UnityEngine.ParticleSystem>();ps.Stop(true,UnityEngine.ParticleSystemStopBehavior.StopEmittingAndClear);
    ps.useAutoRandomSeed=false;ps.randomSeed=2721;
    var main=ps.main;var emission=ps.emission;var shape=ps.shape;
    main.maxParticles=96;emission.rateOverTime=1.95f;shape.scale=new UnityEngine.Vector3(20,2.4f,20);ps.Simulate(14,true,true,true);Save("Particles-Before");
    int before=ps.particleCount;
    main.maxParticles=125;emission.rateOverTime=2.535f;shape.scale=new UnityEngine.Vector3(20*UnityEngine.Mathf.Sqrt(1.15f),2.4f,20*UnityEngine.Mathf.Sqrt(1.15f));
    ps.Simulate(14,true,true,true);Save("Particles-After");int after=ps.particleCount;
    var report=new {mode="Controlled Edit Mode render, deterministic 14-second particle simulation; no Play Mode or GPU profiling",particlesBefore=before,particlesAfter=after,
        emissionMultiplier=1.3f,horizontalAreaMultiplier=shape.scale.x*shape.scale.z/400,cap=main.maxParticles,flareElements=flare.lensFlareData.elements.Length,observations};
    var json=Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented);
    System.IO.File.WriteAllText(folder+"/Appearance.json",json);return json;
}
finally
{
    UnityEngine.RenderTexture.active=active;
    UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(original);
    UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(material);
    originalFlare.enabled=originalFlareEnabled;UnityEditor.ShaderUtil.allowAsyncCompilation=async;UnityEngine.Shader.SetGlobalFloat("_AeroDaylight",daylight);
}
