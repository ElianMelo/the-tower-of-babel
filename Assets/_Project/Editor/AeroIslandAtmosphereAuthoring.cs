using System;
using System.Linq;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.Resources;
using TowerOfBabel.World;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class AeroIslandAtmosphereAuthoring
{
    public const string Folder = "Assets/_Project/Environment/Lighting";
    public const string ProfilePath = Folder + "/AeroDayNight.asset";
    public const string GlassPrefabPath = "Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab";
    public const string GlassMaterialPath = Folder + "/GlassWaterFloor.mat";
    // Crest Ocean System caustic net (MIT); see Environment/Lighting/Caustics/THIRD_PARTY_NOTICE.md.
    public const string CausticPath = "Assets/_Project/Environment/Lighting/Caustics/CrestCaustics.png";
    public const string CompleteMeshPath = "Assets/_Project/Models/Buildings/FrutigerAero/GlassWaterFloorComplete.asset";

    private static T Asset<T>(string path, Func<T> create) where T : UnityEngine.Object
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = create(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(obj); so.FindProperty(field).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tower of Babel/Environment/Apply island, glass channels and atmosphere")]
    public static object Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/_Project/Scenes/Tower.unity")
            throw new InvalidOperationException("Use this project's Tower scene in Edit Mode.");
        var generator = UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
        var manager = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
        var landscape = UnityEngine.Object.FindFirstObjectByType<AeroTowerTerrain>();
        var caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(CausticPath);
        if (caustics == null) throw new InvalidOperationException("The project caustics texture is required: " + CausticPath);
        var patternImporter = (TextureImporter)AssetImporter.GetAtPath(CausticPath);
        if (patternImporter.wrapMode != TextureWrapMode.Repeat) { patternImporter.wrapMode = TextureWrapMode.Repeat; patternImporter.SaveAndReimport(); }

        // Reuse the installed vendor's animation sampling on the lake, bounded projections and lamps.
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AeroWaterLightAuthoring.RendererPath);
        var aquatic = renderer.rendererFeatures.OfType<AeroLightRendererFeature>().Single();
        aquatic.caustics = caustics; aquatic.settings.scale = .42f; aquatic.settings.speed = .045f;
        aquatic.settings.causticIntensity = 1.2f; aquatic.Create(); EditorUtility.SetDirty(aquatic);
        var water = AssetDatabase.LoadAssetAtPath<Material>(AeroWaterLightAuthoring.WaterPath);
        water.SetTexture("_CausticsTex", caustics); water.SetFloat("_CausticsBrightness", 1.7f); water.SetFloat("_CausticsTiling", .42f);
        water.SetFloat("_CausticsSpeed", .1f); water.EnableKeyword("_CAUSTICS"); EditorUtility.SetDirty(water);
        water.SetFloat("_DepthHorizontal",1.2f); water.SetFloat("_DepthVertical",4); water.SetFloat("_ColorAbsorption",.2f);
        var cookie = AssetDatabase.LoadAssetAtPath<Material>(AeroAtmosphereAuthoring.CookiePath);
        cookie.SetTexture("_CausticsTex", caustics); EditorUtility.SetDirty(cookie);
        foreach (var lamp in UnityEngine.Object.FindObjectsByType<AeroProjectionLamp>(FindObjectsSortMode.None))
        { lamp.enabled = false; lamp.cookieMaterial = cookie; lamp.enabled = true; }

        // A 4 m sealed slab with the same datum as an ordinary walking tile.
        var importer = (ModelImporter)AssetImporter.GetAtPath("Assets/_Project/Models/Buildings/FrutigerAero/GlassWaterFloor.fbx");
        importer.isReadable = true; importer.useFileScale = false; importer.bakeAxisConversion = true;
        importer.importAnimation = false; importer.materialImportMode = ModelImporterMaterialImportMode.None; importer.SaveAndReimport();
        var source = AssetDatabase.LoadAllAssetsAtPath(importer.assetPath).OfType<Mesh>().Single();
        var completed = Asset<Mesh>(CompleteMeshPath, () => new Mesh { name = "Glass water sealed floor" });
        completed.Clear(); var vertices = source.vertices; var bounds = source.bounds;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = new Vector3((vertices[i].x - bounds.min.x) / bounds.size.x * 4,
                (vertices[i].y - bounds.max.y) / bounds.size.y * .2f,
                -(vertices[i].z - bounds.min.z) / bounds.size.z * 4);
        completed.vertices = vertices; completed.uv = source.uv;
        int[] triangles = source.triangles;
        for (int i = 0; i < triangles.Length; i += 3) (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);
        completed.triangles = triangles; completed.RecalculateNormals(); completed.RecalculateTangents(); completed.RecalculateBounds(); EditorUtility.SetDirty(completed);
        var glass = Asset<Material>(GlassMaterialPath, () => new Material(Shader.Find("TowerOfBabel/Buildings/Glass water floor")));
        glass.SetTexture("_CausticsTex", caustics); glass.enableInstancing = true; EditorUtility.SetDirty(glass);
        glass.SetTexture("_Environment", aquatic.environment);
        glass.SetColor("_WaterColor",new Color(.035f,.56f,.72f));
        glass.SetVector("_RiverLayout",new Vector4(generator.radius,generator.decreaseAmount,generator.heighIncrease,1.3f));
        var root = PrefabUtility.LoadPrefabContents(DeckConstructionAuthoring.PrefabPath("Floor_Tile"));
        try
        {
            root.name = "Glass water floor";
            root.GetComponent<TowerAsset>().SetAssetType(TowerAssetType.GlassWaterFloor);
            root.GetComponent<Renderer>().sharedMaterial = glass;
            var data = new SerializedObject(root.GetComponent<Building>());
            data.FindProperty("resourceType").intValue = (int)ResourceType.RiverBlock; // see EssenceWorkshopAuthoring.BuildingCosts
            data.FindProperty("stageMeshes").GetArrayElementAtIndex(10).objectReferenceValue = completed;
            data.ApplyModifiedPropertiesWithoutUndo(); root.GetComponent<MeshCollider>().sharedMesh = completed;
            var lightRoot = new GameObject("Completed water light footprint"); lightRoot.transform.SetParent(root.transform, false);
            lightRoot.transform.localPosition = new Vector3(2, 0, -2);
            var region = lightRoot.AddComponent<AeroWaterLightRegion>(); region.radii = new Vector2(1.8f, 1.7f);
            region.height = 2.6f; region.depth = .3f; region.shoreFade = 1.2f; region.enabled = false;
            root.AddComponent<GlassWaterFloorLight>().region = region;
            PrefabUtility.SaveAsPrefabAsset(root, GlassPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        generator.glassWaterFloorObject = AssetDatabase.LoadAssetAtPath<GameObject>(GlassPrefabPath);
        manager.ConfigureAssetPrefabs(generator);
        int channels = 0;
        var floors = manager.CachedChunks.SelectMany(c => c.Assets).Where(a => a.AssetType.IsFloor()).Select(a =>
        {
            int floor = Mathf.FloorToInt((a.Position.y + .001f) / generator.heighIncrease);
            float depth = a.Scale.x * 4;
            // Keep structural support bridges and stair clearances as white stone.
            bool regular = Mathf.Abs(depth - generator.tileDecreaseRate) < .02f || Vector2.Distance(new Vector2(a.Position.x,a.Position.z),generator.center) < generator.tileDecreaseRate;
            bool channel = regular && GlassWaterFloorLayout.IsChannel(GlassWaterFloorLayout.TileCenter(a.Position,a.Rotation,a.Scale,generator.center),generator.GetWallRadius(floor),generator.tileDecreaseRate);
            if (channel) channels++;
            return new ChunkAssetData(a.LocalIndex, channel ? TowerAssetType.GlassWaterFloor : TowerAssetType.Floor, a.Position, a.Rotation, a.Scale, a.Stage);
        }).ToArray();
        manager.ReplaceCachedFloors(floors); EditorUtility.SetDirty(generator);

        // A real surrounding sea: the old mainland perimeter is now submerged.
        var terrain = landscape.GeneratedTerrainData;
        var sand = Asset<TerrainLayer>(AeroTowerTerrainAuthoring.Root + "/IslandSand.terrainlayer", () => new TerrainLayer());
        sand.diffuseTexture = terrain.terrainLayers[0].diffuseTexture; sand.tileSize = new Vector2(24,24);
        sand.diffuseRemapMin = new Vector4(.34f,.29f,.15f,0); sand.diffuseRemapMax = new Vector4(.88f,.8f,.57f,1);
        sand.smoothness = .08f; EditorUtility.SetDirty(sand);
        terrain.terrainLayers = terrain.terrainLayers.Take(3).Concat(new[] {sand}).ToArray();
        landscape.Generate(); EditorUtility.SetDirty(terrain); EditorUtility.SetDirty(landscape);
        for (int i=1;i<=4;i++)
        {
            var marker=GameObject.Find("Marker"+i);
            if(marker!=null) foreach(var guide in marker.GetComponentsInChildren<Renderer>()) {guide.enabled=false;EditorUtility.SetDirty(guide);}
        }

        var profile = Asset<AeroDayNightProfile>(ProfilePath, ScriptableObject.CreateInstance<AeroDayNightProfile>);
        var atmosphereRoot = GameObject.Find("Aero island atmosphere") ?? new GameObject("Aero island atmosphere");
        var cycle = atmosphereRoot.GetComponent<AeroDayNightCycle>() ?? atmosphereRoot.AddComponent<AeroDayNightCycle>();
        cycle.profile = profile; if (cycle.sun == null) cycle.sun = RenderSettings.sun; cycle.timeOfDay = profile.startTime;
        var moonRoot = atmosphereRoot.transform.Find("Moonlight");
        var moon = moonRoot != null ? moonRoot.GetComponent<Light>() : new GameObject("Moonlight", typeof(Light)).GetComponent<Light>();
        moon.transform.SetParent(atmosphereRoot.transform, false); moon.type = LightType.Directional; moon.shadows = LightShadows.Soft;
        cycle.moon = moon;
        var fx = atmosphereRoot.GetComponent<AeroTowerAtmosphere>() ?? atmosphereRoot.AddComponent<AeroTowerAtmosphere>();
        fx.cycle = cycle; fx.center = generator.center; fx.baseRadius = generator.radius; fx.decrease = generator.decreaseAmount; fx.floorHeight = generator.heighIncrease; fx.levels = generator.levels;
        fx.basePillarAngle = generator.pillarAngle;
        if(fx.beams!=null) UnityEngine.Object.DestroyImmediate(fx.beams.gameObject);
        fx.beams=null;
        fx.fireflies = CreateParticles(atmosphereRoot.transform, "Aqua meadow fireflies"); ConfigureFireflies(fx.fireflies);
        PrefabUtility.SaveAsPrefabAsset(fx.fireflies.gameObject,"Assets/_Project/Prefabs/Lighting/AeroFireflies.prefab");
        var volumeRoot = atmosphereRoot.transform.Find("Tower exit atmosphere");
        var volume = volumeRoot != null ? volumeRoot.GetComponent<Volume>() : new GameObject("Tower exit atmosphere",typeof(Volume)).GetComponent<Volume>();
        volume.transform.SetParent(atmosphereRoot.transform, false); volume.isGlobal = false; volume.priority = 5; volume.blendDistance = 3;
        var sphere = volume.GetComponent<SphereCollider>(); if (sphere == null) sphere = volume.gameObject.AddComponent<SphereCollider>(); sphere.isTrigger = true; sphere.radius = generator.radius;
        var vp = Asset<VolumeProfile>(Folder + "/TowerExitAtmosphere.asset", ScriptableObject.CreateInstance<VolumeProfile>);
        if (!vp.TryGet<Bloom>(out var bloom)) { bloom = vp.Add<Bloom>(); AssetDatabase.AddObjectToAsset(bloom,vp); }
        bloom.intensity.Override(.65f); bloom.threshold.Override(1.15f); bloom.scatter.Override(.62f);
        if (!vp.TryGet<ColorAdjustments>(out var color)) { color = vp.Add<ColorAdjustments>(); AssetDatabase.AddObjectToAsset(color,vp); }
        color.postExposure.Override(.13f); color.contrast.Override(4); color.colorFilter.Override(new Color(.92f,.98f,1));
        if (!vp.TryGet<AeroShaftVolume>(out var shaftVolume)) { shaftVolume = vp.Add<AeroShaftVolume>(); AssetDatabase.AddObjectToAsset(shaftVolume,vp); }
        shaftVolume.intensity.Override(1.6f); shaftVolume.density.Override(1.5f); EditorUtility.SetDirty(shaftVolume);
        volume.sharedProfile = vp; fx.exitVolume = volume;
        fx.bounceLights = new Light[2];
        for (int i = 0; i < 2; i++)
        {
            var found = atmosphereRoot.transform.Find("Side bounce " + i);
            var light = found != null ? found.GetComponent<Light>() : new GameObject("Side bounce " + i,typeof(Light)).GetComponent<Light>();
            light.transform.SetParent(atmosphereRoot.transform,false); light.type = LightType.Spot; light.range = 18; light.spotAngle = 85; light.shadows = LightShadows.None;
            fx.bounceLights[i] = light;
        }
        ConfigureFlares(cycle.sun);
        cycle.ApplyTime(profile.startTime); fx.Sample(Camera.main.transform.position,0);
        foreach (var obj in new UnityEngine.Object[] {profile,cycle,moon,fx,volume,vp,bloom,color}) EditorUtility.SetDirty(obj);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        TowerTileRepairAuthoring.Apply();
        TowerGodRayAuthoring.Configure();
        return new { channels, totalAssets=manager.CachedChunks.Sum(c=>c.Assets.Count), meshBounds=completed.bounds.ToString(), rays="Dynamic shadow scattering", profile=ProfilePath };
    }

    private static ParticleSystem CreateParticles(Transform parent, string name)
    {
        var found = parent.Find(name);
        var ps = found != null ? found.GetComponent<ParticleSystem>() : new GameObject(name,typeof(ParticleSystem)).GetComponent<ParticleSystem>();
        ps.transform.SetParent(parent,false); ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.simulationSpace = ParticleSystemSimulationSpace.World; main.playOnAwake = true; main.loop = true; main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        return ps;
    }
    private static void ConfigureBeams(ParticleSystem ps)
    {
        var main=ps.main; main.maxParticles=AeroTowerAtmosphere.BeamCount; main.startLifetime=1000; main.startSpeed=0; main.startSize3D=true; main.startRotation3D=true;
        var emission=ps.emission; emission.enabled=false; var shape=ps.shape; shape.enabled=false;
        var mesh=Asset<Mesh>(Folder+"/SoftBeamCross.asset",()=>new Mesh {name="Crossed soft beam sheets"});
        mesh.Clear(); mesh.vertices=new[]{new Vector3(-.5f,0,-.5f),new Vector3(.5f,0,-.5f),new Vector3(-.5f,0,.5f),new Vector3(.5f,0,.5f),new Vector3(0,-.5f,-.5f),new Vector3(0,.5f,-.5f),new Vector3(0,-.5f,.5f),new Vector3(0,.5f,.5f)};
        mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up,Vector2.one,Vector2.zero,Vector2.right,Vector2.up,Vector2.one}; mesh.triangles=new[]{0,2,1,1,2,3,4,6,5,5,6,7}; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        var mat=Asset<Material>(Folder+"/SoftSunBeam.mat",()=>new Material(Shader.Find("TowerOfBabel/Atmosphere/Soft light VFX")));
        mat.SetFloat("_Beam",1); mat.SetColor("_Color",new Color(2,1.9f,1.7f,1)); EditorUtility.SetDirty(mat);
        var r=ps.GetComponent<ParticleSystemRenderer>(); r.renderMode=ParticleSystemRenderMode.Mesh; r.mesh=mesh; r.sharedMaterial=mat; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
    }
    private static void ConfigureFireflies(ParticleSystem ps)
    {
        var main=ps.main; main.maxParticles=125; main.startLifetime=new ParticleSystem.MinMaxCurve(8,16); main.startSpeed=new ParticleSystem.MinMaxCurve(.01f,.06f); main.startSize3D=false; main.startSize=new ParticleSystem.MinMaxCurve(.018f,.055f);
        main.startColor=new ParticleSystem.MinMaxGradient(new Color(.25f,.85f,1),new Color(.5f,1,.45f));
        var emission=ps.emission; emission.enabled=true; emission.rateOverTime=2.535f;
        var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Box; shape.scale=new Vector3(20*Mathf.Sqrt(1.15f),2.4f,20*Mathf.Sqrt(1.15f));
        var noise=ps.noise; noise.enabled=true; noise.strength=.28f; noise.frequency=.18f; noise.scrollSpeed=.12f; noise.octaveCount=1; noise.damping=true; noise.quality=ParticleSystemNoiseQuality.Medium;
        var velocity=ps.velocityOverLifetime; velocity.enabled=true; velocity.space=ParticleSystemSimulationSpace.World;
        velocity.x=new ParticleSystem.MinMaxCurve(-.05f,.05f); velocity.y=new ParticleSystem.MinMaxCurve(.015f,.07f); velocity.z=new ParticleSystem.MinMaxCurve(-.05f,.05f);
        var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(new Color(.7f,1,1),.6f),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(.8f,.18f),new GradientAlphaKey(.25f,.47f),new GradientAlphaKey(.85f,.72f),new GradientAlphaKey(0,1)});
        var color=ps.colorOverLifetime; color.enabled=true; color.color=gradient;
        var size=ps.sizeOverLifetime; size.enabled=true; size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.EaseInOut(0,.65f,1,1.1f));
        var mat=Asset<Material>(Folder+"/AquaFireflies.mat",()=>new Material(Shader.Find("TowerOfBabel/Atmosphere/Soft light VFX")));
        mat.SetFloat("_Beam",0); mat.SetColor("_Color",new Color(5,5,5,1)); EditorUtility.SetDirty(mat);
        var r=ps.GetComponent<ParticleSystemRenderer>(); r.renderMode=ParticleSystemRenderMode.Billboard; r.sharedMaterial=mat; r.shadowCastingMode=ShadowCastingMode.Off; r.receiveShadows=false;
    }
    private static void ConfigureFlares(Light sun)
    {
        var halo=Asset<Texture2D>(Folder+"/AeroSunFlareSoftHalo.asset",()=>
        {
            const int size=128;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,true,true) {name="Soft sun flare halo",wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Trilinear};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float px=(x+.5f)/size*2-1,py=(y+.5f)/size*2-1,r2=px*px+py*py;
                float v=Mathf.Exp(-r2*5)*Mathf.SmoothStep(0,1,Mathf.Clamp01((1-r2)*5));
                pixels[y*size+x]=new Color(v,v,v,1);
            }
            texture.SetPixels(pixels);texture.Apply(true,false);return texture;
        });
        var data=Asset<LensFlareDataSRP>(Folder+"/AeroSunFlare.asset",ScriptableObject.CreateInstance<LensFlareDataSRP>);
        data.elements=new[]{new LensFlareDataElementSRP {flareType=SRPLensFlareType.Circle,visible=true,localIntensity=4,uniformScale=1,sizeXY=new Vector2(.1f,.1f),tint=new Color(1,.98f,.9f),position=0,fallOff=.65f},
            new LensFlareDataElementSRP {flareType=SRPLensFlareType.Circle,visible=true,localIntensity=.32f,uniformScale=1,sizeXY=new Vector2(.9f,.9f),tint=new Color(1,.86f,.63f),position=0,fallOff=.5f},
            new LensFlareDataElementSRP {flareType=SRPLensFlareType.Circle,visible=true,localIntensity=.25f,uniformScale=1,sizeXY=new Vector2(2.4f,.025f),tint=new Color(1,.94f,.8f),position=0,fallOff=.6f},
            new LensFlareDataElementSRP {flareType=SRPLensFlareType.Circle,visible=true,localIntensity=.12f,uniformScale=1,sizeXY=new Vector2(.07f,.07f),tint=new Color(.5f,.82f,1),position=.55f,fallOff=.9f}};
        foreach(var element in data.elements){element.flareType=SRPLensFlareType.Image;element.lensFlareTexture=halo;}
        EditorUtility.SetDirty(data);
        var flare=sun.GetComponent<LensFlareComponentSRP>() ?? sun.gameObject.AddComponent<LensFlareComponentSRP>();
        flare.lensFlareData=data; flare.intensity=.85f; flare.useOcclusion=true; flare.sampleCount=32; flare.occlusionRadius=.02f; flare.allowOffScreen=false;
        flare.radialScreenAttenuationCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.25f,.9f),new Keyframe(.7f,.12f),new Keyframe(1,0)); EditorUtility.SetDirty(flare);
        var rp=(UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline; var so=new SerializedObject(rp);
        so.FindProperty("m_SupportDataDrivenLensFlare").boolValue=true; so.FindProperty("m_SupportScreenSpaceLensFlare").boolValue=true; so.ApplyModifiedPropertiesWithoutUndo();
        var global=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset");
        if(!global.TryGet<ScreenSpaceLensFlare>(out var screen)) {screen=global.Add<ScreenSpaceLensFlare>(); AssetDatabase.AddObjectToAsset(screen,global);}
        screen.intensity.Override(.035f); screen.streaksIntensity.Override(.03f); screen.streaksLength.Override(.16f);
        EditorUtility.SetDirty(screen); EditorUtility.SetDirty(global);
    }
}
