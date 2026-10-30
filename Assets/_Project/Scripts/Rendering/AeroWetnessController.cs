using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Drives the global wetness film: a subtle dew that follows the day/night cycle,
    /// ramping to a full rain film (moving drops, wall streaks, ripples) while it rains.
    /// Visual only and local to each client; nothing here is networked.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class AeroWetnessController : MonoBehaviour
    {
        public static AeroWetnessController Instance { get; private set; }

        public AeroWetnessProfile profile;
        [Tooltip("Optional: dew follows its time of day.")]
        public AeroDayNightCycle dayNight;
        [Tooltip("Optional: tower geometry for rain shelter under upper floors.")]
        public AeroTowerAtmosphere tower;
        [SerializeField] private bool raining;
        [Tooltip("Fixed shader clock for previews and tests; negative uses game time.")]
        public float previewTime = -1;

        private float? overrideWetness;
        private float clock;

        public bool Raining { get => raining; set => raining = value; }
        /// <summary>Current film coverage 0..1 sent to shaders.</summary>
        public float Wetness { get; private set; }
        /// <summary>Current active rain 0..1 (drops, streaks, ripples).</summary>
        public float RainAmount { get; private set; }
        public float? WetnessOverride => overrideWetness;

        public static readonly int WetnessId = Shader.PropertyToID("_AeroWetness");
        public static readonly int RainId = Shader.PropertyToID("_AeroRain");
        public static readonly int ClockId = Shader.PropertyToID("_AeroWetClock");
        public static readonly int ReadyId = Shader.PropertyToID("_AeroWetReady");
        public static readonly int ShelteredId = Shader.PropertyToID("_AeroWetnessSheltered");
        public static readonly int DropParamsId = Shader.PropertyToID("_AeroRainDropParams");
        public static readonly int StreakParamsId = Shader.PropertyToID("_AeroRainStreakParams");
        public static readonly int ShelterId = Shader.PropertyToID("_AeroRainShelter");
        public static readonly int Shelter2Id = Shader.PropertyToID("_AeroRainShelter2");
        public static readonly int DropsId = Shader.PropertyToID("_AeroRainDrops");
        public static readonly int StreaksId = Shader.PropertyToID("_AeroRainStreaks");
        public static readonly int RipplesId = Shader.PropertyToID("_AeroRainRipples");

        /// <summary>Force a film level (0 dry, ~.2 dew, 1 soaked); null returns to weather.</summary>
        public void OverrideWetness(float? value)
        {
            overrideWetness = value.HasValue ? Mathf.Clamp01(value.Value) : null;
            if (overrideWetness.HasValue) Wetness = overrideWetness.Value;
        }

        /// <summary>Cycles dry -> dew -> soaked -> weather-driven; returns a label.</summary>
        public string CycleWetnessPreset()
        {
            if (!overrideWetness.HasValue) { OverrideWetness(0); return "Dry"; }
            if (overrideWetness.Value < .01f) { OverrideWetness(.22f); return "Dew"; }
            if (overrideWetness.Value < .99f) { OverrideWetness(1); return "Soaked"; }
            OverrideWetness(null); return "Weather";
        }

        private void OnEnable()
        {
            Instance = this;
            if (profile != null) Wetness = TargetDew();
            Apply();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            Shader.SetGlobalFloat(ReadyId, 0);
            Shader.SetGlobalFloat(WetnessId, 0);
            Shader.SetGlobalFloat(RainId, 0);
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 1f / 30f;
            clock = previewTime >= 0 ? previewTime : (Application.isPlaying ? Time.time : (float)(Time.realtimeSinceStartupAsDouble % 3600));
            Tick(dt);
        }

        private float TargetDew() => profile == null ? 0 : profile.DewAt(dayNight != null ? dayNight.timeOfDay : .3f);

        /// <summary>Advances ramps by dt seconds and publishes the globals.</summary>
        public void Tick(float dt)
        {
            if (profile == null) { Apply(); return; }
            float dew = TargetDew();
            if (overrideWetness.HasValue)
                Wetness = overrideWetness.Value;
            else if (raining)
                Wetness = Mathf.MoveTowards(Wetness, Mathf.Max(dew, profile.rainWetness), dt / profile.rampIn);
            else
                Wetness = Mathf.MoveTowards(Wetness, dew, dt / (Wetness > dew ? profile.dryOut : profile.rampIn));
            RainAmount = Mathf.MoveTowards(RainAmount, raining ? 1 : 0, dt / profile.rainFade);
            Apply();
        }

        private void Apply()
        {
            bool ready = profile != null && profile.TexturesReady && isActiveAndEnabled;
            Shader.SetGlobalFloat(ReadyId, ready ? 1 : 0);
            if (profile == null) return;
            Shader.SetGlobalFloat(WetnessId, Wetness);
            Shader.SetGlobalFloat(RainId, RainAmount);
            Shader.SetGlobalFloat(ShelteredId, TargetDew());
            Shader.SetGlobalFloat(ClockId, previewTime >= 0 ? previewTime : clock);
            Shader.SetGlobalVector(DropParamsId, new Vector4(profile.dropTileSize, profile.dropSpeed, profile.dewDropWeight, profile.normalStrength));
            Shader.SetGlobalVector(StreakParamsId, new Vector4(profile.streakSize.x, profile.streakSize.y, profile.streakSpeed, profile.streakDistortion));
            if (tower != null && profile.shelterEnabled)
            {
                Shader.SetGlobalVector(ShelterId, new Vector4(tower.center.x, tower.center.y, tower.baseRadius, tower.decrease));
                Shader.SetGlobalVector(Shelter2Id, new Vector4(tower.floorHeight, tower.levels, 1, 0));
            }
            else Shader.SetGlobalVector(Shelter2Id, Vector4.zero);
            if (profile.drops != null) Shader.SetGlobalTexture(DropsId, profile.drops);
            if (profile.streaks != null) Shader.SetGlobalTexture(StreaksId, profile.streaks);
            if (profile.ripples != null) Shader.SetGlobalTexture(RipplesId, profile.ripples);
        }
    }
}
