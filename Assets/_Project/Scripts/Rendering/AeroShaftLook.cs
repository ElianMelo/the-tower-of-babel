using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// How strongly the key light shows as shafts in the tower air, apart from how bright it makes surfaces.
    /// A low sun rakes through the arches as long beams, a high sun only grazes the openings, hazier air
    /// (misty mornings, autumn, winter: the fog of the atmosphere presets) carries more light, and moonlight gets
    /// a lift so night shafts stay faintly readable. Clouds and rain already dim the light itself.
    /// </summary>
    public static class AeroShaftLook
    {
        /// <summary>Gain for a key light at <paramref name="sinElevation"/> (0 horizon .. 1 overhead).</summary>
        public static float ElevationGain(float sinElevation, AeroRaySettings settings)
        {
            float high = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(settings.lowSunElevation, settings.highSunElevation, Mathf.Clamp01(sinElevation)));
            return Mathf.Lerp(settings.lowSunGain, settings.highSunGain, high);
        }

        /// <summary>Gain from haze: <paramref name="fogDensityScale"/> is the preset fog (1 = clear noon).</summary>
        public static float HazeGain(float fogDensityScale, AeroRaySettings settings)
            => Mathf.Clamp(Mathf.LerpUnclamped(1, fogDensityScale, settings.hazeResponse), .5f, 2.5f);

        /// <summary>
        /// Gain from how direct the light is. Clouds and rain scatter it, which the presets express as softer shadows
        /// (<paramref name="shadowStrength"/> 0.75+ clear day, about 0.45 cloudy, 0.3 rain): diffuse light draws weaker shafts.
        /// </summary>
        public static float DirectLightGain(float shadowStrength, AeroRaySettings settings)
            => Mathf.Lerp(settings.diffuseLightGain, 1, Mathf.InverseLerp(DiffuseShadow, DirectShadow, shadowStrength));

        public const float DiffuseShadow = .3f, DirectShadow = .75f;

        /// <summary>The sun-only shaft gain for a key light direction and the current light grade.</summary>
        public static float SunGain(AeroRaySettings settings, Vector3 lightForward, bool moon, in AeroLightGrade grade)
        {
            if (settings == null) return 1;
            float gain = settings.sunStrength * ElevationGain(-lightForward.normalized.y, settings);
            if (moon) gain *= settings.moonGain;
            else if (grade.active) gain *= DirectLightGain(grade.shadowStrength, settings);
            if (grade.active) gain *= HazeGain(grade.fogDensityScale, settings);
            return Mathf.Max(0, gain);
        }

        /// <summary>The gain for the current key light of a tower (the sun, or the moon once it takes over).</summary>
        public static float SunGain(AeroRaySettings settings, AeroDayNightCycle cycle, Light key)
        {
            if (settings == null) return 1;
            if (key == null) return settings.sunStrength;
            bool moon = cycle != null && cycle.moon != null && key == cycle.moon;
            return SunGain(settings, key.transform.forward, moon, cycle != null ? cycle.grade : AeroLightGrade.Identity);
        }
    }
}
