using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace TowerOfBabel.World.Tests
{
    public sealed class TowerTileVisibilityEditModeTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static GameObject Prefab(TowerAssetType type) => AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/Prefabs/Buildings/" + (type switch
            {TowerAssetType.Floor => "Floor_Tile", TowerAssetType.Stair => "Step_Tile", _ => type.ToString()}) + ".prefab");
        private static void Set(object target, string name, object value) => target.GetType().GetField(name,Private).SetValue(target,value);
        private static void Draw(InstancedFarChunkRenderer far) => typeof(InstancedFarChunkRenderer).GetMethod("LateUpdate",Private).Invoke(far,null);

        [Test]
        public void ExplicitLegacyModels_FillMissingGlassTypeWithoutReplacingOverrides()
        {
            var root = new GameObject("Legacy far model configuration"); root.SetActive(false);
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.enabled = false;
                foreach (TowerAssetType type in Enum.GetValues(typeof(TowerAssetType))) manager.AssetPrefabs.SetPrefab(type,Prefab(type));
                var models = new List<TowerAssetRenderModel>();
                foreach (var type in new[] {TowerAssetType.Floor,TowerAssetType.Pillar,TowerAssetType.Arch,TowerAssetType.Stair})
                {
                    var model = new TowerAssetRenderModel(); Set(model,"assetType",type); Set(model,"prefab",Prefab(type)); models.Add(model);
                }
                var far = root.AddComponent<InstancedFarChunkRenderer>(); Set(far,"models",models);
                var assets = new List<ChunkAssetData>();
                foreach (var type in new[] {TowerAssetType.Floor,TowerAssetType.GlassWaterFloor})
                    foreach (byte stage in new byte[] {0,1,5,10})
                        assets.Add(new ChunkAssetData(assets.Count,type,new Vector3(50,0,0),Quaternion.identity,Vector3.one,stage));
                var key = new ChunkKey(0,0,0); far.LoadChunk(new FarChunkSnapshot(key,assets));
                Assert.That(far.LoadedInstanceCount,Is.EqualTo(6),"Every built glass/stone stage must survive an older four-model scene list; blueprints stay near-only.");
                far.RemoveChunk(key); Assert.That(far.LoadedInstanceCount,Is.Zero);
            }
            finally {Object.DestroyImmediate(root);}
        }

        [TestCase(TowerAssetType.Floor)]
        [TestCase(TowerAssetType.GlassWaterFloor)]
        public void NativeFarDraw_RendersAll1024NonUniformInstancesAcrossPageBoundaries(TowerAssetType type)
        {
            var root = new GameObject("Far instance pixel fixture"); root.SetActive(false);
            var modelRoot = new GameObject("Render model"); modelRoot.SetActive(false);
            var viewRoot = new GameObject("Far coverage camera");
            var lightRoot = new GameObject("Far coverage sunlight");
            var target = new RenderTexture(1024,1024,24);
            var image = new Texture2D(1024,1024,TextureFormat.RGB24,false);
            var oldTarget = RenderTexture.active; var oldSun = RenderSettings.sun;
            var otherLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
            bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            foreach (var light in otherLights) light.enabled = false;
            try
            {
                var source = Prefab(type);
                modelRoot.AddComponent<MeshFilter>().sharedMesh = source.GetComponent<Building>().GetStageMesh(10);
                modelRoot.AddComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
                var model = new TowerAssetRenderModel(); Set(model,"assetType",type); Set(model,"prefab",modelRoot);
                var far = root.AddComponent<InstancedFarChunkRenderer>(); root.layer = 31;
                Set(far,"models",new List<TowerAssetRenderModel> {model});
                Set(far,"enableFrustumCulling",false); Set(far,"useLightProbes",false);
                var view = viewRoot.AddComponent<Camera>(); view.enabled = false; view.cullingMask = 1<<31;
                view.orthographic = true; view.orthographicSize = 10; view.clearFlags = CameraClearFlags.SolidColor;
                view.backgroundColor = Color.black; view.targetTexture = target; view.nearClipPlane = .1f; view.farClipPlane = 30;
                view.transform.SetPositionAndRotation(new Vector3(0,10,0),Quaternion.Euler(90,0,0));
                Set(far,"renderCamera",view);
                var sun = lightRoot.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1;
                sun.cullingMask = 1<<31; sun.transform.rotation = Quaternion.Euler(90,0,0); RenderSettings.sun = sun;
                var assets = new List<ChunkAssetData>();
                for (int i=0;i<1024;i++)
                {
                    var position = new Vector3((i%32-15.5f)*.6f,0,(i/32-15.5f)*.6f);
                    assets.Add(new ChunkAssetData(i,type,position,Quaternion.identity,new Vector3(.1f,.7f,.1f),10));
                }
                far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0,0,0),assets));
                Draw(far); view.Render(); Draw(far); view.Render();
                RenderTexture.active = target; image.ReadPixels(new Rect(0,0,1024,1024),0,0); image.Apply();
                var missing = new List<int>();
                for (int i=0;i<assets.Count;i++)
                {
                    var point = view.WorldToViewportPoint(assets[i].Position+new Vector3(.2f,0,-.2f));
                    var color = image.GetPixel(Mathf.RoundToInt(point.x*1024),Mathf.RoundToInt(point.y*1024));
                    if (Mathf.Max(color.r,color.g,color.b)<.04f) missing.Add(i);
                }
                Directory.CreateDirectory("Documentation/FrutigerAero/Buildings/Previews");
                File.WriteAllBytes($"Documentation/FrutigerAero/Buildings/Previews/FarCoverage-{type}.png",image.EncodeToPNG());
                Assert.That(missing,Is.Empty,"Native GPU coverage failed at instance indices: "+string.Join(",",missing.Take(25)));
                Assert.That(far.VisiblePageCount,Is.EqualTo(3),"1024 non-uniform transforms require three safe draw pages.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = oldAsync; RenderTexture.active = oldTarget; RenderSettings.sun = oldSun;
                Object.DestroyImmediate(root); Object.DestroyImmediate(modelRoot); Object.DestroyImmediate(viewRoot); Object.DestroyImmediate(lightRoot);
                target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                foreach(var light in otherLights) if(light) light.enabled = true;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GlassSurround_MatchesContinuousMarbleFromAboveAndBelow(bool underside)
        {
            var oldSun = RenderSettings.sun; var oldTarget = RenderTexture.active;
            bool oldAsync = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
            var otherLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
            foreach(var light in otherLights) light.enabled = false;
            var lightRoot = new GameObject("Matched surface light");
            try
            {
                var sun = lightRoot.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1; sun.cullingMask = 1<<31;
                sun.transform.rotation = Quaternion.Euler(underside?-90:90,0,0); RenderSettings.sun = sun;
                var sector = new TowerFloorLayout.Sector(Vector2.zero,0,62,.25f,4f/62,4,1);
                var matrix = Matrix4x4.TRS(sector.Position,sector.Rotation,sector.Scale);
                var centre = matrix.MultiplyPoint3x4(TowerFloorLayout.Bend(new Vector3(2,underside?-.2f:0,-2),62,sector.Scale));
                Color[] Render(TowerAssetType type)
                {
                    var root = new GameObject("Matched material "+type); root.SetActive(false); root.layer = 31;
                    var viewRoot = new GameObject("Matched material camera "+type);
                    var target = new RenderTexture(128,128,24);
                    var image = new Texture2D(128,128,TextureFormat.RGB24,false);
                    try
                    {
                        var manager = root.AddComponent<ChunkManager>(); manager.enabled = false; Set(manager,"radialFloors",true);
                        foreach(TowerAssetType t in Enum.GetValues(typeof(TowerAssetType))) manager.AssetPrefabs.SetPrefab(t,Prefab(t));
                        var far = root.AddComponent<InstancedFarChunkRenderer>(); Set(far,"useLightProbes",false);
                        var view = viewRoot.AddComponent<Camera>(); view.enabled = false; view.cullingMask = 1<<31;
                        view.orthographic = true; view.orthographicSize = 1.8f; view.targetTexture = target;
                        view.clearFlags = CameraClearFlags.SolidColor; view.backgroundColor = Color.black;
                        view.transform.SetPositionAndRotation(centre+Vector3.up*(underside?-8:8),Quaternion.Euler(underside?-90:90,0,0));
                        Set(far,"renderCamera",view);
                        far.LoadChunk(new FarChunkSnapshot(new ChunkKey(0,0,0),new[]{new ChunkAssetData(0,type,sector.Position,sector.Rotation,sector.Scale,10)}));
                        Draw(far); view.Render(); Draw(far); view.Render(); RenderTexture.active = target;
                        image.ReadPixels(new Rect(0,0,128,128),0,0); image.Apply(); return image.GetPixels();
                    }
                    finally
                    {
                        Object.DestroyImmediate(root); Object.DestroyImmediate(viewRoot);
                        RenderTexture.active = oldTarget; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image);
                    }
                }
                var stone = Render(TowerAssetType.Floor); var glass = Render(TowerAssetType.GlassWaterFloor);
                double difference = 0; float maximum = 0;
                for(int i=0;i<stone.Length;i++)
                {
                    float d = Mathf.Max(Mathf.Abs(stone[i].r-glass[i].r),Mathf.Abs(stone[i].g-glass[i].g),Mathf.Abs(stone[i].b-glass[i].b));
                    difference += d; maximum = Mathf.Max(maximum,d);
                }
                Assert.That(stone[64*128+64].maxColorComponent,Is.GreaterThan(.1f),"The material comparison must render a lit face.");
                Assert.That(difference/stone.Length,Is.LessThan(2f/255),"White glass surrounds must not introduce a brighter frame around marble tiles.");
                Assert.That(maximum,Is.LessThan(5f/255),"A tile edge has different shading from the surrounding floor.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation = oldAsync; RenderTexture.active = oldTarget; RenderSettings.sun = oldSun;
                Object.DestroyImmediate(lightRoot); foreach(var light in otherLights) if(light) light.enabled = true;
            }
        }

        [Test]
        public void FinishedFloorFaces_AreFlushSharedAndCloseTheCentre()
        {
            var stone = Prefab(TowerAssetType.Floor).GetComponent<Building>().GetStageMesh(10);
            var glass = Prefab(TowerAssetType.GlassWaterFloor).GetComponent<Building>().GetStageMesh(10);
            Assert.That(stone,Is.SameAs(glass),"Both materials need identical finished edge tessellation.");
            var vertices = stone.vertices; var normals = stone.normals;
            foreach (float y in new[] {0f,-.2f})
            {
                var face = vertices.Where((v,i)=>Mathf.Abs(v.y-y)<.00001f&&Mathf.Abs(normals[i].y)>.99f).ToArray();
                Assert.That(face.Min(v=>v.x),Is.EqualTo(0)); Assert.That(face.Max(v=>v.x),Is.EqualTo(4));
                Assert.That(face.Min(v=>v.z),Is.EqualTo(-4)); Assert.That(face.Max(v=>v.z),Is.EqualTo(0));
                Assert.That(face.Where(v=>Mathf.Abs(v.x)<.00001f).Select(v=>v.z).Distinct().Count(),Is.EqualTo(17));
                Assert.That(face.Where(v=>Mathf.Abs(v.x-4)<.00001f).Select(v=>v.z).Distinct().Count(),Is.EqualTo(17));
            }
            using var cache = new RadialFloorMeshCache();
            foreach (float radius in new[] {94.47f,91.47f,49.47f,7.47f,1.47f})
            {
                var sectors = TowerFloorLayout.Build(Vector2.zero,.2f,radius,3.9f,3.351f).ToArray();
                foreach (var ring in sectors.GroupBy(s=>s.Radius))
                {
                    var tiles = ring.ToArray();
                    for (int i=0;i<tiles.Length;i++)
                    {
                        var a = tiles[i]; var b = tiles[(i+1)%tiles.Length];
                        var ma = Matrix4x4.TRS(a.Position,a.Rotation,a.Scale); var mb = Matrix4x4.TRS(b.Position,b.Rotation,b.Scale);
                        foreach(float x in new[] {0f,2f,4f})
                        {
                            Vector3 edgeA = ma.MultiplyPoint3x4(TowerFloorLayout.Bend(new Vector3(x,0,-4),a.Radius,a.Scale));
                            Vector3 edgeB = mb.MultiplyPoint3x4(TowerFloorLayout.Bend(new Vector3(x,0,0),b.Radius,b.Scale));
                            Assert.That(Vector3.Distance(edgeA,edgeB),Is.LessThan(.0001f),"Angular tile boundary opens.");
                        }
                    }
                }
                var centre = sectors.Last(); var mesh = cache.Get(stone,centre.Radius,centre.Scale);
                var centreTip = mesh.vertices.Where((v,i)=>normals[i].y>.99f && vertices[i].x==4).ToArray();
                Assert.That(centreTip.Max(p=>Vector2.Distance(new Vector2(p.x,p.z),new Vector2(centreTip[0].x,centreTip[0].z))),Is.LessThan(.00001f));
            }
            foreach(var type in new[] {TowerAssetType.Floor,TowerAssetType.GlassWaterFloor})
            {
                var prefab = Prefab(type); var building = prefab.GetComponent<Building>();
                Assert.That(prefab.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(stone));
                for(byte stage=0;stage<10;stage++) Assert.That(building.GetStageMesh(stage),Is.Not.SameAs(stone));
            }
        }
    }
}
