using UnityEngine;

namespace TowerOfBabel.Rendering
{
    public enum AeroCloudWeather { ClearDay, Overcast, Rain, Storm }

    /// <summary>
    /// One complete cloud look. Edit the asset in Play Mode and assign it to
    /// <see cref="AeroVolumetricClouds.previewPreset"/> to see changes live.
    /// </summary>
    [CreateAssetMenu(menuName = "Tower of Babel/Clouds/Cloud preset", order = 0)]
    public sealed class AeroCloudPreset : ScriptableObject
    {
        public AeroCloudWeather weather = AeroCloudWeather.ClearDay;
        [TextArea(1, 3)] public string notes;
        public AeroCloudLook look = AeroCloudLook.Default;

        private void OnValidate() => look = look.Sanitized();
    }
}
