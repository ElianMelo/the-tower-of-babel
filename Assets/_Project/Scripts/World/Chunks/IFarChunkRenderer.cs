using System;
using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public struct FarChunkAsset
    {
        [SerializeField] private TowerAssetType assetType;
        [SerializeField] private Matrix4x4 objectToWorld;

        public TowerAssetType AssetType => assetType;
        public Matrix4x4 ObjectToWorld => objectToWorld;

        public FarChunkAsset(TowerAssetType assetType, Matrix4x4 objectToWorld)
        {
            this.assetType = assetType;
            this.objectToWorld = objectToWorld;
        }
    }

    public readonly struct FarChunkSnapshot
    {
        public ChunkKey Key { get; }
        public IReadOnlyList<FarChunkAsset> Assets { get; }

        public FarChunkSnapshot(ChunkKey key, IReadOnlyList<FarChunkAsset> assets)
        {
            Key = key;
            Assets = assets ?? throw new ArgumentNullException(nameof(assets));
        }
    }

    public readonly struct FarChunkStageSnapshot
    {
        public ChunkKey Key { get; }
        public IReadOnlyList<byte> StageByAssetIndex { get; }

        public FarChunkStageSnapshot(ChunkKey key, IReadOnlyList<byte> stageByAssetIndex)
        {
            Key = key;
            StageByAssetIndex = stageByAssetIndex ?? throw new ArgumentNullException(nameof(stageByAssetIndex));
        }
    }

    public interface IFarChunkRenderer
    {
        void LoadChunk(in FarChunkSnapshot snapshot);
        void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot);
        void RemoveChunk(ChunkKey key);
        void SetVisible(bool visible);
    }
}
