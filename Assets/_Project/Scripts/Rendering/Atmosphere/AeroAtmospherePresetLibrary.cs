using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Atmosphere presets: 5 times of day x 3 weathers x 4 seasons = 60 permutations.
    /// Each time of day is a complete look (global volume, lighting, fog, particle light); weather and season are
    /// adjustments layered on top, and any permutation can carry its own extra adjustment for review fixes.
    /// Defaults follow Documentation/FrutigerAero/Lighting/ATMOSPHERE_PRESETS.md: Restrained-centred, gently saturated,
    /// low contrast, never a hard grade.
    /// </summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Atmosphere preset library")]
    public sealed class AeroAtmospherePresetLibrary : ScriptableObject
    {
        public const int TimeCount = 5, WeatherCount = 3, SeasonCount = 4;
        public const int PermutationCount = TimeCount * WeatherCount * SeasonCount;

        [Serializable]
        public struct TimePreset
        {
            public AeroTimeSlot slot;
            [Tooltip("Normalized time of day this look is exact at (0 midnight, .5 noon).")]
            [Range(0, 1)] public float time;
            [TextArea(1, 3)] public string notes;
            public AeroAtmosphereLook look;
        }

        [Serializable]
        public struct WeatherPreset
        {
            public AeroWeatherKind weather;
            [TextArea(1, 3)] public string notes;
            public AeroAtmosphereAdjust adjust;
            [Tooltip("Particle amount for seasons whose particles are not precipitation (pollen, petals, leaves).")]
            [Range(0, 3)] public float particleScale;
            [Tooltip("Particle amount for seasons whose particles are precipitation (snow).")]
            [Range(0, 3)] public float precipitationScale;
        }

        [Serializable]
        public struct SeasonPreset
        {
            public AeroSeason season;
            [TextArea(1, 3)] public string notes;
            public AeroAtmosphereAdjust adjust;
            public AeroParticleLayer dayParticles;
            public AeroParticleLayer nightParticles;
            [Tooltip("Particles behave like precipitation: rain and clouds increase them instead of clearing them.")]
            public bool precipitation;
        }

        [Serializable]
        public struct PermutationOverride
        {
            public AeroTimeSlot time;
            public AeroWeatherKind weather;
            public AeroSeason season;
            [TextArea(1, 2)] public string notes;
            public AeroAtmosphereAdjust adjust;
        }

        /// <summary>A resolved permutation: the final look plus up to four weighted particle layers.</summary>
        public struct Frame
        {
            public AeroAtmosphereLook look;
            /// <summary>Season-from day/night, season-to day/night; density already includes every weight.</summary>
            public AeroParticleLayer[] particles;
            public AeroTimeSlot slot;
            public AeroWeatherKind weather;
            public AeroSeason season;
        }

        public TimePreset[] times = DefaultTimes();
        public WeatherPreset[] weathers = DefaultWeathers();
        public SeasonPreset[] seasons = DefaultSeasons();
        public List<PermutationOverride> overrides = new();

        /// <summary>Permutation index (0..59): season-major, then weather, then time.</summary>
        public static int IndexOf(AeroTimeSlot time, AeroWeatherKind weather, AeroSeason season)
            => ((int)season * WeatherCount + (int)weather) * TimeCount + (int)time;

        public static (AeroTimeSlot time, AeroWeatherKind weather, AeroSeason season) Permutation(int index)
        {
            index = ((index % PermutationCount) + PermutationCount) % PermutationCount;
            return ((AeroTimeSlot)(index % TimeCount), (AeroWeatherKind)(index / TimeCount % WeatherCount), (AeroSeason)(index / (TimeCount * WeatherCount)));
        }

        public static string Describe(AeroTimeSlot time, AeroWeatherKind weather, AeroSeason season)
            => $"{AeroAtmosphereNames.Of(season)} · {AeroAtmosphereNames.Of(weather)} · {AeroAtmosphereNames.Of(time)}";

        public TimePreset Time(AeroTimeSlot slot) { foreach (var t in times) if (t.slot == slot) return t; return DefaultTimes()[(int)slot]; }
        public WeatherPreset Weather(AeroWeatherKind kind) { foreach (var w in weathers) if (w.weather == kind) return w; return DefaultWeathers()[(int)kind]; }
        public SeasonPreset Season(AeroSeason season) { foreach (var s in seasons) if (s.season == season) return s; return DefaultSeasons()[(int)season]; }
        public float TimeOf(AeroTimeSlot slot) => Time(slot).time;

        /// <summary>The two time presets around <paramref name="timeOfDay"/> and the smoothed blend between them.</summary>
        public void TimeNeighbours(float timeOfDay, out AeroTimeSlot from, out AeroTimeSlot to, out float blend)
        {
            timeOfDay = Mathf.Repeat(timeOfDay, 1);
            var sorted = new List<TimePreset>(TimeCount);
            for (int i = 0; i < TimeCount; i++) sorted.Add(Time((AeroTimeSlot)i));
            sorted.Sort((a, b) => a.time.CompareTo(b.time));
            int next = sorted.FindIndex(t => t.time > timeOfDay);
            if (next < 0) next = 0;
            int previous = (next - 1 + sorted.Count) % sorted.Count;
            float start = sorted[previous].time, span = Mathf.Repeat(sorted[next].time - start, 1);
            if (span <= 0) span = 1;
            from = sorted[previous].slot; to = sorted[next].slot;
            blend = Mathf.SmoothStep(0, 1, Mathf.Repeat(timeOfDay - start, 1) / span);
        }

        /// <summary>The nearest time preset to a time of day.</summary>
        public AeroTimeSlot NearestSlot(float timeOfDay)
        {
            TimeNeighbours(timeOfDay, out var from, out var to, out float blend);
            return blend < .5f ? from : to;
        }

        /// <summary>One exact permutation, as listed in the review window.</summary>
        public Frame Compose(AeroTimeSlot time, AeroWeatherKind weather, AeroSeason season)
        {
            var weights = new float[WeatherCount]; weights[(int)weather] = 1;
            return Compose(TimeOf(time), weights, season, season, 1);
        }

        /// <summary>
        /// The live blend: time of day interpolates between neighbouring time presets, weather weights
        /// (clear, cloudy, rain) mix the weather layers, and a season change crossfades from one season to another.
        /// </summary>
        public Frame Compose(float timeOfDay, float[] weatherWeights, AeroSeason fromSeason, AeroSeason toSeason, float seasonBlend)
        {
            if (weatherWeights == null || weatherWeights.Length != WeatherCount) throw new ArgumentException("Expected clear, cloudy and rain weights.", nameof(weatherWeights));
            seasonBlend = Mathf.Clamp01(seasonBlend);
            TimeNeighbours(timeOfDay, out var fromSlot, out var toSlot, out float timeBlend);
            var look = AeroAtmosphereLook.Lerp(Time(fromSlot).look, Time(toSlot).look, timeBlend);

            var weatherAdjusts = new AeroAtmosphereAdjust[WeatherCount];
            for (int i = 0; i < WeatherCount; i++) { weatherAdjusts[i] = Weather((AeroWeatherKind)i).adjust; weatherAdjusts[i].particleScale = 1; }
            look = AeroAtmosphereAdjust.Blend(weatherAdjusts, weatherWeights).Apply(look);
            var seasonFrom = Season(fromSeason); var seasonTo = Season(toSeason);
            look = AeroAtmosphereAdjust.Lerp(seasonFrom.adjust, seasonTo.adjust, seasonBlend).Apply(look);

            // Per-permutation fixes, weighted by how much of that permutation is showing.
            foreach (var o in overrides)
            {
                float w = (o.time == fromSlot ? 1 - timeBlend : 0) + (o.time == toSlot ? timeBlend : 0);
                w *= Weight(weatherWeights, o.weather);
                w *= (o.season == fromSeason ? 1 - seasonBlend : 0) + (o.season == toSeason ? seasonBlend : 0);
                if (w > .0001f) look = AeroAtmosphereAdjust.Lerp(AeroAtmosphereAdjust.Identity, o.adjust, w).Apply(look);
            }

            int dominantWeather = 0;
            for (int i = 1; i < WeatherCount; i++) if (weatherWeights[i] > weatherWeights[dominantWeather]) dominantWeather = i;
            return new Frame
            {
                look = look,
                particles = new[]
                {
                    Layer(seasonFrom.dayParticles, seasonFrom, look, weatherWeights, (1 - look.night) * (1 - seasonBlend)),
                    Layer(seasonFrom.nightParticles, seasonFrom, look, weatherWeights, look.night * (1 - seasonBlend)),
                    Layer(seasonTo.dayParticles, seasonTo, look, weatherWeights, (1 - look.night) * seasonBlend),
                    Layer(seasonTo.nightParticles, seasonTo, look, weatherWeights, look.night * seasonBlend)
                },
                slot = timeBlend < .5f ? fromSlot : toSlot,
                weather = (AeroWeatherKind)dominantWeather,
                season = seasonBlend < .5f ? fromSeason : toSeason
            };
        }

        static float Weight(float[] weights, AeroWeatherKind kind)
        {
            float total = 0; foreach (float w in weights) total += Mathf.Max(0, w);
            return total <= 0 ? (kind == AeroWeatherKind.Clear ? 1 : 0) : Mathf.Max(0, weights[(int)kind]) / total;
        }

        AeroParticleLayer Layer(AeroParticleLayer layer, SeasonPreset season, in AeroAtmosphereLook look, float[] weatherWeights, float weight)
        {
            float weather = 0;
            for (int i = 0; i < WeatherCount; i++)
            {
                var preset = Weather((AeroWeatherKind)i);
                weather += Weight(weatherWeights, (AeroWeatherKind)i) * (season.precipitation ? preset.precipitationScale : preset.particleScale);
            }
            layer.density = Mathf.Clamp01(layer.density * weight * weather * look.particleScale);
            var color = layer.color * (layer.Glows ? layer.glow : layer.glow * look.particleLight);
            color.a = 1; layer.color = color;
            return layer;
        }

        public void ResetToDefaults()
        {
            times = DefaultTimes(); weathers = DefaultWeathers(); seasons = DefaultSeasons(); overrides = new List<PermutationOverride>();
        }

        private void OnValidate()
        {
            if (times == null || times.Length != TimeCount) times = DefaultTimes();
            if (weathers == null || weathers.Length != WeatherCount) weathers = DefaultWeathers();
            if (seasons == null || seasons.Length != SeasonCount) seasons = DefaultSeasons();
        }

        // ---- Defaults ---------------------------------------------------------------------------------------------
        // Grey (0.5) split-tone colours are neutral; offsets stay small so tinting reads as mood, not a filter.

        static Color Grey(float r, float g, float b) => new(r, g, b, 1);

        public static TimePreset[] DefaultTimes()
        {
            var r = AeroAtmosphereLook.Restrained;
            var morning = r; // Fresh, misty, cool shadows with a peach sun: the eye wakes up gently.
            morning.exposure = .05f; morning.contrast = 4; morning.saturation = 14; morning.temperature = -3; morning.tint = 2;
            morning.colorFilter = new Color(1, .99f, .98f); morning.bloomIntensity = .6f; morning.bloomThreshold = .93f; morning.bloomTint = new Color(1, .96f, .9f);
            morning.vignette = .12f; morning.splitShadows = Grey(.44f, .5f, .58f); morning.splitHighlights = Grey(.56f, .51f, .46f); morning.splitBalance = 10;
            morning.sunTint = new Color(1, .93f, .84f); morning.sunScale = .9f; morning.ambientTint = new Color(.98f, .98f, 1); morning.ambientScale = 1;
            morning.shadowStrength = .75f; morning.skyExposureScale = 1; morning.fogColor = new Color(.76f, .82f, .88f); morning.fogDensityScale = 1.4f;
            morning.night = 0; morning.particleLight = 1;

            var midDay = r; // The reference landscape: clear sky blue, grass green, crisp white marble.
            midDay.exposure = .05f; midDay.contrast = 7; midDay.saturation = 16; midDay.temperature = -7; midDay.tint = 2;
            midDay.colorFilter = Color.white; midDay.bloomIntensity = .55f; midDay.bloomThreshold = .95f; midDay.bloomTint = new Color(.92f, .97f, 1);
            midDay.vignette = .12f; midDay.splitShadows = Grey(.46f, .5f, .56f); midDay.splitHighlights = Grey(.51f, .51f, .5f); midDay.splitBalance = 0;
            midDay.sunTint = new Color(1, 1, .98f); midDay.sunScale = 1.05f; midDay.ambientTint = new Color(.98f, 1, 1.02f); midDay.ambientScale = 1.05f;
            midDay.shadowStrength = .85f; midDay.skyExposureScale = 1.05f; midDay.fogColor = new Color(.64f, .79f, .9f); midDay.fogDensityScale = 1;
            midDay.night = 0; midDay.particleLight = 1;

            var afternoon = r; // Golden hour: warm low sun, long soft shadows, honeyed haze, teal shade.
            afternoon.exposure = .03f; afternoon.contrast = 6; afternoon.saturation = 17; afternoon.temperature = 5; afternoon.tint = 1;
            afternoon.colorFilter = new Color(1, .98f, .95f); afternoon.bloomIntensity = .65f; afternoon.bloomThreshold = .92f; afternoon.bloomTint = new Color(1, .93f, .82f);
            afternoon.vignette = .13f; afternoon.splitShadows = Grey(.44f, .51f, .57f); afternoon.splitHighlights = Grey(.58f, .53f, .45f); afternoon.splitBalance = 10;
            afternoon.sunTint = new Color(1, .87f, .7f); afternoon.sunScale = .95f; afternoon.ambientTint = new Color(1, .96f, .9f); afternoon.ambientScale = 1;
            afternoon.shadowStrength = .75f; afternoon.skyExposureScale = 1; afternoon.fogColor = new Color(.8f, .79f, .76f); afternoon.fogDensityScale = 1.2f;
            afternoon.night = 0; afternoon.particleLight = 1.05f;

            var night = r; // Blue hour into night: moonlit blue (Purkinje cheat), lifted so silhouettes stay readable.
            night.exposure = .12f; night.contrast = 5; night.saturation = 11; night.temperature = -10; night.tint = 3;
            night.colorFilter = new Color(.97f, .99f, 1.03f); night.bloomIntensity = .75f; night.bloomThreshold = .9f; night.bloomTint = new Color(.8f, .92f, 1);
            night.vignette = .16f; night.splitShadows = Grey(.45f, .48f, .58f); night.splitHighlights = Grey(.52f, .5f, .56f); night.splitBalance = -10;
            night.sunTint = Color.white; night.sunScale = 1; night.ambientTint = new Color(.9f, .95f, 1.05f); night.ambientScale = 1.15f;
            night.shadowStrength = .6f; night.skyExposureScale = 1; night.fogColor = new Color(.2f, .3f, .48f); night.fogDensityScale = 1.1f;
            night.night = 1; night.particleLight = .55f;

            var midnight = night; // Deepest, calmest blue; still never black.
            midnight.exposure = -.12f; midnight.contrast = 5; midnight.saturation = 10; midnight.temperature = -13; midnight.tint = 4;
            midnight.bloomIntensity = .8f; midnight.bloomTint = new Color(.75f, .9f, 1); midnight.vignette = .18f;
            midnight.splitShadows = Grey(.46f, .47f, .57f); midnight.ambientTint = new Color(.88f, .93f, 1.06f); midnight.ambientScale = .85f;
            midnight.shadowStrength = .5f; midnight.skyExposureScale = .65f; midnight.fogColor = new Color(.11f, .17f, .31f); midnight.fogDensityScale = .9f;
            midnight.night = 1; midnight.particleLight = .45f;

            return new[]
            {
                new TimePreset { slot = AeroTimeSlot.Morning, time = 7f / 24, look = morning, notes = "07:00 fresh mist, peach sun, cool shade." },
                new TimePreset { slot = AeroTimeSlot.MidDay, time = .5f, look = midDay, notes = "12:00 clean sky blue and grass green; the accepted Restrained baseline, slightly richer." },
                new TimePreset { slot = AeroTimeSlot.Afternoon, time = 16.5f / 24, look = afternoon, notes = "16:30 golden hour: warm low sun, teal shadows, honey haze." },
                new TimePreset { slot = AeroTimeSlot.Night, time = 21f / 24, look = night, notes = "21:00 blue hour into night, moonlit and readable." },
                new TimePreset { slot = AeroTimeSlot.Midnight, time = 0, look = midnight, notes = "00:00 deepest calm blue; fireflies and lamps carry the glow." }
            };
        }

        public static WeatherPreset[] DefaultWeathers()
        {
            var clear = AeroAtmosphereAdjust.Identity;
            clear.saturation = 1;

            // Diffuse light: softer shadows, less contrast, a pearly haze. Volumetric clouds already shade the sun.
            var cloudy = AeroAtmosphereAdjust.Identity;
            cloudy.exposure = -.03f; cloudy.contrast = -2; cloudy.saturation = -3; cloudy.temperature = -2; cloudy.vignette = .01f;
            cloudy.bloomScale = .85f; cloudy.splitScale = .7f; cloudy.sunScale = .9f; cloudy.ambientTint = new Color(.99f, 1, 1.02f); cloudy.ambientScale = 1.08f;
            cloudy.shadowScale = .6f; cloudy.skyExposureScale = .95f; cloudy.fogTint = new Color(.98f, 1, 1.02f); cloudy.fogDesaturate = .2f; cloudy.fogDensityScale = 1.2f;

            // Calm rain, not a storm: cooler, a little muted, misty depth, wet highlights glow slightly.
            // AeroRainController's overcast already greys the sky and thickens fog, so these stay mild.
            var rain = AeroAtmosphereAdjust.Identity;
            rain.exposure = -.05f; rain.contrast = -1; rain.saturation = -5; rain.temperature = -4; rain.tint = -1; rain.vignette = .02f;
            rain.colorFilter = new Color(.97f, .99f, 1.02f); rain.bloomScale = 1.1f; rain.splitScale = .8f; rain.ambientScale = 1.05f;
            rain.shadowScale = .4f; rain.fogTint = new Color(.96f, 1, 1.03f); rain.fogDesaturate = .15f; rain.fogDensityScale = 1.3f;

            return new[]
            {
                new WeatherPreset { weather = AeroWeatherKind.Clear, adjust = clear, particleScale = 1, precipitationScale = .55f, notes = "Full sun, crisp shadows, a touch more colour." },
                new WeatherPreset { weather = AeroWeatherKind.Cloudy, adjust = cloudy, particleScale = .7f, precipitationScale = 1, notes = "Soft diffuse light, pearly haze, gentle shadows." },
                new WeatherPreset { weather = AeroWeatherKind.Rain, adjust = rain, particleScale = .2f, precipitationScale = 1.7f, notes = "Cosy rain: cool, misty, shadows almost gone; snow thickens in winter." }
            };
        }

        public static SeasonPreset[] DefaultSeasons()
        {
            var spring = AeroAtmosphereAdjust.Identity; // Fresh pastel green, light and airy.
            spring.exposure = .03f; spring.contrast = -1; spring.saturation = 3; spring.temperature = -1; spring.tint = -2;
            spring.colorFilter = new Color(.98f, 1.02f, .99f); spring.bloomScale = 1.05f; spring.sunTint = new Color(1, .99f, .95f);
            spring.ambientTint = new Color(.98f, 1.03f, .98f); spring.fogTint = new Color(.99f, 1.02f, 1); spring.fogDensityScale = 1.1f;

            var summer = AeroAtmosphereAdjust.Identity; // The Frutiger Aero peak: vivid sky and grass, warm bright sun.
            summer.exposure = .03f; summer.contrast = 1; summer.saturation = 4; summer.temperature = 2;
            summer.colorFilter = new Color(1.01f, 1, .98f); summer.sunTint = new Color(1, .98f, .92f); summer.sunScale = 1.05f;
            summer.fogDensityScale = .9f;

            var autumn = AeroAtmosphereAdjust.Identity; // Warm amber and honey; muted, hazier, still soft.
            autumn.contrast = 1; autumn.saturation = 1; autumn.temperature = 7; autumn.tint = 1; autumn.splitBalance = 5;
            autumn.colorFilter = new Color(1.03f, .99f, .93f); autumn.bloomScale = 1.05f; autumn.splitScale = 1.2f;
            autumn.sunTint = new Color(1, .91f, .76f); autumn.sunScale = .95f; autumn.ambientTint = new Color(1.02f, .97f, .9f);
            autumn.fogTint = new Color(1.05f, 1, .92f); autumn.fogDensityScale = 1.35f;

            var winter = AeroAtmosphereAdjust.Identity; // Cool, clean, bright; desaturated but never grey.
            winter.exposure = .06f; winter.contrast = 1; winter.saturation = -5; winter.temperature = -8; winter.tint = 1;
            winter.colorFilter = new Color(.97f, 1, 1.04f); winter.bloomScale = 1.1f; winter.sunTint = new Color(1, .98f, .96f); winter.sunScale = .85f;
            winter.ambientTint = new Color(.94f, .98f, 1.06f); winter.ambientScale = 1.05f;
            winter.fogTint = new Color(.95f, 1, 1.06f); winter.fogDesaturate = .15f; winter.fogDensityScale = 1.25f;

            return new[]
            {
                new SeasonPreset
                {
                    season = AeroSeason.Spring, adjust = spring, notes = "Pastel green freshness, drifting blossom petals; pollen glow at night.",
                    dayParticles = new AeroParticleLayer { kind = AeroParticleKind.Petal, color = new Color(1, .86f, .92f), density = .55f, size = .09f, fallSpeed = .35f, drift = .6f, glow = 1 },
                    nightParticles = new AeroParticleLayer { kind = AeroParticleKind.Dot, color = new Color(.55f, 1, .85f), density = .35f, size = .05f, fallSpeed = -.03f, drift = .25f, glow = 2.2f }
                },
                new SeasonPreset
                {
                    season = AeroSeason.Summer, adjust = summer, notes = "Vivid sky and grass, golden dust motes by day, fireflies at night.",
                    dayParticles = new AeroParticleLayer { kind = AeroParticleKind.Dot, color = new Color(1, .95f, .78f), density = .5f, size = .045f, fallSpeed = .03f, drift = .3f, glow = 1.15f },
                    nightParticles = new AeroParticleLayer { kind = AeroParticleKind.Dot, color = new Color(.75f, 1, .45f), density = .8f, size = .06f, fallSpeed = -.02f, drift = .35f, glow = 3 }
                },
                new SeasonPreset
                {
                    season = AeroSeason.Autumn, adjust = autumn, notes = "Amber light, misty depth, tumbling leaves; warm motes at night.",
                    dayParticles = new AeroParticleLayer { kind = AeroParticleKind.Leaf, color = new Color(1, .6f, .25f), density = .5f, size = .16f, fallSpeed = .9f, drift = 1.2f, glow = 1 },
                    nightParticles = new AeroParticleLayer { kind = AeroParticleKind.Dot, color = new Color(1, .75f, .45f), density = .25f, size = .05f, fallSpeed = 0, drift = .25f, glow = 1.6f }
                },
                new SeasonPreset
                {
                    season = AeroSeason.Winter, adjust = winter, precipitation = true, notes = "Cool clean light, soft snowfall that thickens with clouds and rain.",
                    dayParticles = new AeroParticleLayer { kind = AeroParticleKind.Flake, color = Color.white, density = .7f, size = .07f, fallSpeed = .8f, drift = .5f, glow = 1 },
                    nightParticles = new AeroParticleLayer { kind = AeroParticleKind.Flake, color = new Color(.9f, .95f, 1), density = .6f, size = .07f, fallSpeed = .7f, drift = .45f, glow = 1 }
                }
            };
        }
    }
}
