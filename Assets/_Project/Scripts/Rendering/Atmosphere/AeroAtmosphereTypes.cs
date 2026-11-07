using System;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    public enum AeroTimeSlot { Morning = 0, MidDay = 1, Afternoon = 2, Night = 3, Midnight = 4 }
    public enum AeroWeatherKind { Clear = 0, Cloudy = 1, Rain = 2 }
    public enum AeroSeason { Spring = 0, Summer = 1, Autumn = 2, Winter = 3 }
    /// <summary>Shape drawn by the ambient particle shader: soft dot (pollen, dust, fireflies), petal, leaf or snowflake.</summary>
    public enum AeroParticleKind { Dot = 0, Petal = 1, Leaf = 2, Flake = 3 }

    public static class AeroAtmosphereNames
    {
        public static string Of(AeroTimeSlot slot) => slot switch
        {
            AeroTimeSlot.MidDay => "Mid-day",
            _ => slot.ToString()
        };
        public static string Of(AeroWeatherKind weather) => weather.ToString();
        public static string Of(AeroSeason season) => season == AeroSeason.Autumn ? "Autumn" : season.ToString();
        public static string Of(AeroParticleKind kind) => kind switch
        {
            AeroParticleKind.Dot => "motes",
            AeroParticleKind.Petal => "petals",
            AeroParticleKind.Leaf => "leaves",
            _ => "snowflakes"
        };
    }

    /// <summary>
    /// One complete atmosphere: global-volume grade, sun/ambient/fog lighting and particle response.
    /// Time-of-day presets are full looks; weather, season and per-permutation overrides adjust them.
    /// Values are absolute and centred on the accepted Restrained grade (contrast 6, saturation 12, temperature -6).
    /// </summary>
    [Serializable]
    public struct AeroAtmosphereLook
    {
        [Header("Global volume")]
        [Range(-1, 1)] public float exposure;
        [Range(-30, 30)] public float contrast;
        [Range(-40, 40)] public float saturation;
        [Tooltip("White balance: negative is cooler (bluer), positive warmer.")]
        [Range(-40, 40)] public float temperature;
        [Tooltip("White balance: negative is greener, positive more magenta.")]
        [Range(-20, 20)] public float tint;
        public Color colorFilter;
        [Range(0, 2)] public float bloomIntensity;
        [Range(.8f, 1.2f)] public float bloomThreshold;
        public Color bloomTint;
        [Range(0, .3f)] public float vignette;
        [Tooltip("Split toning: grey is neutral; small offsets from grey tint shadows / highlights.")]
        public Color splitShadows;
        public Color splitHighlights;
        [Range(-100, 100)] public float splitBalance;

        [Header("Lighting")]
        public Color sunTint;
        [Range(0, 2)] public float sunScale;
        public Color ambientTint;
        [Range(0, 2)] public float ambientScale;
        [Range(0, 1)] public float shadowStrength;
        [Range(0, 2)] public float skyExposureScale;

        [Header("Fog")]
        public Color fogColor;
        [Range(0, 4)] public float fogDensityScale;

        [Header("Particles")]
        [Tooltip("0 uses the season's day particles, 1 its night particles.")]
        [Range(0, 1)] public float night;
        [Tooltip("Brightness of unlit particles (leaves, snow) at this time; glowing particles ignore it.")]
        [Range(0, 2)] public float particleLight;
        [Range(0, 3)] public float particleScale;

        /// <summary>The accepted Restrained grade at noon, neutral lighting and fog.</summary>
        public static AeroAtmosphereLook Restrained => new()
        {
            exposure = 0, contrast = 6, saturation = 12, temperature = -6, tint = 2, colorFilter = Color.white,
            bloomIntensity = .55f, bloomThreshold = .95f, bloomTint = new Color(.92f, .97f, 1), vignette = .14f,
            splitShadows = Color.grey, splitHighlights = Color.grey, splitBalance = 0,
            sunTint = Color.white, sunScale = 1, ambientTint = Color.white, ambientScale = 1, shadowStrength = .85f, skyExposureScale = 1,
            fogColor = new Color(.62f, .76f, .86f), fogDensityScale = 1, night = 0, particleLight = 1, particleScale = 1
        };

        public static AeroAtmosphereLook Lerp(in AeroAtmosphereLook a, in AeroAtmosphereLook b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AeroAtmosphereLook
            {
                exposure = Mathf.Lerp(a.exposure, b.exposure, t), contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t), temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                tint = Mathf.Lerp(a.tint, b.tint, t), colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t),
                bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t), bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t),
                bloomTint = Color.Lerp(a.bloomTint, b.bloomTint, t), vignette = Mathf.Lerp(a.vignette, b.vignette, t),
                splitShadows = Color.Lerp(a.splitShadows, b.splitShadows, t), splitHighlights = Color.Lerp(a.splitHighlights, b.splitHighlights, t),
                splitBalance = Mathf.Lerp(a.splitBalance, b.splitBalance, t),
                sunTint = Color.Lerp(a.sunTint, b.sunTint, t), sunScale = Mathf.Lerp(a.sunScale, b.sunScale, t),
                ambientTint = Color.Lerp(a.ambientTint, b.ambientTint, t), ambientScale = Mathf.Lerp(a.ambientScale, b.ambientScale, t),
                shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t), skyExposureScale = Mathf.Lerp(a.skyExposureScale, b.skyExposureScale, t),
                fogColor = Color.Lerp(a.fogColor, b.fogColor, t), fogDensityScale = Mathf.Lerp(a.fogDensityScale, b.fogDensityScale, t),
                night = Mathf.Lerp(a.night, b.night, t), particleLight = Mathf.Lerp(a.particleLight, b.particleLight, t),
                particleScale = Mathf.Lerp(a.particleScale, b.particleScale, t)
            };
        }

        /// <summary>Warmth score used by the review window: positive reads warm, negative cool.</summary>
        public float Warmth => temperature + (sunTint.r - sunTint.b) * 20 + (colorFilter.r - colorFilter.b) * 30;
    }

    /// <summary>
    /// A relative change layered on a look: offsets add, scales and tints multiply.
    /// Used by weather, season and per-permutation overrides.
    /// </summary>
    [Serializable]
    public struct AeroAtmosphereAdjust
    {
        [Header("Global volume (added)")]
        public float exposure;
        public float contrast;
        public float saturation;
        public float temperature;
        public float tint;
        public float vignette;
        public float splitBalance;
        [Header("Global volume (multiplied)")]
        public Color colorFilter;
        public float bloomScale;
        [Tooltip("Pushes split toning further from neutral (above 1) or towards it (below 1).")]
        public float splitScale;
        [Header("Lighting (multiplied)")]
        public Color sunTint;
        public float sunScale;
        public Color ambientTint;
        public float ambientScale;
        public float shadowScale;
        public float skyExposureScale;
        [Header("Fog")]
        public Color fogTint;
        [Tooltip("Moves the fog colour towards its own grey, 0..1.")]
        [Range(0, 1)] public float fogDesaturate;
        public float fogDensityScale;
        [Header("Particles")]
        public float particleScale;

        public static AeroAtmosphereAdjust Identity => new()
        {
            colorFilter = Color.white, bloomScale = 1, splitScale = 1, sunTint = Color.white, sunScale = 1, ambientTint = Color.white,
            ambientScale = 1, shadowScale = 1, skyExposureScale = 1, fogTint = Color.white, fogDensityScale = 1, particleScale = 1
        };

        public static AeroAtmosphereAdjust Lerp(in AeroAtmosphereAdjust a, in AeroAtmosphereAdjust b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AeroAtmosphereAdjust
            {
                exposure = Mathf.Lerp(a.exposure, b.exposure, t), contrast = Mathf.Lerp(a.contrast, b.contrast, t),
                saturation = Mathf.Lerp(a.saturation, b.saturation, t), temperature = Mathf.Lerp(a.temperature, b.temperature, t),
                tint = Mathf.Lerp(a.tint, b.tint, t), vignette = Mathf.Lerp(a.vignette, b.vignette, t), splitBalance = Mathf.Lerp(a.splitBalance, b.splitBalance, t),
                colorFilter = Color.Lerp(a.colorFilter, b.colorFilter, t), bloomScale = Mathf.Lerp(a.bloomScale, b.bloomScale, t),
                splitScale = Mathf.Lerp(a.splitScale, b.splitScale, t),
                sunTint = Color.Lerp(a.sunTint, b.sunTint, t), sunScale = Mathf.Lerp(a.sunScale, b.sunScale, t),
                ambientTint = Color.Lerp(a.ambientTint, b.ambientTint, t), ambientScale = Mathf.Lerp(a.ambientScale, b.ambientScale, t),
                shadowScale = Mathf.Lerp(a.shadowScale, b.shadowScale, t), skyExposureScale = Mathf.Lerp(a.skyExposureScale, b.skyExposureScale, t),
                fogTint = Color.Lerp(a.fogTint, b.fogTint, t), fogDesaturate = Mathf.Lerp(a.fogDesaturate, b.fogDesaturate, t),
                fogDensityScale = Mathf.Lerp(a.fogDensityScale, b.fogDensityScale, t), particleScale = Mathf.Lerp(a.particleScale, b.particleScale, t)
            };
        }

        /// <summary>Weighted blend of several adjustments (weights need not sum to one; missing weight is identity).</summary>
        public static AeroAtmosphereAdjust Blend(AeroAtmosphereAdjust[] adjusts, float[] weights)
        {
            float total = 0;
            for (int i = 0; i < weights.Length; i++) total += Mathf.Max(0, weights[i]);
            if (total <= 0) return Identity;
            var result = adjusts[0]; float used = Mathf.Max(0, weights[0]);
            for (int i = 1; i < adjusts.Length; i++)
            {
                float w = Mathf.Max(0, weights[i]); if (w <= 0) continue;
                used += w; result = Lerp(result, adjusts[i], w / used);
            }
            return result;
        }

        public AeroAtmosphereLook Apply(AeroAtmosphereLook look)
        {
            look.exposure += exposure; look.contrast += contrast; look.saturation += saturation;
            look.temperature += temperature; look.tint += tint; look.vignette = Mathf.Clamp(look.vignette + vignette, 0, .3f);
            look.splitBalance += splitBalance;
            look.colorFilter *= colorFilter; look.colorFilter.a = 1;
            look.bloomIntensity *= bloomScale;
            look.splitShadows = Color.LerpUnclamped(Color.grey, look.splitShadows, splitScale);
            look.splitHighlights = Color.LerpUnclamped(Color.grey, look.splitHighlights, splitScale);
            look.sunTint *= sunTint; look.sunScale *= sunScale;
            look.ambientTint *= ambientTint; look.ambientScale *= ambientScale;
            look.shadowStrength = Mathf.Clamp01(look.shadowStrength * shadowScale);
            look.skyExposureScale *= skyExposureScale;
            var fog = look.fogColor * fogTint; float grey = fog.grayscale;
            look.fogColor = Color.Lerp(fog, new Color(grey, grey, grey * 1.02f), fogDesaturate); look.fogColor.a = 1;
            look.fogDensityScale *= fogDensityScale;
            look.particleScale *= particleScale;
            return look;
        }
    }

    /// <summary>One layer of floating particles: a shape, colour and motion.</summary>
    [Serializable]
    public struct AeroParticleLayer
    {
        public AeroParticleKind kind;
        [ColorUsage(false, true)] public Color color;
        [Tooltip("0 none .. 1 dense.")]
        [Range(0, 1)] public float density;
        [Tooltip("World size, metres.")]
        [Range(.01f, .5f)] public float size;
        [Tooltip("Metres per second downwards; negative rises.")]
        public float fallSpeed;
        [Tooltip("Sideways wander, metres per second.")]
        [Range(0, 3)] public float drift;
        [Tooltip("1 = lit like the scene; above 1 glows (fireflies) and ignores the time of day.")]
        [Range(0, 6)] public float glow;

        public bool Glows => glow > 1.01f;
    }

    /// <summary>What the director hands the day/night cycle; identity when no director is active.</summary>
    public struct AeroLightGrade
    {
        public bool active;
        public Color sunTint;
        public float sunScale;
        public Color ambientTint;
        public float ambientScale;
        public float shadowStrength;
        public float skyExposureScale;
        public Color fogColor;
        public float fogDensityScale;

        public static AeroLightGrade Identity => new()
        {
            active = false, sunTint = Color.white, sunScale = 1, ambientTint = Color.white, ambientScale = 1,
            shadowStrength = 1, skyExposureScale = 1, fogColor = Color.clear, fogDensityScale = 1
        };

        public static AeroLightGrade From(in AeroAtmosphereLook look) => new()
        {
            active = true, sunTint = look.sunTint, sunScale = look.sunScale, ambientTint = look.ambientTint, ambientScale = look.ambientScale,
            shadowStrength = look.shadowStrength, skyExposureScale = look.skyExposureScale, fogColor = look.fogColor, fogDensityScale = look.fogDensityScale
        };
    }
}
