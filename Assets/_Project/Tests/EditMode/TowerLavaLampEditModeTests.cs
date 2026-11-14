using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.World.Tests
{
    /// <summary>
    /// Lava lamps hang inside the tower on one pillar, skip three, around every ring, with random lamps outside free
    /// pillars; never two on one pillar or on neighbouring pillars. Each lamp is a buildable detail asset made from iron
    /// ingots: it lights up once fully built (without lamp assets, a lamp shows with its pillar), and every lamp in view
    /// (up to the budget) lights its wall with one shared caustics cookie. Lights never switch at full brightness:
    /// they fade in and out, the lit radius shrinks to the budget, and lamps behind a deck take no light.
    /// </summary>
    public sealed class TowerLavaLampEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab";
        private const string CookiePath = "Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat";
        private const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
        private const string BuildablePath = "Assets/_Project/Prefabs/Buildings/Detail_LavaLamp.prefab";
        private const int RingPillars = 10;
        private const float RingRadius = 10, MountHeight = 3.6f, PillarRadius = .38f;

        private GameObject root, lampObject, stairPrefab, cameraObject;
        private ChunkManager manager;
        private TowerLavaLamps lamps;
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab, Is.Not.Null, "Run Tower of Babel/Environment/Apply tower lava lamps.");
            root = new GameObject("Lava lamp test");
            root.SetActive(false);
            manager = root.AddComponent<ChunkManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (lampObject != null) Object.DestroyImmediate(lampObject);
            if (cameraObject != null) Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(root);
            if (stairPrefab != null) Object.DestroyImmediate(stairPrefab);
        }

        // Exterior lamps are off unless a test asks for them, so the inside pattern can be checked on its own.
        private void Build(System.Action<Transform> extra = null, float exterior = 0, int ringPillars = RingPillars, int seed = 1)
        {
            var tower = new GameObject("Authoring");
            tower.transform.SetParent(root.transform);
            for (int floor = 0; floor < 2; floor++)
                for (int i = 0; i < ringPillars; i++)
                {
                    float a = i * Mathf.PI * 2 / ringPillars;
                    AddAsset(tower.transform, new Vector3(Mathf.Cos(a) * RingRadius, floor * 6, Mathf.Sin(a) * RingRadius), TowerAssetType.Pillar);
                }
            extra?.Invoke(tower.transform);
            Field("towerRoot").SetValue(manager, tower);
            manager.CacheChunks();
            lampObject = new GameObject("Tower lava lamps");
            lamps = lampObject.AddComponent<TowerLavaLamps>();
            lamps.Configure(manager, prefab, MountHeight, PillarRadius);
            lamps.SetExterior(exterior, seed);
        }

        /// <summary>No pillar carries two lamps and no two lamps hang on neighbouring pillars of a ring.</summary>
        private static void AssertSpacing(IEnumerable<TowerLavaLamps.Placement> placements, System.Func<int, int> ringSize)
        {
            foreach (var floor in placements.GroupBy(p => p.floor))
            {
                int n = ringSize(floor.Key);
                var used = floor.Select(p => p.ringIndex).ToList();
                Assert.That(used.Distinct().Count(), Is.EqualTo(used.Count), $"Floor {floor.Key}: one lamp per pillar, inside or outside.");
                var set = new HashSet<int>(used);
                foreach (int i in used)
                    Assert.That(set.Contains((i + 1) % n), Is.False, $"Floor {floor.Key}: lamps on pillars {i} and {(i + 1) % n} are neighbours.");
            }
        }

        private static Vector3 PillarAt(int index, int floor = 0)
        {
            float a = index * Mathf.PI * 2 / RingPillars;
            return new Vector3(Mathf.Cos(a) * RingRadius, floor * 6, Mathf.Sin(a) * RingRadius);
        }

        /// <summary>Completes the floor's structure; buildable lamps (detail) stay as they are.</summary>
        private void CompletePillars(int floor)
        {
            var pending = manager.CachedChunks.Where(c => c.Key.FloorIndex == floor)
                .SelectMany(c => c.Assets.Where(a => !a.AssetType.IsDetail()).Select(a => (c.Key, a.LocalIndex))).ToArray();
            foreach (var (key, index) in pending) manager.SetAssetStage(key, index, ChunkAssetData.CompletedStage);
        }

        /// <summary>Adds a buildable lamp asset at every spot, as LavaLampAuthoring does for the Tower scene.</summary>
        private List<ChunkAssetData> AddLampAssets()
        {
            if (manager.AssetPrefabs == null) Field("assetPrefabs").SetValue(manager, new TowerAssetPrefabSet());
            manager.AssetPrefabs.SetPrefab(TowerAssetType.LavaLamp, AssetDatabase.LoadAssetAtPath<GameObject>(BuildablePath));
            var assets = lamps.BuildLampAssets();
            manager.ReplaceCachedAssets(type => type == TowerAssetType.LavaLamp, assets);
            lamps.Rebuild();
            return assets;
        }

        [Test]
        public void LampAssets_TakeOverThePlacements_OneAtEverySpot()
        {
            Build(exterior: 1);
            var spots = lamps.Placements.ToArray();
            Assert.That(lamps.UsesLampAssets, Is.False);
            var assets = AddLampAssets();
            Assert.That(assets.Count, Is.EqualTo(spots.Length));
            Assert.That(assets.All(a => a.AssetType == TowerAssetType.LavaLamp && a.Scale == Vector3.one));
            Assert.That(lamps.UsesLampAssets, Is.True);
            Assert.That(lamps.Placements.Count, Is.EqualTo(spots.Length));
            foreach (var placement in lamps.Placements)
            {
                Assert.That(placement.lamp, Is.GreaterThanOrEqualTo(0));
                Assert.That(manager.TryGetAsset(placement.lampKey, placement.lamp, out var lamp) && lamp.AssetType == TowerAssetType.LavaLamp);
                var spot = spots.Single(s => Vector3.Distance(s.Position, placement.Position) < .001f);
                Assert.That(Quaternion.Angle(spot.mount.rotation, placement.mount.rotation), Is.LessThan(.01f));
                Assert.That((placement.ringIndex, placement.exterior, placement.floor), Is.EqualTo((spot.ringIndex, spot.exterior, spot.floor)),
                    "Each lamp keeps its pillar's ring position and face.");
                Assert.That(lamp.AssetType.Phase(), Is.EqualTo(TowerConstructionPhase.Detail));
            }
            AssertSpacing(lamps.Placements, _ => RingPillars);
        }

        [Test]
        public void BuildableLamp_LightsUpOnlyOnceBuilt_AfterItsFloorStructure()
        {
            Build();
            AddLampAssets();
            var first = lamps.Placements[0];
            Assert.That(manager.CanConstructAsset(first.lampKey, first.lamp), Is.False, "Lamps are detail: they wait for the floor's structure.");
            CompletePillars(0);
            Assert.That(manager.CanConstructAsset(first.lampKey, first.lamp), Is.True);
            Assert.That(lamps.IsShown(0), Is.False, "A finished pillar no longer lights an unbuilt lamp.");
            Assert.That(lamps.VisibleLampCount, Is.Zero);
            manager.SetAssetStage(first.lampKey, first.lamp, 6);
            Assert.That(lamps.IsShown(0), Is.False, "A half-built sconce has no glass or light yet.");
            manager.SetAssetStage(first.lampKey, first.lamp, ChunkAssetData.CompletedStage);
            Assert.That(lamps.IsShown(0), Is.True);
            Assert.That(lamps.VisibleLampCount, Is.EqualTo(1));
            lamps.SelectLights(first.Position + Vector3.up);
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(1), "Only the built lamp carries a light.");

            manager.SetAssetStage(first.key, first.pillar, 4);
            Assert.That(lamps.IsShown(0), Is.False, "Rolling the structure back withdraws the floor's detail, lamps included.");
            Assert.That(lamps.VisibleLampCount, Is.Zero);
            manager.SetAssetStage(first.key, first.pillar, ChunkAssetData.CompletedStage);
            Assert.That(lamps.IsShown(0), Is.True, "The built lamp returns with its floor's detail.");
        }

        [Test]
        public void GeneratorPlacement_MatchesTheCachedPlacement()
        {
            Build(exterior: .5f, ringPillars: 24, seed: 2);
            var assets = new List<ChunkAssetData>();
            foreach (var chunk in manager.CachedChunks) assets.AddRange(chunk.Assets);
            // The generator has no chunks yet: it groups floors by height, as for the vines.
            var generated = lamps.BuildLampAssets(assets, a => Mathf.FloorToInt((a.Position.y + .001f) / 6), manager.FloorCenter, new Bounds(Vector3.zero, Vector3.zero));
            Assert.That(generated.Select(a => a.Position), Is.EquivalentTo(lamps.Placements.Select(p => p.Position)));
        }

        [Test]
        public void BuildablePrefab_AssemblesTheChromeSconceFromIronIngots()
        {
            var buildable = AssetDatabase.LoadAssetAtPath<GameObject>(BuildablePath);
            Assert.That(buildable, Is.Not.Null, "Run Tower of Babel/Environment/Apply tower lava lamps.");
            Assert.That(buildable.GetComponent<TowerAsset>().AssetType, Is.EqualTo(TowerAssetType.LavaLamp));
            var building = buildable.GetComponent<TowerOfBabel.Buildings.Building>();
            Assert.That(building.ResourceType, Is.EqualTo(TowerOfBabel.Resources.ResourceType.IronIngot));
            Assert.That(building.ResourceCostPerStage, Is.EqualTo(1));
            Assert.That(buildable.GetComponentsInChildren<Collider>(true).Length, Is.EqualTo(1));
            Assert.That(buildable.GetComponent<TowerOfBabel.Outline>(), Is.Not.Null, "Highlighted when aimed at or scanned.");
            var chrome = prefab.GetComponentsInChildren<MeshRenderer>(true).Single(r => r.sharedMaterial.shader.name != "TowerOfBabel/Lighting/Lava Lamp Fluid");
            Assert.That(building.GetStageMesh(10), Is.SameAs(chrome.GetComponent<MeshFilter>().sharedMesh), "The finished stage is the lamp's own sconce.");
            Assert.That(buildable.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(chrome.sharedMaterial));
            int previous = 0;
            for (byte stage = 0; stage <= 10; stage++)
            {
                int triangles = building.GetStageMesh(stage).triangles.Length / 3;
                Assert.That(triangles, Is.GreaterThan(previous), $"Stage {stage} adds a part.");
                previous = triangles;
            }
            var box = buildable.GetComponent<BoxCollider>();
            var lamp = building.GetStageMesh(10).bounds;
            var area = new Bounds(box.center, box.size);
            Assert.That(area.Contains(lamp.min) && area.Contains(lamp.max), "Aimable over the whole hanging lamp.");
        }

        [Test]
        public void OneLamp_ThenThreeBarePillars_AroundEachRing()
        {
            Build();
            var ground = lamps.Placements.Where(p => p.floor == 0).ToArray();
            Assert.That(ground.Length, Is.EqualTo(3), "Pillars 0, 4 and 8 of ten carry a lamp.");
            Assert.That(lamps.Placements.Any(p => p.exterior), Is.False);
            Assert.That(lamps.Placements.Count(p => p.floor == 1), Is.EqualTo(3));
            for (int n = 0; n < ground.Length; n++)
            {
                var pillar = PillarAt(n * TowerLavaLamps.PillarInterval);
                var inward = -new Vector3(pillar.x, 0, pillar.z).normalized;
                Assert.That(lamps.Interior, Is.True);
                Assert.That(Vector3.Distance(ground[n].Position, pillar + Vector3.up * MountHeight + inward * PillarRadius), Is.LessThan(.001f));
                Assert.That(Vector3.Dot(ground[n].mount.rotation * Vector3.forward, inward), Is.GreaterThan(.999f), "The sconce reaches into the tower.");
                Assert.That(manager.TryGetAsset(ground[n].key, ground[n].pillar, out var asset) && asset.AssetType == TowerAssetType.Pillar);
                Assert.That(Vector3.Distance(asset.Position, pillar), Is.LessThan(.001f));
            }
        }

        [Test]
        public void Lamp_ShowsOnlyOnceItsPillarIsBuilt_OnAVisibleFloor()
        {
            Build();
            Assert.That(lamps.VisibleLampCount, Is.Zero);
            var first = lamps.Placements[0];
            manager.SetAssetStage(first.key, first.pillar, ChunkAssetData.CompletedStage);
            Assert.That(lamps.IsShown(0), Is.True);
            Assert.That(lamps.VisibleLampCount, Is.EqualTo(1));
            manager.SetAssetStage(first.key, first.pillar, 4);
            Assert.That(lamps.VisibleLampCount, Is.Zero, "Rolling the pillar back takes its lamp down.");

            int upper = lamps.Placements.ToList().FindIndex(p => p.floor == 1);
            var above = lamps.Placements[upper];
            manager.SetAssetStage(above.key, above.pillar, ChunkAssetData.CompletedStage);
            Assert.That(manager.IsFloorVisible(1), Is.False);
            Assert.That(lamps.IsShown(upper), Is.False, "Floors above the construction floor stay hidden.");
            CompletePillars(0);
            Assert.That(manager.IsFloorVisible(1), Is.True);
            Assert.That(lamps.IsShown(upper), Is.True);
            Assert.That(lamps.VisibleLampCount, Is.EqualTo(4));
        }

        [Test]
        public void StairAcrossALamp_LeavesThatPillarBare_AndKeepsThePattern()
        {
            stairPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var set = new TowerAssetPrefabSet(); set.SetPrefab(TowerAssetType.Stair, stairPrefab);
            Field("assetPrefabs").SetValue(manager, set);
            var pillar = PillarAt(4);
            var inward = -new Vector3(pillar.x, 0, pillar.z).normalized;
            Build(t => AddAsset(t, pillar + inward * .9f + Vector3.up * 2.9f, TowerAssetType.Stair));
            var ground = lamps.Placements.Where(p => p.floor == 0).Select(p => p.Position).ToArray();
            Assert.That(ground.Length, Is.EqualTo(2));
            Assert.That(ground.All(p => Vector3.Distance(new Vector3(p.x, 0, p.z), pillar) > 2), "The lamp that would hang in the stair is left out.");
            Assert.That(ground.Any(p => Vector3.Distance(new Vector3(p.x, 0, p.z), PillarAt(8)) < 1), "The next lamp still hangs four pillars on.");
        }

        [Test]
        public void StairBelowALamp_KeepsIt_UnlessHeadroomIsRequested()
        {
            stairPrefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var set = new TowerAssetPrefabSet(); set.SetPrefab(TowerAssetType.Stair, stairPrefab);
            Field("assetPrefabs").SetValue(manager, set);
            var pillar = PillarAt(4);
            var inward = -new Vector3(pillar.x, 0, pillar.z).normalized;
            Build(t => AddAsset(t, pillar + inward * .9f + Vector3.up * 1f, TowerAssetType.Stair));
            Assert.That(lamps.Placements.Count(p => p.floor == 0), Is.EqualTo(3), "The step passes under the lamp without touching it.");
            typeof(TowerLavaLamps).GetField("stairHeadroom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(lamps, 2.05f);
            lamps.Rebuild();
            Assert.That(lamps.Placements.Count(p => p.floor == 0), Is.EqualTo(2), "With headroom, someone climbing would walk into it.");
        }

        [Test]
        public void ProjectionLights_FollowTheNearestBuiltLamps_AndDimByDay()
        {
            Build();
            CompletePillars(0);
            var view = lamps.Placements[0].Position + Vector3.up;
            lamps.SelectLights(view);
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(3), "Three built ground lamps; the rest of the budget stays idle.");
            var spot = prefab.GetComponent<AeroProjectionLamp>().projection;
            var active = lamps.Lights.Where(l => l.gameObject.activeInHierarchy && l.type == LightType.Spot).ToArray();
            Assert.That(active.Length, Is.EqualTo(3));
            foreach (var light in active)
            {
                Assert.That(light.transform.forward.y, Is.GreaterThan(.9f), "The water projection points upward.");
                Assert.That(lamps.Placements.Any(p => Vector3.Distance(light.transform.root.position, p.Position) < .001f ||
                    Vector3.Distance(light.transform.parent.position, p.Position) < .001f));
            }
            lamps.ApplyDaylight(0);
            Assert.That(active[0].intensity, Is.EqualTo(spot.intensity).Within(1e-4f), "Full strength at night.");
            lamps.ApplyDaylight(1);
            Assert.That(active[0].intensity, Is.LessThan(spot.intensity * .5f).And.GreaterThan(0), "Dimmer but still on during the day.");

            lamps.SelectLights(view + Vector3.up * 200);
            Assert.That(lamps.ActiveLightCount, Is.Zero, "No lights beyond the light distance.");
        }

        [Test]
        public void ExteriorLamps_OnlyOnFreePillars_NeverNeighbours_NeverTwoOnAPillar()
        {
            Build(exterior: 1);
            var ground = lamps.Placements.Where(p => p.floor == 0).ToArray();
            Assert.That(ground.Where(p => !p.exterior).Select(p => p.ringIndex), Is.EquivalentTo(new[] { 0, 4, 8 }), "The inside pattern is unchanged.");
            // Pillar 9 sits between lamps 8 and 0, so only the middle pillars of the two full gaps are free.
            Assert.That(ground.Where(p => p.exterior).Select(p => p.ringIndex), Is.EquivalentTo(new[] { 2, 6 }));
            AssertSpacing(lamps.Placements, _ => RingPillars);
            foreach (var p in lamps.Placements.Where(p => p.exterior))
            {
                var pillar = PillarAt(p.ringIndex, p.floor);
                var outward = new Vector3(pillar.x, 0, pillar.z).normalized;
                Assert.That(Vector3.Dot(p.mount.rotation * Vector3.forward, outward), Is.GreaterThan(.999f), "Exterior sconces face out of the tower.");
                Assert.That(Vector3.Distance(p.Position, pillar + Vector3.up * MountHeight + outward * PillarRadius), Is.LessThan(.001f));
            }
        }

        [Test]
        public void ExteriorLamps_AreRandomButStable_AndFollowTheChance()
        {
            Build(exterior: .5f, ringPillars: 40, seed: 3);
            var first = lamps.Placements.Where(p => p.exterior).Select(p => (p.floor, p.ringIndex)).ToArray();
            lamps.Rebuild();
            Assert.That(lamps.Placements.Where(p => p.exterior).Select(p => (p.floor, p.ringIndex)), Is.EqualTo(first), "Same lamps every rebuild.");
            // Ten free middle pillars per ring of forty; about half get a lamp.
            Assert.That(first.Length, Is.InRange(4, 16));
            AssertSpacing(lamps.Placements, _ => 40);
            lamps.SetExterior(.5f, 4);
            var other = lamps.Placements.Where(p => p.exterior).Select(p => (p.floor, p.ringIndex)).ToArray();
            Assert.That(other, Is.Not.EqualTo(first), "Another seed scatters them differently.");
            AssertSpacing(lamps.Placements, _ => 40);
            lamps.SetExterior(0);
            Assert.That(lamps.Placements.Any(p => p.exterior), Is.False);
        }

        [Test]
        public void RingThatDoesNotDivideByFour_DropsTheLampNextToTheFirst()
        {
            Build(ringPillars: 9);
            Assert.That(lamps.Placements.Where(p => p.floor == 0).Select(p => p.ringIndex), Is.EquivalentTo(new[] { 0, 4 }),
                "Pillar 8 is next to pillar 0 around the ring.");
            AssertSpacing(lamps.Placements, _ => 9);
        }

        [Test]
        public void EveryLampInView_GetsALight_SharingOneSharpCookie()
        {
            Build(exterior: 1, ringPillars: 40);
            CompletePillars(0);
            lamps.LightBudget = 24;
            int shown = Enumerable.Range(0, lamps.Placements.Count).Count(lamps.IsShown);
            Assert.That(shown, Is.GreaterThan(lamps.MaxLights / 2));
            cameraObject = new GameObject("Lava lamp view");
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 70; camera.farClipPlane = 400;
            // From the middle of the ring looking along +X: only lamps ahead are in view.
            camera.transform.SetPositionAndRotation(new Vector3(0, 3, 0), Quaternion.LookRotation(Vector3.right));
            var planes = GeometryUtility.CalculateFrustumPlanes(camera);
            lamps.SelectLights(camera.transform.position, planes);
            var lit = lamps.Lights.Where(l => l.gameObject.activeInHierarchy && l.type == LightType.Spot).ToArray();
            // Lamps well inside the view cone, straight ahead of the camera, must all be lit.
            var ahead = Enumerable.Range(0, lamps.Placements.Count).Where(i => lamps.IsShown(i) &&
                Mathf.Abs(Mathf.Atan2(lamps.Placements[i].Position.z, lamps.Placements[i].Position.x)) < 20 * Mathf.Deg2Rad).ToArray();
            Assert.That(ahead.Length, Is.GreaterThan(2));
            foreach (int i in ahead)
                Assert.That(lit.Any(l => Vector3.Distance(l.transform.parent.position, lamps.Placements[i].Position) < .001f), $"Lamp {i} is in sight but dark.");
            Assert.That(lit.Length, Is.LessThanOrEqualTo(lamps.MaxLights));
            Assert.That(lit.Length, Is.GreaterThan(AeroProjectionLamp.MaximumActiveLamps), "Not just the four nearest.");
            Assert.That(lit.All(l => l.transform.position.x > 0), "Lamps behind the camera stay dark.");
            Assert.That(lit.All(l => l.enabled), "Rigs are not switched off by the per-lamp projection budget.");
            Assert.That(lamps.GetComponentsInChildren<AeroProjectionLamp>(true), Is.Empty);

            Assert.That(lamps.SharedCookie, Is.Not.Null);
            Assert.That(lamps.SharedCookie.width, Is.GreaterThanOrEqualTo(512), "Sharp enough for a wall-sized pattern.");
            Assert.That(lit.All(l => l.cookie == lamps.SharedCookie), "One cookie for every lamp.");
            Assert.That(lit.Select(l => Quaternion.Angle(l.transform.localRotation, lit[0].transform.localRotation)).Count(a => a > 5), Is.GreaterThan(lit.Length / 2),
                "Each lamp turns the pattern differently.");
            Assert.That(lit.Count(l => l.shadows != LightShadows.None), Is.LessThanOrEqualTo(1), "At most the nearest lamp casts shadows.");

            // Turning around swaps the lit set to the other side.
            camera.transform.rotation = Quaternion.LookRotation(Vector3.left);
            lamps.SelectLights(camera.transform.position, GeometryUtility.CalculateFrustumPlanes(camera));
            Assert.That(lamps.Lights.Where(l => l.gameObject.activeInHierarchy && l.type == LightType.Spot).All(l => l.transform.position.x < 0));
        }

        [Test]
        public void LightsFadeOutTowardTheEdgeOfTheirReach()
        {
            Build();
            CompletePillars(0);
            var lamp = lamps.Placements[0].Position;
            var away = (new Vector3(lamp.x, 0, lamp.z)).normalized;
            lamps.SelectLights(lamp + away * (lamps.LightReach * .5f));
            lamps.ApplyDaylight(0);
            var spot = lamps.Lights.First(l => l.gameObject.activeInHierarchy && l.type == LightType.Spot &&
                Vector3.Distance(l.transform.parent.position, lamp) < .001f);
            float full = spot.intensity;
            lamps.UpdateFades(lamp + away * (lamps.LightReach * .95f)); lamps.ApplyDaylight(0);
            Assert.That(spot.intensity, Is.LessThan(full * .25f).And.GreaterThan(0), "Nearly faded just inside the reach.");
        }

        private Light[] LitSpots() => lamps.Lights.Where(l => l.gameObject.activeInHierarchy && l.type == LightType.Spot).ToArray();

        private float FullIntensity => prefab.GetComponent<AeroProjectionLamp>().projection.intensity;

        // With 24 lights and a hundred lamps in view, the nearest 24 were lit at full strength and the 25th was dark:
        // every step swapped a light from one lamp to another at full brightness.
        [Test]
        public void MoreLampsThanLights_ShrinkTheLitRadius_SoNoLightSwitchesAtFullBrightness()
        {
            Build(exterior: 1, ringPillars: 40);
            CompletePillars(0);
            lamps.LightBudget = 6;
            int[] shown = Enumerable.Range(0, lamps.Placements.Count).Where(lamps.IsShown).ToArray();
            Assert.That(shown.Length, Is.GreaterThan(12));
            float full = FullIntensity;

            Dictionary<Vector3, float> Lit(Vector3 view)
            {
                lamps.SelectLights(view);
                lamps.ApplyDaylight(0);
                return LitSpots().ToDictionary(l => l.transform.parent.position, l => l.intensity);
            }

            var view = new Vector3(RingRadius - 2, 3, 0);
            var lit = Lit(view);
            Assert.That(lamps.CandidateCount, Is.EqualTo(shown.Length), "Every built lamp is within reach.");
            Assert.That(lit.Count, Is.EqualTo(6), "The budget.");
            float[] distances = shown.Select(i => Vector3.Distance(lamps.Placements[i].Position, view)).OrderBy(d => d).ToArray();
            Assert.That(lamps.EffectiveReach, Is.EqualTo(distances[6]).Within(.001f), "The lit radius ends at the first lamp left without a light.");
            Assert.That(lamps.EffectiveReach, Is.LessThan(lamps.LightReach));
            Assert.That(lit.Values.Max(), Is.EqualTo(full).Within(.001f), "The nearest lamp is fully lit.");
            foreach (var pair in lit)
            {
                float d = Vector3.Distance(pair.Key, view), radius = lamps.EffectiveReach;
                float expected = full * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * .75f, radius, d)));
                Assert.That(pair.Value, Is.EqualTo(expected).Within(.01f), "Lights fade out over the last quarter of the lit radius.");
            }

            // Walk a quarter of the ring: a lamp only ever gains or loses its light while it is all but dark.
            int handed = 0;
            for (float angle = .01f; angle < Mathf.PI * .5f; angle += .01f)
            {
                var next = Lit(new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (RingRadius - 2) + Vector3.up * 3);
                foreach (var pair in lit)
                    if (!next.ContainsKey(pair.Key))
                    {
                        handed++;
                        Assert.That(pair.Value, Is.LessThan(full * .1f), $"The lamp at {pair.Key} lost its light while still bright.");
                    }
                foreach (var pair in next)
                    if (!lit.ContainsKey(pair.Key))
                        Assert.That(pair.Value, Is.LessThan(full * .1f), $"The lamp at {pair.Key} came on bright.");
                lit = next;
            }
            Assert.That(handed, Is.GreaterThan(2), "Lights did move from lamp to lamp along the walk.");

            // With lights to spare the radius is the full reach again.
            lamps.LightBudget = 64;
            Lit(view);
            Assert.That(lamps.EffectiveReach, Is.EqualTo(lamps.LightReach));
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(shown.Length));
        }

        [Test]
        public void InTheGame_LightsFadeInOnALamp_AndFadeOutBeforeTheyMove()
        {
            Build();
            CompletePillars(0);
            float full = FullIntensity;
            cameraObject = new GameObject("Lava lamp view");
            var camera = cameraObject.AddComponent<Camera>();
            // A square picture, 30 degrees to each side: one lamp of the three around the ring at a time.
            camera.fieldOfView = 60; camera.aspect = 1; camera.farClipPlane = 400;
            var eye = new Vector3(0, 3, 0);
            Vector3 first = lamps.Placements.First(p => p.floor == 0 && p.ringIndex == 0).Position;
            Vector3 second = lamps.Placements.First(p => p.floor == 0 && p.ringIndex == 4).Position;
            Light SpotAt(Vector3 lamp) => LitSpots().SingleOrDefault(l => Vector3.Distance(l.transform.parent.position, lamp) < .001f);
            void Look(Vector3 lamp, bool smooth)
            {
                camera.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(new Vector3(lamp.x, 0, lamp.z)));
                lamps.SelectLights(eye, GeometryUtility.CalculateFrustumPlanes(camera), smooth);
                lamps.ApplyDaylight(0);
            }
            void Wait(float seconds) { lamps.Advance(eye, seconds); lamps.ApplyDaylight(0); }

            // Lights to spare: the lamp coming into view fades in while the one that left fades out.
            lamps.LightBudget = 4;
            Look(first, true);
            Assert.That(LitSpots().Length, Is.EqualTo(1), "Only the lamp ahead is in view.");
            Assert.That(SpotAt(first).intensity, Is.Zero, "A light arrives dark.");
            Wait(TowerLavaLamps.FadeSeconds * .5f);
            Assert.That(SpotAt(first).intensity, Is.EqualTo(full * .5f).Within(full * .02f), "Half way in after half the fade time.");
            Wait(TowerLavaLamps.FadeSeconds);
            Assert.That(SpotAt(first).intensity, Is.EqualTo(full).Within(.001f));

            Look(second, true);
            Assert.That(SpotAt(first).intensity, Is.EqualTo(full).Within(.001f), "The lamp that left the view is not switched off.");
            Assert.That(SpotAt(second).intensity, Is.Zero);
            Wait(TowerLavaLamps.FadeSeconds * .5f);
            Assert.That(SpotAt(first).intensity, Is.EqualTo(full * .5f).Within(full * .02f), "It fades out");
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full * .5f).Within(full * .02f), "while the new one fades in.");
            Wait(TowerLavaLamps.FadeSeconds);
            Assert.That(SpotAt(first), Is.Null, "Faded out, the light is handed back.");
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full).Within(.001f));
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(1));

            // Back in view before it is gone: it comes back up from where it was.
            Look(first, true);
            Wait(TowerLavaLamps.FadeSeconds * .5f);
            Look(second, true);
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full * .5f).Within(full * .02f));
            Wait(TowerLavaLamps.FadeSeconds);
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full).Within(.001f));
            Assert.That(SpotAt(first), Is.Null);

            // One light only: it fades out where it is, then appears dark on the lamp that waits.
            lamps.LightBudget = 1;
            Look(second, false);
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full).Within(.001f), "Outside the game the lights are in place at once.");
            Look(first, true);
            Assert.That(SpotAt(second).intensity, Is.EqualTo(full).Within(.001f));
            Assert.That(SpotAt(first), Is.Null, "The only light is still busy.");
            Wait(TowerLavaLamps.FadeSeconds * 1.01f);
            Assert.That(SpotAt(second), Is.Null);
            Assert.That(SpotAt(first), Is.Not.Null, "The light moved once it was dark.");
            Assert.That(SpotAt(first).intensity, Is.Zero);
            Wait(TowerLavaLamps.FadeSeconds);
            Assert.That(SpotAt(first).intensity, Is.EqualTo(full).Within(.001f));
        }

        [Test]
        public void LampsBehindADeck_TakeNoLight()
        {
            Build();
            CompletePillars(0); CompletePillars(1);
            var view = new Vector3(0, 1.5f, 0);
            lamps.SelectLights(view);
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(6), "Three lamps on each of the two floors are within reach.");

            // A solid deck under the upper floor: an 80 m slab around the ring.
            var decks = new FarOccluderSet();
            decks.Configure(false, Vector2.zero);
            decks.SetChunk(new ChunkKey(1, 0, 0), new List<ChunkAssetData>
            {
                new(0, TowerAssetType.Floor, new Vector3(-40, 6, 40), Quaternion.identity, new Vector3(20, 1, 20), ChunkAssetData.CompletedStage)
            }, 1, 0, false);
            decks.Update(0);
            Assert.That(decks.SolidTexelCount, Is.GreaterThan(10000));
            lamps.SelectLights(view, null, false, decks);
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(3), "The lamps above the deck cannot be seen from under it.");
            Assert.That(lamps.CandidateCount, Is.EqualTo(3));
            Assert.That(LitSpots().All(l => l.transform.parent.position.y < 6), "The lit lamps are the ones on the player's floor.");

            // From the upper floor it is the other way round, except that a ground lamp's light reaches up to the deck.
            lamps.SelectLights(new Vector3(0, 7.5f, 0), null, false, decks);
            Assert.That(LitSpots().Count(l => l.transform.parent.position.y > 6), Is.EqualTo(3), "The player's own floor is lit.");
        }

        [Test]
        public void FarLamps_KeepTheirSpotlight_WithoutTheGlowOrShadows()
        {
            Build();
            CompletePillars(0);
            float shadowDistance = new SerializedObject(lamps).FindProperty("shadowDistance").floatValue;
            Assert.That(shadowDistance, Is.LessThan(TowerLavaLamps.GlowReach));
            Vector3 lamp = lamps.Placements.First(p => p.floor == 0 && p.ringIndex == 0).Position;
            var away = new Vector3(lamp.x, 0, lamp.z).normalized;
            Light Spot() => LitSpots().Single(l => Vector3.Distance(l.transform.parent.position, lamp) < .001f);
            Light Glow() => Spot().transform.parent.GetComponentsInChildren<Light>(true).Single(l => l.type == LightType.Point);
            void From(float distance) { lamps.SelectLights(lamp + away * distance); lamps.ApplyDaylight(0); }

            From(3);
            float strength = Spot().shadowStrength;
            Assert.That(Glow().enabled && Glow().intensity > 0, "Close by, the glow under the lamp is lit.");
            Assert.That(Spot().shadows, Is.Not.EqualTo(LightShadows.None), "The nearest lamp casts shadows.");
            Assert.That(LitSpots().Count(l => l.shadows != LightShadows.None), Is.EqualTo(1));

            From(shadowDistance * .9f);
            Assert.That(Spot().shadows, Is.Not.EqualTo(LightShadows.None));
            Assert.That(Spot().shadowStrength, Is.LessThan(strength * .5f).And.GreaterThan(0), "Its shadows fade out with distance instead of switching off.");

            From(shadowDistance * 1.2f);
            Assert.That(Spot().shadows, Is.EqualTo(LightShadows.None));
            Assert.That(Glow().enabled && Glow().intensity > 0);

            From(TowerLavaLamps.GlowReach * 1.5f);
            Assert.That(Spot().intensity, Is.EqualTo(FullIntensity).Within(.001f), "The spotlight is what shows from afar.");
            Assert.That(Glow().enabled, Is.False, "The small glow would only spend a light.");

            From(3);
            Assert.That(Glow().enabled && Glow().intensity > 0, "It comes back when the player does.");
            Assert.That(Spot().shadowStrength, Is.EqualTo(strength).Within(.001f));
        }

        [Test]
        public void LavaLampPrefab_ReusesTheAquaProjection_PointingUp()
        {
            var lamp = prefab.GetComponent<AeroProjectionLamp>();
            Assert.That(lamp, Is.Not.Null);
            Assert.That(lamp.cookieMaterial, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Material>(CookiePath)), "Same animated water cookie as the aqua lamp.");
            Assert.That(lamp.projection.type, Is.EqualTo(LightType.Spot));
            Assert.That(lamp.projection.transform.forward.y, Is.GreaterThan(.9f));
            Assert.That(lamp.projection.transform.forward.z, Is.LessThan(0), "Leans toward the wall it hangs on.");
            Assert.That(prefab.GetComponentsInChildren<Collider>(true), Is.Empty);
            var glow = prefab.GetComponentsInChildren<Light>(true).Single(l => l.type == LightType.Point);
            Assert.That(glow.shadows, Is.EqualTo(LightShadows.None));

            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            var glass = renderers.Single(r => r.sharedMaterial.shader.name == "TowerOfBabel/Lighting/Lava Lamp Fluid");
            var chrome = renderers.Single(r => r != glass);
            Assert.That(glass.sharedMaterial.enableInstancing && chrome.sharedMaterial.enableInstancing, "Both are drawn instanced on the tower.");
            Assert.That(chrome.sharedMaterial.GetFloat("_Metallic"), Is.GreaterThan(.85f));
            var vessel = glass.GetComponent<MeshFilter>().sharedMesh.bounds;
            Assert.That(vessel.size.y, Is.EqualTo(glass.sharedMaterial.GetFloat("_VesselHeight")).Within(.001f), "The ray march follows the glass mesh.");
            Assert.That(vessel.extents.x, Is.EqualTo(glass.sharedMaterial.GetFloat("_RadiusBottom")).Within(.002f));
            // The lamp hangs below the arm, clear of the wall, with the spotlight over its cap.
            Assert.That(glass.transform.localPosition.z, Is.GreaterThan(.4f));
            Assert.That(lamp.projection.transform.localPosition.y, Is.GreaterThan(glass.transform.localPosition.y + vessel.size.y));
            var sconce = chrome.GetComponent<MeshFilter>().sharedMesh;
            Assert.That(sconce.bounds.min.y, Is.LessThan(glass.transform.localPosition.y), "The chrome base sits under the glass.");
            Assert.That(sconce.bounds.max.y, Is.GreaterThan(0), "The torch arm meets the wall plate.");
        }

        [Test]
        public void TowerScene_HangsALampInsideOnEveryFourthPillar()
        {
            var scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                var tower = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TowerLavaLamps>(true)).Single();
                Assert.That(tower.transform.root.name, Is.EqualTo("Lighting & Atmosphere"), "Its light rigs live with the other lights.");
                Assert.That(tower.LampPrefab, Is.EqualTo(prefab));
                Assert.That(tower.Chunks, Is.Not.Null);
                Assert.That(tower.Chunks.gameObject.scene, Is.EqualTo(scene));
                // Every lamp is a buildable detail asset of the tower.
                var buildable = AssetDatabase.LoadAssetAtPath<GameObject>(BuildablePath);
                Assert.That(tower.Chunks.AssetPrefabs.GetPrefab(TowerAssetType.LavaLamp), Is.EqualTo(buildable));
                var generator = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TowerGenerator>(true)).Single();
                Assert.That(generator.detailLavaLampObject, Is.EqualTo(buildable), "Regenerating the tower keeps the lamps.");
                int lampAssets = tower.Chunks.CachedChunks.Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.LavaLamp));
                Assert.That(tower.Placements.Count, Is.EqualTo(lampAssets));
                Assert.That(tower.UsesLampAssets, Is.True);
                Assert.That(tower.Placements.All(p => p.lamp >= 0 && p.ringIndex >= 0), "Every lamp asset sits on a placement spot.");
                int pillars = tower.Chunks.CachedChunks.Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar));
                Assert.That(tower.Interior, Is.True);
                // Every fourth pillar, less the ones where the winding stair ramp runs through the lamp.
                Assert.That(tower.Placements.Count(p => !p.exterior), Is.InRange(pillars / 6, pillars / 4 + 50));
                var center = tower.Chunks.FloorCenter;
                Assert.That(tower.ExteriorChance, Is.GreaterThan(0));
                Assert.That(tower.MaxLights, Is.GreaterThan(AeroProjectionLamp.MaximumActiveLamps));
                // A hundred to three hundred lamps are in view around the tower: a couple of dozen lights lit only a patch.
                Assert.That(tower.MaxLights, Is.GreaterThanOrEqualTo(64).And.LessThanOrEqualTo(TowerLavaLamps.MaximumLights));
                Assert.That(tower.Placements.Count(p => p.exterior), Is.GreaterThan(pillars / 40), "Lamps scattered outside free pillars.");
                var ringSize = tower.Chunks.CachedChunks.GroupBy(c => c.Key.FloorIndex)
                    .ToDictionary(g => g.Key, g => g.Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar)));
                AssertSpacing(tower.Placements, f => ringSize[f]);
                foreach (var p in tower.Placements.Where(p => !p.exterior).Take(200))
                {
                    var outward = new Vector3(p.Position.x - center.x, 0, p.Position.z - center.y).normalized;
                    Assert.That(Vector3.Dot(p.mount.rotation * Vector3.forward, outward), Is.LessThan(-.999f), "Sconces face the tower's interior.");
                }
                foreach (var floor in tower.Placements.Where(p => !p.exterior).GroupBy(p => p.floor).Take(6))
                {
                    // Ordered from angle zero like the placement, so the one seam where the ring closes is not compared.
                    var angles = floor.Select(p => Mathf.Repeat(Mathf.Atan2(p.Position.z - center.y, p.Position.x - center.x), Mathf.PI * 2)).OrderBy(a => a).ToArray();
                    int bays = tower.Chunks.CachedChunks.Where(c => c.Key.FloorIndex == floor.Key).Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar));
                    float bay = Mathf.PI * 2 / bays;
                    // Inside lamps are a whole number of four-pillar steps apart; a gap is a pillar left bare for the stairs.
                    for (int i = 1; i < angles.Length; i++)
                    {
                        float steps = (angles[i] - angles[i - 1]) / bay / 4;
                        Assert.That(steps, Is.GreaterThan(.98f), $"Floor {floor.Key}");
                        Assert.That(Mathf.Abs(steps - Mathf.Round(steps)), Is.LessThan(.02f), $"Floor {floor.Key}");
                    }
                }
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        private static FieldInfo Field(string name) =>
            typeof(ChunkManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

        private static void AddAsset(Transform parent, Vector3 position, TowerAssetType type)
        {
            var asset = new GameObject(type.ToString());
            asset.transform.SetParent(parent);
            asset.transform.position = position;
            asset.AddComponent<TowerAsset>().SetAssetType(type);
        }
    }
}
