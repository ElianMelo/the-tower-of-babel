using System;
using System.Collections.Generic;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Tower;
using Unity.Profiling;
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

    /// <summary>
    /// Renders every construction stage in spatially-cullable instance pages.
    /// Pages are rebuilt only when their member chunks change.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstancedFarChunkRenderer : MonoBehaviour, IFarChunkRenderer
    {
        private const int MaximumInstancesPerDraw = 1023;
        private static readonly ProfilerMarker RenderMarker = new("Tower.FarRenderer.Render");
        private static readonly ProfilerMarker RebuildMarker = new("Tower.FarRenderer.RebuildPages");

        [Tooltip("One configured building prefab for every tower asset type.")]
        [SerializeField] private List<TowerAssetRenderModel> models = new();
        [Tooltip("Fallback data source for the four model prefabs when Models is empty.")]
        [SerializeField] private ChunkManager chunkManager;

        [Header("Spatial batching")]
        [Tooltip("Camera used for far-cell culling. Defaults to Camera.main.")]
        [SerializeField] private Camera renderCamera;
        [Tooltip("Number of vertical chunk layers grouped into one cullable render cell.")]
        [SerializeField, Min(1)] private int cellFloorSpan = 4;
        [Tooltip("Number of X/Z chunks grouped into one cullable render cell.")]
        [SerializeField, Min(1)] private int cellHorizontalSpan = 2;
        [SerializeField] private bool enableFrustumCulling = true;
        [Tooltip("Zero means no additional distance culling.")]
        [SerializeField, Min(0f)] private float maximumDrawDistance;
        [SerializeField] private bool useLightProbes = true;

        private readonly Dictionary<RenderBatchKey, TypeBatch> batches = new();
        private readonly HashSet<ChunkKey> loadedChunks = new();
        private readonly Dictionary<ChunkKey, PreparedChunk> preparedChunks = new();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private bool isVisible = true;

        public int LoadedChunkCount => loadedChunks.Count;
        public int VisiblePageCount { get; private set; }
        public int CulledPageCount { get; private set; }
        public int DrawCallCount { get; private set; }

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

            using (RenderMarker.Auto())
            {
                ResolveCamera();
                bool canCull = enableFrustumCulling && renderCamera != null;
                if (canCull)
                    GeometryUtility.CalculateFrustumPlanes(renderCamera, frustumPlanes);

                VisiblePageCount = 0;
                CulledPageCount = 0;
                DrawCallCount = 0;
                Vector3 cameraPosition = renderCamera != null ? renderCamera.transform.position : Vector3.zero;
                float maximumDistanceSquared = maximumDrawDistance > 0f
                    ? maximumDrawDistance * maximumDrawDistance
                    : 0f;

                foreach (TypeBatch batch in batches.Values)
                {
                    BatchRenderStats stats = batch.Render(
                        gameObject.layer,
                        renderCamera,
                        canCull ? frustumPlanes : null,
                        cameraPosition,
                        maximumDistanceSquared,
                        useLightProbes);
                    VisiblePageCount += stats.VisiblePages;
                    CulledPageCount += stats.CulledPages;
                    DrawCallCount += stats.DrawCalls;
                }
            }
        }

        public void LoadChunk(in FarChunkSnapshot snapshot)
        {
            EnsureModelBatches();
            RemoveChunk(snapshot.Key);
            loadedChunks.Add(snapshot.Key);

            if (!preparedChunks.TryGetValue(snapshot.Key, out PreparedChunk prepared) ||
                prepared.Version != snapshot.Version)
            {
                prepared = PrepareChunk(in snapshot);
                preparedChunks[snapshot.Key] = prepared;
            }

            foreach (KeyValuePair<RenderBatchKey, TypeBatch> pair in batches)
            {
                if (prepared.MatricesByModel.TryGetValue(pair.Key, out List<Matrix4x4> matrices) &&
                    matrices.Count > 0)
                    pair.Value.SetChunk(snapshot.Key, prepared.CellKey, matrices);
            }
        }

        private PreparedChunk PrepareChunk(in FarChunkSnapshot snapshot)
        {
            Dictionary<RenderBatchKey, List<Matrix4x4>> matricesByModel = new();
            IReadOnlyList<ChunkAssetData> assets = snapshot.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                ChunkAssetData asset = assets[i];
                RenderBatchKey batchKey = new(asset.AssetType, asset.Stage);
                if (!batches.ContainsKey(batchKey))
                    continue;

                if (!matricesByModel.TryGetValue(batchKey, out List<Matrix4x4> matrices))
                {
                    matrices = new List<Matrix4x4>();
                    matricesByModel.Add(batchKey, matrices);
                }
                matrices.Add(asset.ObjectToWorld);
            }

            RenderCellKey cellKey = RenderCellKey.From(snapshot.Key, cellFloorSpan, cellHorizontalSpan);
            return new PreparedChunk(snapshot.Version, cellKey, matricesByModel);
        }

        public void ApplyChunkStageSnapshot(in FarChunkStageSnapshot snapshot)
        {
            // Stage selection remains behind this boundary. ChunkManager reloads the affected
            // chunk for now, which dirties only its spatial cell instead of all far matrices.
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
                        AddModelBatches(model.AssetType, model.Prefab);
                }
                return;
            }

            if (chunkManager == null)
                chunkManager = GetComponent<ChunkManager>();
            if (chunkManager == null)
                chunkManager = FindFirstObjectByType<ChunkManager>();
            if (chunkManager == null)
            {
                Debug.LogError("Far chunk rendering needs explicit Models or a ChunkManager prefab data source.", this);
                return;
            }

            TowerAssetPrefabSet prefabs = chunkManager.AssetPrefabs;
            AddModelBatches(TowerAssetType.Floor, prefabs.GetPrefab(TowerAssetType.Floor));
            AddModelBatches(TowerAssetType.Stair, prefabs.GetPrefab(TowerAssetType.Stair));
            AddModelBatches(TowerAssetType.Pillar, prefabs.GetPrefab(TowerAssetType.Pillar));
            AddModelBatches(TowerAssetType.Arch, prefabs.GetPrefab(TowerAssetType.Arch));
        }

        private void EnsureModelBatches()
        {
            if (batches.Count == 0)
                BuildModelBatches();
        }

        private void ResolveCamera()
        {
            if (renderCamera == null)
                renderCamera = Camera.main;
        }

        private void AddModelBatches(TowerAssetType assetType, GameObject prefab)
        {
            if (prefab == null)
            {
                Debug.LogError($"No building prefab is assigned for far {assetType} rendering.", this);
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

            Building building = prefab.GetComponent<Building>();
            for (byte stage = 0; stage <= ChunkAssetData.CompletedStage; stage++)
            {
                RenderBatchKey key = new(assetType, stage);
                if (batches.ContainsKey(key))
                {
                    Debug.LogWarning($"Duplicate far render model for {assetType} stage {stage}; the later entry was ignored.", this);
                    continue;
                }

                Mesh mesh = building != null ? building.GetStageMesh(stage) : meshFilter.sharedMesh;
                if (mesh != null)
                    batches.Add(key, new TypeBatch(mesh, meshRenderer));
            }
        }

        private void DisposeBatches()
        {
            foreach (TypeBatch batch in batches.Values)
                batch.Dispose();
            batches.Clear();
            loadedChunks.Clear();
            preparedChunks.Clear();
        }

        private void OnDisable()
        {
            DisposeBatches();
        }

        private void OnDestroy()
        {
            DisposeBatches();
        }

        private void OnValidate()
        {
            cellFloorSpan = Mathf.Max(1, cellFloorSpan);
            cellHorizontalSpan = Mathf.Max(1, cellHorizontalSpan);
            maximumDrawDistance = Mathf.Max(0f, maximumDrawDistance);
        }

        private readonly struct BatchRenderStats
        {
            public int VisiblePages { get; }
            public int CulledPages { get; }
            public int DrawCalls { get; }

            public BatchRenderStats(int visiblePages, int culledPages, int drawCalls)
            {
                VisiblePages = visiblePages;
                CulledPages = culledPages;
                DrawCalls = drawCalls;
            }
        }

        private sealed class PreparedChunk
        {
            public int Version { get; }
            public RenderCellKey CellKey { get; }
            public Dictionary<RenderBatchKey, List<Matrix4x4>> MatricesByModel { get; }

            public PreparedChunk(int version, RenderCellKey cellKey,
                Dictionary<RenderBatchKey, List<Matrix4x4>> matricesByModel)
            {
                Version = version;
                CellKey = cellKey;
                MatricesByModel = matricesByModel;
            }
        }

        private readonly struct RenderBatchKey : IEquatable<RenderBatchKey>
        {
            private readonly TowerAssetType assetType;
            private readonly byte stage;

            public RenderBatchKey(TowerAssetType assetType, byte stage)
            {
                this.assetType = assetType;
                this.stage = stage;
            }

            public bool Equals(RenderBatchKey other) =>
                assetType == other.assetType && stage == other.stage;

            public override bool Equals(object obj) => obj is RenderBatchKey other && Equals(other);

            public override int GetHashCode() => ((int)assetType * 397) ^ stage;
        }

        private readonly struct RenderCellKey : IEquatable<RenderCellKey>
        {
            private readonly int floor;
            private readonly int x;
            private readonly int z;

            private RenderCellKey(int floor, int x, int z)
            {
                this.floor = floor;
                this.x = x;
                this.z = z;
            }

            public static RenderCellKey From(ChunkKey key, int floorSpan, int horizontalSpan)
            {
                return new RenderCellKey(
                    FloorDivide(key.FloorIndex, floorSpan),
                    FloorDivide(key.X, horizontalSpan),
                    FloorDivide(key.Z, horizontalSpan));
            }

            public bool Equals(RenderCellKey other)
            {
                return floor == other.floor && x == other.x && z == other.z;
            }

            public override bool Equals(object obj)
            {
                return obj is RenderCellKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = floor;
                    hash = (hash * 397) ^ x;
                    return (hash * 397) ^ z;
                }
            }

            private static int FloorDivide(int value, int divisor)
            {
                int quotient = value / divisor;
                int remainder = value % divisor;
                return remainder < 0 ? quotient - 1 : quotient;
            }
        }

        private sealed class TypeBatch : IDisposable
        {
            private readonly Mesh mesh;
            private readonly Material material;
            private readonly Dictionary<RenderCellKey, RenderCell> cells = new();
            private readonly Dictionary<ChunkKey, RenderCellKey> cellByChunk = new();

            public int Count { get; private set; }

            public TypeBatch(Mesh mesh, MeshRenderer sourceRenderer)
            {
                this.mesh = mesh;
                Material sourceMaterial = sourceRenderer.sharedMaterial;
                material = new Material(sourceMaterial)
                {
                    name = $"{sourceMaterial.name} (Far Instanced)",
                    enableInstancing = true,
                    hideFlags = HideFlags.DontSave
                };
                if (material.HasProperty("_ReceiveShadows"))
                    material.SetFloat("_ReceiveShadows", 0f);
            }

            public void SetChunk(ChunkKey owner, RenderCellKey cellKey, List<Matrix4x4> matrices)
            {
                RemoveChunk(owner);
                if (!cells.TryGetValue(cellKey, out RenderCell cell))
                {
                    cell = new RenderCell();
                    cells.Add(cellKey, cell);
                }

                cell.SetChunk(owner, matrices);
                cellByChunk.Add(owner, cellKey);
                Count += matrices.Count;
            }

            public void RemoveChunk(ChunkKey owner)
            {
                if (!cellByChunk.Remove(owner, out RenderCellKey cellKey) ||
                    !cells.TryGetValue(cellKey, out RenderCell cell))
                    return;

                Count -= cell.RemoveChunk(owner);
                if (cell.IsEmpty)
                    cells.Remove(cellKey);
            }

            public BatchRenderStats Render(int layer, Camera camera, Plane[] planes,
                Vector3 cameraPosition, float maximumDistanceSquared, bool useLightProbes)
            {
                int visiblePages = 0;
                int culledPages = 0;
                int drawCalls = 0;

                RenderParams baseParams = new(material)
                {
                    layer = layer,
                    camera = camera,
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                };

                foreach (RenderCell cell in cells.Values)
                {
                    using (RebuildMarker.Auto())
                        cell.RebuildIfDirty(mesh.bounds, useLightProbes);

                    IReadOnlyList<RenderPage> pages = cell.Pages;
                    for (int i = 0; i < pages.Count; i++)
                    {
                        RenderPage page = pages[i];
                        bool outsideFrustum = planes != null &&
                                              !GeometryUtility.TestPlanesAABB(planes, page.Bounds);
                        bool outsideDistance = maximumDistanceSquared > 0f &&
                                               page.Bounds.SqrDistance(cameraPosition) > maximumDistanceSquared;
                        if (outsideFrustum || outsideDistance)
                        {
                            culledPages++;
                            continue;
                        }

                        RenderParams renderParams = baseParams;
                        renderParams.worldBounds = page.Bounds;
                        renderParams.matProps = page.LightProbeProperties;
                        renderParams.lightProbeUsage = page.LightProbeProperties != null
                            ? LightProbeUsage.CustomProvided
                            : LightProbeUsage.Off;
                        Graphics.RenderMeshInstanced(renderParams, mesh, 0,
                            cell.Matrices, page.Count, page.StartIndex);
                        visiblePages++;
                        drawCalls++;
                    }
                }

                return new BatchRenderStats(visiblePages, culledPages, drawCalls);
            }

            public void Dispose()
            {
                cells.Clear();
                cellByChunk.Clear();
                if (material == null)
                    return;
                if (Application.isPlaying)
                    Destroy(material);
                else
                    DestroyImmediate(material);
            }
        }

        private sealed class RenderCell
        {
            private readonly Dictionary<ChunkKey, List<Matrix4x4>> matricesByChunk = new();
            private readonly List<Matrix4x4> matrices = new();
            private readonly List<RenderPage> pages = new();
            private bool dirty = true;

            public bool IsEmpty => matricesByChunk.Count == 0;
            public List<Matrix4x4> Matrices => matrices;
            public IReadOnlyList<RenderPage> Pages => pages;

            public void SetChunk(ChunkKey key, List<Matrix4x4> chunkMatrices)
            {
                matricesByChunk[key] = chunkMatrices;
                dirty = true;
            }

            public int RemoveChunk(ChunkKey key)
            {
                if (!matricesByChunk.Remove(key, out List<Matrix4x4> removed))
                    return 0;
                dirty = true;
                return removed.Count;
            }

            public void RebuildIfDirty(Bounds meshBounds, bool useLightProbes)
            {
                if (!dirty)
                    return;

                matrices.Clear();
                pages.Clear();
                foreach (List<Matrix4x4> chunkMatrices in matricesByChunk.Values)
                    matrices.AddRange(chunkMatrices);

                for (int start = 0; start < matrices.Count; start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(MaximumInstancesPerDraw, matrices.Count - start);
                    Bounds bounds = CalculateBounds(meshBounds, matrices, start, count);
                    MaterialPropertyBlock probeProperties = useLightProbes
                        ? BuildLightProbeProperties(bounds.center, count)
                        : null;
                    pages.Add(new RenderPage(start, count, bounds, probeProperties));
                }

                dirty = false;
            }

            private static Bounds CalculateBounds(Bounds meshBounds, List<Matrix4x4> source,
                int start, int count)
            {
                Bounds bounds = TransformBounds(meshBounds, source[start]);
                for (int i = 1; i < count; i++)
                    bounds.Encapsulate(TransformBounds(meshBounds, source[start + i]));
                return bounds;
            }

            private static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
            {
                Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
                Vector3 extents = localBounds.extents;
                Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
                Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
                Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
                extents = new Vector3(
                    Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                    Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                    Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
                return new Bounds(center, extents * 2f);
            }

            private static MaterialPropertyBlock BuildLightProbeProperties(Vector3 position, int count)
            {
                if (LightmapSettings.lightProbes == null || LightmapSettings.lightProbes.count == 0)
                    return null;

                LightProbes.GetInterpolatedProbe(position, null, out SphericalHarmonicsL2 probe);
                List<SphericalHarmonicsL2> probes = new(count);
                for (int i = 0; i < count; i++)
                    probes.Add(probe);

                MaterialPropertyBlock properties = new();
                properties.CopySHCoefficientArraysFrom(probes);
                return properties;
            }
        }

        private sealed class RenderPage
        {
            public int StartIndex { get; }
            public int Count { get; }
            public Bounds Bounds { get; }
            public MaterialPropertyBlock LightProbeProperties { get; }

            public RenderPage(int startIndex, int count, Bounds bounds,
                MaterialPropertyBlock lightProbeProperties)
            {
                StartIndex = startIndex;
                Count = count;
                Bounds = bounds;
                LightProbeProperties = lightProbeProperties;
            }
        }
    }
}
