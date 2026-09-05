using System;
using System.Collections.Generic;

namespace TowerOfBabel.Upgrades
{
    [Serializable]
    public struct UpgradeJobSnapshot
    {
        public UpgradeJob Job;
        public int Level;
        public int Experience;
        public int AvailablePoints;
        public string[] PurchasedUpgradeIds;

        public UpgradeJobSnapshot(UpgradeJob job, int level, int experience, int availablePoints,
            string[] purchasedUpgradeIds)
        {
            Job = job;
            Level = level;
            Experience = experience;
            AvailablePoints = availablePoints;
            PurchasedUpgradeIds = purchasedUpgradeIds ?? Array.Empty<string>();
        }
    }

    public sealed class UpgradeJobProgress
    {
        private readonly HashSet<string> purchasedUpgradeIds = new(StringComparer.Ordinal);

        public UpgradeJob Job { get; }
        public int Level { get; private set; }
        public int Experience { get; private set; }
        public int AvailablePoints { get; private set; }
        public IReadOnlyCollection<string> PurchasedUpgradeIds => purchasedUpgradeIds;
        public int ExperienceRequired => GetExperienceRequired(Level);

        public UpgradeJobProgress(UpgradeJob job)
        {
            Job = job;
        }

        public static int GetExperienceRequired(int currentLevel)
        {
            return currentLevel >= UpgradeTreeConfig.MaxLevel ? 0 : (currentLevel + 1) * 10;
        }

        public bool GainExperience(int amount)
        {
            if (amount <= 0 || Level >= UpgradeTreeConfig.MaxLevel)
                return false;

            bool changed = false;
            for (int i = 0; i < amount && Level < UpgradeTreeConfig.MaxLevel; i++)
            {
                Experience++;
                changed = true;
                if (Experience < GetExperienceRequired(Level))
                    continue;

                Level++;
                Experience = 0;
                AvailablePoints++;
            }

            return changed;
        }

        public bool SetLevel(int level)
        {
            int clampedLevel = Math.Clamp(level, 0, UpgradeTreeConfig.MaxLevel);
            if (Level == clampedLevel && Experience == 0)
                return false;

            if (clampedLevel > Level)
                AvailablePoints += clampedLevel - Level;

            Level = clampedLevel;
            Experience = 0;
            return true;
        }

        public bool GrantUpgradePoints(int amount)
        {
            if (amount <= 0)
                return false;

            AvailablePoints = (int)Math.Min(int.MaxValue, (long)AvailablePoints + amount);
            return true;
        }

        public void Reset()
        {
            Level = 0;
            Experience = 0;
            AvailablePoints = 0;
            purchasedUpgradeIds.Clear();
        }

        public bool CanPurchase(UpgradeTreeConfig config, string upgradeId)
        {
            return AvailablePoints > 0 && !HasPurchased(upgradeId) &&
                   IsUpgradeRevealed(config, upgradeId);
        }

        public bool TryPurchase(UpgradeTreeConfig config, string upgradeId)
        {
            if (!CanPurchase(config, upgradeId))
                return false;

            purchasedUpgradeIds.Add(upgradeId);
            AvailablePoints--;
            return true;
        }

        public bool HasPurchased(string upgradeId)
        {
            return !string.IsNullOrEmpty(upgradeId) && purchasedUpgradeIds.Contains(upgradeId);
        }

        public bool IsUpgradeRevealed(UpgradeTreeConfig config, string upgradeId)
        {
            if (config == null || !config.TryGetUpgrade(Job, upgradeId, out UpgradeData upgrade))
                return false;
            if (HasPurchased(upgradeId))
                return true;

            if (upgrade.IsLevelFiftyUpgrade)
            {
                UpgradeData prerequisite = config.GetGridUpgrade(Job,
                    UpgradeTreeConfig.LevelFiftyPrerequisiteRow,
                    UpgradeTreeConfig.LevelFiftyPrerequisiteColumn);
                return prerequisite != null && HasPurchased(prerequisite.Id);
            }

            if (upgrade.Row == UpgradeTreeConfig.CenterCoordinate &&
                upgrade.Column == UpgradeTreeConfig.CenterCoordinate)
                return true;

            return IsPurchasedAt(config, upgrade.Row - 1, upgrade.Column) ||
                   IsPurchasedAt(config, upgrade.Row + 1, upgrade.Column) ||
                   IsPurchasedAt(config, upgrade.Row, upgrade.Column - 1) ||
                   IsPurchasedAt(config, upgrade.Row, upgrade.Column + 1);
        }

        public float GetEffectTotal(UpgradeTreeConfig config, UpgradeEffectType effectType)
        {
            if (config == null)
                return 0f;

            float total = 0f;
            IReadOnlyList<UpgradeData> upgrades = config.GetUpgrades(Job);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeData upgrade = upgrades[i];
                if (upgrade != null && upgrade.EffectType == effectType && HasPurchased(upgrade.Id))
                    total += upgrade.Value;
            }

            return total;
        }

        public UpgradeJobSnapshot CreateSnapshot()
        {
            string[] purchased = new string[purchasedUpgradeIds.Count];
            purchasedUpgradeIds.CopyTo(purchased);
            Array.Sort(purchased, StringComparer.Ordinal);
            return new UpgradeJobSnapshot(Job, Level, Experience, AvailablePoints, purchased);
        }

        public void ApplySnapshot(UpgradeJobSnapshot snapshot)
        {
            if (snapshot.Job != Job)
                return;

            Level = Math.Clamp(snapshot.Level, 0, UpgradeTreeConfig.MaxLevel);
            Experience = Level >= UpgradeTreeConfig.MaxLevel
                ? 0
                : Math.Clamp(snapshot.Experience, 0, Math.Max(0, GetExperienceRequired(Level) - 1));
            AvailablePoints = Math.Max(0, snapshot.AvailablePoints);
            purchasedUpgradeIds.Clear();
            if (snapshot.PurchasedUpgradeIds == null)
                return;

            for (int i = 0; i < snapshot.PurchasedUpgradeIds.Length; i++)
            {
                string id = snapshot.PurchasedUpgradeIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    purchasedUpgradeIds.Add(id);
            }
        }

        private bool IsPurchasedAt(UpgradeTreeConfig config, int row, int column)
        {
            if (row < 0 || row >= UpgradeTreeConfig.GridSize ||
                column < 0 || column >= UpgradeTreeConfig.GridSize)
                return false;

            UpgradeData adjacent = config.GetGridUpgrade(Job, row, column);
            return adjacent != null && HasPurchased(adjacent.Id);
        }
    }

    public sealed class PlayerUpgradeProgress
    {
        private readonly Dictionary<UpgradeJob, UpgradeJobProgress> jobs = new();

        public PlayerUpgradeProgress()
        {
            jobs.Add(UpgradeJob.Gather, new UpgradeJobProgress(UpgradeJob.Gather));
            jobs.Add(UpgradeJob.Process, new UpgradeJobProgress(UpgradeJob.Process));
            jobs.Add(UpgradeJob.Build, new UpgradeJobProgress(UpgradeJob.Build));
        }

        public UpgradeJobProgress Get(UpgradeJob job) => jobs[job];
    }
}
