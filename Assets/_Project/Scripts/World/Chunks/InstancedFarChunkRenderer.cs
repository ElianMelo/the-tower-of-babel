using System;
using System.Collections.Generic;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Tower;
using TowerOfBabel.Rendering;
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
    /// Pages are rebuilt only when their member chunks change. Each cell's transforms live in GPU buffers, uploaded
    /// only when the cell rebuilds, and pages draw with Graphics.RenderMeshIndirect (procedural:AeroFarSetup in the
    /// tower shaders): nothing per instance is copied per frame. With the whole tower in view, re-uploading ~64k
    /// instances every frame through RenderMeshInstanced cost ~20 ms of CPU (performance pass 2026-11-03).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InstancedFarChunkRenderer : MonoBehaviour, IFarChunkRenderer, IRiverGlassDrawSource, IAeroReflectionDrawSource
    {
        // Both object-to-world and world-to-object are required by our non-uniform
        // radial transforms. Unity's default instancing layout fits 511, not 1023.
        public const int MaximumInstancesPerDraw = 511;
        private static readonly ProfilerMarker RenderMarker = new("Tower.FarRenderer.Render");
        private static readonly ProfilerMarker RebuildMarker = new("Tower.FarRenderer.RebuildPages");

        [Tooltip("One configured building prefab for every tower asset type.")]
        [SerializeField] private List<TowerAssetRenderModel> models = new();
        [Tooltip("Supplies asset types missing from the explicit model overrides.")]
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
        [Tooltip("Detail vines are small and numerous; their pages stop drawing beyond this distance. Zero disables the limit.")]
        [SerializeField, Min(0f)] private float detailDrawDistance = 140f;
        [Tooltip("Milliseconds per frame spent rebuilding changed render cells (new cells first, then nearest). Cells that " +
                 "wait keep drawing their previous state. Zero rebuilds everything at once.")]
        [SerializeField, Min(0f)] private float rebuildBudgetMs = 2f;

        [Header("Mesh LOD (same material)")]
        [Tooltip("Simplified copies of the finished Floor, Stair, Pillar, Arch and GlassWaterFloor (TowerMeshLodAuthoring), " +
                 "two per type: LOD1 (~50% of the triangles) then LOD2 (~30%). Same material and shader as up close.")]
        [SerializeField] private Mesh[] meshLods = new Mesh[LodSlots];
        [Tooltip("Pages whose nearest point is farther than this draw LOD1 (sub-pixel change at 1080p).")]
        [SerializeField, Min(0f)] private float lod1Distance = 70f;
        [SerializeField, Min(0f)] private float lod2Distance = 140f;

        private readonly Dictionary<RenderBatchKey, TypeBatch> batches = new();
        private readonly HashSet<ChunkKey> loadedChunks = new();
        private readonly Dictionary<ChunkKey, PreparedChunk> preparedChunks = new();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private bool isVisible = true;

        public int LoadedChunkCount => loadedChunks.Count;
        public int VisiblePageCount { get; private set; }
        public int CulledPageCount { get; private set; }
        public int DrawCallCount { get; private set; }
        public int ReflectionDrawCallCount { get; private set; }

        private static MaterialPropertyBlock ambientProbeProperties;
        private static SphericalHarmonicsL2 ambientProbeSource;

        /// <summary>
        /// GPU-resident far instancing on (default) or the per-frame RenderMeshInstanced path (fallback, tests and the
        /// benchmark). The resident path is used only where the hardware and the material's shader support it.
        /// </summary>
        public static bool GpuResidentInstancing = true;
        /// <summary>Mesh LOD on (default) or full meshes everywhere (tests and the benchmark).</summary>
        public static bool MeshLodEnabled = true;
        public const int LodSlots = 10;
        public static int LodSlot(TowerAssetType type, int level) => (int)type * 2 + level - 1;
        /// <summary>Pages drawn with a simplified mesh in the last frame.</summary>
        public int LodPageCount { get; private set; }
        /// <summary>Render cells rebuilt last frame, and those still waiting for the rebuild budget.</summary>
        public int RebuiltCellCount { get; private set; }
        public int PendingCellCount { get; private set; }
        public float RebuildBudgetMs { get => rebuildBudgetMs; set => rebuildBudgetMs = Mathf.Max(0, value); }
        private readonly List<(float priority, TypeBatch batch, RenderCell cell)> dirtyCells = new();
        private static readonly Comparison<(float priority, TypeBatch batch, RenderCell cell)> ByPriority = (a, b) => a.priority.CompareTo(b.priority);
        private readonly System.Diagnostics.Stopwatch rebuildClock = new();
        public float Lod1Distance => lod1Distance;
        public float Lod2Distance => lod2Distance;

        /// <summary>Assigns the simplified meshes and switch distances (authoring and tests), rebuilding loaded batches.</summary>
        public void ConfigureMeshLod(Mesh[] meshes, float lod1, float lod2)
        {
            meshLods = meshes ?? new Mesh[LodSlots];
            lod1Distance = lod1; lod2Distance = lod2;
            foreach (var pair in batches)
                if (pair.Key.Stage == ChunkAssetData.CompletedStage && (int)pair.Key.AssetType * 2 + 1 < meshLods.Length)
                    pair.Value.SetLods(meshLods[LodSlot(pair.Key.AssetType, 1)], meshLods[LodSlot(pair.Key.AssetType, 2)], lod1Distance, lod2Distance);
        }
        public static bool GpuResidentSupported => SystemInfo.supportsComputeShaders && SystemInfo.supportsIndirectArgumentsBuffer;
        /// <summary>Pages drawn through the resident indirect path in the last frame.</summary>
        public int ResidentDrawCount { get; private set; }

        private static readonly int FarObjectToWorld = Shader.PropertyToID("_AeroFarObjectToWorld");
        private static readonly int FarWorldToObject = Shader.PropertyToID("_AeroFarWorldToObject");
        private static readonly int FarBase = Shader.PropertyToID("_AeroFarBase");
        private static readonly int FarFloor = Shader.PropertyToID("_AeroFarFloor");
        private static readonly int[] ShIds =
        {
            Shader.PropertyToID("unity_SHAr"), Shader.PropertyToID("unity_SHAg"), Shader.PropertyToID("unity_SHAb"),
            Shader.PropertyToID("unity_SHBr"), Shader.PropertyToID("unity_SHBg"), Shader.PropertyToID("unity_SHBb"),
            Shader.PropertyToID("unity_SHC")
        };
        private static readonly Vector4[] ambientSh = new Vector4[7];
        private static int ambientShVersion;

        /// <summary>
        /// The scene's ambient probe as the seven per-draw SH constants, for vine pages on the resident path (one draw,
        /// one probe: the same values the instanced path copies to every instance). Version bumps when it changes.
        /// </summary>
        private static int AmbientShVersion()
        {
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            if (ambientShVersion != 0 && probe == ambientProbeSource && ambientProbeProperties != null) return ambientShVersion;
            AmbientProbeProperties();
            for (int c = 0; c < 3; c++)
            {
                ambientSh[c] = new Vector4(probe[c, 3], probe[c, 1], probe[c, 2], probe[c, 0] - probe[c, 6]);
                ambientSh[c + 3] = new Vector4(probe[c, 4], probe[c, 5], probe[c, 6] * 3, probe[c, 7]);
            }
            ambientSh[6] = new Vector4(probe[0, 8], probe[1, 8], probe[2, 8], 1);
            return ++ambientShVersion;
        }

        /// <summary>
        /// The scene's ambient probe, replicated for a full instanced draw. Detail foliage uses it when the scene has
        /// no baked light probes; without it instanced pages get no ambient light at all and the vines read dark.
        /// Rebuilt only when the ambient probe changes (day and night, weather).
        /// </summary>
        public static MaterialPropertyBlock AmbientProbeProperties()
        {
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            if (ambientProbeProperties != null && probe == ambientProbeSource) return ambientProbeProperties;
            ambientProbeSource = probe;
            ambientProbeProperties ??= new MaterialPropertyBlock();
            var probes = new List<SphericalHarmonicsL2>(MaximumInstancesPerDraw);
            for (int i = 0; i < MaximumInstancesPerDraw; i++) probes.Add(probe);
            ambientProbeProperties.CopySHCoefficientArraysFrom(probes);
            return ambientProbeProperties;
        }

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
            RiverGlassRenderRegistry.Register(this);
            AeroBuildingReflectionRegistry.Register(this);
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
                ResidentDrawCount = 0;
                LodPageCount = 0;
                Vector3 cameraPosition = renderCamera != null ? renderCamera.transform.position : Vector3.zero;
                float maximumDistanceSquared = maximumDrawDistance > 0f
                    ? maximumDrawDistance * maximumDrawDistance
                    : 0f;

                float detailDistanceSquared = detailDrawDistance > 0f
                    ? Mathf.Min(maximumDistanceSquared > 0f ? maximumDistanceSquared : float.MaxValue, detailDrawDistance * detailDrawDistance)
                    : maximumDistanceSquared;

                RebuildDirtyCells(cameraPosition);
                foreach (TypeBatch batch in batches.Values)
                {
                    BatchRenderStats stats = batch.Render(
                        gameObject.layer,
                        renderCamera,
                        canCull ? frustumPlanes : null,
                        cameraPosition,
                        batch.Detail ? detailDistanceSquared : maximumDistanceSquared,
                        useLightProbes);
                    VisiblePageCount += stats.VisiblePages;
                    CulledPageCount += stats.CulledPages;
                    DrawCallCount += stats.DrawCalls;
                    ResidentDrawCount += stats.ResidentDraws;
                    LodPageCount += stats.LodPages;
                }
            }
        }

        /// <summary>
        /// Rebuilds changed cells within the frame budget. Never-built cells always build (they draw nothing yet);
        /// changed cells follow nearest first (a chunk that just moved near is still drawn here until its cell
        /// rebuilds), at least one per frame.
        /// </summary>
        private void RebuildDirtyCells(Vector3 cameraPosition)
        {
            dirtyCells.Clear();
            foreach (TypeBatch batch in batches.Values) batch.CollectDirty(cameraPosition, dirtyCells);
            dirtyCells.Sort(ByPriority);
            rebuildClock.Restart();
            int rebuilt = 0;
            using (RebuildMarker.Auto())
                foreach (var entry in dirtyCells)
                {
                    // Never-built cells (priority below zero) draw nothing yet, so they always build; changed cells keep
                    // drawing their previous state and wait for the budget.
                    bool isNew = entry.priority < 0;
                    if (!isNew && rebuilt > 0 && rebuildBudgetMs > 0 && rebuildClock.Elapsed.TotalMilliseconds >= rebuildBudgetMs) break;
                    entry.batch.Rebuild(entry.cell, useLightProbes);
                    rebuilt++;
                }
            RebuiltCellCount = rebuilt;
            PendingCellCount = dirtyCells.Count - rebuilt;
            dirtyCells.Clear();
        }

        public void LoadChunk(in FarChunkSnapshot snapshot)
        {
            RiverGlassRenderRegistry.Register(this);
            AeroBuildingReflectionRegistry.Register(this);
            EnsureModelBatches();
            // Reloading replaces the chunk inside its cell rather than removing it first: a cell that would empty and
            // be recreated would draw nothing until its budgeted rebuild.
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
                    pair.Value.SetChunk(snapshot.Key, pair.Key.IsRiver ? RenderCellKey.From(snapshot.Key, 1, cellHorizontalSpan) : prepared.CellKey, matrices);
                else
                    pair.Value.RemoveChunk(snapshot.Key);
            }
        }

        private PreparedChunk PrepareChunk(in FarChunkSnapshot snapshot)
        {
            Dictionary<RenderBatchKey, List<Matrix4x4>> matricesByModel = new();
            IReadOnlyList<ChunkAssetData> assets = snapshot.Assets;
            for (int i = 0; i < assets.Count; i++)
            {
                ChunkAssetData asset = assets[i];
                // Blueprints exist only in the near pool; built stages remain visible at distance.
                if (asset.Stage == 0) continue;
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
            if (chunkManager == null) chunkManager = GetComponent<ChunkManager>();
            var configuredTypes = new HashSet<TowerAssetType>();
            for (int i = 0; i < models.Count; i++)
            {
                TowerAssetRenderModel model = models[i];
                if (model != null && model.Prefab != null)
                {
                    AddModelBatches(model.AssetType, model.Prefab);
                    configuredTypes.Add(model.AssetType);
                }
            }

            if (chunkManager == null && models.Count == 0)
                chunkManager = FindFirstObjectByType<ChunkManager>();
            if (chunkManager == null)
            {
                if (configuredTypes.Count == 0)
                    Debug.LogError("Far chunk rendering needs explicit Models or a ChunkManager prefab data source.", this);
                return;
            }

            TowerAssetPrefabSet prefabs = chunkManager.AssetPrefabs;
            foreach (TowerAssetType type in Enum.GetValues(typeof(TowerAssetType)))
            {
                GameObject prefab = prefabs.GetPrefab(type);
                if (!configuredTypes.Contains(type) && prefab != null)
                    AddModelBatches(type, prefab);
            }
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
                if (mesh == null) continue;
                var batch = new TypeBatch(mesh, meshRenderer, assetType.IsDetail(), assetType.IsFloor() ? chunkManager : null,
                    assetType == TowerAssetType.GlassWaterFloor, stage);
                // Simplified meshes match the finished piece; part-built stages keep their own mesh.
                if (stage == ChunkAssetData.CompletedStage && (int)assetType * 2 + 1 < (meshLods?.Length ?? 0))
                    batch.SetLods(meshLods[LodSlot(assetType, 1)], meshLods[LodSlot(assetType, 2)], lod1Distance, lod2Distance);
                batches.Add(key, batch);
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
            RiverGlassRenderRegistry.Unregister(this);
            AeroBuildingReflectionRegistry.Unregister(this);
            DisposeBatches();
        }

        private void OnDestroy()
        {
            RiverGlassRenderRegistry.Unregister(this);
            AeroBuildingReflectionRegistry.Unregister(this);
            DisposeBatches();
        }

        public void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> draws)
        {
            if (!isVisible || (camera.scene.IsValid() && camera.scene != gameObject.scene) ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            foreach (var pair in batches)
                if (pair.Key.IsCompletedRiver) pair.Value.CollectRiverGlass(camera, planes, draws, maximumDrawDistance);
        }

        public int SubmitReflection(Camera camera, Camera owner, Plane[] planes, int floor, float range)
        {
            ReflectionDrawCallCount = 0;
            if (!isActiveAndEnabled || !isVisible || owner.gameObject.scene != gameObject.scene ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0) return 0;
            int count = 0;
            // Cull cached spatial pages first and group across chunks by mesh/material.
            // Near chunks are ordinary renderers and never enter these far cells.
            foreach (var batch in batches.Values)
            {
                count += batch.SubmitReflection(camera, owner.transform.position, planes, floor, range, gameObject.layer);
                ReflectionDrawCallCount += batch.LastReflectionDrawCalls;
            }
            return count;
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
            public int ResidentDraws { get; }
            public int LodPages { get; }

            public BatchRenderStats(int visiblePages, int culledPages, int drawCalls, int residentDraws, int lodPages)
            {
                VisiblePages = visiblePages;
                CulledPages = culledPages;
                DrawCalls = drawCalls;
                ResidentDraws = residentDraws;
                LodPages = lodPages;
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
            public bool IsRiver => assetType == TowerAssetType.GlassWaterFloor;
            public TowerAssetType AssetType => assetType;
            public byte Stage => stage;
            public bool IsCompletedRiver => IsRiver && stage == ChunkAssetData.CompletedStage;

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
            private readonly Bounds meshBounds;
            private readonly Dictionary<RenderCellKey, RenderCell> cells = new();
            private readonly Dictionary<ChunkKey, RenderCellKey> cellByChunk = new();
            private readonly bool river;
            private readonly bool floor;
            public bool Detail { get; }
            private readonly Vector2 floorCenter;
            private readonly float floorHeight;
            private readonly float radial;
            private readonly Matrix4x4[] reflectionMatrices = new Matrix4x4[MaximumInstancesPerDraw];
            private readonly float[] reflectionFloors = new float[MaximumInstancesPerDraw];
            private MaterialPropertyBlock reflectionProperties;

            public int Count { get; private set; }
            public int LastReflectionDrawCalls { get; private set; }

            public int SubmitReflection(Camera camera, Vector3 eye, Plane[] planes, int selectedFloor,
                float range, int layer)
            {
                LastReflectionDrawCalls = 0;
                int count = 0, total = 0;
                Bounds bounds = default;
                foreach (var cell in cells.Values)
                {
                    if (!cell.Built) cell.RebuildIfDirty(meshBounds, false);
                    foreach (var page in cell.Pages)
                    {
                        if (page.Bounds.SqrDistance(eye) > range * range || !GeometryUtility.TestPlanesAABB(planes, page.Bounds)) continue;
                        int end = page.StartIndex + page.Count;
                        for (int i = page.StartIndex; i < end; i++)
                        {
                            int floorIndex = cell.FloorIndices[i];
                            if (floorIndex != selectedFloor && floorIndex != selectedFloor + 1) continue;
                            Bounds b = cell.InstanceBounds[i];
                            if (b.SqrDistance(eye) > range * range || !GeometryUtility.TestPlanesAABB(planes, b)) continue;
                            if (count == 0) bounds = b; else bounds.Encapsulate(b);
                            reflectionMatrices[count] = cell.Matrices[i]; reflectionFloors[count++] = floorIndex;
                            if (count == MaximumInstancesPerDraw) { Flush(count, bounds); total += count; count = 0; }
                        }
                    }
                }
                if (count > 0) { Flush(count, bounds); total += count; }
                return total;

                void Flush(int instances, Bounds worldBounds)
                {
                    LastReflectionDrawCalls++;
                    reflectionProperties ??= new MaterialPropertyBlock();
                    if (river) reflectionProperties.SetFloatArray("_RiverFloorIndex", reflectionFloors);
                    var parameters = new RenderParams(material)
                    {
                        camera = camera, layer = layer, worldBounds = worldBounds, matProps = reflectionProperties,
                        shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                        reflectionProbeUsage = ReflectionProbeUsage.Off, lightProbeUsage = LightProbeUsage.Off,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                    };
                    Graphics.RenderMeshInstanced(parameters, mesh, 0, reflectionMatrices, instances);
                }
            }

            public TypeBatch(Mesh mesh, MeshRenderer sourceRenderer, bool detail, ChunkManager floorOwner, bool river, byte stage)
            {
                this.mesh = mesh;
                Detail = detail;
                this.river = river; floor = floorOwner != null;
                floorCenter = floorOwner != null ? floorOwner.FloorCenter : Vector2.zero;
                floorHeight = floorOwner != null ? floorOwner.FloorHeight : 6;
                radial = floorOwner != null && floorOwner.RadialFloors ? 1 : 0;
                Material sourceMaterial = sourceRenderer.sharedMaterial;
                material = new Material(sourceMaterial)
                {
                    name = $"{sourceMaterial.name} (Far Instanced)",
                    enableInstancing = true,
                    hideFlags = HideFlags.DontSave
                };
                if (material.HasProperty("_ReceiveShadows"))
                    material.SetFloat("_ReceiveShadows", 0f);
                if (river) material.SetFloat("_RiverStage", stage);
                meshBounds = mesh.bounds;
                if (floorOwner != null)
                {
                    TowerFloorLayout.ConfigureMaterial(material, floorOwner.RadialFloors, floorOwner.FloorCenter);
                    if (floorOwner.RadialFloors)
                        meshBounds = TowerFloorLayout.ConservativeBounds(mesh.bounds, floorOwner.FloorRadialDepth, floorOwner.FloorArcLength);
                }
                // The resident path needs the shader's procedural:AeroFarSetup variant (and a readable index range).
                residentCapable = material.shader.keywordSpace.FindKeyword("PROCEDURAL_INSTANCING_ON").isValid && mesh.subMeshCount > 0;
            }

            private readonly bool residentCapable;
            public bool UsesResidentPath => residentCapable && GpuResidentInstancing && GpuResidentSupported;
            // Full mesh, LOD1, LOD2 (null when absent) and the squared switch distances.
            private readonly Mesh[] levels = new Mesh[3];
            private float lod1Sq = float.PositiveInfinity, lod2Sq = float.PositiveInfinity;

            public void SetLods(Mesh lod1, Mesh lod2, float lod1Distance, float lod2Distance)
            {
                levels[1] = lod1; levels[2] = lod2;
                lod1Sq = lod1 != null && lod1Distance > 0 ? lod1Distance * lod1Distance : float.PositiveInfinity;
                lod2Sq = lod2 != null && lod2Distance > 0 ? lod2Distance * lod2Distance : float.PositiveInfinity;
                foreach (var cell in cells.Values) cell.ReleaseGpu();
            }

            private int LevelFor(float distanceSq) =>
                !MeshLodEnabled ? 0 : distanceSq > lod2Sq ? 2 : distanceSq > lod1Sq ? 1 : 0;

            public void SetChunk(ChunkKey owner, RenderCellKey cellKey, List<Matrix4x4> matrices)
            {
                // Same cell: swap the instances in place so the cell keeps drawing until it rebuilds.
                if (cellByChunk.TryGetValue(owner, out RenderCellKey existingKey) && existingKey.Equals(cellKey) &&
                    cells.TryGetValue(cellKey, out RenderCell existing))
                {
                    Count += matrices.Count - existing.ReplaceChunk(owner, matrices);
                    return;
                }
                RemoveChunk(owner);
                if (!cells.TryGetValue(cellKey, out RenderCell cell))
                {
                    cell = new RenderCell { Floor = owner.FloorIndex };
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
                {
                    cell.ReleaseGpu();
                    cells.Remove(cellKey);
                }
            }

            public void CollectDirty(Vector3 cameraPosition, List<(float priority, TypeBatch batch, RenderCell cell)> into)
            {
                foreach (RenderCell cell in cells.Values)
                    if (cell.Dirty)
                        into.Add((cell.Built ? cell.Bounds.SqrDistance(cameraPosition) : -1f, this, cell));
            }

            public void Rebuild(RenderCell cell, bool useLightProbes) => cell.RebuildIfDirty(meshBounds, useLightProbes);

            public BatchRenderStats Render(int layer, Camera camera, Plane[] planes,
                Vector3 cameraPosition, float maximumDistanceSquared, bool useLightProbes)
            {
                int visiblePages = 0;
                int culledPages = 0;
                int drawCalls = 0;
                int residentDraws = 0;
                int lodPages = 0;
                levels[0] = mesh;
                bool resident = UsesResidentPath;
                int shVersion = resident && Detail ? AmbientShVersion() : 0;

                RenderParams baseParams = new(material)
                {
                    layer = layer,
                    camera = camera,
                    // TowerAtmosphereBridge owns bounded sun occlusion. River lights only
                    // need native floor casters inside their small local projection volumes.
                    shadowCastingMode = ShadowCastingMode.Off,
                    receiveShadows = false,
                    reflectionProbeUsage = ReflectionProbeUsage.Off,
                    motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                };

                foreach (RenderCell cell in cells.Values)
                {
                    // Rebuilt by the owner's budgeted pass; a cell still waiting draws its previous, consistent state.
                    // Resident pages share one transform buffer, so neighbouring visible pages with the same mesh level
                    // and shadow mode draw as one run: one indirect draw instead of one per 511-instance page.
                    int runs = 0, runStart = 0, runEnd = -1, runLevel = 0;
                    bool runShadows = false;
                    Bounds runBounds = default;

                    void FlushRun()
                    {
                        if (runEnd < 0) return;
                        cell.EnsureGpu();
                        RenderParams runParams = baseParams;
                        runParams.shadowCastingMode = runShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                        runParams.worldBounds = runBounds;
                        runParams.matProps = cell.RunProperties(runs, runStart, Detail, shVersion);
                        runParams.lightProbeUsage = Detail ? LightProbeUsage.CustomProvided : LightProbeUsage.Off;
                        Mesh runMesh = levels[runLevel] != null ? levels[runLevel] : levels[0];
                        cell.SetRunCommand(runs, runMesh, runEnd - runStart);
                        Graphics.RenderMeshIndirect(runParams, runMesh, cell.Arguments, 1, runs);
                        runs++; drawCalls++; residentDraws++;
                        runEnd = -1;
                    }

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
                            FlushRun();
                            continue;
                        }

                        bool shadows = floor && RiverGlassRenderRegistry.NeedsProjectionShadows(page.Bounds, camera, layer);
                        // Same material and shader at every level; only distant pages use the lighter mesh.
                        int level = LevelFor(page.Bounds.SqrDistance(cameraPosition));
                        Mesh drawMesh = levels[level];
                        if (level > 0) lodPages++;
                        visiblePages++;
                        if (resident && page.LightProbeProperties == null)
                        {
                            if (runEnd == page.StartIndex && runLevel == level && runShadows == shadows)
                            {
                                runEnd += page.Count; runBounds.Encapsulate(page.Bounds);
                            }
                            else
                            {
                                FlushRun();
                                runStart = page.StartIndex; runEnd = page.StartIndex + page.Count;
                                runLevel = level; runShadows = shadows; runBounds = page.Bounds;
                            }
                            continue;
                        }
                        FlushRun();

                        RenderParams renderParams = baseParams;
                        if (shadows)
                            renderParams.shadowCastingMode = ShadowCastingMode.On;
                        renderParams.worldBounds = page.Bounds;
                        renderParams.matProps = page.LightProbeProperties;
                        if (river)
                        {
                            // A page never changes floor; set its floor identity once, not every frame.
                            if (page.FloorIdentities == null)
                            {
                                page.RiverProperties = page.LightProbeProperties ?? new MaterialPropertyBlock();
                                page.FloorIdentities = new float[page.Count];
                                System.Array.Fill(page.FloorIdentities, cell.Floor);
                                page.RiverProperties.SetFloatArray("_RiverFloorIndex", page.FloorIdentities);
                            }
                            renderParams.matProps = page.RiverProperties;
                        }
                        renderParams.lightProbeUsage = page.LightProbeProperties != null
                            ? LightProbeUsage.CustomProvided
                            : LightProbeUsage.Off;
                        if (Detail && page.LightProbeProperties == null)
                        {
                            renderParams.matProps = AmbientProbeProperties();
                            renderParams.lightProbeUsage = LightProbeUsage.CustomProvided;
                        }
                        Graphics.RenderMeshInstanced(renderParams, drawMesh, 0,
                            cell.Matrices, page.Count, page.StartIndex);
                        drawCalls++;
                    }
                    FlushRun();
                    if (runs > 0) cell.UploadRunCommands(runs);
                }

                return new BatchRenderStats(visiblePages, culledPages, drawCalls, residentDraws, lodPages);
            }

            public void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> draws, float maxDistance)
            {
                foreach (var cell in cells.Values)
                {
                    if (!cell.Built) cell.RebuildIfDirty(meshBounds, false);
                    foreach (var page in cell.Pages)
                    {
                        if (planes != null && !GeometryUtility.TestPlanesAABB(planes, page.Bounds)) continue;
                        if (maxDistance > 0 && page.Bounds.SqrDistance(camera.transform.position) > maxDistance * maxDistance) continue;
                        if (page.RiverDraw == null)
                        {
                            var matrices = new Matrix4x4[page.Count];
                            cell.Matrices.CopyTo(page.StartIndex, matrices, 0, page.Count);
                            page.RiverDraw = new RiverGlassDraw { mesh = mesh, matrices = matrices, count = page.Count,
                                floor = cell.Floor, height = cell.Floor * floorHeight, center = floorCenter,
                                radial = radial, bounds = page.Bounds };
                            page.RiverDraw.Configure();
                        }
                        draws.Add(page.RiverDraw);
                    }
                }
            }

            public void Dispose()
            {
                foreach (var cell in cells.Values) cell.ReleaseGpu();
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
            public int Floor;
            private readonly Dictionary<ChunkKey, List<Matrix4x4>> matricesByChunk = new();
            // World bounds keyed by the prepared matrix list: reloading an unchanged chunk (every
            // near/far hand-off) reuses them; a new chunk version brings a new list.
            private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<List<Matrix4x4>, List<Bounds>> boundsByMatrices = new();
            private static readonly Unity.Profiling.ProfilerMarker BoundsMarker = new("Tower.FarRenderer.Bounds");
            private static readonly Unity.Profiling.ProfilerMarker ProbesMarker = new("Tower.FarRenderer.Probes");
            private readonly List<Matrix4x4> matrices = new();
            private readonly List<int> floorIndices = new();
            private readonly List<Bounds> instanceBounds = new();
            private readonly List<RenderPage> pages = new();
            private readonly List<KeyValuePair<ChunkKey, List<Matrix4x4>>> orderedChunks = new();
            private static readonly Comparison<KeyValuePair<ChunkKey, List<Matrix4x4>>> BySpace = (a, b) =>
            {
                int c = a.Key.FloorIndex.CompareTo(b.Key.FloorIndex);
                if (c != 0) return c;
                c = a.Key.X.CompareTo(b.Key.X);
                return c != 0 ? c : a.Key.Z.CompareTo(b.Key.Z);
            };
            private bool dirty = true;

            public bool IsEmpty => matricesByChunk.Count == 0;
            public bool Dirty => dirty;
            /// <summary>True once the cell has pages; until then it draws nothing.</summary>
            public bool Built { get; private set; }
            /// <summary>World bounds of the cell's last rebuild.</summary>
            public Bounds Bounds { get; private set; }
            // Shared scratch for inverse transforms: rebuilds no longer allocate a new array per cell.
            private static Matrix4x4[] inverseScratch = Array.Empty<Matrix4x4>();
            public List<Matrix4x4> Matrices => matrices;
            public IReadOnlyList<int> FloorIndices => floorIndices;
            public IReadOnlyList<Bounds> InstanceBounds => instanceBounds;
            public IReadOnlyList<RenderPage> Pages => pages;

            public void SetChunk(ChunkKey key, List<Matrix4x4> chunkMatrices)
            {
                matricesByChunk[key] = chunkMatrices;
                dirty = true;
            }

            /// <summary>Replaces a chunk's instances; returns how many it had before.</summary>
            public int ReplaceChunk(ChunkKey key, List<Matrix4x4> chunkMatrices)
            {
                int previous = matricesByChunk.TryGetValue(key, out List<Matrix4x4> old) ? old.Count : 0;
                matricesByChunk[key] = chunkMatrices;
                dirty = true;
                return previous;
            }

            public int RemoveChunk(ChunkKey key)
            {
                if (!matricesByChunk.Remove(key, out List<Matrix4x4> removed))
                    return 0;
                dirty = true;
                return removed.Count;
            }

            // GPU-resident copies of this cell: transforms, their inverses and one indirect command per page.
            private GraphicsBuffer gpuObjectToWorld, gpuWorldToObject, gpuArguments;
            private MaterialPropertyBlock[] pageProperties = Array.Empty<MaterialPropertyBlock>();
            private int[] pageShVersions = Array.Empty<int>();
            private bool gpuDirty = true;
            public GraphicsBuffer Arguments => gpuArguments;

            // Run commands: one indirect command per merged run of visible pages, rewritten only when the runs change.
            private GraphicsBuffer.IndirectDrawIndexedArgs[] runCommands = Array.Empty<GraphicsBuffer.IndirectDrawIndexedArgs>();
            private GraphicsBuffer.IndirectDrawIndexedArgs[] uploadedCommands = Array.Empty<GraphicsBuffer.IndirectDrawIndexedArgs>();
            private int uploadedRuns = -1;
            private float[] runBases = Array.Empty<float>();
            private readonly Mesh[] commandMeshes = new Mesh[3];
            private readonly GraphicsBuffer.IndirectDrawIndexedArgs[] commandTemplates = new GraphicsBuffer.IndirectDrawIndexedArgs[3];

            /// <summary>Uploads the cell's transforms once per rebuild (not per frame) and sizes the run commands.</summary>
            public void EnsureGpu()
            {
                if (!gpuDirty && gpuObjectToWorld != null) return;
                int count = Mathf.Max(1, matrices.Count);
                if (gpuObjectToWorld == null || gpuObjectToWorld.count < count)
                {
                    ReleaseBuffers();
                    gpuObjectToWorld = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 64) { name = "Far cell object to world" };
                    gpuWorldToObject = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, 64) { name = "Far cell world to object" };
                }
                if (inverseScratch.Length < matrices.Count) inverseScratch = new Matrix4x4[Mathf.NextPowerOfTwo(matrices.Count)];
                for (int i = 0; i < matrices.Count; i++) inverseScratch[i] = matrices[i].inverse;
                gpuObjectToWorld.SetData(matrices);
                gpuWorldToObject.SetData(inverseScratch, 0, 0, matrices.Count);
                // A run covers at least one page, so there are never more runs than pages.
                int commandCount = Mathf.Max(1, pages.Count);
                if (gpuArguments == null || gpuArguments.count < commandCount)
                {
                    gpuArguments?.Release();
                    gpuArguments = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount,
                        GraphicsBuffer.IndirectDrawIndexedArgs.size) { name = "Far cell run commands" };
                }
                runCommands = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
                uploadedCommands = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
                uploadedRuns = -1;
                pageProperties = new MaterialPropertyBlock[commandCount];
                pageShVersions = new int[commandCount];
                runBases = new float[commandCount];
                Array.Fill(runBases, -1);
                gpuDirty = false;
            }

            /// <summary>Stages run <paramref name="run"/>: <paramref name="count"/> instances of <paramref name="mesh"/>.</summary>
            public void SetRunCommand(int run, Mesh mesh, int count)
            {
                int slot = Array.IndexOf(commandMeshes, mesh);
                if (slot < 0)
                {
                    slot = commandMeshes[0] == null ? 0 : commandMeshes[1] == null ? 1 : commandMeshes[2] == null ? 2 : 0;
                    commandMeshes[slot] = mesh;
                    commandTemplates[slot] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = mesh.GetIndexCount(0), startIndex = mesh.GetIndexStart(0), baseVertexIndex = mesh.GetBaseVertex(0)
                    };
                }
                var command = commandTemplates[slot];
                command.instanceCount = (uint)count; command.startInstance = 0;
                runCommands[run] = command;
            }

            /// <summary>Sends the staged run commands to the GPU when they differ from the last upload.</summary>
            public void UploadRunCommands(int runs)
            {
                bool changed = runs > uploadedRuns;
                for (int i = 0; !changed && i < runs; i++)
                {
                    var a = runCommands[i]; var b = uploadedCommands[i];
                    changed = a.instanceCount != b.instanceCount || a.indexCountPerInstance != b.indexCountPerInstance ||
                              a.startIndex != b.startIndex || a.baseVertexIndex != b.baseVertexIndex;
                }
                if (!changed) return;
                gpuArguments.SetData(runCommands, 0, 0, runs);
                Array.Copy(runCommands, uploadedCommands, runs);
                uploadedRuns = runs;
            }

            /// <summary>Per-run draw properties: the cell buffers, the run's first instance and its floor.</summary>
            public MaterialPropertyBlock RunProperties(int run, int firstInstance, bool ambient, int shVersion)
            {
                var block = pageProperties[run];
                if (block == null)
                {
                    block = pageProperties[run] = new MaterialPropertyBlock();
                    block.SetBuffer(FarObjectToWorld, gpuObjectToWorld);
                    block.SetBuffer(FarWorldToObject, gpuWorldToObject);
                    block.SetFloat(FarFloor, Floor);
                    pageShVersions[run] = -1;
                }
                if (runBases[run] != firstInstance)
                {
                    block.SetFloat(FarBase, firstInstance);
                    runBases[run] = firstInstance;
                }
                if (ambient && pageShVersions[run] != shVersion)
                {
                    for (int i = 0; i < ShIds.Length; i++) block.SetVector(ShIds[i], ambientSh[i]);
                    pageShVersions[run] = shVersion;
                }
                return block;
            }

            private void ReleaseBuffers()
            {
                gpuObjectToWorld?.Release(); gpuObjectToWorld = null;
                gpuWorldToObject?.Release(); gpuWorldToObject = null;
            }

            public void ReleaseGpu()
            {
                ReleaseBuffers();
                gpuArguments?.Release(); gpuArguments = null;
                pageProperties = Array.Empty<MaterialPropertyBlock>();
                Array.Clear(commandMeshes, 0, commandMeshes.Length);
                uploadedRuns = -1;
                gpuDirty = true;
            }

            public void RebuildIfDirty(Bounds meshBounds, bool useLightProbes)
            {
                if (!dirty)
                    return;
                gpuDirty = true;

                matrices.Clear();
                floorIndices.Clear(); instanceBounds.Clear();
                pages.Clear();
                // Chunks in spatial order (floor, then x, then z), so each page covers neighbouring chunks: tight page
                // bounds cull well even in large cells.
                orderedChunks.Clear();
                foreach (var pair in matricesByChunk) orderedChunks.Add(pair);
                orderedChunks.Sort(BySpace);
                using (BoundsMarker.Auto())
                foreach (var pair in orderedChunks)
                {
                    if (!boundsByMatrices.TryGetValue(pair.Value, out List<Bounds> chunkBounds) || chunkBounds.Count != pair.Value.Count)
                    {
                        chunkBounds = new List<Bounds>(pair.Value.Count);
                        foreach (var matrix in pair.Value) chunkBounds.Add(TransformBounds(meshBounds, matrix));
                        boundsByMatrices.AddOrUpdate(pair.Value, chunkBounds);
                    }
                    matrices.AddRange(pair.Value);
                    instanceBounds.AddRange(chunkBounds);
                    for (int i = 0; i < pair.Value.Count; i++) floorIndices.Add(pair.Key.FloorIndex);
                }

                for (int start = 0; start < matrices.Count; start += MaximumInstancesPerDraw)
                {
                    int count = Mathf.Min(MaximumInstancesPerDraw, matrices.Count - start);
                    Bounds bounds = CalculateBounds(instanceBounds, start, count);
                    MaterialPropertyBlock probeProperties = null;
                    if (useLightProbes)
                        using (ProbesMarker.Auto())
                            probeProperties = BuildLightProbeProperties(bounds.center, count);
                    pages.Add(new RenderPage(start, count, bounds, probeProperties));
                }
                if (pages.Count > 0)
                {
                    Bounds all = pages[0].Bounds;
                    for (int i = 1; i < pages.Count; i++) all.Encapsulate(pages[i].Bounds);
                    Bounds = all;
                }
                Built = pages.Count > 0;

                dirty = false;
            }

            private static Bounds CalculateBounds(List<Bounds> source,
                int start, int count)
            {
                Bounds bounds = source[start];
                for (int i = 1; i < count; i++)
                    bounds.Encapsulate(source[start + i]);
                return bounds;
            }

            public static Bounds TransformBounds(Bounds localBounds, Matrix4x4 matrix)
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
            public RiverGlassDraw RiverDraw;
            public MaterialPropertyBlock RiverProperties;
            public float[] FloorIdentities;
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
