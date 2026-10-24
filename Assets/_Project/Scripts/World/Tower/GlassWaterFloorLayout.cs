using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    /// <summary>The user's two rings, cardinal spokes and central pool, quantized to real floor tiles.</summary>
    public static class GlassWaterFloorLayout
    {
        public static bool IsChannel(Vector2 point, float wallRadius, float tileDepth)
        {
            float radius = point.magnitude;
            // Select every tile touched by the design. The material draws the exact
            // circular boundaries and straight spokes inside these standard footprints.
            if (radius < wallRadius * .16f + tileDepth * .72f) return true;
            float halfWidth = tileDepth * .9f;
            if (Mathf.Abs(radius - wallRadius * .5f) < halfWidth || Mathf.Abs(radius - wallRadius * .88f) < halfWidth) return true;
            return radius < wallRadius * .96f && Mathf.Min(Mathf.Abs(point.x), Mathf.Abs(point.y)) < halfWidth;
        }

        public static Vector2 TileCenter(Vector3 position, Quaternion rotation, Vector3 scale, Vector2 center)
        {
            float radius = Vector2.Distance(new Vector2(position.x, position.z), center);
            var p = TowerFloorLayout.Bend(new Vector3(2, 0, -2), radius, scale);
            Vector3 world = position + rotation * Vector3.Scale(p, scale);
            return new Vector2(world.x, world.z) - center;
        }
    }
}
