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
            foreach (var region in regions)
            {
                if (region == null || !region.isActiveAndEnabled ||
                    (camera.scene.IsValid() && region.gameObject.scene != camera.scene)) continue;
                if (count == MaximumRegions) break;
                Vector3 p = region.transform.position;
                float angle = region.transform.eulerAngles.y * Mathf.Deg2Rad;
                centers[count] = new Vector4(p.x, p.y, p.z, Mathf.Max(.1f, region.shoreFade));
                shapes[count] = new Vector4(Mathf.Max(.1f, region.radii.x), Mathf.Max(.1f, region.radii.y), Mathf.Cos(angle), Mathf.Sin(angle));
                limits[count] = new Vector4(Mathf.Max(.1f, region.height), Mathf.Max(.1f, region.depth), 0, 0);
                count++;
            }
            return count;
        }
    }
}
