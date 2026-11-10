using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>How visible the tower's sun shafts are for a time of day, weather and season (AeroShaftLook).</summary>
    public sealed class AeroShaftLookEditModeTests
    {
        private const string SettingsPath = "Assets/_Project/Environment/Lighting/TowerInteriorRays.asset";
        private AeroRaySettings settings;
        private AeroAtmospherePresetLibrary library;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<AeroRaySettings>();
            library = ScriptableObject.CreateInstance<AeroAtmospherePresetLibrary>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
            Object.DestroyImmediate(library);
        }

        /// <summary>Direction of a key light <paramref name="degrees"/> above the horizon.</summary>
        private static Vector3 Light(float degrees) => Quaternion.Euler(degrees, 330, 0) * Vector3.forward;

        private float Gain(float time, AeroWeatherKind weather, AeroSeason season, bool moon = false)
        {
            var weights = new float[AeroAtmospherePresetLibrary.WeatherCount]; weights[(int)weather] = 1;
            var grade = AeroLightGrade.From(library.Compose(time, weights, season, season, 1).look);
            // Same sun path as AeroDayNightCycle: the elevation angle is the time of day around the clock.
            float elevation = Mathf.Abs(Mathf.Asin(Mathf.Sin((time - .25f) * Mathf.PI * 2)) * Mathf.Rad2Deg);
            return AeroShaftLook.SunGain(settings, Light(Mathf.Max(1, elevation)), moon, grade);
        }

        [Test]
        public void LowSun_DrawsStrongerShaftsThanHighSun()
        {
            float low = AeroShaftLook.ElevationGain(Mathf.Sin(20 * Mathf.Deg2Rad), settings);
            float mid = AeroShaftLook.ElevationGain(Mathf.Sin(50 * Mathf.Deg2Rad), settings);
            float high = AeroShaftLook.ElevationGain(1, settings);
            Assert.That(low, Is.EqualTo(settings.lowSunGain).Within(1e-4f), "Morning and golden-hour sun rakes through the arches at full strength.");
            Assert.That(high, Is.EqualTo(settings.highSunGain).Within(1e-4f));
            Assert.That(low, Is.GreaterThan(mid).And.GreaterThan(high));
            Assert.That(mid, Is.GreaterThan(high), "The gain falls steadily as the sun climbs.");
            Assert.That(high, Is.GreaterThan(.5f), "Noon shafts stay visible near the openings.");
        }

        [Test]
        public void SunGain_FollowsTheKeyLightDirectionWithoutAGrade()
        {
            var none = AeroLightGrade.Identity;
            Assert.That(AeroShaftLook.SunGain(settings, Light(20), false, none), Is.EqualTo(settings.sunStrength * settings.lowSunGain).Within(1e-4f));
            Assert.That(AeroShaftLook.SunGain(settings, Light(90), false, none), Is.EqualTo(settings.sunStrength * settings.highSunGain).Within(1e-4f));
            Assert.That(AeroShaftLook.SunGain(null, Light(20), false, none), Is.EqualTo(1), "No settings: the shader's neutral gain.");
            Assert.That(AeroShaftLook.SunGain(null, null, null), Is.EqualTo(1));
            Assert.That(AeroShaftLook.SunGain(settings, null, null), Is.EqualTo(settings.sunStrength));
        }

        [Test]
        public void HazierAir_CarriesMoreLight()
        {
            Assert.That(AeroShaftLook.HazeGain(1, settings), Is.EqualTo(1).Within(1e-5f));
            Assert.That(AeroShaftLook.HazeGain(1.4f, settings), Is.GreaterThan(1.2f));
            Assert.That(AeroShaftLook.HazeGain(.8f, settings), Is.LessThan(1));
            Assert.That(AeroShaftLook.HazeGain(100, settings), Is.LessThanOrEqualTo(2.5f), "Bounded even for extreme fog.");
            float mistyAutumnMorning = Gain(7f / 24, AeroWeatherKind.Clear, AeroSeason.Autumn);
            float summerNoon = Gain(.5f, AeroWeatherKind.Clear, AeroSeason.Summer);
            Assert.That(mistyAutumnMorning, Is.GreaterThan(summerNoon * 1.6f), "A misty autumn morning shows far stronger shafts than a clear summer noon.");
        }

        [Test]
        public void Seasons_OrderShaftsByTheirHaze()
        {
            float time = 16.5f / 24;
            float summer = Gain(time, AeroWeatherKind.Clear, AeroSeason.Summer), spring = Gain(time, AeroWeatherKind.Clear, AeroSeason.Spring);
            float winter = Gain(time, AeroWeatherKind.Clear, AeroSeason.Winter), autumn = Gain(time, AeroWeatherKind.Clear, AeroSeason.Autumn);
            Assert.That(autumn, Is.GreaterThan(winter)); Assert.That(winter, Is.GreaterThan(spring)); Assert.That(spring, Is.GreaterThan(summer));
            Assert.That(summer, Is.GreaterThan(settings.sunStrength), "Every season keeps golden-hour shafts above the base strength.");
        }

        [Test]
        public void CloudsAndRain_SoftenShaftsButKeepThem()
        {
            float time = 7f / 24;
            float clear = Gain(time, AeroWeatherKind.Clear, AeroSeason.Spring), cloudy = Gain(time, AeroWeatherKind.Cloudy, AeroSeason.Spring), rain = Gain(time, AeroWeatherKind.Rain, AeroSeason.Spring);
            Assert.That(cloudy, Is.LessThan(clear * .9f)); Assert.That(rain, Is.LessThan(cloudy));
            Assert.That(rain, Is.GreaterThan(clear * .3f), "Rain dims the light itself as well; the shaft gain must not remove what is left.");
            Assert.That(AeroShaftLook.DirectLightGain(1, settings), Is.EqualTo(1));
            Assert.That(AeroShaftLook.DirectLightGain(0, settings), Is.EqualTo(settings.diffuseLightGain));
        }

        [Test]
        public void Moonlight_IsLiftedAndIgnoresDaytimeShadowSoftness()
        {
            float night = 21f / 24;
            float moon = Gain(night, AeroWeatherKind.Clear, AeroSeason.Summer, true), sunAtSameAngle = Gain(night, AeroWeatherKind.Clear, AeroSeason.Summer);
            Assert.That(moon, Is.GreaterThan(sunAtSameAngle * 2), "The moon is weak; its shafts need the lift to read at all.");
            var cycleObject = new GameObject("Shaft look cycle"); var moonObject = new GameObject("Shaft look moon");
            try
            {
                var cycle = cycleObject.AddComponent<AeroDayNightCycle>(); cycle.enabled = false;
                cycle.moon = moonObject.AddComponent<Light>(); cycle.moon.transform.rotation = Quaternion.Euler(20, 0, 0);
                Assert.That(AeroShaftLook.SunGain(settings, cycle, cycle.moon),
                    Is.EqualTo(settings.sunStrength * settings.lowSunGain * settings.moonGain).Within(1e-4f), "The cycle's moon is recognised as the key light.");
            }
            finally { Object.DestroyImmediate(cycleObject); Object.DestroyImmediate(moonObject); }
        }

        [Test]
        public void SavedRaySettings_KeepSunShaftsNoticeableAndBounded()
        {
            var saved = AssetDatabase.LoadAssetAtPath<AeroRaySettings>(SettingsPath);
            Assert.That(saved, Is.Not.Null);
            Assert.That(saved.sunStrength, Is.InRange(1.5f, 3), "Below this the shafts vanished against the white marble (2026-11-10).");
            Assert.That(saved.sunDensity, Is.GreaterThan(saved.density * 1.5f).And.LessThanOrEqualTo(.1f), "Sun air is denser than the fills' so near beams read.");
            Assert.That(saved.lowSunGain, Is.GreaterThan(saved.highSunGain));
            Assert.That(saved.moonGain, Is.InRange(1, 4));
            Assert.That(saved.hazeResponse, Is.InRange(.4f, 1));
            Assert.That(saved.diffuseLightGain, Is.InRange(.3f, .7f));
            // The calmer interior accepted on 2026-10-31 stays: fills, interior haze and mist contrast are unchanged.
            Assert.That(saved.intensity, Is.EqualTo(1.15f).Within(1e-3f)); Assert.That(saved.fillStrength, Is.EqualTo(1.5f).Within(1e-3f));
            Assert.That(saved.interiorHaze, Is.EqualTo(.05f).Within(1e-3f)); Assert.That(saved.density, Is.EqualTo(.028f).Within(1e-4f));
        }
    }
}
