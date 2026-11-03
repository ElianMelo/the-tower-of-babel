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
    /// <summary>Lava lamps hang on one pillar, skip three, around every ring, and show once their pillar is built.</summary>
    public sealed class TowerLavaLampEditModeTests
    {
        private const string PrefabPath = "Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab";
        private const string CookiePath = "Assets/_Project/Environment/Lighting/AquaProjectionCookie.mat";
        private const string ScenePath = "Assets/_Project/Scenes/Tower.unity";
        private const int RingPillars = 10;
        private const float RingRadius = 10, MountHeight = 3.3f, PillarRadius = .38f;

        private GameObject root, lampObject, stairPrefab;
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
            Object.DestroyImmediate(root);
            if (stairPrefab != null) Object.DestroyImmediate(stairPrefab);
        }

        private void Build(System.Action<Transform> extra = null)
        {
            var tower = new GameObject("Authoring");
            tower.transform.SetParent(root.transform);
            for (int floor = 0; floor < 2; floor++)
                for (int i = 0; i < RingPillars; i++)
                {
                    float a = i * Mathf.PI * 2 / RingPillars;
                    AddAsset(tower.transform, new Vector3(Mathf.Cos(a) * RingRadius, floor * 6, Mathf.Sin(a) * RingRadius), TowerAssetType.Pillar);
                }
            extra?.Invoke(tower.transform);
            Field("towerRoot").SetValue(manager, tower);
            manager.CacheChunks();
            lampObject = new GameObject("Tower lava lamps");
            lamps = lampObject.AddComponent<TowerLavaLamps>();
            lamps.Configure(manager, prefab, MountHeight, PillarRadius);
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
            Assert.That(lamps.Placements.Count(p => p.floor == 1), Is.EqualTo(3));
            for (int n = 0; n < ground.Length; n++)
            {
                var pillar = PillarAt(n * TowerLavaLamps.PillarInterval);
                var outward = new Vector3(pillar.x, 0, pillar.z).normalized;
                Assert.That(Vector3.Distance(ground[n].Position, pillar + Vector3.up * MountHeight + outward * PillarRadius), Is.LessThan(.001f));
                Assert.That(Vector3.Dot(ground[n].mount.rotation * Vector3.forward, outward), Is.GreaterThan(.999f), "The sconce reaches out of the tower.");
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
            var outward = new Vector3(pillar.x, 0, pillar.z).normalized;
            Build(t => AddAsset(t, pillar + outward * .9f + Vector3.up * 2.6f, TowerAssetType.Stair));
            var ground = lamps.Placements.Where(p => p.floor == 0).Select(p => p.Position).ToArray();
            Assert.That(ground.Length, Is.EqualTo(2));
            Assert.That(ground.All(p => Vector3.Distance(new Vector3(p.x, 0, p.z), pillar) > 2), "The lamp that would hang in the stair is left out.");
            Assert.That(ground.Any(p => Vector3.Distance(new Vector3(p.x, 0, p.z), PillarAt(8)) < 1), "The next lamp still hangs four pillars on.");
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
        public void TowerScene_HangsALampOnEveryFourthPillar()
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
                Assert.That(tower.Placements.Count, Is.InRange(pillars / 4 - 60, pillars / 4 + 50));
                // Lamps along a ring are four pillar bays apart.
                var center = tower.Chunks.FloorCenter;
                foreach (var floor in tower.Placements.GroupBy(p => p.floor).Take(6))
                {
                    // Ordered from angle zero like the placement, so the one seam where the ring closes is not compared.
                    var angles = floor.Select(p => Mathf.Repeat(Mathf.Atan2(p.Position.z - center.y, p.Position.x - center.x), Mathf.PI * 2)).OrderBy(a => a).ToArray();
                    int bays = tower.Chunks.CachedChunks.Where(c => c.Key.FloorIndex == floor.Key).Sum(c => c.Assets.Count(a => a.AssetType == TowerAssetType.Pillar));
                    float bay = Mathf.PI * 2 / bays;
                    for (int i = 1; i < angles.Length; i++)
                        Assert.That((angles[i] - angles[i - 1]) / bay, Is.EqualTo(4).Within(.05f).Or.EqualTo(8).Within(.05f), $"Floor {floor.Key}");
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
