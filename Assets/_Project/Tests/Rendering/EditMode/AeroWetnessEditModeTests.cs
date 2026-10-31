using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroWetnessEditModeTests
    {
        const string ProfilePath = "Assets/_Project/Environment/Lighting/AeroWetness.asset";

        static AeroWetnessController MakeController(out GameObject go, AeroWetnessProfile profile)
        {
            go = new GameObject("Wetness test") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            var controller = go.AddComponent<AeroWetnessController>();
            controller.profile = profile;
            go.SetActive(true);
            return controller;
        }

        static void RestoreSceneDriver()
        {
            var scene = Object.FindObjectsByType<AeroWetnessController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(c => c.gameObject.hideFlags == HideFlags.None);
            if (scene != null) { scene.enabled = false; scene.enabled = true; }
        }

        [Test]
        public void Profile_UsesTheDynamicRainWetnessTextures()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.TexturesReady, Is.True);
            foreach (var texture in new[] { profile.drops, profile.streaks, profile.ripples })
                Assert.That(AssetDatabase.GetAssetPath(texture), Does.StartWith("Assets/MoveRotateResize/"));
            Assert.That(profile.rampIn, Is.LessThan(profile.dryOut), "Surfaces wet quickly and dry slowly.");
        }

        [Test]
        public void Rain_RampsInFastAndDriesOutSlowly()
        {
            var profile = ScriptableObject.CreateInstance<AeroWetnessProfile>();
            profile.dewDay = profile.dewDawn = .1f; profile.rampIn = 4; profile.dryOut = 20; profile.rainFade = 2;
            var controller = MakeController(out var go, profile);
            try
            {
                controller.WetnessEnabled = true;
                controller.Tick(10);
                Assert.That(controller.Wetness, Is.EqualTo(.1f).Within(.001f), "Without rain the film settles at dew.");
                controller.Raining = true;
                controller.Tick(2);
                Assert.That(controller.Wetness, Is.EqualTo(.6f).Within(.01f));
                Assert.That(controller.RainAmount, Is.EqualTo(1).Within(.001f));
                controller.Tick(10);
                Assert.That(controller.Wetness, Is.EqualTo(1).Within(.001f));
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.WetnessId), Is.EqualTo(1).Within(.001f));
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.RainId), Is.EqualTo(1).Within(.001f));
                controller.Raining = false;
                controller.Tick(10);
                Assert.That(controller.Wetness, Is.EqualTo(.5f).Within(.01f), "Drying takes the longer dry-out time (10 s of 20 s).");
                Assert.That(controller.RainAmount, Is.Zero);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); RestoreSceneDriver(); }
        }

        [Test]
        public void Wetness_IsOffByDefaultUntilRainOrAPresetIsRequested()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>(ProfilePath);
            var controller = MakeController(out var go, profile);
            try
            {
                Assert.That(controller.WetnessEnabled, Is.False, "Wetness stays off until rain is implemented.");
                controller.Tick(30);
                Assert.That(controller.Wetness, Is.Zero, "No dew while wetness is off.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ReadyId), Is.Zero, "Shaders skip the wet path while off.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.WetnessId), Is.Zero);
                controller.Raining = true; controller.Tick(1);
                Assert.That(controller.Wetness, Is.GreaterThan(0), "The F3 rain toggle still previews the film.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ReadyId), Is.EqualTo(1));
                controller.Raining = false; for (int i = 0; i < 60; i++) controller.Tick(1);
                Assert.That(controller.Wetness, Is.Zero, "Surfaces dry fully, not back to dew.");
                Assert.That(Shader.GetGlobalFloat(AeroWetnessController.ReadyId), Is.Zero);
            }
            finally { Object.DestroyImmediate(go); RestoreSceneDriver(); }
        }

        [Test]
        public void Dew_FollowsTheTimeOfDayAndPresetsCycle()
        {
            var profile = ScriptableObject.CreateInstance<AeroWetnessProfile>();
            Assert.That(profile.DewAt(.25f), Is.GreaterThan(profile.DewAt(.55f)), "Dawn is dewier than early afternoon.");
            var controller = MakeController(out var go, profile);
            try
            {
                Assert.That(controller.CycleWetnessPreset(), Is.EqualTo("Dry")); Assert.That(controller.Wetness, Is.Zero);
                Assert.That(controller.CycleWetnessPreset(), Is.EqualTo("Dew"));
                Assert.That(controller.CycleWetnessPreset(), Is.EqualTo("Soaked")); Assert.That(controller.Wetness, Is.EqualTo(1));
                Assert.That(controller.CycleWetnessPreset(), Is.EqualTo("Weather")); Assert.That(controller.WetnessOverride.HasValue, Is.False);
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(profile); RestoreSceneDriver(); }
        }

        [Test]
        public void TowerScene_DrivesWetnessFromTheDayNightCycleAndTowerShelter()
        {
            TowerSceneAccess.With(scene =>
            {
                var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AeroWetnessController>(true)).Single();
                Assert.That(controller.profile, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>(ProfilePath)));
                Assert.That(controller.dayNight, Is.Not.Null);
                Assert.That(controller.tower, Is.Not.Null, "Rain shelter follows the stepped tower geometry.");
                Assert.That(controller.WetnessEnabled, Is.False, "The scene keeps wetness off until rain is implemented.");
            });
        }

        [Test]
        public void WetMarble_IsGlossierAndRainMovesOverTime()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroWetnessProfile>(ProfilePath);
            using var h = new AeroRenderHarness(128);
            var material = h.Own(new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat")));
            material.EnableKeyword("_AERO_WETNESS"); material.SetFloat("_WetnessOn", 1);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            for (int x = -1; x <= 1; x++) for (int z = -1; z <= 1; z++) h.Spawn(mesh, material, new Vector3(x * 4, 0, z * 4));
            h.Camera.transform.position = new Vector3(0, 2.2f, -5); h.Camera.transform.LookAt(new Vector3(0, 0, 1));
            h.Sun.transform.rotation = Quaternion.Euler(35, 180, 0); // low sun ahead: glossy floors light up
            var controller = MakeController(out var go, profile);
            try
            {
                controller.previewTime = 2; controller.OverrideWetness(0); controller.Tick(0);
                var dry = h.Render();
                controller.OverrideWetness(1); controller.Raining = true;
                for (int i = 0; i < 20; i++) controller.Tick(.5f);
                var wet = h.Render();
                controller.previewTime = 2.6f; controller.Tick(0);
                var later = h.Render();
                float Diff(Color[] a, Color[] b) => a.Select((c, i) => Mathf.Abs(c.grayscale - b[i].grayscale)).Average();
                Assert.That(Diff(dry, wet), Is.GreaterThan(.005f), "A soaked floor must look different from a dry one.");
                Assert.That(Diff(wet, later), Is.GreaterThan(.0005f), "Rain drops and ripples move over time.");
                // Feature on with no water matches the dry look.
                controller.Raining = false; controller.OverrideWetness(0); for (int i = 0; i < 10; i++) controller.Tick(1);
                material.DisableKeyword("_AERO_WETNESS");
                var off = h.Render();
                material.EnableKeyword("_AERO_WETNESS");
                var zero = h.Render();
                Assert.That(Diff(off, zero), Is.LessThan(.002f), "Wetness 0 must not change the accepted marble.");
            }
            finally { Object.DestroyImmediate(go); RestoreSceneDriver(); }
        }
    }
}
