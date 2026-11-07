#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroAtmospherePresetPlayModeTests
    {
        sealed class MemoryStore : IAeroSettingsStore
        {
            public readonly Dictionary<string, int> values = new();
            public int GetInt(string key, int fallback) => values.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => values[key] = value;
        }

        [UnityTest]
        public IEnumerator Director_GradesTheSceneAndFloatsSeasonalParticlesAtRuntime()
        {
            var previous = SceneManager.GetActiveScene(); var scene = SceneManager.CreateScene("Atmosphere preset runtime fixture"); SceneManager.SetActiveScene(scene);
            var root = new GameObject("Atmosphere fixture"); root.SetActive(false);
            var cameraObject = new GameObject("Fixture camera") { tag = "MainCamera" };
            try
            {
                cameraObject.AddComponent<Camera>(); cameraObject.transform.position = new Vector3(0, 2, 0);
                var baseVolume = root.AddComponent<Volume>(); baseVolume.isGlobal = true; baseVolume.priority = -50;
                var director = root.AddComponent<AeroAtmosphereDirector>();
                director.library = AssetDatabase.LoadAssetAtPath<AeroAtmospherePresetLibrary>("Assets/_Project/Environment/Lighting/AeroAtmospherePresets.asset");
                director.particleShader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/AeroAmbientParticles.shader");
                director.baseVolume = baseVolume; director.fallbackTimeOfDay = .5f; director.seasonBlendSeconds = .2f;
                var store = new MemoryStore(); store.SetInt(AeroAtmosphereDirector.SeasonKey, (int)AeroSeason.Summer);
                director.Store = store;
                root.SetActive(true);
                yield return null; yield return null;

                Assert.That(AeroAtmosphereDirector.Instance, Is.SameAs(director));
                Assert.That(director.GradeVolume, Is.Not.Null);
                Assert.That(director.GradeVolume.priority, Is.EqualTo(baseVolume.priority + 1));
                var motes = director.Particles.System(AeroParticleKind.Dot);
                Assert.That(director.Particles.Rates[(int)AeroParticleKind.Dot], Is.GreaterThan(0), "Summer mid-day floats golden motes.");
                Assert.That(motes.isPlaying, Is.True);
                yield return new WaitForSeconds(.5f);
                Assert.That(motes.particleCount, Is.GreaterThan(0));

                director.SetSeason(AeroSeason.Winter);
                yield return new WaitForSeconds(.4f);
                Assert.That(director.SeasonBlend, Is.EqualTo(1));
                Assert.That(director.Particles.Rates[(int)AeroParticleKind.Flake], Is.GreaterThan(0), "Winter snow.");
                Assert.That(director.Particles.Rates[(int)AeroParticleKind.Dot], Is.Zero, "Summer motes stop.");
                Assert.That(store.values[AeroAtmosphereDirector.SeasonKey], Is.EqualTo((int)AeroSeason.Winter));

                root.SetActive(false);
                Assert.That(AeroAtmosphereDirector.Instance, Is.Null);
                yield return null;
                Assert.That(root.transform.childCount, Is.Zero, "Disabling removes the grade and particles.");
            }
            finally { Object.Destroy(root); Object.Destroy(cameraObject); SceneManager.SetActiveScene(previous); }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
