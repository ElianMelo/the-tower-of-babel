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
        // Twelve procedural vine shapes on a pillar and its arches: climbing, draped, dense, sparse, pillar-only and arch-only.
        Vine0 = 5,
        Vine1 = 6,
        Vine2 = 7,
        Vine3 = 8,
        Vine4 = 9,
        Vine5 = 10,
        Vine6 = 11,
        Vine7 = 12,
        Vine8 = 13,
        Vine9 = 14,
        Vine10 = 15,
        Vine11 = 16
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
        [Tooltip("One prefab per detail vine shape, in TowerAssetType order.")]
        [SerializeField] private GameObject[] vines = new GameObject[TowerAssetTypeExtensions.DetailCount];

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
                _ when assetType.IsDetail() => vines != null && assetType.DetailIndex() < vines.Length ? vines[assetType.DetailIndex()] : null,
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
                case var detail when detail.IsDetail():
                    if (vines == null || vines.Length < TowerAssetTypeExtensions.DetailCount)
                        Array.Resize(ref vines, TowerAssetTypeExtensions.DetailCount);
                    vines[detail.DetailIndex()] = prefab;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(assetType), assetType, null);
            }
        }
    }

    public static class TowerAssetTypeExtensions
    {
        public static bool IsFloor(this TowerAssetType type) => type == TowerAssetType.Floor || type == TowerAssetType.GlassWaterFloor;
        public const int DetailCount = TowerAssetType.Vine11 - TowerAssetType.Vine0 + 1;
        public static readonly TowerAssetType[] DetailTypes =
        {
            TowerAssetType.Vine0, TowerAssetType.Vine1, TowerAssetType.Vine2, TowerAssetType.Vine3,
            TowerAssetType.Vine4, TowerAssetType.Vine5, TowerAssetType.Vine6, TowerAssetType.Vine7,
            TowerAssetType.Vine8, TowerAssetType.Vine9, TowerAssetType.Vine10, TowerAssetType.Vine11
        };
        public static bool IsDetail(this TowerAssetType type) => type >= TowerAssetType.Vine0 && type <= TowerAssetType.Vine11;
        public static int DetailIndex(this TowerAssetType type) => type - TowerAssetType.Vine0;
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
