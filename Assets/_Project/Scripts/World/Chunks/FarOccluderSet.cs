using System;
using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    /// <summary>Receives the tower's solid deck tiles (near and far chunks alike) for far occlusion culling.</summary>
    public interface IFarOccluderSink
    {
        /// <summary>Replaces one chunk's occluder source. Null assets: the chunk occludes nothing (hidden floor).</summary>
        void SetOccluderChunk(ChunkKey key, IReadOnlyList<ChunkAssetData> assets, int version);
        void ClearOccluders();
    }

    /// <summary>
    /// The far tower's occluders: for every floor, a top-down map of where its deck is solid and opaque.
    /// The far renderer hides an instance when, seen from the camera, it lies entirely beyond a deck plane and its
    /// projection onto that plane falls inside solid map texels only. The test is exact for the camera position: it
    /// uses no screen-space depth and no data from an earlier frame.
    ///
    /// A texel is solid only when all of it is opaque deck. Texels whose centre lies in a completed floor tile are
    /// filled; then every texel near a stretch of tile edge that borders a hole (stairs, an unbuilt or still-assembling
    /// tile, the deck rim) is opened again, so a solid texel never reaches a hole. Edges shared by two solid tiles
    /// stay closed, so the deck has no cracks. Completed river tiles are opaque marble outside their channel; the
    /// channel itself (water under glass) is carved out with the same distance function the shader clips with.
    /// </summary>
    public sealed class FarOccluderSet
    {
        /// <summary>Map texels per side. One float per texel and floor on the GPU.</summary>
        public const int MapSize = 384;
        /// <summary>How far beyond a tile edge its neighbour is probed, metres.</summary>
        public const float Overlap = .1f;
        /// <summary>Extra clearance kept from an edge that borders a hole (simplified meshes move edges by centimetres), metres.</summary>
        public const float Margin = .06f;
        /// <summary>Spacing of the probes along a tile edge. Holes are whole tiles, several metres wide.</summary>
        public const float SampleSpacing = .5f;
        /// <summary>A tile that just reached its final stage still assembles on screen; it occludes after this delay.</summary>
        public const float ActivationDelay = 2f;
        /// <summary>The map plane lies this far below the deck's walking surface, inside the 20 cm slab.</summary>
        public const float PlaneDepth = .1f;
        /// <summary>The CPU test (<see cref="Hides"/>) works on blocks of this many texels per side.</summary>
        public const int CoarseFactor = 4;
        public const int CoarseSize = MapSize / CoarseFactor;
        private const float TwoPi = Mathf.PI * 2f;
        // Probes sit 5 to 10 cm beyond an edge and holes are metres wide; these only absorb rounding.
        private const float ProbeSlack = .002f, AngleSlack = 1e-4f;
        private const int AngularBins = 128;
        private const float RadialBin = 2f;

        private struct Tile
        {
            public int localIndex;
            public float activeAt;
            public bool glass;
            // Radial sector: radius range, start angle and sweep about the tower axis.
            public float outer, inner, angle, sweep;
            // The slab: top and thickness.
            public float top, thickness;
            // Plain rectangular tile (towers without radial floors).
            public Vector3 position, scale;
            public Quaternion rotation;
        }

        private sealed class ChunkEntry
        {
            public int version;
            public readonly List<Tile> tiles = new();
        }

        /// <summary>One floor's solid map.</summary>
        public sealed class FloorMap
        {
            public int Floor { get; internal set; }
            /// <summary>Stable index of this floor's slice in the GPU map array.</summary>
            public int Slice { get; internal set; }
            public float PlaneY { get; internal set; }
            /// <summary>World X and Z of texel (0, 0)'s corner.</summary>
            public Vector2 Origin { get; internal set; }
            public float TexelSize { get; internal set; }
            /// <summary>False while the floor has no solid texel; it then occludes nothing.</summary>
            public bool HasSolid { get; internal set; }
            /// <summary>Bumps every time the map is rebuilt.</summary>
            public int Version { get; internal set; }
            public int SolidTexels { get; internal set; }
            internal readonly HashSet<ChunkKey> chunks = new();
            internal byte[] solid;
            // Summed-area table of the blocks that are solid throughout, for the CPU test; built on first use.
            internal int[] coarse;
            internal int coarseVersion = -1;
            internal bool dirty, urgent;
            internal float nextMaturity = float.PositiveInfinity;
        }

        private readonly Dictionary<ChunkKey, ChunkEntry> chunks = new();
        private readonly Dictionary<int, FloorMap> floors = new();
        private readonly List<FloorMap> floorList = new();
        private readonly Stack<int> freeSlices = new();
        private int nextSlice;
        private readonly List<Tile> scratchTiles = new();
        private readonly List<Vector2> openEdges = new();
        private readonly bool[] probes = new bool[64];
        private readonly Dictionary<int, List<int>> bins = new();
        private readonly Stack<List<int>> binPool = new();
        private readonly System.Diagnostics.Stopwatch clock = new();
        private bool hasRiver;
        private Vector4 riverLayout;
        // Never reset: a map version identifies one rebuild for the lifetime of the set.
        private int mapStamp;

        public bool Radial { get; private set; }
        public Vector2 Center { get; private set; }
        public IReadOnlyList<FloorMap> Floors => floorList;
        /// <summary>Slices the GPU array needs to hold every floor.</summary>
        public int SliceCount => nextSlice;
        public int ChunkCount => chunks.Count;
        /// <summary>Bumps whenever any floor map changes.</summary>
        public int Version { get; private set; }
        public int PendingFloorCount { get { int n = 0; foreach (FloorMap f in floorList) if (f.dirty) n++; return n; } }
        public int SolidTexelCount { get { int n = 0; foreach (FloorMap f in floorList) n += f.SolidTexels; return n; } }

        /// <summary>
        /// Sets the floor layout. <paramref name="river"/> is the river layout the glass floor shader clips its
        /// channel with (base radius, setback, floor height, channel half width); without it glass floors occlude
        /// nothing.
        /// </summary>
        public void Configure(bool radial, Vector2 center, Vector4? river = null)
        {
            bool riverNow = river.HasValue;
            Vector4 layout = river ?? Vector4.zero;
            if (Radial == radial && Center == center && hasRiver == riverNow && riverLayout == layout) return;
            Radial = radial; Center = center; hasRiver = riverNow; riverLayout = layout;
            // The same tiles map to different polygons; the retained sources no longer match.
            Clear();
        }

        public void Clear()
        {
            chunks.Clear(); floors.Clear(); floorList.Clear(); freeSlices.Clear(); nextSlice = 0;
            Version++;
        }

        public bool TryGetFloor(int floor, out FloorMap map) => floors.TryGetValue(floor, out map);

        /// <summary>
        /// Replaces a chunk's tiles. A tile of a chunk already known here that becomes solid waits
        /// <see cref="ActivationDelay"/> when <paramref name="delayNew"/> is set; a tile that stops being solid is
        /// withdrawn at once.
        /// </summary>
        public void SetChunk(ChunkKey key, IReadOnlyList<ChunkAssetData> assets, int version, float now, bool delayNew)
        {
            bool known = chunks.TryGetValue(key, out ChunkEntry entry);
            if (assets == null)
            {
                if (!known) return;
                chunks.Remove(key);
                if (floors.TryGetValue(key.FloorIndex, out FloorMap removedFrom))
                {
                    removedFrom.chunks.Remove(key);
                    if (entry.tiles.Count > 0) { removedFrom.dirty = true; removedFrom.urgent = true; }
                }
                return;
            }
            if (known && entry.version == version) return;

            scratchTiles.Clear();
            bool removed = false, added = false;
            for (int i = 0; i < assets.Count; i++)
            {
                ChunkAssetData asset = assets[i];
                bool glass = asset.AssetType == TowerAssetType.GlassWaterFloor;
                if ((asset.AssetType != TowerAssetType.Floor && !(glass && hasRiver)) || asset.Stage < ChunkAssetData.CompletedStage) continue;
                if (!TryCreateTile(asset, glass, out Tile tile)) continue;
                int previous = known ? IndexOf(entry.tiles, asset.LocalIndex) : -1;
                if (previous >= 0) tile.activeAt = entry.tiles[previous].activeAt;
                else { tile.activeAt = known && delayNew ? now + ActivationDelay : float.NegativeInfinity; added = true; }
                scratchTiles.Add(tile);
            }
            if (known)
                for (int i = 0; i < entry.tiles.Count && !removed; i++)
                    removed = IndexOf(scratchTiles, entry.tiles[i].localIndex) < 0;

            if (!known) { entry = new ChunkEntry(); chunks.Add(key, entry); }
            entry.version = version;
            entry.tiles.Clear(); entry.tiles.AddRange(scratchTiles);
            scratchTiles.Clear();

            if (!floors.TryGetValue(key.FloorIndex, out FloorMap floor))
            {
                floor = new FloorMap { Floor = key.FloorIndex, Slice = freeSlices.Count > 0 ? freeSlices.Pop() : nextSlice++ };
                floors.Add(key.FloorIndex, floor);
                floorList.Add(floor);
            }
            floor.chunks.Add(key);
            if (removed) { floor.dirty = true; floor.urgent = true; }
            else if (added) floor.dirty = true;
            if (added)
                for (int i = 0; i < entry.tiles.Count; i++)
                    if (entry.tiles[i].activeAt > now) floor.nextMaturity = Mathf.Min(floor.nextMaturity, entry.tiles[i].activeAt);
        }

        /// <summary>
        /// Rebuilds changed floor maps. A floor that lost a solid tile always rebuilds now (its old map is no longer
        /// safe); floors that only gained tiles share the time budget and keep their previous, still valid map while
        /// they wait. Returns true when any map changed.
        /// </summary>
        public bool Update(float now, float budgetMs = float.PositiveInfinity)
        {
            clock.Restart();
            bool rebuilt = false;
            for (int pass = 0; pass < 2; pass++)
                for (int i = 0; i < floorList.Count; i++)
                {
                    FloorMap floor = floorList[i];
                    if (pass == 1 && !floor.dirty && now >= floor.nextMaturity) floor.dirty = true;
                    if (!floor.dirty || (pass == 0) != floor.urgent) continue;
                    if (!floor.urgent && rebuilt && clock.Elapsed.TotalMilliseconds >= budgetMs) continue;
                    RebuildFloor(floor, now);
                    rebuilt = true;
                }
            if (rebuilt) Version++;
            return rebuilt;
        }

        /// <summary>True when the texel holding <paramref name="point"/> (world X, Z) is solid deck on that floor.</summary>
        public bool IsSolid(int floor, Vector2 point)
        {
            if (!floors.TryGetValue(floor, out FloorMap map) || map.solid == null || !map.HasSolid) return false;
            int x = Mathf.FloorToInt((point.x - map.Origin.x) / map.TexelSize), y = Mathf.FloorToInt((point.y - map.Origin.y) / map.TexelSize);
            return x >= 0 && y >= 0 && x < MapSize && y < MapSize && map.solid[y * MapSize + x] != 0;
        }

        /// <summary>
        /// The far renderer's deck test on the CPU, for the few things that are not far instances (lamp lights).
        /// True when, seen from <paramref name="eye"/>, the box lies entirely beyond a deck and every sight line to it
        /// crosses solid deck. A deck closer to the eye than <paramref name="closest"/> metres is not trusted. The
        /// test works on blocks of <see cref="CoarseFactor"/> texels that are solid throughout, so it hides a little
        /// less than the GPU test and never more.
        /// </summary>
        public bool Hides(Vector3 eye, Bounds box, float closest = 1f)
        {
            Vector3 center = box.center, extents = box.extents;
            float boxLow = center.y - extents.y, boxHigh = center.y + extents.y;
            for (int f = 0; f < floorList.Count; f++)
            {
                FloorMap map = floorList[f];
                if (!map.HasSolid || map.solid == null) continue;
                float rise = map.PlaneY - eye.y;
                if (Mathf.Abs(rise) < closest) continue;
                // The whole box must lie beyond the plane.
                if (rise > 0 ? boxLow < map.PlaneY : boxHigh > map.PlaneY) continue;
                float lowX = float.MaxValue, lowZ = float.MaxValue, highX = float.MinValue, highZ = float.MinValue;
                for (int corner = 0; corner < 8; corner++)
                {
                    float px = center.x + ((corner & 1) != 0 ? extents.x : -extents.x);
                    float py = center.y + ((corner & 2) != 0 ? extents.y : -extents.y);
                    float pz = center.z + ((corner & 4) != 0 ? extents.z : -extents.z);
                    // Where the sight line to this corner crosses the deck plane.
                    float t = rise / (py - eye.y);
                    float hx = eye.x + (px - eye.x) * t, hz = eye.z + (pz - eye.z) * t;
                    if (hx < lowX) lowX = hx; if (hx > highX) highX = hx;
                    if (hz < lowZ) lowZ = hz; if (hz > highZ) highZ = hz;
                }
                float inverse = 1f / map.TexelSize;
                int x0 = Mathf.FloorToInt((lowX - map.Origin.x) * inverse), x1 = Mathf.FloorToInt((highX - map.Origin.x) * inverse);
                int y0 = Mathf.FloorToInt((lowZ - map.Origin.y) * inverse), y1 = Mathf.FloorToInt((highZ - map.Origin.y) * inverse);
                if (x0 < 0 || y0 < 0 || x1 >= MapSize || y1 >= MapSize) continue;
                int[] table = CoarseTable(map);
                int cx0 = x0 / CoarseFactor, cy0 = y0 / CoarseFactor, cx1 = x1 / CoarseFactor, cy1 = y1 / CoarseFactor;
                int solid = CoarseSum(table, cx1, cy1) - CoarseSum(table, cx0 - 1, cy1) - CoarseSum(table, cx1, cy0 - 1) + CoarseSum(table, cx0 - 1, cy0 - 1);
                if (solid == (cx1 - cx0 + 1) * (cy1 - cy0 + 1)) return true;
            }
            return false;
        }

        private static int CoarseSum(int[] table, int x, int y) => x < 0 || y < 0 ? 0 : table[y * CoarseSize + x];

        private static int[] CoarseTable(FloorMap map)
        {
            if (map.coarse != null && map.coarseVersion == map.Version) return map.coarse;
            int[] table = map.coarse ??= new int[CoarseSize * CoarseSize];
            byte[] solid = map.solid;
            for (int y = 0; y < CoarseSize; y++)
            {
                int running = 0;
                for (int x = 0; x < CoarseSize; x++)
                {
                    int filled = 1;
                    for (int v = 0; v < CoarseFactor && filled != 0; v++)
                    {
                        int row = (y * CoarseFactor + v) * MapSize + x * CoarseFactor;
                        for (int u = 0; u < CoarseFactor; u++)
                            if (solid[row + u] == 0) { filled = 0; break; }
                    }
                    running += filled;
                    table[y * CoarseSize + x] = running + (y > 0 ? table[(y - 1) * CoarseSize + x] : 0);
                }
            }
            map.coarseVersion = map.Version;
            return table;
        }

        /// <summary>
        /// Writes the floor's summed-area table (row y, column x: solid texels with both coordinates at or below) into
        /// <paramref name="table"/>, which must hold <see cref="MapSize"/> squared floats.
        /// </summary>
        public void FillSummedArea(FloorMap map, float[] table)
        {
            byte[] solid = map.solid;
            for (int y = 0; y < MapSize; y++)
            {
                int row = y * MapSize;
                float running = 0;
                for (int x = 0; x < MapSize; x++)
                {
                    running += solid != null ? solid[row + x] : 0;
                    table[row + x] = running + (y > 0 ? table[row - MapSize + x] : 0);
                }
            }
        }

        private static int IndexOf(List<Tile> tiles, int localIndex)
        {
            for (int i = 0; i < tiles.Count; i++) if (tiles[i].localIndex == localIndex) return i;
            return -1;
        }

        private bool TryCreateTile(ChunkAssetData asset, bool glass, out Tile tile)
        {
            tile = new Tile
            {
                localIndex = asset.LocalIndex, glass = glass, position = asset.Position, rotation = asset.Rotation, scale = asset.Scale,
                top = asset.Position.y, thickness = .2f * Mathf.Abs(asset.Scale.y)
            };
            if (tile.thickness < PlaneDepth + .03f) return false;
            if (!Radial) return Mathf.Abs(asset.Scale.x) > .01f && Mathf.Abs(asset.Scale.z) > .01f;
            Vector2 delta = new Vector2(asset.Position.x, asset.Position.z) - Center;
            float radius = delta.magnitude;
            if (radius < .01f) return false;
            tile.outer = radius;
            tile.inner = Mathf.Max(0f, radius - asset.Scale.x * TowerFloorLayout.TemplateSize);
            tile.angle = Mathf.Atan2(delta.y, delta.x);
            tile.sweep = asset.Scale.z * TowerFloorLayout.TemplateSize / radius;
            return tile.sweep > 1e-4f && tile.sweep <= TwoPi + 1e-3f && tile.outer - tile.inner > .01f;
        }

        private void RebuildFloor(FloorMap floor, float now)
        {
            floor.dirty = floor.urgent = false;
            floor.nextMaturity = float.PositiveInfinity;
            floor.Version = ++mapStamp;
            floor.HasSolid = false; floor.SolidTexels = 0;
            scratchTiles.Clear();
            // The deck's walking surface is the first tile's top; a piece at another height is left out below.
            float top = float.NaN;
            foreach (ChunkKey key in floor.chunks)
            {
                if (!chunks.TryGetValue(key, out ChunkEntry entry)) continue;
                for (int i = 0; i < entry.tiles.Count; i++)
                {
                    Tile tile = entry.tiles[i];
                    if (tile.activeAt > now) { floor.nextMaturity = Mathf.Min(floor.nextMaturity, tile.activeAt); continue; }
                    if (float.IsNaN(top)) top = tile.top;
                    scratchTiles.Add(tile);
                }
            }
            if (scratchTiles.Count == 0) return;
            float plane = top - PlaneDepth;
            // Only slabs that hold the map plane count; a piece at another height is left out.
            for (int i = scratchTiles.Count - 1; i >= 0; i--)
            {
                Tile tile = scratchTiles[i];
                if (plane < tile.top - tile.thickness + .02f || plane > tile.top - .02f) scratchTiles.RemoveAt(i);
            }
            if (scratchTiles.Count == 0) return;

            Rasterize(floor, plane);
        }

        private Vector2 Point(float radius, float angle) =>
            new(Center.x + Mathf.Cos(angle) * radius, Center.y + Mathf.Sin(angle) * radius);

        /// <summary>
        /// Probes the strip just beyond one tile edge and records every stretch that does not border solid tile.
        /// A radial edge (<paramref name="alongRadius"/>) runs from radius <paramref name="from"/> to
        /// <paramref name="to"/> at angle <paramref name="fixedValue"/>; an arc runs between two angles at that radius.
        /// The two probe rows lie at <paramref name="rowA"/> and <paramref name="rowB"/> across the edge.
        /// </summary>
        private void OpenStretches(float from, float to, float fixedValue, float rowA, float rowB, bool alongRadius)
        {
            float length = alongRadius ? to - from : (to - from) * fixedValue;
            int samples = Mathf.Clamp(Mathf.CeilToInt(length / SampleSpacing) + 1, 2, probes.Length);
            for (int i = 0; i < samples; i++)
            {
                float along = Mathf.Lerp(from, to, i / (float)(samples - 1));
                probes[i] = alongRadius ? PointSolid(along, rowA) && PointSolid(along, rowB) : PointSolid(rowA, along) && PointSolid(rowB, along);
            }
            for (int i = 0; i < samples - 1; i++)
            {
                // A hole starts somewhere between a solid and an open probe: the whole interval is open.
                if (probes[i] && probes[i + 1]) continue;
                float t0 = Mathf.Lerp(from, to, i / (float)(samples - 1)), t1 = Mathf.Lerp(from, to, (i + 1) / (float)(samples - 1));
                openEdges.Add(alongRadius ? Point(t0, fixedValue) : Point(fixedValue, t0));
                openEdges.Add(alongRadius ? Point(t1, fixedValue) : Point(fixedValue, t1));
            }
        }

        private void AddSectorEdges(in Tile tile)
        {
            float beyond = Overlap / tile.outer, end = tile.angle + tile.sweep;
            OpenStretches(tile.inner, tile.outer, tile.angle, tile.angle - beyond * .5f, tile.angle - beyond, true);
            OpenStretches(tile.inner, tile.outer, end, end + beyond * .5f, end + beyond, true);
            OpenStretches(tile.angle, end, tile.outer, tile.outer + Overlap * .5f, tile.outer + Overlap, false);
            if (tile.inner > 1e-3f)
                OpenStretches(tile.angle, end, tile.inner, tile.inner - Overlap * .5f, tile.inner - Overlap, false);
        }

        private bool PointSolid(float radius, float angle)
        {
            if (radius < 0f) return false;
            if (!bins.TryGetValue(BinKey(RadialIndex(radius), AngularIndex(angle)), out List<int> candidates)) return false;
            for (int i = 0; i < candidates.Count; i++)
            {
                Tile tile = scratchTiles[candidates[i]];
                // A probe on the line where two tiles meet belongs to both (rings share the edge at angle zero).
                if (radius < tile.inner - ProbeSlack || radius > tile.outer + ProbeSlack) continue;
                if (Mathf.Repeat(angle - tile.angle + AngleSlack, TwoPi) > tile.sweep + AngleSlack * 2f) continue;
                return true;
            }
            return false;
        }

        // Texels whose centre lies in the sector: between its radii and, for the usual sweep under half a turn,
        // on the inner side of both radial edges.
        private void FillSector(in Tile tile, byte[] solid, Vector2 origin, float texel)
        {
            float end = tile.angle + tile.sweep, inverse = 1f / texel;
            Vector2 start = new(Mathf.Cos(tile.angle), Mathf.Sin(tile.angle)), stop = new(Mathf.Cos(end), Mathf.Sin(end));
            Vector2 low = Vector2.Min(Vector2.Min(Center + start * tile.inner, Center + start * tile.outer), Vector2.Min(Center + stop * tile.inner, Center + stop * tile.outer));
            Vector2 high = Vector2.Max(Vector2.Max(Center + start * tile.inner, Center + start * tile.outer), Vector2.Max(Center + stop * tile.inner, Center + stop * tile.outer));
            // The arc bulges past its end points where it crosses an axis direction.
            for (int quarter = Mathf.CeilToInt(tile.angle / (Mathf.PI * .5f)); quarter * (Mathf.PI * .5f) <= end; quarter++)
            {
                Vector2 p = Point(tile.outer, quarter * (Mathf.PI * .5f));
                low = Vector2.Min(low, p); high = Vector2.Max(high, p);
            }
            int x0 = Mathf.Max(0, Mathf.FloorToInt((low.x - origin.x) * inverse)), x1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt((high.x - origin.x) * inverse));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((low.y - origin.y) * inverse)), y1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt((high.y - origin.y) * inverse));
            float innerSq = tile.inner * tile.inner, outerSq = tile.outer * tile.outer;
            bool narrow = tile.sweep < Mathf.PI;
            // Shared edges belong to both tiles.
            const float Slack = 1e-4f;
            for (int y = y0; y <= y1; y++)
            {
                float vz = origin.y + (y + .5f) * texel - Center.y;
                int row = y * MapSize;
                for (int x = x0; x <= x1; x++)
                {
                    float vx = origin.x + (x + .5f) * texel - Center.x;
                    float distanceSq = vx * vx + vz * vz;
                    if (distanceSq < innerSq - Slack || distanceSq > outerSq + Slack) continue;
                    if (narrow)
                    {
                        if (start.x * vz - start.y * vx < -Slack || vx * stop.y - vz * stop.x < -Slack) continue;
                    }
                    else if (Mathf.Repeat(Mathf.Atan2(vz, vx) - tile.angle, TwoPi) > tile.sweep) continue;
                    solid[row + x] = 1;
                }
            }
        }

        // A plain tile's neighbours are not consulted: every edge stays open.
        private void FillRectangle(in Tile tile, byte[] solid, Vector2 origin, float texel)
        {
            Matrix4x4 matrix = Matrix4x4.TRS(tile.position, tile.rotation, tile.scale), toLocal = matrix.inverse;
            float size = TowerFloorLayout.TemplateSize, inverse = 1f / texel;
            Vector2 low = new(float.MaxValue, float.MaxValue), high = new(float.MinValue, float.MinValue);
            Vector2 previous = default, first = default;
            for (int corner = 0; corner < 4; corner++)
            {
                // Corners in order around the tile.
                Vector3 local = corner switch { 0 => new Vector3(0, 0, 0), 1 => new Vector3(size, 0, 0), 2 => new Vector3(size, 0, -size), _ => new Vector3(0, 0, -size) };
                Vector3 world = matrix.MultiplyPoint3x4(local);
                var p = new Vector2(world.x, world.z);
                low = Vector2.Min(low, p); high = Vector2.Max(high, p);
                if (corner == 0) first = p; else { openEdges.Add(previous); openEdges.Add(p); }
                previous = p;
            }
            openEdges.Add(previous); openEdges.Add(first);
            int x0 = Mathf.Max(0, Mathf.FloorToInt((low.x - origin.x) * inverse)), x1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt((high.x - origin.x) * inverse));
            int y0 = Mathf.Max(0, Mathf.FloorToInt((low.y - origin.y) * inverse)), y1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt((high.y - origin.y) * inverse));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    Vector3 local = toLocal.MultiplyPoint3x4(new Vector3(origin.x + (x + .5f) * texel, tile.position.y, origin.y + (y + .5f) * texel));
                    if (local.x >= 0 && local.x <= size && local.z <= 0 && local.z >= -size) solid[y * MapSize + x] = 1;
                }
        }

        private static int RadialIndex(float radius) => Mathf.FloorToInt(radius / RadialBin);
        private static int AngularIndex(float angle) => Mathf.Min(AngularBins - 1, (int)(Mathf.Repeat(angle, TwoPi) / TwoPi * AngularBins));
        private static int BinKey(int radial, int angular) => radial * AngularBins + angular;

        private void BuildBins()
        {
            for (int i = 0; i < scratchTiles.Count; i++)
            {
                Tile tile = scratchTiles[i];
                int r0 = RadialIndex(tile.inner), r1 = RadialIndex(tile.outer);
                int span = Mathf.Min(AngularBins, Mathf.CeilToInt(tile.sweep / TwoPi * AngularBins) + 1);
                int first = AngularIndex(tile.angle);
                for (int r = r0; r <= r1; r++)
                    for (int s = 0; s < span; s++)
                    {
                        int key = BinKey(r, (first + s) % AngularBins);
                        if (!bins.TryGetValue(key, out List<int> list))
                        {
                            list = binPool.Count > 0 ? binPool.Pop() : new List<int>(8);
                            bins.Add(key, list);
                        }
                        list.Add(i);
                    }
            }
        }

        private void ReleaseBins()
        {
            foreach (List<int> list in bins.Values) { list.Clear(); binPool.Push(list); }
            bins.Clear();
        }

        // Fills every texel whose centre lies in a solid tile, opens every texel near an edge stretch that borders a
        // hole and carves the river channel. scratchTiles holds the floor's solid tiles.
        private void Rasterize(FloorMap floor, float plane)
        {
            Vector2 low = new(float.MaxValue, float.MaxValue), high = new(float.MinValue, float.MinValue);
            bool carveRiver = false;
            Matrix4x4 rectangle = default;
            for (int i = 0; i < scratchTiles.Count; i++)
            {
                Tile tile = scratchTiles[i];
                carveRiver |= tile.glass;
                if (Radial)
                {
                    // The sector lies inside its outer circle's box; exact enough for the map extent.
                    low = Vector2.Min(low, Center - Vector2.one * tile.outer); high = Vector2.Max(high, Center + Vector2.one * tile.outer);
                    continue;
                }
                rectangle = Matrix4x4.TRS(tile.position, tile.rotation, tile.scale);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 p = rectangle.MultiplyPoint3x4(new Vector3((corner & 1) * TowerFloorLayout.TemplateSize, 0, -(corner >> 1) * TowerFloorLayout.TemplateSize));
                    low = Vector2.Min(low, new Vector2(p.x, p.z)); high = Vector2.Max(high, new Vector2(p.x, p.z));
                }
            }
            float extent = Mathf.Max(high.x - low.x, high.y - low.y, 1f);
            // Two texels of open border all around.
            float texel = extent / (MapSize - 4);
            Vector2 origin = (low + high) * .5f - Vector2.one * (texel * MapSize * .5f);
            floor.PlaneY = plane; floor.Origin = origin; floor.TexelSize = texel;
            byte[] solid = floor.solid ??= new byte[MapSize * MapSize];
            Array.Clear(solid, 0, solid.Length);
            float inverse = 1f / texel;
            openEdges.Clear();

            if (Radial)
            {
                BuildBins();
                for (int i = 0; i < scratchTiles.Count; i++)
                {
                    Tile tile = scratchTiles[i];
                    FillSector(tile, solid, origin, texel);
                    AddSectorEdges(tile);
                }
                ReleaseBins();
            }
            else
                for (int i = 0; i < scratchTiles.Count; i++) FillRectangle(scratchTiles[i], solid, origin, texel);
            scratchTiles.Clear();

            // A texel that touches an open stretch has its centre within .71 texels of it; the margin covers edges
            // that simplified meshes pull inward and the straight spans standing in for arcs.
            float reach = .75f + Margin * inverse;
            for (int i = 0; i < openEdges.Count; i += 2)
            {
                float ax = (openEdges[i].x - origin.x) * inverse, ay = (openEdges[i].y - origin.y) * inverse;
                float bx = (openEdges[i + 1].x - origin.x) * inverse, by = (openEdges[i + 1].y - origin.y) * inverse;
                int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ax, bx) - reach)), x1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt(Mathf.Max(ax, bx) + reach));
                int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(ay, by) - reach)), y1 = Mathf.Min(MapSize - 1, Mathf.FloorToInt(Mathf.Max(ay, by) + reach));
                float dx = bx - ax, dy = by - ay, lengthSq = dx * dx + dy * dy;
                for (int y = y0; y <= y1; y++)
                {
                    float py = y + .5f;
                    int row = y * MapSize;
                    for (int x = x0; x <= x1; x++)
                    {
                        if (solid[row + x] == 0) continue;
                        float px = x + .5f;
                        float t = lengthSq > 1e-12f ? Mathf.Clamp01(((px - ax) * dx + (py - ay) * dy) / lengthSq) : 0f;
                        float qx = ax + dx * t - px, qy = ay + dy * t - py;
                        if (qx * qx + qy * qy <= reach * reach) solid[row + x] = 0;
                    }
                }
            }

            if (carveRiver)
            {
                // The channel distance changes by at most a metre per metre: a texel whose centre is farther from the
                // channel than its half diagonal lies wholly on opaque marble.
                float clearance = texel * .7072f + Margin;
                for (int y = 0; y < MapSize; y++)
                {
                    int row = y * MapSize;
                    float z = origin.y + (y + .5f) * texel - Center.y;
                    for (int x = 0; x < MapSize; x++)
                    {
                        if (solid[row + x] == 0) continue;
                        var point = new Vector2(origin.x + (x + .5f) * texel - Center.x, z);
                        if (TowerOfBabel.Rendering.RiverPoolGeometry.ChannelDistance(point, floor.Floor, riverLayout) < clearance) solid[row + x] = 0;
                    }
                }
            }

            int count = 0;
            for (int i = 0; i < solid.Length; i++) count += solid[i];
            floor.SolidTexels = count;
            floor.HasSolid = count > 0;
        }
    }
}
