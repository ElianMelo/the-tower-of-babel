using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Upgrades
{
    [CreateAssetMenu(fileName = "UpgradeTreeConfig", menuName = "Tower of Babel/Upgrades/Tree Config")]
    public sealed class UpgradeTreeConfig : ScriptableObject
    {
        public const int GridSize = 7;
        public const int MaxLevel = 50;
        public const int CenterCoordinate = 3;
        public const int LevelFiftyPrerequisiteRow = 6;
        public const int LevelFiftyPrerequisiteColumn = 3;

        [SerializeField] private List<UpgradeBoardData> boards = new();

        public IReadOnlyList<UpgradeBoardData> Boards => boards;

        public IReadOnlyList<UpgradeData> GetUpgrades(UpgradeJob job)
        {
            UpgradeBoardData board = FindBoard(job);
            return board != null ? board.Upgrades : Array.Empty<UpgradeData>();
        }

        public bool TryGetUpgrade(UpgradeJob job, string upgradeId, out UpgradeData upgrade)
        {
            upgrade = null;
            if (string.IsNullOrWhiteSpace(upgradeId))
                return false;

            IReadOnlyList<UpgradeData> upgrades = GetUpgrades(job);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeData candidate = upgrades[i];
                if (candidate != null && string.Equals(candidate.Id, upgradeId, StringComparison.Ordinal))
                {
                    upgrade = candidate;
                    return true;
                }
            }

            return false;
        }

        public UpgradeData GetGridUpgrade(UpgradeJob job, int row, int column)
        {
            IReadOnlyList<UpgradeData> upgrades = GetUpgrades(job);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeData candidate = upgrades[i];
                if (candidate != null && !candidate.IsLevelFiftyUpgrade &&
                    candidate.Row == row && candidate.Column == column)
                    return candidate;
            }

            return null;
        }

        public UpgradeData GetLevelFiftyUpgrade(UpgradeJob job)
        {
            IReadOnlyList<UpgradeData> upgrades = GetUpgrades(job);
            for (int i = 0; i < upgrades.Count; i++)
            {
                UpgradeData candidate = upgrades[i];
                if (candidate != null && candidate.IsLevelFiftyUpgrade)
                    return candidate;
            }

            return null;
        }

        public void ReplaceBoards(List<UpgradeBoardData> newBoards)
        {
            boards = newBoards ?? new List<UpgradeBoardData>();
        }

        private UpgradeBoardData FindBoard(UpgradeJob job)
        {
            for (int i = 0; i < boards.Count; i++)
            {
                UpgradeBoardData board = boards[i];
                if (board != null && board.Job == job)
                    return board;
            }

            return null;
        }

        private void OnValidate()
        {
            if (boards == null)
                boards = new List<UpgradeBoardData>();
        }
    }
}
