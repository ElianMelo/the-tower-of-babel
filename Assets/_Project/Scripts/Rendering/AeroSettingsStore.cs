using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Where local visual choices (such as the season) are remembered. Tests inject an in-memory store.</summary>
    public interface IAeroSettingsStore
    {
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
    }

    public sealed class PlayerPrefsAeroSettingsStore : IAeroSettingsStore
    {
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) { PlayerPrefs.SetInt(key, value); PlayerPrefs.Save(); }
    }
}
