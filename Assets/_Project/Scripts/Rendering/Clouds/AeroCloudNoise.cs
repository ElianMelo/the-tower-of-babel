using System;
using System.Threading.Tasks;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Seamlessly tiling noise for the cloud layer (the Perlin-Worley / Worley recipe from Schneider's
    /// "Nubis" talks, as used by HDRP and the referenced repositories; this is an independent implementation).
    /// Shape: billowy Perlin-Worley 3D. Detail: Worley fBm 3D for edge erosion. Coverage: 2D weather map,
    /// histogram-equalized so a coverage of 0.4 covers ~40% of the sky. Cirrus: 2D map of hair strands traced along a flow field.
    /// </summary>
    public static class AeroCloudNoise
    {
        public const int DefaultShapeSize = 128, DefaultDetailSize = 32, DefaultCoverageSize = 256, DefaultCirrusSize = 1024;

        public static byte[] Shape(int size, int seed)
        {
            var values = new float[size * size * size];
            Parallel.For(0, size, z =>
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector3((x + .5f) / size, (y + .5f) / size, (z + .5f) / size);
                    float perlin = .5f + .5f * (Perlin(p, 4, seed) * .5f + Perlin(p, 8, seed + 1) * .25f + Perlin(p, 16, seed + 2) * .125f) / .875f * 1.6f;
                    perlin = Mathf.Clamp01(perlin);
                    float billow = Worley(p, 4, seed + 3) * .625f + Worley(p, 8, seed + 4) * .25f + Worley(p, 16, seed + 5) * .125f;
                    float perlinWorley = Remap(perlin, billow - 1, 1, 0, 1);
                    float erode = Worley(p, 8, seed + 6) * .625f + Worley(p, 16, seed + 7) * .25f + Worley(p, 32, seed + 8) * .125f;
                    values[(z * size + y) * size + x] = Mathf.Clamp01(Remap(perlinWorley, (1 - erode) * .45f, 1, 0, 1));
                }
            });
            return Normalize(values, .01f, .995f);
        }

        public static byte[] Detail(int size, int seed)
        {
            var values = new float[size * size * size];
            Parallel.For(0, size, z =>
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector3((x + .5f) / size, (y + .5f) / size, (z + .5f) / size);
                    values[(z * size + y) * size + x] = Worley(p, 2, seed) * .625f + Worley(p, 4, seed + 1) * .25f + Worley(p, 8, seed + 2) * .125f;
                }
            });
            return Normalize(values, .005f, .995f);
        }

        /// <summary>RG8: R = where clouds form (uniform distribution), G = slow variation of cloud height.</summary>
        public static byte[] Coverage(int size, int seed)
        {
            var main = new float[size * size];
            var height = new float[size * size];
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector3((x + .5f) / size, (y + .5f) / size, .37f);
                    float fbm = Perlin2(p, 3, seed) * .5f + Perlin2(p, 6, seed + 1) * .3f + Perlin2(p, 12, seed + 2) * .15f + Perlin2(p, 24, seed + 3) * .05f;
                    float cells = Worley2(p, 5, seed + 4) * .7f + Worley2(p, 10, seed + 5) * .3f;
                    main[y * size + x] = fbm * .55f + cells * .45f;
                    height[y * size + x] = Perlin2(p, 2, seed + 6) * .7f + Perlin2(p, 4, seed + 7) * .3f;
                }
            });
            byte[] r = Equalize(main), g = Normalize(height, 0, 1);
            var rg = new byte[size * size * 2];
            for (int i = 0; i < main.Length; i++) { rg[i * 2] = r[i]; rg[i * 2 + 1] = g[i]; }
            return rg;
        }

        /// <summary>Wisp patches per tile side in <see cref="Cirrus"/> (a jittered grid, some cells left clear).</summary>
        public const int CirrusCells = 4;

        /// <summary>
        /// R8 cirrus hair map, tiling, u along the wind. Strands are traced along a gently curving, wind-combed flow
        /// field: each wisp is a few brush strokes of 10-25 thin hairs that start together, run side by side and fade
        /// out one by one into a feathered tail (some strokes hook at the head, like mares' tails), plus a few faint
        /// loose threads. Clear sky lies between the wisps. Review 2026-11-15: smooth noise contours made strands that
        /// read as smeared paint, not as the fine hair of real cirrus.
        /// </summary>
        public static byte[] Cirrus(int size, int seed)
        {
            var random = new System.Random(seed);
            float U(float a, float b) => a + (float)random.NextDouble() * (b - a);
            float Normal() => Mathf.Sqrt(-2 * Mathf.Log(1 - (float)random.NextDouble())) * Mathf.Cos(2 * Mathf.PI * (float)random.NextDouble());
            // Lengths below are texels of a 512 map (one tile), scaled to the map size.
            float s = size / 512f;
            // Flow direction, radians from +u: mostly along the wind, curving gently.
            var angle = new float[size * size];
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector3((x + .5f) / size, (y + .5f) / size, 0);
                    angle[y * size + x] = Perlin2(p, 2, seed) * .8f + Perlin2(p, 4, seed + 1) * .3f;
                }
            });
            var hair = new float[size * size];
            float cell = size / (float)CirrusCells;
            for (int cy = 0; cy < CirrusCells; cy++)
            for (int cx = 0; cx < CirrusCells; cx++)
            {
                if (random.NextDouble() < .2) continue; // some of the sky stays clear
                float px = (cx + U(.1f, .9f)) * cell, py = (cy + U(.1f, .9f)) * cell;
                int strokes = random.Next(1, 4);
                for (int k = 0; k < strokes; k++)
                {
                    float a0 = Bilinear(angle, size, px, py);
                    float bx = px + Normal() * 28 * s, by = py + Normal() * 14 * s;
                    float length = U(90, 230) * s, spread = U(2.5f, 8) * s;
                    float hook = random.NextDouble() < .35 ? U(.5f, .9f) * (random.Next(2) * 2 - 1) : 0;
                    float twist = U(-.1f, .1f);
                    int hairs = random.Next(10, 26);
                    float amplitude = 1 / Mathf.Sqrt(hairs);
                    for (int i = 0; i < hairs; i++)
                    {
                        float across = Normal() * spread, along = U(-.3f, .3f) * length;
                        float x = bx - Mathf.Sin(a0) * across + Mathf.Cos(a0) * along, y = by + Mathf.Cos(a0) * across + Mathf.Sin(a0) * along;
                        TraceHair(hair, angle, size, x, y, length * U(.45f, 1), U(.3f, .7f) * s, U(.3f, 1) * amplitude, hook, twist + Normal() * .025f);
                    }
                }
                // Faint loose threads that link the wisps (cirrus fibratus).
                for (int i = random.Next(2, 5); i > 0; i--)
                    TraceHair(hair, angle, size, px + Normal() * 40 * s, py + Normal() * 25 * s, U(150, 280) * s, U(.4f, .7f) * s, U(.06f, .14f), U(-.4f, .4f), U(-.06f, .06f));
            }
            // A soft glow of finer ice around each wisp, so strokes melt into the sky instead of ending at a crisp edge.
            var glow = Blur(hair, size, 5 * s);
            var bytes = new byte[size * size];
            for (int i = 0; i < hair.Length; i++) bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01(1 - Mathf.Exp(-(hair[i] + glow[i] * .35f) * 1.2f)) * 255);
            return bytes;
        }

        /// <summary>Lays one hair along the flow field: soft start, slightly thicker head, long fading tail.</summary>
        private static void TraceHair(float[] hair, float[] angle, int size, float x, float y, float length, float width, float amplitude, float hook, float twist)
        {
            const float Step = .7f;
            int steps = Mathf.Max(2, (int)(length / Step));
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float a = Bilinear(angle, size, x, y) + twist + hook * Mathf.Pow(1 - t, 8);
                x += Mathf.Cos(a) * Step; y += Mathf.Sin(a) * Step;
                float intensity = amplitude * Mathf.Min(1, t / .2f) * Mathf.Pow(1 - t, 1.5f);
                float w = Mathf.Max(.5f, width * (1 + .6f * (1 - t)));
                // Normalized by width, so a hair's total brightness does not depend on the map resolution.
                Splat(hair, size, x, y, w, intensity * Step / w);
            }
        }

        private static void Splat(float[] target, int size, float x, float y, float width, float amount)
        {
            int r = Mathf.Min(12, Mathf.CeilToInt(width * 2.5f)), ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
            float k = -1 / (2 * width * width);
            for (int dy = -r; dy <= r; dy++)
            {
                float fy = iy + dy - y; int row = Wrap(iy + dy, size) * size;
                for (int dx = -r; dx <= r; dx++)
                {
                    float fx = ix + dx - x;
                    target[row + Wrap(ix + dx, size)] += amount * Mathf.Exp((fx * fx + fy * fy) * k);
                }
            }
        }

        /// <summary>Wrapping separable Gaussian blur.</summary>
        private static float[] Blur(float[] source, int size, float sigma)
        {
            int r = Mathf.CeilToInt(sigma * 3);
            var kernel = new float[r * 2 + 1];
            float sum = 0;
            for (int i = -r; i <= r; i++) sum += kernel[i + r] = Mathf.Exp(-i * i / (2 * sigma * sigma));
            for (int i = 0; i < kernel.Length; i++) kernel[i] /= sum;
            var rows = new float[source.Length];
            var result = new float[source.Length];
            Parallel.For(0, size, y =>
            {
                for (int x = 0; x < size; x++)
                {
                    float v = 0;
                    for (int i = -r; i <= r; i++) v += source[y * size + Wrap(x + i, size)] * kernel[i + r];
                    rows[y * size + x] = v;
                }
            });
            Parallel.For(0, size, x =>
            {
                for (int y = 0; y < size; y++)
                {
                    float v = 0;
                    for (int i = -r; i <= r; i++) v += rows[Wrap(y + i, size) * size + x] * kernel[i + r];
                    result[y * size + x] = v;
                }
            });
            return result;
        }

        private static float Bilinear(float[] field, int size, float x, float y)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int ax = Wrap(x0, size), bx = Wrap(x0 + 1, size), ay = Wrap(y0, size) * size, by = Wrap(y0 + 1, size) * size;
            return Mathf.Lerp(Mathf.Lerp(field[ay + ax], field[ay + bx], fx), Mathf.Lerp(field[by + ax], field[by + bx], fx), fy);
        }

        public static float Remap(float v, float a, float b, float c, float d) => c + (v - a) / Mathf.Max(1e-5f, b - a) * (d - c);

        /// <summary>Tiling gradient noise in -1..1 with an integer <paramref name="period"/> per unit.</summary>
        public static float Perlin(Vector3 p, int period, int seed)
        {
            p *= period;
            int x0 = Mathf.FloorToInt(p.x), y0 = Mathf.FloorToInt(p.y), z0 = Mathf.FloorToInt(p.z);
            float fx = p.x - x0, fy = p.y - y0, fz = p.z - z0;
            float u = Fade(fx), v = Fade(fy), w = Fade(fz);
            float n000 = Grad(x0, y0, z0, period, seed, fx, fy, fz), n100 = Grad(x0 + 1, y0, z0, period, seed, fx - 1, fy, fz);
            float n010 = Grad(x0, y0 + 1, z0, period, seed, fx, fy - 1, fz), n110 = Grad(x0 + 1, y0 + 1, z0, period, seed, fx - 1, fy - 1, fz);
            float n001 = Grad(x0, y0, z0 + 1, period, seed, fx, fy, fz - 1), n101 = Grad(x0 + 1, y0, z0 + 1, period, seed, fx - 1, fy, fz - 1);
            float n011 = Grad(x0, y0 + 1, z0 + 1, period, seed, fx, fy - 1, fz - 1), n111 = Grad(x0 + 1, y0 + 1, z0 + 1, period, seed, fx - 1, fy - 1, fz - 1);
            float x00 = Mathf.Lerp(n000, n100, u), x10 = Mathf.Lerp(n010, n110, u), x01 = Mathf.Lerp(n001, n101, u), x11 = Mathf.Lerp(n011, n111, u);
            return Mathf.Lerp(Mathf.Lerp(x00, x10, v), Mathf.Lerp(x01, x11, v), w);
        }

        /// <summary>Tiling cellular noise, 1 at feature points falling to 0 between them.</summary>
        public static float Worley(Vector3 p, int cells, int seed)
        {
            p *= cells;
            int cx = Mathf.FloorToInt(p.x), cy = Mathf.FloorToInt(p.y), cz = Mathf.FloorToInt(p.z);
            float best = 9;
            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy, z = cz + dz;
                uint h = Hash(Wrap(x, cells), Wrap(y, cells), Wrap(z, cells), seed);
                float px = x + (h & 1023) / 1023f, py = y + ((h >> 10) & 1023) / 1023f, pz = z + ((h >> 20) & 1023) / 1023f;
                float d = (px - p.x) * (px - p.x) + (py - p.y) * (py - p.y) + (pz - p.z) * (pz - p.z);
                if (d < best) best = d;
            }
            return 1 - Mathf.Clamp01(Mathf.Sqrt(best));
        }

        private static float Perlin2(Vector3 p, int period, int seed) => Perlin(new Vector3(p.x, p.y, 0), period, seed);

        private static float Worley2(Vector3 p, int cells, int seed)
        {
            float px0 = p.x * cells, py0 = p.y * cells;
            int cx = Mathf.FloorToInt(px0), cy = Mathf.FloorToInt(py0);
            float best = 9;
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx, y = cy + dy;
                uint h = Hash(Wrap(x, cells), Wrap(y, cells), 0, seed);
                float px = x + (h & 1023) / 1023f, py = y + ((h >> 10) & 1023) / 1023f;
                float d = (px - px0) * (px - px0) + (py - py0) * (py - py0);
                if (d < best) best = d;
            }
            return 1 - Mathf.Clamp01(Mathf.Sqrt(best));
        }

        private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static int Wrap(int v, int n) => ((v % n) + n) % n;

        private static float Grad(int x, int y, int z, int period, int seed, float fx, float fy, float fz)
        {
            switch (Hash(Wrap(x, period), Wrap(y, period), Wrap(z, period), seed) % 12)
            {
                case 0: return fx + fy; case 1: return -fx + fy; case 2: return fx - fy; case 3: return -fx - fy;
                case 4: return fx + fz; case 5: return -fx + fz; case 6: return fx - fz; case 7: return -fx - fz;
                case 8: return fy + fz; case 9: return -fy + fz; case 10: return fy - fz; default: return -fy - fz;
            }
        }

        private static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed * 0x9E3779B1u ^ (uint)x * 0x85EBCA77u ^ (uint)y * 0xC2B2AE3Du ^ (uint)z * 0x27D4EB2Fu;
                h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
                return h;
            }
        }

        /// <summary>Maps the given percentiles to 0 and 255.</summary>
        private static byte[] Normalize(float[] values, float lowPercentile, float highPercentile)
        {
            var sorted = (float[])values.Clone();
            Array.Sort(sorted);
            float lo = sorted[(int)((sorted.Length - 1) * lowPercentile)], hi = sorted[(int)((sorted.Length - 1) * highPercentile)];
            var bytes = new byte[values.Length];
            float scale = 1 / Mathf.Max(1e-6f, hi - lo);
            for (int i = 0; i < values.Length; i++) bytes[i] = (byte)Mathf.RoundToInt(Mathf.Clamp01((values[i] - lo) * scale) * 255);
            return bytes;
        }

        /// <summary>Rank-maps values to a uniform 0..255 distribution.</summary>
        private static byte[] Equalize(float[] values)
        {
            var order = new int[values.Length];
            for (int i = 0; i < order.Length; i++) order[i] = i;
            var keys = (float[])values.Clone();
            Array.Sort(keys, order);
            var bytes = new byte[values.Length];
            for (int rank = 0; rank < order.Length; rank++) bytes[order[rank]] = (byte)(rank * 256L / order.Length);
            return bytes;
        }

        public static Texture3D CreateTexture3D(string name, int size, byte[] data)
        {
            var texture = new Texture3D(size, size, size, TextureFormat.R8, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 0
            };
            texture.SetPixelData(data, 0);
            texture.Apply(true, false);
            return texture;
        }

        /// <summary>
        /// The cirrus hair map. Seen near the horizon the sheet is minified far more along the view than across it,
        /// so it filters anisotropically: isotropic mips blurred the strands into haze there.
        /// </summary>
        public static Texture2D CreateCirrusTexture(string name, int size, byte[] r)
        {
            var texture = new Texture2D(size, size, TextureFormat.R8, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 8
            };
            texture.SetPixelData(r, 0);
            texture.Apply(true, false);
            return texture;
        }

        public static Texture2D CreateCoverageTexture(string name, int size, byte[] rg)
        {
            var texture = new Texture2D(size, size, TextureFormat.RG16, true, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 0
            };
            texture.SetPixelData(rg, 0);
            texture.Apply(true, false);
            return texture;
        }
    }
}
