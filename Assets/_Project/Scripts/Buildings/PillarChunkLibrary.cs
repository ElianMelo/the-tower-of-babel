using UnityEngine;

namespace TowerOfBabel.Buildings
{
    /// <summary>Closed irregular pieces in building coordinates, grouped by the stage that adds them.</summary>
    public sealed class PillarChunkLibrary : ScriptableObject
    {
        [System.Serializable]
        public struct Piece
        {
            public Mesh Mesh;
            public int Stage;
        }
        public Piece[] Pieces=System.Array.Empty<Piece>();
    }
}
