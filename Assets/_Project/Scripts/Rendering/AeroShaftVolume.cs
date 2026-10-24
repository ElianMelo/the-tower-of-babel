using System;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    [Serializable, VolumeComponentMenu("Tower of Babel/Interior sunlight")]
    public sealed class AeroShaftVolume : VolumeComponent, IPostProcessComponent
    {
        public ClampedFloatParameter intensity = new(1, 0, 3);
        public ClampedFloatParameter density = new(1, .2f, 3);
        public bool IsActive() => active && intensity.value > 0;
        public bool IsTileCompatible() => false;
    }
}
