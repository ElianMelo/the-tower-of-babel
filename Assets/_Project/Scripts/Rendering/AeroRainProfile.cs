using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Falling rain tuning (URP particle port of DYNAMIC Falling Rain). See AeroRainController.</summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Rendering/Aero Rain")]
    public sealed class AeroRainProfile : ScriptableObject
    {
        [Header("Materials (DYNAMIC Falling Rain textures)")]
        public Material dropMaterial;
        public Material splashMaterial;
        public AudioClip rainLoop;

        [Header("Drops")]
        [Min(0)] public float dropsPerSecond = 16000;
        [Min(1)] public int maxDrops = 24000;
        [Tooltip("Half-width of the square rain area around the camera, metres.")]
        [Min(1)] public float areaRadius = 14;
        [Tooltip("Spawn height above the camera, metres.")]
        [Min(1)] public float spawnHeight = 14;
        [Tooltip("How far below the camera drops may fall before dying, metres.")]
        [Min(0)] public float fallBelow = 12;
        [Min(.1f)] public float dropSpeed = 20;
        public Vector2 dropWidth = new(.08f, .13f);
        public Vector2 dropLength = new(.8f, 1.3f);
        [Tooltip("Horizontal wind drift, metres per second (x, z).")]
        public Vector2 wind = new(1.2f, .5f);
        [Tooltip("Push the rain area ahead of the camera so the view is filled.")]
        [Min(0)] public float lookAhead = 6;
        public LayerMask collidesWith = ~0;
        [Min(100)] public int maxSplashes = 5000;

        [Header("Splashes")]
        [Range(0, 1)] public float splashChance = .3f;
        public Vector2 splashSize = new(.1f, .26f);
        [Min(.05f)] public float splashLifetime = .22f;

        [Header("Colour (multiplies the sprite)")]
        public Color dayTint = new(.66f, .74f, .84f, .95f);
        public Color nightTint = new(.4f, .5f, .66f, .8f);
        public Color splashDayTint = new(.92f, .97f, 1f, .65f);
        public Color splashNightTint = new(.5f, .64f, .84f, .5f);

        [Header("Sky while raining")]
        [Range(0, 1)] public float overcast = 1;
        [Range(0, 1)] public float sunScale = .45f;
        [Range(0, 1)] public float skyScale = .78f;
        [Range(1, 5)] public float fogScale = 2.2f;

        [Header("Audio")]
        [Range(0, 1)] public float outsideVolume = .8f;
        [Range(0, 1)] public float shelteredVolume = .3f;
        [Min(.1f)] public float shelterCheckDistance = 40;

        public bool Ready => dropMaterial != null;
        public float DropLifetime => (spawnHeight + fallBelow) / Mathf.Max(.1f, dropSpeed);
    }
}
