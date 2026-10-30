using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroPostProfileEditModeTests
    {
        const string Restrained = "Assets/_Project/UI/FrutigerAero/AeroAtmosphere.asset";
        const string Bright = "Assets/_Project/Environment/Lighting/AeroPost_Bright.asset";
        const string Y2K = "Assets/_Project/Environment/Lighting/AeroPost_Y2K.asset";

        sealed class MemoryStore : IAeroSettingsStore
        {
            public readonly Dictionary<string, int> values = new();
            public int GetInt(string key, int fallback) => values.TryGetValue(key, out var v) ? v : fallback;
            public void SetInt(string key, int value) => values[key] = value;
        }

        static VolumeProfile Load(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            return profile;
        }

        [Test]
        public void Looks_GradeTowardFrutigerAeroWithoutBlowingOut()
        {
            foreach (var path in new[] { Restrained, Bright, Y2K })
            {
                var p = Load(path);
                Assert.That(p.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.Neutral, Is.True, path + " keeps cool whites with Neutral");
                Assert.That(p.TryGet<Bloom>(out var bloom), Is.True);
                Assert.That(bloom.threshold.value, Is.GreaterThanOrEqualTo(.88f), path + " bloom threshold keeps marble readable");
                Assert.That(bloom.clamp.value, Is.LessThanOrEqualTo(16));
                Assert.That(p.TryGet<WhiteBalance>(out var white) && white.temperature.value < 0, Is.True, path + " is cool");
                Assert.That(p.TryGet<ColorAdjustments>(out var color) && color.saturation.value > 0, Is.True);
                Assert.That(p.TryGet<Vignette>(out var vignette) && vignette.intensity.value <= .2f, Is.True, "Minimal vignette");
                Assert.That(p.TryGet<ScreenSpaceLensFlare>(out var flare) && flare.intensity.value <= .08f, Is.True);
            }
            var restrained = Load(Restrained);
            restrained.TryGet<Bloom>(out var accepted);
            Assert.That(accepted.intensity.value, Is.InRange(.3f, .7f), "Restrained stays close to the accepted look.");
            Load(Bright).TryGet<Bloom>(out var brightBloom);
            Assert.That(brightBloom.intensity.value, Is.GreaterThan(accepted.intensity.value), "Bright Aero glows more.");
            Assert.That(Load(Y2K).TryGet<FilmGrain>(out var grain) && grain.active && grain.intensity.value > 0, Is.True, "Y2K adds grain.");
            Assert.That(Load(Bright).TryGet<FilmGrain>(out var noGrain) && noGrain.active, Is.False, "Bright Aero is clean.");
        }

        [Test]
        public void PcGradesInHdrAndMobileStaysLdr()
        {
            var pc = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            var mobile = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            Assert.That(pc.colorGradingMode, Is.EqualTo(ColorGradingMode.HighDynamicRange));
            Assert.That(mobile.colorGradingMode, Is.EqualTo(ColorGradingMode.LowDynamicRange));
        }

        [Test]
        public void Switcher_SelectsCyclesAndRemembersTheLook()
        {
            var go = new GameObject("Post switcher test") { hideFlags = HideFlags.HideAndDontSave };
            go.SetActive(false);
            var volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = -100;
            var switcher = go.AddComponent<AeroPostProfileSwitcher>();
            switcher.restrained.profile = Load(Restrained); switcher.brightAero.profile = Load(Bright); switcher.y2k.profile = Load(Y2K);
            var store = new MemoryStore(); switcher.Store = store; switcher.crossfadeSeconds = 0;
            go.SetActive(true);
            try
            {
                switcher.Set(AeroPostLook.BrightAero);
                Assert.That(volume.sharedProfile, Is.EqualTo(switcher.brightAero.profile));
                Assert.That(store.values[AeroPostProfileSwitcher.PreferenceKey], Is.EqualTo((int)AeroPostLook.BrightAero));
                Assert.That(switcher.Cycle(), Is.EqualTo(AeroPostLook.Y2K));
                Assert.That(volume.sharedProfile, Is.EqualTo(switcher.y2k.profile));
                Assert.That(switcher.Cycle(), Is.EqualTo(AeroPostLook.Restrained));
                Assert.That(store.values[AeroPostProfileSwitcher.PreferenceKey], Is.EqualTo((int)AeroPostLook.Restrained));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TowerScene_GlobalVolumeHasTheSwitcherAndTheExitVolumeStillWins()
        {
            TowerSceneAccess.With(scene =>
            {
                var volumes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).ToArray();
                var global = volumes.Single(v => v.isGlobal);
                var switcher = global.GetComponent<AeroPostProfileSwitcher>();
                Assert.That(switcher, Is.Not.Null);
                Assert.That(switcher.restrained.profile, Is.EqualTo(Load(Restrained)));
                Assert.That(switcher.brightAero.profile, Is.EqualTo(Load(Bright)));
                Assert.That(switcher.y2k.profile, Is.EqualTo(Load(Y2K)));
                Assert.That(global.sharedProfile, Is.EqualTo(Load(Restrained)), "The accepted look stays the default.");
                var exit = volumes.Single(v => !v.isGlobal);
                Assert.That(exit.priority, Is.GreaterThan(global.priority));
            });
        }

        [Test]
        public void BrightLook_KeepsLitWhiteMarbleBelowClipping()
        {
            using var h = new AeroRenderHarness(128);
            var marble = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            for (int x = -2; x <= 2; x++) for (int z = -1; z <= 3; z++) h.Spawn(mesh, marble, new Vector3(x * 4, 0, z * 4));
            h.Camera.transform.position = new Vector3(0, 3, -4); h.Camera.transform.LookAt(new Vector3(0, 0, 4));
            h.Camera.allowHDR = true;
            var data = h.Camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.volumeLayerMask = 1 << AeroRenderHarness.Layer;
            var go = new GameObject("Bright review volume") { layer = AeroRenderHarness.Layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, h.Scene);
            var volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100; volume.sharedProfile = Load(Bright);
            var image = h.Render();
            float clipped = image.Count(c => c.r > .995f && c.g > .995f && c.b > .995f) / (float)image.Length;
            Assert.That(clipped, Is.LessThan(.02f), "Bright Aero must keep detail in sunlit white marble.");
        }
    }
}
