using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Bounded mesh chips, dust, stroke accents and cooldown markers; no gameplay colliders move.</summary>
    [DisallowMultipleComponent]
    public sealed class ResourceHarvestFeedback : MonoBehaviour, IHarvestFeedback
    {
        [SerializeField] private bool sand;
        [SerializeField] private Transform visualRoot;
        [SerializeField] private GameObject cooldownMarker;
        [SerializeField] private ParticleSystem chips, dust;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip strokeClip, breakClip, respawnClip;
        private AeroInteractionMotion motion;
        private bool working, depleted;
        private float progress, impact, respawnTime;
        private int stroke;
        public bool IsSand => sand;
        public bool IsDepleted => depleted;
        public bool MarkerVisible => cooldownMarker != null && cooldownMarker.activeSelf;
        public float PresentationScale => sand ? Mathf.Lerp(1, .16f, progress) : 1;
        public int CompletionCount { get; private set; }

        private void Awake() => ResetFeedback();
        private void Bind() => motion ??= GetComponent<AeroInteractionMotion>();
        public void BeginWork()
        {
            Bind(); working = true; progress = 0; stroke = 0; respawnTime = 0;
            Strike();
        }
        public void SetProgress(float value)
        {
            if (!working) return;
            progress = Mathf.Clamp01(value);
            int next = Mathf.FloorToInt(progress * 7);
            if (next > stroke) { stroke = next; Strike(); }
            ApplyPose();
        }
        private void Strike()
        {
            impact = 1;
            Emit(chips, sand ? 5 : 3);
            Emit(dust, sand ? 9 : 3);
            Sound(strokeClip, sand ? .22f : .3f, Mathf.Lerp(.88f, 1.15f, progress));
        }
        public void StopWork()
        {
            working = false; progress = 0; impact = 0;
            motion?.SetGatherPose(Vector3.one, 0, false);
        }
        public void Deplete()
        {
            if (depleted) return;
            depleted = true; CompletionCount++; working = false; respawnTime = 0;
            if (cooldownMarker != null) cooldownMarker.SetActive(true);
            Emit(chips, sand ? 15 : 24);
            Emit(dust, sand ? 24 : 15);
            Sound(breakClip, .48f, 1);
        }
        public void Respawn()
        {
            depleted = false; progress = 0;
            if (cooldownMarker != null) cooldownMarker.SetActive(false);
            Bind();
            if (motion != null && visualRoot != null)
            {
                motion.Begin(visualRoot, 0, 0); respawnTime = .38f; ApplyPose();
            }
            Emit(dust, 8);
            Sound(respawnClip, .2f, 1);
        }
        private void Update()
        {
            if (!working && respawnTime <= 0) return;
            impact = Mathf.MoveTowards(impact, 0, Time.deltaTime * 8);
            if (respawnTime > 0)
            {
                respawnTime = Mathf.Max(0, respawnTime - Time.deltaTime);
                if (respawnTime == 0) { motion?.Stop(true); return; }
            }
            ApplyPose();
        }
        private void ApplyPose()
        {
            Vector3 scale = sand ? new Vector3(Mathf.Lerp(1, .6f, progress), PresentationScale, Mathf.Lerp(1, .6f, progress))
                : new Vector3(1 + impact * .025f, 1 - impact * .04f, 1 + impact * .025f);
            if (respawnTime > 0)
            {
                float t = 1 - respawnTime / .38f;
                scale = Vector3.one * Mathf.Lerp(.08f, 1, 1 - Mathf.Pow(1 - t, 3));
            }
            motion?.SetGatherPose(scale, impact, sand || respawnTime > 0);
        }
        private void Sound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || audioSource == null) return;
            audioSource.pitch = pitch + Random.Range(-.045f, .045f);
            audioSource.PlayOneShot(clip, volume);
        }
        private static void Emit(ParticleSystem particles, int count)
        {
            if (particles == null) return;
            if (!particles.isPlaying) particles.Play();
            particles.Emit(count);
        }
        public void ResetFeedback()
        {
            working = depleted = false; progress = impact = respawnTime = 0;
            if (cooldownMarker != null) cooldownMarker.SetActive(false);
            if (chips != null) chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (dust != null) dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (audioSource != null) audioSource.Stop();
            motion?.Stop(true);
        }
        private void OnDisable() => ResetFeedback();
#if UNITY_EDITOR
        public void SampleEditorPose(float seconds, float normalizedProgress)
        {
            if (Application.isPlaying) return;
            SetProgress(normalizedProgress);
            impact = Mathf.Exp(-Mathf.Repeat(normalizedProgress * 7, 1) * 4);
            motion?.SampleEditorPose(seconds, normalizedProgress);
            ApplyPose();
        }
#endif
    }
}
