using System;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Every value that shapes and lights the volumetric cloud layer. Presets hold one; the controller blends
    /// them (clear-day roll, rain, transitions) and hands the result to <see cref="AeroVolumetricCloudsRendererFeature"/>.
    /// </summary>
    [Serializable]
    public struct AeroCloudLook
    {
        [Header("Amount and shape")]
        [Tooltip("Share of the sky covered by clouds, 0 clear .. 1 overcast.")]
        [Range(0, 1)] public float coverage;
        [Tooltip("Optical thickness. Low = soft, see-through; high = dense, dark undersides.")]
        [Range(0, 3)] public float density;
        [Tooltip("Scales every noise pattern: <1 small puffs, >1 large cloud banks.")]
        [Range(.25f, 4)] public float cloudSize;
        [Tooltip("0 flat stratus layer, .5 puffy cumulus, 1 towering cumulonimbus.")]
        [Range(0, 1)] public float cloudType;
        [Tooltip("Detail erosion of the edges: 0 smooth blobs, 1 wispy and torn.")]
        [Range(0, 1)] public float erosion;
        [Tooltip("Width of the fade at each cloud's border: low = crisp outlines, high = hazy edges.")]
        [Range(.02f, 1)] public float edgeSoftness;
        [Tooltip("Cloud base above sea level, metres.")]
        [Min(50)] public float baseAltitude;
        [Tooltip("Layer depth from base to the highest tops, metres.")]
        [Min(50)] public float thickness;
        [Tooltip("Each cloud floats up to this many metres above the base, so clouds sit at different heights. 0 = one shared base.")]
        [Range(0, 1000)] public float heightVariation;

        [Header("Motion")]
        [Range(0, 360)] public float windDirection;
        [Tooltip("Drift speed, metres per second.")]
        [Min(0)] public float windSpeed;
        [Tooltip("How fast the cloud shapes boil and change, metres per second of noise travel.")]
        [Min(0)] public float evolution;
        [Tooltip("Tops lean downwind by this many metres (shear).")]
        public float shear;

        [Header("Lighting")]
        [Tooltip("Multiplies direct sun or moon light on the clouds.")]
        [Range(0, 4)] public float sunLight;
        [Tooltip("Multiplies sky light on the clouds; fills the shadowed sides.")]
        [Range(0, 4)] public float ambientLight;
        [Tooltip("Colour of the lit sides (multiplies the sun colour).")]
        public Color litTint;
        [Tooltip("Colour of the shadowed sides and bases (multiplies the sky light).")]
        public Color shadowTint;
        [Tooltip("Storm darkness: thicker light absorption and darker bases.")]
        [Range(0, 1)] public float darkness;
        [Tooltip("Darkens cloud edges facing away from the sun (powder effect).")]
        [Range(0, 1)] public float powder;
        [Tooltip("Forward scattering: bright silver linings when looking toward the sun.")]
        [Range(0, .95f)] public float silverLining;
        [Tooltip("Approximate light bouncing inside the cloud; brightens thick cores.")]
        [Range(0, .95f)] public float multiScatter;

        [Header("Distance")]
        [Tooltip("Distance over which clouds fade into the horizon haze, metres.")]
        [Min(100)] public float hazeDistance;
        [Range(0, 1)] public float hazeStrength;
        [Tooltip("Clouds are fully faded by this distance, metres.")]
        [Min(1000)] public float fadeDistance;
        [Tooltip("How much this sky dims the sunlight on the ground.")]
        [Range(0, 1)] public float sunOcclusion;

        /// <summary>Calm, fair-weather defaults.</summary>
        public static AeroCloudLook Default => new()
        {
            coverage = .4f, density = .8f, cloudSize = 1, cloudType = .5f, erosion = .6f, edgeSoftness = .4f,
            baseAltitude = 150, thickness = 450, heightVariation = 200,
            windDirection = 60, windSpeed = 8, evolution = 1.5f, shear = 150,
            sunLight = 1, ambientLight = 1, litTint = Color.white, shadowTint = new Color(.82f, .88f, 1),
            darkness = 0, powder = .5f, silverLining = .65f, multiScatter = .5f,
            hazeDistance = 14000, hazeStrength = .55f, fadeDistance = 32000, sunOcclusion = .08f
        };

        /// <summary>Blends every value; wind direction takes the short way round.</summary>
        public static AeroCloudLook Lerp(in AeroCloudLook a, in AeroCloudLook b, float t)
        {
            t = Mathf.Clamp01(t);
            return new AeroCloudLook
            {
                coverage = Mathf.Lerp(a.coverage, b.coverage, t),
                density = Mathf.Lerp(a.density, b.density, t),
                cloudSize = Mathf.Lerp(a.cloudSize, b.cloudSize, t),
                cloudType = Mathf.Lerp(a.cloudType, b.cloudType, t),
                erosion = Mathf.Lerp(a.erosion, b.erosion, t),
                edgeSoftness = Mathf.Lerp(a.edgeSoftness, b.edgeSoftness, t),
                baseAltitude = Mathf.Lerp(a.baseAltitude, b.baseAltitude, t),
                thickness = Mathf.Lerp(a.thickness, b.thickness, t),
                heightVariation = Mathf.Lerp(a.heightVariation, b.heightVariation, t),
                windDirection = Mathf.Repeat(Mathf.LerpAngle(a.windDirection, b.windDirection, t), 360),
                windSpeed = Mathf.Lerp(a.windSpeed, b.windSpeed, t),
                evolution = Mathf.Lerp(a.evolution, b.evolution, t),
                shear = Mathf.Lerp(a.shear, b.shear, t),
                sunLight = Mathf.Lerp(a.sunLight, b.sunLight, t),
                ambientLight = Mathf.Lerp(a.ambientLight, b.ambientLight, t),
                litTint = Color.Lerp(a.litTint, b.litTint, t),
                shadowTint = Color.Lerp(a.shadowTint, b.shadowTint, t),
                darkness = Mathf.Lerp(a.darkness, b.darkness, t),
                powder = Mathf.Lerp(a.powder, b.powder, t),
                silverLining = Mathf.Lerp(a.silverLining, b.silverLining, t),
                multiScatter = Mathf.Lerp(a.multiScatter, b.multiScatter, t),
                hazeDistance = Mathf.Lerp(a.hazeDistance, b.hazeDistance, t),
                hazeStrength = Mathf.Lerp(a.hazeStrength, b.hazeStrength, t),
                fadeDistance = Mathf.Lerp(a.fadeDistance, b.fadeDistance, t),
                sunOcclusion = Mathf.Lerp(a.sunOcclusion, b.sunOcclusion, t),
            };
        }

        /// <summary>Keeps hand-edited values inside what the shader can render.</summary>
        public AeroCloudLook Sanitized()
        {
            var l = this;
            l.coverage = Mathf.Clamp01(l.coverage); l.density = Mathf.Clamp(l.density, 0, 3);
            l.cloudSize = Mathf.Clamp(l.cloudSize, .25f, 4); l.cloudType = Mathf.Clamp01(l.cloudType);
            l.erosion = Mathf.Clamp01(l.erosion); l.edgeSoftness = Mathf.Clamp(l.edgeSoftness, .02f, 1);
            l.baseAltitude = Mathf.Max(50, l.baseAltitude); l.thickness = Mathf.Max(50, l.thickness);
            l.heightVariation = Mathf.Clamp(l.heightVariation, 0, 1000);
            l.windDirection = Mathf.Repeat(l.windDirection, 360); l.windSpeed = Mathf.Max(0, l.windSpeed);
            l.evolution = Mathf.Max(0, l.evolution);
            l.sunLight = Mathf.Clamp(l.sunLight, 0, 4); l.ambientLight = Mathf.Clamp(l.ambientLight, 0, 4);
            l.darkness = Mathf.Clamp01(l.darkness); l.powder = Mathf.Clamp01(l.powder);
            l.silverLining = Mathf.Clamp(l.silverLining, 0, .95f); l.multiScatter = Mathf.Clamp(l.multiScatter, 0, .95f);
            l.hazeDistance = Mathf.Max(100, l.hazeDistance); l.hazeStrength = Mathf.Clamp01(l.hazeStrength);
            l.fadeDistance = Mathf.Max(1000, l.fadeDistance); l.sunOcclusion = Mathf.Clamp01(l.sunOcclusion);
            return l;
        }

        public Vector2 WindVector
        {
            get
            {
                float r = windDirection * Mathf.Deg2Rad;
                return new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * windSpeed;
            }
        }
    }
}
