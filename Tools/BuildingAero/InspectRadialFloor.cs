// Read-only authoring inspection, plus temporary near-pool materialization in Edit Mode.
if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets"||EditorApplication.isPlaying)throw new Exception("Use this project in Edit Mode");
var manager=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.ChunkManager>();
var generator=UnityEngine.Object.FindFirstObjectByType<TowerGenerator>();
var prefab=generator.floorTileObject;var template=prefab.GetComponent<MeshFilter>().sharedMesh;
var floorData=manager.CachedChunks.SelectMany(c=>c.Assets).Where(a=>a.AssetType==TowerOfBabel.World.Tower.TowerAssetType.Floor).ToArray();
var rows=new List<TowerOfBabel.World.Chunks.ChunkAssetData>();
foreach(var a in floorData.Where(a=>Mathf.Abs(a.Position.y-.2f)<.0001f&&new Vector2(a.Position.x,a.Position.z).magnitude<4).Concat(floorData.Where(a=>Mathf.Abs(a.Position.y-.2f)<.0001f&&Mathf.Abs(a.Position.z)<.01f).Take(24)).GroupBy(a=>a.Position).Select(g=>g.First())){
    var copy=a;copy.SetLocalIndex(rows.Count);rows.Add(copy);
}
var ctor=typeof(TowerOfBabel.World.Chunks.ChunkSceneCache).GetConstructors(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Single();
var chunk=(TowerOfBabel.World.Chunks.ChunkSceneCache)ctor.Invoke(new object[]{new TowerOfBabel.World.Chunks.ChunkKey(0,0,0),rows});
var root=new GameObject("Temporary radial floor pool inspection");var pool=root.AddComponent<TowerOfBabel.World.Chunks.NearTowerAssetPool>();
var reports=new List<object>();var centerHits=new List<object>();int outsideBounds=0;int shapeCount=0;int reusedCount=0;
try{
    pool.Configure(manager.AssetPrefabs,manager);pool.BeginUpdate();pool.AddChunk(chunk);pool.EndUpdate();Physics.SyncTransforms();
    foreach(var go in pool.ActiveAssets){
        var filter=go.GetComponent<MeshFilter>();var collider=go.GetComponent<MeshCollider>();var material=go.GetComponent<MeshRenderer>().sharedMaterial;
        var uv=new List<Vector3>();filter.sharedMesh.GetUVs(2,uv);
        var p=go.transform.TransformPoint(TowerOfBabel.World.Tower.TowerFloorLayout.Bend(new Vector3(2,0,-2),new Vector2(go.transform.position.x,go.transform.position.z).magnitude,go.transform.lossyScale));
        bool hit=collider.Raycast(new Ray(p+Vector3.up*2,Vector3.down),out var hitInfo,4);
        reports.Add(new{radius=new Vector2(go.transform.position.x,go.transform.position.z).magnitude,sameRenderAndCollider=filter.sharedMesh==collider.sharedMesh,
            radialMode=material.GetFloat("_RadialFloor"),uvPatch=uv.Count,walkHit=hit,hitY=hit?hitInfo.point.y:-999f});
    }
    foreach(var point in new[]{Vector2.zero,new Vector2(.01f,0),new Vector2(-.01f,.01f),new Vector2(1,1)}){
        bool hit=false;float height=-999;
        foreach(var go in pool.ActiveAssets){var c=go.GetComponent<MeshCollider>();if(c.Raycast(new Ray(new Vector3(point.x,2,point.y),Vector3.down),out var h,4)){hit=true;height=h.point.y;break;}}
        centerHits.Add(new{point=point.ToString(),hit,height});
    }
    var firstMeshes=pool.ActiveAssets.Select(g=>g.GetComponent<MeshFilter>().sharedMesh).ToHashSet();
    pool.BeginUpdate();pool.EndUpdate();pool.BeginUpdate();pool.AddChunk(chunk);pool.EndUpdate();
    reusedCount=pool.ActiveAssets.Count(g=>firstMeshes.Contains(g.GetComponent<MeshFilter>().sharedMesh));
    using(var cache=new TowerOfBabel.World.Tower.RadialFloorMeshCache()){
        var bounds=TowerOfBabel.World.Tower.TowerFloorLayout.ConservativeBounds(template.bounds,manager.FloorRadialDepth,manager.FloorArcLength);
        foreach(var a in floorData){
            float radius=Vector2.Distance(new Vector2(a.Position.x,a.Position.z),manager.FloorCenter);
            var mesh=cache.Get(template,radius,a.Scale);
            if(mesh.bounds.min.x<bounds.min.x-.0001f||mesh.bounds.max.x>bounds.max.x+.0001f||mesh.bounds.min.z<bounds.min.z-.0001f||mesh.bounds.max.z>bounds.max.z+.0001f)outsideBounds++;
        }
        shapeCount=cache.Count;
    }
}finally{UnityEngine.Object.DestroyImmediate(root);}
var far=UnityEngine.Object.FindFirstObjectByType<TowerOfBabel.World.Chunks.InstancedFarChunkRenderer>();
var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
far.GetType().GetMethod("EnsureModelBatches",flags).Invoke(far,null);
var batches=(System.Collections.IDictionary)far.GetType().GetField("batches",flags).GetValue(far);var farModes=new List<object>();
foreach(System.Collections.DictionaryEntry pair in batches){var b=pair.Value;var material=(Material)b.GetType().GetField("material",flags).GetValue(b);if(material.GetFloat("_ModelKind")==1)farModes.Add(new{material=material.name,mode=material.GetFloat("_RadialFloor"),material.enableInstancing});}
var report=new{floorInstances=floorData.Length,nearSamples=reports,centerHits,reusedCount,sharedCollisionShapes=shapeCount,outsideConservativeBounds=outsideBounds,farFloorBatches=farModes,
    triangles=template.triangles.Length/3,submeshes=template.subMeshCount,stages=new SerializedObject(prefab.GetComponent<TowerOfBabel.Buildings.Building>()).FindProperty("stageMeshes").arraySize,
    compilationFailed=EditorUtility.scriptCompilationFailed,shaderMessages=ShaderUtil.GetShaderMessages(prefab.GetComponent<MeshRenderer>().sharedMaterial.shader).Select(x=>x.message).ToArray()};
System.IO.File.WriteAllText("Documentation/FrutigerAero/Buildings/RadialFloorPoolInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
return new{near=reports.Count,centerHits,reusedCount,shapeCount,outsideBounds,farBatches=farModes,report.compilationFailed,report.shaderMessages};
