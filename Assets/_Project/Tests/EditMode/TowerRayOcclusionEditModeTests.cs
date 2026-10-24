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
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(profile);}
        }
    }
}
