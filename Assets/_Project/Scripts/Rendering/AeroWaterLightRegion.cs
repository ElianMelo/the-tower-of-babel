using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Actual water footprint, independent of the large shared ocean renderer.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroWaterLightRegion : MonoBehaviour
    {
        public const int MaximumRegions = 8;
        private static readonly List<AeroWaterLightRegion> regions = new();
        private static readonly float[] selectedDistances = new float[MaximumRegions];
        public Vector2 radii = new(8, 6);
        [Min(.1f)] public float shoreFade = 4;
        [Min(.1f)] public float height = 3;
        [Min(.1f)] public float depth = 5;

        private void OnEnable() { if (!regions.Contains(this)) regions.Add(this); }
        private void OnDisable() => regions.Remove(this);

        public static bool HasNearbyRegion(Camera camera, float viewDistance)
        {
            foreach (var region in regions)
            {
                if (region == null || !region.isActiveAndEnabled ||
                    (camera.scene.IsValid() && region.gameObject.scene != camera.scene)) continue;
                float radius = Mathf.Max(region.radii.x,region.radii.y) + region.shoreFade + Mathf.Max(region.height,region.depth);
                if ((region.transform.position-camera.transform.position).sqrMagnitude < Mathf.Pow(radius+viewDistance,2)) return true;
            }
            return false;
        }

        public static int CopyTo(Camera camera, Vector4[] centers, Vector4[] shapes, Vector4[] limits)
        {
            int count = 0;
            for (int i = 0; i < MaximumRegions; i++) selectedDistances[i] = float.PositiveInfinity;
            foreach (var region in regions)
            {
                if (region == null || !region.isActiveAndEnabled ||
                    (camera.scene.IsValid() && region.gameObject.scene != camera.scene)) continue;
                Vector3 p = region.transform.position;
                float distance = (p - camera.transform.position).sqrMagnitude;
                int slot = 0;
                while (slot < MaximumRegions && distance >= selectedDistances[slot]) slot++;
                if (slot == MaximumRegions) continue;
                for (int i = MaximumRegions - 1; i > slot; i--)
                {
                    selectedDistances[i] = selectedDistances[i - 1]; centers[i] = centers[i - 1]; shapes[i] = shapes[i - 1]; limits[i] = limits[i - 1];
                }
                selectedDistances[slot] = distance;
                float angle = region.transform.eulerAngles.y * Mathf.Deg2Rad;
                centers[slot] = new Vector4(p.x, p.y, p.z, Mathf.Max(.1f, region.shoreFade));
                shapes[slot] = new Vector4(Mathf.Max(.1f, region.radii.x), Mathf.Max(.1f, region.radii.y), Mathf.Cos(angle), Mathf.Sin(angle));
                limits[slot] = new Vector4(Mathf.Max(.1f, region.height), Mathf.Max(.1f, region.depth), 0, 0);
                count = Mathf.Min(MaximumRegions, count + 1);
            }
            return count;
        }
    }
}
