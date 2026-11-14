using System;
using System.Collections.Generic;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Tower
{
    /// <summary>
    /// Lava lamps hung like torches on the tower's pillars: inside, one lamp then three bare pillars around every ring,
    /// plus lamps scattered on the outside of free pillars. A pillar carries at most one lamp (inside or outside), and
    /// two lamps never hang on neighbouring pillars. Each lamp is a buildable detail asset (TowerAssetType.LavaLamp):
    /// the tower draws its chrome sconce stage by stage, and once it is fully built this component adds the lava glass
    /// and its light. Lamps are drawn instanced, so the tower carries no lamp objects. Every lamp whose light the
    /// player can see, up to a budget, carries a real spotlight projecting one shared, animated caustics cookie.
    ///
    /// The lights never switch at full brightness. The nearest lamps win; when there are more lamps than lights the
    /// lit radius shrinks to what the budget covers and lights fade out toward its edge, a light that is handed to
    /// another lamp fades out first and fades in there, and lamps hidden behind the tower's decks take no light.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerLavaLamps : MonoBehaviour
    {
        /// <summary>A lamp on one pillar, then three pillars without.</summary>
        public const int PillarInterval = 4;
        /// <summary>Seconds between light selections while the camera is still; a moving camera selects at once.</summary>
        public const float SelectionInterval = .1f;
        /// <summary>Most light rigs the budget can ask for. Each is a spotlight, plus a small glow near the player.</summary>
        public const int MaximumLights = 128;
        /// <summary>Seconds a light takes to fade in on a lamp, or out before it moves to another.</summary>
        public const float FadeSeconds = .3f;
        /// <summary>The small glow under a lamp is only lit this close; beyond it only the spotlight shows.</summary>
        public const float GlowReach = 40;
        /// <summary>Degrees added to the camera's field of view when choosing lamps, so turning finds them already lit.</summary>
        public const float ViewPadding = 30;
        // The camera moved or turned this much since the last selection: select again now.
        private const float ReselectDistance = 1.5f, ReselectAngle = 3, TeleportDistance = 25;
        // How fast the lit radius follows the budget, per second.
        private const float ReachRate = 5;
        public const int CookieResolution = 512;
        public const float CookieInterval = 1 / 30f;

        [SerializeField] private ChunkManager chunks;
        [Tooltip("The lava lamp sconce. Its origin is the point where the wall plate meets the pillar.")]
        [SerializeField] private GameObject lampPrefab;
        [Tooltip("Height of the sconce arm above the pillar base, just under the capital where the arches spring.")]
        [SerializeField, Min(0)] private float mountHeight = 3.6f;
        [Tooltip("Hang the sconces on the inner face of the pillars, over the deck and stair ramp, instead of the facade.")]
        [SerializeField] private bool interior = true;
        [Tooltip("Pillar shaft radius at the sconce. The wall plate sits on this surface.")]
        [SerializeField, Min(0)] private float pillarRadius = .38f;
        [SerializeField, Min(10)] private float visibleDistance = 260;
        [Tooltip("Lamps in view within this distance carry a live spotlight; the light fades out over the last quarter.")]
        [SerializeField, Min(1)] private float lightReach = 90;
        [Tooltip("Most lamp spotlights at once. Forward+ has no per-object light limit; the nearest lamps in view win. " +
                 "With more lamps in view than lights, the lit radius shrinks and the lights fade out toward its edge.")]
        [SerializeField, Range(0, MaximumLights)] private int maxLights = 96;
        [Tooltip("The nearest lamp within this distance casts spotlight shadows; they fade out over the last quarter.")]
        [SerializeField, Min(0)] private float shadowDistance = 20;
        [Tooltip("Chance that a free pillar (no lamp on it or on either neighbour) gets a lamp on its other face.")]
        [SerializeField, Range(0, 1)] private float exteriorChance = .5f;
        [SerializeField] private int placementSeed = 1;
        [Tooltip("Spotlight strength during the day, relative to night.")]
        [SerializeField, Range(0, 1)] private float daytimeLight = .3f;
        [SerializeField, Min(0)] private float stairClearance = .08f;
        [Tooltip("Free height kept above a stair under a lamp. 0 only skips lamps that touch the steps; 2 also keeps a " +
                 "climber's head clear, but leaves long bare stretches where the ramp winds past the pillars.")]
        [SerializeField, Min(0)] private float stairHeadroom;

        [Serializable]
        public struct Placement
        {
            public ChunkKey key;
            public int pillar, floor;
            /// <summary>Position of the pillar in its ring, ordered by angle.</summary>
            public int ringIndex;
            /// <summary>Hangs on the outside face of the pillar.</summary>
            public bool exterior;
            public Matrix4x4 mount;
            /// <summary>The buildable lamp asset hung here; -1 when the tower has no lamp assets.</summary>
            public ChunkKey lampKey;
            public int lamp;
            public Vector3 Position => mount.GetColumn(3);
        }

        private readonly struct Part
        {
            public readonly Mesh Mesh; public readonly Material Material; public readonly Matrix4x4 Local; public readonly ShadowCastingMode Shadows;
            /// <summary>The tower draws this part itself, as the finished stage of the buildable lamp.</summary>
            public readonly bool DrawnByTower;
            public Part(Mesh mesh, Material material, Matrix4x4 local, ShadowCastingMode shadows, bool drawnByTower)
            { Mesh = mesh; Material = material; Local = local; Shadows = shadows; DrawnByTower = drawnByTower; }
        }

        private sealed class FloorBatch
        {
            public int floor;
            public readonly List<int> placements = new();
            public Matrix4x4[][] matrices;
            public int visible;
            public Bounds bounds;
            public bool dirty = true;
        }

        private sealed class Rig
        {
            public GameObject root; public Light[] lights; public float[] intensities; public int placement = -1;
            public Light spot; public Quaternion spotRotation;
            /// <summary>Distance fade toward the edge of the lit radius, and of the glow's own reach.</summary>
            public float fade = 1, glow = 1;
            /// <summary>Fade in time: 0 when the rig arrives on a lamp, 1 once it is fully lit.</summary>
            public float presence;
            /// <summary>The lamp lost its light; the rig fades out, then is free.</summary>
            public bool leaving;
            /// <summary>Shadow strength now (0 to 1) and as authored.</summary>
            public float shadow, shadowStrength = 1;
            /// <summary>The lamp's position while the rig sits on one.</summary>
            public Vector3 position;
            // What the lights were last set to: a frame that changes nothing touches no light.
            public float[] applied; public bool[] switchedOn;
            public LightShadows shadowMode = LightShadows.None; public float appliedShadowStrength = -1;
        }

        private readonly List<Placement> placements = new();
        private readonly List<Part> parts = new();
        private readonly Dictionary<int, FloorBatch> floors = new();
        private readonly List<FloorBatch> batchList = new();
        private readonly List<Rig> rigs = new();
        private readonly Plane[] planes = new Plane[6];
        // Light selection scratch: lamps whose light can be seen, sorted by distance; chosen lamps still without a rig.
        private float[] candidateDistances = new float[64];
        private int[] candidates = new int[64];
        private int candidateCount;
        private readonly List<int> pending = new();
        [NonSerialized] private float reach, targetReach;
        [NonSerialized] private int shadowLamp = -1;
        [NonSerialized] private bool selectedOnce;
        [NonSerialized] private Vector3 selectedEye;
        [NonSerialized] private Quaternion selectedLook = Quaternion.identity;
        // Runtime state only: an editor script reload must not restore "already built" without the lists.
        [NonSerialized] private Bounds lampBounds;
        [NonSerialized] private int builtFromChunks = -1;
        [NonSerialized] private bool subscribed;
        [NonSerialized] private float nextSelection, nextCookie;
        [NonSerialized] private RenderTexture cookie;
        [NonSerialized] private Material cookieAnimation;

        public IReadOnlyList<Placement> Placements { get { EnsureBuilt(); return placements; } }
        public GameObject LampPrefab => lampPrefab;
        public ChunkManager Chunks => chunks;
        public float MountHeight => mountHeight;
        public float PillarRadius => pillarRadius;
        public bool Interior => interior;
        public float ExteriorChance => exteriorChance;
        public int PlacementSeed => placementSeed;
        public float LightReach => lightReach;
        public int MaxLights => maxLights;
        /// <summary>Most lamp spotlights at once (graphics settings). Extra rigs are released; missing ones are created on the next selection.</summary>
        public int LightBudget
        {
            get => maxLights;
            set
            {
                value = Mathf.Clamp(value, 0, MaximumLights);
                if (value == maxLights) return;
                maxLights = value;
                for (int i = rigs.Count - 1; i >= maxLights; i--)
                {
                    if (rigs[i].root != null) { if (Application.isPlaying) Destroy(rigs[i].root); else DestroyImmediate(rigs[i].root); }
                    rigs.RemoveAt(i);
                }
                nextSelection = 0;
            }
        }
        /// <summary>The one animated caustics cookie every lamp spotlight projects.</summary>
        public RenderTexture SharedCookie => cookie;
        public int DrawnBatchCount { get; private set; }
        public int VisibleLampCount { get { int n = 0; foreach (var b in batchList) { Refresh(b); n += b.visible; } return n; } }
        public int ActiveLightCount { get { int n = 0; foreach (var r in rigs) if (r.root != null && r.root.activeSelf) n++; return n; } }
        /// <summary>
        /// The lit radius now: the light reach, or less while more lamps are in view than the budget has lights.
        /// </summary>
        public float EffectiveReach => reach > 0 ? reach : lightReach;
        /// <summary>Lamps the last selection found lit-worthy (in reach, in view, not behind a deck), before the budget.</summary>
        public int CandidateCount => candidateCount;
        public IEnumerable<Light> Lights { get { foreach (var r in rigs) foreach (var l in r.lights) if (l != null) yield return l; } }

        public void Configure(ChunkManager manager, GameObject prefab, float height, float radius)
        {
            chunks = manager; lampPrefab = prefab; mountHeight = height; pillarRadius = radius;
            Unsubscribe(); builtFromChunks = -1; ReleaseRigs();
            if (isActiveAndEnabled) { Subscribe(); Rebuild(); }
        }

        /// <summary>Sets how often free pillars get a lamp on their other face, and the random pattern, then rebuilds.</summary>
        public void SetExterior(float chance, int seed = 1)
        {
            exteriorChance = Mathf.Clamp01(chance); placementSeed = seed;
            builtFromChunks = -1; ReleaseRigs();
            if (chunks != null) { Subscribe(); Rebuild(); }
        }

        private void OnEnable() { Subscribe(); builtFromChunks = -1; selectedOnce = false; reach = targetReach = 0; }
        private void OnDisable() { Unsubscribe(); ReleaseRigs(); }

        private void Subscribe()
        {
            if (subscribed || chunks == null) return;
            chunks.AssetStageChanged += OnStageChanged;
            chunks.ConstructionFloorChanged += MarkAllDirty;
            chunks.DetailReleaseChanged += OnDetailReleaseChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || chunks == null) { subscribed = false; return; }
            chunks.AssetStageChanged -= OnStageChanged;
            chunks.ConstructionFloorChanged -= MarkAllDirty;
            chunks.DetailReleaseChanged -= OnDetailReleaseChanged;
            subscribed = false;
        }

        private void OnStageChanged(ChunkKey key, int index, byte stage)
        {
            if (floors.TryGetValue(key.FloorIndex, out var batch)) batch.dirty = true;
        }

        private void MarkAllDirty() { foreach (var b in batchList) b.dirty = true; }

        private void OnDetailReleaseChanged(int floor)
        {
            if (floors.TryGetValue(floor, out var batch)) batch.dirty = true;
        }

        /// <summary>The placement rules: where on a pillar a lamp hangs, and which pillars carry one.</summary>
        public readonly struct Layout
        {
            public readonly float MountHeight, PillarRadius, ExteriorChance, StairClearance, StairHeadroom;
            public readonly bool Interior;
            public readonly int Seed;
            public Layout(float mountHeight, float pillarRadius, bool interior, float exteriorChance, int seed, float stairClearance, float stairHeadroom)
            {
                MountHeight = mountHeight; PillarRadius = pillarRadius; Interior = interior; ExteriorChance = exteriorChance;
                Seed = seed; StairClearance = stairClearance; StairHeadroom = stairHeadroom;
            }
        }

        public Layout CurrentLayout => new(mountHeight, pillarRadius, interior, exteriorChance, placementSeed, stairClearance, stairHeadroom);
        /// <summary>The lamps come from buildable lamp assets in the chunk cache, and each lights up once it is built.</summary>
        public bool UsesLampAssets { get; private set; }

        /// <summary>
        /// Takes the lamps from the buildable lamp assets in the chunk cache when there are any; otherwise places them on
        /// the pillars (see <see cref="Place"/>) and shows each with its pillar.
        /// </summary>
        public void Rebuild()
        {
            placements.Clear(); floors.Clear(); batchList.Clear(); parts.Clear(); UsesLampAssets = false;
            // The lights point at placements by index: hand them all back.
            foreach (var rig in rigs) if (rig.root != null) Release(rig);
            pending.Clear(); candidateCount = 0; shadowLamp = -1; selectedOnce = false;
            builtFromChunks = chunks != null ? chunks.CachedChunks.Count : -1;
            if (chunks == null || lampPrefab == null) return;
            CollectParts();
            var structure = new List<(ChunkKey key, ChunkAssetData asset, int floor)>();
            var lamps = new List<(ChunkKey key, ChunkAssetData asset)>();
            foreach (var chunk in chunks.CachedChunks)
                foreach (var asset in chunk.Assets)
                {
                    if (asset.AssetType == TowerAssetType.LavaLamp) lamps.Add((chunk.Key, asset));
                    else if (asset.AssetType == TowerAssetType.Pillar || asset.AssetType == TowerAssetType.Stair) structure.Add((chunk.Key, asset, chunk.Key.FloorIndex));
                }
            var computed = Place(structure, chunks.FloorCenter, StairBounds(), lampBounds, CurrentLayout);
            if (lamps.Count == 0)
            {
                foreach (var placement in computed) AddPlacement(placement);
            }
            else
            {
                // The built lamps are what counts. Match each to its spot on a pillar for the ring position and face.
                UsesLampAssets = true;
                var spots = new Dictionary<Vector3Int, Placement>();
                foreach (var placement in computed) spots[Spot(placement.Position)] = placement;
                Vector2 center = chunks.FloorCenter;
                foreach (var (key, lamp) in lamps)
                {
                    if (!spots.TryGetValue(Spot(lamp.Position), out var placement))
                    {
                        var outward = new Vector3(lamp.Position.x - center.x, 0, lamp.Position.z - center.y);
                        placement = new Placement { key = key, pillar = -1, floor = key.FloorIndex, ringIndex = -1,
                            exterior = Vector3.Dot(lamp.Rotation * Vector3.forward, outward) > 0 };
                    }
                    placement.mount = Matrix4x4.TRS(lamp.Position, lamp.Rotation, Vector3.one);
                    placement.lampKey = key; placement.lamp = lamp.LocalIndex;
                    AddPlacement(placement);
                }
            }
            foreach (var batch in batchList)
            {
                batch.matrices = new Matrix4x4[parts.Count][];
                for (int p = 0; p < parts.Count; p++) batch.matrices[p] = new Matrix4x4[batch.placements.Count];
            }
        }

        private static Vector3Int Spot(Vector3 position) => Vector3Int.RoundToInt(position * 100);

        /// <summary>
        /// The buildable lamp assets for a tower: one at every spot <see cref="Place"/> finds on the given pillars and
        /// stairs. <paramref name="floorOf"/> gives each asset's floor index.
        /// </summary>
        public List<ChunkAssetData> BuildLampAssets(IEnumerable<ChunkAssetData> towerAssets, Func<ChunkAssetData, int> floorOf, Vector2 center, Bounds stairMesh)
        {
            var structure = new List<(ChunkKey, ChunkAssetData, int)>();
            foreach (var asset in towerAssets)
                if (asset.AssetType == TowerAssetType.Pillar || asset.AssetType == TowerAssetType.Stair) structure.Add((default, asset, floorOf(asset)));
            var result = new List<ChunkAssetData>();
            foreach (var placement in Place(structure, center, stairMesh, LampBounds(), CurrentLayout))
                result.Add(new ChunkAssetData(0, TowerAssetType.LavaLamp, placement.Position, placement.mount.rotation, Vector3.one));
            return result;
        }

        /// <summary>The lamp assets for the cached tower, placed by the current rules.</summary>
        public List<ChunkAssetData> BuildLampAssets()
        {
            var assets = new List<ChunkAssetData>();
            foreach (var chunk in chunks.CachedChunks) assets.AddRange(chunk.Assets);
            return BuildLampAssets(assets, a => chunks.WorldToChunk(a.Position).FloorIndex, chunks.FloorCenter, StairBounds());
        }

        /// <summary>
        /// Places a lamp on every fourth pillar of each ring (ordered by angle, clear of stairs), then scatters lamps on the
        /// other face of pillars whose neighbours are both bare. A pillar never carries two lamps.
        /// </summary>
        public static List<Placement> Place(IEnumerable<(ChunkKey key, ChunkAssetData asset, int floor)> structure, Vector2 center,
            Bounds stairMesh, Bounds lampBounds, Layout layout)
        {
            var result = new List<Placement>();
            var pillars = new SortedDictionary<int, List<(ChunkKey key, ChunkAssetData asset)>>();
            var stairs = new Dictionary<int, List<ChunkAssetData>>();
            foreach (var (key, asset, floor) in structure)
            {
                if (asset.AssetType == TowerAssetType.Pillar)
                {
                    if (!pillars.TryGetValue(floor, out var list)) pillars[floor] = list = new();
                    list.Add((key, asset));
                }
                else if (asset.AssetType == TowerAssetType.Stair)
                {
                    if (!stairs.TryGetValue(floor, out var list)) stairs[floor] = list = new();
                    list.Add(asset);
                }
            }
            float Angle(Vector3 p) => Mathf.Repeat(Mathf.Atan2(p.z - center.y, p.x - center.x), Mathf.PI * 2);
            foreach (var pair in pillars)
            {
                var ring = pair.Value;
                ring.Sort((a, b) => Angle(a.asset.Position).CompareTo(Angle(b.asset.Position)));
                int n = ring.Count;
                var occupied = new bool[n];
                bool Free(int i) => !occupied[i] && !occupied[(i + 1) % n] && !occupied[(i + n - 1) % n];
                void TryPlace(int i, bool outside)
                {
                    var (key, pillar) = ring[i];
                    var outward = new Vector3(pillar.Position.x - center.x, 0, pillar.Position.z - center.y);
                    if (outward.sqrMagnitude < 1e-6f) return;
                    outward.Normalize();
                    var facing = outside ? outward : -outward;
                    var mount = Matrix4x4.TRS(pillar.Position + Vector3.up * layout.MountHeight + facing * layout.PillarRadius,
                        Quaternion.LookRotation(facing, Vector3.up), Vector3.one);
                    if (TouchesStairs(mount, pair.Key, stairs, stairMesh, lampBounds, layout)) return;
                    result.Add(new Placement { key = key, pillar = pillar.LocalIndex, floor = pair.Key, ringIndex = i, exterior = outside, mount = mount, lamp = -1 });
                    occupied[i] = true;
                }
                // The regular pattern. A ring that does not divide by four skips its last lamp if it would sit next to the first.
                for (int i = 0; i < n; i += PillarInterval)
                    if (Free(i)) TryPlace(i, !layout.Interior);
                // Random lamps on the other face, only where the pillar and both its neighbours are bare.
                if (layout.ExteriorChance > 0)
                    for (int i = 0; i < n; i++)
                        if (Free(i) && Hash01(pair.Key, i, layout.Seed) < layout.ExteriorChance) TryPlace(i, layout.Interior);
            }
            return result;
        }

        /// <summary>Stable 0..1 value per floor, pillar and seed, so scattered lamps never move between sessions.</summary>
        public static float Hash01(int floor, int pillar, int seed)
        {
            unchecked
            {
                uint h = (uint)(floor * 73856093) ^ (uint)(pillar * 19349663) ^ (uint)(seed * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; h *= 0x27d4eb2d; h ^= h >> 16;
                return (h & 0xffffff) / 16777216f;
            }
        }

        private void AddPlacement(Placement placement)
        {
            int index = placements.Count;
            placements.Add(placement);
            if (!floors.TryGetValue(placement.floor, out var batch))
            {
                floors[placement.floor] = batch = new FloorBatch { floor = placement.floor, bounds = WorldBounds(placement.mount) };
                batchList.Add(batch);
            }
            batch.placements.Add(index);
            batch.bounds.Encapsulate(WorldBounds(placement.mount));
        }

        private void CollectParts()
        {
            var root = lampPrefab.transform;
            var inverse = root.worldToLocalMatrix;
            bool any = false;
            // With buildable lamps, the tower already draws the chrome sconce as the lamp's finished stage.
            // Unity-null checks: an unassigned serialized prefab is not C# null.
            var lampAsset = chunks != null && chunks.AssetPrefabs != null ? chunks.AssetPrefabs.GetPrefab(TowerAssetType.LavaLamp) : null;
            var building = lampAsset != null ? lampAsset.GetComponent<Building>() : null;
            var drawnByTower = building != null ? building.GetStageMesh(ChunkAssetData.CompletedStage) : null;
            foreach (var renderer in lampPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                var local = inverse * renderer.transform.localToWorldMatrix;
                parts.Add(new Part(filter.sharedMesh, renderer.sharedMaterial, local, renderer.shadowCastingMode, filter.sharedMesh == drawnByTower));
                var b = TransformBounds(filter.sharedMesh.bounds, local);
                if (!any) { lampBounds = b; any = true; } else lampBounds.Encapsulate(b);
            }
        }

        /// <summary>Bounds of the whole lamp in sconce space.</summary>
        public Bounds LampBounds()
        {
            if (lampPrefab == null) return new Bounds();
            var inverse = lampPrefab.transform.worldToLocalMatrix;
            Bounds bounds = default; bool any = false;
            foreach (var filter in lampPrefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;
                var b = TransformBounds(filter.sharedMesh.bounds, inverse * filter.transform.localToWorldMatrix);
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }
            return bounds;
        }

        private Bounds StairBounds()
        {
            var prefab = chunks.AssetPrefabs?.GetPrefab(TowerAssetType.Stair);
            if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
            var mesh = prefab.GetComponent<Building>()?.GetStageMesh(ChunkAssetData.CompletedStage) ?? prefab.GetComponent<MeshFilter>()?.sharedMesh;
            return mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        // A stair passing the pillar would run through a hanging lamp, or someone climbing it would: leave that pillar bare.
        private static bool TouchesStairs(Matrix4x4 mount, int floor, Dictionary<int, List<ChunkAssetData>> stairs, Bounds stairMesh, Bounds lampBounds, Layout layout)
        {
            if (stairMesh.size == Vector3.zero) return false;
            Vector3 origin = mount.GetColumn(3);
            for (int f = floor - 1; f <= floor + 1; f++)
            {
                if (!stairs.TryGetValue(f, out var list)) continue;
                foreach (var stair in list)
                {
                    if ((stair.Position - origin).sqrMagnitude > 64) continue;
                    var inverse = Quaternion.Inverse(stair.Rotation);
                    Vector3 c = Vector3.Scale(stairMesh.center, stair.Scale), e = Vector3.Scale(stairMesh.extents, stair.Scale) + Vector3.one * layout.StairClearance;
                    // Stairs only turn about Y, so the walking space is the step box raised by the headroom.
                    c.y += layout.StairHeadroom * .5f; e.y += layout.StairHeadroom * .5f;
                    for (int x = 0; x <= 2; x++) for (int y = 0; y <= 4; y++) for (int z = 0; z <= 2; z++)
                    {
                        var local = lampBounds.min + Vector3.Scale(lampBounds.size, new Vector3(x * .5f, y * .25f, z * .5f));
                        Vector3 p = inverse * (mount.MultiplyPoint3x4(local) - stair.Position) - c;
                        if (Mathf.Abs(p.x) < e.x && Mathf.Abs(p.y) < e.y && Mathf.Abs(p.z) < e.z) return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A buildable lamp shows once it is fully built and its floor's detail is released; without lamp assets, a lamp
        /// shows once its pillar is complete. Only on floors the player can see.
        /// </summary>
        public bool IsShown(int placement)
        {
            var p = placements[placement];
            if (chunks == null || !chunks.IsFloorVisible(p.floor)) return false;
            if (p.lamp >= 0)
                return chunks.IsAssetReleased(p.floor, TowerAssetType.LavaLamp) &&
                       chunks.TryGetAsset(p.lampKey, p.lamp, out var lamp) && lamp.Stage >= ChunkAssetData.CompletedStage;
            return chunks.TryGetAsset(p.key, p.pillar, out var pillar) && pillar.Stage >= ChunkAssetData.CompletedStage;
        }

        private void EnsureBuilt()
        {
            if (chunks != null && builtFromChunks != chunks.CachedChunks.Count) { Subscribe(); Rebuild(); }
        }

        private void Refresh(FloorBatch batch)
        {
            if (!batch.dirty) return;
            batch.visible = 0;
            foreach (int index in batch.placements)
            {
                if (!IsShown(index)) continue;
                var mount = placements[index].mount;
                for (int p = 0; p < parts.Count; p++) batch.matrices[p][batch.visible] = mount * parts[p].Local;
                batch.visible++;
            }
            batch.dirty = false;
        }

        private void LateUpdate()
        {
#if !UNITY_SERVER
            if (!Application.isPlaying) return;
            var camera = Camera.main;
            if (camera == null) return;
            EnsureBuilt();
            Vector3 eye = camera.transform.position;
            Quaternion look = camera.transform.rotation;
            float travelled = (eye - selectedEye).magnitude;
            // A moving or turning camera selects twice as often; the padded view covers the turn in between.
            bool moved = travelled > ReselectDistance || Quaternion.Angle(look, selectedLook) > ReselectAngle;
            if (!selectedOnce || travelled > TeleportDistance || Time.unscaledTime >= nextSelection - (moved ? SelectionInterval * .5f : 0))
            {
                LightFrustum(camera, lightReach, planes);
                // The first selection and a teleport light the lamps at once; from then on lights fade.
                SelectLights(eye, planes, selectedOnce && travelled < TeleportDistance, chunks != null ? chunks.FarOccluders : null);
                nextSelection = Time.unscaledTime + SelectionInterval;
                selectedOnce = true; selectedEye = eye; selectedLook = look;
            }
            Advance(eye, Time.unscaledDeltaTime);
            ApplyDaylight(Shader.GetGlobalFloat("_AeroDaylight"));
            if (AnyLit() && Time.time >= nextCookie)
            {
                RenderCookie(Time.time);
                nextCookie = Time.time + CookieInterval;
            }
            Draw(camera);
#endif
        }

        public void Draw(Camera camera)
        {
            DrawnBatchCount = 0;
            if (camera == null) return;
            EnsureBuilt();
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
            Vector3 eye = camera.transform.position;
            foreach (var batch in batchList)
            {
                if (batch.bounds.SqrDistance(eye) > visibleDistance * visibleDistance || !GeometryUtility.TestPlanesAABB(planes, batch.bounds)) continue;
                Refresh(batch);
                if (batch.visible == 0) continue;
                for (int p = 0; p < parts.Count; p++)
                {
                    var part = parts[p];
                    if (UsesLampAssets && part.DrawnByTower) continue;
                    var settings = new RenderParams(part.Material)
                    {
                        camera = camera, worldBounds = batch.bounds, layer = gameObject.layer,
                        shadowCastingMode = part.Shadows, receiveShadows = true, lightProbeUsage = LightProbeUsage.Off,
                        reflectionProbeUsage = ReflectionProbeUsage.BlendProbes
                    };
                    for (int start = 0; start < batch.visible; start += 1023)
                        Graphics.RenderMeshInstanced(settings, part.Mesh, 0, batch.matrices[p], Mathf.Min(1023, batch.visible - start), start);
                }
                DrawnBatchCount++;
            }
        }

        /// <summary>
        /// Moves the light rigs onto the nearest shown lamps within reach whose light could be seen: inside the view
        /// frustum when one is given (the spotlight's wash above the lamp counts), up to the light budget.
        /// </summary>
        /// <remarks>
        /// With <paramref name="smooth"/> (the game), a lamp that gains a light fades in and a light that leaves a
        /// lamp fades out first (see <see cref="Advance"/>); without it the lights are in place at once.
        /// Lamps whose light <paramref name="decks"/> hide from the view take no light.
        /// </remarks>
        public void SelectLights(Vector3 view, Plane[] frustum = null, bool smooth = false, FarOccluderSet decks = null)
        {
            EnsureBuilt();
            EnsureRigs();
            candidateCount = 0;
            float limit = lightReach * lightReach;
            foreach (var batch in batchList)
            {
                if (batch.bounds.SqrDistance(view) > limit) continue;
                if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, Expand(batch.bounds))) continue;
                foreach (int index in batch.placements)
                {
                    var placement = placements[index];
                    float d = (placement.Position - view).sqrMagnitude;
                    if (d > limit || !IsShown(index)) continue;
                    Bounds light = LightBounds(placement);
                    if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, light)) continue;
                    if (decks != null && decks.Hides(view, light)) continue;
                    if (candidateCount == candidates.Length)
                    {
                        Array.Resize(ref candidates, candidateCount * 2);
                        Array.Resize(ref candidateDistances, candidateCount * 2);
                    }
                    candidates[candidateCount] = index; candidateDistances[candidateCount++] = d;
                }
            }
            Array.Sort(candidateDistances, candidates, 0, candidateCount);

            // The nearest lamps get the lights. With more lamps than lights, the lit radius ends at the first lamp
            // left out, so the farthest lit lamp is already faded out when it loses its light.
            int budget = rigs.Count, chosen = Mathf.Min(budget, candidateCount);
            targetReach = candidateCount > budget ? Mathf.Min(lightReach, Mathf.Sqrt(candidateDistances[budget])) : lightReach;
            pending.Clear();
            for (int i = 0; i < chosen; i++) pending.Add(candidates[i]);
            // Rigs that already sit on a chosen lamp stay put, so their light does not jump.
            foreach (var rig in rigs)
            {
                if (rig.placement < 0) continue;
                if (pending.Remove(rig.placement)) rig.leaving = false;
                else if (smooth) rig.leaving = true;
                else Release(rig);
            }
            foreach (var rig in rigs)
            {
                if (rig.placement >= 0 || pending.Count == 0) continue;
                Assign(rig, pending[0], smooth ? 0 : 1);
                pending.RemoveAt(0);
            }
            // Only the nearest lamp casts spotlight shadows, as with the aqua lamps.
            shadowLamp = chosen > 0 && candidateDistances[0] <= shadowDistance * shadowDistance ? candidates[0] : -1;
            if (smooth) UpdateFades(view);
            else Advance(view, float.PositiveInfinity);
        }

        private void Assign(Rig rig, int placement, float presence)
        {
            rig.placement = placement; rig.presence = presence; rig.leaving = false; rig.shadow = 0;
            var p = placements[placement];
            rig.position = p.Position;
            rig.root.transform.SetPositionAndRotation(rig.position, p.mount.rotation);
            // Each lamp turns the shared cookie by its own angle, so neighbouring washes never repeat.
            if (rig.spot != null)
                rig.spot.transform.localRotation = rig.spotRotation * Quaternion.AngleAxis(Hash01(p.floor, p.ringIndex, placementSeed + 7) * 360, Vector3.forward);
            SetShadows(rig, LightShadows.None);
            if (!rig.root.activeSelf) rig.root.SetActive(true);
        }

        private static void Release(Rig rig)
        {
            rig.placement = -1; rig.presence = 0; rig.leaving = false; rig.shadow = 0; rig.fade = 0; rig.glow = 0;
            SetShadows(rig, LightShadows.None);
            if (rig.root.activeSelf) rig.root.SetActive(false);
        }

        private static void SetShadows(Rig rig, LightShadows mode)
        {
            if (rig.spot == null || rig.shadowMode == mode) return;
            rig.spot.shadows = mode; rig.shadowMode = mode;
        }

        private bool AnyLit()
        {
            foreach (var rig in rigs) if (rig.placement >= 0) return true;
            return false;
        }

        /// <summary>
        /// Moves the fades on by <paramref name="deltaTime"/> seconds: lights fade in on their lamp, fade out before
        /// they are handed to a lamp that waits, the lit radius follows the budget and the nearest lamp's shadows
        /// fade with its distance. An infinite time settles everything at once.
        /// </summary>
        public void Advance(Vector3 view, float deltaTime)
        {
            if (targetReach <= 0) targetReach = lightReach;
            bool settle = float.IsPositiveInfinity(deltaTime);
            float step = settle ? 1 : Mathf.Max(0, deltaTime) / FadeSeconds;
            reach = settle || reach <= 0 ? targetReach : Mathf.Lerp(reach, targetReach, 1 - Mathf.Exp(-Mathf.Max(0, deltaTime) * ReachRate));
            foreach (var rig in rigs)
            {
                if (rig.placement < 0 || rig.root == null) continue;
                rig.presence = Mathf.MoveTowards(rig.presence, rig.leaving ? 0 : 1, step);
                if (!rig.leaving || rig.presence > 0) continue;
                Release(rig);
                if (pending.Count > 0) { Assign(rig, pending[0], 0); pending.RemoveAt(0); }
            }
            UpdateFades(view);
            foreach (var rig in rigs)
            {
                if (rig.placement < 0 || rig.spot == null) continue;
                bool nearest = rig.placement == shadowLamp && !rig.leaving;
                if (!nearest && rig.shadow <= 0) continue;
                float wanted = nearest
                    ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(shadowDistance * .75f, shadowDistance, Vector3.Distance(rig.position, view)))
                    : 0;
                rig.shadow = Mathf.MoveTowards(rig.shadow, wanted, step);
                SetShadows(rig, rig.shadow > .01f ? LightShadows.Hard : LightShadows.None);
                float strength = rig.shadowStrength * rig.shadow;
                if (rig.shadowMode == LightShadows.None || strength == rig.appliedShadowStrength) continue;
                rig.spot.shadowStrength = strength; rig.appliedShadowStrength = strength;
            }
        }

        /// <summary>
        /// Lights fade out over the last quarter of the lit radius, so lamps never pop at its edge; the glow under
        /// a lamp fades the same way toward its own, shorter reach.
        /// </summary>
        public void UpdateFades(Vector3 view)
        {
            float radius = EffectiveReach;
            foreach (var rig in rigs)
            {
                if (rig.placement < 0) { rig.fade = 0; rig.glow = 0; continue; }
                float d = Vector3.Distance(rig.position, view);
                rig.fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(radius * .75f, radius, d));
                rig.glow = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(GlowReach * .75f, GlowReach, d));
            }
        }

        /// <summary>The spotlights are brightest at night and dimmer, but still visible, by day.</summary>
        public void ApplyDaylight(float daylight)
        {
            float scale = Mathf.Lerp(1, daytimeLight, Mathf.Clamp01(daylight));
            foreach (var rig in rigs)
            {
                if (rig.placement < 0) continue;
                float lit = scale * rig.fade * rig.presence;
                for (int i = 0; i < rig.lights.Length; i++)
                {
                    Light light = rig.lights[i];
                    if (light == null) continue;
                    bool main = rig.spot == null || light == rig.spot;
                    // The glow is a second light per lamp: far lamps do without it, so the budget goes to spotlights.
                    float value = rig.intensities[i] * lit * (main ? 1 : rig.glow);
                    if (value != rig.applied[i]) { light.intensity = value; rig.applied[i] = value; }
                    if (main || rig.switchedOn[i] == value > 0) continue;
                    light.enabled = value > 0; rig.switchedOn[i] = value > 0;
                }
            }
        }

        /// <summary>
        /// The view the lights are chosen for: the camera's, widened by <see cref="ViewPadding"/> and cut at the light
        /// reach, so a lamp just outside the picture is lit before the camera turns to it.
        /// </summary>
        public static void LightFrustum(Camera camera, float reach, Plane[] into)
        {
            if (camera.orthographic) { GeometryUtility.CalculateFrustumPlanes(camera, into); return; }
            float near = Mathf.Max(.01f, camera.nearClipPlane);
            var projection = Matrix4x4.Perspective(Mathf.Min(camera.fieldOfView + ViewPadding, 170), camera.aspect, near, Mathf.Max(near + 1, reach + LightWash * 2));
            GeometryUtility.CalculateFrustumPlanes(projection * camera.worldToCameraMatrix, into);
        }

        /// <summary>Draws the animated caustics into the cookie every lamp spotlight shares.</summary>
        public void RenderCookie(float seconds)
        {
            if (!EnsureCookie()) return;
            var old = RenderTexture.active;
            cookieAnimation.SetFloat("_AeroCookieClock", seconds);
            try { Graphics.Blit(null, cookie, cookieAnimation, 0); cookie.IncrementUpdateCount(); }
            finally { RenderTexture.active = old; }
        }

        private bool EnsureCookie()
        {
            if (cookie != null && cookieAnimation != null) return true;
            var source = lampPrefab != null ? lampPrefab.GetComponent<AeroProjectionLamp>() : null;
            if (source == null || source.cookieMaterial == null) return false;
            cookie = new RenderTexture(CookieResolution, CookieResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "Lava lamp caustics cookie", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            cookie.Create();
            cookieAnimation = new Material(source.cookieMaterial) { hideFlags = HideFlags.HideAndDontSave };
            return true;
        }

        private void EnsureRigs()
        {
            rigs.RemoveAll(r => r.root == null);
            if (lampPrefab == null) return;
            bool cookieReady = EnsureCookie();
            if (cookieReady && rigs.Count == 0) RenderCookie(Application.isPlaying ? Time.time : 0);
            while (rigs.Count < maxLights)
            {
                var instance = Instantiate(lampPrefab, transform);
                instance.name = "Lava lamp light " + rigs.Count;
                instance.hideFlags = HideFlags.DontSave;
                // These rigs share one cookie and their own budget, so the per-lamp projection budget stays out of it.
                var projection = instance.GetComponent<AeroProjectionLamp>();
                if (projection != null) DestroyImmediate(projection);
                // The instanced draw already shows the lamp; the rig only brings its light.
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
                var lights = instance.GetComponentsInChildren<Light>(true);
                Light spot = null;
                foreach (var light in lights)
                {
                    light.enabled = true;
                    if (light.type != LightType.Spot) continue;
                    spot = light; light.shadows = LightShadows.None;
                    if (cookieReady) light.cookie = cookie;
                }
                instance.SetActive(false);
                rigs.Add(new Rig
                {
                    root = instance, lights = lights, intensities = Array.ConvertAll(lights, l => l.intensity),
                    spot = spot, spotRotation = spot != null ? spot.transform.localRotation : Quaternion.identity,
                    shadowStrength = spot != null ? spot.shadowStrength : 1,
                    applied = Array.ConvertAll(lights, _ => -1f), switchedOn = Array.ConvertAll(lights, _ => true)
                });
            }
        }

        private void ReleaseRigs()
        {
            foreach (var rig in rigs)
                if (rig.root != null) { if (Application.isPlaying) Destroy(rig.root); else DestroyImmediate(rig.root); }
            rigs.Clear();
            pending.Clear(); candidateCount = 0; shadowLamp = -1; selectedOnce = false;
            if (cookie != null) { cookie.Release(); DestroyOwned(cookie); cookie = null; }
            if (cookieAnimation != null) { DestroyOwned(cookieAnimation); cookieAnimation = null; }
        }

        private static void DestroyOwned(UnityEngine.Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }

        // The spotlight washes the wall up to about two thirds of its range above the lamp.
        private const float LightWash = 6;
        private static Bounds LightBounds(Placement p) => new(p.Position + Vector3.up * (LightWash * .5f), new Vector3(LightWash, LightWash + 2, LightWash));
        private static Bounds Expand(Bounds b) { b.Expand(new Vector3(LightWash, LightWash * 2, LightWash)); return b; }

        private Bounds WorldBounds(Matrix4x4 mount) => TransformBounds(lampBounds, mount);

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            var result = new Bounds(matrix.MultiplyPoint3x4(bounds.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                result.Encapsulate(matrix.MultiplyPoint3x4(corner));
            }
            return result;
        }
    }
}
