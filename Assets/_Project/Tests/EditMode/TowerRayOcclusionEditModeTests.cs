using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class TowerRayOcclusionEditModeTests
    {
        [Test]
        public void ActualTower_ArchSourceIdentitiesAreUniqueAndSurviveConstructionChanges()
        {
            var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity",UnityEditor.SceneManagement.OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();var manager=roots.SelectMany(r=>r.GetComponentsInChildren<ChunkManager>(true)).Single();
                var bridge=roots.SelectMany(r=>r.GetComponentsInChildren<TowerAtmosphereBridge>(true)).Single();
                manager.SetConstructionStage(0,10);bridge.Refresh(0,0);
                var sources=bridge.atmosphere.Openings;var ids=sources.Select(s=>s.Id).ToArray();
                Assert.That(ids.Length,Is.GreaterThan(200));Assert.That(ids.Distinct().Count(),Is.EqualTo(ids.Length));
                var deck=manager.CachedChunks.Where(c=>c.Key.FloorIndex==0).First(c=>c.Assets.Any(a=>a.AssetType.IsFloor()));
                int index=deck.Assets.First(a=>a.AssetType.IsFloor()).LocalIndex;
                manager.SetAssetStage(deck.Key,index,9);bridge.Refresh(0,0);
                Assert.That(bridge.atmosphere.Openings.Select(s=>s.Id),Is.EqualTo(ids));
            }
            finally{UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);}
        }

        [Test]
        public void ShadowCache_RejectsBlueprintsAndHiddenFloorsAndUpdatesOnlyChangedChunks()
        {
            var root=new GameObject("Shadow construction fixture");root.SetActive(false);
            var profile=ScriptableObject.CreateInstance<AeroDayNightProfile>();
            try
            {
                var manager=root.AddComponent<ChunkManager>();manager.enabled=false;
                foreach(var pair in new[]{(TowerAssetType.Floor,"Floor_Tile"),(TowerAssetType.Pillar,"Pillar"),(TowerAssetType.Arch,"Arch"),(TowerAssetType.Stair,"Step_Tile"),(TowerAssetType.GlassWaterFloor,"GlassWaterFloor")})
                    manager.AssetPrefabs.SetPrefab(pair.Item1,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/"+pair.Item2+".prefab"));
                var authoring=new GameObject("Authoring");authoring.transform.SetParent(root.transform);
                foreach(var entry in new[]{(new Vector3(0,0,0),TowerAssetType.Floor),(new Vector3(32,0,0),TowerAssetType.Arch),(new Vector3(0,6,0),TowerAssetType.Floor)})
                {
                    var go=new GameObject("Cached asset");go.transform.SetParent(authoring.transform);go.transform.position=entry.Item1;go.AddComponent<TowerAsset>().SetAssetType(entry.Item2);
                }
                typeof(ChunkManager).GetField("towerRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,authoring);manager.CacheChunks();authoring.SetActive(false);
                var cycle=root.AddComponent<AeroDayNightCycle>();cycle.profile=profile;
                var fx=root.AddComponent<AeroTowerAtmosphere>();fx.cycle=cycle;
                var bridge=root.AddComponent<TowerAtmosphereBridge>();bridge.chunks=manager;bridge.atmosphere=fx;
                root.SetActive(true);bridge.Refresh(0,0);
                Assert.That(bridge.CachedShadowChunks,Is.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<MeshFilter>().All(f=>f.sharedMesh.vertexCount==0),Is.True,"Blueprints cannot be solid occluders.");
                int builds=bridge.ShadowMeshBuilds;bridge.Refresh(0,0);Assert.That(bridge.ShadowMeshBuilds,Is.EqualTo(builds));
                manager.SetAssetStage(new ChunkKey(0,0,0),0,5);bridge.Refresh(0,0);
                Assert.That(bridge.ShadowMeshBuilds,Is.EqualTo(builds+1));
                var partial=root.GetComponentsInChildren<MeshFilter>().Single(f=>f.sharedMesh.vertexCount>0).sharedMesh;
                Assert.That(partial.vertexCount,Is.EqualTo(manager.AssetPrefabs.GetPrefab(TowerAssetType.Floor).GetComponent<Building>().GetStageMesh(5).vertexCount));
                manager.SetAssetStage(new ChunkKey(1,0,0),0,10);bridge.Refresh(0,0);Assert.That(bridge.CachedShadowChunks,Is.EqualTo(2),"A saved built but locked upper floor must remain absent.");
                manager.SetAssetStage(new ChunkKey(0,0,0),0,10);manager.SetAssetStage(new ChunkKey(0,1,0),0,10);bridge.Refresh(0,0);
                Assert.That(bridge.CachedShadowChunks,Is.EqualTo(3));Assert.That(fx.Openings.Count,Is.EqualTo(1));
                manager.SetAssetStage(new ChunkKey(0,0,0),0,0);bridge.Refresh(0,0);
                Assert.That(bridge.CachedShadowChunks,Is.EqualTo(2));
                bridge.Refresh(10,10);Assert.That(bridge.CachedShadowChunks,Is.Zero);
                // Lag-spike fix: walking back to a floor reuses parked proxies instead of recombining.
                Assert.That(bridge.ParkedShadowChunks,Is.EqualTo(2));
                builds=bridge.ShadowMeshBuilds;bridge.Refresh(0,0);
                Assert.That(bridge.CachedShadowChunks,Is.EqualTo(2));Assert.That(bridge.ShadowMeshBuilds,Is.EqualTo(builds),"Parked floors come back without a rebuild.");
                // A zero build budget defers combining to later frames rather than stalling one frame.
                bridge.Refresh(10,10);manager.SetAssetStage(new ChunkKey(0,1,0),0,5);
                builds=bridge.ShadowMeshBuilds;bridge.Refresh(0,0,0);
                Assert.That(bridge.RefreshPending,Is.True);Assert.That(bridge.ShadowMeshBuilds-builds,Is.LessThanOrEqualTo(1));
                bridge.Refresh(0,0);Assert.That(bridge.RefreshPending,Is.False);Assert.That(bridge.CachedShadowChunks,Is.EqualTo(2));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(profile);}
        }

        /// <summary>
        /// River tiles one chunk away cast through the cached proxy, near ones through their own renderer. Both must
        /// leave the water channel open, or the shafts under the river pop on and off at every chunk line (2026-11-10).
        /// </summary>
        [Test]
        public void ShadowProxy_KeepsTheRiverChannelOpenToSunlightAndTheBanksSolid()
        {
            var oldSun=RenderSettings.sun;var oldTarget=RenderTexture.active;bool oldAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;
            var otherLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();foreach(var light in otherLights)light.enabled=false;
            var root=new GameObject("River shadow fixture"){layer=31};root.SetActive(false);
            var stage=new GameObject("River shadow stage");var profile=ScriptableObject.CreateInstance<AeroDayNightProfile>();
            var target=new RenderTexture(128,128,24);var image=new Texture2D(128,128,TextureFormat.RGB24,false);
            var white=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var manager=root.AddComponent<ChunkManager>();manager.enabled=false;
                typeof(ChunkManager).GetField("radialFloors",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,false);
                foreach(var pair in new[]{(TowerAssetType.Floor,"Floor_Tile"),(TowerAssetType.Pillar,"Pillar"),(TowerAssetType.Arch,"Arch"),(TowerAssetType.Stair,"Step_Tile"),(TowerAssetType.GlassWaterFloor,"GlassWaterFloor")})
                    manager.AssetPrefabs.SetPrefab(pair.Item1,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/"+pair.Item2+".prefab"));
                var glassMaterial=manager.AssetPrefabs.GetPrefab(TowerAssetType.GlassWaterFloor).GetComponent<Renderer>().sharedMaterial;
                Vector4 layout=glassMaterial.GetVector("_RiverLayout");
                // A tile spans x..x+4 and z-4..z from its origin. Find one the inner river ring crosses with bank on both sides.
                // Ten metres off the axis, clear of the straight spokes.
                const float row=10;float radius=Mathf.Max(3,layout.x-layout.y)*.5f;float ring=Mathf.Sqrt(radius*radius-row*row);float tileX=ring-2;
                float Channel(float x)=>GlassWaterFloorLayout.ChannelDistance(new Vector2(x,row),0,layout);
                Assert.That(Channel(ring),Is.LessThan(-.3f),"The fixture tile must contain open water.");
                Assert.That(Channel(tileX+.2f),Is.GreaterThan(.2f),"The fixture tile must contain a solid bank.");
                var authoring=new GameObject("Authoring");authoring.transform.SetParent(root.transform);
                foreach(var entry in new[]{(new Vector3(tileX,0,row+2),TowerAssetType.GlassWaterFloor),(new Vector3(tileX-4,0,row+2),TowerAssetType.Floor)})
                {
                    var go=new GameObject("Cached asset");go.transform.SetParent(authoring.transform);go.transform.position=entry.Item1;go.AddComponent<TowerAsset>().SetAssetType(entry.Item2);
                }
                typeof(ChunkManager).GetField("towerRoot",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager,authoring);manager.CacheChunks();authoring.SetActive(false);
                var cycle=root.AddComponent<AeroDayNightCycle>();cycle.profile=profile;cycle.enabled=false;
                var fx=root.AddComponent<AeroTowerAtmosphere>();fx.cycle=cycle;
                var bridge=root.AddComponent<TowerAtmosphereBridge>();bridge.chunks=manager;bridge.atmosphere=fx;
                root.SetActive(true);
                foreach(var chunk in manager.CachedChunks.ToArray())foreach(var asset in chunk.Assets.ToArray())manager.SetAssetStage(chunk.Key,asset.LocalIndex,5);
                bridge.Refresh(0,0);
                Assert.That(bridge.GlassShadowRenderers,Is.Zero,"A river tile under construction is still a plain occluder.");
                foreach(var chunk in manager.CachedChunks.ToArray())foreach(var asset in chunk.Assets.ToArray())manager.SetAssetStage(chunk.Key,asset.LocalIndex,10);
                bridge.Refresh(0,0);
                Assert.That(bridge.GlassShadowRenderers,Is.GreaterThan(0));
                var casters=root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(casters.All(r=>r.shadowCastingMode==UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly),Is.True);
                var river=casters.Single(r=>r.sharedMaterial.shader==glassMaterial.shader);
                Assert.That(river.sharedMaterial.GetFloat("_RadialFloor"),Is.Zero,"Proxy vertices are already in world space.");
                Assert.That(river.GetComponent<MeshFilter>().sharedMesh.vertexCount,Is.GreaterThan(0));
                Assert.That(casters.Count(r=>r!=river&&r.GetComponent<MeshFilter>().sharedMesh.vertexCount>0),Is.EqualTo(1),"The marble tile stays in the plain structural proxy.");

                // Sun straight down onto a white ground three metres below the deck, seen from between the two.
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.layer=31;ground.transform.SetParent(stage.transform);
                ground.transform.position=new Vector3(tileX,-3,row);ground.transform.localScale=new Vector3(40,.1f,40);ground.GetComponent<Renderer>().sharedMaterial=white;
                var sun=new GameObject("River shadow sun").AddComponent<Light>();sun.transform.SetParent(stage.transform);
                sun.type=LightType.Directional;sun.intensity=1;sun.shadows=LightShadows.Hard;sun.shadowStrength=1;sun.cullingMask=1<<31;
                sun.transform.rotation=Quaternion.Euler(90,0,0);RenderSettings.sun=sun;
                var view=new GameObject("River shadow camera").AddComponent<Camera>();view.transform.SetParent(stage.transform);
                view.enabled=false;view.cullingMask=1<<31;view.orthographic=true;view.orthographicSize=5;view.nearClipPlane=.05f;view.farClipPlane=10;
                view.targetTexture=target;view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=Color.black;
                view.transform.SetPositionAndRotation(new Vector3(tileX+1,-1,row),Quaternion.Euler(90,0,0));
                view.Render();view.Render();RenderTexture.active=target;image.ReadPixels(new Rect(0,0,128,128),0,0);image.Apply();
                float Light(float x)
                {
                    int px=Mathf.RoundToInt(((x-view.transform.position.x)/(2*view.orthographicSize)+.5f)*127);
                    float sum=0;for(int dy=-2;dy<=2;dy++)sum+=image.GetPixel(px,64+dy).grayscale;return sum/5;
                }
                float open=Light(tileX+5.5f),water=Light(ring),bank=Light(tileX+.25f),marble=Light(tileX-2);
                Assert.That(open,Is.GreaterThan(marble+.15f),"The fixture must show sunlight and a solid marble shadow.");
                Assert.That(water,Is.GreaterThan(open-.05f),"Sunlight must pass through the water channel of a cached river tile.");
                Assert.That(bank,Is.LessThan(marble+.05f),"The banks of a cached river tile must still cast a shadow.");
            }
            finally
            {
                ShaderUtil.allowAsyncCompilation=oldAsync;RenderTexture.active=oldTarget;RenderSettings.sun=oldSun;
                Object.DestroyImmediate(root);Object.DestroyImmediate(stage);Object.DestroyImmediate(profile);Object.DestroyImmediate(white);
                target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(image);
                foreach(var light in otherLights)if(light)light.enabled=true;
            }
        }
    }
}
