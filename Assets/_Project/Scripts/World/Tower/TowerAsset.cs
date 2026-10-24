using System;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    public enum TowerAssetType : byte
    {
        Floor = 0,
        Stair = 1,
        Pillar = 2,
        Arch = 3,
        GlassWaterFloor = 4
    }

    [Serializable]
    public sealed class TowerAssetPrefabSet
    {
        [SerializeField] private GameObject floor;
        [SerializeField] private GameObject stair;
        [SerializeField] private GameObject pillar;
        [SerializeField] private GameObject arch;
        [SerializeField] private GameObject glassWaterFloor;

        public bool IsComplete => floor != null && stair != null && pillar != null && arch != null;

        public GameObject GetPrefab(TowerAssetType assetType)
        {
            return assetType switch
            {
                TowerAssetType.Floor => floor,
                TowerAssetType.Stair => stair,
                TowerAssetType.Pillar => pillar,
                TowerAssetType.Arch => arch,
                TowerAssetType.GlassWaterFloor => glassWaterFloor,
                _ => null
            };
        }

        public void SetPrefab(TowerAssetType assetType, GameObject prefab)
        {
            switch (assetType)
            {
                case TowerAssetType.Floor:
                    floor = prefab;
                    break;
                case TowerAssetType.Stair:
                    stair = prefab;
                    break;
                case TowerAssetType.Pillar:
                    pillar = prefab;
                    break;
                case TowerAssetType.Arch:
                    arch = prefab;
                    break;
                case TowerAssetType.GlassWaterFloor:
                    glassWaterFloor = prefab;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
            }
        }
    }

    public static class TowerAssetTypeExtensions
    {
        public static bool IsFloor(this TowerAssetType type) => type == TowerAssetType.Floor || type == TowerAssetType.GlassWaterFloor;
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
