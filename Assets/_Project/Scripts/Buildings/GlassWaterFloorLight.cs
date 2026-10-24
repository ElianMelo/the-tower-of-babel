using TowerOfBabel.Rendering;
using UnityEngine;

namespace TowerOfBabel.Buildings
{
    /// <summary>A completed glass channel is a local caustic source; an empty blueprint never emits water light.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Building))]
    public sealed class GlassWaterFloorLight : MonoBehaviour
    {
        public AeroWaterLightRegion region;
        private Building building;
        private void Awake() => building = GetComponent<Building>();
        private void OnEnable() { if (region != null) region.enabled = false; }
        private void LateUpdate()
        {
            if (region == null || building == null) return;
            region.enabled = building.IsBound && building.IsComplete;
        }
        private void OnDisable() { if (region != null) region.enabled = false; }
    }
}
