if(UnityEngine.Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
    throw new System.InvalidOperationException("Configure this project in Edit Mode.");
var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if(scene.path!="Assets/_Project/Scenes/Tower.unity")throw new System.InvalidOperationException("Open Tower first.");
System.IO.File.Copy(scene.path,"Temp/FinishOriginalTower.unity",true);
var atmosphere=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.Rendering.AeroTowerAtmosphere>();
var profile=atmosphere.cycle.profile;
profile.fireflyPopulationMultiplier=1.3f;profile.fireflyCoverageAreaMultiplier=1.15f;profile.sunFlareIntensity=.85f;
UnityEditor.EditorUtility.SetDirty(profile);
void Configure(UnityEngine.ParticleSystem ps,bool prefab)
{
    var main=ps.main;main.maxParticles=125;
    var shape=ps.shape;shape.scale=new UnityEngine.Vector3(20*UnityEngine.Mathf.Sqrt(1.15f),2.4f,20*UnityEngine.Mathf.Sqrt(1.15f));
    var emission=ps.emission;emission.rateOverTime=prefab?2.535f:UnityEngine.Mathf.Lerp(3,7,1-atmosphere.cycle.Daylight)*profile.fireflyDensity*1.3f;
    UnityEditor.EditorUtility.SetDirty(ps);
}
string path="Assets/_Project/Prefabs/Lighting/AeroFireflies.prefab";
var contents=UnityEditor.PrefabUtility.LoadPrefabContents(path);
try{Configure(contents.GetComponent<UnityEngine.ParticleSystem>(),true);UnityEditor.PrefabUtility.SaveAsPrefabAsset(contents,path);}
finally{UnityEditor.PrefabUtility.UnloadPrefabContents(contents);}
Configure(atmosphere.fireflies,false);
typeof(AeroIslandAtmosphereAuthoring).GetMethod("ConfigureFlares",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)
    .Invoke(null,new object[]{atmosphere.cycle.sun});
UnityEditor.AssetDatabase.SaveAssets();
UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
var flare=atmosphere.cycle.sun.GetComponent<UnityEngine.Rendering.LensFlareComponentSRP>();
return new {population=profile.fireflyPopulationMultiplier,area=profile.fireflyCoverageAreaMultiplier,maxParticles=atmosphere.fireflies.main.maxParticles,
    horizontalSize=atmosphere.fireflies.shape.scale.x,flareElements=flare.lensFlareData.elements.Length,flare.intensity,
    particlePrefabInstance=UnityEditor.PrefabUtility.IsPartOfPrefabInstance(atmosphere.fireflies.gameObject)};
