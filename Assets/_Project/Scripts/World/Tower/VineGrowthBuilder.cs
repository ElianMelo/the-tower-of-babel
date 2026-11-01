using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    /// <summary>
    /// Grows a deterministic climbing vine over a facade and turns it into construction-stage meshes.
    /// Space: X along the facade (towards an arch), Y up from the deck, Z out of the facade. The vine climbs the
    /// pillar, spreads over part of the arch, and lets loose strands hang into the opening.
    /// Every stem point and leaf keeps its distance from the root along the stems, so stage k shows the vine
    /// grown to <see cref="GrowthAt"/>(k) of its full length; stage 0 is a small sprout.
    /// </summary>
    public sealed class VineGrowthBuilder
    {
        /// <summary>Front-facing surface at a facade point: its depth (Z) and normal. False where there is no support.</summary>
        public delegate bool FacadeSurface(Vector2 point, out float depth, out Vector3 normal);

        [Serializable]
        public struct Style
        {
            public int seed;
            [Tooltip("Main stems rising from the root.")] public int stems;
            [Tooltip("Height where stems leave the pillar and spread across the arch.")] public float spreadHeight;
            [Tooltip("How far the stems travel over the arch, in metres.")] public float spread;
            [Tooltip("Share of main stems that stop low on the pillar instead of reaching the arch.")] public float shortStems;
            [Tooltip("0 none, 1 many strands hanging from the arch.")] public float drape;
            [Tooltip("Leaf and branch density multiplier.")] public float density;
            public float leafSize;
            [Tooltip("Chance for a leaf to turn red, like Virginia creeper.")] public float redChance;
            public float maxHeight;
        }

        public const float SproutGrowth = .07f;
        public const float StemOffset = .025f;
        private const float Step = .11f;

        private sealed class Strand
        {
            public readonly List<Vector3> Points = new();
            public readonly List<Vector3> Normals = new();
            public readonly List<float> Distance = new();
            public float BaseRadius;
            public bool Hanging;
            public float End => Distance[^1];
        }

        private struct Leaf
        {
            public Vector3 Position, Normal, Tip;
            public float Size, Distance;
            public Color Color;
        }

        // Bright Frutiger greens from the references: deep ivy at the base, lime and yellow-green new growth.
        private static readonly Color Deep = new(.18f, .52f, .14f), Mid = new(.32f, .7f, .18f), Lime = new(.56f, .88f, .24f),
            Young = new(.72f, .92f, .32f), Red = new(.74f, .24f, .14f), Bark = new(.36f, .29f, .17f), Shoot = new(.4f, .62f, .2f);

        private readonly List<Strand> strands = new();
        private readonly List<Leaf> leaves = new();
        private readonly Style style;
        private readonly FacadeSurface surface;
        private readonly System.Random random;

        public float TotalLength { get; private set; }
        public int StrandCount => strands.Count;
        public int LeafCount => leaves.Count;
        public int HangingStrandCount { get; private set; }

        private VineGrowthBuilder(Style style, FacadeSurface surface)
        {
            this.style = style; this.surface = surface;
            random = new System.Random(style.seed);
        }

        public static float GrowthAt(int stage) => Mathf.Lerp(SproutGrowth, 1f, Mathf.Clamp01(stage / 10f));

        public static VineGrowthBuilder Grow(Style style, FacadeSurface surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            var builder = new VineGrowthBuilder(style, surface);
            builder.GrowAll();
            return builder;
        }

        private void GrowAll()
        {
            int stems = Mathf.Max(1, style.stems);
            var mains = new List<Strand>();
            for (int i = 0; i < stems; i++)
            {
                float x = Mathf.Lerp(-.26f, .26f, stems == 1 ? .5f : i / (float)(stems - 1)) + Range(-.06f, .06f);
                bool shortStem = i > 0 && random.NextDouble() < style.shortStems;
                float spreadSign = random.NextDouble() < .72 ? 1 : -1;
                // Enough length to climb the pillar, work up the spandrel around the arch opening, then spread.
                float length = shortStem ? Range(1.4f, style.spreadHeight * .8f) : style.spreadHeight + 1.6f + style.spread * Range(.75f, 1.15f);
                var main = Walk(new Vector2(x, .02f), new Vector2(Range(-.15f, .15f), 1), 0, length, Range(.016f, .024f),
                    shortStem ? 0 : spreadSign, true);
                if (main != null) { strands.Add(main); mains.Add(main); }
            }
            foreach (var main in mains) Branch(main, 0);
            foreach (var strand in strands.ToArray()) Drape(strand);
            foreach (var strand in strands) AddLeaves(strand);
            TotalLength = 0;
            foreach (var strand in strands) TotalLength = Mathf.Max(TotalLength, strand.End);
            foreach (var leaf in leaves) TotalLength = Mathf.Max(TotalLength, leaf.Distance);
        }

        // Follows the facade: steers towards the bias and bends around missing support, never leaving the surface.
        private Strand Walk(Vector2 start, Vector2 direction, float startDistance, float maxLength, float radius, float spreadSign, bool main)
        {
            if (!surface(start, out float depth, out Vector3 normal)) return null;
            var strand = new Strand { BaseRadius = radius };
            Add(strand, start, depth, normal, startDistance);
            Vector2 point = start, dir = direction.normalized;
            float travelled = 0, wander = Range(0, 10);
            while (travelled < maxLength)
            {
                wander += .7f;
                Vector2 bias = Vector2.up;
                if (spreadSign != 0 && point.y > style.spreadHeight)
                {
                    // Lean sideways more the higher it gets; the turns below steer it around the arch opening.
                    float k = Mathf.Clamp01((point.y - style.spreadHeight) / Mathf.Max(.3f, style.maxHeight - .45f - style.spreadHeight));
                    bias = new Vector2(spreadSign * (.35f + k), 1 - k * .95f);
                }
                else if (!main) bias = (dir + Vector2.up * .35f).normalized;
                Vector2 desired = (dir * 1.6f + bias.normalized * .8f + new Vector2(Mathf.Sin(wander), Mathf.Cos(wander * 1.3f)) * .35f).normalized;
                bool moved = false;
                foreach (float turn in Turns)
                {
                    Vector2 candidate = point + Rotate(desired, turn) * Step;
                    if (candidate.y > style.maxHeight || candidate.y < 0 ||
                        !surface(candidate, out float nextDepth, out Vector3 nextNormal) || Mathf.Abs(nextDepth - depth) > .3f) continue;
                    dir = (candidate - point).normalized; point = candidate; depth = nextDepth; travelled += Step;
                    Add(strand, point, depth, nextNormal, startDistance + travelled);
                    moved = true; break;
                }
                if (!moved) break;
            }
            return strand.Points.Count > 1 ? strand : null;
        }

        private static readonly float[] Turns = { 0, 18, -18, 36, -36, 58, -58, 85, -85, 115, -115 };

        private void Branch(Strand parent, int depth)
        {
            float next = Range(.25f, .45f);
            for (int i = 1; i < parent.Points.Count - 1; i++)
            {
                if (parent.Distance[i] - parent.Distance[0] < next) continue;
                next = parent.Distance[i] - parent.Distance[0] + Range(.3f, .55f) / Mathf.Max(.3f, style.density);
                if (random.NextDouble() > .55f * style.density) continue;
                Vector2 tangent = ((Vector2)(parent.Points[i + 1] - parent.Points[i - 1])).normalized;
                float side = random.NextDouble() < .5 ? 1 : -1;
                Vector2 dir = Rotate(tangent, side * Range(35, 75));
                // High on the arch, some branches trail down the face like a curtain.
                if (parent.Points[i].y > style.spreadHeight && random.NextDouble() < .35f * style.drape) dir = Rotate(Vector2.down, Range(-25, 25));
                float length = Range(.25f, .9f) * (parent.Points[i].y > style.spreadHeight ? 1.3f : 1f) / (depth + 1);
                var branch = Walk(parent.Points[i], dir, parent.Distance[i], length, parent.BaseRadius * .55f, 0, false);
                if (branch == null) continue;
                strands.Add(branch);
                if (depth < 1) Branch(branch, depth + 1);
            }
        }

        // Loose strands fall from the arch where there is nothing behind them.
        private void Drape(Strand parent)
        {
            if (style.drape <= 0) return;
            for (int i = 0; i < parent.Points.Count; i++)
            {
                Vector3 p = parent.Points[i];
                if (p.y < style.spreadHeight || random.NextDouble() > .2f * style.drape) continue;
                // Find the edge of the arch opening within reach below; strands fall from that edge.
                float edge = -1;
                for (float d = .12f; d <= .9f; d += .08f)
                    if (!surface(new Vector2(p.x, p.y - d), out _, out _)) { edge = p.y - d + .08f; break; }
                if (edge < 0 || !surface(new Vector2(p.x, edge), out float edgeDepth, out _)) continue;
                float startDistance = parent.Distance[i] + (p.y - edge);
                p = new Vector3(p.x, edge, edgeDepth + StemOffset);
                var strand = new Strand { BaseRadius = parent.BaseRadius * .4f, Hanging = true };
                float length = Range(.35f, 1.4f) * Mathf.Lerp(.6f, 1, style.drape);
                float sway = Range(-.08f, .08f), dz = Range(.02f, .08f);
                for (float d = 0; d <= length; d += Step)
                {
                    float t = d / length;
                    var point = new Vector3(p.x + sway * t * t + Mathf.Sin(d * 4 + i) * .015f, p.y - d, p.z + dz * t);
                    strand.Points.Add(point); strand.Normals.Add(Vector3.forward); strand.Distance.Add(startDistance + d);
                }
                if (strand.Points.Count > 1) { strands.Add(strand); HangingStrandCount++; }
            }
        }

        private void AddLeaves(Strand strand)
        {
            float spacing = .1f / Mathf.Max(.3f, style.density);
            float next = Range(0, spacing);
            float start = strand.Distance[0], end = strand.End;
            for (int i = 1; i < strand.Points.Count; i++)
            {
                float a = strand.Distance[i - 1], b = strand.Distance[i];
                while (next + start <= b)
                {
                    float s = next + start; next += spacing * Range(.7f, 1.3f);
                    float t = Mathf.InverseLerp(a, b, s);
                    Vector3 p = Vector3.Lerp(strand.Points[i - 1], strand.Points[i], t);
                    Vector3 n = Vector3.Slerp(strand.Normals[i - 1], strand.Normals[i], t).normalized;
                    Vector3 along = (strand.Points[i] - strand.Points[i - 1]).normalized;
                    Vector3 side = Vector3.Cross(n, along).normalized;
                    float fromTip = (end - s), young = Mathf.Clamp01(1 - fromTip / .6f);
                    int count = random.NextDouble() < .3f * style.density ? 2 : 1;
                    for (int k = 0; k < count; k++)
                    {
                        Vector3 offset = side * Range(-.07f, .07f) + n * Range(.02f, .06f);
                        Vector3 tilt = Quaternion.AngleAxis(Range(-35, 35), side) * Quaternion.AngleAxis(Range(-35, 35), along) * n;
                        // Ivy leaves hang: tips lean down and outward from the stem.
                        Vector3 tip = Vector3.ProjectOnPlane(Vector3.down * Range(.4f, 1.2f) + side * Range(-1, 1) + along * Range(-.4f, .4f), tilt).normalized;
                        if (tip.sqrMagnitude < .01f) tip = Vector3.ProjectOnPlane(Vector3.down, tilt).normalized;
                        float size = style.leafSize * Range(.75f, 1.2f) * Mathf.Lerp(1.15f, .55f, young) * (strand.Hanging ? .8f : 1f);
                        Color color = random.NextDouble() < style.redChance ? Color.Lerp(Red, Mid, Range(0, .3f))
                            : Color.Lerp(Color.Lerp(Deep, Mid, Range(0, 1)), Color.Lerp(Lime, Young, Range(0, 1)), Mathf.Clamp01(young * .8f + Range(0, .35f)));
                        leaves.Add(new Leaf { Position = p + offset, Normal = tilt, Tip = tip, Size = size, Distance = s, Color = color });
                    }
                }
            }
        }

        private void Add(Strand strand, Vector2 point, float depth, Vector3 normal, float distance)
        {
            strand.Points.Add(new Vector3(point.x, point.y, depth + StemOffset));
            strand.Normals.Add(normal.sqrMagnitude > .01f ? normal.normalized : Vector3.forward);
            strand.Distance.Add(distance);
        }

        /// <summary>Mesh of the vine grown to <see cref="GrowthAt"/>(stage). Vertex alpha drives leaf flutter.</summary>
        public Mesh BuildStage(int stage, string name = null) => BuildGrowth(GrowthAt(stage), name ?? $"Vine_Stage_{stage:00}");

        public Mesh BuildGrowth(float growth, string name)
        {
            float reach = TotalLength * Mathf.Clamp01(growth);
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var colors = new List<Color>(); var triangles = new List<int>();
            foreach (var strand in strands) AppendStrand(strand, reach, vertices, normals, colors, triangles);
            foreach (var leaf in leaves)
            {
                if (leaf.Distance > reach) continue;
                float scale = Mathf.Lerp(.3f, 1, Mathf.SmoothStep(0, 1, (reach - leaf.Distance) / .35f));
                AppendLeaf(leaf, scale, vertices, normals, colors, triangles);
            }
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AppendStrand(Strand strand, float reach, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
        {
            if (strand.Distance[0] >= reach) return;
            var points = new List<Vector3>(); var distances = new List<float>();
            for (int i = 0; i < strand.Points.Count; i++)
            {
                // Stems are drawn at every other walk point; the growth front is always exact.
                bool last = i == strand.Points.Count - 1 || strand.Distance[i + 1] > reach;
                if (strand.Distance[i] <= reach && i % 2 == 1 && !last) continue;
                if (strand.Distance[i] <= reach) { points.Add(strand.Points[i]); distances.Add(strand.Distance[i]); continue; }
                float f = Mathf.InverseLerp(strand.Distance[i - 1], strand.Distance[i], reach);
                points.Add(Vector3.Lerp(strand.Points[i - 1], strand.Points[i], f)); distances.Add(reach);
                break;
            }
            if (points.Count < 2) return;
            const int sides = 3;
            int first = v.Count;
            float end = distances[^1], start = distances[0];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 along = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 side = Vector3.Cross(Vector3.forward, along); if (side.sqrMagnitude < .001f) side = Vector3.right; side.Normalize();
                Vector3 up = Vector3.Cross(along, side).normalized;
                float remaining = (end - distances[i]) / Mathf.Max(.01f, end - start);
                float radius = Mathf.Lerp(.004f, strand.BaseRadius, Mathf.Sqrt(Mathf.Clamp01(remaining)));
                float flutter = strand.Hanging ? Mathf.Clamp01((distances[i] - start) / .6f) * .6f : 0;
                Color color = Color.Lerp(Shoot, Bark, Mathf.Clamp01(remaining * 1.4f)); color.a = flutter;
                for (int k = 0; k < sides; k++)
                {
                    float angle = k * Mathf.PI * 2 / sides;
                    Vector3 dir = side * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                    v.Add(points[i] + dir * radius); n.Add(dir); c.Add(color);
                }
            }
            for (int i = 0; i < points.Count - 1; i++)
            for (int k = 0; k < sides; k++)
            {
                int a = first + i * sides + k, b = first + i * sides + (k + 1) % sides;
                int a2 = a + sides, b2 = b + sides;
                t.Add(a); t.Add(a2); t.Add(b); t.Add(b); t.Add(a2); t.Add(b2);
            }
        }

        // Three-lobed ivy outline: pointed tip, two side lobes, two small base lobes and a notch at the stalk.
        private static readonly (float angle, float radius)[] Outline =
            { (0, 1f), (50, .5f), (95, .8f), (150, .5f), (210, .5f), (265, .8f), (310, .5f) };

        private static void AppendLeaf(Leaf leaf, float scale, List<Vector3> v, List<Vector3> n, List<Color> c, List<int> t)
        {
            float size = leaf.Size * scale;
            Vector3 side = Vector3.Cross(leaf.Normal, leaf.Tip).normalized;
            // The leaf hangs from its stalk: the outline's base sits at the attachment point.
            Vector3 center = leaf.Position + leaf.Tip * size * .45f;
            int first = v.Count;
            Color inner = leaf.Color * .82f; inner.a = .35f;
            v.Add(center + leaf.Normal * size * .12f); n.Add(leaf.Normal); c.Add(inner);
            foreach (var (angle, radius) in Outline)
            {
                float r = angle * Mathf.Deg2Rad;
                Vector3 dir = leaf.Tip * Mathf.Cos(r) + side * Mathf.Sin(r);
                Color rim = leaf.Color; rim.a = 1;
                v.Add(center + dir * radius * size * .55f); n.Add((leaf.Normal + dir * .25f).normalized); c.Add(rim);
            }
            for (int i = 0; i < Outline.Length; i++)
            {
                t.Add(first); t.Add(first + 1 + i); t.Add(first + 1 + (i + 1) % Outline.Length);
            }
        }

        private float Range(float a, float b) => Mathf.Lerp(a, b, (float)random.NextDouble());
        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
