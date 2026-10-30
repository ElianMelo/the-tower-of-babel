using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.Rendering
{
    public enum AeroPostLook { Restrained = 0, BrightAero = 1, Y2K = 2 }

    /// <summary>Where the chosen look is remembered. Tests inject an in-memory store.</summary>
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

    /// <summary>
    /// Swaps the global Volume between the Restrained (accepted baseline), Bright Aero
    /// (reference look) and Y2K (grain/fringe variant) profiles, with a short crossfade.
    /// Local volumes such as the tower exit atmosphere keep layering on top.
    /// </summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Volume))]
    public sealed class AeroPostProfileSwitcher : MonoBehaviour
    {
        public const string PreferenceKey = "Aero.PostLook";

        [Serializable]
        public struct LookSettings
        {
            public VolumeProfile profile;
            [Tooltip("Daytime haze colour for this look.")]
            public Color dayFog;
        }

        public LookSettings restrained = new() { dayFog = new Color(.62f, .76f, .86f) };
        public LookSettings brightAero = new() { dayFog = new Color(.80f, .91f, 1f) };
        public LookSettings y2k = new() { dayFog = new Color(.78f, .9f, 1f) };
        public AeroPostLook defaultLook = AeroPostLook.Restrained;
        [Min(0)] public float crossfadeSeconds = .5f;
        public AeroDayNightCycle dayNight;

        private Volume volume;
        private Volume fadeVolume;
        private float fade = 1;
        private IAeroSettingsStore store;
        private bool loaded;

        public AeroPostLook Current { get; private set; }
        public IAeroSettingsStore Store { get => store ??= new PlayerPrefsAeroSettingsStore(); set { store = value; loaded = false; } }

        public LookSettings Settings(AeroPostLook look) => look switch
        {
            AeroPostLook.BrightAero => brightAero,
            AeroPostLook.Y2K => y2k,
            _ => restrained
        };

        private void OnEnable()
        {
            volume = GetComponent<Volume>();
            if (!Application.isPlaying) { Current = defaultLook; return; }
            LoadSaved();
        }

        private void OnDisable() => DestroyFade();

        private void LoadSaved()
        {
            loaded = true;
            int saved = Store.GetInt(PreferenceKey, (int)defaultLook);
            Apply(Enum.IsDefined(typeof(AeroPostLook), saved) ? (AeroPostLook)saved : defaultLook, false, false);
        }

        /// <summary>Selects a look, optionally remembering it for the next session.</summary>
        public void Set(AeroPostLook look, bool persist = true) => Apply(look, persist, Application.isPlaying && crossfadeSeconds > 0);

        public AeroPostLook Cycle()
        {
            var next = (AeroPostLook)(((int)Current + 1) % 3);
            Set(next);
            return next;
        }

        private void Apply(AeroPostLook look, bool persist, bool crossfade)
        {
            if (volume == null) volume = GetComponent<Volume>();
            var settings = Settings(look);
            if (settings.profile == null) return;
            if (crossfade && volume.sharedProfile != null && volume.sharedProfile != settings.profile)
            {
                // The outgoing profile fades out on a temporary twin volume.
                if (fadeVolume == null)
                {
                    var go = new GameObject("Aero post crossfade") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.SetParent(transform, false);
                    fadeVolume = go.AddComponent<Volume>();
                }
                fadeVolume.isGlobal = true;
                fadeVolume.priority = volume.priority + .01f;
                fadeVolume.sharedProfile = volume.sharedProfile;
                fadeVolume.weight = 1;
                fade = 0;
            }
            volume.sharedProfile = settings.profile;
            Current = look;
            if (dayNight != null) dayNight.dayFogColor = settings.dayFog;
            if (persist) Store.SetInt(PreferenceKey, (int)look);
        }

        private void Update()
        {
            if (Application.isPlaying && !loaded) LoadSaved();
            if (fadeVolume == null) return;
            fade = crossfadeSeconds <= 0 ? 1 : Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / crossfadeSeconds);
            fadeVolume.weight = 1 - Mathf.SmoothStep(0, 1, fade);
            if (fade >= 1) DestroyFade();
        }

        private void DestroyFade()
        {
            if (fadeVolume == null) return;
            if (Application.isPlaying) Destroy(fadeVolume.gameObject); else DestroyImmediate(fadeVolume.gameObject);
            fadeVolume = null;
        }
    }
}
