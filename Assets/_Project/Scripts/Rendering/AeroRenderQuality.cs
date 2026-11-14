using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Runtime quality switches for the Aero render features, set by the game's graphics settings. The defaults are
    /// the authored look (High): every feature at the quality saved in its own settings asset. Lower tiers trade
    /// quality for frame time without editing those assets.
    /// </summary>
    public static class AeroRenderQuality
    {
        public const int Off = 0, Low = 1, Full = 2;
        /// <summary>Interior sun shafts: 0 off, 1 quarter resolution with fewer steps, 2 as authored.</summary>
        public static int SunShafts = Full;
        /// <summary>Volumetric clouds: 0 low, 1 medium, 2 as authored.</summary>
        public static int Clouds = Full;
        /// <summary>The bounded live floor reflection; off leaves the environment reflection.</summary>
        public static bool LiveReflections = true;
        /// <summary>River glass refracts one floor at half resolution instead of up to three.</summary>
        public static bool ReducedRiverRefraction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            SunShafts = Full; Clouds = Full; LiveReflections = true; ReducedRiverRefraction = false;
        }
    }

    /// <summary>
    /// Volume of each sound group, 0 to 1, set by the game's audio settings. Sounds multiply their own volume by their
    /// group's level; the master level is applied to the listener by the settings.
    /// </summary>
    public static class AeroAudio
    {
        public static float Music = 1f;
        /// <summary>Interface cues: hover, press, open, experience pops.</summary>
        public static float Ui = 1f;
        /// <summary>World effects: rain, harvesting, machines.</summary>
        public static float Vfx = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() { Music = Ui = Vfx = 1f; }
    }
}
