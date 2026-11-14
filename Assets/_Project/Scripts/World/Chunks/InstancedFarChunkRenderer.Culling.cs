using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using TowerOfBabel.Rendering;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Chunks
{
    /// <summary>
    /// The GPU-culled far path (performance pass 2026-11-12). From outside, the page path submitted about 37 million
    /// triangles: whole 511-instance pages of lava lamps and vines drew when only their nearest corner was in range,
    /// every page behind the decks drew, and ~300 draws cost the render thread ~8 ms. Here every instance is tested
    /// on the GPU for the camera that is about to render, and each mesh level of each piece is one indirect draw.
    /// </summary>
    public sealed partial class InstancedFarChunkRenderer
    {
        /// <summary>GPU culling on (default) or the page paths (fallback, tests and the benchmark).</summary>
        public static bool GpuCulling = true;
        /// <summary>Deck occlusion on (default). Off leaves frustum, distance and mesh level selection only.</summary>
        public static bool OcclusionCulling = true;
        public const string CullingShaderResource = "Rendering/AeroFarCulling";
        public const string FarLitShaderResource = "Rendering/AeroFarLit";
        /// <summary>Milliseconds per frame for deck maps that only gained tiles (a deck that lost one rebuilds at once).</summary>
        public const float OccluderBudgetMs = 1.5f;

        private static readonly ProfilerMarker CullMarker = new("Tower.FarRenderer.Cull");
        private static readonly ProfilerMarker UploadMarker = new("Tower.FarRenderer.Upload");
        private static readonly int FarVisible = Shader.PropertyToID("_AeroFarVisible");
        private static readonly int FarInstances = Shader.PropertyToID("_AeroFarInstances");
        private static readonly int FarCulled = Shader.PropertyToID("_AeroFarCulled");
        private static readonly int InstancesId = Shader.PropertyToID("_Instances");
        private static readonly int BatchesId = Shader.PropertyToID("_Batches");
        private static readonly int CommandBaseId = Shader.PropertyToID("_CommandBase");
        private static readonly int ArgsId = Shader.PropertyToID("_Args");
        private static readonly int VisibleId = Shader.PropertyToID("_Visible");
        private static readonly int PlanesId = Shader.PropertyToID("_Planes");
        private static readonly int CameraPositionId = Shader.PropertyToID("_CameraPosition");
        private static readonly int CullParamsId = Shader.PropertyToID("_CullParams");
        private static readonly int DecksId = Shader.PropertyToID("_Decks");
        private static readonly int DeckMapsId = Shader.PropertyToID("_DeckMaps");
        private static readonly int OcclusionParamsId = Shader.PropertyToID("_OcclusionParams");
        private static readonly int MarksId = Shader.PropertyToID("_Marks");
        private static readonly int RangesId = Shader.PropertyToID("_Ranges");
        private static readonly int RangeCountsId = Shader.PropertyToID("_RangeCounts");
        private static readonly int OrderedParamsId = Shader.PropertyToID("_OrderedParams");

        // Must match FarInstance in AeroFarCulling.compute and AeroFarInstance in AeroFarInstancing.hlsl.
        [StructLayout(LayoutKind.Sequential)]
        private struct FarInstanceData
        {
            public Vector3 center;
            public uint batch;
            public Vector3 extents;
            public float floorIndex;
            public const int Size = 32;
        }

        // Must match FarBatch in AeroFarCulling.compute.
        [StructLayout(LayoutKind.Sequential)]
        private struct FarBatchData
        {
            public float lod1Sq, lod2Sq, maxDistanceSq;
            public uint command0, command1, command2;
            public int floorCommandBase, floorMin, floorCount;
            public uint ordered, pad1, pad2;
            public const int Size = 48;
        }

        // Must match FarRange in AeroFarCulling.compute.
        [StructLayout(LayoutKind.Sequential)]
        private struct FarRangeData
        {
            public uint start, count, first, batch, last, pad0, pad1, pad2;
            public const int Size = 32;
        }

        // Must match FarDeck in AeroFarCulling.compute.
        [StructLayout(LayoutKind.Sequential)]
        private struct FarDeckData
        {
            public float planeY, originX, originZ, texelsPerMetre, slice, pad0, pad1, pad2;
            public const int Size = 32;
        }

        private static ComputeShader cullingShader;
        private static Shader farLitShader;
        private static bool resourcesResolved;
        private static GraphicsBuffer unusedVisible, unusedInstances;
        private static readonly List<InstancedFarChunkRenderer> cullers = new();
        private static bool cameraHooked;

        private bool culledMode;
        private CulledStore culled;
        private FarOccluderSet occluders;
        private GameObject riverLayoutSource;
        private Vector4? riverLayout;
        private float lodDistanceScale = 1f;

        /// <summary>True while far instances draw through the GPU-culled path.</summary>
        public bool IsGpuCulled => culledMode;
        /// <summary>Instance slots the culling pass walks each camera render.</summary>
        public int CulledInstanceSlots => culled != null ? culled.HighWater : 0;
        /// <summary>Solid texels over every deck map; zero while nothing occludes.</summary>
        public int OccluderSolidTexels => occluders != null ? occluders.SolidTexelCount : 0;
        public FarOccluderSet Occluders => occluders;
        /// <summary>Detail pieces (vines, lava lamps) stop drawing beyond this distance; zero means no limit.</summary>
        public float DetailDrawDistance { get => detailDrawDistance; set => detailDrawDistance = Mathf.Max(0f, value); }
        /// <summary>Nothing of the far tower draws beyond this distance; zero means no limit.</summary>
        public float MaximumDrawDistance { get => maximumDrawDistance; set => maximumDrawDistance = Mathf.Max(0f, value); }
        /// <summary>Scales the mesh LOD switch distances. 1 keeps the authored, sub-pixel distances.</summary>
        public float LodDistanceScale
        {
            get => lodDistanceScale;
            set
            {
                value = Mathf.Max(.05f, value);
                if (Mathf.Approximately(value, lodDistanceScale)) return;
                lodDistanceScale = value;
                ConfigureMeshLod(meshLods, lod1Distance, lod2Distance);
            }
        }

        private static void ResolveResources()
        {
            if (resourcesResolved) return;
            resourcesResolved = true;
            cullingShader = UnityEngine.Resources.Load<ComputeShader>(CullingShaderResource);
            farLitShader = UnityEngine.Resources.Load<Shader>(FarLitShaderResource);
        }

        private static Shader FarLitShader { get { ResolveResources(); return farLitShader; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCullingStatics()
        {
            resourcesResolved = false;
            cullers.Clear();
            if (cameraHooked) RenderPipelineManager.beginCameraRendering -= CullBeforeCamera;
            cameraHooked = false;
        }

        // The procedural shader variant declares the culled path's buffers even on the page path; bind placeholders.
        private static GraphicsBuffer UnusedVisible()
        {
            if (unusedVisible == null || !unusedVisible.IsValid()) CreateUnusedBuffers();
            return unusedVisible;
        }

        private static GraphicsBuffer UnusedInstances()
        {
            if (unusedInstances == null || !unusedInstances.IsValid()) CreateUnusedBuffers();
            return unusedInstances;
        }

        private static void CreateUnusedBuffers()
        {
            ReleaseUnusedBuffers();
            unusedVisible = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(uint)) { name = "Far unused visible list" };
            unusedInstances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, FarInstanceData.Size) { name = "Far unused instance data" };
            unusedVisible.SetData(new uint[1]);
            unusedInstances.SetData(new FarInstanceData[1]);
#if UNITY_EDITOR
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ReleaseUnusedBuffers;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseUnusedBuffers;
#endif
            Application.quitting -= ReleaseUnusedBuffers;
            Application.quitting += ReleaseUnusedBuffers;
        }

        private static void ReleaseUnusedBuffers()
        {
            unusedVisible?.Release(); unusedVisible = null;
            unusedInstances?.Release(); unusedInstances = null;
        }

        private bool WantCulled()
        {
            if (!GpuCulling || !GpuResidentInstancing || !GpuResidentSupported) return false;
            ResolveResources();
            if (cullingShader == null) return false;
            // Baked light probes are sampled per page; that needs the page path.
            return !(useLightProbes && LightmapSettings.lightProbes != null && LightmapSettings.lightProbes.count > 0);
        }

        private bool BatchCulled(TypeBatch batch) => culledMode && batch.ResidentCapable;

        /// <summary>Moves every loaded chunk to the other path when the culling switches or its support changes.</summary>
        private void EnsureMode()
        {
            bool want = WantCulled();
            if (want == culledMode) return;
            culledMode = want;
            foreach (TypeBatch batch in batches.Values) batch.ClearChunks();
            culled?.Clear();
            routedBatches.Clear();
            if (want)
            {
                culled ??= new CulledStore(this);
                if (!cullers.Contains(this)) cullers.Add(this);
                if (!cameraHooked) { RenderPipelineManager.beginCameraRendering += CullBeforeCamera; cameraHooked = true; }
            }
            foreach (ChunkKey key in loadedChunks)
                if (preparedChunks.TryGetValue(key, out PreparedChunk prepared)) RouteChunk(key, prepared);
        }

        private static void CullBeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            for (int i = cullers.Count - 1; i >= 0; i--)
            {
                InstancedFarChunkRenderer renderer = cullers[i];
                if (renderer == null)
                {
                    // Destroyed without OnDestroy (it was never active): release what it still holds.
                    cullers.RemoveAt(i);
                    if (!ReferenceEquals(renderer, null)) { renderer.culled?.Dispose(); renderer.culled = null; renderer.culledMode = false; }
                    continue;
                }
                renderer.CullFor(camera);
            }
        }

        private void CullFor(Camera camera)
        {
            if (!culledMode || culled == null || !isVisible || camera == null) return;
            if (renderCamera != null) { if (camera != renderCamera) return; }
            else if ((camera.cullingMask & (1 << gameObject.layer)) == 0) return;
            using (CullMarker.Auto())
                culled.Cull(camera, OcclusionCulling && occluders != null ? occluders : null);
        }

        private void SubmitCulled(Plane[] planes, Vector3 cameraPosition, float maximumDistanceSquared, float detailDistanceSquared)
        {
            if (occluders != null)
            {
                ConfigureOccluders();
                occluders.Update(Application.isPlaying ? Time.time : 0f, OccluderBudgetMs);
            }
            BatchRenderStats stats = culled.Submit(gameObject.layer, renderCamera, planes, cameraPosition, maximumDistanceSquared, detailDistanceSquared);
            VisiblePageCount += stats.VisiblePages;
            CulledPageCount += stats.CulledPages;
            DrawCallCount += stats.DrawCalls;
            ResidentDrawCount += stats.ResidentDraws;
            LodPageCount += stats.LodPages;
        }

        private void DisposeCulling()
        {
            culled?.Dispose();
            culled = null;
            culledMode = false;
            cullers.Remove(this);
        }

        public void SetOccluderChunk(ChunkKey key, IReadOnlyList<ChunkAssetData> assets, int version)
        {
            ConfigureOccluders();
            occluders.SetChunk(key, assets, version, Application.isPlaying ? Time.time : 0f, Application.isPlaying);
        }

        // The decks follow the chunk manager's floor layout; river tiles are solid outside the channel their
        // material clips (its _RiverLayout). A changed layout drops the maps; the chunk manager feeds them again.
        private void ConfigureOccluders()
        {
            occluders ??= new FarOccluderSet();
            if (chunkManager == null) chunkManager = GetComponent<ChunkManager>();
            GameObject glass = chunkManager != null ? chunkManager.AssetPrefabs.GetPrefab(TowerOfBabel.World.Tower.TowerAssetType.GlassWaterFloor) : null;
            if (glass != riverLayoutSource)
            {
                riverLayoutSource = glass;
                Material material = glass != null && glass.TryGetComponent(out MeshRenderer renderer) ? renderer.sharedMaterial : null;
                riverLayout = material != null && material.HasProperty("_RiverLayout") ? material.GetVector("_RiverLayout") : (Vector4?)null;
            }
            occluders.Configure(chunkManager != null && chunkManager.RadialFloors, chunkManager != null ? chunkManager.FloorCenter : Vector2.zero, riverLayout);
        }

        public void ClearOccluders() => occluders?.Clear();

        /// <summary>
        /// Instances the last cull left visible, per mesh level (reads the GPU counters back; tests and diagnostics).
        /// </summary>
        public int ReadVisibleInstanceCounts(out int full, out int lod1, out int lod2)
        {
            full = lod1 = lod2 = 0;
            return culled != null ? culled.ReadVisibleCounts(out full, out lod1, out lod2) : 0;
        }

        /// <summary>
        /// Box centres of the finished pieces of one type the last cull left visible at one mesh level, in the order
        /// they draw (reads the GPU lists back; tests and diagnostics).
        /// </summary>
        public List<Vector3> ReadVisibleCenters(TowerOfBabel.World.Tower.TowerAssetType type, int level = 0)
        {
            var centers = new List<Vector3>();
            if (culled != null && batches.TryGetValue(new RenderBatchKey(type, ChunkAssetData.CompletedStage), out TypeBatch batch))
                culled.ReadVisibleCenters(batch, level, centers);
            return centers;
        }

        /// <summary>True when the finished pieces of this type blend (glass) and so draw in a fixed order.</summary>
        public bool DrawsInFixedOrder(TowerOfBabel.World.Tower.TowerAssetType type) =>
            batches.TryGetValue(new RenderBatchKey(type, ChunkAssetData.CompletedStage), out TypeBatch batch) && batch.Ordered;

        private readonly struct FreeRange
        {
            public readonly int Start, Count;
            public FreeRange(int start, int count) { Start = start; Count = count; }
        }

        private readonly struct InstanceRange
        {
            public readonly int Start, Count;
            public readonly Bounds Bounds;
            /// <summary>The prepared matrix list this range was filled from; the same list means nothing changed.</summary>
            public readonly List<Matrix4x4> Source;
            public InstanceRange(int start, int count, Bounds bounds, List<Matrix4x4> source) { Start = start; Count = count; Bounds = bounds; Source = source; }
        }

        private sealed class FloorList
        {
            public int Count, Capacity, Command = -1, DrawVersion = -1;
            public Bounds Bounds;
            public bool BoundsDirty = true;
            public RiverGlassDraw Draw;
        }

        private sealed class CulledBatch
        {
            public readonly TypeBatch Source;
            public readonly uint Id;
            public readonly Dictionary<ChunkKey, InstanceRange> Ranges = new();
            public readonly int[] Commands = { -1, -1, -1 };
            public readonly MaterialPropertyBlock[] Properties = new MaterialPropertyBlock[3];
            public readonly int[] PropertyVersions = { -1, -1, -1 }, ShVersions = { -1, -1, -1 };
            /// <summary>Per-floor composition lists of completed river tiles; null for every other batch.</summary>
            public readonly SortedDictionary<int, FloorList> Floors;
            public int Count, SegmentCapacity, FloorCommandBase, FloorMin, FloorCount;
            public Bounds Bounds;
            public bool BoundsDirty = true;

            public CulledBatch(TypeBatch source, uint id)
            {
                Source = source; Id = id;
                if (source.IsRiver && source.Stage == ChunkAssetData.CompletedStage) Floors = new SortedDictionary<int, FloorList>();
            }

            public Bounds WorldBounds()
            {
                if (!BoundsDirty) return Bounds;
                bool first = true;
                foreach (InstanceRange range in Ranges.Values)
                {
                    if (first) { Bounds = range.Bounds; first = false; }
                    else Bounds.Encapsulate(range.Bounds);
                }
                BoundsDirty = false;
                return Bounds;
            }
        }

        private readonly struct Command
        {
            public readonly Mesh Mesh;
            public readonly int Capacity;
            public Command(Mesh mesh, int capacity) { Mesh = mesh; Capacity = capacity; }
        }

        private sealed class CulledStore
        {
            private readonly InstancedFarChunkRenderer owner;
            // CPU mirrors of the instance buffers: uploads send changed spans only, and reflections, projection
            // shadows and the page fallback read them.
            private Matrix4x4[] objectToWorld = Array.Empty<Matrix4x4>(), worldToObject = Array.Empty<Matrix4x4>();
            private FarInstanceData[] instances = Array.Empty<FarInstanceData>();
            private readonly List<FreeRange> free = new();
            private int dirtyMin = int.MaxValue, dirtyMax = -1;
            private GraphicsBuffer gpuObjectToWorld, gpuWorldToObject, gpuInstances, gpuBatches, gpuCommandBase, gpuArgs, gpuVisible;
            private readonly List<CulledBatch> batchList = new();
            private readonly Dictionary<TypeBatch, CulledBatch> batchLookup = new();
            private readonly List<Command> commands = new();
            private GraphicsBuffer.IndirectDrawIndexedArgs[] argsTemplate = Array.Empty<GraphicsBuffer.IndirectDrawIndexedArgs>();
            private uint[] commandBases = Array.Empty<uint>();
            private FarBatchData[] batchData = Array.Empty<FarBatchData>();
            private bool layoutDirty = true, batchDataDirty = true;
            private float lastMaximumSq = -1, lastDetailSq = -1;
            private bool lastLod;
            private int bindingVersion;
            private readonly CommandBuffer cmd = new() { name = "Far tower culling" };
            private readonly Plane[] planes = new Plane[6];
            private readonly Vector4[] planeVectors = new Vector4[6];
            // Deck occlusion: one summed-area table per floor in a texture array, and the list of decks that occlude.
            private GraphicsBuffer gpuDecks;
            private RenderTexture deckMaps;
            private Texture2D mapScratch;
            private float[] tableScratch;
            private FarDeckData[] deckData = Array.Empty<FarDeckData>();
            private int[] sliceVersions = Array.Empty<int>();
            private int deckCount, syncedOccluders = -1;
            // Transparent batches draw in a fixed order: their chunk ranges, sorted, and the marks the cull leaves.
            private GraphicsBuffer gpuMarks, gpuRanges, gpuRangeCounts;
            private FarRangeData[] rangeData = Array.Empty<FarRangeData>();
            private readonly List<KeyValuePair<ChunkKey, InstanceRange>> rangeOrder = new();
            private static readonly Comparison<KeyValuePair<ChunkKey, InstanceRange>> ByChunk = (a, b) =>
            {
                int c = a.Key.FloorIndex.CompareTo(b.Key.FloorIndex);
                if (c != 0) return c;
                c = a.Key.X.CompareTo(b.Key.X);
                return c != 0 ? c : a.Key.Z.CompareTo(b.Key.Z);
            };
            private int rangeCount;
            private bool rangesDirty = true;
            private readonly int cullKernel, countKernel, writeKernel;

            public int HighWater { get; private set; }
            public int InstanceCount { get { int count = 0; foreach (CulledBatch batch in batchList) count += batch.Count; return count; } }

            public CulledStore(InstancedFarChunkRenderer owner)
            {
                this.owner = owner;
                cullKernel = cullingShader.FindKernel("CullInstances");
                countKernel = cullingShader.FindKernel("CountOrdered");
                writeKernel = cullingShader.FindKernel("WriteOrdered");
            }

            public void MarkLayoutDirty() => layoutDirty = true;

            public void Clear()
            {
                batchList.Clear(); batchLookup.Clear(); free.Clear(); commands.Clear();
                HighWater = 0; dirtyMin = int.MaxValue; dirtyMax = -1;
                layoutDirty = batchDataDirty = rangesDirty = true;
                rangeCount = 0;
                bindingVersion++;
            }

            private CulledBatch GetBatch(TypeBatch source)
            {
                if (batchLookup.TryGetValue(source, out CulledBatch batch)) return batch;
                batch = new CulledBatch(source, (uint)batchList.Count);
                batchList.Add(batch); batchLookup.Add(source, batch);
                layoutDirty = true;
                return batch;
            }

            public void SetChunk(TypeBatch source, ChunkKey key, List<Matrix4x4> matrices)
            {
                CulledBatch batch = GetBatch(source);
                if (batch.Ranges.TryGetValue(key, out InstanceRange existing))
                {
                    if (ReferenceEquals(existing.Source, matrices)) return;
                    Release(batch, key, existing);
                }
                int count = matrices.Count, start = Allocate(count);
                Bounds meshBounds = source.MeshBounds, bounds = default;
                for (int i = 0; i < count; i++)
                {
                    Matrix4x4 matrix = matrices[i];
                    objectToWorld[start + i] = matrix;
                    worldToObject[start + i] = matrix.inverse;
                    Bounds box = RenderCell.TransformBounds(meshBounds, matrix);
                    instances[start + i] = new FarInstanceData { center = box.center, batch = batch.Id, extents = box.extents, floorIndex = key.FloorIndex };
                    if (i == 0) bounds = box; else bounds.Encapsulate(box);
                }
                batch.Ranges[key] = new InstanceRange(start, count, bounds, matrices);
                if (source.Ordered) rangesDirty = true;
                MarkDirty(start, count);
                batch.Count += count; batch.BoundsDirty = true;
                if (batch.Count > batch.SegmentCapacity) layoutDirty = true;
                if (batch.Floors != null)
                {
                    if (!batch.Floors.TryGetValue(key.FloorIndex, out FloorList floor))
                    {
                        floor = new FloorList();
                        batch.Floors.Add(key.FloorIndex, floor);
                        layoutDirty = true;
                    }
                    floor.Count += count; floor.BoundsDirty = true;
                    if (floor.Count > floor.Capacity) layoutDirty = true;
                }
            }

            public void RemoveChunk(TypeBatch source, ChunkKey key)
            {
                if (batchLookup.TryGetValue(source, out CulledBatch batch) && batch.Ranges.TryGetValue(key, out InstanceRange range))
                    Release(batch, key, range);
            }

            private void Release(CulledBatch batch, ChunkKey key, InstanceRange range)
            {
                batch.Ranges.Remove(key);
                if (batch.Source.Ordered) rangesDirty = true;
                for (int i = range.Start; i < range.Start + range.Count; i++) instances[i].extents.x = -1f;
                MarkDirty(range.Start, range.Count);
                batch.Count -= range.Count; batch.BoundsDirty = true;
                if (batch.Floors != null && batch.Floors.TryGetValue(key.FloorIndex, out FloorList floor))
                { floor.Count -= range.Count; floor.BoundsDirty = true; }
                Free(range.Start, range.Count);
            }

            private void MarkDirty(int start, int count)
            {
                if (count <= 0) return;
                dirtyMin = Mathf.Min(dirtyMin, start);
                dirtyMax = Mathf.Max(dirtyMax, start + count - 1);
            }

            private int Allocate(int count)
            {
                for (int i = 0; i < free.Count; i++)
                {
                    FreeRange range = free[i];
                    if (range.Count < count) continue;
                    if (range.Count == count) free.RemoveAt(i);
                    else free[i] = new FreeRange(range.Start + count, range.Count - count);
                    return range.Start;
                }
                int at = HighWater;
                int needed = HighWater + count;
                if (needed > objectToWorld.Length)
                {
                    int capacity = Mathf.NextPowerOfTwo(Mathf.Max(needed, 1024));
                    Array.Resize(ref objectToWorld, capacity);
                    Array.Resize(ref worldToObject, capacity);
                    Array.Resize(ref instances, capacity);
                }
                HighWater = needed;
                return at;
            }

            private void Free(int start, int count)
            {
                int index = 0;
                while (index < free.Count && free[index].Start < start) index++;
                free.Insert(index, new FreeRange(start, count));
                if (index + 1 < free.Count && free[index].Start + free[index].Count == free[index + 1].Start)
                {
                    free[index] = new FreeRange(free[index].Start, free[index].Count + free[index + 1].Count);
                    free.RemoveAt(index + 1);
                }
                if (index > 0 && free[index - 1].Start + free[index - 1].Count == free[index].Start)
                {
                    free[index - 1] = new FreeRange(free[index - 1].Start, free[index - 1].Count + free[index].Count);
                    free.RemoveAt(index);
                }
                // The walked span ends where the last live instance ends.
                if (free.Count > 0 && free[free.Count - 1].Start + free[free.Count - 1].Count == HighWater)
                {
                    HighWater = free[free.Count - 1].Start;
                    free.RemoveAt(free.Count - 1);
                }
            }

            /// <summary>Sends changed instance spans and, when sizes changed, the command layout.</summary>
            public void Flush()
            {
                int capacity = objectToWorld.Length;
                if (capacity == 0) return;
                using (UploadMarker.Auto())
                {
                    if (gpuObjectToWorld == null || gpuObjectToWorld.count != capacity)
                    {
                        gpuObjectToWorld?.Release(); gpuWorldToObject?.Release(); gpuInstances?.Release(); gpuMarks?.Release();
                        gpuObjectToWorld = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 64) { name = "Far object to world" };
                        gpuWorldToObject = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, 64) { name = "Far world to object" };
                        gpuInstances = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, FarInstanceData.Size) { name = "Far instance data" };
                        gpuMarks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, capacity, sizeof(uint)) { name = "Far ordered marks" };
                        dirtyMin = 0; dirtyMax = Mathf.Max(dirtyMax, HighWater - 1);
                        bindingVersion++;
                    }
                    if (dirtyMax >= dirtyMin)
                    {
                        int count = dirtyMax - dirtyMin + 1;
                        gpuObjectToWorld.SetData(objectToWorld, dirtyMin, dirtyMin, count);
                        gpuWorldToObject.SetData(worldToObject, dirtyMin, dirtyMin, count);
                        gpuInstances.SetData(instances, dirtyMin, dirtyMin, count);
                    }
                    dirtyMin = int.MaxValue; dirtyMax = -1;
                    if (layoutDirty) RebuildLayout();
                    else if (batchDataDirty) UploadBatchData();
                    if (rangesDirty) RebuildRanges();
                }
            }

            // The chunk ranges of every transparent batch, batch after batch, each batch's chunks in a fixed order
            // (floor, x, z: the order the page path drew them in). Rebuilt only when such a chunk loads or leaves.
            private void RebuildRanges()
            {
                rangesDirty = false;
                rangeCount = 0;
                foreach (CulledBatch batch in batchList)
                {
                    if (!batch.Source.Ordered || batch.Ranges.Count == 0) continue;
                    rangeOrder.Clear();
                    foreach (KeyValuePair<ChunkKey, InstanceRange> pair in batch.Ranges) rangeOrder.Add(pair);
                    rangeOrder.Sort(ByChunk);
                    int first = rangeCount, needed = rangeCount + rangeOrder.Count;
                    if (rangeData.Length < needed) Array.Resize(ref rangeData, Mathf.NextPowerOfTwo(Mathf.Max(needed, 64)));
                    for (int i = 0; i < rangeOrder.Count; i++)
                    {
                        InstanceRange range = rangeOrder[i].Value;
                        rangeData[rangeCount++] = new FarRangeData
                        {
                            start = (uint)range.Start, count = (uint)range.Count, first = (uint)first, batch = batch.Id,
                            last = i == rangeOrder.Count - 1 ? 1u : 0u
                        };
                    }
                }
                rangeOrder.Clear();
                if (rangeCount == 0) return;
                if (gpuRanges == null || gpuRanges.count < rangeCount)
                {
                    gpuRanges?.Release(); gpuRangeCounts?.Release();
                    gpuRanges = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rangeData.Length, FarRangeData.Size) { name = "Far ordered ranges" };
                    gpuRangeCounts = new GraphicsBuffer(GraphicsBuffer.Target.Structured, rangeData.Length, 16) { name = "Far ordered range counts" };
                }
                gpuRanges.SetData(rangeData, 0, 0, rangeCount);
            }

            private static int SegmentCapacity(int count) => Mathf.NextPowerOfTwo(Mathf.Max(count, 16));

            private int AddCommand(Mesh mesh, int capacity)
            {
                commands.Add(new Command(mesh, capacity));
                return commands.Count - 1;
            }

            // One command per mesh level of every batch, plus one per floor of completed river tiles. Each command owns
            // a span of the visible list sized for all of its batch's instances; spans only ever grow.
            private void RebuildLayout()
            {
                commands.Clear();
                foreach (CulledBatch batch in batchList)
                {
                    if (batch.Count == 0 && batch.SegmentCapacity == 0)
                    {
                        batch.Commands[0] = batch.Commands[1] = batch.Commands[2] = -1;
                        continue;
                    }
                    batch.SegmentCapacity = Mathf.Max(batch.SegmentCapacity, SegmentCapacity(batch.Count));
                    Mesh[] levels = batch.Source.Levels;
                    batch.Commands[0] = AddCommand(levels[0], batch.SegmentCapacity);
                    batch.Commands[1] = levels[1] != null ? AddCommand(levels[1], batch.SegmentCapacity) : batch.Commands[0];
                    batch.Commands[2] = levels[2] != null ? AddCommand(levels[2], batch.SegmentCapacity) : batch.Commands[0];
                    batch.FloorCount = 0;
                    if (batch.Floors == null || batch.Floors.Count == 0) continue;
                    int minimum = int.MaxValue, maximum = int.MinValue;
                    foreach (int floor in batch.Floors.Keys) { minimum = Mathf.Min(minimum, floor); maximum = Mathf.Max(maximum, floor); }
                    batch.FloorMin = minimum; batch.FloorCount = maximum - minimum + 1;
                    batch.FloorCommandBase = commands.Count;
                    for (int floor = minimum; floor <= maximum; floor++)
                    {
                        if (!batch.Floors.TryGetValue(floor, out FloorList list)) { list = new FloorList(); batch.Floors.Add(floor, list); }
                        list.Capacity = Mathf.Max(list.Capacity, SegmentCapacity(list.Count));
                        list.Command = AddCommand(levels[0], list.Capacity);
                    }
                }

                int commandCount = Mathf.Max(1, commands.Count);
                if (argsTemplate.Length < commandCount)
                {
                    int size = Mathf.NextPowerOfTwo(commandCount);
                    argsTemplate = new GraphicsBuffer.IndirectDrawIndexedArgs[size];
                    commandBases = new uint[size];
                }
                uint total = 0;
                for (int i = 0; i < commands.Count; i++)
                {
                    Mesh mesh = commands[i].Mesh;
                    argsTemplate[i] = new GraphicsBuffer.IndirectDrawIndexedArgs
                    {
                        indexCountPerInstance = mesh.GetIndexCount(0), startIndex = mesh.GetIndexStart(0), baseVertexIndex = mesh.GetBaseVertex(0)
                    };
                    commandBases[i] = total;
                    total += (uint)commands[i].Capacity;
                }
                if (gpuArgs == null || gpuArgs.count < commandCount)
                {
                    gpuArgs?.Release(); gpuCommandBase?.Release();
                    gpuArgs = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured, argsTemplate.Length,
                        GraphicsBuffer.IndirectDrawIndexedArgs.size) { name = "Far draw commands" };
                    gpuCommandBase = new GraphicsBuffer(GraphicsBuffer.Target.Structured, argsTemplate.Length, sizeof(uint)) { name = "Far command spans" };
                }
                gpuArgs.SetData(argsTemplate, 0, 0, commandCount);
                gpuCommandBase.SetData(commandBases, 0, 0, commandCount);
                int visibleCount = Mathf.Max(1, (int)total);
                if (gpuVisible == null || gpuVisible.count < visibleCount)
                {
                    gpuVisible?.Release();
                    gpuVisible = new GraphicsBuffer(GraphicsBuffer.Target.Structured, Mathf.NextPowerOfTwo(visibleCount), sizeof(uint)) { name = "Far visible instances" };
                }
                layoutDirty = false;
                bindingVersion++;
                UploadBatchData();
            }

            private void UploadBatchData()
            {
                int count = Mathf.Max(1, batchList.Count);
                if (batchData.Length < count) batchData = new FarBatchData[Mathf.NextPowerOfTwo(count)];
                for (int i = 0; i < batchList.Count; i++)
                {
                    CulledBatch batch = batchList[i];
                    uint first = (uint)Mathf.Max(0, batch.Commands[0]);
                    batchData[i] = new FarBatchData
                    {
                        lod1Sq = Mathf.Min(batch.Source.Lod1Sq, 1e30f), lod2Sq = Mathf.Min(batch.Source.Lod2Sq, 1e30f),
                        maxDistanceSq = batch.Source.Detail ? lastDetailSq : lastMaximumSq,
                        command0 = first, command1 = (uint)Mathf.Max(0, batch.Commands[1]), command2 = (uint)Mathf.Max(0, batch.Commands[2]),
                        floorCommandBase = batch.FloorCommandBase, floorMin = batch.FloorMin, floorCount = batch.FloorCount,
                        ordered = batch.Source.Ordered ? 1u : 0u
                    };
                }
                if (gpuBatches == null || gpuBatches.count < count)
                {
                    gpuBatches?.Release();
                    gpuBatches = new GraphicsBuffer(GraphicsBuffer.Target.Structured, batchData.Length, FarBatchData.Size) { name = "Far batches" };
                }
                gpuBatches.SetData(batchData, 0, 0, count);
                batchDataDirty = false;
            }

            private MaterialPropertyBlock PropertiesFor(CulledBatch batch, int level, int shVersion)
            {
                MaterialPropertyBlock block = batch.Properties[level] ??= new MaterialPropertyBlock();
                if (batch.PropertyVersions[level] != bindingVersion)
                {
                    block.SetBuffer(FarObjectToWorld, gpuObjectToWorld);
                    block.SetBuffer(FarWorldToObject, gpuWorldToObject);
                    block.SetBuffer(FarVisible, gpuVisible);
                    block.SetBuffer(FarInstances, gpuInstances);
                    block.SetFloat(FarBase, commandBases[batch.Commands[level]]);
                    block.SetFloat(FarCulled, 1f);
                    block.SetFloat(FarFloor, -1f);
                    batch.PropertyVersions[level] = bindingVersion;
                    batch.ShVersions[level] = -1;
                }
                if (batch.Source.Detail && batch.ShVersions[level] != shVersion)
                {
                    for (int i = 0; i < ShIds.Length; i++) block.SetVector(ShIds[i], ambientSh[i]);
                    batch.ShVersions[level] = shVersion;
                }
                return block;
            }

            public BatchRenderStats Submit(int layer, Camera camera, Plane[] frustum, Vector3 cameraPosition,
                float maximumDistanceSquared, float detailDistanceSquared)
            {
                if (maximumDistanceSquared != lastMaximumSq || detailDistanceSquared != lastDetailSq || MeshLodEnabled != lastLod)
                {
                    lastMaximumSq = maximumDistanceSquared; lastDetailSq = detailDistanceSquared; lastLod = MeshLodEnabled;
                    batchDataDirty = true;
                }
                // Mesh LOD distances may have changed on the batches (ConfigureMeshLod marks the layout).
                Flush();
                if (gpuArgs == null || HighWater == 0) return default;
                int visible = 0, skipped = 0, draws = 0, lodDraws = 0, shVersion = -1;
                foreach (CulledBatch batch in batchList)
                {
                    if (batch.Count == 0 || batch.Commands[0] < 0) continue;
                    Bounds bounds = batch.WorldBounds();
                    float limit = batch.Source.Detail ? detailDistanceSquared : maximumDistanceSquared;
                    if ((frustum != null && !GeometryUtility.TestPlanesAABB(frustum, bounds)) ||
                        (limit > 0f && bounds.SqrDistance(cameraPosition) > limit))
                    {
                        skipped++;
                        continue;
                    }
                    visible++;
                    if (batch.Source.Detail && shVersion < 0) shVersion = AmbientShVersion();
                    var parameters = new RenderParams(batch.Source.Material)
                    {
                        layer = layer, camera = camera, worldBounds = bounds,
                        // TowerAtmosphereBridge owns bounded sun occlusion; far pieces neither cast nor receive.
                        shadowCastingMode = ShadowCastingMode.Off, receiveShadows = false,
                        reflectionProbeUsage = ReflectionProbeUsage.Off,
                        lightProbeUsage = batch.Source.Detail ? LightProbeUsage.CustomProvided : LightProbeUsage.Off,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                    };
                    Mesh[] levels = batch.Source.Levels;
                    for (int level = 0; level < 3; level++)
                    {
                        int command = batch.Commands[level];
                        // Without mesh LOD every instance goes to the full mesh; a level without its own mesh shares it.
                        if (level > 0 && (command == batch.Commands[0] || !MeshLodEnabled)) continue;
                        parameters.matProps = PropertiesFor(batch, level, shVersion);
                        Graphics.RenderMeshIndirect(parameters, levels[level], gpuArgs, 1, command);
                        draws++;
                        if (level > 0) lodDraws++;
                    }
                    if (batch.Source.IsFloor && camera != null) draws += SubmitProjectionShadows(batch, camera, layer);
                }
                return new BatchRenderStats(visible, skipped, draws, draws, lodDraws);
            }

            // River projection lights need the far decks below them as shadow casters (see NeedsProjectionShadows).
            // Only a few chunks ever qualify; they cast from the CPU copy of their transforms.
            private int SubmitProjectionShadows(CulledBatch batch, Camera camera, int layer)
            {
                if (!RiverGlassRenderRegistry.HasProjectionShadowLights) return 0;
                int draws = 0;
                foreach (InstanceRange range in batch.Ranges.Values)
                {
                    if (!RiverGlassRenderRegistry.NeedsProjectionShadows(range.Bounds, camera, layer)) continue;
                    var parameters = new RenderParams(batch.Source.Material)
                    {
                        layer = layer, camera = camera, worldBounds = range.Bounds,
                        shadowCastingMode = ShadowCastingMode.ShadowsOnly, receiveShadows = false,
                        reflectionProbeUsage = ReflectionProbeUsage.Off, lightProbeUsage = LightProbeUsage.Off,
                        motionVectorMode = MotionVectorGenerationMode.ForceNoMotion
                    };
                    for (int start = 0; start < range.Count; start += MaximumInstancesPerDraw)
                    {
                        Graphics.RenderMeshInstanced(parameters, batch.Source.Mesh, 0, objectToWorld,
                            Mathf.Min(MaximumInstancesPerDraw, range.Count - start), range.Start + start);
                        draws++;
                    }
                }
                return draws;
            }

            /// <summary>Culls every instance for <paramref name="camera"/>; the indirect draws then read the result.</summary>
            public void Cull(Camera camera, FarOccluderSet occluders)
            {
                Flush();
                if (gpuArgs == null || gpuVisible == null || gpuBatches == null || gpuInstances == null || gpuMarks == null || HighWater == 0 || commands.Count == 0) return;
                cmd.Clear();
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                for (int i = 0; i < 6; i++)
                    planeVectors[i] = new Vector4(planes[i].normal.x, planes[i].normal.y, planes[i].normal.z, planes[i].distance);
                // Sight lines meet at the eye only in a perspective view.
                int decks = SyncDecks(occluders != null && !camera.orthographic ? occluders : null);
                // Every command starts the camera with zero instances.
                cmd.SetBufferData(gpuArgs, argsTemplate, 0, 0, commands.Count);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, InstancesId, gpuInstances);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, BatchesId, gpuBatches);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, CommandBaseId, gpuCommandBase);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, ArgsId, gpuArgs);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, VisibleId, gpuVisible);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, DecksId, gpuDecks);
                cmd.SetComputeBufferParam(cullingShader, cullKernel, MarksId, gpuMarks);
                cmd.SetComputeTextureParam(cullingShader, cullKernel, DeckMapsId, deckMaps);
                cmd.SetComputeVectorArrayParam(cullingShader, PlanesId, planeVectors);
                cmd.SetComputeVectorParam(cullingShader, CameraPositionId, camera.transform.position);
                cmd.SetComputeVectorParam(cullingShader, CullParamsId, new Vector4(HighWater, MeshLodEnabled ? 1 : 0, 0, 0));
                // A deck closer than this may be cut by the near plane anywhere in the view; it is not trusted.
                cmd.SetComputeVectorParam(cullingShader, OcclusionParamsId,
                    new Vector4(decks, FarOccluderSet.MapSize, Mathf.Max(1f, camera.nearClipPlane * 4f), 0));
                cmd.DispatchCompute(cullingShader, cullKernel, (HighWater + 63) / 64, 1, 1);
                if (rangeCount > 0 && gpuRanges != null)
                {
                    // Transparent batches: count each chunk's visible instances, then write them in chunk order.
                    cmd.SetComputeVectorParam(cullingShader, OrderedParamsId, new Vector4(rangeCount, 0, 0, 0));
                    cmd.SetComputeBufferParam(cullingShader, countKernel, RangesId, gpuRanges);
                    cmd.SetComputeBufferParam(cullingShader, countKernel, MarksId, gpuMarks);
                    cmd.SetComputeBufferParam(cullingShader, countKernel, RangeCountsId, gpuRangeCounts);
                    cmd.DispatchCompute(cullingShader, countKernel, (rangeCount + 63) / 64, 1, 1);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, RangesId, gpuRanges);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, MarksId, gpuMarks);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, RangeCountsId, gpuRangeCounts);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, BatchesId, gpuBatches);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, CommandBaseId, gpuCommandBase);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, ArgsId, gpuArgs);
                    cmd.SetComputeBufferParam(cullingShader, writeKernel, VisibleId, gpuVisible);
                    cmd.DispatchCompute(cullingShader, writeKernel, (rangeCount + 63) / 64, 1, 1);
                }
                Graphics.ExecuteCommandBuffer(cmd);
            }

            /// <summary>
            /// Uploads deck maps that changed and the list of occluding decks; returns how many decks occlude.
            /// Null (occlusion off) still leaves valid, unused resources bound.
            /// </summary>
            private int SyncDecks(FarOccluderSet occluders)
            {
                int slices = Mathf.Max(1, occluders != null ? occluders.SliceCount : 0);
                if (deckMaps == null || deckMaps.volumeDepth < slices)
                {
                    if (deckMaps != null) { deckMaps.Release(); DestroyOwned(deckMaps); }
                    // Whole floors at a time, so the array is rarely rebuilt as the tower grows.
                    int depth = Mathf.Max(8, (slices + 7) / 8 * 8);
                    deckMaps = new RenderTexture(FarOccluderSet.MapSize, FarOccluderSet.MapSize, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
                    {
                        name = "Far deck maps", dimension = TextureDimension.Tex2DArray, volumeDepth = depth, filterMode = FilterMode.Point,
                        useMipMap = false, hideFlags = HideFlags.HideAndDontSave
                    };
                    deckMaps.Create();
                    sliceVersions = new int[depth];
                    syncedOccluders = -1;
                }
                if (gpuDecks == null)
                {
                    gpuDecks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 64, FarDeckData.Size) { name = "Far decks" };
                    deckData = new FarDeckData[64];
                    gpuDecks.SetData(deckData);
                }
                if (occluders == null || (SystemInfo.copyTextureSupport & CopyTextureSupport.TextureToRT) == 0) return 0;
                if (syncedOccluders == occluders.Version) return deckCount;
                syncedOccluders = occluders.Version;
                IReadOnlyList<FarOccluderSet.FloorMap> floors = occluders.Floors;
                if (deckData.Length < floors.Count)
                {
                    deckData = new FarDeckData[Mathf.NextPowerOfTwo(floors.Count)];
                    gpuDecks.Release();
                    gpuDecks = new GraphicsBuffer(GraphicsBuffer.Target.Structured, deckData.Length, FarDeckData.Size) { name = "Far decks" };
                }
                deckCount = 0;
                for (int i = 0; i < floors.Count; i++)
                {
                    FarOccluderSet.FloorMap map = floors[i];
                    if (!map.HasSolid) continue;
                    if (sliceVersions[map.Slice] != map.Version)
                    {
                        tableScratch ??= new float[FarOccluderSet.MapSize * FarOccluderSet.MapSize];
                        if (mapScratch == null)
                            mapScratch = new Texture2D(FarOccluderSet.MapSize, FarOccluderSet.MapSize, TextureFormat.RFloat, false, true)
                            { name = "Far deck map upload", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                        occluders.FillSummedArea(map, tableScratch);
                        mapScratch.SetPixelData(tableScratch, 0);
                        mapScratch.Apply(false, false);
                        Graphics.CopyTexture(mapScratch, 0, 0, deckMaps, map.Slice, 0);
                        sliceVersions[map.Slice] = map.Version;
                    }
                    deckData[deckCount++] = new FarDeckData
                    {
                        planeY = map.PlaneY, originX = map.Origin.x, originZ = map.Origin.y, texelsPerMetre = 1f / map.TexelSize, slice = map.Slice
                    };
                }
                if (deckCount > 0) gpuDecks.SetData(deckData, 0, 0, deckCount);
                return deckCount;
            }

            private Bounds FloorBounds(CulledBatch batch, int floor, FloorList list)
            {
                if (!list.BoundsDirty) return list.Bounds;
                bool first = true;
                foreach (KeyValuePair<ChunkKey, InstanceRange> pair in batch.Ranges)
                {
                    if (pair.Key.FloorIndex != floor) continue;
                    if (first) { list.Bounds = pair.Value.Bounds; first = false; }
                    else list.Bounds.Encapsulate(pair.Value.Bounds);
                }
                list.BoundsDirty = false;
                return list.Bounds;
            }

            /// <summary>One indirect composition draw per floor with far river tiles in view.</summary>
            public void CollectRiverGlass(TypeBatch source, Camera camera, Plane[] frustum, List<RiverGlassDraw> draws, float maximumDistance)
            {
                if (!batchLookup.TryGetValue(source, out CulledBatch batch) || batch.Floors == null || batch.Count == 0) return;
                Flush();
                if (gpuArgs == null) return;
                Vector3 eye = camera.transform.position;
                foreach (KeyValuePair<int, FloorList> pair in batch.Floors)
                {
                    FloorList floor = pair.Value;
                    if (floor.Count <= 0 || floor.Command < 0) continue;
                    Bounds bounds = FloorBounds(batch, pair.Key, floor);
                    if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, bounds)) continue;
                    if (maximumDistance > 0f && bounds.SqrDistance(eye) > maximumDistance * maximumDistance) continue;
                    RiverGlassDraw draw = floor.Draw ??= new RiverGlassDraw();
                    if (floor.DrawVersion != bindingVersion)
                    {
                        draw.mesh = source.Mesh; draw.matrices = null; draw.floor = pair.Key; draw.height = pair.Key * source.FloorHeight;
                        draw.center = source.FloorCenter; draw.radial = source.Radial;
                        draw.indirectArguments = gpuArgs;
                        draw.indirectOffset = floor.Command * GraphicsBuffer.IndirectDrawIndexedArgs.size;
                        draw.Configure();
                        draw.properties.SetBuffer(FarObjectToWorld, gpuObjectToWorld);
                        draw.properties.SetBuffer(FarWorldToObject, gpuWorldToObject);
                        draw.properties.SetBuffer(FarVisible, gpuVisible);
                        draw.properties.SetFloat(FarBase, commandBases[floor.Command]);
                        draw.properties.SetFloat(FarCulled, 1f);
                        floor.DrawVersion = bindingVersion;
                    }
                    draw.count = floor.Count; draw.bounds = bounds;
                    draws.Add(draw);
                }
            }

            public int SubmitReflection(TypeBatch source, Camera camera, Vector3 eye, Plane[] frustum, int selectedFloor, float range, int layer)
            {
                source.LastReflectionDrawCalls = 0;
                if (!batchLookup.TryGetValue(source, out CulledBatch batch) || batch.Count == 0) return 0;
                Matrix4x4[] matrices = source.ReflectionMatrices;
                float[] floors = source.ReflectionFloors;
                int count = 0, total = 0;
                float rangeSq = range * range;
                Bounds bounds = default;
                foreach (KeyValuePair<ChunkKey, InstanceRange> pair in batch.Ranges)
                {
                    int floorIndex = pair.Key.FloorIndex;
                    if (floorIndex != selectedFloor && floorIndex != selectedFloor + 1) continue;
                    InstanceRange chunk = pair.Value;
                    if (chunk.Bounds.SqrDistance(eye) > rangeSq || !GeometryUtility.TestPlanesAABB(frustum, chunk.Bounds)) continue;
                    for (int i = chunk.Start; i < chunk.Start + chunk.Count; i++)
                    {
                        var box = new Bounds(instances[i].center, instances[i].extents * 2f);
                        if (box.SqrDistance(eye) > rangeSq || !GeometryUtility.TestPlanesAABB(frustum, box)) continue;
                        if (count == 0) bounds = box; else bounds.Encapsulate(box);
                        matrices[count] = objectToWorld[i]; floors[count++] = floorIndex;
                        if (count == MaximumInstancesPerDraw) { source.FlushReflection(camera, layer, count, bounds); total += count; count = 0; }
                    }
                }
                if (count > 0) { source.FlushReflection(camera, layer, count, bounds); total += count; }
                return total;
            }

            public int ReadVisibleCounts(out int full, out int lod1, out int lod2)
            {
                full = lod1 = lod2 = 0;
                if (gpuArgs == null || commands.Count == 0) return 0;
                var data = new GraphicsBuffer.IndirectDrawIndexedArgs[commands.Count];
                gpuArgs.GetData(data, 0, 0, commands.Count);
                foreach (CulledBatch batch in batchList)
                {
                    if (batch.Commands[0] < 0) continue;
                    full += (int)data[batch.Commands[0]].instanceCount;
                    if (batch.Commands[1] != batch.Commands[0]) lod1 += (int)data[batch.Commands[1]].instanceCount;
                    if (batch.Commands[2] != batch.Commands[0]) lod2 += (int)data[batch.Commands[2]].instanceCount;
                }
                return full + lod1 + lod2;
            }

            public void ReadVisibleCenters(TypeBatch source, int level, List<Vector3> centers)
            {
                if (gpuArgs == null || gpuVisible == null || commands.Count == 0) return;
                if (!batchLookup.TryGetValue(source, out CulledBatch batch) || batch.Commands[0] < 0) return;
                int command = batch.Commands[Mathf.Clamp(level, 0, 2)];
                if (level > 0 && command == batch.Commands[0]) return;
                var data = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
                gpuArgs.GetData(data, 0, command, 1);
                int count = (int)data[0].instanceCount;
                if (count == 0) return;
                var visible = new uint[count];
                gpuVisible.GetData(visible, 0, (int)commandBases[command], count);
                foreach (uint index in visible) centers.Add(instances[index].center);
            }

            public void Dispose()
            {
                gpuObjectToWorld?.Release(); gpuWorldToObject?.Release(); gpuInstances?.Release(); gpuBatches?.Release();
                gpuCommandBase?.Release(); gpuArgs?.Release(); gpuVisible?.Release(); gpuDecks?.Release();
                gpuMarks?.Release(); gpuRanges?.Release(); gpuRangeCounts?.Release();
                gpuMarks = gpuRanges = gpuRangeCounts = null;
                gpuObjectToWorld = gpuWorldToObject = gpuInstances = gpuBatches = gpuCommandBase = gpuArgs = gpuVisible = gpuDecks = null;
                cmd.Release();
                if (deckMaps != null) { deckMaps.Release(); DestroyOwned(deckMaps); deckMaps = null; }
                if (mapScratch != null) { DestroyOwned(mapScratch); mapScratch = null; }
                batchList.Clear(); batchLookup.Clear(); commands.Clear(); free.Clear();
                HighWater = 0;
            }

            private static void DestroyOwned(UnityEngine.Object value)
            {
                if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
            }
        }
    }
}
