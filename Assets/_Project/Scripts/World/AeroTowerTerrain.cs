using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.World
{
    /// <summary>Bakes a green tower clearing, flowing hills and ocean-filled lakes into a separate TerrainData asset.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
    public sealed class AeroTowerTerrain : MonoBehaviour
    {
        [Header("Terrain assets")]
        [SerializeField] private Terrain targetTerrain;
        [SerializeField] private TerrainData sourceTerrainData;
        [SerializeField] private TerrainData generatedTerrainData;
        [Header("Four tower boundary pillars")]
        [SerializeField] private Transform[] boundaryMarkers = new Transform[4];
        [SerializeField, Min(1)] private float minimumTowerRadius = 96;
        [SerializeField, Min(0)] private float clearingPadding = 12;
        [SerializeField] private float clearingElevation;
        [Header("Rounded wave mountains")]
        [SerializeField, Min(1)] private float mountainHeight = 88;
        [SerializeField, Min(10)] private float wavelength = 260;
        [SerializeField, Min(1)] private float foothillWidth = 105;
        [SerializeField, Min(0)] private float foothillSetback = 16;
        [SerializeField, Min(1)] private float perimeterBlend = 65;
        [SerializeField] private int seed = 17;
        [SerializeField, Min(0)] private float meadowHeight = 12;
        [Header("Small lakes revealing the existing ocean")]
        [SerializeField] private Transform oceanSurface;
        [SerializeField, Min(.25f)] private float lakeDepth = 3;
        [SerializeField, Min(2)] private float lakeShoreWidth = 18;
        [SerializeField] private LakeBasin[] lakes =
        {
            new() { offset = new(147,92), radii = new(28,21), rotation = 24 },
            new() { offset = new(-150,66), radii = new(24,18), rotation = -32 },
            new() { offset = new(-61,-169), radii = new(29,20), rotation = 12 },
            new() { offset = new(150,-57), radii = new(21,16), rotation = -25 }
        };

        [Serializable]
        public struct LakeBasin
        {
            public Vector2 offset;
            public Vector2 radii;
            public float rotation;
        }
        [Header("Meadow and bare ground")]
        [SerializeField, Min(1)] private float grassFeather = 14;
        [SerializeField, Range(0, 60)] private float grassSlopeLimit = 32;
        [SerializeField, HideInInspector] private Vector2 clearingCenter;
        [SerializeField, HideInInspector] private float clearingRadius;

        public TerrainData SourceTerrainData => sourceTerrainData;
        public TerrainData GeneratedTerrainData => generatedTerrainData;
        public Vector2 ClearingCenter => clearingCenter;
        public float ClearingRadius => clearingRadius;
        public int ClearedDetailCells { get; private set; }
        public float WaterElevation => oceanSurface != null ? oceanSurface.position.y : clearingElevation - 1;
        public LakeBasin[] Lakes => lakes == null ? Array.Empty<LakeBasin>() : (LakeBasin[])lakes.Clone();

        [ContextMenu("Generate Frutiger Aero Landscape")]
        public void Generate()
        {
            if (targetTerrain == null) targetTerrain = GetComponent<Terrain>();
            if (sourceTerrainData == null || generatedTerrainData == null || sourceTerrainData == generatedTerrainData)
                throw new InvalidOperationException("Assign a separate source and generated TerrainData; the source is never modified.");
            if (targetTerrain.transform.rotation != Quaternion.identity || targetTerrain.transform.lossyScale != Vector3.one)
                throw new InvalidOperationException("The terrain must have an unrotated, unit-scale transform.");
            if (generatedTerrainData.terrainLayers.Length != 3)
                throw new InvalidOperationException("Assign the green clearing, meadow and high meadow layers in that order.");
            ResolveClearing();
            var data = generatedTerrainData;
            Vector3 size = data.size, origin = targetTerrain.transform.position;
            if (size != sourceTerrainData.size)
                throw new InvalidOperationException("Generated terrain bounds must match the original source.");
            if (clearingElevation < origin.y || clearingElevation + mountainHeight > origin.y + size.y)
                throw new InvalidOperationException("The requested elevations exceed the terrain's vertical range.");
            if (lakes != null && lakes.Length > 0)
            {
                if (oceanSurface == null || WaterElevation >= clearingElevation || WaterElevation - lakeDepth < origin.y)
                    throw new InvalidOperationException("Assign an ocean surface below the clearing and within the terrain's depth range.");
                foreach (var lake in lakes)
                {
                    if (lake.radii.x < 2 || lake.radii.y < 2)
                        throw new InvalidOperationException("Lake radii must be at least two metres.");
                    float extent = LakeExtent(lake);
                    if (lake.offset.magnitude - extent < ClearingRadius + 2)
                        throw new InvalidOperationException("Lake basins and their soft banks must stay outside the tower clearing.");
                    var point = ClearingCenter + lake.offset - new Vector2(origin.x, origin.z);
                    if (Mathf.Min(point.x, size.x-point.x, point.y, size.z-point.y) < extent + perimeterBlend)
                        throw new InvalidOperationException("Lake basins must fit inside the terrain, away from its perimeter blend.");
                }
            }
            float localX = ClearingCenter.x - origin.x, localZ = ClearingCenter.y - origin.z;
            if (Mathf.Min(localX, size.x - localX, localZ, size.z - localZ) < ClearingRadius + grassFeather)
                throw new InvalidOperationException("The full tower clearing must fit inside this terrain.");

            int resolution = data.heightmapResolution;
            var heights = new float[resolution, resolution];
            for (int z = 0; z < resolution; z++)
            {
                float localWorldZ = z * size.z / (resolution - 1);
                for (int x = 0; x < resolution; x++)
                {
                    float localWorldX = x * size.x / (resolution - 1);
                    float px = origin.x + localWorldX - ClearingCenter.x;
                    float pz = origin.z + localWorldZ - ClearingCenter.y;
                    float radius = Mathf.Sqrt(px * px + pz * pz);
                    float height = SampleLandscapeHeight(new Vector2(px, pz));
                    float edge = Mathf.Min(Mathf.Min(localWorldX, size.x - localWorldX), Mathf.Min(localWorldZ, size.z - localWorldZ));
                    if (edge < perimeterBlend)
                        height = Mathf.Lerp(origin.y + sourceTerrainData.GetInterpolatedHeight(localWorldX / size.x, localWorldZ / size.z), height,
                            Smooth(edge / Mathf.Max(1, perimeterBlend)));
                    heights[z, x] = Mathf.Clamp01((height - origin.y) / size.y);
                }
            }
            data.SetHeights(0, 0, heights);
            PaintGround(data, heights, origin, size);
            ClearGrass(data, heights, origin, size);
            var trees = new List<TreeInstance>();
            foreach (TreeInstance tree in sourceTerrainData.treeInstances)
            {
                Vector2 point = new(origin.x + tree.position.x * size.x, origin.z + tree.position.z * size.z);
                float height = data.GetInterpolatedHeight(tree.position.x, tree.position.z) + origin.y;
                if (Vector2.Distance(point, ClearingCenter) > ClearingRadius && height > WaterElevation + .3f) trees.Add(tree);
            }
            data.SetTreeInstances(trees.ToArray(), true);
            targetTerrain.terrainData = data;
            var collider = GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;
            targetTerrain.Flush();
        }

        public float SampleLandscapeHeight(Vector2 point)
        {
            float radius = point.magnitude;
            float phase = seed * .61803399f;
            float wx = point.x + Mathf.Sin(point.y / 145 + phase) * 42;
            float wz = point.y + Mathf.Cos(point.x / 175 - phase) * 36;
            float meadow = .5f + .3f * Mathf.Sin(wx / 57 + phase) + .2f * Mathf.Cos(wz / 73 - wx / 130);
            float envelope = Smooth((radius - ClearingRadius) / 38);
            float foothills = Smooth((radius - ClearingRadius - foothillSetback) / Mathf.Max(1, foothillWidth));
            float wave = .5f + .25f * Mathf.Sin(wx * Mathf.PI * 2 / wavelength + .55f * Mathf.Sin(wz / 95) + phase)
                + .17f * Mathf.Cos(wz * Mathf.PI * 2 / (wavelength * 1.31f) + phase * .7f)
                + .08f * Mathf.Sin((wx + wz) * Mathf.PI * 2 / (wavelength * .72f) + 1.6f);
            float ridges = .55f + .45f * Smooth(.5f + .5f * Mathf.Sin(Mathf.Atan2(point.y, point.x) * 2 + phase));
            float height = clearingElevation + envelope * meadowHeight * Mathf.Clamp01(meadow)
                + mountainHeight * foothills * ridges * Mathf.Clamp01(wave);
            if (lakes != null)
                foreach (var lake in lakes)
                {
                    // Broad low meadows keep water visible instead of punching wells into hills.
                    float fromBank = Vector2.Distance(point, lake.offset) - LakeExtent(lake);
                    float approach = Smooth(fromBank / 100);
                    float apron = Mathf.Lerp(clearingElevation + 1.2f, height, approach);
                    height = Mathf.Lerp(height, Mathf.Min(height, apron), envelope);
                }
            return CarveLakes(point, height);
        }

        private float CarveLakes(Vector2 point, float height)
        {
            if (lakes == null) return height;
            for (int i = 0; i < lakes.Length; i++)
            {
                var lake = lakes[i];
                Vector2 delta = point - lake.offset;
                float extent = LakeExtent(lake);
                if (Mathf.Abs(delta.x) > extent || Mathf.Abs(delta.y) > extent) continue;
                float radians = lake.rotation * Mathf.Deg2Rad;
                float x = (delta.x * Mathf.Cos(radians) + delta.y * Mathf.Sin(radians)) / lake.radii.x;
                float z = (-delta.x * Mathf.Sin(radians) + delta.y * Mathf.Cos(radians)) / lake.radii.y;
                float angle = Mathf.Atan2(z, x);
                float organicRim = 1 + .035f * Mathf.Sin(angle * 3 + i * 1.7f) + .02f * Mathf.Cos(angle * 5 - i);
                float radius = Mathf.Sqrt(x*x + z*z) / organicRim;
                float shore = (radius - 1) * Mathf.Min(lake.radii.x, lake.radii.y);
                if (shore >= lakeShoreWidth) continue;
                float basin = radius <= 1
                    ? WaterElevation - lakeDepth * (1 - Smooth(radius))
                    : Mathf.Lerp(WaterElevation, height, Smooth(shore / lakeShoreWidth));
                height = Mathf.Min(height, basin);
            }
            return height;
        }

        private float LakeExtent(LakeBasin lake)
            => Mathf.Max(lake.radii.x, lake.radii.y) * 1.06f * (1 + lakeShoreWidth / Mathf.Min(lake.radii.x, lake.radii.y));

        private void ResolveClearing()
        {
            if (boundaryMarkers == null || boundaryMarkers.Length != 4)
                throw new InvalidOperationException("Assign the four boundary pillars.");
            double mx = 0, mz = 0;
            foreach (var marker in boundaryMarkers)
            {
                if (marker == null) throw new InvalidOperationException("A boundary pillar is missing.");
                mx += marker.position.x / 4.0; mz += marker.position.z / 4.0;
            }
            // Least-squares circle fit in XZ; pillar heights do not affect the footprint.
            double xx = 0, xz = 0, zz = 0, bx = 0, bz = 0;
            foreach (var marker in boundaryMarkers)
            {
                double x = marker.position.x - mx, z = marker.position.z - mz;
                xx += x * x; xz += x * z; zz += z * z;
                bx += x * (x * x + z * z) * .5; bz += z * (x * x + z * z) * .5;
            }
            double determinant = xx * zz - xz * xz;
            if (Math.Abs(determinant) < .001) throw new InvalidOperationException("Boundary pillars must surround a circular area.");
            clearingCenter = new Vector2((float)(mx + (bx * zz - bz * xz) / determinant),
                (float)(mz + (bz * xx - bx * xz) / determinant));
            float radius = minimumTowerRadius;
            foreach (var marker in boundaryMarkers)
                radius = Mathf.Max(radius, Vector2.Distance(ClearingCenter, new Vector2(marker.position.x, marker.position.z)));
            clearingRadius = radius + Mathf.Max(0, clearingPadding);
        }

        private void PaintGround(TerrainData data, float[,] heights, Vector3 origin, Vector3 size)
        {
            int resolution = data.alphamapResolution;
            var weights = new float[resolution, resolution, 3];
            int lastHeight = data.heightmapResolution - 1;
            for (int z = 0; z < resolution; z++)
            for (int x = 0; x < resolution; x++)
            {
                float u = (float)x / (resolution - 1), v = (float)z / (resolution - 1);
                float radius = Vector2.Distance(new Vector2(origin.x + u * size.x, origin.z + v * size.z), ClearingCenter);
                float meadow = Smooth((radius - ClearingRadius) / Mathf.Max(1, grassFeather));
                float height = origin.y + heights[Mathf.RoundToInt(v * lastHeight), Mathf.RoundToInt(u * lastHeight)] * size.y;
                float highland = Smooth((height - clearingElevation - mountainHeight * .4f) / Mathf.Max(1, mountainHeight * .5f)) * .42f;
                weights[z, x, 0] = 1 - meadow;
                weights[z, x, 1] = meadow * (1 - highland);
                weights[z, x, 2] = meadow * highland;
            }
            data.SetAlphamaps(0, 0, weights);
        }

        private void ClearGrass(TerrainData data, float[,] heights, Vector3 origin, Vector3 size)
        {
            ClearedDetailCells = 0;
            int resolution = data.detailResolution;
            if (resolution == 0) return;
            float cellRadius = new Vector2(size.x / resolution, size.z / resolution).magnitude * .5f;
            float stepX = size.x / (data.heightmapResolution - 1), stepZ = size.z / (data.heightmapResolution - 1);
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                var density = new int[resolution, resolution];
                for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    Vector2 point = new(origin.x + (x + .5f) * size.x / resolution, origin.z + (z + .5f) * size.z / resolution);
                    float distance = Vector2.Distance(point, ClearingCenter);
                    float coverage = Smooth((distance - ClearingRadius - cellRadius) / Mathf.Max(1, grassFeather));
                    int hx = Mathf.RoundToInt((x + .5f) / resolution * (data.heightmapResolution - 1));
                    int hz = Mathf.RoundToInt((z + .5f) / resolution * (data.heightmapResolution - 1));
                    int left = Mathf.Max(0, hx - 1), right = Mathf.Min(data.heightmapResolution - 1, hx + 1);
                    int down = Mathf.Max(0, hz - 1), up = Mathf.Min(data.heightmapResolution - 1, hz + 1);
                    float dx = (heights[hz, right] - heights[hz, left]) * size.y / (Mathf.Max(1, right - left) * stepX);
                    float dz = (heights[up, hx] - heights[down, hx]) * size.y / (Mathf.Max(1, up - down) * stepZ);
                    float slope = Mathf.Atan(Mathf.Sqrt(dx * dx + dz * dz)) * Mathf.Rad2Deg;
                    coverage *= 1 - Smooth((slope - grassSlopeLimit) / 10);
                    float height = origin.y + heights[hz, hx] * size.y;
                    coverage *= Smooth((height - WaterElevation - .3f) / 1.2f);
                    // Instance counts are authored independently of the source's 0–255 coverage map.
                    // Inheriting those coverage values as counts creates millions of unnecessary clumps.
                    float noise = Mathf.PerlinNoise((point.x + seed * 19) * .045f, (point.y - seed * 13) * .045f);
                    int meadowDensity = Mathf.RoundToInt(Mathf.Lerp(2, 4, noise));
                    density[z, x] = coverage > .08f ? Mathf.Max(1, Mathf.RoundToInt(meadowDensity * coverage)) : 0;
                    if (density[z, x] == 0) ClearedDetailCells++;
                }
                data.SetDetailLayer(0, 0, layer, density);
            }
        }

        private static float Smooth(float value)
        {
            float t = Mathf.Clamp01(value);
            return t * t * t * (t * (t * 6 - 15) + 10);
        }
    }
}
