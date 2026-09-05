using System;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Networking.Upgrades
{
    [DisallowMultipleComponent]
    public sealed class NetworkUpgradeService : NetworkBehaviour
    {
        public static NetworkUpgradeService Instance { get; private set; }

        [SerializeField] private UpgradeTreeConfig config;

        private readonly Dictionary<int, PlayerUpgradeProgress> serverProgress = new();
        private readonly PlayerUpgradeProgress localProgress = new();

        public UpgradeTreeConfig Config => config;
        public event Action<UpgradeJob, UpgradeJobSnapshot> LocalProgressChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("Only one NetworkUpgradeService can be active at a time.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerManager.OnRemoteConnectionState += HandleRemoteConnectionState;
        }

        public override void OnStopServer()
        {
            ServerManager.OnRemoteConnectionState -= HandleRemoteConnectionState;
            serverProgress.Clear();
            base.OnStopServer();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RequestProgressSnapshotServerRpc();
        }

        public override void OnStopClient()
        {
            ResetLocalProgress();
            base.OnStopClient();
        }

        public UpgradeJobSnapshot GetLocalSnapshot(UpgradeJob job)
        {
            return localProgress.Get(job).CreateSnapshot();
        }

        public float GetLocalEffect(UpgradeJob job, UpgradeEffectType effectType)
        {
            return localProgress.Get(job).GetEffectTotal(config, effectType);
        }

        public float GetLocalActionDuration(UpgradeJob job, float baseDuration)
        {
            return Mathf.Max(0.1f, baseDuration - GetLocalEffect(job, UpgradeEffectType.Efficiency));
        }

        public int GetLocalActionCost(UpgradeJob job, int baseCost)
        {
            return Mathf.Max(1, baseCost + Mathf.RoundToInt(GetLocalEffect(job, UpgradeEffectType.Cost)));
        }

        public int GetLocalProduction(UpgradeJob job, int baseAmount)
        {
            return Mathf.Max(1, baseAmount + Mathf.RoundToInt(GetLocalEffect(job, UpgradeEffectType.Production)));
        }

        public void RequestPurchase(UpgradeJob job, string upgradeId)
        {
            if (!InstanceFinder.IsClientStarted || !IsValidJob(job) ||
                string.IsNullOrWhiteSpace(upgradeId) || upgradeId.Length > 64)
                return;

            RequestPurchaseServerRpc(job, upgradeId);
        }

        public bool RequestCheatExperience(UpgradeJob job, int amount)
        {
            if (!CanSendCheat(job) || amount <= 0)
                return false;

            CheatExperienceServerRpc(job, amount);
            return true;
        }

        public bool RequestCheatLevel(UpgradeJob job, int level)
        {
            if (!CanSendCheat(job) || level < 0 || level > UpgradeTreeConfig.MaxLevel)
                return false;

            CheatLevelServerRpc(job, level);
            return true;
        }

        public bool RequestCheatPoints(UpgradeJob job, int amount)
        {
            if (!CanSendCheat(job) || amount <= 0)
                return false;

            CheatPointsServerRpc(job, amount);
            return true;
        }

        public bool RequestCheatPurchase(UpgradeJob job, string upgradeId)
        {
            if (!CanSendCheat(job) || string.IsNullOrWhiteSpace(upgradeId) || upgradeId.Length > 64)
                return false;

            CheatPurchaseServerRpc(job, upgradeId);
            return true;
        }

        public bool RequestCheatPurchase(UpgradeJob job, int row, int column)
        {
            if (!CanSendCheat(job) || row < 0 || row >= UpgradeTreeConfig.GridSize ||
                column < 0 || column >= UpgradeTreeConfig.GridSize)
                return false;

            CheatPurchaseAtServerRpc(job, row, column);
            return true;
        }

        public bool RequestCheatReset(UpgradeJob job)
        {
            if (!CanSendCheat(job))
                return false;

            CheatResetServerRpc(job);
            return true;
        }

        public bool RequestCheatResetAll()
        {
            if (!InstanceFinder.IsClientStarted)
                return false;

            CheatResetAllServerRpc();
            return true;
        }

        public bool ServerGrantActionExperience(NetworkConnection connection, UpgradeJob job, int amount = 1)
        {
            if (!IsServerStarted || connection == null || !IsValidJob(job) || amount <= 0)
                return false;

            UpgradeJobProgress progress = GetOrCreateServerProgress(connection).Get(job);
            if (!progress.GainExperience(amount))
                return false;

            SendProgress(connection, progress);
            return true;
        }

        public float GetServerEffect(NetworkConnection connection, UpgradeJob job,
            UpgradeEffectType effectType)
        {
            if (!IsServerStarted || connection == null || !IsValidJob(job))
                return 0f;

            return GetOrCreateServerProgress(connection).Get(job).GetEffectTotal(config, effectType);
        }

        public float GetServerActionDuration(NetworkConnection connection, UpgradeJob job,
            float baseDuration)
        {
            return Mathf.Max(0.1f,
                baseDuration - GetServerEffect(connection, job, UpgradeEffectType.Efficiency));
        }

        public int GetServerActionCost(NetworkConnection connection, UpgradeJob job, int baseCost)
        {
            return Mathf.Max(1,
                baseCost + Mathf.RoundToInt(GetServerEffect(connection, job, UpgradeEffectType.Cost)));
        }

        public int GetServerProduction(NetworkConnection connection, UpgradeJob job, int baseAmount)
        {
            return Mathf.Max(1,
                baseAmount + Mathf.RoundToInt(GetServerEffect(connection, job, UpgradeEffectType.Production)));
        }

        internal PlayerUpgradeProgress GetOrCreateServerProgress(NetworkConnection connection)
        {
            if (!serverProgress.TryGetValue(connection.ClientId, out PlayerUpgradeProgress progress))
            {
                progress = new PlayerUpgradeProgress();
                serverProgress.Add(connection.ClientId, progress);
            }

            return progress;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestProgressSnapshotServerRpc(NetworkConnection sender = null)
        {
            if (sender == null)
                return;

            PlayerUpgradeProgress progress = GetOrCreateServerProgress(sender);
            SendProgress(sender, progress.Get(UpgradeJob.Gather));
            SendProgress(sender, progress.Get(UpgradeJob.Process));
            SendProgress(sender, progress.Get(UpgradeJob.Build));
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestPurchaseServerRpc(UpgradeJob job, string upgradeId,
            NetworkConnection sender = null)
        {
            if (sender == null || config == null || !IsValidJob(job) ||
                string.IsNullOrWhiteSpace(upgradeId) || upgradeId.Length > 64)
                return;

            UpgradeJobProgress progress = GetOrCreateServerProgress(sender).Get(job);
            if (!progress.TryPurchase(config, upgradeId))
            {
                SendProgress(sender, progress);
                return;
            }

            SendProgress(sender, progress);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatExperienceServerRpc(UpgradeJob job, int amount,
            NetworkConnection sender = null)
        {
            if (sender == null || !IsValidJob(job) || amount <= 0)
                return;

            UpgradeJobProgress progress = GetOrCreateServerProgress(sender).Get(job);
            bool changed = progress.GainExperience(amount);
            SendProgress(sender, progress);
            SendCheatResult(sender, changed,
                changed ? $"{job}: gained {amount} XP." : $"{job}: XP did not change (already max level).");
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatLevelServerRpc(UpgradeJob job, int level,
            NetworkConnection sender = null)
        {
            if (sender == null || !IsValidJob(job) || level < 0 || level > UpgradeTreeConfig.MaxLevel)
                return;

            UpgradeJobProgress progress = GetOrCreateServerProgress(sender).Get(job);
            int previousLevel = progress.Level;
            progress.SetLevel(level);
            SendProgress(sender, progress);
            SendCheatResult(sender, true,
                $"{job}: level set from {previousLevel} to {progress.Level}; points {progress.AvailablePoints}.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatPointsServerRpc(UpgradeJob job, int amount,
            NetworkConnection sender = null)
        {
            if (sender == null || !IsValidJob(job) || amount <= 0)
                return;

            UpgradeJobProgress progress = GetOrCreateServerProgress(sender).Get(job);
            progress.GrantUpgradePoints(amount);
            SendProgress(sender, progress);
            SendCheatResult(sender, true,
                $"{job}: granted {amount} upgrade points; available {progress.AvailablePoints}.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatPurchaseServerRpc(UpgradeJob job, string upgradeId,
            NetworkConnection sender = null)
        {
            if (sender == null || config == null || !IsValidJob(job) ||
                string.IsNullOrWhiteSpace(upgradeId) || upgradeId.Length > 64)
                return;

            PurchaseCheatUpgrade(sender, job, upgradeId);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatPurchaseAtServerRpc(UpgradeJob job, int row, int column,
            NetworkConnection sender = null)
        {
            if (sender == null || config == null || !IsValidJob(job) ||
                row < 0 || row >= UpgradeTreeConfig.GridSize ||
                column < 0 || column >= UpgradeTreeConfig.GridSize)
                return;

            UpgradeData upgrade = config.GetGridUpgrade(job, row, column);
            if (upgrade == null)
            {
                SendCheatResult(sender, false, $"{job}: no upgrade exists at [{row}][{column}].");
                return;
            }

            PurchaseCheatUpgrade(sender, job, upgrade.Id);
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatResetServerRpc(UpgradeJob job, NetworkConnection sender = null)
        {
            if (sender == null || !IsValidJob(job))
                return;

            UpgradeJobProgress progress = GetOrCreateServerProgress(sender).Get(job);
            progress.Reset();
            SendProgress(sender, progress);
            SendCheatResult(sender, true, $"{job}: progression reset.");
        }

        [ServerRpc(RequireOwnership = false)]
        private void CheatResetAllServerRpc(NetworkConnection sender = null)
        {
            if (sender == null)
                return;

            PlayerUpgradeProgress playerProgress = GetOrCreateServerProgress(sender);
            foreach (UpgradeJob job in new[] { UpgradeJob.Gather, UpgradeJob.Process, UpgradeJob.Build })
            {
                UpgradeJobProgress progress = playerProgress.Get(job);
                progress.Reset();
                SendProgress(sender, progress);
            }

            SendCheatResult(sender, true, "All upgrade progression reset.");
        }

        [TargetRpc]
        private void ReceiveProgressTargetRpc(NetworkConnection connection, UpgradeJob job, int level,
            int experience, int availablePoints, string[] purchasedUpgradeIds)
        {
            UpgradeJobSnapshot snapshot = new(job, level, experience, availablePoints,
                purchasedUpgradeIds);
            localProgress.Get(job).ApplySnapshot(snapshot);
            LocalProgressChanged?.Invoke(job, localProgress.Get(job).CreateSnapshot());
        }

        private void SendProgress(NetworkConnection connection, UpgradeJobProgress progress)
        {
            UpgradeJobSnapshot snapshot = progress.CreateSnapshot();
            ReceiveProgressTargetRpc(connection, snapshot.Job, snapshot.Level, snapshot.Experience,
                snapshot.AvailablePoints, snapshot.PurchasedUpgradeIds);
        }

        private void PurchaseCheatUpgrade(NetworkConnection connection, UpgradeJob job,
            string upgradeId)
        {
            UpgradeJobProgress progress = GetOrCreateServerProgress(connection).Get(job);
            bool purchased = progress.TryPurchase(config, upgradeId);
            SendProgress(connection, progress);
            SendCheatResult(connection, purchased, purchased
                ? $"{job}: purchased '{upgradeId}'."
                : $"{job}: could not purchase '{upgradeId}' (check points, reveal path, and purchase state).");
        }

        private void SendCheatResult(NetworkConnection connection, bool success, string message)
        {
            ReceiveCheatResultTargetRpc(connection, success, message);
        }

        [TargetRpc]
        private void ReceiveCheatResultTargetRpc(NetworkConnection connection, bool success,
            string message)
        {
            if (success)
                Debug.Log($"[Upgrade Cheat] {message}");
            else
                Debug.LogWarning($"[Upgrade Cheat] {message}");
        }

        private void HandleRemoteConnectionState(NetworkConnection connection,
            RemoteConnectionStateArgs args)
        {
            if (connection != null && args.ConnectionState == RemoteConnectionState.Stopped)
                serverProgress.Remove(connection.ClientId);
        }

        private void ResetLocalProgress()
        {
            foreach (UpgradeJob job in new[] { UpgradeJob.Gather, UpgradeJob.Process, UpgradeJob.Build })
            {
                UpgradeJobSnapshot snapshot = new(job, 0, 0, 0, Array.Empty<string>());
                localProgress.Get(job).ApplySnapshot(snapshot);
                LocalProgressChanged?.Invoke(job, snapshot);
            }
        }

        private static bool IsValidJob(UpgradeJob job)
        {
            return job is UpgradeJob.Gather or UpgradeJob.Process or UpgradeJob.Build;
        }

        private static bool CanSendCheat(UpgradeJob job)
        {
            return InstanceFinder.IsClientStarted && IsValidJob(job);
        }
    }
}
