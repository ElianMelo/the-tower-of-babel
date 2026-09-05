using System;
using System.Text;
using IngameDebugConsole;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEngine;

[DisallowMultipleComponent]
public class CheatUpgrades : MonoBehaviour
{
    private bool commandsRegistered;

    private void OnEnable()
    {
        RegisterCommands();
    }

    private void OnDisable()
    {
        UnregisterCommands();
    }

    private void RegisterCommands()
    {
        if (commandsRegistered)
            return;

        DebugLogConsole.AddCommand<string, int>("upgrade-xp",
            "Grant XP to a job. Jobs: gather, process, build.", UpgradeExperience, "job", "amount");
        DebugLogConsole.AddCommand<string, int>("upgrade-level",
            "Set a job to an absolute level (0-50). Higher levels grant one point each.",
            UpgradeLevel, "job", "level");
        DebugLogConsole.AddCommand<string, int>("upgrade-points",
            "Grant upgrade points to a job.", UpgradePoints, "job", "amount");
        DebugLogConsole.AddCommand<string, string>("upgrade-buy",
            "Buy an upgrade by ID using normal purchase rules.", UpgradeBuyById, "job", "upgrade-id");
        DebugLogConsole.AddCommand<string, int, int>("upgrade-buy",
            "Buy a grid upgrade using normal purchase rules.", UpgradeBuyAt, "job", "row", "column");
        DebugLogConsole.AddCommand<string>("upgrade-reset",
            "Reset one job or all jobs. Jobs: gather, process, build, all.", UpgradeReset, "job-or-all");
        DebugLogConsole.AddCommand<string>("upgrade-max",
            "Set a job to level 50.", UpgradeMax, "job");
        DebugLogConsole.AddCommand("upgrade-status",
            "Print upgrade status for every job.", UpgradeStatus);
        DebugLogConsole.AddCommand<string>("upgrade-status",
            "Print upgrade status for one job.", UpgradeStatusForJob, "job");

        commandsRegistered = true;
    }

    private void UnregisterCommands()
    {
        if (!commandsRegistered)
            return;

        foreach (string command in new[]
                 {
                     "upgrade-xp", "upgrade-level", "upgrade-points", "upgrade-buy",
                     "upgrade-reset", "upgrade-max", "upgrade-status"
                 })
        {
            DebugLogConsole.RemoveCommand(command);
        }

        commandsRegistered = false;
    }

    private void UpgradeExperience(string jobName, int amount)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job) ||
            amount <= 0)
        {
            if (amount <= 0)
                Debug.LogWarning("[Upgrade Cheat] XP amount must be greater than zero.");
            return;
        }

        ReportRequest(service.RequestCheatExperience(job, amount));
    }

    private void UpgradeLevel(string jobName, int level)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job))
            return;

        if (level < 0 || level > UpgradeTreeConfig.MaxLevel)
        {
            Debug.LogWarning($"[Upgrade Cheat] Level must be between 0 and {UpgradeTreeConfig.MaxLevel}.");
            return;
        }

        ReportRequest(service.RequestCheatLevel(job, level));
    }

    private void UpgradePoints(string jobName, int amount)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job) ||
            amount <= 0)
        {
            if (amount <= 0)
                Debug.LogWarning("[Upgrade Cheat] Point amount must be greater than zero.");
            return;
        }

        ReportRequest(service.RequestCheatPoints(job, amount));
    }

    private void UpgradeBuyById(string jobName, string upgradeId)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job))
            return;

        if (string.IsNullOrWhiteSpace(upgradeId))
        {
            Debug.LogWarning("[Upgrade Cheat] Upgrade ID cannot be empty.");
            return;
        }

        ReportRequest(service.RequestCheatPurchase(job, upgradeId));
    }

    private void UpgradeBuyAt(string jobName, int row, int column)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job))
            return;

        if (row < 0 || row >= UpgradeTreeConfig.GridSize ||
            column < 0 || column >= UpgradeTreeConfig.GridSize)
        {
            Debug.LogWarning("[Upgrade Cheat] Row and column must be between 0 and 6.");
            return;
        }

        ReportRequest(service.RequestCheatPurchase(job, row, column));
    }

    private void UpgradeReset(string jobName)
    {
        NetworkUpgradeService service = GetService();
        if (service == null)
            return;

        if (string.Equals(jobName, "all", StringComparison.OrdinalIgnoreCase))
        {
            ReportRequest(service.RequestCheatResetAll());
            return;
        }

        if (!TryParseJob(jobName, out UpgradeJob job))
            return;

        ReportRequest(service.RequestCheatReset(job));
    }

    private void UpgradeMax(string jobName)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job))
            return;

        ReportRequest(service.RequestCheatLevel(job, UpgradeTreeConfig.MaxLevel));
    }

    private void UpgradeStatus()
    {
        NetworkUpgradeService service = GetService();
        if (service == null)
            return;

        StringBuilder status = new("[Upgrade Cheat] Current progression:");
        AppendStatus(status, service, UpgradeJob.Gather);
        AppendStatus(status, service, UpgradeJob.Process);
        AppendStatus(status, service, UpgradeJob.Build);
        Debug.Log(status.ToString());
    }

    private void UpgradeStatusForJob(string jobName)
    {
        if (!TryGetServiceAndJob(jobName, out NetworkUpgradeService service, out UpgradeJob job))
            return;

        StringBuilder status = new("[Upgrade Cheat] Current progression:");
        AppendStatus(status, service, job);
        Debug.Log(status.ToString());
    }

    private static void AppendStatus(StringBuilder status, NetworkUpgradeService service,
        UpgradeJob job)
    {
        UpgradeJobSnapshot snapshot = service.GetLocalSnapshot(job);
        int requiredExperience = UpgradeJobProgress.GetExperienceRequired(snapshot.Level);
        float efficiency = service.GetLocalEffect(job, UpgradeEffectType.Efficiency);
        float cost = service.GetLocalEffect(job, UpgradeEffectType.Cost);
        float production = service.GetLocalEffect(job, UpgradeEffectType.Production);

        status.Append("\n  ").Append(job)
            .Append(": level ").Append(snapshot.Level)
            .Append(", XP ").Append(snapshot.Experience).Append('/')
            .Append(requiredExperience > 0 ? requiredExperience.ToString() : "MAX")
            .Append(", points ").Append(snapshot.AvailablePoints)
            .Append(", purchased ").Append(snapshot.PurchasedUpgradeIds?.Length ?? 0)
            .Append(", effects [efficiency ").Append(efficiency)
            .Append(", cost ").Append(cost)
            .Append(", production ").Append(production).Append(']');
    }

    private static bool TryGetServiceAndJob(string jobName, out NetworkUpgradeService service,
        out UpgradeJob job)
    {
        service = GetService();
        job = default;
        return service != null && TryParseJob(jobName, out job);
    }

    private static NetworkUpgradeService GetService()
    {
        NetworkUpgradeService service = NetworkUpgradeService.Instance;
        if (service == null)
            Debug.LogWarning("[Upgrade Cheat] NetworkUpgradeService is not available.");
        return service;
    }

    private static bool TryParseJob(string jobName, out UpgradeJob job)
    {
        if (Enum.TryParse(jobName, true, out job) &&
            job is UpgradeJob.Gather or UpgradeJob.Process or UpgradeJob.Build)
            return true;

        Debug.LogWarning($"[Upgrade Cheat] Unknown job '{jobName}'. Use gather, process, or build.");
        return false;
    }

    private static void ReportRequest(bool sent)
    {
        if (!sent)
            Debug.LogWarning("[Upgrade Cheat] Command requires an active client connection and valid arguments.");
    }
}
