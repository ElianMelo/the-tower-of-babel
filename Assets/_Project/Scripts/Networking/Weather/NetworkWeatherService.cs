using System;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using TowerOfBabel.Rendering;
using TowerOfBabel.Weather;
using UnityEngine;

namespace TowerOfBabel.Networking.Weather
{
    /// <summary>
    /// Server-authoritative time of day and weather. The server (or an offline session) runs the
    /// <see cref="WeatherDirector"/>, and every client applies the same state to its day/night cycle and
    /// wetness/rain visuals. State is sent buffered, so late joiners match, and re-sent periodically to
    /// keep every client's clock on the server's.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkWeatherService : NetworkBehaviour
    {
        public static NetworkWeatherService Instance { get; private set; }

        [SerializeField] private AeroDayNightCycle dayNight;
        [SerializeField] private AeroWetnessController wetness;
        [SerializeField] private WeatherSchedule schedule = new();
        [SerializeField] private bool automaticWeather = true;
        [Tooltip("Seconds between clock re-syncs sent by the server.")]
        [SerializeField, Min(1)] private float resyncSeconds = 10;
        [Tooltip("Clients jump to the server clock when they drift more than this many seconds.")]
        [SerializeField, Min(0)] private float snapSeconds = 1.5f;

        private WeatherDirector director;
        private NetworkObject networkObject;
        private float resyncTimer;

        /// <summary>Raised on every client (and offline) when the applied weather changes.</summary>
        public event Action<WeatherState> StateChanged;

        public WeatherState State
        {
            get
            {
                EnsureDirector();
                var state = director.State;
                if (dayNight != null) state.TimeOfDay = dayNight.timeOfDay;
                return state;
            }
        }
        public bool Raining => State.Raining;
        public bool TimeFrozen => State.TimeFrozen;
        public bool AutomaticWeather => State.AutomaticWeather;
        public WeatherSchedule Schedule => schedule;

        private static bool NetworkActive => InstanceFinder.IsServerStarted || InstanceFinder.IsClientStarted;
        /// <summary>True on the server, or everywhere while no session is running.</summary>
        public bool HasAuthority => InstanceFinder.IsServerStarted || !NetworkActive;
        private bool Spawned => networkObject != null && networkObject.IsSpawned;
        private float CycleSeconds => dayNight != null ? dayNight.CycleSeconds : 1200;

        private void Awake()
        {
            Instance = this;
            networkObject = GetComponent<NetworkObject>();
            EnsureDirector();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Scene references used when the inspector fields are empty, and by tests.</summary>
        public void Bind(AeroDayNightCycle cycle, AeroWetnessController wet)
        {
            dayNight = cycle; wetness = wet; director = null; EnsureDirector();
        }

        private void EnsureDirector()
        {
            if (dayNight == null) dayNight = FindFirstObjectByType<AeroDayNightCycle>();
            if (wetness == null) wetness = dayNight != null ? dayNight.GetComponent<AeroWetnessController>() : FindFirstObjectByType<AeroWetnessController>();
            if (director != null) return;
            director = new WeatherDirector(schedule, new WeatherState
            {
                TimeOfDay = dayNight != null ? dayNight.timeOfDay : .3f,
                Raining = wetness != null && wetness.Raining,
                AutomaticWeather = automaticWeather
            });
            Push(director.State, false);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            EnsureDirector();
            Broadcast();
        }

        private void Update() => Tick(Time.deltaTime);

        /// <summary>Runs automatic weather and clock re-sync while this side has authority. Public for tests.</summary>
        public void Tick(float dt)
        {
            EnsureDirector();
            if (!HasAuthority) return;
            if (dayNight == null && !director.State.TimeFrozen)
                director.State.TimeOfDay = WeatherClock.Advance(director.State.TimeOfDay, dt, CycleSeconds, false);
            bool flipped = director.Tick(dt);
            if (flipped) Push(director.State, false);
            if (!InstanceFinder.IsServerStarted) return;
            resyncTimer -= dt;
            if (flipped || resyncTimer <= 0) Broadcast();
        }

        public bool RequestRain(bool raining) => Request(WeatherRequest.Rain, raining ? 1 : 0);
        public bool RequestTimeOfDay(float timeOfDay) => Request(WeatherRequest.TimeOfDay, timeOfDay);
        public bool RequestDay() => RequestTimeOfDay(WeatherClock.Noon);
        public bool RequestNight() => RequestTimeOfDay(WeatherClock.Midnight);
        public bool RequestFreezeTime(bool frozen) => Request(WeatherRequest.FreezeTime, frozen ? 1 : 0);
        public bool RequestAutomaticWeather(bool automatic) => Request(WeatherRequest.AutomaticWeather, automatic ? 1 : 0);

        /// <summary>Applies directly with authority, otherwise asks the server (showing the change at once).</summary>
        public bool Request(WeatherRequest kind, float value)
        {
            EnsureDirector();
            if (!WeatherDirector.IsValid(kind, value)) return false;
            if (HasAuthority) { ServerApply(kind, value); return true; }
            if (!Spawned) return false;
            director.State = WeatherDirector.Modify(State, kind, value);
            Push(director.State, kind == WeatherRequest.TimeOfDay);
            WeatherServerRpc(kind, value);
            return true;
        }

        [ServerRpc(RequireOwnership = false)]
        private void WeatherServerRpc(WeatherRequest kind, float value, NetworkConnection sender = null) => ServerApply(kind, value);

        private void ServerApply(WeatherRequest kind, float value)
        {
            CaptureClock();
            if (!director.Apply(kind, value)) return;
            Push(director.State, kind == WeatherRequest.TimeOfDay);
            if (InstanceFinder.IsServerStarted) Broadcast();
        }

        private void CaptureClock()
        {
            if (dayNight != null) director.State.TimeOfDay = dayNight.timeOfDay;
        }

        private void Broadcast()
        {
            resyncTimer = resyncSeconds;
            if (!Spawned || !IsServerStarted) return;
            CaptureClock();
            var s = director.State;
            StateObserversRpc(s.TimeOfDay, TimeManager.Tick, s.Raining, s.TimeFrozen, s.AutomaticWeather);
        }

        [ObserversRpc(BufferLast = true, ExcludeServer = true)]
        private void StateObserversRpc(float timeOfDay, uint tick, bool raining, bool frozen, bool automatic)
        {
            double elapsed = TimeManager != null ? TimeManager.TimePassed(tick) : 0;
            ReceiveState(new WeatherState
            {
                TimeOfDay = WeatherClock.Advance(timeOfDay, elapsed, CycleSeconds, frozen),
                Raining = raining, TimeFrozen = frozen, AutomaticWeather = automatic
            });
        }

        /// <summary>Client side: mirror the server, snapping the clock only when it drifted noticeably.</summary>
        public void ReceiveState(WeatherState state)
        {
            EnsureDirector();
            float drift = dayNight != null ? Mathf.Abs(WeatherClock.Delta(dayNight.timeOfDay, state.TimeOfDay)) * CycleSeconds : float.MaxValue;
            director.State = state;
            Push(state, drift > snapSeconds);
        }

        private void Push(WeatherState state, bool setTime)
        {
            if (dayNight != null)
            {
                dayNight.paused = state.TimeFrozen;
                if (setTime) dayNight.SetTime(state.TimeOfDay);
            }
            if (wetness != null) wetness.Raining = state.Raining;
            StateChanged?.Invoke(state);
        }
    }
}
