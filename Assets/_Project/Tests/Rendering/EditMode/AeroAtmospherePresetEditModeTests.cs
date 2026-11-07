using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class AeroAtmospherePresetEditModeTests
    {
        const string LibraryPath = "Assets/_Project/Environment/Lighting/AeroAtmospherePresets.asset";
        const string ShaderPath = "Assets/_Project/Shaders/AeroAmbientParticles.shader";

        static AeroAtmospherePresetLibrary Defaults() => ScriptableObject.CreateInstance<AeroAtmospherePresetLibrary>();

        static IEnumerable<(AeroTimeSlot time, AeroWeatherKind weather, AeroSeason season)> All()
            => Enumerable.Range(0, AeroAtmospherePresetLibrary.PermutationCount).Select(AeroAtmospherePresetLibrary.Permutation);

        static AeroAtmosphereLook Look(AeroAtmospherePresetLibrary l, AeroTimeSlot t, AeroWeatherKind w, AeroSeason s) => l.Compose(t, w, s).look;

        static float Density(AeroAtmospherePresetLibrary.Frame frame, AeroParticleKind kind) => frame.particles.Where(p => p.kind == kind).Sum(p => p.density);

        [Test]
        public void Library_HasSixtyDistinctPermutationsThatRoundTrip()
        {
            var library = Defaults();
            try
            {
                Assert.That(AeroAtmospherePresetLibrary.PermutationCount, Is.EqualTo(5 * 3 * 4));
                var seen = new HashSet<string>(); var names = new HashSet<string>();
                for (int i = 0; i < AeroAtmospherePresetLibrary.PermutationCount; i++)
                {
                    var (t, w, s) = AeroAtmospherePresetLibrary.Permutation(i);
                    Assert.That(AeroAtmospherePresetLibrary.IndexOf(t, w, s), Is.EqualTo(i));
                    Assert.That(names.Add(AeroAtmospherePresetLibrary.Describe(t, w, s)), Is.True);
                    var frame = library.Compose(t, w, s);
                    Assert.That(frame.slot, Is.EqualTo(t)); Assert.That(frame.weather, Is.EqualTo(w)); Assert.That(frame.season, Is.EqualTo(s));
                    Assert.That(seen.Add(JsonUtility.ToJson(frame.look)), Is.True, $"{t}/{w}/{s} must differ from every other permutation.");
                }
                Assert.That(AeroAtmospherePresetLibrary.Permutation(-1), Is.EqualTo(AeroAtmospherePresetLibrary.Permutation(59)), "Browsing wraps.");
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void EveryPermutation_StaysCloseToRestrainedAndGentleOnTheEyes()
        {
            var library = Defaults();
            try
            {
                foreach (var (t, w, s) in All())
                {
                    var l = Look(library, t, w, s); string name = $"{t}/{w}/{s}";
                    Assert.That(l.saturation, Is.InRange(0, 26), name + ": saturated but never aggressive (rejected Y2K was 30).");
                    Assert.That(l.contrast, Is.InRange(0, 12), name + ": soft contrast.");
                    Assert.That(Mathf.Abs(l.exposure), Is.LessThanOrEqualTo(.3f), name);
                    Assert.That(l.bloomThreshold, Is.GreaterThanOrEqualTo(.88f), name + ": marble stays readable.");
                    Assert.That(l.bloomIntensity, Is.InRange(.3f, 1f), name + ": bloom stays gentle.");
                    Assert.That(l.vignette, Is.LessThanOrEqualTo(.22f), name);
                    Assert.That(l.temperature, Is.InRange(-30, 20), name);
                    Assert.That(l.fogDensityScale, Is.InRange(.5f, 3.5f), name);
                    Assert.That(l.shadowStrength, Is.InRange(.15f, 1f), name);
                    foreach (var c in new[] { l.splitShadows, l.splitHighlights })
                        Assert.That(Mathf.Max(Mathf.Abs(c.r - .5f), Mathf.Abs(c.g - .5f), Mathf.Abs(c.b - .5f)), Is.LessThan(.15f), name + ": split toning is a hint, not a filter.");
                }
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void TimesOfDay_FollowWarmAndCoolLightLikeTheResearch()
        {
            var library = Defaults();
            try
            {
                foreach (AeroSeason s in System.Enum.GetValues(typeof(AeroSeason)))
                foreach (AeroWeatherKind w in System.Enum.GetValues(typeof(AeroWeatherKind)))
                {
                    var morning = Look(library, AeroTimeSlot.Morning, w, s); var noon = Look(library, AeroTimeSlot.MidDay, w, s);
                    var afternoon = Look(library, AeroTimeSlot.Afternoon, w, s); var night = Look(library, AeroTimeSlot.Night, w, s);
                    var midnight = Look(library, AeroTimeSlot.Midnight, w, s);
                    Assert.That(afternoon.Warmth, Is.GreaterThan(noon.Warmth), $"{s}/{w}: golden afternoon is warmer than mid-day.");
                    Assert.That(morning.Warmth, Is.GreaterThan(noon.Warmth), $"{s}/{w}: morning sun is peachier than mid-day.");
                    Assert.That(night.Warmth, Is.LessThan(noon.Warmth), $"{s}/{w}: night is cool blue.");
                    Assert.That(midnight.temperature, Is.LessThan(night.temperature), $"{s}/{w}: midnight is the coolest.");
                    Assert.That(night.exposure, Is.GreaterThan(noon.exposure), $"{s}/{w}: night is lifted so silhouettes stay readable.");
                    Assert.That(midnight.fogColor.grayscale, Is.LessThan(noon.fogColor.grayscale * .5f), $"{s}/{w}: night haze is dark.");
                    Assert.That(midnight.fogColor.b, Is.GreaterThan(midnight.fogColor.r), $"{s}/{w}: and blue, never black.");
                    Assert.That(morning.fogDensityScale, Is.GreaterThan(noon.fogDensityScale), $"{s}/{w}: morning mist.");
                    Assert.That(night.night, Is.EqualTo(1)); Assert.That(noon.night, Is.EqualTo(0));
                }
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void Weather_CloudsSoftenAndRainCoolsMutesAndMists()
        {
            var library = Defaults();
            try
            {
                foreach (AeroSeason s in System.Enum.GetValues(typeof(AeroSeason)))
                foreach (AeroTimeSlot t in System.Enum.GetValues(typeof(AeroTimeSlot)))
                {
                    var clear = Look(library, t, AeroWeatherKind.Clear, s); var cloudy = Look(library, t, AeroWeatherKind.Cloudy, s); var rain = Look(library, t, AeroWeatherKind.Rain, s);
                    Assert.That(cloudy.shadowStrength, Is.LessThan(clear.shadowStrength), $"{t}/{s}: diffuse light under clouds.");
                    Assert.That(rain.shadowStrength, Is.LessThan(cloudy.shadowStrength), $"{t}/{s}");
                    Assert.That(cloudy.contrast, Is.LessThan(clear.contrast), $"{t}/{s}");
                    Assert.That(rain.saturation, Is.LessThan(cloudy.saturation), $"{t}/{s}: rain is muted.");
                    Assert.That(cloudy.saturation, Is.LessThan(clear.saturation), $"{t}/{s}");
                    Assert.That(rain.temperature, Is.LessThan(clear.temperature), $"{t}/{s}: rain is cooler.");
                    Assert.That(rain.fogDensityScale, Is.GreaterThan(cloudy.fogDensityScale), $"{t}/{s}: rain mist.");
                    Assert.That(cloudy.fogDensityScale, Is.GreaterThan(clear.fogDensityScale), $"{t}/{s}");
                }
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void Seasons_HaveTheirOwnTemperatureColourAndParticles()
        {
            var library = Defaults();
            try
            {
                foreach (AeroTimeSlot t in System.Enum.GetValues(typeof(AeroTimeSlot)))
                foreach (AeroWeatherKind w in System.Enum.GetValues(typeof(AeroWeatherKind)))
                {
                    var spring = Look(library, t, w, AeroSeason.Spring); var summer = Look(library, t, w, AeroSeason.Summer);
                    var autumn = Look(library, t, w, AeroSeason.Autumn); var winter = Look(library, t, w, AeroSeason.Winter);
                    Assert.That(autumn.Warmth, Is.GreaterThan(summer.Warmth), $"{t}/{w}: autumn is the warmest season.");
                    Assert.That(summer.Warmth, Is.GreaterThan(spring.Warmth), $"{t}/{w}");
                    Assert.That(winter.Warmth, Is.LessThan(spring.Warmth), $"{t}/{w}: winter is the coolest.");
                    Assert.That(summer.saturation, Is.GreaterThan(winter.saturation), $"{t}/{w}: summer is the most vivid.");
                    Assert.That(summer.saturation, Is.GreaterThanOrEqualTo(spring.saturation), $"{t}/{w}");
                    Assert.That(spring.colorFilter.g, Is.GreaterThan(spring.colorFilter.r), $"{t}/{w}: spring leans fresh green.");
                    Assert.That(autumn.fogDensityScale, Is.GreaterThan(summer.fogDensityScale), $"{t}/{w}: autumn haze.");
                }
                var day = AeroTimeSlot.MidDay; var clear = AeroWeatherKind.Clear;
                Assert.That(Density(library.Compose(day, clear, AeroSeason.Spring), AeroParticleKind.Petal), Is.GreaterThan(0), "Spring petals.");
                Assert.That(Density(library.Compose(day, clear, AeroSeason.Summer), AeroParticleKind.Dot), Is.GreaterThan(0), "Summer motes.");
                Assert.That(Density(library.Compose(day, clear, AeroSeason.Autumn), AeroParticleKind.Leaf), Is.GreaterThan(0), "Autumn leaves.");
                Assert.That(Density(library.Compose(day, clear, AeroSeason.Winter), AeroParticleKind.Flake), Is.GreaterThan(0), "Winter snow.");
                var summerNight = library.Compose(AeroTimeSlot.Midnight, clear, AeroSeason.Summer);
                Assert.That(summerNight.particles.Any(p => p.density > 0 && p.Glows && p.kind == AeroParticleKind.Dot), Is.True, "Summer fireflies glow at night.");
                Assert.That(Density(summerNight, AeroParticleKind.Petal) + Density(summerNight, AeroParticleKind.Leaf), Is.Zero);
                Assert.That(Density(library.Compose(day, AeroWeatherKind.Rain, AeroSeason.Autumn), AeroParticleKind.Leaf),
                    Is.LessThan(Density(library.Compose(day, clear, AeroSeason.Autumn), AeroParticleKind.Leaf)), "Rain clears floating leaves.");
                Assert.That(Density(library.Compose(day, AeroWeatherKind.Rain, AeroSeason.Winter), AeroParticleKind.Flake),
                    Is.GreaterThan(Density(library.Compose(day, clear, AeroSeason.Winter), AeroParticleKind.Flake)), "Clouds and rain thicken the snow.");
                foreach (var (t, w, s) in All())
                {
                    var frame = library.Compose(t, w, s);
                    Assert.That(frame.particles.Length, Is.EqualTo(4));
                    Assert.That(frame.particles.Sum(p => p.density), Is.InRange(0, 2), $"{t}/{w}/{s}: particles stay light.");
                    foreach (var p in frame.particles.Where(p => p.density > 0))
                        Assert.That(p.color.maxColorComponent, Is.InRange(.05f, 4f), $"{t}/{w}/{s} {p.kind}");
                }
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void TimeBlend_IsExactAtEachPresetAndHasNoPopsBetween()
        {
            var library = Defaults();
            try
            {
                var weights = new float[] { 1, 0, 0 };
                foreach (AeroTimeSlot t in System.Enum.GetValues(typeof(AeroTimeSlot)))
                {
                    var exact = library.Compose(library.TimeOf(t), weights, AeroSeason.Summer, AeroSeason.Summer, 1).look;
                    Assert.That(JsonUtility.ToJson(exact), Is.EqualTo(JsonUtility.ToJson(Look(library, t, AeroWeatherKind.Clear, AeroSeason.Summer))), t.ToString());
                    Assert.That(library.NearestSlot(library.TimeOf(t) + .01f), Is.EqualTo(t));
                }
                var previous = library.Compose(0, weights, AeroSeason.Summer, AeroSeason.Summer, 1).look;
                for (int i = 1; i <= 1440; i++)
                {
                    var look = library.Compose(i / 1440f, weights, AeroSeason.Summer, AeroSeason.Summer, 1).look;
                    Assert.That(Mathf.Abs(look.temperature - previous.temperature), Is.LessThan(.5f), $"minute {i}");
                    Assert.That(Mathf.Abs(look.exposure - previous.exposure), Is.LessThan(.01f), $"minute {i}");
                    Assert.That(Mathf.Abs(look.fogColor.grayscale - previous.fogColor.grayscale), Is.LessThan(.02f), $"minute {i}");
                    previous = look;
                }
                var half = library.Compose(.5f, new float[] { .5f, 0, .5f }, AeroSeason.Autumn, AeroSeason.Winter, .5f).look;
                var clearAutumn = Look(library, AeroTimeSlot.MidDay, AeroWeatherKind.Clear, AeroSeason.Autumn);
                var rainWinter = Look(library, AeroTimeSlot.MidDay, AeroWeatherKind.Rain, AeroSeason.Winter);
                Assert.That(half.temperature, Is.InRange(Mathf.Min(clearAutumn.temperature, rainWinter.temperature), Mathf.Max(clearAutumn.temperature, rainWinter.temperature)), "Weather and season crossfades blend.");
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void PermutationOverride_ChangesOnlyItsOwnPreset()
        {
            var library = Defaults();
            try
            {
                var before = Look(library, AeroTimeSlot.Night, AeroWeatherKind.Rain, AeroSeason.Spring);
                var neighbour = Look(library, AeroTimeSlot.Night, AeroWeatherKind.Rain, AeroSeason.Summer);
                var adjust = AeroAtmosphereAdjust.Identity; adjust.exposure = .1f; adjust.saturation = 3;
                library.overrides.Add(new AeroAtmospherePresetLibrary.PermutationOverride { time = AeroTimeSlot.Night, weather = AeroWeatherKind.Rain, season = AeroSeason.Spring, adjust = adjust });
                var after = Look(library, AeroTimeSlot.Night, AeroWeatherKind.Rain, AeroSeason.Spring);
                Assert.That(after.exposure, Is.EqualTo(before.exposure + .1f).Within(1e-4f));
                Assert.That(after.saturation, Is.EqualTo(before.saturation + 3).Within(1e-4f));
                Assert.That(JsonUtility.ToJson(Look(library, AeroTimeSlot.Night, AeroWeatherKind.Rain, AeroSeason.Summer)), Is.EqualTo(JsonUtility.ToJson(neighbour)));
            }
            finally { Object.DestroyImmediate(library); }
        }

        [Test]
        public void LibraryAsset_HoldsEveryLayerAndTheShaderCompiles()
        {
            var library = AssetDatabase.LoadAssetAtPath<AeroAtmospherePresetLibrary>(LibraryPath);
            Assert.That(library, Is.Not.Null);
            Assert.That(library.times.Select(t => t.slot).OrderBy(x => x), Is.EqualTo(System.Enum.GetValues(typeof(AeroTimeSlot)).Cast<AeroTimeSlot>()));
            Assert.That(library.weathers.Select(t => t.weather).OrderBy(x => x), Is.EqualTo(System.Enum.GetValues(typeof(AeroWeatherKind)).Cast<AeroWeatherKind>()));
            Assert.That(library.seasons.Select(t => t.season).OrderBy(x => x), Is.EqualTo(System.Enum.GetValues(typeof(AeroSeason)).Cast<AeroSeason>()));
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            Assert.That(shader.isSupported, Is.True);
        }

        [Test]
        public void Director_LayersTheGradeOverRestrainedAndRemembersTheSeason()
        {
            var go = new GameObject("Atmosphere director test") { hideFlags = HideFlags.HideAndDontSave };
            var baseObject = new GameObject("Base volume") { hideFlags = HideFlags.HideAndDontSave };
            var library = Defaults();
            var director = go.AddComponent<AeroAtmosphereDirector>();
            try
            {
                var baseVolume = baseObject.AddComponent<Volume>(); baseVolume.isGlobal = true; baseVolume.priority = 0; baseVolume.enabled = false;
                var store = new MemoryAeroSettingsStore(); store.SetInt(AeroAtmosphereDirector.SeasonKey, (int)AeroSeason.Winter);
                director.library = library; director.baseVolume = baseVolume; director.Store = store;
                director.particleShader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
                director.fallbackTimeOfDay = library.TimeOf(AeroTimeSlot.Afternoon);
                director.Tick(0);
                Assert.That(director.Season, Is.EqualTo(AeroSeason.Winter), "The saved season is restored.");
                var grade = director.GradeVolume;
                Assert.That(grade, Is.Not.Null); Assert.That(grade.isGlobal, Is.True); Assert.That(grade.priority, Is.EqualTo(1));
                Assert.That(grade.GetComponent<Volume>().sharedProfile.TryGet<UnityEngine.Rendering.Universal.ColorAdjustments>(out var color), Is.True);
                Assert.That(color.saturation.value, Is.EqualTo(director.Current.look.saturation).Within(1e-4f));
                Assert.That(director.Current.slot, Is.EqualTo(AeroTimeSlot.Afternoon));
                Assert.That(director.Particles, Is.Not.Null, "Particles build from the shader.");
                Assert.That(director.Particles.Rates.Sum(), Is.Zero, "Particles only emit in Play Mode.");

                director.SetWeatherOverride(AeroWeatherKind.Rain, true); director.Tick(0);
                Assert.That(director.WeatherWeights, Is.EqualTo(new float[] { 0, 0, 1 }));
                Assert.That(director.CurrentIndex, Is.EqualTo(AeroAtmospherePresetLibrary.IndexOf(AeroTimeSlot.Afternoon, AeroWeatherKind.Rain, AeroSeason.Winter)));
                Assert.That(director.Description, Does.Contain("Winter").And.Contain("Rain").And.Contain("Afternoon"));

                director.SetSeason(AeroSeason.Spring);
                Assert.That(store.values[AeroAtmosphereDirector.SeasonKey], Is.EqualTo((int)AeroSeason.Spring));
                Assert.That(director.SeasonBlend, Is.Zero, "Seasons crossfade.");
                director.Tick(director.seasonBlendSeconds * .5f);
                Assert.That(director.SeasonBlend, Is.InRange(.4f, .6f));
                director.Tick(director.seasonBlendSeconds);
                Assert.That(director.SeasonBlend, Is.EqualTo(1));
                Assert.That(JsonUtility.ToJson(director.Current.look), Is.EqualTo(JsonUtility.ToJson(Look(library, AeroTimeSlot.Afternoon, AeroWeatherKind.Rain, AeroSeason.Spring))));
                Assert.That(director.CycleSeason(), Is.EqualTo(AeroSeason.Summer));

                director.SetWeatherOverride(null); director.Tick(10);
                Assert.That(director.WeatherWeights[0], Is.EqualTo(1).Within(1e-4f), "No rain and no clouds read as clear.");
                Assert.That(AeroAtmosphereDirector.Summarize(director.Current), Does.Contain("Fog").And.Contain("Particles"));
            }
            finally
            {
                director.Shutdown();
                Assert.That(go.transform.childCount, Is.Zero, "Shutdown removes the grade volume and particles.");
                Object.DestroyImmediate(go); Object.DestroyImmediate(baseObject); Object.DestroyImmediate(library);
            }
        }

        [Test]
        public void DayNightCycle_AppliesTheGradeToSunAmbientAndFog()
        {
            var profile = AssetDatabase.LoadAssetAtPath<AeroDayNightProfile>("Assets/_Project/Environment/Lighting/AeroDayNight.asset");
            Assert.That(profile, Is.Not.Null);
            var go = new GameObject("Grade cycle test") { hideFlags = HideFlags.HideAndDontSave };
            var sunObject = new GameObject("Grade sun") { hideFlags = HideFlags.HideAndDontSave };
            var fog = (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode, RenderSettings.sun,
                RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                RenderSettings.ambientProbe, RenderSettings.reflectionIntensity, Shader.GetGlobalFloat("_AeroDaylight"));
            try
            {
                var sun = sunObject.AddComponent<Light>(); sun.type = LightType.Directional; sun.shadowStrength = .9f;
                var cycle = go.AddComponent<AeroDayNightCycle>(); cycle.profile = profile; cycle.sun = sun;
                cycle.ApplyTime(.5f);
                float plainIntensity = sun.intensity, plainDensity = RenderSettings.fogDensity; var plainSky = RenderSettings.ambientSkyColor;
                var look = AeroAtmosphereLook.Restrained; look.sunScale = .5f; look.ambientScale = 2; look.shadowStrength = .3f;
                look.fogColor = new Color(.2f, .4f, .6f); look.fogDensityScale = 2;
                cycle.grade = AeroLightGrade.From(look);
                cycle.ApplyTime(.5f);
                Assert.That(sun.intensity, Is.EqualTo(plainIntensity * .5f).Within(1e-3f));
                Assert.That(sun.shadowStrength, Is.EqualTo(.3f).Within(1e-4f));
                Assert.That(RenderSettings.fogColor.b, Is.EqualTo(.6f).Within(1e-3f));
                Assert.That(RenderSettings.fogDensity, Is.EqualTo(plainDensity * 2).Within(1e-6f));
                Assert.That(RenderSettings.ambientSkyColor.g, Is.EqualTo(plainSky.g * 2).Within(1e-3f));
                cycle.grade = AeroLightGrade.Identity; cycle.ApplyTime(.5f);
                Assert.That(sun.shadowStrength, Is.EqualTo(.9f).Within(1e-4f), "The scene's shadow strength comes back without a grade.");
                Assert.That(sun.intensity, Is.EqualTo(plainIntensity).Within(1e-3f));
            }
            finally
            {
                Object.DestroyImmediate(go); Object.DestroyImmediate(sunObject);
                (RenderSettings.fog, RenderSettings.fogColor, RenderSettings.fogDensity, RenderSettings.fogMode, RenderSettings.sun,
                    RenderSettings.ambientMode, RenderSettings.ambientSkyColor, RenderSettings.ambientEquatorColor, RenderSettings.ambientGroundColor,
                    RenderSettings.ambientProbe, RenderSettings.reflectionIntensity) = (fog.Item1, fog.Item2, fog.Item3, fog.Item4, fog.Item5, fog.Item6, fog.Item7, fog.Item8, fog.Item9, fog.Item10, fog.Item11);
                Shader.SetGlobalFloat("_AeroDaylight", fog.Item12);
            }
        }

        [Test]
        public void TowerScene_DirectorIsWiredToTheSkyWeatherAndParticles()
        {
            TowerSceneAccess.With(scene =>
            {
                var director = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<AeroAtmosphereDirector>(true)).Single();
                Assert.That(director.library, Is.EqualTo(AssetDatabase.LoadAssetAtPath<AeroAtmospherePresetLibrary>(LibraryPath)));
                Assert.That(director.dayNight, Is.Not.Null); Assert.That(director.wetness, Is.Not.Null); Assert.That(director.clouds, Is.Not.Null);
                Assert.That(director.tower, Is.Not.Null); Assert.That(director.baseVolume, Is.Not.Null);
                Assert.That(director.particleShader, Is.EqualTo(AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath)));
                Assert.That(director.cloudyClouds, Is.Not.Null); Assert.That(director.cloudyClouds.look.coverage, Is.GreaterThan(director.cloudyCoverage.x), "The forced cloudy sky reads as cloudy.");
                Assert.That(director.defaultSeason, Is.EqualTo(AeroSeason.Summer));
                Assert.That(director.particlesEnabled, Is.True);
            });
        }
    }
}
