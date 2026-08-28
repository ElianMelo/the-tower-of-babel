using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    public static class ChunkGrid
    {
        public const float DefaultChunkSizeMeters = 32f;

        public static ChunkKey WorldToChunk(Vector3 worldPosition, float chunkSizeMeters, float floorHeight)
        {
            ValidateDimensions(chunkSizeMeters, floorHeight);

            return new ChunkKey(
                Mathf.FloorToInt(worldPosition.y / floorHeight),
                Mathf.FloorToInt(worldPosition.x / chunkSizeMeters),
                Mathf.FloorToInt(worldPosition.z / chunkSizeMeters));
        }

        public static int ChebyshevDistance(ChunkKey first, ChunkKey second)
        {
            long floorDistance = Math.Abs((long)first.FloorIndex - second.FloorIndex);
            long xDistance = Math.Abs((long)first.X - second.X);
            long zDistance = Math.Abs((long)first.Z - second.Z);
            long distance = Math.Max(floorDistance, Math.Max(xDistance, zDistance));
            return distance > int.MaxValue ? int.MaxValue : (int)distance;
        }

        public static void GetNeighborhood(ChunkKey center, int radius, ICollection<ChunkKey> results)
        {
            if (radius < 0)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            for (int floor = center.FloorIndex - radius; floor <= center.FloorIndex + radius; floor++)
            {
                for (int x = center.X - radius; x <= center.X + radius; x++)
                {
                    for (int z = center.Z - radius; z <= center.Z + radius; z++)
                        results.Add(new ChunkKey(floor, x, z));
                }
            }
        }

        public static Bounds GetWorldBounds(ChunkKey key, float chunkSizeMeters, float floorHeight)
        {
            ValidateDimensions(chunkSizeMeters, floorHeight);

            Vector3 size = new(chunkSizeMeters, floorHeight, chunkSizeMeters);
            Vector3 minimum = new(key.X * chunkSizeMeters, key.FloorIndex * floorHeight, key.Z * chunkSizeMeters);
            return new Bounds(minimum + size * 0.5f, size);
        }

        private static void ValidateDimensions(float chunkSizeMeters, float floorHeight)
        {
            if (chunkSizeMeters <= 0f)
                throw new ArgumentOutOfRangeException(nameof(chunkSizeMeters));
            if (floorHeight <= 0f)
                throw new ArgumentOutOfRangeException(nameof(floorHeight));
        }
    }
}
