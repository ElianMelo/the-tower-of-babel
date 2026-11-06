using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroVolumetricCloudsEditModeTests
    {
        const string Root = "Assets/_Project/Environment/Clouds";
        const string LibraryPath = Root + "/AeroCloudWeather.asset";
        const string RendererPath = "Assets/Settings/PC_Renderer.asset";
        const string ScenePath = "Assets/_Project/Scenes/Tower.unity";

        static AeroCloudWeatherLibrary Library => AssetDatabase.LoadAssetAtPath<AeroCloudWeatherLibrary>(LibraryPath);
        static AeroVolumetricCloudsRendererFeature Feature =>
            AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath).rendererFeatures.OfType<AeroVolumetricCloudsRendererFeature>().Single();

        [Test]
        public void Look_BlendsEveryValueAndTakesTheShortWayRoundForWind()
        {
            var a = AeroCloudLook.Default; a.coverage = .2f; a.windDirection = 350; a.litTint = Color.white; a.heightVariation = 0;
            var b = AeroCloudLook.Default; b.coverage = 1; b.windDirection = 10; b.litTint = Color.black; b.heightVariation = 400;
            var mid = AeroCloudLook.Lerp(a, b, .5f);
            Assert.That(mid.coverage, Is.EqualTo(.6f).Within(1e-5f));
            Assert.That(mid.heightVariation, Is.EqualTo(200).Within(1e-3f));
            Assert.That(Mathf.DeltaAngle(mid.windDirection, 0), Is.EqualTo(0).Within(1e-3f), "Wind turns through north, not all the way round.");
            Assert.That(mid.litTint.r, Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(AeroCloudLook.Lerp(a, b, 2).coverage, Is.EqualTo(1), "Blend is clamped.");

            var wild = AeroCloudLook.Default; wild.coverage = 3; wild.density = -1; wild.cloudSize = 0; wild.windDirection = -90; wild.baseAltitude = 0;
            var safe = wild.Sanitized();
            Assert.That(safe.coverage, Is.EqualTo(1)); Assert.That(safe.density, Is.EqualTo(0)); Assert.That(safe.cloudSize, Is.EqualTo(.25f));
            Assert.That(safe.windDirection, Is.EqualTo(270)); Assert.That(safe.baseAltitude, Is.GreaterThanOrEqualTo(50));
            wild.heightVariation = -5; Assert.That(wild.Sanitized().heightVariation, Is.EqualTo(0));
            wild.heightVariation = 5000; Assert.That(wild.Sanitized().heightVariation, Is.EqualTo(1000));
        }

        [Test]
        public void ShippedPresets_CoverClearDaysAndDarkHeavyRain()
        {
            var library = Library;
            Assert.That(library, Is.Not.Null, "Run Tower of Babel/Environment/Clouds/Author volumetric clouds.");
            var clear = library.clearDay.Select(e => e.preset).ToArray();
            var rain = library.rain.Select(e => e.preset).ToArray();
            Assert.That(clear.Length, Is.GreaterThanOrEqualTo(8)); Assert.That(rain.Length, Is.GreaterThanOrEqualTo(3));
            Assert.That(clear.Concat(rain).All(p => p != null), Is.True);
            Assert.That(clear.All(p => p.weather == AeroCloudWeather.ClearDay), Is.True);
            Assert.That(rain.All(p => p.weather != AeroCloudWeather.ClearDay), Is.True);
            foreach (var p in clear)
            {
                Assert.That(p.look.coverage, Is.LessThanOrEqualTo(.65f), $"{p.name}: clear days stay sparse.");
                Assert.That(p.look.darkness, Is.Zero, $"{p.name}: clear days are not stormy.");
            }
            float clearDensity = clear.Average(p => p.look.density), clearSun = clear.Average(p => p.look.sunLight);
            foreach (var p in rain)
            {
                Assert.That(p.look.coverage, Is.GreaterThanOrEqualTo(.8f), $"{p.name}: rain skies are heavy.");
                Assert.That(p.look.density, Is.GreaterThan(clearDensity), $"{p.name}: rain clouds are denser.");
                Assert.That(p.look.darkness, Is.GreaterThanOrEqualTo(.5f), $"{p.name}: rain clouds are dark.");
                Assert.That(p.look.sunLight, Is.LessThan(clearSun), $"{p.name}");
                Assert.That(p.look.baseAltitude, Is.LessThan(clear.Min(c => c.look.baseAltitude) + 1), $"{p.name}: rain decks hang low.");
            }
        }

        [Test]
        public void ShippedPresets_HangLowEnoughForTheTopFloorsToEnterAndStaySoft()
        {
            // Review 2026-11-05: clouds were too high (twice), too flat at the base and too solid. The latest tower
            // floors reach into the deck; the island's hills (up to ~56 m) stay below it.
            const float towerTop = 32 * 6; // 32 floors of 6 m
            const float hills = 56;
            var library = Library;
            var all = library.clearDay.Concat(library.rain).Select(e => e.preset).ToArray();
            foreach (var p in all)
                Assert.That(p.look.baseAltitude, Is.GreaterThan(hills + 30), $"{p.name}: the deck clears the hills.");
            foreach (var p in library.clearDay.Select(e => e.preset))
            {
                bool highLayer = p.look.cloudType < .25f; // veils and fleece belong higher up
                if (highLayer) { Assert.That(p.look.baseAltitude, Is.LessThanOrEqualTo(1000), $"{p.name}: high layers stay near."); continue; }
                Assert.That(p.look.baseAltitude, Is.InRange(100, towerTop - 20), $"{p.name}: the top floors reach into the clouds.");
                Assert.That(p.look.baseAltitude + p.look.thickness, Is.GreaterThan(towerTop), $"{p.name}: the deck covers the top floor.");
                Assert.That(p.look.density, Is.LessThanOrEqualTo(1.2f), $"{p.name}: clear-day clouds stay see-through.");
                Assert.That(p.look.edgeSoftness, Is.GreaterThanOrEqualTo(.25f), $"{p.name}: soft fringes.");
            }
            Assert.That(library.densityTiers.Max(t => t.densityMultiplier.y), Is.LessThanOrEqualTo(1.25f), "Dense tier stays soft enough.");
            Assert.That(Feature.extinction, Is.LessThanOrEqualTo(.01f), "Global extinction keeps fringes transparent.");
            Assert.That(AeroCloudLook.Default.baseAltitude, Is.InRange(100, towerTop - 20), "New presets start near the tower top.");

            // Review 2026-11-06: smaller clouds at different heights. Clear skies spread their clouds over 100 m or
            // more above the base; rain decks stay a low, even ceiling.
            var clearLooks = library.clearDay.Select(e => e.preset.look).ToArray();
            foreach (var p in library.clearDay.Select(e => e.preset))
                Assert.That(p.look.heightVariation, Is.GreaterThanOrEqualTo(100), $"{p.name}: clouds float at different heights.");
            foreach (var p in library.rain.Select(e => e.preset))
                Assert.That(p.look.heightVariation, Is.LessThanOrEqualTo(150), $"{p.name}: the rain deck stays low.");
            Assert.That(Feature.shapeTile, Is.LessThanOrEqualTo(1500), "Small cloud shapes.");
            Assert.That(Feature.coverageTile, Is.LessThanOrEqualTo(8000), "Small cloud fields.");
            float[] sizes = clearLooks.Select(l => l.cloudSize).OrderBy(x => x).ToArray();
            Assert.That(sizes[sizes.Length / 2], Is.LessThanOrEqualTo(1), "Most clear-day presets are small to medium.");

            // The shader rounds bases with a per-column ramp measured in metres (not a fraction of the layer), so
            // bases stay at the preset altitude and the top floors really are inside the cloud.
            string hlsl = File.ReadAllText("Assets/_Project/Shaders/Aero/AeroClouds.hlsl");
            StringAssert.Contains("float AeroBaseRamp(float metresAboveBase, float thickness, float type, float lift)", hlsl);
            StringAssert.Contains("min(lerp(50, 90, type), thickness * .3)", hlsl);
        }

        [Test]
        public void Tiers_AreOrderedSmallToLargeAndSoftToDense()
        {
            var library = Library;
            var amounts = library.amountTiers.OrderBy(t => t.coverage.x).ToArray();
            Assert.That(amounts.Select(t => t.name), Is.EqualTo(new[] { "Amount Small", "Amount Medium", "Amount Large" }));
            for (int i = 1; i < amounts.Length; i++) Assert.That(amounts[i].coverage.x, Is.GreaterThan(amounts[i - 1].coverage.y), "Amount ranges do not overlap.");
            var densities = library.densityTiers.OrderBy(t => t.densityMultiplier.x).ToArray();
            Assert.That(densities.Select(t => t.name), Is.EqualTo(new[] { "Density Soft", "Density Medium", "Density Dense" }));
            for (int i = 1; i < densities.Length; i++) Assert.That(densities[i].densityMultiplier.x, Is.GreaterThan(densities[i - 1].densityMultiplier.y));
            Assert.That(densities[0].erosionOffset.x, Is.GreaterThan(0), "Soft clouds are wispier."); Assert.That(densities[2].erosionOffset.y, Is.LessThan(0));
        }

        [Test]
        public void ClearDayRolls_VaryAmountAndDensityAndAreRepeatableBySeed()
        {
            var library = Library;
            var a = new System.Random(7); var b = new System.Random(7);
            var amounts = new System.Collections.Generic.HashSet<string>(); var densities = new System.Collections.Generic.HashSet<string>();
            var presets = new System.Collections.Generic.HashSet<string>();
            for (int i = 0; i < 200; i++)
            {
                var roll = library.RollClearDay(a); var twin = library.RollClearDay(b);
                Assert.That(twin.look.coverage, Is.EqualTo(roll.look.coverage), "Same seed, same sky.");
                Assert.That(roll.amount, Is.Not.Null); Assert.That(roll.density, Is.Not.Null);
                Assert.That(roll.look.coverage, Is.InRange(roll.amount.coverage.x, roll.amount.coverage.y), "Amount tier sets the coverage.");
                float baseDensity = roll.preset.look.density;
                Assert.That(roll.look.density, Is.InRange(baseDensity * roll.density.densityMultiplier.x - 1e-4f, Mathf.Min(3, baseDensity * roll.density.densityMultiplier.y) + 1e-4f));
                amounts.Add(roll.amount.name); densities.Add(roll.density.name); presets.Add(roll.preset.name);
                Assert.That(roll.Description, Does.Contain(roll.preset.name));
            }
            Assert.That(amounts.Count, Is.EqualTo(3)); Assert.That(densities.Count, Is.EqualTo(3));
            Assert.That(presets.Count, Is.GreaterThanOrEqualTo(6), "Many clear-day styles come up.");
            for (int i = 0; i < 50; i++) Assert.That(library.RollRain(a).preset.weather, Is.Not.EqualTo(AeroCloudWeather.ClearDay));
        }

        [Test]
        public void WeightedPick_NeverPicksZeroWeight()
        {
            var items = new[] { "never", "always" };
            var random = new System.Random(3);
            for (int i = 0; i < 100; i++) Assert.That(AeroCloudRandom.Pick(random, items, s => s == "never" ? 0 : 1), Is.EqualTo("always"));
            Assert.That(AeroCloudRandom.Pick(random, items, _ => 0), Is.Null);
        }

        sealed class Rig : System.IDisposable
        {
            public readonly GameObject Root;
            public readonly AeroDayNightCycle Cycle;
            public readonly AeroWetnessController Wetness;
            public readonly AeroVolumetricClouds Clouds;
            readonly AeroDayNightProfile dayProfile; readonly AeroWetnessProfile wetProfile;
            readonly Light oldSun;

            public Rig(int seed = 11)
            {
                oldSun = RenderSettings.sun;
                Root = new GameObject("Cloud rig") { hideFlags = HideFlags.HideAndDontSave }; Root.SetActive(false);
                dayProfile = ScriptableObject.CreateInstance<AeroDayNightProfile>();
                wetProfile = ScriptableObject.CreateInstance<AeroWetnessProfile>(); wetProfile.rainFade = 2;
                Cycle = Root.AddComponent<AeroDayNightCycle>(); Cycle.profile = dayProfile;
                Cycle.sun = new GameObject("sun") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>(); Cycle.sun.type = LightType.Directional;
                Cycle.moon = new GameObject("moon") { hideFlags = HideFlags.HideAndDontSave }.AddComponent<Light>(); Cycle.moon.type = LightType.Directional;
                Cycle.sun.transform.SetParent(Root.transform); Cycle.moon.transform.SetParent(Root.transform);
                Wetness = Root.AddComponent<AeroWetnessController>(); Wetness.profile = wetProfile;
                Clouds = Root.AddComponent<AeroVolumetricClouds>();
                Clouds.library = Library; Clouds.dayNight = Cycle; Clouds.wetness = Wetness; Clouds.seed = seed;
            }

            public void Dispose()
            {
                RenderSettings.sun = oldSun;
                Object.DestroyImmediate(Root); Object.DestroyImmediate(dayProfile); Object.DestroyImmediate(wetProfile);
            }
        }

        [Test]
        public void Rain_BlendsTheSkyToTheRainPresetAndANewClearSkyFollows()
        {
            using var rig = new Rig();
            rig.Cycle.ApplyTime(.5f);
            rig.Clouds.Tick(0);
            var clear = rig.Clouds.Look; string firstSky = rig.Clouds.ClearRoll.Description;
            Assert.That(rig.Clouds.RainBlend, Is.Zero);
            Assert.That(clear.coverage, Is.LessThan(.7f));

            rig.Wetness.Raining = true; rig.Wetness.Tick(1); rig.Clouds.Tick(.1f);
            float half = rig.Clouds.RainBlend;
            Assert.That(half, Is.InRange(.3f, .99f), "Clouds thicken as the rain fades in.");
            rig.Wetness.Tick(5); rig.Clouds.Tick(.1f);
            var rain = rig.Clouds.Look;
            Assert.That(rig.Clouds.RainBlend, Is.EqualTo(1).Within(1e-4f));
            Assert.That(rig.Clouds.RainRoll.preset.weather, Is.Not.EqualTo(AeroCloudWeather.ClearDay));
            Assert.That(rain.coverage, Is.EqualTo(rig.Clouds.RainRoll.preset.look.coverage).Within(1e-4f), "Full rain shows the rain preset.");
            Assert.That(rain.darkness, Is.GreaterThan(clear.darkness)); Assert.That(rig.Clouds.Description, Does.StartWith("Rain:"));

            rig.Wetness.Raining = false; rig.Wetness.Tick(5); rig.Clouds.Tick(.1f);
            Assert.That(rig.Clouds.RainBlend, Is.Zero);
            Assert.That(rig.Clouds.ClearTransition, Is.LessThan(1), "A newly rolled clear sky blends in after the rain.");
            for (int i = 0; i < 100; i++) rig.Clouds.Tick(1); // past the 90 s blend, before the next timed re-roll
            Assert.That(rig.Clouds.ClearTransition, Is.EqualTo(1));
            Assert.That(rig.Clouds.Look.darkness, Is.Zero);
            Assert.That(firstSky, Is.Not.Empty);
        }

        [Test]
        public void ClearSky_RerollsOnTimerAndAtDawn()
        {
            using var rig = new Rig(5);
            rig.Cycle.timeOfDay = .5f; rig.Clouds.Tick(0);
            float timer = rig.Clouds.SecondsToReroll;
            Assert.That(timer, Is.InRange(Library.rerollSeconds.x, Library.rerollSeconds.y));
            rig.Clouds.Tick(timer + .1f);
            Assert.That(rig.Clouds.ClearTransition, Is.LessThan(1), "Timer expiry starts a transition to a new sky.");
            Assert.That(rig.Clouds.SecondsToReroll, Is.GreaterThan(timer * .25f), "...and restarts the timer.");
            rig.Clouds.Tick(Library.transitionSeconds + 1);
            Assert.That(rig.Clouds.ClearTransition, Is.EqualTo(1));

            rig.Cycle.timeOfDay = .1f; rig.Clouds.Tick(1);
            rig.Cycle.timeOfDay = .32f; rig.Clouds.Tick(1);
            Assert.That(rig.Clouds.ClearTransition, Is.LessThan(1), "Each sunrise brings a new sky.");
        }

        [Test]
        public void Lighting_FollowsSunByDayMoonByNightAndShadesTheGround()
        {
            using var rig = new Rig();
            rig.Cycle.ApplyTime(.5f); rig.Clouds.Tick(0);
            Assert.That(rig.Clouds.LitByMoon, Is.False);
            Assert.That(Vector3.Dot(rig.Clouds.LightDirection, -rig.Cycle.sun.transform.forward), Is.GreaterThan(.999f));
            Color day = rig.Clouds.LightColor;
            Assert.That(day.grayscale, Is.GreaterThan(.5f));

            rig.Cycle.ApplyTime(0); rig.Clouds.Tick(0);
            Assert.That(rig.Clouds.LitByMoon, Is.True, "Moonlit clouds at night.");
            Assert.That(Vector3.Dot(rig.Clouds.LightDirection, -rig.Cycle.moon.transform.forward), Is.GreaterThan(.999f));
            Assert.That(rig.Clouds.LightColor.grayscale, Is.LessThan(day.grayscale * .5f)); Assert.That(rig.Clouds.LightColor.grayscale, Is.GreaterThan(0));
            Assert.That(rig.Clouds.LightColor.b, Is.GreaterThan(rig.Clouds.LightColor.r), "Moonlight is blue.");

            // The sky's cloud shade dims only the sun on the ground.
            rig.Cycle.ApplyTime(.5f); rig.Clouds.Tick(0);
            float shaded = rig.Cycle.sun.intensity;
            rig.Cycle.cloudShade = 0; rig.Cycle.ApplyTime(.5f);
            Assert.That(shaded, Is.LessThan(rig.Cycle.sun.intensity));
            Assert.That(shaded, Is.GreaterThan(rig.Cycle.sun.intensity * .5f), "Clear days only dim the sun slightly.");
        }

        [Test]
        public void PreviewPreset_OverridesWeatherForLiveTuning()
        {
            using var rig = new Rig();
            var preset = ScriptableObject.CreateInstance<AeroCloudPreset>();
            try
            {
                preset.look.coverage = .77f; preset.look.cloudType = .9f;
                rig.Clouds.previewPreset = preset;
                rig.Wetness.Raining = true; rig.Wetness.Tick(5); rig.Clouds.Tick(.1f);
                Assert.That(rig.Clouds.Look.coverage, Is.EqualTo(.77f)); Assert.That(rig.Clouds.Look.cloudType, Is.EqualTo(.9f));
                Assert.That(rig.Clouds.Description, Does.Contain("Preview"));
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void Wind_DriftsTheShapesDownwind()
        {
            using var rig = new Rig();
            var preset = ScriptableObject.CreateInstance<AeroCloudPreset>();
            try
            {
                preset.look.windDirection = 90; preset.look.windSpeed = 10; preset.look.evolution = 2;
                rig.Clouds.previewPreset = preset; rig.Clouds.Tick(0);
                var start = rig.Clouds.WindOffset;
                rig.Clouds.Tick(3);
                var moved = rig.Clouds.WindOffset - start;
                Assert.That(moved.x, Is.EqualTo(30).Within(1e-3f), "East wind drifts +x."); Assert.That(moved.y, Is.EqualTo(0).Within(1e-3f));
                Assert.That(moved.z, Is.EqualTo(6).Within(1e-3f));
            }
            finally { Object.DestroyImmediate(preset); }
        }

        [Test]
        public void Noise_TilesSeamlesslyAndCoverageMatchesTheSkyFraction()
        {
            var p = new Vector3(.123f, .456f, .789f);
            Assert.That(AeroCloudNoise.Perlin(p + Vector3.one, 4, 3), Is.EqualTo(AeroCloudNoise.Perlin(p, 4, 3)).Within(1e-4f));
            Assert.That(AeroCloudNoise.Worley(p + new Vector3(1, 0, 1), 8, 3), Is.EqualTo(AeroCloudNoise.Worley(p, 8, 3)).Within(1e-4f));
            Assert.That(AeroCloudNoise.Worley(new Vector3(.9999f, .5f, .5f), 6, 1), Is.EqualTo(AeroCloudNoise.Worley(new Vector3(-.0001f, .5f, .5f), 6, 1)).Within(1e-3f), "Opposite faces meet.");

            var coverage = AeroCloudNoise.Coverage(64, 9);
            Assert.That(coverage, Is.EqualTo(AeroCloudNoise.Coverage(64, 9)), "Deterministic.");
            int above = 0; for (int i = 0; i < coverage.Length; i += 2) if (coverage[i] >= 255 * .6f) above++;
            Assert.That(above / (coverage.Length / 2f), Is.EqualTo(.4f).Within(.02f), "Coverage map is equalized: threshold 0.6 covers 40% of the sky.");

            var shape = AeroCloudNoise.Shape(16, 2);
            Assert.That(shape.Min(), Is.EqualTo(0)); Assert.That(shape.Max(), Is.EqualTo(255));
        }

        [Test]
        public void Authoring_WiresTheRendererFeatureAndTheTowerScene()
        {
            var feature = Feature;
            Assert.That(feature.isActive, Is.True);
            Assert.That(feature.Ready || feature.cloudShader != null, Is.True);
            Assert.That(feature.cloudShader, Is.EqualTo(Shader.Find("Hidden/TowerOfBabel/Volumetric Clouds")));
            Assert.That(feature.quality, Is.Not.Null); Assert.That(feature.shapeNoise, Is.Not.Null);
            Assert.That(feature.detailNoise, Is.Not.Null); Assert.That(feature.coverageMap, Is.Not.Null);
            Assert.That(feature.shapeNoise.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(feature.shapeNoise.mipmapCount, Is.GreaterThan(1));
            Assert.That(ShaderUtil.ShaderHasError(feature.cloudShader), Is.False);
            Assert.That(feature.cloudShader.passCount, Is.EqualTo(3));
            Assert.That(feature.quality.Downsample, Is.GreaterThanOrEqualTo(2), "Clouds trace below full resolution for performance.");

            string scene = File.ReadAllText(ScenePath);
            string script = AssetDatabase.AssetPathToGUID("Assets/_Project/Scripts/Rendering/Clouds/AeroVolumetricClouds.cs");
            Assert.That(scene, Does.Contain(script), "The Tower scene has an AeroVolumetricClouds.");
            Assert.That(scene, Does.Contain(AssetDatabase.AssetPathToGUID(LibraryPath)), "...using the weather library.");
            Assert.That(scene, Does.Contain(AssetDatabase.AssetPathToGUID(Root + "/AeroClearSky.mat")), "The painted-cloud skybox is replaced by a clean sky.");
        }

        [Test]
        public void Renderer_SkipsCamerasThatCannotShowClouds()
        {
            var go = new GameObject("cam") { hideFlags = HideFlags.HideAndDontSave };
            try
            {
                var camera = go.AddComponent<Camera>();
                Assert.That(AeroVolumetricCloudsRendererFeature.SupportsCamera(camera), Is.True);
                camera.orthographic = true; Assert.That(AeroVolumetricCloudsRendererFeature.SupportsCamera(camera), Is.False);
                camera.orthographic = false; camera.cameraType = CameraType.Preview;
                Assert.That(AeroVolumetricCloudsRendererFeature.SupportsCamera(camera), Is.False);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static float Rgb(Color c) => new Vector3(c.r, c.g, c.b).magnitude;

        static GameObject CloudsInScene(AeroRenderHarness h, AeroCloudPreset preset)
        {
            var go = new GameObject("Test clouds");
            SceneManager.MoveGameObjectToScene(go, h.Scene);
            var clouds = go.AddComponent<AeroVolumetricClouds>();
            clouds.library = Library; clouds.previewPreset = preset; clouds.seed = 1;
            clouds.Tick(0);
            return go;
        }

        [Test]
        public void Gpu_CloudsFillTheSkyAboveTheHorizonOnlyAndHideBehindNearGeometry()
        {
            using var h = new AeroRenderHarness();
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            preset.look.coverage = 1; preset.look.density = 2; preset.look.edgeSoftness = .5f; preset.look.hazeStrength = 0;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 2, 0), Quaternion.identity);
            h.Camera.fieldOfView = 60; h.Camera.farClipPlane = 5000;
            var wall = h.Spawn(PrimitiveType.Quad, h.Own(new Material(Shader.Find("Universal Render Pipeline/Unlit"))), new Vector3(-30, 20, 60), new Vector3(60, 60, 1));
            wall.GetComponent<Renderer>().sharedMaterial.SetColor("_BaseColor", new Color(.9f, .1f, .1f));

            var clear = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var cloudy = h.Render();
            int s = h.Size;
            Color skyBefore = h.Pixel(clear, s * 3 / 4, s * 7 / 8), skyAfter = h.Pixel(cloudy, s * 3 / 4, s * 7 / 8);
            Assert.That(Mathf.Abs(skyAfter.grayscale - skyBefore.grayscale) + Mathf.Abs(skyAfter.b - skyBefore.b), Is.GreaterThan(.05f),
                $"Overcast covers the upper sky ({skyBefore} -> {skyAfter}).");
            Assert.That(skyAfter.a, Is.EqualTo(skyBefore.a).Within(1e-3f), "The composite leaves the target's alpha alone.");
            Color below = h.Pixel(cloudy, s * 3 / 4, s / 8), belowBefore = h.Pixel(clear, s * 3 / 4, s / 8);
            Assert.That(Rgb(below - belowBefore), Is.LessThan(.01f), "No clouds below the horizon (rays hit the planet).");
            Color wallBefore = h.Pixel(clear, s / 4, s * 7 / 8), wallAfter = h.Pixel(cloudy, s / 4, s * 7 / 8);
            Assert.That(Rgb(wallAfter - wallBefore), Is.LessThan(.02f), "Geometry in front of the layer stays clear.");

            cloudsObject.GetComponent<AeroVolumetricClouds>().enabled = false;
            var off = h.Render();
            Assert.That(Rgb(h.Pixel(off, s * 3 / 4, s * 7 / 8) - skyBefore), Is.LessThan(.01f), "Disabled clouds draw nothing.");
            Object.DestroyImmediate(cloudsObject);
        }

        [Test]
        public void Gpu_ClearDaysCoverRoughlyTheirCoverageShareEvenAtTheSoftestTier()
        {
            // Review 2026-11-06: after softening, clear skies showed almost no clouds (Fair Weather covered 8% of the
            // sky at coverage 0.38) because the coverage map and the shape noise each thresholded the area.
            // Measured top-down at noon over nine spots per preset, at the softest density tier.
            using var h = new AeroRenderHarness(64);
            var library = Library;
            float softest = library.densityTiers.Min(t => t.densityMultiplier.x);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 90; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 3000, 0), Quaternion.Euler(90, 0, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            var report = new System.Text.StringBuilder();
            try
            {
                foreach (var source in library.clearDay.Select(e => e.preset))
                {
                    preset.look = source.look; preset.look.density *= softest; clouds.Tick(0);
                    float size = Mathf.Max(1, preset.look.cloudSize), height = preset.look.baseAltitude + preset.look.thickness + 1500 * size;
                    int visible = 0, total = 0;
                    for (int x = -1; x <= 1; x++)
                    for (int z = -1; z <= 1; z++)
                    {
                        h.Camera.transform.position = new Vector3(x * 6100 + z * 1300, 0, z * 5900 - x * 900) * size + Vector3.up * height;
                        h.Render(); var image = h.Render(); // let the temporal history settle after the jump
                        for (int i = 0; i < image.Length; i++, total++)
                            if (Rgb(image[i] - background[i]) > .08f) visible++;
                    }
                    float share = visible / (float)total, coverage = preset.look.coverage;
                    report.AppendLine($"{source.name}: coverage {coverage:0.00}, visible {share:0.00}");
                    // Thin high sheets (veil, fleece) are short paths seen from straight above, so their bar is lower.
                    float least = preset.look.cloudType < .25f ? .15f : .3f;
                    Assert.That(share, Is.GreaterThan(coverage * least), $"{source.name}: clouds stay visible at the softest tier.\n{report}");
                    Assert.That(share, Is.LessThan(coverage + .2f), $"{source.name}: clear skies stay open.\n{report}");
                }
                TestContext.WriteLine(report.ToString());
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_HeightVariationLiftsSomeCloudsAboveTheRestOfTheLayer()
        {
            // A camera just above a flat layer's top sees nothing when it looks up. With height variation, each
            // cloud floats at its own height, so some now pass overhead while others stay below.
            using var h = new AeroRenderHarness(64);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            preset.look.coverage = 1; preset.look.density = 2; preset.look.edgeSoftness = .5f; preset.look.hazeStrength = 0;
            preset.look.baseAltitude = 300; preset.look.thickness = 150; preset.look.heightVariation = 0;
            h.Camera.fieldOfView = 90; h.Camera.farClipPlane = 65536;
            var eye = new Vector3(0, preset.look.baseAltitude + preset.look.thickness + 40, 0);
            // Up spans horizon to zenith (km of sky, not just the patch overhead); down looks straight at the layer.
            var up = Quaternion.Euler(-45, 0, 0); var down = Quaternion.Euler(90, 0, 0);
            h.Camera.transform.SetPositionAndRotation(eye, up);
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            float Share(Quaternion rotation)
            {
                clouds.Tick(0);
                h.Camera.transform.SetPositionAndRotation(eye, rotation);
                h.Render(); var image = h.Render();
                // Skip the bottom rows: at -45 degrees they graze the horizon, where the lower layer shows through.
                int skip = h.Size / 8 * h.Size;
                return image.Skip(skip).Where((c, i) => Rgb(c - background[skip + i]) > .08f).Count() / (float)(image.Length - skip);
            }
            try
            {
                float flatUp = Share(up), flatDown = Share(down);
                preset.look.heightVariation = 400;
                float variedUp = Share(up), variedDown = Share(down);
                string report = $"flat up {flatUp:0.00} down {flatDown:0.00}; varied up {variedUp:0.00} down {variedDown:0.00}";
                Assert.That(flatDown, Is.GreaterThan(.5f), "The flat layer lies below the camera. " + report);
                Assert.That(flatUp, Is.LessThan(.01f), "Nothing above a flat layer's top. " + report);
                Assert.That(variedUp, Is.GreaterThan(.1f), "Some clouds float above the rest. " + report);
                Assert.That(variedDown, Is.GreaterThan(.2f), "Others stay low. " + report);
                TestContext.WriteLine(report);
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_InsideACloudNearBlocksStayReadableAndDistanceFogsOver()
        {
            // The top tower floors reach into the layer: the player stands inside the cloud.
            using var h = new AeroRenderHarness();
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            preset.look.coverage = 1; preset.look.density = 2; preset.look.edgeSoftness = .5f; preset.look.hazeStrength = 0;
            float inside = preset.look.baseAltitude + preset.look.thickness * .35f;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, inside, 0), Quaternion.identity);
            h.Camera.fieldOfView = 60; h.Camera.farClipPlane = 5000;
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var nearMaterial = h.Own(new Material(unlit)); nearMaterial.SetColor("_BaseColor", new Color(.9f, .1f, .1f));
            var farMaterial = h.Own(new Material(unlit)); farMaterial.SetColor("_BaseColor", new Color(.1f, .9f, .1f));
            h.Spawn(PrimitiveType.Quad, nearMaterial, new Vector3(-6, inside, 12), new Vector3(10, 30, 1));
            h.Spawn(PrimitiveType.Quad, farMaterial, new Vector3(250, inside, 600), new Vector3(500, 400, 1));

            var clear = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var cloudy = h.Render();
            int s = h.Size;
            Color nearBefore = h.Pixel(clear, s / 4, s / 2), nearAfter = h.Pixel(cloudy, s / 4, s / 2);
            Color farBefore = h.Pixel(clear, s * 7 / 8, s / 2), farAfter = h.Pixel(cloudy, s * 7 / 8, s / 2);
            Assert.That(nearBefore.r, Is.GreaterThan(.5f), "Near block is in view."); Assert.That(farBefore.g, Is.GreaterThan(.5f), "Far wall is in view.");
            Assert.That(Rgb(nearAfter - nearBefore), Is.LessThan(.25f), $"A block 12 m away stays readable ({nearBefore} -> {nearAfter}).");
            Assert.That(Rgb(farAfter - farBefore), Is.GreaterThan(Rgb(nearAfter - nearBefore) + .2f),
                $"Cloud thickens with distance ({farBefore} -> {farAfter}).");
            Object.DestroyImmediate(cloudsObject);
        }

        [Test]
        public void Gpu_LookingDownFromInsideTheCloudFogsTheWaterBelow()
        {
            // Water is transparent and draws after the main composite; from inside the deck the cloud must still
            // lie over the sea (the late composite), not under it.
            using var h = new AeroRenderHarness();
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            preset.look.coverage = 1; preset.look.density = 2; preset.look.edgeSoftness = .5f; preset.look.hazeStrength = 0;
            float inside = preset.look.baseAltitude + preset.look.thickness * .35f;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, inside, 0), Quaternion.Euler(35, 0, 0));
            h.Camera.fieldOfView = 40; h.Camera.farClipPlane = 5000;
            // Same transparent setup as the rain drops (Particles/Unlit, alpha blended, no depth write).
            var water = h.Own(new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")));
            water.SetFloat("_Surface", 1); water.SetFloat("_Blend", 0); water.SetFloat("_ZWrite", 0); water.SetFloat("_Cull", 0);
            water.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            water.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            water.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            water.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            water.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); water.SetOverrideTag("RenderType", "Transparent");
            water.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            water.SetColor("_BaseColor", new Color(.05f, .2f, .9f, .95f));
            h.Spawn(PrimitiveType.Quad, water, new Vector3(0, 0, 0), new Vector3(4000, 4000, 1)).transform.rotation = Quaternion.Euler(90, 0, 0);

            var clear = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            Assert.That(AeroVolumetricCloudsRendererFeature.NeedsLateComposite(h.Camera, cloudsObject.GetComponent<AeroVolumetricClouds>()), Is.True);
            var cloudy = h.Render();
            int s = h.Size;
            Color before = h.Pixel(clear, s / 2, s / 2), after = h.Pixel(cloudy, s / 2, s / 2);
            Assert.That(before.b, Is.GreaterThan(.5f), $"Water is in view ({before}).");
            Assert.That(Rgb(after - before), Is.GreaterThan(.2f), $"Cloud between the camera and the sea fogs the water ({before} -> {after}).");
            Assert.That(after.a, Is.EqualTo(before.a).Within(1e-3f), "The late composite leaves alpha alone too.");
            Object.DestroyImmediate(cloudsObject);
        }
    }
}
