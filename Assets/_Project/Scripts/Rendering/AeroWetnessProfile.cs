using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Tuning for the global dew/rain film read by Aero shaders (Aero/AeroWetness.hlsl).</summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Rendering/Aero Wetness")]
    public sealed class AeroWetnessProfile : ScriptableObject
    {
        [Header("Film levels")]
        [Range(0, 1)] public float dewDay = .10f;
        [Range(0, 1)] public float dewDawn = .25f;
        [Range(0, 1)] public float rainWetness = 1f;
        [Tooltip("Dew by time of day (0 midnight, .25 dawn, .5 noon). Multiplied into the dew range.")]
        public AnimationCurve dewByTimeOfDay = new(
            new Keyframe(0, .8f), new Keyframe(.25f, 1), new Keyframe(.4f, .15f),
            new Keyframe(.6f, 0), new Keyframe(.85f, .55f), new Keyframe(1, .8f));

        [Header("Timing (seconds)")]
        [Min(.1f)] public float rampIn = 6f;
        [Min(.1f)] public float dryOut = 10f;
        [Min(.1f)] public float rainFade = 3f;

        [Header("Drops (top faces)")]
        [Min(.05f)] public float dropTileSize = .45f;
        [Min(0)] public float dropSpeed = .9f;
        [Range(0, 1)] public float dewDropWeight = .55f;
        [Range(0, 2)] public float normalStrength = .9f;

        [Header("Streaks (walls)")]
        public Vector2 streakSize = new(.9f, 1.6f);
        [Min(0)] public float streakSpeed = .55f;
        [Range(0, 2)] public float streakDistortion = .6f;

        [Header("Shelter (stepped tower)")]
        public bool shelterEnabled = true;

        [Header("Textures (DYNAMIC Rain/Wetness, linear, repeat)")]
        public Texture2D drops;
        public Texture2D streaks;
        public Texture2D ripples;

        public bool TexturesReady => drops != null && streaks != null && ripples != null;

        public float DewAt(float timeOfDay)
        {
            float shape = Mathf.Clamp01(dewByTimeOfDay.Evaluate(Mathf.Repeat(timeOfDay, 1)));
            return Mathf.Lerp(dewDay, dewDawn, shape);
        }
    }
}
