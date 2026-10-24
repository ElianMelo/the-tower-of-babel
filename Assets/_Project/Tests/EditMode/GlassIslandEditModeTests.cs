using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Resources;
using TowerOfBabel.World;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class GlassIslandEditModeTests
    {
        [Test]
        public void GlassLayout_HasTwoRingsCardinalSpokesAndCenterPool()
        {
            for(int i=0;i<72;i++)
            {
                var direction=new Vector2(Mathf.Cos(i*Mathf.PI/36),Mathf.Sin(i*Mathf.PI/36));
                Assert.That(GlassWaterFloorLayout.IsChannel(direction*46.5f,93,3.9f),Is.True);
                Assert.That(GlassWaterFloorLayout.IsChannel(direction*81.84f,93,3.9f),Is.True);
            }
            Assert.That(GlassWaterFloorLayout.IsChannel(Vector2.zero,93,3.9f),Is.True);
            Assert.That(GlassWaterFloorLayout.IsChannel(new Vector2(0,65),93,3.9f),Is.True);
            Assert.That(GlassWaterFloorLayout.IsChannel(new Vector2(36,36),93,3.9f),Is.False);
        }

        [Test]
        public void GlassPrefab_UsesGlassHasEveryStageAndSameWalkingDatum()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab");
            var building=prefab.GetComponent<Building>(); Assert.That(building.ResourceType,Is.EqualTo(ResourceType.Glass));
            Assert.That(prefab.GetComponent<TowerAsset>().AssetType,Is.EqualTo(TowerAssetType.GlassWaterFloor));
            for(byte i=0;i<=10;i++) Assert.That(building.GetStageMesh(i),Is.Not.Null);
            var mesh=building.GetStageMesh(10); Assert.That(Vector3.Distance(mesh.bounds.min,new Vector3(0,-.2f,-4)),Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(mesh.bounds.max,new Vector3(4,0,0)),Is.LessThan(.0001f));
            Assert.That(prefab.GetComponent<MeshCollider>().sharedMesh,Is.SameAs(mesh));
            Assert.That(prefab.GetComponent<GlassWaterFloorLight>(),Is.Not.Null);
        }

        [Test]
        public void SavedIsland_IsSubmergedOnAllEdgesAndRetainsConstructionAssets()
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity",OpenSceneMode.Additive);
            try
            {
                var roots=scene.GetRootGameObjects();
                var terrain=roots.SelectMany(r=>r.GetComponentsInChildren<AeroTowerTerrain>(true)).Single();
                var data=terrain.GeneratedTerrainData; float origin=terrain.transform.position.y;
                var holes=data.GetHoles(0,0,data.holesResolution,data.holesResolution);
                for(int i=0;i<data.heightmapResolution;i+=8)
                {
                    Assert.That(data.GetHeight(i,0)+origin,Is.LessThan(terrain.WaterElevation-10));
                    Assert.That(data.GetHeight(i,data.heightmapResolution-1)+origin,Is.LessThan(terrain.WaterElevation-10));
                    Assert.That(data.GetHeight(0,i)+origin,Is.LessThan(terrain.WaterElevation-10));
                    Assert.That(data.GetHeight(data.heightmapResolution-1,i)+origin,Is.LessThan(terrain.WaterElevation-10));
                }
                Assert.That(holes[0,0],Is.False);
                var chunks=roots.SelectMany(r=>r.GetComponentsInChildren<ChunkManager>(true)).Single();
                var assets=chunks.CachedChunks.SelectMany(c=>c.Assets).ToArray();
                Assert.That(assets.Length,Is.EqualTo(87972)); Assert.That(assets.Count(a=>a.AssetType==TowerAssetType.GlassWaterFloor),Is.GreaterThan(10000));
                Assert.That(chunks.AssetPrefabs.GetPrefab(TowerAssetType.GlassWaterFloor),Is.Not.Null);
                Assert.That(data.heightmapResolution,Is.EqualTo(513)); Assert.That(data.detailResolution,Is.EqualTo(512));
            }
            finally {EditorSceneManager.CloseScene(scene,true);}
        }

        [Test]
        public void FarRenderer_KeepsGlassBuiltStagesAndCullsItsEmptyBlueprint()
        {
            var go=new GameObject("Glass far renderer fixture"); go.SetActive(false);
            try
            {
                var manager=go.AddComponent<ChunkManager>(); manager.enabled=false;
                var prefabs=manager.AssetPrefabs;
                var floor=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
                prefabs.SetPrefab(TowerAssetType.Floor,floor); prefabs.SetPrefab(TowerAssetType.Stair,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Step_Tile.prefab"));
                prefabs.SetPrefab(TowerAssetType.Pillar,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab"));
                prefabs.SetPrefab(TowerAssetType.Arch,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab"));
                prefabs.SetPrefab(TowerAssetType.GlassWaterFloor,AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/GlassWaterFloor.prefab"));
                var far=go.AddComponent<InstancedFarChunkRenderer>();
                var key=new ChunkKey(0,0,0);
                var assets=new[]{new ChunkAssetData(0,TowerAssetType.GlassWaterFloor,new Vector3(93,.2f,0),Quaternion.identity,Vector3.one,0),
                    new ChunkAssetData(1,TowerAssetType.GlassWaterFloor,new Vector3(89,.2f,0),Quaternion.identity,Vector3.one,5),
                    new ChunkAssetData(2,TowerAssetType.GlassWaterFloor,new Vector3(85,.2f,0),Quaternion.identity,Vector3.one,10)};
                far.LoadChunk(new FarChunkSnapshot(key,assets));
                Assert.That(far.LoadedInstanceCount,Is.EqualTo(2)); far.RemoveChunk(key); Assert.That(far.LoadedInstanceCount,Is.Zero);
            }
            finally {Object.DestroyImmediate(go);}
        }
    }
}
