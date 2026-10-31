using UnityEngine;

namespace TowerOfBabel.Weather
{
    public enum WeatherRequest : byte { Rain, TimeOfDay, FreezeTime, AutomaticWeather }

    /// <summary>
    /// Authoritative weather rules, free of networking and scene objects so they can be tested directly.
    /// The server (or an offline session) owns one; clients only mirror the state it broadcasts.
    /// </summary>
    public sealed class WeatherDirector
    {
        public WeatherState State;
        public readonly WeatherSchedule Schedule;

        public WeatherDirector(WeatherSchedule schedule, WeatherState initial)
        {
            Schedule = schedule ?? new WeatherSchedule();
            State = initial;
            State.TimeOfDay = Mathf.Repeat(State.TimeOfDay, 1);
            Schedule.Force(State.Raining);
        }

        public static bool IsValid(WeatherRequest kind, float value) =>
            kind is >= WeatherRequest.Rain and <= WeatherRequest.AutomaticWeather && !float.IsNaN(value) && !float.IsInfinity(value);

        /// <summary>The state a request leads to. Manually setting rain stops automatic weather so the choice holds.</summary>
        public static WeatherState Modify(WeatherState state, WeatherRequest kind, float value)
        {
            bool on = value >= .5f;
            switch (kind)
            {
                case WeatherRequest.Rain: state.Raining = on; state.AutomaticWeather = false; break;
                case WeatherRequest.TimeOfDay: state.TimeOfDay = Mathf.Repeat(value, 1); break;
                case WeatherRequest.FreezeTime: state.TimeFrozen = on; break;
                case WeatherRequest.AutomaticWeather: state.AutomaticWeather = on; break;
            }
            return state;
        }

        /// <summary>Applies a validated request; returns false when it is rejected.</summary>
        public bool Apply(WeatherRequest kind, float value)
        {
            if (!IsValid(kind, value)) return false;
            bool wasRaining = State.Raining, wasAutomatic = State.AutomaticWeather;
            State = Modify(State, kind, value);
            // A new spell starts whenever rain is set by hand or automatic weather resumes.
            if (State.Raining != wasRaining || (State.AutomaticWeather && !wasAutomatic)) Schedule.Force(State.Raining);
            return true;
        }

        /// <summary>Runs automatic weather; returns true when rain starts or stops.</summary>
        public bool Tick(float dt)
        {
            if (!State.AutomaticWeather || !Schedule.Tick(dt)) return false;
            State.Raining = Schedule.Raining;
            return true;
        }
    }
}
