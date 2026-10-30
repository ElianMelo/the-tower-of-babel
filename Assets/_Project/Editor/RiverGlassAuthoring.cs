#if UNITY_EDITOR
using System;
using System.Linq;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class RiverGlassAuthoring
{
    public const string ProfilePath = "Assets/_Project/Environment/Lighting/RiverGlass.asset";
    public const string GlassPath = "Assets/_Project/Environment/Lighting/GlassWaterFloor.mat";
    /// <summary>Presentation 13 (user request 2026-10-29): Crest caustic net everywhere and more frequent white projected light.</summary>
    public static void MigrateToCrestCaustics(RiverGlassProfile profile)
    {
        var crest=AssetDatabase.LoadAssetAtPath<Texture2D>(AeroIslandAtmosphereAuthoring.CausticPath);
        profile.caustics=crest; profile.surfaceCaustics=crest;
        profile.projectionBurstDuration=new Vector2(7,11); profile.projectionQuietDuration=new Vector2(.6f,1.4f);
        profile.projectionStartSpacing=new Vector2(.5f,1.2f);
        profile.projectionGlobalQuietInterval=new Vector2(30,45); profile.projectionGlobalQuietDuration=new Vector2(1,1.6f);
        profile.presentationVersion=13;
    }

    /// <summary>Presentation 12: water inside glass. Never touches projection/caustic-cookie fields.</summary>
    public static void MigrateToVolume(RiverGlassProfile profile)
    {
        profile.absorption=new Color(4.5f,1.1f,.42f);
        profile.shallowColor=new Color(.25f,.83f,.94f); profile.deepColor=new Color(.04f,.5f,.82f); profile.deepPath=.6f;
        profile.wallColor=new Color(.93f,.96f,1); profile.edgeTint=new Color(.35f,.82f,.74f);
        profile.waterIor=1.33f; profile.glassIor=1.5f; profile.refractionStrength=.4f;
        profile.bedTileSize=.6f; profile.bedEtchOpacity=.05f; profile.bedCausticStrength=1.6f;
        profile.meniscusIntensity=1.2f; profile.meniscusWidth=.008f; profile.wallTint=.35f; profile.inscatter=1.6f;
        profile.bubbleDensity=2.2f; profile.volumeSteps=12; profile.liteSteps=6;
        profile.presentationVersion=12;
    }
    public static RiverGlassProfile ConfigureAssets()
    {
        if (Application.dataPath.Replace('\\', '/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Author this project's river in Edit Mode.");
        var profile = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<RiverGlassProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }
        var glass = AssetDatabase.LoadAssetAtPath<Material>(GlassPath);
        profile.caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(AeroIslandAtmosphereAuthoring.CausticPath);
        profile.ripples = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Stylized Water 3/Materials/Textures/Normals/SmoothWaves.png");
        profile.environment = glass.GetTexture("_Environment") as Cubemap;
        profile.layout = glass.GetVector("_RiverLayout");
        if(profile.presentationVersion<2)
        {
            profile.opticalThickness=.14f; profile.waterTop=.04f; profile.waterBottom=.18f; profile.glassThickness=.02f;
            profile.rippleStrength=.28f; profile.flowSpeed=.065f; profile.refraction=.008f; profile.reflections=.5f;
            profile.waterTint=new Color(.08f,.5f,.65f); profile.glassTint=new Color(.55f,.86f,.94f);
            profile.projectionTint=new Color(.88f,.96f,1); profile.daylightIntensity=48; profile.nightIntensity=36;
            profile.projectionRange=8; profile.selectionDistance=24; profile.projectionAngle=120;
            profile.presentationVersion=2;
        }
        if(profile.presentationVersion<3)
        {
            profile.projectionRange=14;profile.projectionAngle=150;
            profile.daylightIntensity=64;profile.nightIntensity=48;
            profile.presentationVersion=3;
        }
        if(profile.presentationVersion<4)
        {
            profile.surfaceCaustics=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Stylized Water 3/Materials/Textures/Caustics/Caustics_2.png");
            profile.surfacePatternOpacity=.22f;profile.surfacePatternBlur=3;profile.surfacePatternScale=.16f;
            profile.presentationVersion=4;
        }
        if(profile.presentationVersion<5)
        {
            profile.projectionPatchCount=4;profile.projectionAheadDistance=28;profile.projectionPatternDensity=2.5f;
            profile.projectionAngle=110;profile.selectionDistance=42;profile.patchSeparation=8;
            profile.presentationVersion=5;
        }
        if(profile.presentationVersion<6)
        {
            profile.projectionPatternDensity=5;
            profile.daylightIntensity=20;profile.nightIntensity=12;
            profile.presentationVersion=6;
        }
        if(profile.presentationVersion<7)
        {
            profile.projectionTint=new Color(1,1,.99f);
            profile.daylightIntensity=144;profile.nightIntensity=96;
            profile.projectionBurstDuration=new Vector2(1.4f,3.2f);profile.projectionQuietDuration=new Vector2(2.5f,6);
            profile.projectionBurstFade=.55f;profile.presentationVersion=7;
        }
        if(profile.presentationVersion<8)
        {
            profile.projectionAngle=60;profile.daylightIntensity=32;profile.nightIntensity=20;
            profile.projectionBurstDuration=new Vector2(7,11);profile.projectionQuietDuration=new Vector2(3,7);
            profile.projectionBurstFade=2.8f;profile.projectionRelocationFade=1.6f;
            profile.presentationVersion=8;
        }
        if(profile.presentationVersion<9)
        {
            profile.daylightIntensity=96;profile.nightIntensity=64;
            profile.projectionCutoutSpeed=.06f;profile.projectionCutoutWarp=.035f;profile.presentationVersion=9;
        }
        if(profile.presentationVersion<10)
        {
            profile.projectionAngle=90;profile.projectionPatternDensity=2.5f;
            profile.projectionHighlightOpacity=.95f;profile.projectionDetailOpacity=.06f;profile.presentationVersion=10;
        }
        if(profile.presentationVersion<11)
        {
            profile.projectionBurstDuration=new Vector2(6,9);profile.projectionQuietDuration=new Vector2(2,4);
            profile.projectionMaxActivePatches=2;profile.projectionStartSpacing=new Vector2(1.2f,2.5f);
            profile.projectionGlobalQuietInterval=new Vector2(14,20);profile.projectionGlobalQuietDuration=new Vector2(1,2);
            profile.projectionGlobalFade=1.5f;profile.projectionFlowVariation=.15f;profile.projectionFlowSway=.035f;
            profile.projectionDetailLifetime=new Vector2(6,10);profile.projectionHighlightLifetime=new Vector2(2,4);
            profile.projectionHighlightFade=.7f;profile.presentationVersion=11;
        }
        if(profile.presentationVersion<12)
        {
            // Water inside glass: real path absorption and colour. Projection fields stay as accepted.
            MigrateToVolume(profile);
        }
        if(profile.presentationVersion<13)
        {
            MigrateToCrestCaustics(profile);
        }
        glass.SetFloat("_RiverStage", 10); glass.SetFloat("_RiverFloorIndex", -1); glass.enableInstancing = true;
        EditorUtility.SetDirty(glass); EditorUtility.SetDirty(profile);
        foreach (var name in new[] { "PC_Renderer", "Mobile_Renderer" })
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>($"Assets/Settings/{name}.asset");
            var feature = renderer.rendererFeatures.OfType<RiverGlassRendererFeature>().FirstOrDefault();
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RiverGlassRendererFeature>();
                feature.name = "Transparent river glass pools";
                AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Add(feature);
            }
            feature.profile = profile; feature.surfaceShader = Shader.Find("TowerOfBabel/Buildings/Transparent river glass");
            feature.reducedRefraction = name == "Mobile_Renderer"; feature.Create();
            var serialized = new SerializedObject(renderer);
            var map = serialized.FindProperty("m_RendererFeatureMap");
            map.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string guid, out long localId);
                map.GetArrayElementAtIndex(i).longValue = localId;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty(); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
        return profile;
    }

    public static void ConfigureScene()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path != "Assets/_Project/Scenes/Tower.unity") return;
        var manager = UnityEngine.Object.FindObjectsByType<ChunkManager>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene == scene);
        if (manager == null) return;
        var light = manager.GetComponent<RiverGlassLighting>() ?? manager.gameObject.AddComponent<RiverGlassLighting>();
        light.profile = AssetDatabase.LoadAssetAtPath<RiverGlassProfile>(ProfilePath);
        light.cookieShader = Shader.Find("Hidden/TowerOfBabel/River caustic cookie");
        light.enabled = false; light.enabled = true;
        EditorUtility.SetDirty(light); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
    }

    [MenuItem("Tower of Babel/Environment/Apply transparent river glass pools")]
    public static string Apply()
    {
        ConfigureAssets(); ConfigureScene();
        return "Transparent river, shared profile, PC/mobile composition and pooled shadowed water lights configured.";
    }
}
#endif
