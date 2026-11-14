using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Cost/quality of the cloud ray march. Referenced by <see cref="AeroVolumetricCloudsRendererFeature"/>.</summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Clouds/Cloud quality", order = 30)]
    public sealed class AeroCloudQuality : ScriptableObject
    {
        [Tooltip("Clouds are traced at 1/N of the screen in each direction, then upscaled.")]
        [Range(1, 4)] public int downsample = 2;
        [Tooltip("Ray-march samples through the cloud layer.")]
        [Range(16, 128)] public int primarySteps = 56;
        [Tooltip("Samples toward the sun for self-shadowing.")]
        [Range(2, 10)] public int lightSteps = 6;
        [Tooltip("Longest ray step, metres. Lower = finer detail near the horizon, slower.")]
        [Range(50, 1500)] public float maxStep = 420;
        [Tooltip("Blend in previous frames to remove ray-march noise.")]
        public bool temporal = true;
        [Tooltip("History weight; higher = smoother but slower to react.")]
        [Range(0, .97f)] public float temporalBlend = .9f;

        // Tiers below the authored quality (AeroRenderQuality.Clouds): a coarser trace with fewer samples.
        private static int Tier => Mathf.Clamp(AeroRenderQuality.Clouds, 0, AeroRenderQuality.Full);
        public int Downsample => Mathf.Clamp(Tier == AeroRenderQuality.Full ? downsample : Mathf.Max(downsample, Tier == 1 ? 3 : 4), 1, 4);
        public int PrimarySteps => Tier == AeroRenderQuality.Full ? primarySteps : Mathf.Max(16, Mathf.RoundToInt(primarySteps * (Tier == 1 ? .7f : .45f)));
        public int LightSteps => Tier == AeroRenderQuality.Full ? lightSteps : Mathf.Max(2, lightSteps - (Tier == 1 ? 2 : 3));
    }
}
