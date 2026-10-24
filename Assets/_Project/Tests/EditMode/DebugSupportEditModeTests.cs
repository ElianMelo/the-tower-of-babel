using System;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Buildings;
using TowerOfBabel.Cheats;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace TowerOfBabel.World.Tests
{
    public sealed class DebugSupportEditModeTests
    {
        [Test]
        public void InstantBuild_ProfileIsAnOffByDefaultBooleanAndResets()
        {
            var profile = new CheatProfile(); Assert.That(profile.Get(CheatStat.InstantBuild), Is.Zero);
            profile.Set(CheatStat.InstantBuild, .4f); Assert.That(profile.Get(CheatStat.InstantBuild), Is.Zero);
            profile.Set(CheatStat.InstantBuild, 100); Assert.That(profile.Get(CheatStat.InstantBuild), Is.EqualTo(1));
            profile.Reset(); Assert.That(profile.Get(CheatStat.InstantBuild), Is.Zero);
        }
        [Test]
        public void FloorSupport_BridgeConnectsToTheDeckWithoutAddingAnOuterIsland()
        {
            var sectors = TowerFloorLayout.Build(Vector2.zero, 6.2f, 10, 2, 3).ToArray();
            var missing = sectors[0];
            var floors = sectors.Skip(1).Select(s => new ChunkAssetData(0, TowerAssetType.Floor, s.Position, s.Rotation, s.Scale));
            var support = new TowerFloorSupport(floors, Vector2.zero);
            float angle = missing.Angle + missing.Sweep * .5f;
            var point = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 9.4f;
            Assert.That(support.TryGetHeight(point, out _), Is.False);
            Assert.That(support.TryFindInnerConnection(point,.65f,out float connection),Is.True);
            var pad = TowerFloorLayout.SupportBridge(Vector2.zero, 6.2f, point, .65f, connection, 1);
            support.Add(new ChunkAssetData(0, TowerAssetType.Floor, pad.Position, pad.Rotation, pad.Scale));
            Assert.That(support.SupportsDisk(point, .58f, out float height), Is.True); Assert.That(height, Is.EqualTo(6.2f));
            for(float r=connection;r<9.4f;r+=.05f)
                Assert.That(support.TryGetHeight(point.normalized*r,out float top),Is.True,$"Disconnected bridge at radius {r}");
            Assert.That(support.TryGetHeight(point.normalized*10.8f,out _),Is.False,"Do not add another island across the stair route.");
        }
        [Test]
        public void PillarSupportPad_HasRealWalkingCollisionAcrossItsBase()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
            var pad = TowerFloorLayout.SupportBridge(Vector2.zero, 12.2f, new Vector2(40, 0), .65f, 37, 1);
            using var cache = new RadialFloorMeshCache();
            var root = new GameObject("Support collision fixture");
            try
            {
                root.transform.SetPositionAndRotation(pad.Position, pad.Rotation); root.transform.localScale = pad.Scale;
                var collider = root.AddComponent<MeshCollider>(); collider.sharedMesh = cache.Get(prefab.GetComponent<Building>().GetStageMesh(10), pad.Radius, pad.Scale);
                Physics.SyncTransforms();
                for (int i = 0; i < 8; i++)
                {
                    float angle = i * Mathf.PI / 4; var point = new Vector3(40 + Mathf.Cos(angle) * .4f, 13, Mathf.Sin(angle) * .4f);
                    Assert.That(collider.Raycast(new Ray(point, Vector3.down), out var hit, 2), Is.True);
                    Assert.That(hit.point.y, Is.EqualTo(12.2f).Within(.002f));
                }
                for(float radius=37.05f;radius<40.4f;radius+=.1f)
                    Assert.That(collider.Raycast(new Ray(new Vector3(radius,13,0),Vector3.down),out _,2),Is.True,"Collision must span the connection to the deck.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void RegeneratedTower_RetainsPillarSupportsAfterCuttingStairOpenings()
        {
            var root = new GameObject("Regeneration support fixture"); var parent = new GameObject("Generated parts"); parent.transform.SetParent(root.transform);
            var generator = root.AddComponent<TowerGenerator>(); generator.parent = parent.transform;
            generator.center = new Vector2(5000, 5000); generator.radius = 8; generator.decreaseAmount = 1; generator.heighIncrease = 6;
            generator.levels = 3; generator.pillarAngle = 30; generator.tileAngle = 20; generator.tileDecreaseRate = 2;
            generator.floorHeight = 6; generator.stairHeight = .5f; generator.yOffset = 3.85f;
            generator.pillarObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab");
            generator.archObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
            generator.floorTileObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
            generator.stairStepObject = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Step_Tile.prefab");
            try
            {
                generator.GenerateTower();
                var assets = parent.GetComponentsInChildren<TowerAsset>();
                var supports = assets.Where(a => a.AssetType == TowerAssetType.Floor).Select(a => new ChunkAssetData(0, TowerAssetType.Floor, a.transform.position, a.transform.rotation, a.transform.lossyScale))
                    .GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / 6)).ToDictionary(g => g.Key, g => new TowerFloorSupport(g, generator.center));
                foreach (var pillar in assets.Where(a => a.AssetType == TowerAssetType.Pillar))
                {
                    int floor = Mathf.FloorToInt((pillar.transform.position.y + .001f) / 6);
                    Assert.That(supports[floor].SupportsDisk(new Vector2(pillar.transform.position.x, pillar.transform.position.z), generator.PillarSupportRadius - .05f, out _), Is.True);
                    if (supports.TryGetValue(floor + 1, out var ceiling) && Mathf.Abs(generator.GetWallRadius(floor+1)-generator.GetWallRadius(floor))<.01f)
                        Assert.That(ceiling.SupportsDisk(new Vector2(pillar.transform.position.x, pillar.transform.position.z), generator.PillarSupportRadius - .05f, out _), Is.True, "Regenerated pillar cap has no ceiling support.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        [Test]
        public void SavedTower_EveryPillarAndBushHasSupportIncludingShrinkingFloors()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            bool opened=!scene.isLoaded;
            if(opened) scene=EditorSceneManager.OpenScene("Assets/_Project/Scenes/Tower.unity", OpenSceneMode.Additive);
            try
            {
                var roots = scene.GetRootGameObjects(); var g = roots.SelectMany(r => r.GetComponentsInChildren<TowerGenerator>(true)).Single();
                var manager = roots.SelectMany(r => r.GetComponentsInChildren<ChunkManager>(true)).Single();
                var scatter = g.GetComponent<TowerBushScatter>();
                var all = manager.CachedChunks.SelectMany(c => c.Assets).ToArray();
                var supports = all.Where(a => a.AssetType == TowerAssetType.Floor).GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / g.heighIncrease))
                    .ToDictionary(group => group.Key, group => new TowerFloorSupport(group, g.center));
                int checkedPillars = 0;
                foreach (var pillar in all.Where(a => a.AssetType == TowerAssetType.Pillar))
                {
                    int floor = Mathf.FloorToInt((pillar.Position.y + .001f) / g.heighIncrease);
                    Assert.That(supports[floor].SupportsDisk(new Vector2(pillar.Position.x, pillar.Position.z), g.PillarSupportRadius - .05f, out float top), Is.True, $"Unsupported pillar on floor {floor}: {pillar.Position}");
                    Assert.That(top - .2f, Is.EqualTo(pillar.Position.y).Within(.002f)); checkedPillars++;
                    if (supports.TryGetValue(floor + 1, out var ceiling) && Mathf.Abs(g.GetWallRadius(floor+1)-g.GetWallRadius(floor))<.01f)
                    {
                        Assert.That(ceiling.SupportsDisk(new Vector2(pillar.Position.x, pillar.Position.z), g.PillarSupportRadius - .05f, out float ceilingTop), Is.True, $"Missing ceiling at the setback above floor {floor}");
                        Assert.That(ceilingTop - .2f, Is.EqualTo((floor + 1) * g.heighIncrease).Within(.002f));
                    }
                }
                Assert.That(checkedPillars, Is.GreaterThan(5000)); Assert.That(scatter.Placements.Count, Is.GreaterThan(100));
                foreach (var bush in scatter.Placements)
                {
                    Assert.That(supports[bush.floor].SupportsDisk(new Vector2(bush.position.x, bush.position.z), bush.footprint, out float top), Is.True, $"Unsupported bush: {bush.position}");
                    float bottom = scatter.BushPrefabs[bush.variant].GetComponent<MeshFilter>().sharedMesh.bounds.min.y;
                    Assert.That(bush.position.y + bottom * bush.scale.y, Is.EqualTo(top - TowerBushScatter.RootEmbedDepth).Within(.002f));
                    Assert.That(Vector2.Distance(new Vector2(bush.position.x, bush.position.z), g.center) - bush.footprint, Is.GreaterThan(bush.wallRadius));
                }
                for (int floor = 2; floor < g.levels; floor += 2)
                    Assert.That(g.GetFloorOuterRadius(floor), Is.GreaterThan(g.GetWallRadius(floor - 1) + .5f));
                int connected=0;
                foreach(var group in all.Where(a=>a.AssetType==TowerAssetType.Floor).GroupBy(a=>Mathf.FloorToInt(a.Position.y/g.heighIncrease)))
                {
                    float wall=g.GetWallRadius(group.Key),previous=g.GetWallRadius(Mathf.Max(0,group.Key-1));
                    if(wall>=previous-.01f) continue;
                    var mainland=new TowerFloorSupport(group.Where(a=>!TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,wall,previous)),g.center);
                    Assert.That(group.Any(a=>TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,previous)),Is.False,"Outer cap islands must be removed.");
                    foreach(var bridge in group.Where(a=>TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,wall)))
                    {
                        Vector2 delta=new Vector2(bridge.Position.x,bridge.Position.z)-g.center;
                        float outer=delta.magnitude,inner=outer-bridge.Scale.x*4;
                        float angle=Mathf.Atan2(delta.y,delta.x),sweep=bridge.Scale.z*4/outer;
                        for(int i=0;i<=4;i++)
                        {
                            float a=angle+sweep*i/4;
                            Assert.That(mainland.TryGetHeight(g.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(inner+.01f),out _),Is.True,"Every inward bridge edge must overlap the main floor.");
                        }
                        connected++;
                    }
                }
                Assert.That(connected,Is.GreaterThan(1000));
            }
            finally { if(opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
