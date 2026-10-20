using UnityEngine;
using TowerOfBabel.Upgrades;

namespace TowerOfBabel.Interface
{
    /// <summary>Fits seven grid rows plus the capstone inside a board with a safe inset.</summary>
    public static class UpgradeBoardLayout
    {
        public static Vector2 CellSize(Rect available, float gap = 9f, float inset = 14f)
        {
            return new Vector2(
                Mathf.Max(1f, (available.width - inset * 2f - gap * 6f) / 7f),
                Mathf.Max(1f, (available.height - inset * 2f - gap * 7f) / 8f));
        }

        public static Vector2 Position(UpgradeData data, Rect available, Vector2 cell, float gap = 9f)
        {
            int row = data.IsLevelFiftyUpgrade ? 7 : data.Row;
            int column = data.IsLevelFiftyUpgrade ? UpgradeTreeConfig.CenterCoordinate : data.Column;
            return new Vector2((column - 3f) * (cell.x + gap), (3.5f - row) * (cell.y + gap));
        }
    }
}
