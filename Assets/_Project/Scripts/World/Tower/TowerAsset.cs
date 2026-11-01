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
        GlassWaterFloor = 4,
        // Detail phase: released only after every structure asset on the floor is complete.
        Bush = 5
    }

    /// <summary>Each floor is built in two phases: the structure first, then the detail that dresses it.</summary>
    public enum TowerConstructionPhase : byte
    {
        Structure = 0,
        Detail = 1
    }

    [Serializable]
    public sealed class TowerAssetPrefabSet
    {
        [SerializeField] private GameObject floor;
        [SerializeField] private GameObject stair;
        [SerializeField] private GameObject pillar;
        [SerializeField] private GameObject arch;
        [SerializeField] private GameObject glassWaterFloor;
        [SerializeField] private GameObject bush;

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
                TowerAssetType.Bush => bush,
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
                case TowerAssetType.Bush:
                    bush = prefab;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
            }
        }
    }

    public static class TowerAssetTypeExtensions
    {
        public static bool IsFloor(this TowerAssetType type) => type == TowerAssetType.Floor || type == TowerAssetType.GlassWaterFloor;
        public static bool IsDetail(this TowerAssetType type) => type == TowerAssetType.Bush;
        public static TowerConstructionPhase Phase(this TowerAssetType type) =>
            type.IsDetail() ? TowerConstructionPhase.Detail : TowerConstructionPhase.Structure;
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
