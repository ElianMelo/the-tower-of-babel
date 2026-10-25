using System.Collections.Generic;
using TowerOfBabel.World.Chunks;
using TowerOfBabel.World.Tower;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    /// <summary>Intermittent reflected light from separated water patches shares one animated cookie.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(ChunkManager))]
    public sealed class RiverGlassLighting : MonoBehaviour
    {
        public const int MaximumPatches = 4;
        public const int MaximumLights = MaximumPatches * 2;
        public const int MaximumActivePatches = 2;
        public const int CookieResolution = 2048;
        public const int ShadowResolution = 512;
        public const float CookieInterval = .125f;
        // This project's Water layer contains the ocean/lake renderers. The custom river
        // surface samples sunlight/environment only and never receives these extra lights.
        public const int WaterLayerMask = 1 << 4;
        public RiverGlassProfile profile;
        public Shader cookieShader;
        private ChunkManager manager;
        private readonly Dictionary<ChunkKey, List<Candidate>> candidates = new();
        private readonly Patch[] patches = new Patch[MaximumPatches];
        private RenderTexture cookie;
        private Material cookieMaterial;
        private float nextSelection, nextCookie;
        private bool schedulerInitialized, startsWereAllowed;
        private uint schedulerRandom;
        private float nextWindowStart, quietStart, quietFadeEnd, quietHoldEnd, quietResumeEnd;
        public int ActiveLightCount { get; private set; }
        public int AllocatedCookieCount { get; private set; }
        public int RunningWindowCount { get; private set; }
        public float GlobalVisibility { get; private set; } = 1;
        public bool WindowStartsAllowed { get; private set; }
        public int WindowStartCount { get; private set; }
        public int LastStartedSlot { get; private set; } = -1;
        public float LastWindowStartedAt { get; private set; } = float.NegativeInfinity;
        public float LastWindowDuration { get; private set; }
        public float LastWindowRest { get; private set; }
        public float NextGlobalQuietAt => quietStart;
        public Vector2 CurrentFlowOffset { get; private set; }

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
            public float strength;
            public float burstStart, burstEnd, nextBurst, peak, angleScale;
            public uint randomState;
            public GameObject root;
            public Light up, down;
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
        private static float Random01(ref uint state)
        {
            uint x = state;
            x ^= x << 13; x ^= x >> 17; x ^= x << 5;
            state = x;
            return (x >> 8) * (1f / 16777216);
        }
        private static float Random01(Patch patch) => Random01(ref patch.randomState);
        private static float Duration(ref uint state, Vector2 range)
        {
            float low = Mathf.Max(.1f, Mathf.Min(range.x, range.y));
            return Mathf.Lerp(low, Mathf.Max(low, Mathf.Max(range.x, range.y)), Random01(ref state));
        }
        private static float Duration(Patch patch, Vector2 range) => Duration(ref patch.randomState, range);
        private void ResetBursts(Patch patch, float seconds)
        {
            Vector3 point = patch.source.point;
            patch.randomState = unchecked((uint)(Mathf.RoundToInt(point.x * 100) * 73856093 ^
                Mathf.RoundToInt(point.y * 100) * 83492791 ^ Mathf.RoundToInt(point.z * 100) * 19349663));
            if (patch.randomState == 0) patch.randomState = 0x9E3779B9u;
            patch.burstStart = patch.burstEnd = float.NegativeInfinity;
            patch.nextBurst = seconds + Mathf.Lerp(.2f, Mathf.Max(.2f, profile.projectionQuietDuration.y), Random01(patch));
            patch.angleScale = Mathf.Lerp(.9f, 1, Random01(patch));
            // Each source has a fixed orientation; the cutout field supplies the motion.
            float orientation = Random01(patch) * 360;
            patch.up.transform.rotation = Quaternion.LookRotation(Vector3.up) * Quaternion.AngleAxis(orientation, Vector3.forward);
            patch.down.transform.rotation = Quaternion.LookRotation(Vector3.down) * Quaternion.AngleAxis(orientation + 137.5f, Vector3.forward);
        }
        private void ScheduleGlobalQuiet(float seconds)
        {
            float fade = Mathf.Max(.1f, profile.projectionGlobalFade);
            quietStart = seconds + Duration(ref schedulerRandom, profile.projectionGlobalQuietInterval);
            quietFadeEnd = quietStart + fade;
            quietHoldEnd = quietFadeEnd + Duration(ref schedulerRandom, profile.projectionGlobalQuietDuration);
            quietResumeEnd = quietHoldEnd + fade;
        }
        private static bool Running(Patch patch, float seconds) => patch.hasSource && patch.randomState != 0 &&
            seconds >= patch.burstStart && seconds < patch.burstEnd;
        private void ScheduleWindows(float seconds)
        {
            if (!schedulerInitialized)
            {
                schedulerRandom = 0xC0FEBABEu; schedulerInitialized = true;
                nextWindowStart = seconds; ScheduleGlobalQuiet(seconds);
            }
            // Skip missed rests after a large clock jump instead of replaying them.
            if (seconds >= quietResumeEnd) ScheduleGlobalQuiet(seconds);
            WindowStartsAllowed = seconds < quietStart;
            if (seconds < quietStart) GlobalVisibility = 1;
            else if (seconds < quietFadeEnd)
                GlobalVisibility = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(quietStart, quietFadeEnd, seconds));
            else if (seconds < quietHoldEnd) GlobalVisibility = 0;
            else GlobalVisibility = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(quietHoldEnd, quietResumeEnd, seconds));
            if (WindowStartsAllowed && !startsWereAllowed)
                nextWindowStart = Mathf.Max(nextWindowStart, seconds + Duration(ref schedulerRandom, profile.projectionStartSpacing));
            startsWereAllowed = WindowStartsAllowed;

            // Count windows rather than enabled lights: a window still owns its slot
            // during its zero-intensity first frame and while fading through a handoff.
            RunningWindowCount = 0;
            foreach (var patch in patches) if (Running(patch, seconds)) RunningWindowCount++;
            int limit = Mathf.Clamp(profile.projectionMaxActivePatches, 1, MaximumActivePatches);
            if (!WindowStartsAllowed || seconds < nextWindowStart || RunningWindowCount >= limit) return;
            int chosen = -1; float earliest = float.PositiveInfinity;
            for (int slot = 0; slot < patches.Length; slot++)
            {
                var patch = patches[slot];
                if (!patch.hasSource || !patch.hasDesired || !patch.source.Same(patch.desired) ||
                    patch.strength <= .001f || patch.randomState == 0 || Running(patch, seconds) ||
                    seconds < patch.nextBurst || patch.nextBurst >= earliest) continue;
                earliest = patch.nextBurst; chosen = slot;
            }
            if (chosen < 0) return;
            var ready = patches[chosen];
            ready.burstStart = seconds;
            LastWindowDuration = Duration(ready, profile.projectionBurstDuration);
            LastWindowRest = Duration(ready, profile.projectionQuietDuration);
            ready.burstEnd = seconds + LastWindowDuration;
            ready.nextBurst = ready.burstEnd + LastWindowRest;
            ready.peak = Mathf.Lerp(.7f, 1, Random01(ready));
            nextWindowStart = seconds + Duration(ref schedulerRandom, profile.projectionStartSpacing);
            RunningWindowCount++; WindowStartCount++; LastStartedSlot = chosen; LastWindowStartedAt = seconds;
        }
        private float BurstEnvelope(Patch patch, float seconds)
        {
            if (seconds < patch.burstStart || seconds >= patch.burstEnd) return 0;
            float fade = Mathf.Clamp(profile.projectionBurstFade, .05f, (patch.burstEnd - patch.burstStart) * .45f);
            return Mathf.SmoothStep(0, 1, (seconds - patch.burstStart) / fade) *
                Mathf.SmoothStep(0, 1, (patch.burstEnd - seconds) / fade) * patch.peak;
        }
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

        private void Select(Camera camera)
        {
            Vector3 view = camera.transform.position;
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            // Looking straight up/down should retain the player's horizontal heading.
            if (forward.sqrMagnitude < .01f)
                forward = Vector3.Cross(camera.transform.right, Vector3.up);
            forward.Normalize();
            int limit = Mathf.Clamp(profile.projectionPatchCount, 1, MaximumPatches);
            float selectionRadius = Mathf.Max(1, profile.selectionDistance);
            float separationSquared = profile.patchSeparation * profile.patchSeparation;
            for (int slot = 0; slot < patches.Length; slot++)
            {
                var patch = patches[slot]; patch.hasDesired = false;
                if (slot >= limit) continue;
                float ahead = limit > 1 ? Mathf.Min(profile.projectionAheadDistance, selectionRadius * .75f) * slot / (limit - 1) : 0;
                Vector3 target = view + forward * ahead;
                float best = float.PositiveInfinity;
                foreach (var chunk in manager.CachedChunks)
                {
                    if (chunk == null || !manager.IsFloorVisible(chunk.Key.FloorIndex) ||
                        ChunkGrid.GetWorldBounds(chunk.Key, manager.ChunkSizeMeters, manager.FloorHeight).SqrDistance(view) > selectionRadius * selectionRadius) continue;
                    foreach (var source in Candidates(chunk))
                    {
                        Vector3 fromView = source.point - view;
                        if (fromView.sqrMagnitude > selectionRadius * selectionRadius ||
                            (slot > 0 && Vector3.Dot(fromView, forward) < ahead * .25f)) continue;
                        bool occupied = false;
                        for (int previous = 0; previous < slot; previous++)
                        {
                            if (!patches[previous].hasDesired) continue;
                            Vector3 between = source.point - patches[previous].desired.point;
                            // Do not stack multiple patches on vertically aligned floors.
                            between.y = 0;
                            if (between.sqrMagnitude < separationSquared) { occupied = true; break; }
                        }
                        if (occupied) continue;
                        Vector3 offset = source.point - target;
                        float distance = offset.sqrMagnitude + offset.y * offset.y;
                        // Prefer an existing patch slightly to avoid hopping between neighbouring tiles.
                        float score = patch.hasSource && patch.source.Same(source) ? distance * .8f : distance;
                        if (score >= best) continue;
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
                Select(camera); nextSelection = seconds + .25f;
            }
            foreach (var patch in patches)
            {
                if (patch.hasSource && !Valid(patch.source))
                {
                    patch.hasSource = false; patch.strength = 0; patch.burstEnd = seconds;
                    if (patch.root != null) patch.root.SetActive(false);
                }
                bool same = patch.hasSource && patch.hasDesired && patch.source.Same(patch.desired);
                float relocationStep = Mathf.Max(0, deltaTime) / Mathf.Max(.1f, profile.projectionRelocationFade);
                patch.strength = Mathf.MoveTowards(patch.strength, same ? 1 : 0, relocationStep);
                if (patch.strength <= .001f && patch.hasDesired && Valid(patch.desired))
                {
                    patch.source = patch.desired; patch.hasSource = true;
                    CreatePatch(patch); patch.root.SetActive(true); patch.root.transform.position = patch.source.point;
                    patch.strength = Mathf.Min(1, relocationStep);
                    // Moving along the river must not continually restart the quiet period.
                    if (patch.randomState == 0) ResetBursts(patch, seconds);
                }
            }
            ScheduleWindows(seconds);
            float travel = Mathf.Max(0, profile.projectionCutoutSpeed) * (seconds +
                Mathf.Clamp(profile.projectionFlowVariation, 0, .5f) / .21f * Mathf.Sin(seconds * .21f));
            float side = Mathf.Max(0, profile.projectionFlowSway) * Mathf.Sin(seconds * .24f);
            Vector2 direction = new Vector2(1, .37f).normalized;
            CurrentFlowOffset = direction * travel + new Vector2(-direction.y, direction.x) * side;
            foreach (var patch in patches)
            {
                if (patch.root == null) continue;
                if (!patch.hasSource || patch.strength <= 0) { patch.root.SetActive(false); continue; }
                float distance = Vector3.Distance(camera.transform.position, patch.source.point);
                float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(profile.selectionDistance*.7f, profile.selectionDistance, distance));
                float daylight = Mathf.Clamp01(Shader.GetGlobalFloat("_AeroDaylight"));
                float intensity = Mathf.Lerp(profile.nightIntensity, profile.daylightIntensity, daylight) *
                    patch.strength * fade * BurstEnvelope(patch, seconds) * GlobalVisibility;
                ApplyLight(patch.up); ApplyLight(patch.down);
                void ApplyLight(Light light)
                {
                    light.color = profile.projectionTint; light.intensity = intensity; light.range = profile.projectionRange;
                    light.spotAngle=profile.projectionAngle*Mathf.Max(.9f,patch.angleScale); light.innerSpotAngle=light.spotAngle*.95f;
                    light.cullingMask = camera.cullingMask & ~WaterLayerMask; light.enabled = intensity > .001f;
                    if (light.enabled) ActiveLightCount++;
                }
            }
            if (ActiveLightCount > 0 && cookie != null && seconds >= nextCookie)
            {
                cookieMaterial.SetFloat("_RiverClock", seconds);
                cookieMaterial.SetFloat("_FlowSpeed", profile.flowSpeed);
                cookieMaterial.SetFloat("_CookieSpan", 6*Mathf.Tan(profile.projectionAngle*.5f*Mathf.Deg2Rad));
                cookieMaterial.SetFloat("_PatternDensity", profile.projectionPatternDensity);
                cookieMaterial.SetFloat("_HighlightOpacity", Mathf.Clamp01(profile.projectionHighlightOpacity));
                cookieMaterial.SetFloat("_DetailOpacity", Mathf.Clamp01(profile.projectionDetailOpacity));
                cookieMaterial.SetVector("_CutoutOffset", new Vector4(CurrentFlowOffset.x, CurrentFlowOffset.y, 0, 0));
                cookieMaterial.SetFloat("_CutoutWarp", profile.projectionCutoutWarp);
                cookieMaterial.SetVector("_DetailLifetime", new Vector4(profile.projectionDetailLifetime.x, profile.projectionDetailLifetime.y, 0, 0));
                cookieMaterial.SetVector("_HighlightLifetime", new Vector4(profile.projectionHighlightLifetime.x, profile.projectionHighlightLifetime.y, 0, 0));
                cookieMaterial.SetFloat("_HighlightFade", profile.projectionHighlightFade);
                cookieMaterial.SetTexture("_CausticsTex", profile.caustics);
                var old = RenderTexture.active;
                try { Graphics.Blit(null, cookie, cookieMaterial); cookie.IncrementUpdateCount(); }
                finally { RenderTexture.active = old; }
                nextCookie = seconds + CookieInterval;
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
                light.shadowCustomResolution = ShadowResolution; light.shadowBias = .02f; light.shadowNormalBias = .02f;
                RiverGlassProfile.ConfigureProjectionLight(light);
                light.renderMode = LightRenderMode.ForcePixel;
                light.enabled = false;
                return light;
            }
            patch.up = Create("Reflected water light above", Vector3.up, .025f);
            patch.down = Create("Transmitted water light below", Vector3.down, -.225f);
            if (cookie == null)
            {
                cookie = new RenderTexture(CookieResolution, CookieResolution, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = "River shared animated cookie", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                cookie.Create(); AllocatedCookieCount = 1;
                cookieMaterial = new Material(cookieShader) { hideFlags = HideFlags.HideAndDontSave };
                nextCookie = 0;
            }
            patch.up.cookie = patch.down.cookie = cookie;
            RiverGlassRenderRegistry.RegisterProjection(patch.up); RiverGlassRenderRegistry.RegisterProjection(patch.down);
        }

        public void Release()
        {
            foreach (var patch in patches)
            {
                if (patch == null) continue;
                RiverGlassRenderRegistry.UnregisterProjection(patch.up); RiverGlassRenderRegistry.UnregisterProjection(patch.down);
                if (patch.root != null) patch.root.SetActive(false);
                DestroyOwned(patch.root);
                patch.root = null; patch.hasSource = patch.hasDesired = false; patch.strength = 0; patch.randomState = 0;
            }
            if (cookie != null) { cookie.Release(); DestroyOwned(cookie); }
            DestroyOwned(cookieMaterial); cookie = null; cookieMaterial = null;
            AllocatedCookieCount = ActiveLightCount = 0; nextSelection = nextCookie = 0;
            schedulerInitialized = startsWereAllowed = WindowStartsAllowed = false;
            RunningWindowCount = WindowStartCount = 0; GlobalVisibility = 1;
            LastStartedSlot = -1; LastWindowStartedAt = float.NegativeInfinity;
            LastWindowDuration = LastWindowRest = 0; quietStart = quietFadeEnd = quietHoldEnd = quietResumeEnd = nextWindowStart = 0;
            CurrentFlowOffset = Vector2.zero;
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
