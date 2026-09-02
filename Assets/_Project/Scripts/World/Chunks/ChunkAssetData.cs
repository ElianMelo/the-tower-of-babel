using System;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public struct ChunkAssetData
    {
        public const byte CompletedStage = 10;

        [SerializeField] private int localIndex;
        [SerializeField] private TowerAssetType assetType;
        [SerializeField] private Vector3 position;
        [SerializeField] private Quaternion rotation;
        [SerializeField] private Vector3 scale;
        [SerializeField, Range(0, CompletedStage)] private byte stage;

        public int LocalIndex => localIndex;
        public TowerAssetType AssetType => assetType;
        public Vector3 Position => position;
        public Quaternion Rotation => rotation;
        public Vector3 Scale => scale;
        public byte Stage => stage;
        public Matrix4x4 ObjectToWorld => Matrix4x4.TRS(position, rotation, scale);

        public ChunkAssetData(int localIndex, TowerAssetType assetType, Vector3 position,
            Quaternion rotation, Vector3 scale, byte stage = 0)
        {
            this.localIndex = localIndex;
            this.assetType = assetType;
            this.position = position;
            this.rotation = rotation;
            this.scale = scale;
            this.stage = ClampStage(stage);
        }

        public void SetLocalIndex(int value)
        {
            localIndex = value;
        }

        public void SetStage(byte value)
        {
            stage = ClampStage(value);
        }

        private static byte ClampStage(byte value)
        {
            return value > CompletedStage ? CompletedStage : value;
        }
    }
}
