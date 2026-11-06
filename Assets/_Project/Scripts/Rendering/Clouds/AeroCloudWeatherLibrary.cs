using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Which cloud presets the weather uses and how clear days are randomized.
    /// Clear days = a weighted clear-day preset x an amount tier (small/medium/large) x a density tier
    /// (soft/medium/dense), re-rolled every few minutes and at dawn. Rain blends toward a rain preset
    /// as the falling rain fades in.
    /// </summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Clouds/Cloud weather library", order = 20)]
    public sealed class AeroCloudWeatherLibrary : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public AeroCloudPreset preset;
            [Min(0)] public float weight = 1;
        }

        public struct Roll
        {
            public AeroCloudLook look;
            public AeroCloudPreset preset;
            public AeroCloudAmountTier amount;
            public AeroCloudDensityTier density;
            public string Description => preset == null ? "None" :
                amount == null && density == null ? preset.name :
                $"{preset.name} ({(amount != null ? amount.name : "-")}, {(density != null ? density.name : "-")})";
        }

        [Header("Clear days")]
        public List<Entry> clearDay = new();
        [Tooltip("Combine each clear-day preset with a random amount and density tier.")]
        public bool randomizeClearDays = true;
        public List<AeroCloudAmountTier> amountTiers = new();
        public List<AeroCloudDensityTier> densityTiers = new();
        [Tooltip("Game seconds between clear-day re-rolls (random in range).")]
        public Vector2 rerollSeconds = new(240, 480);
        [Tooltip("Also roll a new sky every sunrise.")]
        public bool rerollAtDawn = true;
        [Tooltip("Seconds a new clear-day sky takes to blend in.")]
        [Min(0)] public float transitionSeconds = 90;

        [Header("Rain")]
        [Tooltip("Picked when rain starts; heavy, low, dark clouds.")]
        public List<Entry> rain = new();
        [Tooltip("Falling-rain amount (0..1) -> blend from the clear sky to the rain preset.")]
        public AnimationCurve rainBlend = new(new Keyframe(0, 0, 0, 2.2f), new Keyframe(.45f, .85f), new Keyframe(1, 1, 0, 0));

        [Header("Night")]
        [Tooltip("Moonlight on clouds is multiplied by this so they read against the night sky.")]
        [Range(0, 8)] public float moonLightBoost = 2.2f;
        [Range(0, 4)] public float nightAmbientBoost = 1.6f;

        [Header("World")]
        [Tooltip("Planet radius for the cloud layer's curvature, kilometres. Smaller = clouds dip to the horizon sooner.")]
        [Range(100, 6400)] public float planetRadiusKm = 1200;

        public bool HasClearDay => clearDay.Exists(e => e != null && e.preset != null && e.weight > 0);
        public bool HasRain => rain.Exists(e => e != null && e.preset != null && e.weight > 0);

        /// <summary>A clear-day sky: weighted preset, then (when enabled) an amount and a density tier.</summary>
        public Roll RollClearDay(System.Random random)
        {
            var roll = new Roll { look = AeroCloudLook.Default };
            var entry = AeroCloudRandom.Pick(random, clearDay, e => e.preset != null ? e.weight : 0);
            if (entry == null) return roll;
            roll.preset = entry.preset; roll.look = entry.preset.look;
            if (randomizeClearDays)
            {
                roll.amount = AeroCloudRandom.Pick(random, amountTiers, t => t.weight);
                roll.density = AeroCloudRandom.Pick(random, densityTiers, t => t.weight);
                if (roll.amount != null) roll.amount.Apply(ref roll.look, random);
                if (roll.density != null) roll.density.Apply(ref roll.look, random);
            }
            roll.look = roll.look.Sanitized();
            return roll;
        }

        public Roll RollRain(System.Random random)
        {
            var entry = AeroCloudRandom.Pick(random, rain, e => e.preset != null ? e.weight : 0);
            return entry == null ? new Roll { look = AeroCloudLook.Default } : new Roll { preset = entry.preset, look = entry.preset.look.Sanitized() };
        }

        public float RainBlend(float rainAmount)
            => rainBlend == null || rainBlend.length == 0 ? Mathf.Clamp01(rainAmount) : Mathf.Clamp01(rainBlend.Evaluate(Mathf.Clamp01(rainAmount)));

        public float NextRerollSeconds(System.Random random) => Mathf.Max(10, AeroCloudRandom.Range(random, rerollSeconds));
    }
}
