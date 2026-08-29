using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    public enum TowerAssetType : byte
    {
        Floor = 0,
        Stair = 1,
        Pillar = 2,
        Arch = 3
    }

    /// <summary>Marks one generated tower asset root with its structural model type.</summary>
    [DisallowMultipleComponent]
    public sealed class TowerAsset : MonoBehaviour
    {
        [SerializeField] private TowerAssetType assetType;

        public TowerAssetType AssetType => assetType;

        public void SetAssetType(TowerAssetType value)
        {
            assetType = value;
        }
    }
}
