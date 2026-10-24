using UnityEngine;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower/Interior ray settings")]
    public sealed class AeroRaySettings : ScriptableObject
    {
        public enum Quality { BalancedPC, Low }
        public Quality quality;
        [Range(0, 3)] public float intensity = 1.65f;
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
        public int Downsample => quality == Quality.Low ? 4 : 2;
        public int Steps => quality == Quality.Low ? 12 : 16;
        public int FillCount => quality == Quality.Low ? 1 : 2;
    }
}
