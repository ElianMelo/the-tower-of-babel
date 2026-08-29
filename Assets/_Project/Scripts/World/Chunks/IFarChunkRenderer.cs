using System;
using System.Collections.Generic;

namespace TowerOfBabel.World.Chunks
{
    public readonly struct FarChunkSnapshot
    {
        public ChunkKey Key { get; }
        public IReadOnlyList<ChunkAssetData> Assets { get; }

        public FarChunkSnapshot(ChunkKey key, IReadOnlyList<ChunkAssetData> assets)
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
