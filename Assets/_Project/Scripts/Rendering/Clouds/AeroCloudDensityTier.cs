using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// "How thick the clouds are" for a randomized clear day (soft / medium / dense).
    /// Scales the base preset's density and shifts its erosion and edge softness.
    /// </summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Clouds/Density tier", order = 11)]
    public sealed class AeroCloudDensityTier : ScriptableObject
    {
        [Min(0)] public float weight = 1;
        public Vector2 densityMultiplier = new(.9f, 1.1f);
        [Tooltip("Added to erosion (positive = more wispy).")]
        public Vector2 erosionOffset = new(0, 0);
        [Tooltip("Added to edge softness (positive = hazier borders).")]
        public Vector2 softnessOffset = new(0, 0);
        [Tooltip("Multiplies sky light: soft clouds glow, dense ones keep darker cores.")]
        public Vector2 ambientMultiplier = new(1, 1);

        public void Apply(ref AeroCloudLook look, System.Random random)
        {
            look.density *= AeroCloudRandom.Range(random, densityMultiplier);
            look.erosion += AeroCloudRandom.Range(random, erosionOffset);
            look.edgeSoftness += AeroCloudRandom.Range(random, softnessOffset);
            look.ambientLight *= AeroCloudRandom.Range(random, ambientMultiplier);
        }
    }
}
