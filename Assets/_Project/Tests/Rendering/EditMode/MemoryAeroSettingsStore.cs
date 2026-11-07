using System.Collections.Generic;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>In-memory settings so tests never touch PlayerPrefs.</summary>
    public sealed class MemoryAeroSettingsStore : IAeroSettingsStore
    {
        public readonly Dictionary<string, int> values = new();
        public int GetInt(string key, int fallback) => values.TryGetValue(key, out var v) ? v : fallback;
        public void SetInt(string key, int value) => values[key] = value;
    }
}
