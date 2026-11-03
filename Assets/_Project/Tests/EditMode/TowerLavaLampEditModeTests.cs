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
    /// pillars; never two on one pillar or on neighbouring pillars. They show once their pillar is built, and every
    /// lamp in view (up to the budget) lights its wall with one shared caustics cookie.
    /// </summary>
    public sealed class TowerLavaLampEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab";
        private const string CookiePath = "Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat";
        private const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
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

        private void CompletePillars(int floor)
        {
            var pending = manager.CachedChunks.Where(c => c.Key.FloorIndex == floor)
                .SelectMany(c => c.Assets.Select(a => (c.Key, a.LocalIndex))).ToArray();
            foreach (var (key, index) in pending) manager.SetAssetStage(key, index, ChunkAssetData.CompletedStage);
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
                int pillars = tower.Chunks.CachedChunks.Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar));
                Assert.That(tower.Interior, Is.True);
                // Every fourth pillar, less the ones where the winding stair ramp runs through the lamp.
                Assert.That(tower.Placements.Count(p => !p.exterior), Is.InRange(pillars / 6, pillars / 4 + 50));
                var center = tower.Chunks.FloorCenter;
                Assert.That(tower.ExteriorChance, Is.GreaterThan(0));
                Assert.That(tower.MaxLights, Is.GreaterThan(AeroProjectionLamp.MaximumActiveLamps));
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
