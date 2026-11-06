using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// "How many clouds" for a randomized clear day (small / medium / large amount).
    /// Replaces the base preset's coverage with a value from <see cref="coverage"/> and scales its cloud size.
    /// </summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Clouds/Amount tier", order = 10)]
    public sealed class AeroCloudAmountTier : ScriptableObject
    {
        [Min(0)] public float weight = 1;
        [Tooltip("Sky coverage range, picked at random.")]
        public Vector2 coverage = new(.3f, .45f);
        [Tooltip("Multiplies the base preset's cloud size.")]
        public Vector2 sizeMultiplier = new(.9f, 1.1f);

        public void Apply(ref AeroCloudLook look, System.Random random)
        {
            look.coverage = AeroCloudRandom.Range(random, coverage);
            look.cloudSize *= AeroCloudRandom.Range(random, sizeMultiplier);
        }
    }
}
