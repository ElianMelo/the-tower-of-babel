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
    /// two lamps never hang on neighbouring pillars. Lamps are placed from the chunk cache and drawn instanced, so the
    /// tower carries no lamp objects. A lamp shows once its pillar is built. Every lamp in view, up to a budget,
    /// carries a real spotlight projecting one shared, animated caustics cookie.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TowerLavaLamps : MonoBehaviour
    {
        /// <summary>A lamp on one pillar, then three pillars without.</summary>
        public const int PillarInterval = 4;
        public const float SelectionInterval = .25f;
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
        [Tooltip("Most lamp spotlights at once. Forward+ has no per-object light limit; the nearest lamps in view win.")]
        [SerializeField, Range(0, 64)] private int maxLights = 24;
        [Tooltip("The nearest lamp within this distance casts spotlight shadows.")]
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
            public Vector3 Position => mount.GetColumn(3);
        }

        private readonly struct Part
        {
            public readonly Mesh Mesh; public readonly Material Material; public readonly Matrix4x4 Local; public readonly ShadowCastingMode Shadows;
            public Part(Mesh mesh, Material material, Matrix4x4 local, ShadowCastingMode shadows) { Mesh = mesh; Material = material; Local = local; Shadows = shadows; }
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
            public Light spot; public Quaternion spotRotation; public float fade = 1;
        }

        private readonly List<Placement> placements = new();
        private readonly List<Part> parts = new();
        private readonly Dictionary<int, FloorBatch> floors = new();
        private readonly List<FloorBatch> batchList = new();
        private readonly List<Rig> rigs = new();
        private readonly Plane[] planes = new Plane[6];
        private readonly List<int> nearest = new();
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
        /// <summary>The one animated caustics cookie every lamp spotlight projects.</summary>
        public RenderTexture SharedCookie => cookie;
        public int DrawnBatchCount { get; private set; }
        public int VisibleLampCount { get { int n = 0; foreach (var b in batchList) { Refresh(b); n += b.visible; } return n; } }
        public int ActiveLightCount { get { int n = 0; foreach (var r in rigs) if (r.root != null && r.root.activeSelf) n++; return n; } }
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

        private void OnEnable() { Subscribe(); builtFromChunks = -1; }
        private void OnDisable() { Unsubscribe(); ReleaseRigs(); }

        private void Subscribe()
        {
            if (subscribed || chunks == null) return;
            chunks.AssetStageChanged += OnStageChanged;
            chunks.ConstructionFloorChanged += MarkAllDirty;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || chunks == null) { subscribed = false; return; }
            chunks.AssetStageChanged -= OnStageChanged;
            chunks.ConstructionFloorChanged -= MarkAllDirty;
            subscribed = false;
        }

        private void OnStageChanged(ChunkKey key, int index, byte stage)
        {
            if (floors.TryGetValue(key.FloorIndex, out var batch)) batch.dirty = true;
        }

        private void MarkAllDirty() { foreach (var b in batchList) b.dirty = true; }

        /// <summary>
        /// Places a lamp on every fourth pillar of each ring (ordered by angle, clear of stairs), then scatters lamps on the
        /// other face of pillars whose neighbours are both bare. A pillar never carries two lamps.
        /// </summary>
        public void Rebuild()
        {
            placements.Clear(); floors.Clear(); batchList.Clear(); parts.Clear();
            builtFromChunks = chunks != null ? chunks.CachedChunks.Count : -1;
            if (chunks == null || lampPrefab == null) return;
            CollectParts();
            var pillars = new SortedDictionary<int, List<(ChunkKey key, ChunkAssetData asset)>>();
            var stairs = new Dictionary<int, List<ChunkAssetData>>();
            foreach (var chunk in chunks.CachedChunks)
            {
                foreach (var asset in chunk.Assets)
                {
                    int floor = chunk.Key.FloorIndex;
                    if (asset.AssetType == TowerAssetType.Pillar)
                    {
                        if (!pillars.TryGetValue(floor, out var list)) pillars[floor] = list = new();
                        list.Add((chunk.Key, asset));
                    }
                    else if (asset.AssetType == TowerAssetType.Stair)
                    {
                        if (!stairs.TryGetValue(floor, out var list)) stairs[floor] = list = new();
                        list.Add(asset);
                    }
                }
            }
            Bounds stairMesh = StairBounds();
            Vector2 center = chunks.FloorCenter;
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
                    var mount = Matrix4x4.TRS(pillar.Position + Vector3.up * mountHeight + facing * pillarRadius,
                        Quaternion.LookRotation(facing, Vector3.up), Vector3.one);
                    if (TouchesStairs(mount, pair.Key, stairs, stairMesh)) return;
                    AddPlacement(new Placement { key = key, pillar = pillar.LocalIndex, floor = pair.Key, ringIndex = i, exterior = outside, mount = mount });
                    occupied[i] = true;
                }
                // The regular pattern. A ring that does not divide by four skips its last lamp if it would sit next to the first.
                for (int i = 0; i < n; i += PillarInterval)
                    if (Free(i)) TryPlace(i, !interior);
                // Random lamps on the other face, only where the pillar and both its neighbours are bare.
                if (exteriorChance > 0)
                    for (int i = 0; i < n; i++)
                        if (Free(i) && Hash01(pair.Key, i, placementSeed) < exteriorChance) TryPlace(i, interior);
            }
            foreach (var batch in batchList)
            {
                batch.matrices = new Matrix4x4[parts.Count][];
                for (int p = 0; p < parts.Count; p++) batch.matrices[p] = new Matrix4x4[batch.placements.Count];
            }
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
            foreach (var renderer in lampPrefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null) continue;
                var local = inverse * renderer.transform.localToWorldMatrix;
                parts.Add(new Part(filter.sharedMesh, renderer.sharedMaterial, local, renderer.shadowCastingMode));
                var b = TransformBounds(filter.sharedMesh.bounds, local);
                if (!any) { lampBounds = b; any = true; } else lampBounds.Encapsulate(b);
            }
        }

        private Bounds StairBounds()
        {
            var prefab = chunks.AssetPrefabs?.GetPrefab(TowerAssetType.Stair);
            if (prefab == null) return new Bounds(Vector3.zero, Vector3.zero);
            var mesh = prefab.GetComponent<Building>()?.GetStageMesh(ChunkAssetData.CompletedStage) ?? prefab.GetComponent<MeshFilter>()?.sharedMesh;
            return mesh != null ? mesh.bounds : new Bounds(Vector3.zero, Vector3.zero);
        }

        // A stair passing the pillar would run through a hanging lamp, or someone climbing it would: leave that pillar bare.
        private bool TouchesStairs(Matrix4x4 mount, int floor, Dictionary<int, List<ChunkAssetData>> stairs, Bounds stairMesh)
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
                    Vector3 c = Vector3.Scale(stairMesh.center, stair.Scale), e = Vector3.Scale(stairMesh.extents, stair.Scale) + Vector3.one * stairClearance;
                    // Stairs only turn about Y, so the walking space is the step box raised by the headroom.
                    c.y += stairHeadroom * .5f; e.y += stairHeadroom * .5f;
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

        /// <summary>A lamp shows once its pillar is complete, on floors the player can see.</summary>
        public bool IsShown(int placement)
        {
            var p = placements[placement];
            return chunks != null && chunks.IsFloorVisible(p.floor) &&
                   chunks.TryGetAsset(p.key, p.pillar, out var pillar) && pillar.Stage >= ChunkAssetData.CompletedStage;
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
            if (Time.unscaledTime >= nextSelection)
            {
                GeometryUtility.CalculateFrustumPlanes(camera, planes);
                SelectLights(eye, planes);
                nextSelection = Time.unscaledTime + SelectionInterval;
            }
            UpdateFades(eye);
            ApplyDaylight(Shader.GetGlobalFloat("_AeroDaylight"));
            if (ActiveLightCount > 0 && Time.time >= nextCookie)
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
        public void SelectLights(Vector3 view, Plane[] frustum = null)
        {
            EnsureBuilt();
            EnsureRigs();
            nearest.Clear();
            var distances = new List<float>();
            float limit = lightReach * lightReach;
            foreach (var batch in batchList)
            {
                if (batch.bounds.SqrDistance(view) > limit) continue;
                if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, Expand(batch.bounds))) continue;
                foreach (int index in batch.placements)
                {
                    float d = (placements[index].Position - view).sqrMagnitude;
                    if (d > limit || !IsShown(index)) continue;
                    if (frustum != null && !GeometryUtility.TestPlanesAABB(frustum, LightBounds(placements[index]))) continue;
                    int at = distances.Count;
                    while (at > 0 && distances[at - 1] > d) at--;
                    if (at >= rigs.Count) continue;
                    distances.Insert(at, d); nearest.Insert(at, index);
                    if (nearest.Count > rigs.Count) { nearest.RemoveAt(nearest.Count - 1); distances.RemoveAt(distances.Count - 1); }
                }
            }
            // Rigs that already sit on a chosen lamp stay put, so their light does not jump.
            var free = new List<int>(nearest);
            foreach (var rig in rigs) if (rig.placement >= 0 && !free.Remove(rig.placement)) rig.placement = -1;
            foreach (var rig in rigs)
            {
                if (rig.placement < 0 && free.Count > 0) { rig.placement = free[0]; free.RemoveAt(0); }
                bool active = rig.placement >= 0;
                if (active)
                {
                    var p = placements[rig.placement];
                    rig.root.transform.SetPositionAndRotation(p.mount.GetColumn(3), p.mount.rotation);
                    // Each lamp turns the shared cookie by its own angle, so neighbouring washes never repeat.
                    if (rig.spot != null)
                        rig.spot.transform.localRotation = rig.spotRotation * Quaternion.AngleAxis(Hash01(p.floor, p.ringIndex, placementSeed + 7) * 360, Vector3.forward);
                }
                if (rig.root.activeSelf != active) rig.root.SetActive(active);
                if (rig.spot != null) rig.spot.shadows = LightShadows.None;
            }
            // Only the nearest lamp casts spotlight shadows, as with the aqua lamps.
            if (nearest.Count > 0 && distances[0] <= shadowDistance * shadowDistance)
                foreach (var rig in rigs) if (rig.placement == nearest[0] && rig.spot != null) rig.spot.shadows = LightShadows.Hard;
            UpdateFades(view);
        }

        /// <summary>Lights fade out over the last quarter of their reach, so lamps never pop at the edge.</summary>
        public void UpdateFades(Vector3 view)
        {
            foreach (var rig in rigs)
            {
                if (rig.placement < 0) { rig.fade = 0; continue; }
                float d = Vector3.Distance(placements[rig.placement].Position, view);
                rig.fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(lightReach * .75f, lightReach, d));
            }
        }

        /// <summary>The spotlights are brightest at night and dimmer, but still visible, by day.</summary>
        public void ApplyDaylight(float daylight)
        {
            float scale = Mathf.Lerp(1, daytimeLight, Mathf.Clamp01(daylight));
            foreach (var rig in rigs)
                for (int i = 0; i < rig.lights.Length; i++) if (rig.lights[i] != null) rig.lights[i].intensity = rig.intensities[i] * scale * rig.fade;
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
                    spot = spot, spotRotation = spot != null ? spot.transform.localRotation : Quaternion.identity
                });
            }
        }

        private void ReleaseRigs()
        {
            foreach (var rig in rigs)
                if (rig.root != null) { if (Application.isPlaying) Destroy(rig.root); else DestroyImmediate(rig.root); }
            rigs.Clear();
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
