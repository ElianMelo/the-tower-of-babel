using UnityEngine;

namespace TowerOfBabel.Interface
{
    public enum AeroUICue { Hover, Press, Open, Close, Success, Change }

    /// <summary>Quiet shared glass/water cues. No per-button sources or external audio assets.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(AudioSource))]
    public sealed class AeroUIAudio : MonoBehaviour
    {
        [SerializeField] private bool playSounds = true;
        [SerializeField, Range(0, 1)] private float volume = .14f;
        private static AeroUIAudio instance;
        private AudioSource source;
        private AudioClip[] clips;
        private float lastHover = -1;
        private float lastChange = -1;

        private void Awake()
        {
            if (instance != null && instance != this) { enabled = false; return; }
            instance = this; source = GetComponent<AudioSource>();
            source.playOnAwake = false; source.spatialBlend = 0;
            clips = new AudioClip[6];
            for (int cue = 0; cue < clips.Length; cue++)
            {
                const int rate = 22050;
                float duration = cue == (int)AeroUICue.Success ? .22f : cue >= 2 ? .14f : .065f;
                var samples = new float[Mathf.CeilToInt(duration * rate)];
                float frequency = cue switch { 0 => 1120, 1 => 820, 2 => 620, 3 => 700, 4 => 880, _ => 960 };
                for (int i = 0; i < samples.Length; i++)
                {
                    float t = (float)i / rate, u = t / duration;
                    float envelope = Mathf.Sin(Mathf.PI * u) * Mathf.Exp(-u * 4);
                    float glide = cue == 3 ? -.25f : .22f;
                    float wave = Mathf.Sin(Mathf.PI * 2 * frequency * (t + glide * t * u));
                    if (cue == 4) wave = wave * .65f + Mathf.Sin(Mathf.PI * 2 * frequency * 1.5f * t) * .35f;
                    samples[i] = wave * envelope * .35f;
                }
                clips[cue] = AudioClip.Create("AeroUI" + (AeroUICue)cue, samples.Length, 1, rate, false);
                clips[cue].SetData(samples, 0);
            }
        }

        public static void Play(AeroUICue cue)
        {
            var player = instance;
            if (!Application.isPlaying || player == null || !player.isActiveAndEnabled || !player.playSounds || player.clips == null) return;
            if (cue == AeroUICue.Hover)
            {
                if (Time.unscaledTime - player.lastHover < .065f) return;
                player.lastHover = Time.unscaledTime;
            }
            if (cue == AeroUICue.Change)
            {
                if (Time.unscaledTime - player.lastChange < .1f) return;
                player.lastChange = Time.unscaledTime;
            }
            player.source.PlayOneShot(player.clips[(int)cue], player.volume * (cue == AeroUICue.Hover ? .4f : 1));
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
            if (clips != null) foreach (var clip in clips) if (clip != null) Destroy(clip);
        }
    }
}
