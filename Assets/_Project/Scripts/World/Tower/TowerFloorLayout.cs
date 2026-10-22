using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    /// <summary>A closed annular layout encoded in ordinary instance transforms.</summary>
    public static class TowerFloorLayout
    {
        public const float TemplateSize = 4f;

        public static bool SupportsRadialFloor(GameObject prefab)
        {
            if (prefab == null) return false;
            var filter = prefab.GetComponent<MeshFilter>();
            var renderer = prefab.GetComponent<MeshRenderer>();
            return filter != null && filter.sharedMesh != null && renderer != null &&
                   renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_RadialFloor");
        }

        public readonly struct Sector
        {
            public readonly Vector3 Position;
            public readonly Quaternion Rotation;
            public readonly Vector3 Scale;
            public readonly float Radius;
            public readonly float Angle;
            public readonly float Sweep;
            public readonly float Depth;

            public Sector(Vector2 center, float height, float radius, float angle, float sweep, float depth, float thicknessScale)
            {
                Radius = radius; Angle = angle; Sweep = sweep; Depth = depth;
                Position = new Vector3(center.x + Mathf.Cos(angle) * radius, height, center.y + Mathf.Sin(angle) * radius);
                Rotation = Quaternion.Euler(0, -180f - angle * Mathf.Rad2Deg, 0);
                Scale = new Vector3(depth / TemplateSize, thicknessScale, radius * sweep / TemplateSize);
            }

            public bool Contains(Vector2 point, Vector2 center)
            {
                Vector2 delta = point - center;
                float radius = delta.magnitude;
                if (radius > Radius + .0001f || radius < Radius - Depth - .0001f) return false;
                if (radius < .0001f) return Radius - Depth < .0001f;
                float angle = Mathf.Repeat(Mathf.Atan2(delta.y, delta.x) - Angle, Mathf.PI * 2f);
                return angle <= Sweep + .00001f;
            }
        }

        public static IEnumerable<Sector> Build(Vector2 center, float height, float outerRadius,
            float radialDepth, float targetArcLength, float thicknessScale = 1f)
        {
            if (!float.IsFinite(outerRadius) || !float.IsFinite(radialDepth) || !float.IsFinite(targetArcLength) ||
                radialDepth <= 0f || targetArcLength <= 0f)
                throw new ArgumentOutOfRangeException(nameof(radialDepth), "Floor spacing must be finite and positive.");
            for (float radius = outerRadius; radius > .0001f;)
            {
                float depth = Mathf.Min(radius, radialDepth);
                int count = Mathf.Max(12, Mathf.CeilToInt(Mathf.PI * 2f * radius / targetArcLength));
                float sweep = Mathf.PI * 2f / count;
                for (int i = 0; i < count; i++)
                    yield return new Sector(center, height, radius, i * sweep, sweep, depth, thicknessScale);
                radius = Mathf.Max(0f, radius - depth);
            }
        }

        // Must match BuildingRadialFloor.hlsl. The local X axis points inward;
        // negative local Z advances around the ring. Y and the floor datum remain unchanged.
        public static Vector3 Bend(Vector3 p, float radius, Vector3 scale)
        {
            float remaining = Mathf.Max(0f, radius - p.x * scale.x);
            if (remaining < .0002f) remaining = 0f;
            float angle = -p.z * scale.z / radius;
            return new Vector3((radius - remaining * Mathf.Cos(angle)) / scale.x,
                p.y, -remaining * Mathf.Sin(angle) / scale.z);
        }

        public static void ConfigureMaterial(Material material, bool radial, Vector2 center, bool cpuMesh = false)
        {
            if (!material.HasProperty("_RadialFloor")) return;
            material.SetFloat("_RadialFloor", radial ? (cpuMesh ? 2f : 1f) : 0f);
            material.SetVector("_RadialFloorCenter", new Vector4(center.x, center.y, 0, 0));
        }

        public static Bounds ConservativeBounds(Bounds bounds, float radialDepth, float targetArcLength)
        {
            // Max inward bulge of an arc relative to its tangent rectangle.
            Vector3 max = bounds.max;
            max.x += TemplateSize * targetArcLength * targetArcLength / (8f * radialDepth * radialDepth) + .01f;
            bounds.SetMinMax(bounds.min, max);
            return bounds;
        }
    }

    /// <summary>Shares bent collision/authoring meshes by ring shape, not by instance.</summary>
    public sealed class RadialFloorMeshCache : IDisposable
    {
        private readonly Dictionary<(int, int, int, int), Mesh> meshes = new();
        public int Count => meshes.Count;

        public Mesh Get(Mesh source, float radius, Vector3 scale)
        {
            var key = (source.GetInstanceID(), Mathf.RoundToInt(radius * 10000f),
                Mathf.RoundToInt(scale.x * 100000f), Mathf.RoundToInt(scale.z * 100000f));
            if (meshes.TryGetValue(key, out Mesh cached)) return cached;
            radius = key.Item2 / 10000f;
            scale.x = key.Item3 / 100000f; scale.z = key.Item4 / 100000f;
            var mesh = UnityEngine.Object.Instantiate(source);
            mesh.name = $"{source.name} sector r={radius:F4}";
            mesh.hideFlags = HideFlags.DontSave;
            var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
            mesh.SetUVs(2, new List<Vector3>(vertices)); // Preserve patch coordinates for the material mask.
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 p = vertices[i];
                float angle = -p.z * scale.z / radius;
                float sn = Mathf.Sin(angle), cs = Mathf.Cos(angle);
                float ratio = Mathf.Max((radius - p.x * scale.x) / radius, .001f);
                vertices[i] = TowerFloorLayout.Bend(p, radius, scale);
                if (normals.Length == vertices.Length)
                {
                    Vector3 n = normals[i];
                    normals[i] = new Vector3(cs * n.x - scale.x / scale.z * sn / ratio * n.z,
                        n.y, scale.z / scale.x * sn * n.x + cs / ratio * n.z).normalized;
                }
                if (tangents.Length == vertices.Length)
                {
                    Vector4 t = tangents[i];
                    Vector3 v = new Vector3(cs * t.x - ratio * scale.z / scale.x * sn * t.z,
                        t.y, scale.x / scale.z * sn * t.x + ratio * cs * t.z).normalized;
                    tangents[i] = new Vector4(v.x, v.y, v.z, t.w);
                }
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.tangents = tangents; mesh.RecalculateBounds();
            meshes.Add(key, mesh);
            return mesh;
        }

        public void Dispose()
        {
            foreach (Mesh mesh in meshes.Values)
                if (Application.isPlaying) UnityEngine.Object.Destroy(mesh);
                else UnityEngine.Object.DestroyImmediate(mesh);
            meshes.Clear();
        }
    }
}
