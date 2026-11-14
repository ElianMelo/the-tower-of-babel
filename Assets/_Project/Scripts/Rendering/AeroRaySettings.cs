using UnityEngine;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower/Interior ray settings")]
    public sealed class AeroRaySettings : ScriptableObject
    {
        public enum Quality { BalancedPC, Low }
        public Quality quality;
        [Range(0, 3)] public float intensity = 1.65f;
        [Tooltip("Sun and moon shafts only, on top of intensity; arch fills and interior haze are not affected.")]
        [Range(0, 6)] public float sunStrength = 2;
        [Tooltip("Density of the air for sun and moon shafts, per metre. Higher shows each near beam and shortens the reach.")]
        [Range(.001f, .2f)] public float sunDensity = .06f;
        [Tooltip("Shaft gain while the sun is low (long beams raking through the arches).")]
        [Range(0, 3)] public float lowSunGain = 1.3f;
        [Tooltip("Shaft gain while the sun is high (it only grazes the openings).")]
        [Range(0, 3)] public float highSunGain = .8f;
        [Tooltip("Sine of the elevation where the low-sun gain starts to fall / where the high-sun gain is reached.")]
        [Range(0, 1)] public float lowSunElevation = .4f;
        [Range(0, 1)] public float highSunElevation = .95f;
        [Tooltip("How much the fog of the atmosphere presets (time of day, weather, season) thickens the shafts.")]
        [Range(0, 1.5f)] public float hazeResponse = .7f;
        [Tooltip("Shaft gain left under fully diffuse light (clouds and rain soften the preset shadows).")]
        [Range(0, 1)] public float diffuseLightGain = .45f;
        [Tooltip("Extra gain for moonlight shafts.")]
        [Range(0, 6)] public float moonGain = 3;
        [Range(.001f, .1f)] public float density = .035f;
        [Range(8, 32)] public float distance = 32;
        [Range(.05f, 2)] public float softness = .4f;
        [Range(.01f, 1)] public float noiseScale = .23f;
        public Vector3 noiseVelocity = new(.035f, .016f, -.025f);
        [Range(0, .95f)] public float noiseContrast = .65f;
        [Range(0, 6)] public float fillStrength = 2.2f;
        [Range(4, 28)] public float fillRange = 24;
        [Range(15, 65)] public float fillAngle = 38;
        [Range(0, 25)] public float fillDownAngle = 12;
        [Range(.1f, 4)] public float transitionSeconds = .85f;
        [Range(.05f, .8f)] public float selectionHysteresis = .25f;
        [Range(0, 1)] public float interiorHaze = .12f;
        // The game's graphics settings can drop to the Low tier without editing this asset.
        private bool LowTier => quality == Quality.Low || AeroRenderQuality.SunShafts <= AeroRenderQuality.Low;
        public int Downsample => LowTier ? 4 : 2;
        public int Steps => LowTier ? 12 : 16;
        public int FillCount => LowTier ? 1 : 2;
    }
}
