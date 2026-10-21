using UnityEngine;
using TowerOfBabel.Upgrades;

namespace TowerOfBabel.Interface
{
    /// <summary>Readable fixed-size skills inside a large, draggable tree canvas.</summary>
    public static class UpgradeBoardLayout
    {
        public static Vector2 TreeSize => new(2600,2200);
        public static Vector2 NodeSize => new(200,96);
        public static Vector2 TreePosition(UpgradeData data) => new(((data.IsLevelFiftyUpgrade ? 3 : data.Column)-3)*260,
            (3-(data.IsLevelFiftyUpgrade ? 7 : data.Row))*158);
    }
}
