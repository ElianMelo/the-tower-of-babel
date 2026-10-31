using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Networking.Weather;
using TowerOfBabel.Rendering;
using TowerOfBabel.Weather;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class WeatherEditModeTests
    {
        const string RainProfilePath = "Assets/_Project/Environment/Lighting/AeroRain.asset";
        [Test]
        public void Schedule_AlternatesClearAndRainSpellsWithinTheirRanges()
        {
            var schedule = new WeatherSchedule { clearSeconds = new Vector2(10, 20), rainSeconds = new Vector2(3, 5) };
            schedule.Seed(7);
            schedule.Force(false);
            int flips = 0;
            for (int i = 0; i < 2000; i++)
            {
                bool before = schedule.Raining; float left = schedule.Remaining;
                Assert.That(left, Is.LessThanOrEqualTo(before ? 5 : 20) .And.GreaterThan(0));
                if (schedule.Tick(.1f)) { flips++; Assert.That(schedule.Raining, Is.Not.EqualTo(before)); }
            }
            Assert.That(flips, Is.InRange(14, 30), "About 200 s of ~11.5 s half-cycles.");
        }

        [Test]
        public void Clock_AdvancesWrapsAndHoldsWhileFrozen()
        {
            Assert.That(WeatherClock.Advance(.9f, 240, 1200, false), Is.EqualTo(.1f).Within(1e-4f));
            Assert.That(WeatherClock.Advance(.25f, 240, 1200, true), Is.EqualTo(.25f));
            Assert.That(WeatherClock.Advance(.25f, 0, 1200, false), Is.EqualTo(.25f));
            Assert.That(WeatherClock.Delta(.95f, .05f), Is.EqualTo(.1f).Within(1e-5f), "Shortest way across midnight.");
            Assert.That(WeatherClock.Delta(.05f, .95f), Is.EqualTo(-.1f).Within(1e-5f));
        }

        [Test]
        public void Director_ManualRainHoldsAndAutomaticWeatherResumes()
        {
            var schedule = new WeatherSchedule { clearSeconds = new Vector2(5, 5), rainSeconds = new Vector2(5, 5) };
            var director = new WeatherDirector(schedule, new WeatherState { TimeOfDay = 1.25f, AutomaticWeather = true });
            Assert.That(director.State.TimeOfDay, Is.EqualTo(.25f).Within(1e-5f));
            Assert.That(director.Tick(6), Is.True); Assert.That(director.State.Raining, Is.True, "Automatic weather starts rain.");

            Assert.That(director.Apply(WeatherRequest.Rain, 0), Is.True);
            Assert.That(director.State.Raining, Is.False);
            Assert.That(director.State.AutomaticWeather, Is.False, "Setting rain by hand pauses automatic weather.");
            Assert.That(director.Tick(100), Is.False); Assert.That(director.State.Raining, Is.False, "The manual choice holds.");

            director.Apply(WeatherRequest.AutomaticWeather, 1);
            Assert.That(director.Tick(4), Is.False, "Resuming starts a fresh spell.");
            Assert.That(director.Tick(2), Is.True); Assert.That(director.State.Raining, Is.True);

            director.Apply(WeatherRequest.TimeOfDay, WeatherClock.Midnight); Assert.That(director.State.TimeOfDay, Is.Zero);
            director.Apply(WeatherRequest.FreezeTime, 1); Assert.That(director.State.TimeFrozen, Is.True);
            Assert.That(director.Apply(WeatherRequest.TimeOfDay, float.NaN), Is.False, "Bad values are rejected.");
            Assert.That(director.Apply((WeatherRequest)99, 1), Is.False);
            Assert.That(director.State.TimeOfDay, Is.Zero);
        }

        [Test]
        public void Service_OfflineAppliesRainAndTimeToTheSceneDrivers()
        {
            var root = new GameObject("Weather test") { hideFlags = HideFlags.HideAndDontSave };
            var dayProfile = ScriptableObject.CreateInstance<AeroDayNightProfile>();
            var wetProfile = ScriptableObject.CreateInstance<AeroWetnessProfile>();
            try
            {
                root.SetActive(false);
                var cycle = root.AddComponent<AeroDayNightCycle>(); cycle.profile = dayProfile; cycle.timeOfDay = .3f;
                var wetness = root.AddComponent<AeroWetnessController>(); wetness.profile = wetProfile;
                var service = root.AddComponent<NetworkWeatherService>();
                service.Bind(cycle, wetness);
                Assert.That(service.HasAuthority, Is.True, "With no session running the menu works locally.");
                Assert.That(service.AutomaticWeather, Is.True);
                WeatherState? changed = null; service.StateChanged += s => changed = s;

                Assert.That(service.RequestRain(true), Is.True);
                Assert.That(wetness.Raining, Is.True); Assert.That(service.Raining, Is.True);
                Assert.That(service.AutomaticWeather, Is.False);
                Assert.That(changed.HasValue && changed.Value.Raining, Is.True);

                Assert.That(service.RequestNight(), Is.True); Assert.That(cycle.timeOfDay, Is.EqualTo(0).Within(1e-5f));
                Assert.That(service.RequestDay(), Is.True); Assert.That(cycle.timeOfDay, Is.EqualTo(.5f).Within(1e-5f));
                Assert.That(service.State.Raining, Is.True, "Changing the time keeps the rain.");
                Assert.That(service.RequestFreezeTime(true), Is.True); Assert.That(cycle.paused, Is.True);
                Assert.That(cycle.Advancing, Is.False);
                Assert.That(service.RequestRain(false), Is.True); Assert.That(wetness.Raining, Is.False);

                service.ReceiveState(new WeatherState { TimeOfDay = .5005f, Raining = true });
                Assert.That(cycle.timeOfDay, Is.EqualTo(.5f).Within(1e-5f), "Sub-second drift is not snapped.");
                Assert.That(wetness.Raining, Is.True); Assert.That(cycle.paused, Is.False);
                service.ReceiveState(new WeatherState { TimeOfDay = .8f });
                Assert.That(cycle.timeOfDay, Is.EqualTo(.8f).Within(1e-5f), "A real difference jumps to the server clock.");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(dayProfile); Object.DestroyImmediate(wetProfile); }
        }

        [Test]
        public void TowerScene_WiresRainAndTheSyncedWeatherService()
        {
            const string path = "Assets/_Project/Scenes/Tower.unity";
            var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByPath(path);
            bool opened = !loaded.IsValid() || !loaded.isLoaded;
            var scene = opened ? UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Additive) : loaded;
            try { CheckWiring(scene); }
            finally { if (opened) UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
        }

        static void CheckWiring(UnityEngine.SceneManagement.Scene scene)
        {
            var service = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<NetworkWeatherService>(true)).Single();
            Assert.That(service.GetComponent<FishNet.Object.NetworkObject>(), Is.Not.Null, "The service replicates through the scene's network authority.");
            var serialized = new SerializedObject(service);
            var cycle = (AeroDayNightCycle)serialized.FindProperty("dayNight").objectReferenceValue;
            Assert.That(cycle, Is.Not.Null);
            Assert.That(serialized.FindProperty("wetness").objectReferenceValue, Is.SameAs(cycle.GetComponent<AeroWetnessController>()));
            var rain = cycle.GetComponent<AeroRainController>();
            Assert.That(rain, Is.Not.Null);
            Assert.That(rain.wetness, Is.SameAs(cycle.GetComponent<AeroWetnessController>()));
            Assert.That(rain.profile, Is.SameAs(AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath)));
        }

        [Test]
        public void RainProfile_UsesTheDynamicPackageAssetsInUrp()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            Assert.That(profile, Is.Not.Null); Assert.That(profile.Ready, Is.True);
            foreach (var material in new[] { profile.dropMaterial, profile.splashMaterial })
            {
                Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Particles/Unlit"));
                Assert.That(material.renderQueue, Is.EqualTo(3000), "Transparent.");
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Does.StartWith("Assets/MoveRotateResize/DYNAMIC- Falling Rain/"));
            }
            Assert.That(AssetDatabase.GetAssetPath(profile.rainLoop), Does.StartWith("Assets/MoveRotateResize/DYNAMIC- RainWetness/Audio/"));
            Assert.That(profile.DropLifetime, Is.InRange(.5f, 4f), "Drops live just long enough to pass the camera.");
        }
    }
}
