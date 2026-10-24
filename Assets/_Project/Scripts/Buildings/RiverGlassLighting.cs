using System.Collections.Generic;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    /// <summary>One local water patch and two occluded projections share an animated cookie.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ChunkManager))]
    public sealed class RiverGlassLighting : MonoBehaviour
    {
        public const int MaximumLights = 2;
        public const int CookieResolution = 128;
        public const float CookieInterval = .125f;
        public RiverGlassProfile profile;
        public Shader cookieShader;
        private ChunkManager manager;
        private readonly Dictionary<ChunkKey, List<Candidate>> candidates = new();
        private readonly Patch[] patches = new Patch[1];
        private float nextSelection;
        public int ActiveLightCount { get; private set; }
        public int AllocatedCookieCount { get; private set; }

        private readonly struct Candidate
        {
            public readonly ChunkKey chunk;
            public readonly int index;
            public readonly Vector3 point;
            public Candidate(ChunkKey chunk, int index, Vector3 point) { this.chunk = chunk; this.index = index; this.point = point; }
            public bool Same(Candidate other) => chunk.Equals(other.chunk) && index == other.index;
        }
        private sealed class Patch
        {
            public Candidate source, desired;
            public bool hasSource, hasDesired;
            public float strength, nextCookie;
            public GameObject root;
            public Light up, down;
            public RenderTexture cookie;
            public Material material;
        }
        private void OnEnable()
        {
            manager = GetComponent<ChunkManager>();
            manager.AssetStageChanged += StageChanged;
            manager.ConstructionFloorChanged += Invalidate;
            candidates.Clear(); nextSelection = 0;
        }
        private void StageChanged(ChunkKey key, int index, byte stage) { candidates.Remove(key); nextSelection = 0; }
        private void Invalidate() { candidates.Clear(); nextSelection = 0; }
        private void LateUpdate()
        {
            if (Application.isPlaying && Camera.main != null) Sample(Camera.main, Time.time, Time.deltaTime);
        }

        private bool Valid(Candidate source)
        {
            return manager != null && manager.isActiveAndEnabled && manager.IsFloorVisible(source.chunk.FloorIndex) &&
                manager.TryGetAsset(source.chunk, source.index, out var asset) &&
                asset.AssetType == TowerAssetType.GlassWaterFloor && asset.Stage == ChunkAssetData.CompletedStage;
        }

        private List<Candidate> Candidates(ChunkSceneCache chunk)
        {
            if (candidates.TryGetValue(chunk.Key, out var cached)) return cached;
            cached = new List<Candidate>(); candidates.Add(chunk.Key, cached);
            foreach (var asset in chunk.Assets)
            {
                if (asset.AssetType != TowerAssetType.GlassWaterFloor || asset.Stage != ChunkAssetData.CompletedStage) continue;
                bool found = false; Vector3 best = default; float bestDistance = float.PositiveInfinity;
                float radius = Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), manager.FloorCenter);
                for (int x = 0; x < 5; x++) for (int z = 0; z < 5; z++)
                {
                    var local = new Vector3((x + .5f) * .8f, 0, -(z + .5f) * .8f);
                    float distance = (local - new Vector3(2, 0, -2)).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    var bent = manager.RadialFloors ? TowerFloorLayout.Bend(local, radius, asset.Scale) : local;
                    Vector3 point = asset.Position + asset.Rotation * Vector3.Scale(bent, asset.Scale);
                    if (GlassWaterFloorLayout.ChannelDistance(new Vector2(point.x, point.z) - manager.FloorCenter,
                            chunk.Key.FloorIndex, profile.layout) > -.08f) continue;
                    best = point; bestDistance = distance; found = true;
                }
                if (found) cached.Add(new Candidate(chunk.Key, asset.LocalIndex, best));
            }
            return cached;
        }

        private void Select(Vector3 view)
        {
            for (int slot = 0; slot < patches.Length; slot++)
            {
                var patch = patches[slot]; patch.hasDesired = false;
                float best = profile.selectionDistance * profile.selectionDistance;
                foreach (var chunk in manager.CachedChunks)
                {
                    if (chunk == null || !manager.IsFloorVisible(chunk.Key.FloorIndex) ||
                        ChunkGrid.GetWorldBounds(chunk.Key, manager.ChunkSizeMeters, manager.FloorHeight).SqrDistance(view) > best) continue;
                    foreach (var source in Candidates(chunk))
                    {
                        float distance = (source.point - view).sqrMagnitude;
                        // Prefer an existing patch slightly to avoid hopping between neighbouring tiles.
                        float score = patch.hasSource && patch.source.Same(source) ? distance * .8f : distance;
                        if (distance > profile.selectionDistance * profile.selectionDistance || score >= best) continue;
                        best = score; patch.desired = source; patch.hasDesired = true;
                    }
                }
            }
        }

        public void Sample(Camera camera, float seconds, float deltaTime)
        {
            ActiveLightCount = 0;
            if (manager == null) manager = GetComponent<ChunkManager>();
            if (!isActiveAndEnabled || !manager.isActiveAndEnabled || profile == null || cookieShader == null ||
                !RiverGlassProfile.SupportsProjection ||
                (camera.scene.IsValid() && camera.scene != gameObject.scene)) { Release(); return; }
            for (int i = 0; i < patches.Length; i++) patches[i] ??= new Patch();
            if (seconds >= nextSelection)
            {
                Select(camera.transform.position); nextSelection = seconds + .25f;
            }
            foreach (var patch in patches)
            {
                if (patch.hasSource && !Valid(patch.source))
                { patch.hasSource = false; patch.strength = 0; if (patch.root != null) patch.root.SetActive(false); }
                bool same = patch.hasSource && patch.hasDesired && patch.source.Same(patch.desired);
                patch.strength = Mathf.MoveTowards(patch.strength, same ? 1 : 0, Mathf.Max(0, deltaTime) * 5);
                if (patch.strength <= .001f && patch.hasDesired && Valid(patch.desired))
                {
                    patch.source = patch.desired; patch.hasSource = true;
                    CreatePatch(patch); patch.root.SetActive(true); patch.root.transform.position = patch.source.point;
                    patch.strength = Mathf.Min(1, Mathf.Max(0, deltaTime) * 5); patch.nextCookie = 0;
                }
                if (patch.root == null) continue;
                if (!patch.hasSource || patch.strength <= 0) { patch.root.SetActive(false); continue; }
                if (seconds >= patch.nextCookie)
                {
                    patch.material.SetFloat("_RiverClock", seconds);
                    patch.material.SetFloat("_FlowSpeed", profile.flowSpeed);
                    patch.material.SetVector("_CookieOrigin", patch.source.point);
                    var old = RenderTexture.active;
                    try { Graphics.Blit(null, patch.cookie, patch.material); patch.cookie.IncrementUpdateCount(); }
                    finally { RenderTexture.active = old; }
                    patch.nextCookie = seconds + CookieInterval;
                }
                float distance = Vector3.Distance(camera.transform.position, patch.source.point);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(profile.selectionDistance*.7f, profile.selectionDistance, distance));
                float daylight = Mathf.Clamp01(Shader.GetGlobalFloat("_AeroDaylight"));
                float intensity = Mathf.Lerp(profile.nightIntensity, profile.daylightIntensity, daylight) * patch.strength * fade;
                ApplyLight(patch.up); ApplyLight(patch.down);
                void ApplyLight(Light light)
                {
                    light.color = profile.projectionTint; light.intensity = intensity; light.range = profile.projectionRange;
                    light.spotAngle=profile.projectionAngle; light.innerSpotAngle=profile.projectionAngle*.95f;
                    light.cullingMask = camera.cullingMask; light.enabled = intensity > .001f;
                    if (light.enabled) ActiveLightCount++;
                }
            }
        }

        private void CreatePatch(Patch patch)
        {
            if (patch.root != null) return;
            patch.root = new GameObject("Pooled river caustics") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            patch.root.transform.SetParent(transform, false);
            Light Create(string name, Vector3 direction, float offset)
            {
                var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
                go.transform.SetParent(patch.root.transform, false); go.transform.localPosition = Vector3.up * offset;
                go.transform.rotation = Quaternion.LookRotation(direction);
                var light = go.AddComponent<Light>(); light.type = LightType.Spot; light.spotAngle = profile.projectionAngle;
                light.innerSpotAngle = profile.projectionAngle*.95f; light.shadows = LightShadows.Hard;
                light.shadowCustomResolution = 256; light.shadowBias = .02f; light.shadowNormalBias = .02f;
                RiverGlassProfile.ConfigureProjectionLight(light);
                light.renderMode = LightRenderMode.ForcePixel;
                return light;
            }
            patch.up = Create("Reflected water light above", Vector3.up, .025f);
            patch.down = Create("Transmitted water light below", Vector3.down, -.225f);
            patch.cookie = new RenderTexture(CookieResolution, CookieResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            { name = "River shared animated cookie", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
            patch.cookie.Create(); AllocatedCookieCount++;
            patch.material = new Material(cookieShader) { hideFlags = HideFlags.HideAndDontSave };
            patch.material.SetTexture("_CausticsTex", profile.caustics);
            patch.up.cookie = patch.down.cookie = patch.cookie;
            RiverGlassRenderRegistry.RegisterProjection(patch.up); RiverGlassRenderRegistry.RegisterProjection(patch.down);
        }

        public void Release()
        {
            foreach (var patch in patches)
            {
                if (patch == null) continue;
                RiverGlassRenderRegistry.UnregisterProjection(patch.up); RiverGlassRenderRegistry.UnregisterProjection(patch.down);
                if (patch.cookie != null) { patch.cookie.Release(); DestroyOwned(patch.cookie); }
                DestroyOwned(patch.material); DestroyOwned(patch.root);
                patch.root = null; patch.cookie = null; patch.material = null; patch.hasSource = patch.hasDesired = false; patch.strength = 0;
            }
            AllocatedCookieCount = ActiveLightCount = 0; nextSelection = 0;
        }
        private static void DestroyOwned(Object value)
        { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private void OnDisable()
        {
            if (manager != null) { manager.AssetStageChanged -= StageChanged; manager.ConstructionFloorChanged -= Invalidate; }
            Release(); candidates.Clear();
        }
    }
}
