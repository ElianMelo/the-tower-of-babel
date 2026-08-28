using System;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public struct ChunkKey : IEquatable<ChunkKey>, IComparable<ChunkKey>
    {
        [SerializeField] private int floorIndex;
        [SerializeField] private int x;
        [SerializeField] private int z;

        public int FloorIndex => floorIndex;
        public int X => x;
        public int Z => z;

        public ChunkKey(int floorIndex, int x, int z)
        {
            this.floorIndex = floorIndex;
            this.x = x;
            this.z = z;
        }

        public int CompareTo(ChunkKey other)
        {
            int floorComparison = floorIndex.CompareTo(other.floorIndex);
            if (floorComparison != 0)
                return floorComparison;

            int xComparison = x.CompareTo(other.x);
            return xComparison != 0 ? xComparison : z.CompareTo(other.z);
        }

        public bool Equals(ChunkKey other)
        {
            return floorIndex == other.floorIndex && x == other.x && z == other.z;
        }

        public override bool Equals(object obj)
        {
            return obj is ChunkKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = floorIndex;
                hash = (hash * 397) ^ x;
                return (hash * 397) ^ z;
            }
        }

        public override string ToString()
        {
            return $"Floor {floorIndex}, X {x}, Z {z}";
        }

        public static bool operator ==(ChunkKey left, ChunkKey right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ChunkKey left, ChunkKey right)
        {
            return !left.Equals(right);
        }
    }
}
