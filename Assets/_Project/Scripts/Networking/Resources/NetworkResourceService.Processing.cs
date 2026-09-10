using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Resources;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Networking.Resources
{
    public sealed partial class NetworkResourceService
    {
        private sealed class ActiveProcess
        {
            public ResourceProcessor Processor;
            public ResourceType Source;
            public ResourceType Target;
            public int Cost;
            public int Production;
            public float Duration;
            public Coroutine Routine;
            public uint RequestId;
        }

        private readonly Dictionary<string, ResourceProcessor> processors = new();
        private readonly Dictionary<int, ActiveProcess> activeProcesses = new();
        private ResourceProcessor localActiveProcessor;
        private uint localProcessRequestId;

        public bool CanExchangeLocal(ResourceType source, int cost, ResourceType target, int production)
        {
            long targetAfter = (long)GetLocalAmount(target) + production - (source == target ? cost : 0);
            return cost > 0 && production > 0 && GetLocalAmount(source) >= cost && targetAfter <= capacityPerResource;
        }

        public bool RequestProcessStart(ResourceProcessor processor, Vector3 playerPosition)
        {
            if (!InstanceFinder.IsClientStarted || processor == null || !processor.CanInteract
                || localActiveProcessor != null || localActiveResource != null)
                return false;
            localActiveProcessor = processor;
            localProcessRequestId++;
            RequestProcessStartServerRpc(processor.ProcessorId, localProcessRequestId, playerPosition);
            return true;
        }

        public void RequestProcessCancel(ResourceProcessor processor)
        {
            if (processor == null || localActiveProcessor != processor)
                return;
            if (InstanceFinder.IsClientStarted)
                RequestProcessCancelServerRpc(processor.ProcessorId, localProcessRequestId);
            localActiveProcessor = null;
        }

        public override void OnStopClient()
        {
            ResourceProcessor interrupted = localActiveProcessor;
            localActiveProcessor = null;
            interrupted?.RejectByServer();
            Resource resource = localActiveResource;
            localActiveResource = null;
            resource?.RejectByServer();
            base.OnStopClient();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestProcessStartServerRpc(string processorId, uint requestId, Vector3 claimedPlayerPosition,
            NetworkConnection sender = null)
        {
            if (sender == null)
                return;
            if (string.IsNullOrEmpty(processorId) || !processors.TryGetValue(processorId, out ResourceProcessor processor))
            {
                // Scene processors may have been loaded after the service started.
                RebuildProcessorLookup();
                if (string.IsNullOrEmpty(processorId) || !processors.TryGetValue(processorId, out processor))
                {
                    FinishProcessTargetRpc(sender, processorId, requestId, false);
                    return;
                }
            }

            if (!TryStartServerProcess(sender, processor, claimedPlayerPosition, requestId))
                FinishProcessTargetRpc(sender, processorId, requestId, false);
        }

        private bool TryStartServerProcess(NetworkConnection sender, ResourceProcessor processor, Vector3 playerPosition, uint requestId)
        {
            if (sender == null || processor == null || !processor.ServerCanProcess
                || activeProcesses.ContainsKey(sender.ClientId) || activeGathers.ContainsKey(sender.ClientId)
                || !IsWithinProcessDistance(playerPosition, processor.transform.position))
                return false;

            NetworkUpgradeService upgrades = NetworkUpgradeService.Instance;
            ActiveProcess process = new()
            {
                Processor = processor,
                RequestId = requestId,
                Source = processor.SourceResource.ResourceType,
                Target = processor.TargetResource.ResourceType,
                Cost = upgrades != null ? upgrades.GetServerActionCost(sender, UpgradeJob.Process, processor.SourceAmount) : processor.SourceAmount,
                Production = upgrades != null ? upgrades.GetServerProduction(sender, UpgradeJob.Process, processor.TargetAmount) : processor.TargetAmount,
                Duration = upgrades != null ? upgrades.GetServerActionDuration(sender, UpgradeJob.Process, processor.InteractionDuration) : processor.InteractionDuration
            };
            if (!serverResources.CanExchange(sender.ClientId, process.Source, process.Cost, process.Target, process.Production))
                return false;

            activeProcesses.Add(sender.ClientId, process);
            process.Routine = StartCoroutine(CompleteProcessAfterDelay(sender, process));
            return true;
        }

        private bool IsWithinProcessDistance(Vector3 playerPosition, Vector3 processorPosition)
        {
            float distance = Vector3.Distance(playerPosition, processorPosition);
            return !float.IsNaN(distance) && !float.IsInfinity(distance) && distance <= maximumInteractionDistance;
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestProcessCancelServerRpc(string processorId, uint requestId, NetworkConnection sender = null)
        {
            if (sender != null && activeProcesses.TryGetValue(sender.ClientId, out ActiveProcess process)
                && process.RequestId == requestId && process.Processor != null && process.Processor.ProcessorId == processorId)
                CancelServerProcess(sender.ClientId);
        }

        private void CancelServerProcess(int playerId)
        {
            if (activeProcesses.Remove(playerId, out ActiveProcess process) && process.Routine != null)
                StopCoroutine(process.Routine);
        }

        private IEnumerator CompleteProcessAfterDelay(NetworkConnection connection, ActiveProcess process)
        {
            string id = process.Processor.ProcessorId;
            float elapsed = 0f;
            while (elapsed < process.Duration)
            {
                yield return null;
                // Disabling/removing a processor interrupts its work even if it is re-enabled later.
                if (process.Processor == null || !process.Processor.ServerCanProcess)
                {
                    activeProcesses.Remove(connection.ClientId);
                    FinishProcessTargetRpc(connection, id, process.RequestId, false);
                    yield break;
                }
                elapsed += Time.deltaTime;
            }

            activeProcesses.Remove(connection.ClientId);
            if (!serverResources.TryExchange(connection.ClientId, process.Source, process.Cost,
                    process.Target, process.Production, out int sourceAmount, out int targetAmount))
            {
                FinishProcessTargetRpc(connection, id, process.RequestId, false);
                yield break;
            }

            UpdateWalletTargetRpc(connection, process.Source, sourceAmount);
            if (process.Source != process.Target)
                UpdateWalletTargetRpc(connection, process.Target, targetAmount);
            NetworkUpgradeService.Instance?.ServerGrantActionExperience(connection, UpgradeJob.Process);
            FinishProcessTargetRpc(connection, id, process.RequestId, true);
        }

        [TargetRpc]
        private void FinishProcessTargetRpc(NetworkConnection connection, string processorId, uint requestId, bool succeeded)
        {
            HandleProcessFinished(processorId, requestId, succeeded);
        }

        private void HandleProcessFinished(string processorId, uint requestId, bool succeeded)
        {
            if (localActiveProcessor == null || localActiveProcessor.ProcessorId != processorId || localProcessRequestId != requestId)
                return;
            ResourceProcessor processor = localActiveProcessor;
            localActiveProcessor = null;
            if (succeeded)
                processor.CompleteByServer();
            else
                processor.RejectByServer();
        }

        private void RebuildProcessorLookup()
        {
            processors.Clear();
            HashSet<string> duplicates = new();
            foreach (ResourceProcessor processor in FindObjectsByType<ResourceProcessor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string id = processor.ProcessorId;
                if (duplicates.Contains(id))
                    continue;
                if (processors.ContainsKey(id))
                {
                    processors.Remove(id);
                    duplicates.Add(id);
                    Debug.LogError($"Duplicate resource processor ID '{id}'. Assign a unique ID to each processor.", processor);
                }
                else
                    processors.Add(id, processor);
            }
        }
    }
}
