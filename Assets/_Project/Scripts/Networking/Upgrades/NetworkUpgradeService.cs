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
    }
}
