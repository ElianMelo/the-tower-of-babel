using System;
using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Chunks
{
    [Serializable]
    public sealed class TowerAssetRenderModel
    {
        [SerializeField] private TowerAssetType assetType;
        [SerializeField] private GameObject prefab;

        public TowerAssetType AssetType => assetType;
        public GameObject Prefab => prefab;
    }

    /// <summary>Renders stage-10 far tower assets in prefab-backed GPU instance batches.</summary>
    [DisallowMultipleComponent]
    public sealed class InstancedFarChunkRenderer : MonoBehaviour, IFarChunkRenderer
    {
        private const int MaximumInstancesPerDraw = 1023;

        [Tooltip("One stage-10 model prefab for every tower asset type.")]
        [SerializeField] private List<TowerAssetRenderModel> models = new();
        [Tooltip("Fallback source for the four model prefabs when Models is empty.")]
        [SerializeField] private global::TowerGenerator modelProvider;

        private readonly Dictionary<TowerAssetType, TypeBatch> batches = new();
        private readonly HashSet<ChunkKey> loadedChunks = new();
        private bool isVisible = true;

        public int LoadedChunkCount => loadedChunks.Count;

        public int LoadedInstanceCount
        {
            get
            {
                int count = 0;
                foreach (TypeBatch batch in batches.Values)
                    count += batch.Count;
                return count;
            }
        }

        private void OnEnable()
        {
#if UNITY_SERVER
            enabled = false;
            return;
#else
            EnsureModelBatches();
#endif
        }

        private void LateUpdate()
        {
            if (!isVisible)
                return;

            foreach (TypeBatch batch in batches.Values)
                batch.Render(gameObject.layer);
        }

        public void LoadChunk(in FarChunkSnapshot snapshot)
        {
            EnsureModelBatches();
            RemoveChunk(snapshot.Key);
            loadedChunks.Add(snapshot.Key);

            IReadOnlyList<FarChunkAsset> assets = snapshot.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                FarChunkAsset asset = assets[i];
                if (batches.TryGetValue(asset.AssetType, out TypeBatch batch))
                    batch.Add(snapshot.Key, asset.ObjectToWorld);
            }
        }

        public void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot)
        {
            // The first implementation deliberately renders the authored stage-10 model.
            // This hook keeps construction-stage selection behind the renderer boundary.
        }

        public void RemoveChunk(ChunkKey key)
        {
            if (!loadedChunks.Remove(key))
                return;

            foreach (TypeBatch batch in batches.Values)
                batch.RemoveChunk(key);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
        }

        private void BuildModelBatches()
        {
            DisposeBatches();
            if (models.Count > 0)
            {
                for (int i = 0; i < models.Count; i++)
                {
                    TowerAssetRenderModel model = models[i];
                    if (model != null)
                        AddModelBatch(model.AssetType, model.Prefab);
                }
                return;
            }

            if (modelProvider == null)
                modelProvider = FindFirstObjectByType<global::TowerGenerator>();
            if (modelProvider == null)
            {
                Debug.LogError("Far chunk rendering needs explicit Models or a TowerGenerator model provider.", this);
                return;
            }

            AddModelBatch(TowerAssetType.Floor, modelProvider.GetModelPrefab(TowerAssetType.Floor));
            AddModelBatch(TowerAssetType.Stair, modelProvider.GetModelPrefab(TowerAssetType.Stair));
            AddModelBatch(TowerAssetType.Pillar, modelProvider.GetModelPrefab(TowerAssetType.Pillar));
            AddModelBatch(TowerAssetType.Arch, modelProvider.GetModelPrefab(TowerAssetType.Arch));
        }

        private void EnsureModelBatches()
        {
            if (batches.Count == 0)
                BuildModelBatches();
        }

        private void AddModelBatch(TowerAssetType assetType, GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"No stage-10 prefab is assigned for far {assetType} rendering.", this);
                return;
            }
            if (batches.ContainsKey(assetType))
            {
                Debug.LogWarning($"Duplicate far render model for {assetType}; the later entry was ignored.", this);
                return;
            }

            MeshFilter meshFilter = prefab.GetComponent<MeshFilter>();
            MeshRenderer meshRenderer = prefab.GetComponent<MeshRenderer>();
            if (meshFilter == null || meshFilter.sharedMesh == null ||
                meshRenderer == null || meshRenderer.sharedMaterial == null)
            {
                Debug.LogError(
                    $"Far render model {prefab.name} for {assetType} needs a root MeshFilter and MeshRenderer.",
                    this);
                return;
            }

            batches.Add(assetType, new TypeBatch(meshFilter.sharedMesh, meshRenderer));
        }

        private void DisposeBatches()
        {
            foreach (TypeBatch batch in batches.Values)
                batch.Dispose();
            batches.Clear();
            loadedChunks.Clear();
        }

        private void OnDisable()
        {
            DisposeBatches();
        }

        private void OnDestroy()
        {
            DisposeBatches();
        }

        private sealed class TypeBatch : IDisposable
        {
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly bool ownsMaterial;
            private readonly ShadowCastingMode shadowCastingMode;
            private readonly bool receiveShadows;
            private readonly uint renderingLayerMask;
            private readonly List<Matrix4x4> matrices = new();
            private readonly List<ChunkKey> owners = new();
            private readonly Dictionary<ChunkKey, HashSet<int>> indicesByChunk = new();

            public int Count => matrices.Count;

            public TypeBatch(Mesh mesh, MeshRenderer sourceRenderer)
            {
                this.mesh = mesh;
                shadowCastingMode = sourceRenderer.shadowCastingMode;
                receiveShadows = sourceRenderer.receiveShadows;
                renderingLayerMask = sourceRenderer.renderingLayerMask;

                Material sourceMaterial = sourceRenderer.sharedMaterial;
                if (sourceMaterial.enableInstancing)
                {
                    material = sourceMaterial;
                    ownsMaterial = false;
                }
                else
                {
                    material = new Material(sourceMaterial)
                    {
                        name = $"{sourceMaterial.name} (Far Instanced)",
                        enableInstancing = true,
                        hideFlags = HideFlags.DontSave
                    };
                    ownsMaterial = true;
                }
            }

            public void Add(ChunkKey owner, Matrix4x4 matrix)
            {
                if (!indicesByChunk.TryGetValue(owner, out HashSet<int> indices))
                {
                    indices = new HashSet<int>();
                    indicesByChunk.Add(owner, indices);
                }

                int index = matrices.Count;
                matrices.Add(matrix);
                owners.Add(owner);
                indices.Add(index);
            }

            public void RemoveChunk(ChunkKey owner)
            {
                if (!indicesByChunk.TryGetValue(owner, out HashSet<int> indices))
                    return;

                while (indices.Count > 0)
                {
                    int removeIndex = First(indices);
                    indices.Remove(removeIndex);
                    int lastIndex = matrices.Count - 1;
                    if (removeIndex != lastIndex)
                    {
                        ChunkKey swappedOwner = owners[lastIndex];
                        matrices[removeIndex] = matrices[lastIndex];
                        owners[removeIndex] = swappedOwner;

                        HashSet<int> swappedIndices = indicesByChunk[swappedOwner];
                        swappedIndices.Remove(lastIndex);
                        swappedIndices.Add(removeIndex);
                    }

                    matrices.RemoveAt(lastIndex);
                    owners.RemoveAt(lastIndex);
                }

                indicesByChunk.Remove(owner);
            }

            public void Render(int layer)
            {
                if (matrices.Count == 0)
                    return;

                RenderParams renderParams = new(material)
                {
                    layer = layer,
                    shadowCastingMode = shadowCastingMode,
                    receiveShadows = receiveShadows,
                    renderingLayerMask = renderingLayerMask
                };

                for (int start = 0; start < matrices.Count; start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(MaximumInstancesPerDraw, matrices.Count - start);
                    Graphics.RenderMeshInstanced(renderParams, mesh, 0, matrices, count, start);
                }
            }

            public void Dispose()
            {
                if (!ownsMaterial || material == null)
                    return;

                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }

            private static int First(HashSet<int> values)
            {
                foreach (int value in values)
                    return value;
                return -1;
            }
        }
    }
}
