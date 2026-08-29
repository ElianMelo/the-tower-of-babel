using System;
using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using UnityEngine;

namespace TowerOfBabel.World.Chunks
{
    /// <summary>Materializes the current 3x3x3 neighborhood from cached chunk data.</summary>
    [DisallowMultipleComponent]
    public sealed class NearTowerAssetPool : MonoBehaviour
    {
        [SerializeField] private Transform poolRoot;

        private readonly Dictionary<TowerAssetType, TypePool> pools = new();
        private readonly List<GameObject> activeAssets = new();
        private TowerAssetPrefabSet prefabs;

        public int ActiveInstanceCount => activeAssets.Count;
        public IReadOnlyList<GameObject> ActiveAssets => activeAssets;

        private void Awake()
        {
#if UNITY_SERVER
            enabled = false;
#else
            if (poolRoot == null)
                poolRoot = transform;
#endif
        }

        public void Configure(TowerAssetPrefabSet modelPrefabs)
        {
            if (modelPrefabs == null)
                throw new ArgumentNullException(nameof(modelPrefabs));
            if (ReferenceEquals(prefabs, modelPrefabs) && pools.Count > 0)
                return;

            DisposePools();
            prefabs = modelPrefabs;
            if (poolRoot == null)
                poolRoot = transform;

            CreateTypePool(TowerAssetType.Floor);
            CreateTypePool(TowerAssetType.Stair);
            CreateTypePool(TowerAssetType.Pillar);
            CreateTypePool(TowerAssetType.Arch);
        }

        public void BeginUpdate()
        {
            ClearVisibleAssets();
        }

        public void AddChunk(ChunkSceneCache chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            IReadOnlyList<ChunkAssetData> assets = chunk.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                ChunkAssetData asset = assets[i];
                if (!asset.UsesStageTenModel || !pools.TryGetValue(asset.AssetType, out TypePool pool))
                    continue;

                GameObject instance = pool.Rent();
                Transform instanceTransform = instance.transform;
                instanceTransform.SetPositionAndRotation(asset.Position, asset.Rotation);
                instanceTransform.localScale = WorldToLocalScale(asset.Scale, poolRoot.lossyScale);
                instance.SetActive(true);
                activeAssets.Add(instance);
            }
        }

        public void EndUpdate()
        {
        }

        public void ClearVisibleAssets()
        {
            for (int i = 0; i < activeAssets.Count; i++)
            {
                GameObject instance = activeAssets[i];
                if (instance == null)
                    continue;

                TowerAsset marker = instance.GetComponent<TowerAsset>();
                if (marker != null && pools.TryGetValue(marker.AssetType, out TypePool pool))
                    pool.Return(instance);
                else
                    instance.SetActive(false);
            }
            activeAssets.Clear();
        }

        public int GetCapacity(TowerAssetType assetType)
        {
            return pools.TryGetValue(assetType, out TypePool pool) ? pool.Capacity : 0;
        }

        private void CreateTypePool(TowerAssetType assetType)
        {
            GameObject prefab = prefabs.GetPrefab(assetType);
            if (prefab == null)
            {
                Debug.LogError($"Near tower pool has no prefab for {assetType}.", this);
                return;
            }
            pools.Add(assetType, new TypePool(assetType, prefab, poolRoot));
        }

        private void DisposePools()
        {
            activeAssets.Clear();
            foreach (TypePool pool in pools.Values)
                pool.Dispose();
            pools.Clear();
        }

        private void OnDestroy()
        {
            DisposePools();
        }

        private static Vector3 WorldToLocalScale(Vector3 worldScale, Vector3 parentScale)
        {
            return new Vector3(
                SafeDivide(worldScale.x, parentScale.x),
                SafeDivide(worldScale.y, parentScale.y),
                SafeDivide(worldScale.z, parentScale.z));
        }

        private static float SafeDivide(float value, float divisor)
        {
            return Mathf.Abs(divisor) <= 0.000001f ? value : value / divisor;
        }

        private sealed class TypePool : IDisposable
        {
            private readonly TowerAssetType assetType;
            private readonly GameObject prefab;
            private readonly Transform root;
            private readonly Stack<GameObject> available = new();
            private readonly List<GameObject> instances = new();

            public int Capacity => instances.Count;

            public TypePool(TowerAssetType assetType, GameObject prefab, Transform root)
            {
                this.assetType = assetType;
                this.prefab = prefab;
                this.root = root;
            }

            public GameObject Rent()
            {
                while (available.Count > 0)
                {
                    GameObject instance = available.Pop();
                    if (instance != null)
                        return instance;
                }

                GameObject created = Instantiate(prefab, root);
                created.name = $"Pooled {assetType}";
                TowerAsset marker = created.GetComponent<TowerAsset>();
                if (marker == null)
                    marker = created.AddComponent<TowerAsset>();
                marker.SetAssetType(assetType);
                created.SetActive(false);
                instances.Add(created);
                return created;
            }

            public void Return(GameObject instance)
            {
                instance.SetActive(false);
                available.Push(instance);
            }

            public void Dispose()
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    GameObject instance = instances[i];
                    if (instance == null)
                        continue;
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(instance);
                    else
                        UnityEngine.Object.DestroyImmediate(instance);
                }
                instances.Clear();
                available.Clear();
            }
        }
    }
}
