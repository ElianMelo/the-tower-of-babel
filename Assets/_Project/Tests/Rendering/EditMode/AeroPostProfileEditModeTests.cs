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
        const string Library = "Assets/_Project/Environment/Lighting/AeroAtmospherePresets.asset";

        static VolumeProfile Load(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            Assert.That(profile, Is.Not.Null, path);
            return profile;
        }

        [Test]
        public void Restrained_GradesTowardFrutigerAeroWithoutBlowingOut()
        {
            var p = Load(Restrained);
            Assert.That(p.TryGet<Tonemapping>(out var tone) && tone.mode.value == TonemappingMode.Neutral, Is.True, "Neutral keeps cool whites");
            Assert.That(p.TryGet<Bloom>(out var bloom), Is.True);
            Assert.That(bloom.threshold.value, Is.GreaterThanOrEqualTo(.88f), "Bloom threshold keeps marble readable");
            Assert.That(bloom.intensity.value, Is.InRange(.3f, .7f), "Restrained stays close to the accepted look.");
            Assert.That(bloom.clamp.value, Is.LessThanOrEqualTo(16));
            Assert.That(p.TryGet<WhiteBalance>(out var white) && white.temperature.value < 0, Is.True, "Cool");
            Assert.That(p.TryGet<ColorAdjustments>(out var color) && color.saturation.value > 0, Is.True);
            Assert.That(p.TryGet<Vignette>(out var vignette) && vignette.intensity.value <= .2f, Is.True, "Minimal vignette");
            Assert.That(p.TryGet<ScreenSpaceLensFlare>(out var flare) && flare.intensity.value <= .08f, Is.True);
            Assert.That(p.TryGet<FilmGrain>(out var grain) && grain.active, Is.False, "No Y2K grain.");
            Assert.That(p.TryGet<ChromaticAberration>(out var fringe) && fringe.active, Is.False, "No colour fringe.");
        }

        [Test]
        public void RejectedLooks_AreRetired()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Project/Environment/Lighting/AeroPost_Bright.asset"), Is.Null, "Bright Aero was rejected.");
            Assert.That(AssetDatabase.LoadAssetAtPath<Object>("Assets/_Project/Environment/Lighting/AeroPost_Y2K.asset"), Is.Null, "Y2K was rejected.");
            Assert.That(System.Type.GetType("TowerOfBabel.Rendering.AeroPostProfileSwitcher, TowerOfBabel.Rendering"), Is.Null);
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
        public void TowerScene_GlobalVolumeIsRestrainedAndTheExitVolumeStillWinsOverTheGrade()
        {
            TowerSceneAccess.With(scene =>
            {
                var volumes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Volume>(true)).ToArray();
                var global = volumes.Single(v => v.isGlobal);
                Assert.That(global.sharedProfile, Is.EqualTo(Load(Restrained)), "The accepted look stays the base.");
                Assert.That(global.GetComponents<MonoBehaviour>().Any(m => m == null), Is.False, "No missing scripts left by the retired switcher.");
                var exit = volumes.Single(v => !v.isGlobal);
                var director = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AeroAtmosphereDirector>(true)).Single();
                Assert.That(director.baseVolume, Is.SameAs(global));
                Assert.That(global.priority + 1, Is.LessThan(exit.priority), "The runtime grade sits above Restrained and below the tower exit volume.");
            });
        }

        [Test]
        public void BrightestPreset_KeepsLitWhiteMarbleBelowClipping()
        {
            var library = AssetDatabase.LoadAssetAtPath<AeroAtmospherePresetLibrary>(Library);
            Assert.That(library, Is.Not.Null);
            // The permutation with the most exposure and bloom is the one most likely to clip.
            var brightest = Enumerable.Range(0, AeroAtmospherePresetLibrary.PermutationCount).Select(AeroAtmospherePresetLibrary.Permutation)
                .Where(p => p.time is AeroTimeSlot.MidDay or AeroTimeSlot.Morning or AeroTimeSlot.Afternoon)
                .OrderByDescending(p => { var l = library.Compose(p.time, p.weather, p.season).look; return l.exposure + l.bloomIntensity * .2f; }).First();

            using var h = new AeroRenderHarness(128);
            var marble = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Aero_WhiteMarble_Floor_Tile.mat");
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Models/Buildings/FrutigerAero/FloorTileComplete.asset");
            for (int x = -2; x <= 2; x++) for (int z = -1; z <= 3; z++) h.Spawn(mesh, marble, new Vector3(x * 4, 0, z * 4));
            h.Camera.transform.position = new Vector3(0, 3, -4); h.Camera.transform.LookAt(new Vector3(0, 0, 4));
            h.Camera.allowHDR = true;
            var data = h.Camera.GetUniversalAdditionalCameraData(); data.renderPostProcessing = true; data.volumeLayerMask = 1 << AeroRenderHarness.Layer;
            var go = new GameObject("Restrained review volume") { layer = AeroRenderHarness.Layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, h.Scene);
            var baseVolume = go.AddComponent<Volume>(); baseVolume.isGlobal = true; baseVolume.priority = 100; baseVolume.sharedProfile = Load(Restrained);
            var directorObject = new GameObject("Grade review") { layer = AeroRenderHarness.Layer };
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(directorObject, h.Scene);
            var director = directorObject.AddComponent<AeroAtmosphereDirector>();
            director.library = library; director.baseVolume = baseVolume; director.Store = new MemoryAeroSettingsStore();
            try
            {
                director.SetSeason(brightest.season, false, true);
                director.SetWeatherOverride(brightest.weather, true);
                director.dayNight = null; director.fallbackTimeOfDay = library.TimeOf(brightest.time);
                director.Tick(0);
                director.GradeVolume.gameObject.layer = AeroRenderHarness.Layer;
                var image = h.Render();
                float clipped = image.Count(c => c.r > .995f && c.g > .995f && c.b > .995f) / (float)image.Length;
                Assert.That(clipped, Is.LessThan(.02f), $"{brightest} must keep detail in sunlit white marble.");
            }
            finally { director.Shutdown(); }
        }
    }
}
