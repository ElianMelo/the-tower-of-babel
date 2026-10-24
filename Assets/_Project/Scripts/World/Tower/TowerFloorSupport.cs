using System;
using System.Collections.Generic;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    /// <summary>Queries the real cached annular tiles, including missing sectors at stair openings.</summary>
    public sealed class TowerFloorSupport
    {
        private sealed class Ring
        {
            public float radius, depth, height, sweep;
            public readonly List<float> starts = new();
        }
        private readonly List<Ring> rings = new();
        private readonly Vector2 center;
        private readonly float meshTop;
        public TowerFloorSupport(IEnumerable<ChunkAssetData> floors, Vector2 center, float meshTop = 0)
        {
            this.center = center; this.meshTop = meshTop;
            foreach (var asset in floors) Add(asset);
        }
        public void Add(ChunkAssetData asset)
        {
            if (asset.AssetType != TowerAssetType.Floor) return;
            float radius = Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), center);
            if (radius < .0001f) return;
            float depth = asset.Scale.x * TowerFloorLayout.TemplateSize, sweep = asset.Scale.z * TowerFloorLayout.TemplateSize / radius;
            float height = asset.Position.y + meshTop * asset.Scale.y;
            Ring ring = rings.Find(r => Mathf.Abs(r.radius - radius) < .001f && Mathf.Abs(r.depth - depth) < .001f && Mathf.Abs(r.sweep - sweep) < .00001f && Mathf.Abs(r.height - height) < .001f);
            if (ring == null) { ring = new Ring { radius = radius, depth = depth, sweep = sweep, height = height }; rings.Add(ring); }
            ring.starts.Add(Mathf.Atan2(asset.Position.z - center.y, asset.Position.x - center.x));
        }
        public bool TryGetHeight(Vector2 point, out float height)
        {
            Vector2 delta = point - center; float radius = delta.magnitude, angle = Mathf.Atan2(delta.y, delta.x);
            foreach (var ring in rings)
            {
                if (radius > ring.radius + .0002f || radius < ring.radius - ring.depth - .0002f) continue;
                foreach (float start in ring.starts)
                {
                    float difference = Mathf.Repeat(angle - start, Mathf.PI * 2);
                    if (difference > ring.sweep + .00001f && difference < Mathf.PI * 2 - .00001f) continue;
                    height = ring.height; return true;
                }
            }
            height = 0; return false;
        }
        public bool SupportsDisk(Vector2 point, float radius, out float height)
        {
            if (!TryGetHeight(point, out height)) return false;
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                if (!TryGetHeight(point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius, out float edge) || Mathf.Abs(edge - height) > .02f) return false;
            }
            return true;
        }

        /// <summary>Finds an inward landing wide enough for the entire support, not just its centre.</summary>
        public bool TryFindInnerConnection(Vector2 pillar, float halfWidth, out float innerRadius)
        {
            Vector2 delta = pillar - center;
            float radius = delta.magnitude;
            Vector2 direction = delta.normalized;
            float stride = Mathf.Max(.05f, halfWidth * .25f);
            for (float r = radius - halfWidth; r > halfWidth; r -= stride)
            {
                if (!SupportsDisk(center + direction * r, halfWidth, out _)) continue;
                innerRadius = Mathf.Max(0, r - .05f); // Positive overlap with the existing deck.
                return true;
            }
            innerRadius = 0;
            return false;
        }

        /// <summary>Recognizes old pads and connected replacements without treating ordinary deck tiles as supports.</summary>
        public static bool IsPillarSupport(ChunkAssetData asset, Vector2 center, float margin, params float[] wallRadii)
        {
            if (asset.AssetType != TowerAssetType.Floor) return false;
            float outerRadius = Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), center);
            float sweep = asset.Scale.z * TowerFloorLayout.TemplateSize / Mathf.Max(.001f, outerRadius);
            foreach (float wall in wallRadii)
                if (wall > margin && Mathf.Abs(outerRadius - wall - margin) < .01f &&
                    Mathf.Abs(sweep - 2 * Mathf.Asin(margin / wall)) < .0001f) return true;
            return false;
        }
    }
}
