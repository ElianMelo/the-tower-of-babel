using System;
using UnityEngine;

namespace TowerOfBabel.Weather
{
    /// <summary>Synced world weather: what the server broadcasts and every client applies.</summary>
    [Serializable]
    public struct WeatherState : IEquatable<WeatherState>
    {
        public float TimeOfDay;
        public bool Raining;
        public bool TimeFrozen;
        public bool AutomaticWeather;

        public bool Equals(WeatherState other) => Mathf.Approximately(TimeOfDay, other.TimeOfDay) && Raining == other.Raining &&
                                                  TimeFrozen == other.TimeFrozen && AutomaticWeather == other.AutomaticWeather;
        public override bool Equals(object obj) => obj is WeatherState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(TimeOfDay, Raining, TimeFrozen, AutomaticWeather);
    }

    public static class WeatherClock
    {
        public const float Noon = .5f, Midnight = 0f;

        /// <summary>Time of day after <paramref name="elapsedSeconds"/> of a cycle lasting <paramref name="cycleSeconds"/>.</summary>
        public static float Advance(float timeOfDay, double elapsedSeconds, float cycleSeconds, bool frozen)
        {
            if (frozen || elapsedSeconds <= 0 || cycleSeconds <= 0) return Mathf.Repeat(timeOfDay, 1);
            return (float)((timeOfDay + elapsedSeconds / cycleSeconds) % 1.0 + 1.0) % 1f;
        }

        /// <summary>Shortest signed distance between two times of day, in cycle fractions (-.5 .. .5).</summary>
        public static float Delta(float from, float to) => Mathf.Repeat(to - from + .5f, 1) - .5f;
    }

    /// <summary>
    /// Server-side automatic weather: alternating clear and rainy spells of random length,
    /// independent of the time of day so rain can fall by day or by night.
    /// </summary>
    [Serializable]
    public sealed class WeatherSchedule
    {
        [Tooltip("Clear spell length range, seconds.")]
        public Vector2 clearSeconds = new(420, 900);
        [Tooltip("Rain spell length range, seconds.")]
        public Vector2 rainSeconds = new(150, 360);

        [NonSerialized] private System.Random random;
        [NonSerialized] private float remaining = -1;

        public bool Raining { get; private set; }
        public float Remaining => remaining;

        public void Seed(int seed) { random = new System.Random(seed); remaining = -1; }

        /// <summary>Starts a spell of the given kind with a fresh random length.</summary>
        public void Force(bool raining)
        {
            Raining = raining;
            remaining = Next(raining ? rainSeconds : clearSeconds);
        }

        /// <summary>Advances the spell timer; returns true when the weather flips.</summary>
        public bool Tick(float dt)
        {
            if (remaining < 0) { Force(Raining); return false; }
            remaining -= Mathf.Max(0, dt);
            if (remaining > 0) return false;
            Force(!Raining);
            return true;
        }

        private float Next(Vector2 range)
        {
            random ??= new System.Random(Environment.TickCount);
            float min = Mathf.Max(1, Mathf.Min(range.x, range.y)), max = Mathf.Max(min, Mathf.Max(range.x, range.y));
            return Mathf.Lerp(min, max, (float)random.NextDouble());
        }
    }
}
