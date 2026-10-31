using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroRainEditModeTests
    {
        const string RainProfilePath = "Assets/_Project/Environment/Lighting/AeroRain.asset";

        [Test]
        public void Rain_FollowsTheWetnessRampAroundTheCameraAndTintsForNight()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            Assert.That(profile, Is.Not.Null);
            var wetProfile = ScriptableObject.CreateInstance<AeroWetnessProfile>(); wetProfile.rainFade = 2;
            var dayProfile = ScriptableObject.CreateInstance<AeroDayNightProfile>();
            var root = new GameObject("Rain test") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            try
            {
                var cycle = root.AddComponent<AeroDayNightCycle>(); cycle.profile = dayProfile; cycle.timeOfDay = .5f;
                var wetness = root.AddComponent<AeroWetnessController>(); wetness.profile = wetProfile;
                var rain = root.AddComponent<AeroRainController>(); rain.profile = profile; rain.wetness = wetness; rain.dayNight = cycle;
                var camera = new Vector3(10, 30, -4);

                rain.Tick(.1f, camera, Vector3.forward);
                Assert.That(rain.Drops, Is.Null, "Nothing is built while it is dry.");
                Assert.That(cycle.overcast, Is.Zero);

                wetness.Raining = true; wetness.Tick(1);
                rain.Tick(.1f, camera, Vector3.forward);
                Assert.That(rain.Amount, Is.EqualTo(.5f).Within(.01f), "Falling rain fades in with the surface rain.");
                Assert.That(rain.EmissionRate, Is.EqualTo(profile.dropsPerSecond * .5f).Within(1));
                wetness.Tick(5); rain.Tick(.1f, camera, Vector3.forward);
                Assert.That(rain.EmissionRate, Is.EqualTo(profile.dropsPerSecond).Within(1));
                Assert.That(cycle.overcast, Is.EqualTo(profile.overcast).Within(1e-4f));

                var emitter = rain.Drops.transform.position;
                Assert.That(emitter.y, Is.EqualTo(camera.y + profile.spawnHeight).Within(1e-3f));
                Assert.That(emitter.z, Is.EqualTo(camera.z + profile.lookAhead).Within(1e-3f), "The rain area leads the view.");
                Assert.That(rain.Drops.main.simulationSpace, Is.EqualTo(ParticleSystemSimulationSpace.World), "Drops stay put while the camera moves.");
                Assert.That(rain.Drops.collision.enabled, Is.True, "Drops stop on roofs and decks, so covered floors stay dry.");
                Assert.That(rain.Drops.subEmitters.GetSubEmitterSystem(0), Is.SameAs(rain.Splashes));
                Assert.That(rain.Drops.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.SameAs(profile.dropMaterial));
                Assert.That(rain.Audio != null && rain.Audio.clip == profile.rainLoop && rain.Audio.loop, Is.True);

                Color day = rain.Drops.main.startColor.color;
                cycle.timeOfDay = 0; rain.Tick(.1f, camera, Vector3.forward);
                Color night = rain.Drops.main.startColor.color;
                Assert.That(night.grayscale, Is.LessThan(day.grayscale * .7f), "Night rain is darker.");
                Assert.That(night.a, Is.GreaterThan(0), "...but still visible at night.");

                wetness.Raining = false; wetness.Tick(5); rain.Tick(.1f, camera, Vector3.forward);
                Assert.That(rain.EmissionRate, Is.Zero); Assert.That(cycle.overcast, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(wetProfile); Object.DestroyImmediate(dayProfile);
                foreach (var scene in Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (scene.gameObject.hideFlags == HideFlags.None) { scene.enabled = false; scene.enabled = true; }
            }
        }

        static AeroTowerAtmosphere MakeTower(GameObject root)
        {
            var tower = root.AddComponent<AeroTowerAtmosphere>();
            tower.center = Vector2.zero; tower.baseRadius = 96; tower.decrease = 3; tower.floorHeight = 6; tower.levels = 10;
            return tower;
        }

        [Test]
        public void Shelter_FollowsTheSteppedTowerAndOnlyBuiltFloors()
        {
            var root = new GameObject("Shelter test") { hideFlags = HideFlags.HideAndDontSave }; root.SetActive(false);
            try
            {
                var tower = MakeTower(root);
                // Floor 0 interior (radius 60) is under floor 1's deck (wall radius 93).
                Assert.That(tower.IsUnderRoof(new Vector3(60, 2, 0)), Is.True);
                Assert.That(tower.IsUnderRoof(new Vector3(100, 2, 0)), Is.False, "Outside the tower is open sky.");
                // Floor 3 (wall radius 90) under floor 4 (wall radius 87): its terrace at radius 89 is open.
                Assert.That(tower.IsUnderRoof(new Vector3(89, 3 * 6 + 2, 0)), Is.False, "Outer terraces are exposed.");
                Assert.That(tower.IsUnderRoof(new Vector3(0, 9 * 6 + 2, 0)), Is.False, "The top floor has no roof.");
                Assert.That(tower.TryDeckBelow(new Vector3(89, 3 * 6 + 14, 0), out float deck), Is.True);
                Assert.That(deck, Is.EqualTo(3 * 6), "A drop over the terrace lands on the highest deck that reaches it.");
                Assert.That(tower.TryDeckBelow(new Vector3(120, 30, 0), out _), Is.False, "Beyond the tower drops fall to the ground.");
                tower.BuiltFloors = 2;
                Assert.That(tower.IsUnderRoof(new Vector3(60, 2, 0)), Is.True, "Floor 1 is built, so floor 0 is roofed.");
                Assert.That(tower.IsUnderRoof(new Vector3(60, 8, 0)), Is.False, "Floor 2 is not built: floor 1 is open to the sky.");
                Assert.That(tower.TryDeckBelow(new Vector3(60, 40, 0), out deck), Is.True); Assert.That(deck, Is.EqualTo(6));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void Drops_NeverSpawnUnderARoofAndStopOnTheFirstDeck()
        {
            var root = new GameObject("Drop plan test") { hideFlags = HideFlags.HideAndDontSave }; root.SetActive(false);
            try
            {
                var tower = MakeTower(root);
                var fall = new Vector3(1.2f, -20, .5f);
                Assert.That(AeroRainController.PlanDrop(tower, new Vector3(50, 16, 0), fall, 1.3f, out _, out _), Is.False, "Inside the tower: no drop.");
                Assert.That(AeroRainController.PlanDrop(tower, new Vector3(91.5f, 16, 0), fall, 1.3f, out _, out _), Is.False, "Wind margin keeps drops off the arch line.");
                Assert.That(AeroRainController.PlanDrop(tower, new Vector3(110, 16, 0), fall, 1.3f, out float life, out bool deck), Is.True);
                Assert.That(life, Is.EqualTo(1.3f)); Assert.That(deck, Is.False);
                Assert.That(AeroRainController.PlanDrop(tower, new Vector3(89, 40, 0), fall, 2f, out life, out deck), Is.True, "Over an exposed terrace.");
                Assert.That(deck, Is.True); Assert.That(life, Is.EqualTo((40 - 18) / 20f).Within(1e-4f), "Ends on the floor-3 deck, never falls through it.");
                Assert.That(AeroRainController.PlanDrop(null, new Vector3(50, 16, 0), fall, 1.3f, out life, out _), Is.True, "Without a tower everything rains.");

                // Camera on floor 0, deep inside: every candidate drop above the camera is rejected.
                var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
                var wetProfile = ScriptableObject.CreateInstance<AeroWetnessProfile>();
                var wetness = root.AddComponent<AeroWetnessController>(); wetness.profile = wetProfile;
                var rain = root.AddComponent<AeroRainController>(); rain.profile = profile; rain.wetness = wetness; rain.tower = tower;
                wetness.Raining = true; wetness.Tick(10);
                rain.Tick(.1f, new Vector3(40, 2, 0), Vector3.forward);
                Assert.That(rain.EmittedLastTick, Is.Zero, "No rain inside the tower.");
                Assert.That(rain.RejectedLastTick, Is.GreaterThan(1000));
                rain.Tick(.1f, new Vector3(140, 2, 0), Vector3.forward);
                Assert.That(rain.EmittedLastTick, Is.GreaterThan(1000), "Full rain outside.");
                Object.DestroyImmediate(wetProfile);
            }
            finally { Object.DestroyImmediate(root); RestoreWetness(); }
        }

        static void RestoreWetness()
        {
            foreach (var scene in Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (scene.gameObject.hideFlags == HideFlags.None) { scene.enabled = false; scene.enabled = true; }
        }

        [Test]
        public void RainChange_ClearsAForcedWetnessPresetAndSurfacesDryQuickly()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>("Assets/_Project/Environment/Lighting/AeroWetness.asset");
            Assert.That(profile.dryOut, Is.LessThanOrEqualTo(12), "Rain off should dry the world in seconds, not most of a minute.");
            var go = new GameObject("Wetness preset test") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            try
            {
                var controller = go.AddComponent<AeroWetnessController>(); controller.profile = profile;
                controller.CycleWetnessPreset(); controller.CycleWetnessPreset();
                Assert.That(controller.CycleWetnessPreset(), Is.EqualTo("Soaked"));
                controller.Raining = true;
                Assert.That(controller.WetnessOverride.HasValue, Is.False, "Starting rain hands wetness back to the weather.");
                controller.Tick(10);
                controller.OverrideWetness(1);
                controller.Raining = false;
                Assert.That(controller.WetnessOverride.HasValue, Is.False, "Stopping rain drops a forced soaked preset.");
                for (int i = 0; i < 12; i++) controller.Tick(1);
                Assert.That(controller.Wetness, Is.Zero); Assert.That(controller.RainAmount, Is.Zero);
                Assert.That(controller.IsActive, Is.False, "Once dry the shaders skip the wet path again.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                foreach (var scene in Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (scene.gameObject.hideFlags == HideFlags.None) { scene.enabled = false; scene.enabled = true; }
            }
        }

        [Test]
        public void Profile_IsHeavyRain()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroRainProfile>(RainProfilePath);
            Assert.That(profile.dropsPerSecond, Is.GreaterThanOrEqualTo(8000), "Heavy rain, not a drizzle.");
            Assert.That(profile.maxDrops, Is.GreaterThanOrEqualTo(profile.dropsPerSecond * profile.DropLifetime), "The particle cap must not thin the rain.");
            Assert.That(profile.dayTint.a, Is.GreaterThanOrEqualTo(.7f)); Assert.That(profile.dayTint.grayscale, Is.LessThan(.85f), "Grey streaks read against a bright sky."); Assert.That(profile.nightTint.a, Is.GreaterThanOrEqualTo(.45f));
            Assert.That(profile.dropLength.x, Is.GreaterThanOrEqualTo(.8f), "Long streaks read as heavy rain.");
            var crop = profile.dropMaterial.GetTextureScale("_BaseMap");
            Assert.That(crop.x, Is.LessThan(.2f), "The material crops to the streak, so particle width is drop width.");
            Assert.That(profile.dropWidth.x, Is.GreaterThanOrEqualTo(.025f), "Wide enough to cover more than a pixel at 10 m.");
        }

        [Test]
        public void Interior_IsCalmerAndPillarsKeepAnAquariumLightAtNight()
        {
            var exit = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>("Assets/_Project/Environment/Lighting/TowerExitAtmosphere.asset");
            Assert.That(exit.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var color), Is.True);
            Assert.That(color.postExposure.value, Is.LessThanOrEqualTo(0), "Inside the tower is not brighter than outside.");
            Assert.That(color.contrast.value, Is.LessThanOrEqualTo(0), "No extra contrast punch inside.");
            Assert.That(exit.TryGet<UnityEngine.Rendering.Universal.Bloom>(out var bloom), Is.True); Assert.That(bloom.intensity.value, Is.LessThanOrEqualTo(.4f));
            Assert.That(exit.TryGet<AeroShaftVolume>(out var shafts), Is.True); Assert.That(shafts.intensity.value, Is.LessThanOrEqualTo(1.2f));
            var rays = AssetDatabase.LoadAssetAtPath<AeroRaySettings>("Assets/_Project/Environment/Lighting/TowerInteriorRays.asset");
            Assert.That(rays.intensity, Is.LessThanOrEqualTo(1.2f)); Assert.That(rays.fillStrength, Is.LessThanOrEqualTo(1.6f));
            Assert.That(rays.noiseContrast, Is.LessThanOrEqualTo(.5f), "Softer, less dramatic shafts.");
            Assert.That(rays.interiorHaze, Is.LessThanOrEqualTo(.06f), "No milky haze over the interior.");
            Assert.That(color.postExposure.value, Is.LessThanOrEqualTo(-.3f));
            var shafts2 = AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Project/Shaders/AeroSunShafts.shader");
            Assert.That(System.IO.File.ReadAllText(AssetDatabase.GetAssetPath(shafts2)), Does.Contain("min(fill.distanceAttenuation,AERO_FILL_SCATTER_CAP)"),
                "Fill scattering is capped so arches never glow into blinding orbs.");
            var pillar = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_BubbleTube_Pillar.mat");
            Assert.That(pillar.HasProperty("_NightWaterGlow"), Is.True);
            Assert.That(pillar.GetFloat("_NightWaterGlow"), Is.GreaterThan(.05f), "Water, spine and bubbles stay readable at night.");
        }

        [Test]
        public void Overcast_DimsTheSunAndThickensFogWithoutChangingClearSkies()
        {
            var profile = ScriptableObject.CreateInstance<AeroDayNightProfile>();
            var root = new GameObject("Overcast test") { hideFlags = HideFlags.HideAndDontSave };
            root.SetActive(false);
            var fog = RenderSettings.fogDensity; var fogColor = RenderSettings.fogColor; var sunSetting = RenderSettings.sun;
            try
            {
                var sun = root.AddComponent<Light>(); sun.type = LightType.Directional;
                var cycle = root.AddComponent<AeroDayNightCycle>(); cycle.profile = profile; cycle.sun = sun;
                cycle.ApplyTime(.5f);
                float clearSun = sun.intensity, clearFog = RenderSettings.fogDensity;
                Assert.That(clearSun, Is.EqualTo(profile.sunIntensity).Within(.01f), "No change while clear.");
                Assert.That(clearFog, Is.EqualTo(.0006f).Within(1e-6f));
                cycle.overcast = 1; cycle.ApplyTime(.5f);
                Assert.That(sun.intensity, Is.EqualTo(clearSun * cycle.overcastSunScale).Within(.01f));
                Assert.That(RenderSettings.fogDensity, Is.EqualTo(clearFog * cycle.overcastFogScale).Within(1e-6f));

                cycle.SetTime(.1f); Assert.That(cycle.timeOfDay, Is.EqualTo(.1f).Within(1e-6f));
                cycle.paused = true; Assert.That(cycle.Advancing, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root); Object.DestroyImmediate(profile);
                RenderSettings.fogDensity = fog; RenderSettings.fogColor = fogColor; RenderSettings.sun = sunSetting;
                var scene = Object.FindFirstObjectByType<AeroDayNightCycle>();
                if (scene != null) scene.ApplyTime(scene.timeOfDay);
            }
        }
    }
}
