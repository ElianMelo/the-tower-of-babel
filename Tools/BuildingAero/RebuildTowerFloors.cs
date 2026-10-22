// Rebuild the scene's floor cache directly; do not recreate other building types.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying||
UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!="Assets/_Project/Scenes/Tower.unity")throw new Exception("Use this project's Tower in Edit Mode");
var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>(FindObjectsInactive.Include);
var manager=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.ChunkManager>(FindObjectsInactive.Include);
var previous=manager.CachedChunks.SelectMany(c=>c.Assets).ToArray();
var stairs=previous.Where(a=>a.AssetType==TowerOfBabel.World.Tower.TowerAssetType.Stair).ToArray();
System.IO.Directory.CreateDirectory("Temp/BuildingAero/RadialFloor");
if(!System.IO.File.Exists("Temp/BuildingAero/RadialFloor/Tower-BeforeFloors.unity"))
    System.IO.File.Copy("Assets/_Project/Scenes/Tower.unity","Temp/BuildingAero/RadialFloor/Tower-BeforeFloors.unity");
var before=previous.Where(a=>a.AssetType==TowerOfBabel.World.Tower.TowerAssetType.Floor).Select(a=>new{position=new[]{a.Position.x,a.Position.y,a.Position.z},rotation=new[]{a.Rotation.x,a.Rotation.y,a.Rotation.z,a.Rotation.w},scale=new[]{a.Scale.x,a.Scale.y,a.Scale.z}}).ToArray();
if(!System.IO.File.Exists("Temp/BuildingAero/RadialFloor/Floors-Before.json"))
    System.IO.File.WriteAllText("Temp/BuildingAero/RadialFloor/Floors-Before.json",Newtonsoft.Json.JsonConvert.SerializeObject(before));
var floors=new List<TowerOfBabel.World.Chunks.ChunkAssetData>();var levels=new List<object>();float radius=generator.radius;
for(int level=0;level<generator.levels;level++){
    radius-=level%2==0?generator.decreaseAmount:0;
    float floorRadius=level==0?radius:(level%2==0?radius+generator.decreaseAmount:radius);
    float height=level*generator.heighIncrease+.2f;
    var sectors=TowerOfBabel.World.Tower.TowerFloorLayout.Build(generator.center,height,floorRadius,generator.tileDecreaseRate,
        generator.radius*generator.tileAngle*Mathf.Deg2Rad,generator.floorTileObject.transform.localScale.y).ToArray();
    var removed=new HashSet<int>();
    foreach(var stair in stairs){
        float rayY=stair.Position.y+.2f;
        if(height-.2f<rayY-.0001f||height-.2f>rayY+2)continue;
        var point=new Vector2(stair.Position.x,stair.Position.z);point-=(point-generator.center).normalized;
        for(int i=0;i<sectors.Length;i++)if(sectors[i].Contains(point,generator.center))removed.Add(i);
    }
    for(int i=0;i<sectors.Length;i++)if(!removed.Contains(i)){
        var s=sectors[i];floors.Add(new TowerOfBabel.World.Chunks.ChunkAssetData(0,TowerOfBabel.World.Tower.TowerAssetType.Floor,s.Position,s.Rotation,s.Scale));
    }
    levels.Add(new{level,radius=floorRadius,height,sectors=sectors.Length,stairOpeningSectors=removed.Count});
}
manager.ConfigureAssetPrefabs(generator);manager.ReplaceCachedFloors(floors);
var after=manager.CachedChunks.SelectMany(c=>c.Assets).Where(a=>a.AssetType!=TowerOfBabel.World.Tower.TowerAssetType.Floor).ToArray();
Func<TowerOfBabel.World.Chunks.ChunkAssetData,string> signature=a=>a.AssetType+"|"+a.Position.ToString("F6")+"|"+a.Rotation.ToString("F6")+"|"+a.Scale.ToString("F6")+"|"+a.Stage;
bool preserved=previous.Where(a=>a.AssetType!=TowerOfBabel.World.Tower.TowerAssetType.Floor).Select(signature).OrderBy(x=>x).SequenceEqual(after.Select(signature).OrderBy(x=>x));
if(!preserved)throw new Exception("Non-floor data changed");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(manager.gameObject.scene);
var report=new{previousFloors=before.Length,newFloors=floors.Count,nonFloorAssetsPreserved=preserved,nonFloorCount=after.Length,
    cacheAssets=manager.CachedAssetCount,chunks=manager.CachedChunks.Count,manager.RadialFloors,floorCenter=manager.FloorCenter.ToString(),manager.FloorRadialDepth,manager.FloorArcLength,levels};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/RadialFloorCacheInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
