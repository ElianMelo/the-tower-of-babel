using System;
using System.Linq;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AeroAtmosphereAuthoring
{
    public const string Folder="Assets/_Project/Environment/Lighting";
    public const string PrefabPath="Assets/_Project/Prefabs/Lighting/AquaProjectionLamp.prefab";
    public const string CookiePath=Folder+"/AquaProjectionCookie.mat";

    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path)) return;
        string parent=System.IO.Path.GetDirectoryName(path).Replace('\\','/'); EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent,System.IO.Path.GetFileName(path));
    }
    private static Material Material(string name,Color color,float metal,float gloss,Color emission)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null) {m=new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color); m.SetFloat("_Metallic",metal); m.SetFloat("_Smoothness",gloss);
        m.SetColor("_EmissionColor",emission);
        if(emission.maxColorComponent>0)
        {
            m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            MaterialEditor.FixupEmissiveFlag(m); m.EnableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(m); return m;
    }

    [MenuItem("Tower of Babel/Environment/Apply sun rays and projection lamps")]
    public static object Apply()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode ||
            scene.path!="Assets/_Project/Scenes/Tower.unity" || scene.isDirty) throw new InvalidOperationException("Open this project's saved Tower scene in Edit Mode.");
        EnsureFolder(Folder); EnsureFolder("Assets/_Project/Prefabs/Lighting");
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroWaterLightAuthoring.RendererPath);
        // Its depth reconstruction produced dark coplanar tile lines visible through translucent UI.
        // Retain the kit's baked occlusion and native shadows without another full-screen AO overlay.
        var screenAO=renderer.rendererFeatures.FirstOrDefault(f=>f.GetType().Name=="ScreenSpaceAmbientOcclusion");
        if(screenAO!=null) {screenAO.SetActive(false); EditorUtility.SetDirty(screenAO);}
        var shafts=renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().FirstOrDefault();
        if(shafts==null)
        {
            shafts=ScriptableObject.CreateInstance<AeroSunShaftRendererFeature>(); shafts.name="Aero shadow-shaped sun rays";
            AssetDatabase.AddObjectToAsset(shafts,renderer); renderer.rendererFeatures.Add(shafts);
        }
        shafts.shaftShader=Shader.Find("Hidden/TowerOfBabel/Sun Shafts"); shafts.settings=new AeroSunShaftRendererFeature.ShaftSettings(); shafts.SetActive(true); shafts.Create();
        var so=new SerializedObject(renderer); var map=so.FindProperty("m_RendererFeatureMap"); map.arraySize=renderer.rendererFeatures.Count;
        for(int i=0;i<map.arraySize;i++) {AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i],out string guid,out long id); map.GetArrayElementAtIndex(i).longValue=id;}
        so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(shafts); EditorUtility.SetDirty(renderer); renderer.SetDirty();

        var profile=UnityEngine.Object.FindFirstObjectByType<Volume>().sharedProfile;
        if(!profile.TryGet<Bloom>(out var bloom)) {bloom=profile.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,profile);}
        bloom.active=true; bloom.threshold.Override(1.05f); bloom.intensity.Override(.42f); bloom.scatter.Override(.65f);
        bloom.clamp.Override(12); bloom.maxIterations.Override(4); bloom.highQualityFiltering.Override(false);
        bloom.downscale.Override(BloomDownscaleMode.Half);
        EditorUtility.SetDirty(bloom); EditorUtility.SetDirty(profile);
        var camera=Camera.main.GetComponent<UniversalAdditionalCameraData>(); camera.renderPostProcessing=true; EditorUtility.SetDirty(camera);
        // Low sunlight enters the open tower sides beneath the covered floors.
        var sun=RenderSettings.sun;
        if(sun!=null)
        {
            sun.transform.rotation=Quaternion.Euler(10,330,0); sun.intensity=2.4f;
            sun.color=new Color(1,.97f,.9f); sun.shadows=LightShadows.Soft;
            EditorUtility.SetDirty(sun); EditorUtility.SetDirty(sun.transform);
        }
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.55f,.65f,.76f);
        RenderSettings.ambientEquatorColor=new Color(.33f,.38f,.43f);
        RenderSettings.ambientGroundColor=new Color(.22f,.25f,.27f);
        var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        foreach(var prefab in new[]{generator.floorTileObject,generator.pillarObject,generator.archObject,generator.stairStepObject})
        {
            var m=prefab.GetComponent<Renderer>().sharedMaterial;
            if(m.HasProperty("_GlazeGlow")) {m.SetFloat("_GlazeGlow",1.8f); EditorUtility.SetDirty(m);}
        }

        var pearl=Material("AquaLampPearl",new Color(.9f,.95f,.99f),.15f,.85f,Color.black);
        var blue=Material("AquaLampBlue",new Color(.035f,.23f,.65f),.35f,.9f,Color.black);
        var core=Material("AquaLampGlow",new Color(.035f,.35f,.55f),.05f,.94f,new Color(.1f,3.5f,5.5f));
        core.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(AeroWaterLightAuthoring.PatternPath));
        core.SetTextureScale("_BaseMap",new Vector2(.25f,.25f));
        var cookie=AssetDatabase.LoadAssetAtPath<Material>(CookiePath);
        if(cookie==null) {cookie=new Material(Shader.Find("Hidden/TowerOfBabel/Water Projection Cookie")); AssetDatabase.CreateAsset(cookie,CookiePath);}
        cookie.SetTexture("_Caustics",AssetDatabase.LoadAssetAtPath<Texture2D>(AeroWaterLightAuthoring.PatternPath)); EditorUtility.SetDirty(cookie);
        var preview=EditorSceneManager.NewPreviewScene();
        GameObject root=null;
        try
        {
            root=new GameObject("AquaProjectionLamp"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
            Renderer Piece(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
            {
                var go=GameObject.CreatePrimitive(shape); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,preview);
                go.name=name; go.transform.SetParent(root.transform,false); go.transform.localPosition=position; go.transform.localScale=scale;
                UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
                var r=go.GetComponent<Renderer>(); r.sharedMaterial=material; return r;
            }
            Piece("Pearl plinth",PrimitiveType.Cube,new Vector3(0,.055f,0),new Vector3(.46f,.11f,.46f),pearl);
            Piece("Azure lower rim",PrimitiveType.Cube,new Vector3(0,.12f,0),new Vector3(.42f,.045f,.42f),blue);
            var glow=Piece("Frosted aquatic core",PrimitiveType.Cube,new Vector3(0,.35f,0),new Vector3(.34f,.42f,.34f),core);
            Piece("Pearl canopy",PrimitiveType.Cube,new Vector3(0,.58f,0),new Vector3(.42f,.07f,.42f),pearl);
            Piece("Aqua lens",PrimitiveType.Sphere,new Vector3(0,.36f,.195f),new Vector3(.13f,.13f,.055f),core);
            for(int x=-1;x<=1;x+=2) for(int z=-1;z<=1;z+=2)
                Piece("Rounded corner post",PrimitiveType.Cylinder,new Vector3(x*.18f,.35f,z*.18f),new Vector3(.025f,.22f,.025f),pearl);
            var lightObject=new GameObject("Water projection spotlight"); lightObject.transform.SetParent(root.transform,false);
            lightObject.transform.localPosition=new Vector3(0,.38f,.24f);
            lightObject.transform.localRotation=Quaternion.LookRotation(new Vector3(0,-.2f,1));
            var light=lightObject.AddComponent<Light>(); light.type=LightType.Spot; light.color=new Color(.19f,.78f,1);
            light.intensity=32; light.range=10; light.spotAngle=95; light.innerSpotAngle=65;
            light.shadowBias=.025f; light.shadowNormalBias=.12f; light.shadowNearPlane=.05f;
            var lightData=light.gameObject.AddComponent<UniversalAdditionalLightData>();
            var lightSettings=new SerializedObject(lightData);
            lightSettings.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue=0;
            lightSettings.ApplyModifiedPropertiesWithoutUndo();
            var lamp=root.AddComponent<AeroProjectionLamp>(); lamp.projection=light; lamp.cookieMaterial=cookie; lamp.glowingCore=glow;
            lamp.glow=new Color(.12f,5,8);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally {EditorSceneManager.ClosePreviewScene(preview);}
        var prefabAsset=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var group=GameObject.Find("Aquatic workshop lamps"); if(group==null) group=new GameObject("Aquatic workshop lamps");
        if(group.transform.childCount==0)
        {
            var positions=new[]{new Vector3(-12,.03f,-103),new Vector3(4,.03f,-102)};
            for(int i=0;i<positions.Length;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(prefabAsset,scene); go.transform.SetParent(group.transform,false); go.transform.position=positions[i];
                var target=new Vector3(-4,.03f,-98); go.transform.rotation=Quaternion.LookRotation(target-positions[i]);
                go.GetComponent<AeroProjectionLamp>().phase=i*2.2f;
            }
        }
        var terrain=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.AeroTowerTerrain>().GetComponent<Terrain>();
        foreach(Transform child in group.transform)
        {
            var position=child.position;
            position.y=terrain.SampleHeight(position)+terrain.transform.position.y+.01f;
            child.position=position;
        }
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AeroProjectionLamp.RefreshBudget(Camera.main.transform.position);
        return new {scene=scene.path,halfResolution=true,samples=shafts.settings.samples,lamps=group.transform.childCount,bloom=bloom.intensity.value};
    }
}

[InitializeOnLoad]
public static class AeroLampSaveHooks
{
    static AeroLampSaveHooks()
    {
        EditorSceneManager.sceneSaving+=(scene,path)=> {foreach(var root in scene.GetRootGameObjects()) foreach(var lamp in root.GetComponentsInChildren<AeroProjectionLamp>(true)) if(lamp.projection!=null) lamp.projection.cookie=null;};
        EditorSceneManager.sceneSaved+=scene=> {foreach(var root in scene.GetRootGameObjects()) foreach(var lamp in root.GetComponentsInChildren<AeroProjectionLamp>(true)) if(lamp.projection!=null && lamp.Cookie!=null) lamp.projection.cookie=lamp.Cookie;};
    }
}
