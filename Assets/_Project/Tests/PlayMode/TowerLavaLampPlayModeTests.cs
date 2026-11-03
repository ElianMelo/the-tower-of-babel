using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.World.Tests
{
    /// <summary>At runtime the built lamps draw, and the nearest ones carry a live upward water projection.</summary>
    public sealed class TowerLavaLampPlayModeTests
    {
        private readonly List<GameObject> objects = new();
        private float daylight;

        private GameObject Create(string name) { var go = new GameObject(name); objects.Add(go); return go; }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Shader.SetGlobalFloat("_AeroDaylight", daylight);
            foreach (var item in objects) if (item != null) Object.Destroy(item);
            objects.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator BuiltLamps_Draw_AndTheNearestProjectUpward_BrighterAtNight()
        {
#if UNITY_EDITOR
            daylight = Shader.GetGlobalFloat("_AeroDaylight");
            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Lighting/LavaProjectionLamp.prefab");
            Assert.That(prefab, Is.Not.Null);
            var owner = Create("Lava lamp chunks");
            owner.SetActive(false);
            var manager = owner.AddComponent<ChunkManager>();
            manager.enabled = false;
            // Eight finished pillars around a ring: lamps hang on the first and the fifth.
            var assets = new List<ChunkAssetData>();
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI * 2 / 8;
                assets.Add(new ChunkAssetData(i, TowerAssetType.Pillar, new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * 8, Quaternion.identity, Vector3.one, ChunkAssetData.CompletedStage));
            }
            var chunk = (ChunkSceneCache)System.Activator.CreateInstance(typeof(ChunkSceneCache),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { new ChunkKey(0, 0, 0), assets }, null);
            typeof(ChunkManager).GetField("cachedChunks", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, new List<ChunkSceneCache> { chunk });

            var lamps = Create("Tower lava lamps").AddComponent<TowerLavaLamps>();
            lamps.Configure(manager, prefab, 3.3f, .38f);
            Assert.That(lamps.Placements.Count, Is.EqualTo(2));

            var camera = Create("Lava lamp camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            var target = lamps.Placements[0].Position;
            camera.transform.position = target + new Vector3(6, -.5f, 0);
            camera.transform.LookAt(target);

            Shader.SetGlobalFloat("_AeroDaylight", 0);
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSeconds(.35f);

            Assert.That(lamps.VisibleLampCount, Is.EqualTo(2));
            Assert.That(lamps.DrawnBatchCount, Is.GreaterThan(0), "Built lamps are drawn instanced.");
            Assert.That(lamps.ActiveLightCount, Is.EqualTo(2));
            var spots = lamps.Lights.Where(l => l.type == LightType.Spot && l.gameObject.activeInHierarchy).ToArray();
            Assert.That(spots.Length, Is.EqualTo(2));
            Assert.That(spots.All(s => s.enabled && s.transform.forward.y > .9f), "The projection budget switched them on, pointing up.");
            Assert.That(spots.All(s => s.cookie != null), "The animated water cookie is projected.");
            float night = spots[0].intensity;

            Shader.SetGlobalFloat("_AeroDaylight", 1);
            yield return null; yield return null;
            Assert.That(spots[0].intensity, Is.LessThan(night * .5f).And.GreaterThan(0), "Still lit by day, but much dimmer.");

            camera.transform.position = target + Vector3.up * 300;
            yield return new WaitForSeconds(.35f);
            Assert.That(lamps.ActiveLightCount, Is.Zero, "Far away, the lights are handed back.");
            Assert.That(AeroProjectionLamp.MaximumActiveLamps, Is.EqualTo(4));
#else
            Assert.Ignore("Needs the editor asset database.");
            yield break;
#endif
        }
    }
}
