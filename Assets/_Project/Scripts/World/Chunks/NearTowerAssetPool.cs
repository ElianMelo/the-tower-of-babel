using System;
using System.Collections.Generic;
using TowerOfBabel.World.Tower;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Chunks
{
    /// <summary>Materializes and incrementally updates the current 3x3x3 neighborhood.</summary>
    [DisallowMultipleComponent]
    public sealed class NearTowerAssetPool : MonoBehaviour
    {
        private const int ParallelTransformThreshold = 128;

        private static readonly ProfilerMarker SynchronizeMarker =
            new("Tower.NearPool.Synchronize");
        private static readonly ProfilerMarker TransformMarker =
            new("Tower.NearPool.ApplyTransforms");

        [SerializeField] private Transform poolRoot;

        private readonly Dictionary<TowerAssetType, TypePool> pools = new();
        private readonly Dictionary<ChunkKey, ChunkLease> activeChunks = new();
        private readonly Dictionary<ChunkKey, ChunkSceneCache> desiredChunks = new();
        private readonly Dictionary<GameObject, PooledInstance> activeByObject = new();
        private readonly List<GameObject> activeAssets = new();
        private readonly List<ChunkKey> chunksToRelease = new(ChunkManager.NearChunkSlotCount);
        private readonly List<PendingActivation> pendingActivations = new();
        private TowerAssetPrefabSet prefabs;

        public int ActiveInstanceCount => activeAssets.Count;
        public int ActiveChunkCount => activeChunks.Count;
        public int LastAddedChunkCount { get; private set; }
        public int LastRemovedChunkCount { get; private set; }
        public int LastPositionedAssetCount { get; private set; }
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
            desiredChunks.Clear();
        }

        public void AddChunk(ChunkSceneCache chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            desiredChunks[chunk.Key] = chunk;
        }

        public void EndUpdate()
        {
            using (SynchronizeMarker.Auto())
            {
                LastAddedChunkCount = 0;
                LastRemovedChunkCount = 0;
                LastPositionedAssetCount = 0;
                chunksToRelease.Clear();

                foreach (KeyValuePair<ChunkKey, ChunkLease> pair in activeChunks)
                {
                    if (!desiredChunks.ContainsKey(pair.Key))
                        chunksToRelease.Add(pair.Key);
                }

                for (int i = 0; i < chunksToRelease.Count; i++)
                {
                    ReleaseChunk(chunksToRelease[i], true);
                    LastRemovedChunkCount++;
                }

                pendingActivations.Clear();
                foreach (KeyValuePair<ChunkKey, ChunkSceneCache> pair in desiredChunks)
                {
                    if (activeChunks.ContainsKey(pair.Key))
                        continue;

                    AddNewChunk(pair.Value);
                    LastAddedChunkCount++;
                }

                ApplyPendingTransforms();
                foreach (TypePool pool in pools.Values)
                    pool.FlushDeferredReturns();
                desiredChunks.Clear();
            }
        }

        public bool ApplyAssetStage(ChunkSceneCache chunk, int localIndex)
        {
            if (chunk == null || !activeChunks.TryGetValue(chunk.Key, out ChunkLease lease) ||
                (uint)localIndex >= (uint)lease.Assets.Length ||
                (uint)localIndex >= (uint)chunk.Assets.Count)
                return false;

            ChunkAssetData asset = chunk.Assets[localIndex];
            PooledInstance current = lease.Assets[localIndex];
            if (!asset.UsesStageTenModel)
            {
                if (current != null)
                {
                    ReturnInstance(current, false);
                    lease.Assets[localIndex] = null;
                }
                return true;
            }

            if (current == null)
            {
                if (!pools.TryGetValue(asset.AssetType, out TypePool pool))
                    return false;
                current = pool.Rent();
                lease.Assets[localIndex] = current;
            }

            ApplyTransform(current.GameObject.transform, asset);
            ActivateInstance(current);
            return true;
        }

        public void ClearVisibleAssets()
        {
            chunksToRelease.Clear();
            foreach (ChunkKey key in activeChunks.Keys)
                chunksToRelease.Add(key);
            for (int i = 0; i < chunksToRelease.Count; i++)
                ReleaseChunk(chunksToRelease[i], false);

            desiredChunks.Clear();
            pendingActivations.Clear();
            activeAssets.Clear();
            activeByObject.Clear();
        }

        public int GetCapacity(TowerAssetType assetType)
        {
            return pools.TryGetValue(assetType, out TypePool pool) ? pool.Capacity : 0;
        }

        private void AddNewChunk(ChunkSceneCache chunk)
        {
            ChunkLease lease = new(chunk.Assets.Count);
            activeChunks.Add(chunk.Key, lease);

            IReadOnlyList<ChunkAssetData> assets = chunk.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                ChunkAssetData asset = assets[i];
                if (!asset.UsesStageTenModel || !pools.TryGetValue(asset.AssetType, out TypePool pool))
                    continue;

                PooledInstance instance = pool.Rent();
                lease.Assets[i] = instance;
                pendingActivations.Add(new PendingActivation(instance, asset));
            }
        }

        private void ReleaseChunk(ChunkKey key, bool deferDisable)
        {
            if (!activeChunks.Remove(key, out ChunkLease lease))
                return;

            for (int i = 0; i < lease.Assets.Length; i++)
            {
                PooledInstance instance = lease.Assets[i];
                if (instance != null)
                    ReturnInstance(instance, deferDisable);
            }
        }

        private void ApplyPendingTransforms()
        {
            int count = pendingActivations.Count;
            if (count == 0)
                return;

            using (TransformMarker.Auto())
            {
                if (Application.isPlaying && count >= ParallelTransformThreshold)
                    ApplyTransformsParallel();
                else
                {
                    for (int i = 0; i < count; i++)
                    {
                        PendingActivation activation = pendingActivations[i];
                        ApplyTransform(activation.Instance.GameObject.transform, activation.Asset);
                    }
                }

                for (int i = 0; i < count; i++)
                    ActivateInstance(pendingActivations[i].Instance);
            }

            LastPositionedAssetCount = count;
            pendingActivations.Clear();
        }

        private void ApplyTransformsParallel()
        {
            int count = pendingActivations.Count;
            NativeArray<Vector3> positions = new(count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<Quaternion> rotations = new(count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            NativeArray<Vector3> scales = new(count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);
            TransformAccessArray transforms = new(count);
            try
            {
                Vector3 parentScale = poolRoot.lossyScale;
                for (int i = 0; i < count; i++)
                {
                    PendingActivation activation = pendingActivations[i];
                    positions[i] = activation.Asset.Position;
                    rotations[i] = activation.Asset.Rotation;
                    scales[i] = WorldToLocalScale(activation.Asset.Scale, parentScale);
                    transforms.Add(activation.Instance.GameObject.transform);
                }

                ApplyTowerTransformsJob job = new()
                {
                    Positions = positions,
                    Rotations = rotations,
                    LocalScales = scales
                };
                JobHandle handle = job.Schedule(transforms);
                handle.Complete();
            }
            finally
            {
                transforms.Dispose();
                scales.Dispose();
                rotations.Dispose();
                positions.Dispose();
            }
        }

        private void ActivateInstance(PooledInstance instance)
        {
            if (instance.ActiveIndex >= 0)
                return;

            instance.ActiveIndex = activeAssets.Count;
            activeAssets.Add(instance.GameObject);
            activeByObject.Add(instance.GameObject, instance);
            if (!instance.GameObject.activeSelf)
                instance.GameObject.SetActive(true);
        }

        private void ReturnInstance(PooledInstance instance, bool deferDisable)
        {
            int removeIndex = instance.ActiveIndex;
            if (removeIndex >= 0)
            {
                int lastIndex = activeAssets.Count - 1;
                GameObject lastObject = activeAssets[lastIndex];
                activeAssets[removeIndex] = lastObject;
                activeAssets.RemoveAt(lastIndex);
                activeByObject.Remove(instance.GameObject);
                if (removeIndex != lastIndex && activeByObject.TryGetValue(lastObject, out PooledInstance swapped))
                    swapped.ActiveIndex = removeIndex;
                instance.ActiveIndex = -1;
            }

            instance.Owner.Return(instance, deferDisable);
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
            ClearVisibleAssets();
            foreach (TypePool pool in pools.Values)
                pool.Dispose();
            pools.Clear();
        }

        private void OnDestroy()
        {
            DisposePools();
        }

        private void ApplyTransform(Transform instanceTransform, ChunkAssetData asset)
        {
            instanceTransform.SetPositionAndRotation(asset.Position, asset.Rotation);
            instanceTransform.localScale = WorldToLocalScale(asset.Scale, poolRoot.lossyScale);
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

        [BurstCompile]
        private struct ApplyTowerTransformsJob : IJobParallelForTransform
        {
            [ReadOnly] public NativeArray<Vector3> Positions;
            [ReadOnly] public NativeArray<Quaternion> Rotations;
            [ReadOnly] public NativeArray<Vector3> LocalScales;

            public void Execute(int index, TransformAccess transform)
            {
                transform.position = Positions[index];
                transform.rotation = Rotations[index];
                transform.localScale = LocalScales[index];
            }
        }

        private sealed class ChunkLease
        {
            public readonly PooledInstance[] Assets;

            public ChunkLease(int assetCount)
            {
                Assets = new PooledInstance[assetCount];
            }
        }

        private readonly struct PendingActivation
        {
            public PooledInstance Instance { get; }
            public ChunkAssetData Asset { get; }

            public PendingActivation(PooledInstance instance, ChunkAssetData asset)
            {
                Instance = instance;
                Asset = asset;
            }
        }

        private sealed class PooledInstance
        {
            public GameObject GameObject { get; }
            public TypePool Owner { get; }
            public int ActiveIndex { get; set; } = -1;
            public bool HasDeferredReturn { get; set; }

            public PooledInstance(GameObject gameObject, TypePool owner)
            {
                GameObject = gameObject;
                Owner = owner;
            }
        }

        private sealed class TypePool : IDisposable
        {
            private readonly TowerAssetType assetType;
            private readonly GameObject prefab;
            private readonly Transform root;
            private readonly Stack<PooledInstance> available = new();
            private readonly List<PooledInstance> instances = new();
            private readonly List<PooledInstance> deferredReturns = new();
            private readonly Dictionary<Material, Material> runtimeMaterials = new();

            public int Capacity => instances.Count;

            public TypePool(TowerAssetType assetType, GameObject prefab, Transform root)
            {
                this.assetType = assetType;
                this.prefab = prefab;
                this.root = root;
            }

            public PooledInstance Rent()
            {
                while (available.Count > 0)
                {
                    PooledInstance instance = available.Pop();
                    if (instance.GameObject != null)
                    {
                        instance.HasDeferredReturn = false;
                        return instance;
                    }
                }

                GameObject created = Instantiate(prefab, root);
                created.name = $"Pooled {assetType}";
                TowerAsset marker = created.GetComponent<TowerAsset>();
                if (marker == null)
                    marker = created.AddComponent<TowerAsset>();
                marker.SetAssetType(assetType);
                ConfigureRenderers(created);
                created.SetActive(false);

                PooledInstance pooled = new(created, this);
                instances.Add(pooled);
                return pooled;
            }

            public void Return(PooledInstance instance, bool deferDisable)
            {
                available.Push(instance);
                if (deferDisable)
                {
                    instance.HasDeferredReturn = true;
                    deferredReturns.Add(instance);
                }
                else
                {
                    instance.HasDeferredReturn = false;
                    instance.GameObject.SetActive(false);
                }
            }

            public void FlushDeferredReturns()
            {
                for (int i = 0; i < deferredReturns.Count; i++)
                {
                    PooledInstance instance = deferredReturns[i];
                    if (!instance.HasDeferredReturn || instance.GameObject == null)
                        continue;
                    instance.GameObject.SetActive(false);
                    instance.HasDeferredReturn = false;
                }
                deferredReturns.Clear();
            }

            private void ConfigureRenderers(GameObject instance)
            {
                MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>(true);
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    MeshRenderer renderer = renderers[rendererIndex];
                    Material[] materials = renderer.sharedMaterials;
                    for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                    {
                        Material source = materials[materialIndex];
                        if (source == null)
                            continue;
                        if (!runtimeMaterials.TryGetValue(source, out Material runtime))
                        {
                            runtime = new Material(source)
                            {
                                name = $"{source.name} ({assetType} Near Instanced)",
                                enableInstancing = true,
                                hideFlags = HideFlags.DontSave
                            };
                            if (runtime.HasProperty("_ReceiveShadows"))
                                runtime.SetFloat("_ReceiveShadows", 0f);
                            runtime.EnableKeyword("_RECEIVE_SHADOWS_OFF");
                            runtimeMaterials.Add(source, runtime);
                        }
                        materials[materialIndex] = runtime;
                    }

                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                    renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                }
            }

            public void Dispose()
            {
                for (int i = 0; i < instances.Count; i++)
                {
                    GameObject instance = instances[i].GameObject;
                    if (instance == null)
                        continue;
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(instance);
                    else
                        UnityEngine.Object.DestroyImmediate(instance);
                }
                instances.Clear();
                available.Clear();
                deferredReturns.Clear();

                foreach (Material material in runtimeMaterials.Values)
                {
                    if (material == null)
                        continue;
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(material);
                    else
                        UnityEngine.Object.DestroyImmediate(material);
                }
                runtimeMaterials.Clear();
            }
        }
    }
}
