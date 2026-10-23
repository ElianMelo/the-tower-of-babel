using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>A heating, breathing kiln cycle with output shown only after the server confirms exchange.</summary>
    [DisallowMultipleComponent]
    public sealed class GlassFurnaceAnimation : MonoBehaviour
    {
        [SerializeField] private Transform bellows, flywheel, lid, sandFeed, glassOutput, moltenPool;
        [SerializeField] private Renderer[] heatSurfaces;
        [SerializeField] private ParticleSystem embers, steam, glassSparkles;
        [SerializeField] private AudioSource motor, accents;
        [SerializeField] private AudioClip completionClip;
        private Transform[] parts;
        private Vector3[] positions, scales;
        private Quaternion[] rotations;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock[] originalBlocks;
        private bool initialized, working, confirmed;
        private float progress, elapsed, blend, celebration, angle;
        private const float OutputDuration = 1.25f;
        public bool IsWorking => working;
        public bool IsOutputVisible => glassOutput != null && glassOutput.gameObject.activeSelf;
        public int CompletionCount { get; private set; }
        public Vector3 FeedbackPosition => glassOutput != null ? glassOutput.position + Vector3.up * .15f : transform.position;

        private void Awake() { Initialize(); ResetImmediately(); }
        private void Initialize()
        {
            if (initialized) return;
            initialized = true; parts = new[] { bellows, flywheel, lid, sandFeed, glassOutput, moltenPool };
            positions = new Vector3[6]; scales = new Vector3[6]; rotations = new Quaternion[6];
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { positions[i] = parts[i].localPosition; scales[i] = parts[i].localScale; rotations[i] = parts[i].localRotation; }
            heatSurfaces ??= System.Array.Empty<Renderer>();
            block = new MaterialPropertyBlock(); originalBlocks = new MaterialPropertyBlock[heatSurfaces.Length];
            for (int i = 0; i < heatSurfaces.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (heatSurfaces[i] != null) heatSurfaces[i].GetPropertyBlock(originalBlocks[i]);
            }
        }
        public void BeginWork()
        {
            Initialize(); working = true; confirmed = false; progress = elapsed = celebration = 0;
            embers?.Play(); steam?.Play();
            if (motor != null && !motor.isPlaying) motor.Play();
            ApplyPose();
        }
        public void SetProgress(float value) { progress = Mathf.Clamp01(value); ApplyPose(); }
        public void StopWork()
        {
            working = false;
            embers?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            steam?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (!confirmed) progress = 0;
            ApplyPose();
        }
        public void ConfirmCompletion()
        {
            Initialize();
            if (confirmed) return;
            StopWork(); confirmed = true; progress = 1; celebration = OutputDuration; CompletionCount++;
            if (glassSparkles != null) { glassSparkles.Play(); glassSparkles.Emit(18); }
            if (steam != null) { steam.Play(); steam.Emit(14); steam.Stop(true, ParticleSystemStopBehavior.StopEmitting); }
            if (accents != null && completionClip != null) accents.PlayOneShot(completionClip, .45f);
            ApplyPose();
        }
        private void Update()
        {
            if (!working && blend <= 0 && celebration <= 0) return;
            elapsed += Time.deltaTime;
            blend = Mathf.MoveTowards(blend, working ? 1 : 0, Time.deltaTime / .25f);
            angle = Mathf.Repeat(angle + Time.deltaTime * (100 + Mathf.SmoothStep(0, 1, progress) * 180) * blend, 360);
            celebration = Mathf.Max(0, celebration - Time.deltaTime);
            ApplyPose();
        }
        private void ApplyPose()
        {
            if (!initialized) return;
            float breath = Mathf.Sin(elapsed * 8);
            float heat = Mathf.SmoothStep(0, 1, progress);
            if (bellows != null) bellows.localScale = Vector3.Scale(scales[0], new Vector3(1 + breath * .18f * blend, 1 - breath * .025f * blend, 1 - breath * .025f * blend));
            if (flywheel != null) flywheel.localRotation = rotations[1] * Quaternion.AngleAxis(angle, Vector3.right);
            if (lid != null) lid.localPosition = positions[2] + Vector3.up * ((.02f + .012f * breath) * blend * heat);
            if (sandFeed != null)
            {
                sandFeed.gameObject.SetActive(working && progress < .86f);
                float enter = Mathf.SmoothStep(0, 1, elapsed / .35f);
                float consume = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.12f, .86f, progress));
                sandFeed.localPosition = positions[3] + Vector3.up * ((1 - enter) * .4f - consume * .24f);
                sandFeed.localScale = scales[3] * (1 - consume * .96f);
            }
            if (moltenPool != null)
            {
                moltenPool.gameObject.SetActive(working && progress > .18f);
                moltenPool.localScale = scales[5] * Mathf.Lerp(.15f, 1, heat);
            }
            if (glassOutput != null)
            {
                bool visible = confirmed && celebration > 0;
                glassOutput.gameObject.SetActive(visible);
                float t = 1 - celebration / OutputDuration;
                float reveal = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .2f), 3);
                float collect = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, t));
                glassOutput.localScale = scales[4] * (visible ? reveal * (1 - collect) : 1);
                glassOutput.localPosition = positions[4] + (visible ? new Vector3(0, Mathf.Sin(t * Mathf.PI) * .18f, -reveal * .22f - collect * .3f) : Vector3.zero);
                glassOutput.localRotation = rotations[4] * Quaternion.Euler(0, visible ? Mathf.Sin(t * Mathf.PI) * 14 : 0, 0);
            }
            for (int i = 0; i < heatSurfaces.Length; i++)
            {
                var renderer = heatSurfaces[i]; if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                float intensity = celebration > 0 ? 2.4f : .18f + blend * (.4f + heat * 1.8f + breath * .15f);
                block.SetColor("_EmissionColor", new Color(1, .23f + heat * .2f, .025f) * intensity);
                renderer.SetPropertyBlock(block);
            }
            if (motor != null)
            {
                motor.volume = .13f * blend; motor.pitch = .7f + heat * .45f;
                if (!working && blend <= 0) motor.Stop();
            }
        }
        public void ResetImmediately()
        {
            if (!initialized) return;
            working = confirmed = false; progress = elapsed = blend = celebration = angle = 0;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = positions[i]; parts[i].localScale = scales[i]; parts[i].localRotation = rotations[i]; }
            if (sandFeed != null) sandFeed.gameObject.SetActive(false);
            if (glassOutput != null) glassOutput.gameObject.SetActive(false);
            if (moltenPool != null) moltenPool.gameObject.SetActive(false);
            foreach (var ps in new[] { embers, steam, glassSparkles }) if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < heatSurfaces.Length; i++) if (heatSurfaces[i] != null) heatSurfaces[i].SetPropertyBlock(originalBlocks[i]);
            motor?.Stop(); accents?.Stop();
        }
        private void OnDisable() => ResetImmediately();
#if UNITY_EDITOR
        public void SampleEditorPose(float seconds, float normalizedProgress, float completionElapsed = -1)
        {
            Initialize(); elapsed = seconds; blend = 1; progress = normalizedProgress;
            angle = seconds * (100 + Mathf.SmoothStep(0, 1, progress) * 180);
            working = completionElapsed < 0; confirmed = !working;
            celebration = confirmed ? Mathf.Max(0, OutputDuration - completionElapsed) : 0;
            ApplyPose();
        }
#endif
    }
}
