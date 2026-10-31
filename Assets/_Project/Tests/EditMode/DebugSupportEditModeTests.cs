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
        public void PillarLedge_NeighbouringTilesShareWalkingCollisionAcrossTheJoint()
        {
            var pillars=Enumerable.Range(0,24).Select(i=>new Vector3(Mathf.Cos(i*Mathf.PI/12)*40,12,Mathf.Sin(i*Mathf.PI/12)*40)).ToArray();
            var layout=new TowerPillarLedge(pillars.Concat(new[]{pillars[0]+Vector3.forward*.0002f}),Vector2.zero,.65f);
            var first=layout.Bridge(new Vector2(40,0),12.2f,37,1);
            Assert.That(first.Sweep,Is.EqualTo(Mathf.PI/12+TowerPillarLedge.JointOverlap*2/40).Within(.00001f),"A nearly duplicated seam pillar must not truncate the connecting tile.");
            var second=layout.Bridge(new Vector2(pillars[1].x,pillars[1].z),12.2f,37,1);
            var a=new ChunkAssetData(0,TowerAssetType.Floor,first.Position,first.Rotation,first.Scale);
            var b=new ChunkAssetData(1,TowerAssetType.Floor,second.Position,second.Rotation,second.Scale);
            Assert.That(layout.IsConnectedSupport(a),Is.True);Assert.That(layout.IsConnectedSupport(b),Is.True);
            var support=new TowerFloorSupport(new[]{a,b},Vector2.zero);
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab");
            using var cache=new RadialFloorMeshCache();var roots=new[]{new GameObject("Ledge tile A"),new GameObject("Ledge tile B")};
            var colliders=new MeshCollider[2];var sectors=new[]{first,second};
            try
            {
                for(int i=0;i<2;i++)
                {
                    roots[i].transform.SetPositionAndRotation(sectors[i].Position,sectors[i].Rotation);roots[i].transform.localScale=sectors[i].Scale;
                    colliders[i]=roots[i].AddComponent<MeshCollider>();colliders[i].sharedMesh=cache.Get(prefab.GetComponent<Building>().GetStageMesh(10),sectors[i].Radius,sectors[i].Scale);
                }
                Physics.SyncTransforms();
                for(int i=0;i<=80;i++)
                {
                    float angle=Mathf.PI/12*i/80;Vector2 p=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*40;
                    Assert.That(support.TryGetHeight(p,out _),Is.True,"A support ledge must connect all the way to the side pillar.");
                    var ray=new Ray(new Vector3(p.x,13,p.y),Vector3.down);
                    Assert.That(colliders.Any(c=>c.Raycast(ray,out _,2)),Is.True,"The joint needs real continuous walking collision.");
                }
            }
            finally {foreach(var root in roots)UnityEngine.Object.DestroyImmediate(root);}
        }
        [Test]
        public void ArchBearing_RequiresContactWithTheUpperBeamAndRespectsTransforms()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Arch.prefab");
            var mesh=prefab.GetComponent<Building>().GetStageMesh(10);
            var arch=new ChunkAssetData(42,TowerAssetType.Arch,new Vector3(10000,3.85f,10000),Quaternion.Euler(0,73,0),new Vector3(1.3f,1,1.2f));
            var bearing=new TowerArchSupport(new[]{arch},mesh.bounds);
            var point=arch.ObjectToWorld.MultiplyPoint3x4(new Vector3(0,mesh.bounds.max.y,0));
            Assert.That(bearing.TryGetSupport(point,out var found),Is.True);Assert.That(found.LocalIndex,Is.EqualTo(42));
            Assert.That(bearing.SupportsBase(point+Vector3.up*.2f),Is.False,"An arch below with a vertical gap cannot replace a support.");
            Assert.That(bearing.SupportsBase(point+(arch.Rotation*Vector3.forward)*.6f),Is.False,"A different radial row is not connected.");
            Assert.That(bearing.SupportsBase(arch.ObjectToWorld.MultiplyPoint3x4(new Vector3(.98f,1.0f,0))),Is.False,"The opening or inset glazing is not a bearing beam.");
            Assert.That(new TowerArchSupport(Array.Empty<ChunkAssetData>(),mesh.bounds).SupportsBase(point),Is.False);
            var go=new GameObject("Actual arch beam contact");
            try
            {
                go.transform.SetPositionAndRotation(arch.Position,arch.Rotation);go.transform.localScale=arch.Scale;
                var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=mesh;Physics.SyncTransforms();
                Assert.That(collider.Raycast(new Ray(point+Vector3.up*.06f,Vector3.down),out var hit,.14f),Is.True,
                    "The imported arch must have real solid geometry at the pillar bearing point.");
                Assert.That(Mathf.Abs(hit.point.y-point.y),Is.LessThan(TowerArchSupport.ContactTolerance));
            }
            finally {UnityEngine.Object.DestroyImmediate(go);}
        }
        [Test]
        public void UpperFloors_EndFlushWithTheArchFaceBelow()
        {
            var go = new GameObject("gen"); try {
                var g = go.AddComponent<TowerGenerator>(); g.radius = 96; g.decreaseAmount = 3;
                Assert.That(g.GetFloorOuterRadius(0), Is.EqualTo(g.GetWallRadius(0) + g.PillarSupportRadius).Within(1e-5f));
                Assert.That(g.GetFloorOuterRadius(1), Is.EqualTo(g.GetWallRadius(0) + g.ArchOuterHalfDepth).Within(1e-5f));
                Assert.That(g.GetFloorOuterRadius(3), Is.EqualTo(g.GetWallRadius(2) + g.ArchOuterHalfDepth).Within(1e-5f));
                Assert.That(g.GetFloorOuterRadius(2), Is.EqualTo(g.GetWallRadius(1) + g.ArchOuterHalfDepth).Within(1e-5f));
                Assert.That(g.GetFloorOuterRadius(2), Is.LessThan(g.GetWallRadius(1) + g.PillarSupportRadius));
            } finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TrimOuter_ClipsOverhangKeepsAngleAndDropsOuterOnlyRows()
        {
            var sector = new TowerFloorLayout.Sector(Vector2.zero, 2, 30, .3f, .1f, 4, 1);
            Assert.That(TowerFloorLayout.TryTrimOuter(sector.Position, sector.Scale, Vector2.zero, 28, out var p, out var s), Is.True);
            Assert.That(new Vector2(p.x, p.z).magnitude, Is.EqualTo(28).Within(1e-4f));
            Assert.That(Mathf.Atan2(p.z, p.x), Is.EqualTo(.3f).Within(1e-4f));
            Assert.That(s.x * TowerFloorLayout.TemplateSize, Is.EqualTo(2).Within(1e-4f));
            Assert.That(s.z * TowerFloorLayout.TemplateSize, Is.EqualTo(28 * .1f).Within(1e-4f));
            Assert.That(TowerFloorLayout.TryTrimOuter(sector.Position, sector.Scale, Vector2.zero, 26, out _, out _), Is.False);
            Assert.That(TowerFloorLayout.TryTrimOuter(sector.Position, sector.Scale, Vector2.zero, 31, out p, out s), Is.True);
            Assert.That(p, Is.EqualTo(sector.Position));
        }

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
                var arches=new TowerArchSupport(assets.Where(a=>a.AssetType==TowerAssetType.Arch).Select(a=>new ChunkAssetData(0,a.AssetType,a.transform.position,a.transform.rotation,a.transform.lossyScale)),
                    generator.archObject.GetComponent<Building>().GetStageMesh(10).bounds);
                foreach(var pad in assets.Where(a=>a.name=="Pillar support deck"))
                {
                    Vector2 delta=new Vector2(pad.transform.position.x,pad.transform.position.z)-generator.center;float radius=delta.magnitude;
                    float angle=Mathf.Atan2(delta.y,delta.x)+pad.transform.lossyScale.z*4/radius*.5f;
                    var basePoint=new Vector3(generator.center.x+Mathf.Cos(angle)*(radius-generator.PillarSupportRadius),pad.transform.position.y-.2f*pad.transform.lossyScale.y,generator.center.y+Mathf.Sin(angle)*(radius-generator.PillarSupportRadius));
                    Assert.That(arches.SupportsBase(basePoint),Is.False,"Regeneration must not restore an arch-supported pad.");
                }
                var supports = assets.Where(a => a.AssetType.IsFloor()).Select(a => new ChunkAssetData(0, TowerAssetType.Floor, a.transform.position, a.transform.rotation, a.transform.lossyScale))
                    .GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / 6)).ToDictionary(g => g.Key, g => new TowerFloorSupport(g, generator.center));
                foreach (var pillar in assets.Where(a => a.AssetType == TowerAssetType.Pillar))
                {
                    int floor = Mathf.FloorToInt((pillar.transform.position.y + .001f) / 6);
                    Assert.That(supports[floor].SupportsDisk(new Vector2(pillar.transform.position.x, pillar.transform.position.z), generator.PillarSupportRadius - .05f, out _) || arches.SupportsBase(pillar.transform.position), Is.True);
                    if (supports.TryGetValue(floor + 1, out var ceiling) && Mathf.Abs(generator.GetWallRadius(floor+1)-generator.GetWallRadius(floor))<.01f)
                        Assert.That(ceiling.SupportsDisk(new Vector2(pillar.transform.position.x, pillar.transform.position.z), generator.PillarSupportRadius - .05f, out _) ||
                            arches.SupportsBase(pillar.transform.position+Vector3.up*generator.heighIncrease), Is.True, "The next pillar needs either deck contact or a real arch beneath it.");
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
                var ledges=all.Where(a=>a.AssetType==TowerAssetType.Pillar).GroupBy(a=>Mathf.FloorToInt(a.Position.y/g.heighIncrease))
                    .ToDictionary(group=>group.Key,group=>new TowerPillarLedge(group.Select(p=>p.Position),g.center,g.PillarSupportRadius));
                var arches=new TowerArchSupport(all,g.archObject.GetComponent<Building>().GetStageMesh(10).bounds);
                var supports = all.Where(a => a.AssetType.IsFloor()).GroupBy(a => Mathf.FloorToInt((a.Position.y + .001f) / g.heighIncrease))
                    .ToDictionary(group => group.Key, group => new TowerFloorSupport(group, g.center));
                int checkedPillars = 0,archOnly=0;
                foreach (var pillar in all.Where(a => a.AssetType == TowerAssetType.Pillar))
                {
                    int floor = Mathf.FloorToInt((pillar.Position.y + .001f) / g.heighIncrease);
                    bool deck=supports[floor].SupportsDisk(new Vector2(pillar.Position.x,pillar.Position.z),g.PillarSupportRadius-.05f,out float top);
                    Assert.That(deck || arches.SupportsBase(pillar.Position),Is.True,$"Unsupported pillar on floor {floor}: {pillar.Position}");
                    if(deck)Assert.That(top-.2f,Is.EqualTo(pillar.Position.y).Within(.002f));else archOnly++;
                    checkedPillars++;
                    if (supports.TryGetValue(floor + 1, out var ceiling) && Mathf.Abs(g.GetWallRadius(floor+1)-g.GetWallRadius(floor))<.01f)
                    {
                        bool ceilingDeck=ceiling.SupportsDisk(new Vector2(pillar.Position.x,pillar.Position.z),g.PillarSupportRadius-.05f,out float ceilingTop);
                        Assert.That(ceilingDeck || arches.SupportsBase(pillar.Position+Vector3.up*g.heighIncrease),Is.True,$"Missing deck or arch above floor {floor}");
                        if(ceilingDeck)Assert.That(ceilingTop-.2f,Is.EqualTo((floor+1)*g.heighIncrease).Within(.002f));
                    }
                }
                Assert.That(checkedPillars, Is.GreaterThan(5000)); Assert.That(scatter.Placements.Count, Is.GreaterThan(100));
                Assert.That(archOnly,Is.GreaterThan(1000),"Arches should replace redundant pads on the saved tower.");
                foreach(var group in all.Where(a=>a.AssetType.IsFloor()).GroupBy(a=>Mathf.FloorToInt(a.Position.y/g.heighIncrease)))
                foreach(var pad in group.Where(a=>TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,g.GetWallRadius(group.Key),g.GetWallRadius(Mathf.Max(0,group.Key-1))) || ledges[group.Key].IsConnectedSupport(a)))
                {
                    Vector2 delta=new Vector2(pad.Position.x,pad.Position.z)-g.center;float radius=delta.magnitude;
                    float angle=Mathf.Atan2(delta.y,delta.x)+pad.Scale.z*4/radius*.5f;
                    var basePoint=new Vector3(g.center.x+Mathf.Cos(angle)*(radius-g.PillarSupportRadius),pad.Position.y-.2f*pad.Scale.y,g.center.y+Mathf.Sin(angle)*(radius-g.PillarSupportRadius));
                    Assert.That(arches.SupportsBase(basePoint),Is.False,"Do not retain a separate pad where an arch already bears the pillar.");
                }
                foreach (var bush in scatter.Placements)
                {
                    Assert.That(supports[bush.floor].SupportsDisk(new Vector2(bush.position.x, bush.position.z), bush.footprint, out float top), Is.True, $"Unsupported bush: {bush.position}");
                    float bottom = scatter.BushPrefabs[bush.variant].GetComponent<MeshFilter>().sharedMesh.bounds.min.y;
                    Assert.That(bush.position.y + bottom * bush.scale.y, Is.EqualTo(top - TowerBushScatter.RootEmbedDepth).Within(.002f));
                    Assert.That(Vector2.Distance(new Vector2(bush.position.x, bush.position.z), g.center) - bush.footprint, Is.GreaterThan(bush.wallRadius));
                }
                for (int floor = 1; floor < g.levels; floor++)
                    Assert.That(g.GetFloorOuterRadius(floor), Is.EqualTo(g.GetWallRadius(floor - 1) + g.ArchOuterHalfDepth).Within(.001f));
                float deckThickness = g.floorTileObject.transform.localScale.y * TowerFloorLayout.DeckThicknessStretch;
                Assert.That(all.Where(a => a.AssetType == TowerAssetType.Floor).All(a => Mathf.Abs(a.Scale.y - deckThickness) < .0001f), "White deck tiles overlap the arch tops.");
                Assert.That(all.Where(a => a.AssetType == TowerAssetType.GlassWaterFloor).All(a => Mathf.Abs(a.Scale.y - g.glassWaterFloorObject.transform.localScale.y) < .0001f), "GlassWater keeps its own thickness.");
                float tileBottom = g.floorTileObject.GetComponent<Building>().GetStageMesh(10).bounds.min.y * deckThickness;
                Assert.That(.2f + tileBottom, Is.LessThan(-.04f), "Tile underside sinks at least 4 cm below the arch top (floor base + 0.2 m).");
                foreach (var tile in all.Where(a => a.AssetType.IsFloor()))
                {
                    int floor = Mathf.FloorToInt((tile.Position.y + .001f) / g.heighIncrease);
                    float outer = Vector2.Distance(new Vector2(tile.Position.x, tile.Position.z), g.center);
                    Assert.That(outer, Is.LessThanOrEqualTo(g.GetFloorOuterRadius(floor) + .01f), $"Floor {floor} tile overhangs the arch below at {tile.Position}.");
                }
                int connected=0;
                foreach(var group in all.Where(a=>a.AssetType.IsFloor()).GroupBy(a=>Mathf.FloorToInt(a.Position.y/g.heighIncrease)))
                {
                    float wall=g.GetWallRadius(group.Key),previous=g.GetWallRadius(Mathf.Max(0,group.Key-1));
                    if(wall>=previous-.01f) continue;
                    var mainland=new TowerFloorSupport(group.Where(a=>!TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,wall,previous) && !ledges[group.Key].IsConnectedSupport(a)),g.center);
                    Assert.That(group.Any(a=>TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,previous)),Is.False,"Outer cap islands must be removed.");
                    foreach(var bridge in group.Where(a=>TowerFloorSupport.IsPillarSupport(a,g.center,g.PillarSupportRadius,wall) || ledges[group.Key].IsConnectedSupport(a)))
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
                    var ordered=all.Where(a=>a.AssetType==TowerAssetType.Pillar&&Mathf.FloorToInt(a.Position.y/g.heighIncrease)==group.Key)
                        .OrderBy(a=>Mathf.Repeat(Mathf.Atan2(a.Position.z-g.center.y,a.Position.x-g.center.x),Mathf.PI*2)).ToArray();
                    for(int i=0;i<ordered.Length;i++)
                    {
                        var p=ordered[i].Position;var q=ordered[(i+1)%ordered.Length].Position;
                        bool touchesLedge=group.Any(a=>ledges[group.Key].IsConnectedSupport(a) && (ledges[group.Key].ContainsPillar(a,new Vector2(p.x,p.z)) || ledges[group.Key].ContainsPillar(a,new Vector2(q.x,q.z))));
                        if(!touchesLedge)continue;
                        float start=Mathf.Atan2(p.z-g.center.y,p.x-g.center.x),gap=Mathf.Repeat(Mathf.Atan2(q.z-g.center.y,q.x-g.center.x)-start,Mathf.PI*2);
                        for(int sample=0;sample<=16;sample++)
                        {
                            float angle=start+gap*sample/16;
                            Assert.That(supports[group.Key].TryGetHeight(g.center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*wall,out _),Is.True,$"Disconnected neighbouring pillar ledge on floor {group.Key}, angle {angle}.");
                        }
                    }
                    var stepMesh=g.stairStepObject.GetComponent<Building>().GetStageMesh(10);
                    foreach(var step in all.Where(a=>a.AssetType==TowerAssetType.Stair&&a.Position.y<group.Key*g.heighIncrease&&a.Position.y>group.Key*g.heighIncrease-1.8f))
                    {
                        var p=step.ObjectToWorld.MultiplyPoint3x4(stepMesh.bounds.center);
                        Assert.That(Vector2.Distance(new Vector2(p.x,p.z),g.center)-.3f,Is.GreaterThan(wall+g.PillarSupportRadius),"The connecting ledge must stay clear of the incoming stair centreline and player footprint.");
                    }
                }
                Assert.That(connected,Is.GreaterThan(1000));
            }
            finally { if(opened) EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
