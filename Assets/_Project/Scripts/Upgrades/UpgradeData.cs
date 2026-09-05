using System;
using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Upgrades
{
    public enum UpgradeJob : byte
    {
        Gather,
        Process,
        Build
    }

    public enum UpgradeEffectType : byte
    {
        Efficiency,
        Cost,
        Production
    }

    [Serializable]
    public sealed class UpgradeData
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Range(0, UpgradeTreeConfig.GridSize - 1)] private int row;
        [SerializeField, Range(0, UpgradeTreeConfig.GridSize - 1)] private int column;
        [SerializeField] private bool isLevelFiftyUpgrade;
        [SerializeField] private UpgradeEffectType effectType;
        [SerializeField] private float value;

        public string Id => id;
        public string DisplayName => displayName;
        public int Row => row;
        public int Column => column;
        public bool IsLevelFiftyUpgrade => isLevelFiftyUpgrade;
        public UpgradeEffectType EffectType => effectType;
        public float Value => value;

        public UpgradeData(string id, string displayName, int row, int column,
            UpgradeEffectType effectType, float value, bool isLevelFiftyUpgrade = false)
        {
            this.id = id;
            this.displayName = displayName;
            this.row = row;
            this.column = column;
            this.effectType = effectType;
            this.value = value;
            this.isLevelFiftyUpgrade = isLevelFiftyUpgrade;
        }
    }

    [Serializable]
    public sealed class UpgradeBoardData
    {
        [SerializeField] private UpgradeJob job;
        [SerializeField] private List<UpgradeData> upgrades = new();

        public UpgradeJob Job => job;
        public IReadOnlyList<UpgradeData> Upgrades => upgrades;

        public UpgradeBoardData(UpgradeJob job, List<UpgradeData> upgrades)
        {
            this.job = job;
            this.upgrades = upgrades ?? new List<UpgradeData>();
        }
    }

}
