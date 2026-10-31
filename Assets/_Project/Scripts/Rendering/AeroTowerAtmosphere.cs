using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TowerOfBabel.Rendering
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroTowerAtmosphere : MonoBehaviour
    {
        public AeroDayNightCycle cycle;
        public AeroRaySettings raySettings;
        public Vector2 center;
        public float baseRadius = 96, decrease = 3, floorHeight = 6;
        public float basePillarAngle = 2;
        public int levels = 45;
        // Migration reference only: fixed sheets are stopped, never used for scattering.
        public ParticleSystem beams, fireflies;
        public Volume exitVolume;
        public Light[] bounceLights;
        public const int BeamCount = 48;
        private readonly List<AeroArchOpening> openings = new();
        private readonly int[] sourceIds = { -1, -1 }, pendingIds = { -1, -1 };
        private readonly float[] sourceWeights = new float[2];
        private float nextSelection, lastClock = -1, floorBlend = 1;
        private int previousFloor, lastFloor = -1;
        private bool geometryVisible;
        public int CurrentFloor { get; private set; }
        public int PreviousFloor => previousFloor;
        public float ExitWeight { get; private set; }
        public float InteriorWeight { get; private set; }
        public bool HasGeometry => geometryVisible;
        public IReadOnlyList<AeroArchOpening> Openings => openings;
        public int SourceId(int slot) => sourceIds[slot];
        public float SourceWeight(int slot) => sourceWeights[slot];
        public Vector4 Volume0 => new(center.x, center.y, WallRadius(CurrentFloor), CurrentFloor * floorHeight + .2f);
        public Vector4 Volume1 => new(center.x, center.y, WallRadius(previousFloor), previousFloor * floorHeight + .2f);
        public Vector4 VolumeBlend => new(floorHeight, floorBlend, InteriorWeight, raySettings != null ? raySettings.softness : .4f);

        private void OnEnable() { AeroTowerLightContext.Register(this); StopLegacy(); }
        private void OnDisable()
        {
            AeroTowerLightContext.Unregister(this);
            if (bounceLights != null) foreach (var light in bounceLights) if (light != null) light.enabled = false;
        }
        private void StopLegacy() { if (beams != null && beams.particleCount > 0) beams.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); }
        public void SetConstructionContext(bool visible, IReadOnlyList<AeroArchOpening> actualOpenings)
        {
            geometryVisible = visible; openings.Clear();
            if (actualOpenings != null) for (int i = 0; i < actualOpenings.Count; i++) openings.Add(actualOpenings[i]);
            nextSelection = 0;
        }
        /// <summary>Firefly spawns per second. Daytime base raised 3 -> 6 (2026-10-29) so world-space particles appear more often.</summary>
        public static float FireflyRate(float daylight, AeroDayNightProfile profile, float inside) =>
            Mathf.Lerp(6, 10, 1 - daylight) * profile.fireflyDensity * Mathf.Max(0, profile.fireflyPopulationMultiplier) * inside;
        public float WallRadius(int floor) => Mathf.Max(3, baseRadius - decrease * (floor / 2 + 1));

        /// <summary>Floors 0..BuiltFloors-1 have a solid deck (set by TowerAtmosphereBridge from construction).</summary>
        public int BuiltFloors { get; set; } = int.MaxValue;
        private int DeckCount => Mathf.Min(levels, BuiltFloors);
        private int FloorAt(float y) => Mathf.FloorToInt((y - .2f) / Mathf.Max(.01f, floorHeight));
        private float RadiusOf(Vector3 p) => Vector2.Distance(new Vector2(p.x, p.z), center);

        /// <summary>True when a built deck above <paramref name="point"/> covers it (inside the tower, under a roof).
        /// Same stepped-cone rule as the wetness shelter (AeroWetness.hlsl), widened by <paramref name="margin"/>.</summary>
        public bool IsUnderRoof(Vector3 point, float margin = 0)
        {
            int roof = Mathf.Max(0, FloorAt(point.y) + 1);
            if (point.y < -.3f) return false;
            return roof < DeckCount && RadiusOf(point) <= WallRadius(roof) + margin;
        }

        /// <summary>Top of the highest built deck at or below <paramref name="point"/> covering its column, or false (open ground).</summary>
        public bool TryDeckBelow(Vector3 point, out float deckY)
        {
            float radius = RadiusOf(point);
            // Decks widen going down, so the first covering deck found from the top is the one a drop meets.
            for (int floor = Mathf.Min(FloorAt(point.y), DeckCount - 1); floor >= 0; floor--)
                if (radius <= WallRadius(floor) + .5f) { deckY = floor * floorHeight; return true; }
            deckY = 0; return false;
        }
        public float ExitProximity(Vector3 view)
        {
            int floor = Mathf.Clamp(Mathf.FloorToInt((view.y - .2f) / floorHeight), 0, levels - 1);
            float radius = Vector2.Distance(new Vector2(view.x, view.z), center);
            if (view.y < .2f || view.y > levels * floorHeight || radius > WallRadius(floor) + 2) return 0;
            return Mathf.SmoothStep(0, 1, Mathf.InverseLerp(WallRadius(floor) * .65f, WallRadius(floor) - 1, radius));
        }
        private void Update() { if (Application.isPlaying && Camera.main != null) Sample(Camera.main.transform.position, Time.time); }
        public void Sample(Vector3 view, float clock)
        {
            StopLegacy();
            if (cycle == null || cycle.profile == null) return;
            float dt = lastClock < 0 ? 0 : Mathf.Clamp(clock - lastClock, 0, .1f); lastClock = clock;
            int floor = Mathf.Clamp(Mathf.FloorToInt((view.y - .2f) / floorHeight), 0, levels - 1);
            if (floor != CurrentFloor) { previousFloor = CurrentFloor; CurrentFloor = floor; floorBlend = 0; }
            float duration = raySettings != null ? raySettings.transitionSeconds : .85f;
            floorBlend = Mathf.MoveTowards(floorBlend, 1, dt / Mathf.Max(.1f, duration));
            if (floorBlend >= 1) previousFloor = CurrentFloor;
            ExitWeight = ExitProximity(view);
            float radial = Vector2.Distance(new Vector2(view.x, view.z), center);
            float inside = geometryVisible && view.y >= -.3f && view.y <= levels * floorHeight
                ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(WallRadius(floor) - .5f, WallRadius(floor) + 2, radial)) : 0;
            InteriorWeight = Mathf.MoveTowards(InteriorWeight, inside, dt / Mathf.Max(.1f, duration));
            if (exitVolume != null)
            {
                exitVolume.transform.position = new Vector3(center.x, CurrentFloor * floorHeight + 3, center.y);
                // Calm interior grade everywhere inside, a little stronger toward the open edge (2026-10-31 feedback).
                exitVolume.weight = InteriorWeight * Mathf.Lerp(.65f, 1, ExitWeight) * cycle.profile.exitVolumeStrength;
            }
            if (clock >= nextSelection) { SelectSources(view); nextSelection = clock + .1f; }
            UpdateLights(dt, duration);
            if (fireflies != null)
            {
                float population = Mathf.Max(0, cycle.profile.fireflyPopulationMultiplier);
                float spread = Mathf.Sqrt(Mathf.Max(.01f, cycle.profile.fireflyCoverageAreaMultiplier));
                var main = fireflies.main;
                int capacity = Mathf.Max(1, Mathf.CeilToInt(96 * population));
                if (main.maxParticles != capacity) main.maxParticles = capacity;
                var shape = fireflies.shape;
                var coverage = new Vector3(20 * spread, 2.4f, 20 * spread);
                if (shape.scale != coverage) shape.scale = coverage;
                Vector3 relative = view - new Vector3(center.x, 0, center.y);
                relative.y = 0; relative = Vector3.ClampMagnitude(relative, Mathf.Max(1, WallRadius(floor) - 12 * spread));
                fireflies.transform.position = new Vector3(center.x + relative.x, floor * floorHeight + 2, center.y + relative.z);
                var emission = fireflies.emission;
                emission.rateOverTime = FireflyRate(cycle.Daylight, cycle.profile, inside);
                if (lastFloor != floor) fireflies.Clear();
            }
            lastFloor = floor;
        }
        private int Find(int id) { for (int i = 0; i < openings.Count; i++) if (openings[i].Id == id) return i; return -1; }
        private void SelectSources(Vector3 view)
        {
            int count = raySettings != null ? raySettings.FillCount : 0;
            for (int slot = 0; slot < 2; slot++)
            {
                int best = -1; float score = float.MaxValue;
                if (slot < count && geometryVisible) for (int i = 0; i < openings.Count; i++)
                {
                    var candidate = openings[i];
                    if (candidate.Floor != CurrentFloor || (slot == 1 && candidate.Id == pendingIds[0])) continue;
                    int other = slot == 1 ? Find(pendingIds[0]) : -1;
                    if (other >= 0 && Vector3.Distance(openings[other].Position, candidate.Position) < 5) continue;
                    float distance = (candidate.Position - view).sqrMagnitude;
                    if (candidate.Id == sourceIds[slot]) distance *= 1 - raySettings.selectionHysteresis;
                    if (distance < score) { score = distance; best = candidate.Id; }
                }
                pendingIds[slot] = best;
            }
        }
        private void UpdateLights(float dt, float duration)
        {
            if (bounceLights == null) return;
            var p = cycle.profile;
            float day = cycle.Daylight;
            Color tint = Color.Lerp(p.moonColor, Color.Lerp(p.sunsetSun, p.daySun, day), day);
            for (int i = 0; i < bounceLights.Length; i++)
            {
                var light = bounceLights[i]; if (light == null) continue;
                if (i >= 2 || raySettings == null) { light.enabled = false; continue; }
                bool changing = pendingIds[i] != sourceIds[i];
                sourceWeights[i] = Mathf.MoveTowards(sourceWeights[i], changing || pendingIds[i] < 0 ? 0 : 1, dt / Mathf.Max(.1f, duration));
                if (changing && sourceWeights[i] <= 0)
                {
                    sourceIds[i] = pendingIds[i]; int index = Find(sourceIds[i]);
                    if (index >= 0)
                    {
                        var opening = openings[index]; light.transform.position = opening.Position;
                        light.transform.rotation = Quaternion.LookRotation((opening.Inward + Vector3.down * Mathf.Tan(raySettings.fillDownAngle * Mathf.Deg2Rad)).normalized);
                    }
                }
                light.type = LightType.Spot; light.range = raySettings.fillRange;
                light.spotAngle = raySettings.fillAngle; light.innerSpotAngle = raySettings.fillAngle * .65f;
                light.shadows = LightShadows.Soft; light.shadowCustomResolution = 512;
                light.shadowBias = .04f; light.shadowNormalBias = .15f;
                light.color = tint;
                light.intensity = raySettings.fillStrength * Mathf.Lerp(.12f, 1, day) * sourceWeights[i] * InteriorWeight;
                light.enabled = sourceIds[i] >= 0 && light.intensity > .001f;
            }
        }
    }
}
