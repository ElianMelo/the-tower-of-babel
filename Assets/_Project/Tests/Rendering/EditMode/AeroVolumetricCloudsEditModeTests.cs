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

            // Form and growth (review 2026-11-14): cirrus fibres, streaks along the wind, ragged base, sculpted towers.
            a.fibres = 0; b.fibres = 1; a.stretch = 1; b.stretch = 5; a.raggedBase = .1f; b.raggedBase = .5f;
            a.sculpt = 0; b.sculpt = 1; a.updraft = 0; b.updraft = 4;
            mid = AeroCloudLook.Lerp(a, b, .5f);
            Assert.That(mid.fibres, Is.EqualTo(.5f).Within(1e-5f)); Assert.That(mid.stretch, Is.EqualTo(3).Within(1e-5f));
            Assert.That(mid.raggedBase, Is.EqualTo(.3f).Within(1e-5f)); Assert.That(mid.sculpt, Is.EqualTo(.5f).Within(1e-5f));
            Assert.That(mid.updraft, Is.EqualTo(2).Within(1e-5f));
            wild.fibres = 2; wild.stretch = 0; wild.raggedBase = 0; wild.sculpt = -1; wild.updraft = 99;
            safe = wild.Sanitized();
            Assert.That(safe.fibres, Is.EqualTo(1)); Assert.That(safe.stretch, Is.EqualTo(1), "Stretch 0 would divide by zero in the shader.");
            Assert.That(safe.raggedBase, Is.EqualTo(.01f)); Assert.That(safe.sculpt, Is.EqualTo(0)); Assert.That(safe.updraft, Is.EqualTo(10));
            wild.stretch = 50; Assert.That(wild.Sanitized().stretch, Is.EqualTo(12));
            // New looks keep the shape every preset had before these settings existed.
            var plain = AeroCloudLook.Default;
            Assert.That(plain.fibres, Is.Zero); Assert.That(plain.stretch, Is.EqualTo(1)); Assert.That(plain.sculpt, Is.Zero);
            Assert.That(plain.updraft, Is.Zero); Assert.That(plain.raggedBase, Is.EqualTo(1 / 3f).Within(1e-6f));
        }

        [Test]
        public void PreviewPresetCycle_StepsThroughAutomaticAndEveryLibraryPresetBothWays()
        {
            var library = ScriptableObject.CreateInstance<AeroCloudWeatherLibrary>();
            var a = ScriptableObject.CreateInstance<AeroCloudPreset>(); a.name = "A";
            var b = ScriptableObject.CreateInstance<AeroCloudPreset>(); b.name = "B";
            var storm = ScriptableObject.CreateInstance<AeroCloudPreset>(); storm.name = "Storm"; storm.look.coverage = 1;
            library.clearDay.Add(new AeroCloudWeatherLibrary.Entry { preset = a });
            library.clearDay.Add(new AeroCloudWeatherLibrary.Entry { preset = b });
            library.rain.Add(new AeroCloudWeatherLibrary.Entry { preset = storm });
            library.rain.Add(new AeroCloudWeatherLibrary.Entry { preset = a }); // Duplicates are listed once.
            var go = new GameObject("Cloud preset cycle fixture"); go.SetActive(false);
            var clouds = go.AddComponent<AeroVolumetricClouds>(); clouds.library = library; clouds.seed = 7;
            try
            {
                Assert.That(clouds.LibraryPresets(), Is.EqualTo(new[] { a, b, storm }), "Clear-day presets first, then rain, no duplicates.");
                Assert.That(clouds.CyclePreviewPreset(), Is.SameAs(a));
                Assert.That(clouds.Description, Is.EqualTo("Preview: A"));
                Assert.That(clouds.CyclePreviewPreset(), Is.SameAs(b));
                Assert.That(clouds.CyclePreviewPreset(), Is.SameAs(storm));
                Assert.That(clouds.Look.coverage, Is.EqualTo(1), "The previewed preset is shown immediately.");
                Assert.That(clouds.CyclePreviewPreset(), Is.Null, "After the last preset the sky returns to automatic weather.");
                Assert.That(clouds.CyclePreviewPreset(-1), Is.SameAs(storm), "Stepping back from automatic wraps to the last preset.");
                clouds.previewPreset = ScriptableObject.CreateInstance<AeroCloudPreset>();
                Assert.That(clouds.CyclePreviewPreset(), Is.SameAs(a), "An outside preset counts as automatic.");
                Object.DestroyImmediate(clouds.library); clouds.library = null;
                clouds.previewPreset = null;
                Assert.That(clouds.CyclePreviewPreset(), Is.Null, "No library: only automatic.");
            }
            finally
            {
                Object.DestroyImmediate(go);
                foreach (var o in new Object[] { a, b, storm, library }) if (o != null) Object.DestroyImmediate(o);
            }
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
            // Review 2026-11-06 (later): high layers (fleece, veils) are exempt. Their clouds are only 50-100 m tall,
            // so a large float tilted and smeared them into streaks; altocumulus forms as one layer anyway.
            var clearLooks = library.clearDay.Select(e => e.preset.look).ToArray();
            foreach (var p in library.clearDay.Select(e => e.preset).Where(p => p.look.cloudType >= .25f))
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
        public void ReworkedPresets_FleeceIsARoundedCloudletFieldAndDreamIsCrispCumulus()
        {
            // Review 2026-11-06: the old fleece (thin torn sheet, erosion .7, 250 m float) read as floating lines;
            // Frutiger Dream was soft, flat white blobs.
            var library = Library;
            var fleece = library.clearDay.Select(e => e.preset).Single(p => p.name == "Clear Altocumulus Fleece").look;
            Assert.That(fleece.cloudType, Is.LessThan(.25f), "Fleece stays a high layer.");
            Assert.That(fleece.baseAltitude, Is.InRange(600, 1000), "Altocumulus floats well above the cumulus.");
            Assert.That(fleece.cloudSize, Is.LessThanOrEqualTo(.35f), "Small cloudlets.");
            Assert.That(fleece.erosion, Is.LessThanOrEqualTo(.4f), "Rounded billows, not torn wisps.");
            Assert.That(fleece.heightVariation, Is.LessThanOrEqualTo(60), "One layer; a large float smears thin cloudlets.");
            Assert.That(fleece.shear, Is.LessThanOrEqualTo(50), "Cloudlets stay upright.");
            Assert.That(fleece.fadeDistance, Is.LessThanOrEqualTo(20000), "The far field melts into haze before it aliases.");
            var dream = library.clearDay.Select(e => e.preset).Single(p => p.name == "Clear Frutiger Dream").look;
            Assert.That(dream.erosion, Is.GreaterThanOrEqualTo(.45f), "Billowy cauliflower tops.");
            Assert.That(dream.edgeSoftness, Is.LessThanOrEqualTo(.3f), "Crisp outlines.");
            Assert.That(dream.shadowTint.b, Is.GreaterThan(dream.shadowTint.r + .25f), "Sky-blue shaded bellies.");

            AssertShippedMatchesDefinition("Clear Altocumulus Fleece", "Clear Frutiger Dream");
        }

        /// <summary>
        /// The shipped assets are what "Reset cloud presets to shipped values" rebuilds. The authoring script lives in
        /// the editor assembly, which test assemblies cannot reference, so its definitions are read by reflection.
        /// </summary>
        static void AssertShippedMatchesDefinition(params string[] names)
        {
            var authoring = System.AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("AeroCloudAuthoring")).Single(t => t != null);
            var definitions = ((System.Collections.IEnumerable)authoring.GetMethod("PresetDefinitions").Invoke(null, null)).Cast<object>().ToArray();
            var library = Library;
            foreach (string name in names)
            {
                var definition = definitions.Single(d => (string)d.GetType().GetField("name").GetValue(d) == name);
                var args = new object[] { AeroCloudLook.Default };
                ((System.Delegate)definition.GetType().GetField("edit").GetValue(definition)).DynamicInvoke(args);
                var look = (AeroCloudLook)args[0];
                var shipped = library.clearDay.Concat(library.rain).Select(e => e.preset).Single(p => p.name == name);
                Assert.That(JsonUtility.ToJson(shipped.look.Sanitized()), Is.EqualTo(JsonUtility.ToJson(look.Sanitized())), name + " matches its definition.");
                Assert.That(shipped.notes, Is.EqualTo((string)definition.GetType().GetField("notes").GetValue(definition)));
            }
        }

        static AeroCloudLook Shipped(string name) => Library.clearDay.Concat(Library.rain).Select(e => e.preset).Single(p => p.name == name).look;

        /// <summary>
        /// Strand cirrus: the hair-textured sheet (fibres 1). Scattered Wisps used this look until review 2026-11-15
        /// (fourth round); no shipped preset uses it now, but the sheet stays available for cirrus looks.
        /// </summary>
        static AeroCloudLook StrandCirrus()
        {
            var l = AeroCloudLook.Default;
            l.heightVariation = 40; l.coverage = .3f; l.density = 1.2f; l.cloudSize = 1.3f; l.cloudType = .1f; l.erosion = .3f; l.edgeSoftness = .9f;
            l.baseAltitude = 950; l.thickness = 250; l.fibres = 1; l.stretch = 3; l.windSpeed = 7; l.evolution = .5f; l.shear = 120;
            l.sunLight = 1.05f; l.ambientLight = 1.25f; l.shadowTint = new Color(.88f, .93f, 1); l.powder = 0; l.silverLining = .8f;
            l.multiScatter = .7f; l.hazeStrength = .75f; l.fadeDistance = 30000; l.sunOcclusion = .03f;
            return l.Sanitized();
        }

        [Test]
        public void ReworkedPresets_VeilIsCirrusAndTowersAndWispsAreSculptedCumulus()
        {
            // Review 2026-11-14: Thin High Veil and Scattered Wisps did not look like clouds (a 280-350 m float bent
            // their thin sheets into twisted ribbons; heavy torn-base erosion left nets of lines), and Towering Cumulus
            // broke into blurry fragments with flat walls and horizontal lines.
            var veil = Shipped("Clear Thin High Veil");
            Assert.That(veil.cloudType, Is.LessThan(.25f), "The veil is a high sheet.");
            Assert.That(veil.heightVariation, Is.LessThanOrEqualTo(40), "One flat sheet; a large float twisted it into ribbons.");
            Assert.That(veil.fibres, Is.InRange(.1f, .4f), "Faint fibres in a milky sheet, not separate strands.");
            Assert.That(veil.stretch, Is.GreaterThanOrEqualTo(2), "Combed along the wind.");
            Assert.That(veil.edgeSoftness, Is.GreaterThanOrEqualTo(.8f), "No hard outlines.");
            Assert.That(veil.density, Is.LessThanOrEqualTo(AeroCloudLook.Default.density), "Translucent: the sun shines through.");
            Assert.That(veil.powder, Is.Zero, "Ice veils have no dark edges.");

            var tower = Shipped("Clear Towering Cumulus");
            Assert.That(tower.sculpt, Is.GreaterThanOrEqualTo(.8f), "Turbulence outlines the towers, not flat map walls.");
            Assert.That(tower.raggedBase * tower.thickness, Is.LessThanOrEqualTo(100), "Billowy almost to the base (a torn lower third was 370 m of fragments).");
            Assert.That(tower.thickness, Is.GreaterThan(tower.cloudSize * 1200), "Taller than the shape noise is wide: towers, not blobs.");
            Assert.That(tower.heightVariation, Is.LessThanOrEqualTo(150), "Congestus share one condensation level.");
            Assert.That(tower.updraft, Is.GreaterThan(tower.evolution), "Billows rise faster than they boil: growing towers.");
            Assert.That(tower.fibres, Is.Zero); Assert.That(tower.stretch, Is.EqualTo(1));

            // Review 2026-11-15 (fourth round): no cirrus form of the wisps looked like cloud, and Towering Cumulus has the
            // best shape. Scattered Wisps is built like the towers, but smaller, shorter and more scattered, with
            // wind-torn fringes.
            var wisps = Shipped("Clear Scattered Wisps");
            Assert.That(wisps.fibres, Is.Zero, "Volumetric cumulus, not the cirrus sheet."); Assert.That(wisps.stretch, Is.EqualTo(1));
            Assert.That(wisps.sculpt, Is.GreaterThanOrEqualTo(.8f), "Turbulence outlines the clouds, as for the towers.");
            Assert.That(wisps.cloudType, Is.EqualTo(tower.cloudType).Within(.1f), "The towers' billowy build.");
            Assert.That(wisps.raggedBase * wisps.thickness, Is.LessThanOrEqualTo(100), "Billowy almost to a flat base.");
            Assert.That(wisps.updraft, Is.GreaterThan(wisps.evolution), "Billows rise.");
            Assert.That(wisps.cloudSize, Is.LessThan(tower.cloudSize), "Smaller than the towers.");
            Assert.That(wisps.thickness, Is.LessThan(tower.thickness), "Shorter than the towers.");
            Assert.That(wisps.coverage, Is.LessThanOrEqualTo(tower.coverage), "Scattered, with open sky between.");
            Assert.That(wisps.erosion, Is.GreaterThan(tower.erosion), "Wind-torn, wispy fringes...");
            Assert.That(wisps.shear, Is.GreaterThan(tower.shear), "...trailing downwind.");
            Assert.That(wisps.heightVariation + wisps.thickness, Is.LessThanOrEqualTo(AeroVolumetricCloudsRendererFeature.DeepLayerMetres), "Keeps the authored ray steps.");

            // Every other preset keeps the shape it had before the new settings existed.
            foreach (var p in Library.clearDay.Concat(Library.rain).Select(e => e.preset)
                         .Where(p => p.name != "Clear Thin High Veil" && p.name != "Clear Scattered Wisps" && p.name != "Clear Towering Cumulus"))
            {
                Assert.That(p.look.fibres, Is.Zero, p.name); Assert.That(p.look.stretch, Is.EqualTo(1), p.name);
                Assert.That(p.look.sculpt, Is.Zero, p.name); Assert.That(p.look.updraft, Is.Zero, p.name);
                Assert.That(p.look.raggedBase, Is.EqualTo(1 / 3f).Within(1e-5f), p.name);
            }
            AssertShippedMatchesDefinition("Clear Thin High Veil", "Clear Scattered Wisps", "Clear Towering Cumulus");
        }

        [Test]
        public void DeepLayers_MarchMoreStepsWhileShallowSkiesKeepTheirCost()
        {
            // Review 2026-11-14: rays toward the horizon ran out of steps inside distant towers at a distance that
            // changed row by row, drawing horizontal lines across them.
            var quality = Feature.quality;
            var tower = Shipped("Clear Towering Cumulus");
            Assert.That(AeroVolumetricCloudsRendererFeature.PrimarySteps(quality, tower), Is.EqualTo(quality.PrimarySteps * 2), "Towers get twice the steps.");
            foreach (var p in Library.clearDay.Select(e => e.preset).Where(p => p.name != "Clear Towering Cumulus"))
                Assert.That(AeroVolumetricCloudsRendererFeature.PrimarySteps(quality, p.look), Is.EqualTo(quality.PrimarySteps), $"{p.name}: no extra cost.");

            var go = new GameObject("Form fixture"); go.SetActive(false);
            var preset = ScriptableObject.CreateInstance<AeroCloudPreset>();
            try
            {
                var clouds = go.AddComponent<AeroVolumetricClouds>();
                preset.look = tower; preset.look.windDirection = 90; clouds.previewPreset = preset; clouds.Tick(0); clouds.Tick(4);
                var block = new MaterialPropertyBlock();
                AeroVolumetricCloudsRendererFeature.FillConstants(block, clouds, quality, 1200, 200, 7000, .009f);
                Assert.That(block.GetVector("_AeroCloudMarch").x, Is.EqualTo(quality.PrimarySteps * 2));
                Assert.That(block.GetVector("_AeroCloudForm"), Is.EqualTo(new Vector4(tower.fibres, tower.stretch, tower.raggedBase, tower.sculpt)));
                Assert.That(block.GetVector("_AeroCloudWindDir").z, Is.EqualTo(tower.updraft * 4).Within(1e-3f), "The rise offset reaches the shader.");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(preset); }
        }

        [Test]
        public void Exposure_KeepsSunlitCloudsOffTheTonemapperShoulder()
        {
            var feature = Feature;
            Assert.That(feature.sunExposure, Is.InRange(.2f, .7f), "At 1 every sunlit cloud clipped to one flat white.");
            Assert.That(feature.skyExposure, Is.InRange(.8f, 1.5f));
            var go = new GameObject("Exposure fixture"); go.SetActive(false);
            var quality = ScriptableObject.CreateInstance<AeroCloudQuality>();
            try
            {
                var clouds = go.AddComponent<AeroVolumetricClouds>();
                var block = new MaterialPropertyBlock();
                AeroVolumetricCloudsRendererFeature.FillConstants(block, clouds, quality, 1200, 200, 7000, .009f, .35f, 1.2f);
                Assert.That(block.GetVector("_AeroCloudTone"), Is.EqualTo(new Vector4(.35f, 1.2f, 0, 0)));
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(quality); }
        }

        [Test]
        public void Shader_GuardsAgainstTheLinesThatCutThroughClouds()
        {
            string hlsl = File.ReadAllText("Assets/_Project/Shaders/Aero/AeroClouds.hlsl");
            // Tops keep the Worley cell centres (billows); eroding them left only the cell borders, a net of lines.
            // The torn underside is a share of each cloud (raggedBase: a third by default, small for towers).
            StringAssert.Contains("float detail = lerp(noise, 1 - noise, saturate(h / _AeroCloudForm.z));", hlsl);
            // Whole-step skipping keeps one sample lattice for every pixel (a 1.6x skip drew contour bands).
            StringAssert.DoesNotContain("step * 1.6", hlsl);
            StringAssert.Contains("else t += step * 2;", hlsl);
            // The jitter walks a golden-ratio sequence, so every pixel visits all phases.
            StringAssert.Contains("_AeroCloudMarch.z * .61803398875", hlsl);
            // Review 2026-11-06 (Cotton Puffs): empty air is marched in double steps, so the start offset must span a
            // double step. Spanning one left half the positions unvisited and thin fringes converged to a comb of
            // horizontal teeth. Each pixel's phase is hashed independently (interleaved gradient noise repeats
            // every ~3 rows, which the temporal blend turned into row-dependent stripes).
            StringAssert.Contains("min(stepSize, max(8, range.x * .25)) * 2 * AeroPixelPhase(pixel);", hlsl);
            StringAssert.DoesNotContain("52.9829189", hlsl);
            // The per-cloud float mixes a rotated lookup at an unrelated scale (no axis-aligned repeat), and the
            // weather map is filtered to the sample footprint.
            StringAssert.Contains("float2 turned = float2(uv.x * .819 - uv.y * .574, uv.x * .574 + uv.y * .819) * .731;", hlsl);
            StringAssert.Contains("_AeroCloudCoverage, sampler_LinearRepeat, AeroAlongWind(field, sqrt(_AeroCloudForm.y)) * _AeroCloudShape2.z, coverageLod)", hlsl);
            // Strand cirrus (review 2026-11-15, third round) is a thin sheet textured with the hair map, filtered with
            // screen derivatives (anisotropic toward the horizon). The volume fades out as the sheet fades in, and a pure
            // sheet skips the march.
            StringAssert.Contains("float4 AeroCirrusSheet(float3 ray, float sceneDistance, float3 hazeColor, float3 phases)", hlsl);
            StringAssert.Contains("float2 adx = ddx(a), ady = ddy(a), bdx = ddx(b), bdy = ddy(b);", hlsl);
            StringAssert.Contains("SAMPLE_TEXTURE2D_GRAD(_AeroCloudCirrus, sampler_AeroCloudCirrus, a, adx, ady)", hlsl);
            StringAssert.Contains("return volume * saturate((shape * profile - (1 - sculpted))", hlsl);
            StringAssert.Contains("|| AeroStrandy() >= 1) return empty;", hlsl);
            StringAssert.Contains("float AeroCirrusStrands(float2 xz, float lod)", hlsl);
            StringAssert.DoesNotContain("shape = lerp(shape, 1, strandy);", hlsl);
            // Stretch along the wind leaves unstretched skies exactly as they were (no rotation into a wind frame).
            StringAssert.Contains("return xz - _AeroCloudWindDir.xy * (along * (1 - 1 / stretch));", hlsl);
            string feature = File.ReadAllText("Assets/_Project/Scripts/Rendering/Clouds/AeroVolumetricCloudsRendererFeature.cs");
            StringAssert.Contains("march.z = NextJitterFrame(camera);", feature);
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
                preset.look.windDirection = 90; preset.look.windSpeed = 10; preset.look.evolution = 2; preset.look.updraft = 1.5f;
                rig.Clouds.previewPreset = preset; rig.Clouds.Tick(0);
                var start = rig.Clouds.WindOffset; float risen = rig.Clouds.RiseOffset;
                rig.Clouds.Tick(3);
                var moved = rig.Clouds.WindOffset - start;
                Assert.That(moved.x, Is.EqualTo(30).Within(1e-3f), "East wind drifts +x."); Assert.That(moved.y, Is.EqualTo(0).Within(1e-3f));
                Assert.That(moved.z, Is.EqualTo(6).Within(1e-3f));
                Assert.That(rig.Clouds.RiseOffset - risen, Is.EqualTo(4.5f).Within(1e-3f), "Billows rise at the updraft speed.");
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
        public void CirrusMap_IsWispsOfHairCombedAlongTheWindWithClearSkyBetween()
        {
            // Review 2026-11-15 (third round): the cirrus map is traced hair, one byte per texel, u along the wind.
            const int n = 128;
            var cirrus = AeroCloudNoise.Cirrus(n, 9);
            Assert.That(cirrus.Length, Is.EqualTo(n * n), "R8: one byte per texel.");
            Assert.That(cirrus, Is.EqualTo(AeroCloudNoise.Cirrus(n, 9)), "Cirrus map is deterministic.");
            float share = cirrus.Count(b => b > 25) / (float)cirrus.Length;
            Assert.That(share, Is.InRange(.08f, .4f), $"Wisps with clear sky between them, not a pelt of hair (share {share:0.00}).");
            // Strands run along u: brightness changes far less along a row than down a column.
            float along = 0, across = 0;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int c = cirrus[y * n + x];
                along += Mathf.Abs(cirrus[y * n + (x + 1) % n] - c);
                across += Mathf.Abs(cirrus[(y + 1) % n * n + x] - c);
            }
            Assert.That(across, Is.GreaterThan(along * 2), $"Hair combed along the wind (across {across:0} vs along {along:0}).");
            // Hairs wrap across both tile edges: the wrap pair differs no more than the roughest pair inside the tile.
            float Step(int a, int b, bool columns)
            {
                float sum = 0;
                for (int i = 0; i < n; i++) sum += columns ? Mathf.Abs(cirrus[i * n + a] - cirrus[i * n + b]) : Mathf.Abs(cirrus[a * n + i] - cirrus[b * n + i]);
                return sum / n;
            }
            foreach (bool columns in new[] { true, false })
            {
                float roughest = 0; for (int i = 0; i < n - 1; i++) roughest = Mathf.Max(roughest, Step(i, i + 1, columns));
                Assert.That(Step(n - 1, 0, columns), Is.LessThanOrEqualTo(roughest), (columns ? "Columns" : "Rows") + " wrap with no seam line in the sky.");
            }
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
            Assert.That(feature.cirrusMap, Is.Not.Null, "Cirrus strands need their baked map.");
            Assert.That(feature.cirrusMap.wrapMode, Is.EqualTo(TextureWrapMode.Repeat)); Assert.That(feature.cirrusMap.mipmapCount, Is.GreaterThan(1));
            Assert.That(feature.cirrusMap.format, Is.EqualTo(TextureFormat.R8)); Assert.That(feature.cirrusMap.width, Is.EqualTo(AeroCloudNoise.DefaultCirrusSize));
            Assert.That(feature.cirrusMap.anisoLevel, Is.GreaterThanOrEqualTo(4), "The sheet is seen at grazing angles toward the horizon.");
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
            // Over black, a cloud counts by its own light. Over the harness's grey, thin sunlit clouds at realistic
            // exposure (and this harness's weaker sun) matched the grey and went uncounted, though they cover the sky.
            h.Camera.backgroundColor = Color.black;
            // ...and under the game's noon sun (the harness default is weaker), since cloud light scales with it.
            h.Sun.intensity = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>("Assets/_Project/Environment/Lighting/AeroDayNight.asset").sunIntensity;
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

        static float Percentile(float[] sorted, float p) => sorted[Mathf.Clamp(Mathf.RoundToInt((sorted.Length - 1) * p), 0, sorted.Length - 1)];

        [Test]
        public void Gpu_SunlitCumulusHaveShadedBlueBelliesAndBrightBillows()
        {
            // Review 2026-11-06: Frutiger Dream rendered as flat white blobs. The sun term was ~3x too bright, so
            // every cloud sat on the tonemapper's shoulder, and thick bellies were lit almost like thin fringes.
            using var h = new AeroRenderHarness(96);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            preset.look = Library.clearDay.Select(e => e.preset).Single(p => p.name == "Clear Frutiger Dream").look;
            preset.look.hazeStrength = 0;
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(-35, 20, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            try
            {
                h.Render(); var image = h.Render();
                var cloud = image.Where((c, i) => Rgb(c - background[i]) > .08f).ToArray();
                Assert.That(cloud.Length, Is.GreaterThan(image.Length / 10), "Clouds in view.");
                var lum = cloud.Select(c => c.grayscale).OrderBy(x => x).ToArray();
                float low = Percentile(lum, .1f), high = Percentile(lum, .9f);
                var shaded = cloud.OrderBy(c => c.grayscale).Take(cloud.Length / 5).ToArray();
                float blue = shaded.Average(c => c.b), red = shaded.Average(c => c.r);
                string report = $"cloud share {cloud.Length / (float)image.Length:0.00}, luminance p10 {low:0.000} p90 {high:0.000}, shaded rgb ({red:0.000}, {shaded.Average(c => c.g):0.000}, {blue:0.000})";
                TestContext.WriteLine(report);
                Assert.That(low, Is.LessThan(high * .6f), "Shaded bellies sit clearly below the lit billows. " + report);
                Assert.That(high, Is.LessThan(h.Sun.intensity * 1.6f), "Lit parts stay in range instead of blowing out. " + report);
                // The harness's ambient is neutral grey (in game it is the blue sky), so only the shadow tint adds blue here.
                Assert.That(blue, Is.GreaterThan(red * 1.03f), "Shadows lean blue, not flat grey. " + report);
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_HighCloudletFieldShowsNoStreaksFanningToTheHorizon()
        {
            // Review 2026-11-06: Altocumulus Fleece showed straight lines converging on vanishing points along the
            // world axes. The per-cloud height float repeated on an axis-aligned grid, and grazing rays through the
            // thin layer summed the repeats. Streaks near the horizon run nearly vertical on screen, while a healthy
            // field is flattened by perspective, so horizontal pixel differences outgrow vertical ones only with streaks.
            using var h = new AeroRenderHarness(256); // streaks are 1-2 px wide; coarser renders blur them away
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            var shipped = Library.clearDay.Select(e => e.preset).Single(p => p.name == "Clear Altocumulus Fleece").look;
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 30, 0), Quaternion.Euler(-4, 135, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            float Streakiness(AeroCloudLook look)
            {
                preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 4; i++) h.Render();
                var image = h.Render();
                // The band from the horizon up ~10 degrees (rows count from the bottom).
                int s = h.Size, from = s / 2 - 1, to = s / 2 + s / 8;
                float horizontal = 0, vertical = 0;
                for (int y = from; y < to; y++)
                for (int x = 0; x < s - 1; x++)
                {
                    float c = (image[y * s + x] - background[y * s + x]).grayscale;
                    horizontal += Mathf.Abs((image[y * s + x + 1] - background[y * s + x + 1]).grayscale - c);
                    vertical += Mathf.Abs((image[(y + 1) * s + x] - background[(y + 1) * s + x]).grayscale - c);
                }
                return horizontal / Mathf.Max(vertical, 1e-5f);
            }
            try
            {
                float clean = Streakiness(shipped);
                var floating = shipped; floating.heightVariation = 250; floating.fadeDistance = 32000; // the old fleece's float and reach
                float streaky = Streakiness(floating);
                string report = $"shipped {clean:0.000}, with a 250 m float {streaky:0.000}";
                TestContext.WriteLine(report);
                Assert.That(streaky, Is.GreaterThan(clean * 1.15f), "The measure detects streaks. " + report);
                Assert.That(streaky, Is.GreaterThan(clean * 1.25f), "...clearly. " + report);
                Assert.That(clean, Is.LessThan(.7f), "The shipped fleece has no streak fan (it measured ~0.62; streaky skies ~0.87). " + report);
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        /// <summary>Horizontal over vertical brightness change of the cloud layer (image minus background).</summary>
        static float Anisotropy(Color[] image, Color[] background, int size, int fromRow, int toRow)
        {
            float horizontal = 0, vertical = 0;
            for (int y = fromRow; y < toRow - 1; y++)
            for (int x = 0; x < size - 1; x++)
            {
                float c = (image[y * size + x] - background[y * size + x]).grayscale;
                horizontal += Mathf.Abs((image[y * size + x + 1] - background[y * size + x + 1]).grayscale - c);
                vertical += Mathf.Abs((image[(y + 1) * size + x] - background[(y + 1) * size + x]).grayscale - c);
            }
            return horizontal / Mathf.Max(vertical, 1e-5f);
        }

        [Test]
        public void Gpu_CirrusStrandsAreCombedAlongTheWind()
        {
            // Review 2026-11-14: the wisps read as twisted ribbons, not clouds. Cirrus strands now trail along the
            // wind, so seen from straight below they run across the screen in an east wind and up it in a north wind.
            using var h = new AeroRenderHarness(128);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            // Looking straight up: screen right is world +x, screen up is world -z.
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(-90, 0, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            int s = h.Size;
            float Measure(AeroCloudLook look, out float share)
            {
                preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 3; i++) h.Render();
                var image = h.Render();
                share = image.Where((c, i) => Rgb(c - background[i]) > .03f).Count() / (float)image.Length;
                return Anisotropy(image, background, s, 0, s);
            }
            try
            {
                var wisps = StrandCirrus(); wisps.coverage = .6f; wisps.hazeStrength = 0;
                var east = wisps; east.windDirection = 90;
                var north = wisps; north.windDirection = 0;
                var round = east; round.stretch = 1; round.fibres = 0;
                float e = Measure(east, out float share), n = Measure(north, out _), r = Measure(round, out _);
                string report = $"east {e:0.000} (cloud share {share:0.00}), north {n:0.000}, unstretched {r:0.000}";
                TestContext.WriteLine(report);
                // Measured 2026-11-14 (cirrus map strands): east 0.21, north 3.46, unstretched 0.92 (cloud share 0.54).
                // Measured 2026-11-15 (hair sheet): east 0.32, north 2.23, unstretched 0.93 (cloud share 0.33).
                Assert.That(share, Is.GreaterThan(.1f), "Strands in view. " + report);
                Assert.That(e, Is.LessThan(r * .6f), "East wind: the strands run across the screen. " + report);
                Assert.That(n, Is.GreaterThan(r * 1.6f), "North wind: the strands run up the screen. " + report);
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        /// <summary>
        /// How much of the brightness change along one axis keeps the same sign across the other axis: 1 when every
        /// row (or column) steps the same way at once, near 0 for natural, uncorrelated edges.
        /// Rows: horizontal lines. Columns: vertical walls.
        /// </summary>
        static float Coherence(Color[] image, Color[] background, int size, int fromRow, int toRow, bool rows)
        {
            float C(int x, int y) => (image[y * size + x] - background[y * size + x]).grayscale;
            float coherent = 0, total = 0;
            int outer = rows ? toRow - fromRow - 1 : size - 1, inner = rows ? size : toRow - fromRow;
            for (int o = 0; o < outer; o++)
            {
                float signedSum = 0, absSum = 0;
                for (int i = 0; i < inner; i++)
                {
                    float d = rows ? C(i, fromRow + o + 1) - C(i, fromRow + o) : C(o + 1, fromRow + i) - C(o, fromRow + i);
                    signedSum += d; absSum += Mathf.Abs(d);
                }
                coherent += Mathf.Abs(signedSum); total += absSum;
            }
            return coherent / Mathf.Max(total, 1e-5f);
        }

        [Test]
        public void Gpu_DistantCirrusMeltsIntoTheHorizonInsteadOfAFieldOfFlecks()
        {
            // Review 2026-11-14 (second round): a field of small flecks toward the horizon. Review 2026-11-15 (third
            // round): the sheet's strokes, foreshortened toward the horizon, packed into a busy band of white flecks as
            // crisp as the wisps overhead. Real cirrus there sits behind far more air and fades into the haze. Measured
            // as the mean cloud contrast in the 6 degrees above the horizon over that at 22-38 degrees.
            using var h = new AeroRenderHarness(256);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 45, 0), Quaternion.Euler(-20, 38, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            // Rows count from the bottom; the horizon sits 20 degrees below the centre.
            int s = h.Size, horizon = s / 2 - Mathf.RoundToInt(s * 20 / 70f);
            int Row(float degrees) => horizon + Mathf.RoundToInt(s * degrees / 70f);
            (float low, float high) Measure(AeroCloudLook look)
            {
                preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 6; i++) h.Render();
                var image = h.Render();
                float Contrast(int from, int to)
                {
                    float sum = 0;
                    for (int y = from; y < to; y++)
                    for (int x = 0; x < s; x++) sum += Rgb(image[y * s + x] - background[y * s + x]);
                    return sum / (s * (to - from));
                }
                return (Contrast(horizon + 1, Row(6)), Contrast(Row(22), Row(38)));
            }
            try
            {
                var wisps = StrandCirrus();
                var clear = wisps; clear.hazeStrength = 0;
                var shipped = Measure(wisps);
                var crisp = Measure(clear);
                float fade = shipped.low / Mathf.Max(shipped.high, 1e-5f), flecks = crisp.low / Mathf.Max(crisp.high, 1e-5f);
                string report = $"shipped horizon/high contrast {shipped.low:0.000}/{shipped.high:0.000} = {fade:0.00}; without haze {crisp.low:0.000}/{crisp.high:0.000} = {flecks:0.00}";
                TestContext.WriteLine(report);
                Assert.That(shipped.high, Is.GreaterThan(.03f), "Wisps in view higher up. " + report);
                Assert.That(shipped.low, Is.GreaterThan(.005f), "Still some cirrus toward the horizon. " + report);
                // Measured 2026-11-15: shipped 0.016/0.082 = 0.19; without haze 0.032/0.097 = 0.33.
                Assert.That(flecks, Is.GreaterThan(fade * 1.4f), "The measure sees a crisp horizon band. " + report);
                Assert.That(fade, Is.LessThan(.6f), "Distant wisps fade into the horizon haze. " + report);
            }
            finally { Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_StrandCirrusIsAHairSheetWithNoVolumeLeftToSmear()
        {
            // Review 2026-11-15 (third round): drawn as a 250 m deep volume, each strand stood as a wall that the view
            // from below smeared into a broad band, and the weather map cut the bands off along smooth outlines.
            // Strand cirrus is now only the hair-textured sheet: with a blank hair map the sky stays empty, while a
            // look that keeps the volume (fibres .5) still shows cloud there.
            using var h = new AeroRenderHarness(128);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 20, 0), Quaternion.Euler(-50, 30, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            var feature = Feature; var hairMap = feature.cirrusMap;
            var blank = new Texture2D(4, 4, TextureFormat.R8, true, true) { wrapMode = TextureWrapMode.Repeat };
            blank.SetPixelData(new byte[16], 0); blank.Apply(true, false);
            float Share(AeroCloudLook look, Texture2D map)
            {
                feature.cirrusMap = map; preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 4; i++) h.Render();
                var image = h.Render();
                return image.Where((c, i) => Rgb(c - background[i]) > .03f).Count() / (float)image.Length;
            }
            try
            {
                var wisps = StrandCirrus(); wisps.coverage = .6f; wisps.hazeStrength = 0;
                var volume = wisps; volume.fibres = .5f;
                float sheet = Share(wisps, hairMap), bare = Share(wisps, blank), kept = Share(volume, blank);
                string report = $"sheet {sheet:0.000}, sheet without hair {bare:0.000}, volume without hair {kept:0.000}";
                TestContext.WriteLine(report);
                // Measured 2026-11-15: sheet 0.482, sheet without hair 0.000, volume without hair 0.644.
                Assert.That(sheet, Is.GreaterThan(.1f), "Wisps in view. " + report);
                Assert.That(kept, Is.GreaterThan(.1f), "The measure sees a volume when one is drawn. " + report);
                Assert.That(bare, Is.LessThan(.005f), "Every wisp comes from the hair sheet; no volume behind it. " + report);
            }
            finally { feature.cirrusMap = hairMap; Object.DestroyImmediate(blank); Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_ToweringCumulusShowsNoFlatWallsOrHorizontalLines()
        {
            // Review 2026-11-14: Towering Cumulus "fragment and weird lines". The weather map's outline rose up the
            // whole tower as a flat wall (left-right edges lined up down each column), and rays toward the horizon ran
            // out of steps inside distant towers at a distance that changes row by row (edges lined up along rows).
            // Measured 2026-11-14 in the 17 degrees above the horizon: shipped 0.455 (columns) / 0.332 (rows);
            // unsculpted 0.540 columns; the authored step count 0.422 rows; the reported look 0.494 / 0.501.
            using var h = new AeroRenderHarness(256);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 45, 0), Quaternion.Euler(-12, 38, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            var quality = Feature.quality;
            int authoredSteps = quality.primarySteps;
            int s = h.Size, horizon = s / 2 - Mathf.RoundToInt(s * 12 / 70f), top = horizon + s / 4;
            (float walls, float lines) Measure(AeroCloudLook look, int steps)
            {
                quality.primarySteps = steps; preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 8; i++) h.Render();
                var image = h.Render();
                return (Coherence(image, background, s, horizon + 1, top, false), Coherence(image, background, s, horizon + 1, top, true));
            }
            try
            {
                var tower = Shipped("Clear Towering Cumulus"); tower.hazeStrength = 0;
                var unsculpted = tower; unsculpted.sculpt = 0;
                var reported = AeroCloudLook.Default; // the look reviewed on 2026-11-14
                reported.heightVariation = 250; reported.coverage = .3f; reported.density = 1.15f; reported.cloudSize = 1.1f; reported.cloudType = .78f;
                reported.baseAltitude = 160; reported.thickness = 1100; reported.shear = 300; reported.powder = .6f; reported.hazeStrength = 0;
                var shipped = Measure(tower, authoredSteps);
                var walls = Measure(unsculpted, authoredSteps);
                var fewSteps = Measure(tower, authoredSteps / 2); // the step count before deep layers doubled it
                var before = Measure(reported, authoredSteps / 2);
                string report = $"shipped columns {shipped.walls:0.000} rows {shipped.lines:0.000}; unsculpted columns {walls.walls:0.000}; " +
                                $"authored steps rows {fewSteps.lines:0.000}; reported look columns {before.walls:0.000} rows {before.lines:0.000}";
                TestContext.WriteLine(report);
                Assert.That(walls.walls, Is.GreaterThan(shipped.walls * 1.08f), "Sculpting removes the flat walls. " + report);
                Assert.That(fewSteps.lines, Is.GreaterThan(shipped.lines * 1.1f), "Doubled steps remove the horizontal lines. " + report);
                Assert.That(before.walls, Is.GreaterThan(shipped.walls * 1.03f), "Fewer walls than the reported look. " + report);
                Assert.That(before.lines, Is.GreaterThan(shipped.lines * 1.2f), "Fewer lines than the reported look. " + report);
            }
            finally { quality.primarySteps = authoredSteps; Object.DestroyImmediate(cloudsObject); }
        }

        [Test]
        public void Gpu_ScatteredWispsAreSculptedLikeTheTowersWithMoreOpenSky()
        {
            // Review 2026-11-15 (fourth round): Scattered Wisps now shares the towers' build. Turbulence outlines the
            // clouds (without sculpting, the weather map's outline rises up each cloud as a flat wall, which lines
            // brightness edges up down the columns), and the smaller, scattered clouds leave more sky open.
            using var h = new AeroRenderHarness(256);
            var preset = h.Own(ScriptableObject.CreateInstance<AeroCloudPreset>());
            h.Camera.fieldOfView = 70; h.Camera.farClipPlane = 65536;
            h.Camera.transform.SetPositionAndRotation(new Vector3(0, 45, 0), Quaternion.Euler(-12, 38, 0));
            var background = h.Render();
            var cloudsObject = CloudsInScene(h, preset);
            var clouds = cloudsObject.GetComponent<AeroVolumetricClouds>();
            int s = h.Size, horizon = s / 2 - Mathf.RoundToInt(s * 12 / 70f), top = horizon + s / 4;
            (float walls, float share) Measure(AeroCloudLook look)
            {
                preset.look = look; clouds.Tick(0);
                for (int i = 0; i < 8; i++) h.Render();
                var image = h.Render();
                int cloud = 0;
                for (int y = horizon + 1; y < s; y++)
                for (int x = 0; x < s; x++) if (Rgb(image[y * s + x] - background[y * s + x]) > .05f) cloud++;
                return (Coherence(image, background, s, horizon + 1, top, false), cloud / (float)(s * (s - horizon - 1)));
            }
            try
            {
                var wisps = Shipped("Clear Scattered Wisps"); wisps.hazeStrength = 0;
                var unsculpted = wisps; unsculpted.sculpt = 0;
                var tower = Shipped("Clear Towering Cumulus"); tower.hazeStrength = 0;
                var shipped = Measure(wisps); var walls = Measure(unsculpted); var towers = Measure(tower);
                string report = $"wisps columns {shipped.walls:0.000} (sky share {shipped.share:0.00}); unsculpted columns {walls.walls:0.000}; towers sky share {towers.share:0.00}";
                TestContext.WriteLine(report);
                Assert.That(shipped.share, Is.GreaterThan(.08f), "Clouds in view. " + report);
                // Measured 2026-11-15: wisps columns 0.482 (sky share 0.58); unsculpted 0.516; towers sky share 0.74.
                Assert.That(walls.walls, Is.GreaterThan(shipped.walls * 1.04f), "Sculpting removes the flat walls. " + report);
                Assert.That(shipped.share, Is.LessThan(towers.share * .9f), "More open sky than the towers. " + report);
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
