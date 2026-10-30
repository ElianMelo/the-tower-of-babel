using UnityEngine;

namespace TowerOfBabel.Rendering
{
    [CreateAssetMenu(menuName = "Tower of Babel/Day and night profile")]
    public sealed class AeroDayNightProfile : ScriptableObject
    {
        [Min(60)] public float cycleSeconds = 1200;
        [Range(0, 1)] public float startTime = .29f;
        public bool advanceTime = true;
        [Range(0, 360)] public float sunAzimuth = 330;
        [Min(0)] public float sunIntensity = 2.4f;
        [Min(0)] public float moonIntensity = .32f;
        public Color daySun = new(1, .97f, .88f);
        public Color sunsetSun = new(1, .52f, .24f);
        public Color moonColor = new(.4f, .63f, 1);
        public Color daySky = new(.55f, .67f, .82f);
        public Color nightSky = new(.19f, .29f, .45f);
        [Range(.1f, 2)] public float dayExposure = 1;
        [Range(.01f, 1)] public float nightExposure = .22f;
        [Range(0, 2)] public float beamIntensity = 1;
        [Range(3, 20)] public float beamDownAngle = 14;
        [Range(10, 30)] public float beamLength = 22;
        [Range(0, .8f)] public float exitVolumeStrength = .5f;
        [Range(0, 1)] public float fireflyDensity = 1f;
        [Min(0)] public float fireflyPopulationMultiplier = 2.2f;
        [Min(.01f)] public float fireflyCoverageAreaMultiplier = 1.15f;
        [Range(0, 1)] public float sunFlareIntensity = .85f;

        public static float DaylightAt(float time)
            => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-.06f, .22f, Mathf.Sin((Mathf.Repeat(time, 1) - .25f) * Mathf.PI * 2)));
    }
}
