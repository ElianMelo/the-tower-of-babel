using System;
using System.Collections.Generic;
using System.Linq;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking.Buildings;
using TowerOfBabel.Networking.Resources;
using TowerOfBabel.Resources;
using TowerOfBabel.Upgrades;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.Networking.Upgrades
{
    public sealed partial class NetworkUpgradeService
    {
        private readonly CheatProfile localCheats = new();
        private readonly Dictionary<int, CheatProfile> serverCheats = new();
        public event Action<string> CheatResult;
        public event Action CheatStatsChanged;
        public float GetLocalCheatStat(CheatStat stat) => localCheats.Get(stat);
        public float GetServerCheatStat(NetworkConnection player, CheatStat stat) => player != null && serverCheats.TryGetValue(player.ClientId, out var profile) ? profile.Get(stat) : CheatProfile.Default(stat);
        private static CheatStat SpeedStat(UpgradeJob job) => (CheatStat)((int)CheatStat.GatherSpeed + (int)job);
        private static CheatStat CostStat(UpgradeJob job) => (CheatStat)((int)CheatStat.GatherCost + (int)job);
        private static CheatStat ProductionStat(UpgradeJob job) => (CheatStat)((int)CheatStat.GatherProduction + (int)job);
        public bool RequestCheatStat(CheatStat stat, float value)
        {
            if (!InstanceFinder.IsClientStarted || !Enum.IsDefined(typeof(CheatStat), stat) || float.IsNaN(value) || float.IsInfinity(value)) return false;
            CheatStatServerRpc(stat, value); return true;
        }
        public bool RequestCheatCommand(CheatCommand command, int value = 100, int floor = 0,
            ChunkKey key = default, int localIndex = -1, ulong node = 0, string processor = "")
        {
            if (!InstanceFinder.IsClientStarted || !Enum.IsDefined(typeof(CheatCommand), command)) return false;
            CheatCommandServerRpc(command, value, floor, key.FloorIndex, key.X, key.Z, localIndex, node, processor); return true;
        }
        [ServerRpc(RequireOwnership = false)]
        private void CheatStatServerRpc(CheatStat stat, float value, NetworkConnection sender = null)
        {
            if (sender == null || !Enum.IsDefined(typeof(CheatStat), stat)) return;
            if (!serverCheats.TryGetValue(sender.ClientId, out var profile)) serverCheats[sender.ClientId] = profile = new CheatProfile();
            profile.Set(stat, value);
            if (stat == CheatStat.BagCapacity) NetworkResourceService.Instance?.ServerCheatCapacity(sender, (int)profile.Get(stat));
            if (stat == CheatStat.InstantBuild && profile.Get(stat) > .5f && NetworkResourceService.Instance != null)
            {
                var resources = NetworkResourceService.Instance;
                foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                    resources.ServerCheatSetResource(sender, type, resources.ServerCapacity(sender));
            }
            CheatStatsTargetRpc(sender, profile.Snapshot());
            SendCheatResult(sender, true, $"{stat}: {profile.Get(stat):0.##}");
        }
        [TargetRpc]
        private void CheatStatsTargetRpc(NetworkConnection sender, float[] values)
        {
            localCheats.Apply(values); CheatStatsChanged?.Invoke();
        }
        [ServerRpc(RequireOwnership = false)]
        private void CheatCommandServerRpc(CheatCommand command, int value, int floor, int keyFloor, int x, int z,
            int localIndex, ulong node, string processor, NetworkConnection sender = null)
        {
            if (sender == null || !Enum.IsDefined(typeof(CheatCommand), command)) return;
            value = Mathf.Clamp(value, 1, 1000000);
            bool success = false;
            var resources = NetworkResourceService.Instance;
            switch (command)
            {
                case CheatCommand.AddStone: success = resources != null && resources.ServerCheatResource(sender, ResourceType.Stone, value); break;
                case CheatCommand.AddProcessedStone: success = resources != null && resources.ServerCheatResource(sender, ResourceType.ProcessedStone, value); break;
                case CheatCommand.AddSand: success = resources != null && resources.ServerCheatResource(sender, ResourceType.Sand, value); break;
                case CheatCommand.AddGlass: success = resources != null && resources.ServerCheatResource(sender, ResourceType.Glass, value); break;
                case CheatCommand.AddRiverEssence: success = resources != null && resources.ServerCheatResource(sender, ResourceType.RiverEssence, value); break;
                case CheatCommand.AddRiverBlock: success = resources != null && resources.ServerCheatResource(sender, ResourceType.RiverBlock, value); break;
                case CheatCommand.AddWaterEssence: success = resources != null && resources.ServerCheatResource(sender, ResourceType.WaterEssence, value); break;
                case CheatCommand.AddWaterBlock: success = resources != null && resources.ServerCheatResource(sender, ResourceType.WaterBlock, value); break;
                case CheatCommand.AddSkyEssence: success = resources != null && resources.ServerCheatResource(sender, ResourceType.SkyEssence, value); break;
                case CheatCommand.AddSkyBlock: success = resources != null && resources.ServerCheatResource(sender, ResourceType.SkyBlock, value); break;
                case CheatCommand.FillBag: case CheatCommand.ClearBag:
                    if (resources == null) break;
                    foreach (ResourceType type in Enum.GetValues(typeof(ResourceType))) resources.ServerCheatSetResource(sender, type, command == CheatCommand.FillBag ? resources.ServerCapacity(sender) : 0);
                    success = true; break;
                case CheatCommand.ResetResources: success = resources != null && resources.ServerCheatResetNodes(); break;
                case CheatCommand.InstantGather: success = resources != null && resources.ServerCheatGather(sender, node); break;
                case CheatCommand.InstantProcess: success = resources != null && resources.ServerCheatProcess(sender, processor); break;
                case CheatCommand.BuildAsset: case CheatCommand.AdvanceAsset: case CheatCommand.ResetAsset:
                case CheatCommand.BuildFloor: case CheatCommand.ResetFloor: case CheatCommand.BuildTower: case CheatCommand.ResetTower:
                    success = NetworkBuildingService.Instance != null && NetworkBuildingService.Instance.ServerCheatConstruction(command, floor, new ChunkKey(keyFloor, x, z), localIndex); break;
                case CheatCommand.UnlockAllUpgrades:
                    if (config == null) break;
                    foreach (UpgradeJob job in Enum.GetValues(typeof(UpgradeJob)))
                    {
                        var progress = GetOrCreateServerProgress(sender).Get(job);
                        progress.ApplySnapshot(new UpgradeJobSnapshot(job, UpgradeTreeConfig.MaxLevel, 0, 100,
                            config.GetUpgrades(job).Select(u => u.Id).ToArray()));
                        SendProgress(sender, progress);
                    }
                    success = true; break;
                case CheatCommand.ResetModifiers:
                    serverCheats[sender.ClientId] = new CheatProfile();
                    resources?.ServerCheatCapacity(sender, (int)CheatProfile.Default(CheatStat.BagCapacity));
                    CheatStatsTargetRpc(sender, serverCheats[sender.ClientId].Snapshot()); success = true; break;
            }
            SendCheatResult(sender, success, success ? $"{command} completed." : $"{command}: select a valid target, check materials, or connect to a session.");
        }
    }
}
