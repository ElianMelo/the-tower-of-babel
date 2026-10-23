using FishNet.Object;
using TowerOfBabel.Cheats;
using TowerOfBabel.World.Chunks;

namespace TowerOfBabel.Networking.Buildings
{
    public sealed partial class NetworkBuildingService
    {
        public bool ServerCheatConstruction(CheatCommand command, int floor, ChunkKey key, int index)
        {
            if (!IsServerStarted || chunkManager == null) return false;
            bool assetOnly = command is CheatCommand.BuildAsset or CheatCommand.ResetAsset or CheatCommand.AdvanceAsset;
            bool all = command is CheatCommand.BuildTower or CheatCommand.ResetTower;
            byte stage = command is CheatCommand.ResetAsset or CheatCommand.ResetFloor or CheatCommand.ResetTower ? (byte)0 : ChunkAssetData.CompletedStage;
            if (assetOnly)
            {
                if (!chunkManager.TryGetAsset(key, index, out var asset)) return false;
                if (command == CheatCommand.AdvanceAsset) stage = (byte)UnityEngine.Mathf.Min(10, asset.Stage + 1);
            }
            else if (!all)
            {
                bool exists = false;
                foreach (var chunk in chunkManager.CachedChunks) if (chunk.Key.FloorIndex == floor) { exists = true; break; }
                if (!exists) return false;
            }
            foreach (var build in activeBuilds.Values) if (build.Routine != null) StopCoroutine(build.Routine);
            activeBuilds.Clear();
            if (assetOnly)
            {
                chunkManager.SetAssetStage(key, index, stage);
                ApplyStageObserversRpc(key.FloorIndex, key.X, key.Z, index, stage);
            }
            else
            {
                chunkManager.SetConstructionStage(all ? null : floor, stage);
                CheatConstructionObserversRpc(all, floor, stage);
            }
            return true;
        }
        [ObserversRpc(ExcludeServer = true)]
        private void CheatConstructionObserversRpc(bool all, int floor, byte stage)
        { ResolveChunkManager(); chunkManager?.SetConstructionStage(all ? null : floor, stage); }
    }
}
