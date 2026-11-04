using System;
using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Work state a station exposes so its ambient life can react to it.</summary>
    public interface IAmbientActivity
    {
        /// <summary>0 at rest, rising to 1 as the station works harder.</summary>
        float Activity { get; }
        /// <summary>A short 0..1 impulse for each stroke or hit, decaying on its own.</summary>
        float Impulse { get; }
        int CompletionCount { get; }
        /// <summary>True while there is nothing to show (an essence on cooldown).</summary>
        bool Dormant { get; }
    }

    /// <summary>
    /// Ambient life for a station, layered on top of its work animation: parts spin, sway, bob and breathe
    /// at rest (with an occasional wind gust), speed up with the station's activity, dip in anticipation when
    /// work starts and spring past rest when a cycle completes. Ambient particle systems emit at an idle
    /// rate and ramp to a work rate. Directions are in this station's space (Y up, -Z toward the front).
    /// Only parts the work animation does not drive should be listed here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientMotion : MonoBehaviour
    {
        public enum Kind : byte { Spin, Sway, Bob, Breathe, Punch }

        [Serializable] public sealed class Channel
        {
            public Transform part;
            public Kind kind;
            public Vector3 axis = Vector3.up;
            [Tooltip("At rest: degrees per second (Spin), degrees (Sway), metres (Bob) or scale fraction (Breathe, Punch).")]
            public float idle = 10;
            [Tooltip("The same at full activity.")] public float work = 60;
            [Tooltip("Oscillation speed in radians per second.")] public float rate = 1.5f;
            public float phase;
            [Tooltip("How strongly wind gusts add to this channel (0..1).")] public float gust = .5f;
        }

        [Serializable] public sealed class Ambient
        {
            public ParticleSystem effect;
            public float idleRate = 2, workRate = 10;
        }

        [SerializeField] private Channel[] channels = Array.Empty<Channel>();
        [SerializeField] private Ambient[] ambients = Array.Empty<Ambient>();
        [Tooltip("Any renderer of the station; updates pause while it is off screen and nothing is happening.")]
        [SerializeField] private Renderer visibility;
        [SerializeField, Range(0, 1)] private float gustStrength = .6f;

        public const float KickDuration = 1.1f;
        private Transform[] parts;
        private Vector3[] restPosition, restScale;
        private Quaternion[] restRotation;
        private Vector3[] axes;
        private int[] partOf;
        private float[] angles, phases;
        private Vector3[] position, scale;
        private Quaternion[] rotation;
        private IAmbientActivity source;
        private bool initialized, wasActive, playing;
        private int completions;
        private float elapsed, activity, softPulse, kickAge = KickDuration, startAge = KickDuration, seed;
        private const float AnticipationDuration = .5f;

        public float Activity => activity;
        public float Gust { get; private set; }
        /// <summary>Spring value after a completion: overshoots past rest and settles (0 when calm).</summary>
        /// <remarks>One soft overshoot and a small undershoot, faded to exactly zero by <see cref="KickDuration"/>.</remarks>
        public float Kick => kickAge < KickDuration
            ? Mathf.Exp(-kickAge * 3.5f) * Mathf.Sin(kickAge * 7.5f) * (1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.7f, KickDuration, kickAge)))
            : 0;
        /// <summary>Burst of extra spin after a completion, fading out (0 when calm).</summary>
        public float KickBurst => kickAge < KickDuration ? (1 - Mathf.Exp(-kickAge * 10)) * Mathf.Exp(-kickAge * 3.5f) * (1 - kickAge / KickDuration) : 0;
        /// <summary>Dip when work starts, the anticipation before the action (0..1).</summary>
        public float Anticipation => startAge < AnticipationDuration ? Mathf.Sin(startAge / AnticipationDuration * Mathf.PI) : 0;
        public int ChannelCount => channels.Length;
        public int AmbientCount => ambients.Length;

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            channels ??= Array.Empty<Channel>(); ambients ??= Array.Empty<Ambient>();
            source = GetComponent<IAmbientActivity>();
            var unique = new System.Collections.Generic.List<Transform>();
            partOf = new int[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                var part = channels[i].part;
                int index = part != null ? unique.IndexOf(part) : -1;
                if (part != null && index < 0) { unique.Add(part); index = unique.Count - 1; }
                partOf[i] = index;
            }
            parts = unique.ToArray();
            restPosition = new Vector3[parts.Length]; restScale = new Vector3[parts.Length]; restRotation = new Quaternion[parts.Length];
            position = new Vector3[parts.Length]; scale = new Vector3[parts.Length]; rotation = new Quaternion[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                restPosition[i] = parts[i].localPosition; restScale[i] = parts[i].localScale; restRotation[i] = parts[i].localRotation;
            }
            axes = new Vector3[channels.Length]; angles = new float[channels.Length]; phases = new float[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                var part = channels[i].part; if (part == null) continue;
                Vector3 world = transform.TransformDirection(channels[i].axis.normalized);
                axes[i] = part.parent != null ? part.parent.InverseTransformDirection(world).normalized : world;
            }
            // Each placed station gets its own rhythm so a row of them never moves in lockstep.
            seed = Mathf.Repeat(transform.position.x * 1.37f + transform.position.z * .71f, 100);
            completions = source?.CompletionCount ?? 0;
            for (int i = 0; i < channels.Length; i++) phases[i] = seed * channels[i].rate;
        }

        private void OnEnable()
        {
            Initialize();
            if (Application.isPlaying) SetAmbientPlaying(source == null || !source.Dormant);
        }

        private void OnDisable()
        {
            if (!initialized) return;
            SetAmbientPlaying(false);
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = restPosition[i]; parts[i].localRotation = restRotation[i]; parts[i].localScale = restScale[i]; }
        }

        private void Update()
        {
            if (!initialized) return;
            float dt = Time.deltaTime;
            Step(dt);
            bool calm = activity < .01f && kickAge >= KickDuration;
            if (calm && visibility != null && !visibility.isVisible) return;
            ApplyPose();
            UpdateAmbientRates();
        }

        private void Step(float dt)
        {
            elapsed += dt;
            float target = source?.Activity ?? 0;
            // Rise quickly toward work, settle slowly afterwards: the follow-through after effort.
            activity = Mathf.MoveTowards(activity, target, dt / (target > activity ? .3f : 1.2f));
            bool active = target > .05f;
            if (active && !wasActive) startAge = 0;
            wasActive = active;
            startAge += dt;
            int done = source?.CompletionCount ?? 0;
            if (done != completions) { completions = done; kickAge = 0; }
            kickAge += dt;
            float t = elapsed + seed;
            // Gusts: slow layered noise that occasionally peaks, never a constant gale.
            Gust = Mathf.Clamp01(Mathf.PerlinNoise(t * .23f, seed) * 1.6f - .55f) * gustStrength;
            // Strokes and completions arrive as sudden impulses; ease them in so nothing jerks.
            softPulse = Mathf.Lerp(softPulse, source?.Impulse ?? 0, 1 - Mathf.Exp(-dt * 6));
            float boost = 1 + KickBurst * 1.2f;
            for (int i = 0; i < channels.Length; i++)
            {
                var c = channels[i];
                if (c.kind == Kind.Spin) angles[i] += Mathf.Lerp(c.idle, c.work, activity) * (1 + Gust * c.gust) * boost * dt;
                // Oscillations accumulate their phase, so changing speed with activity never makes them jump.
                else phases[i] += c.rate * (1 + activity * .5f) * dt;
            }
        }

        private void ApplyPose()
        {
            for (int i = 0; i < parts.Length; i++) { position[i] = restPosition[i]; rotation[i] = restRotation[i]; scale[i] = restScale[i]; }
            float pulse = softPulse;
            for (int i = 0; i < channels.Length; i++)
            {
                var c = channels[i]; int p = partOf[i]; if (p < 0) continue;
                float amount = Mathf.Lerp(c.idle, c.work, activity) * (1 + Gust * c.gust);
                float wave = Mathf.Sin(phases[i] + c.phase);
                switch (c.kind)
                {
                    case Kind.Spin:
                        rotation[p] = Quaternion.AngleAxis(angles[i], axes[i]) * rotation[p]; break;
                    case Kind.Sway:
                        // A gust leans the part downwind; each stroke bows it a little further, then it eases back.
                        float lean = Gust * c.gust * amount * .8f + pulse * amount * .6f;
                        rotation[p] = Quaternion.AngleAxis(wave * amount + lean + Kick * amount * .8f, axes[i]) * rotation[p]; break;
                    case Kind.Bob:
                        position[p] += axes[i] * (wave * amount + Kick * amount * .5f); break;
                    case Kind.Breathe:
                        scale[p] *= 1 + wave * amount; break;
                    case Kind.Punch:
                        // Squash in anticipation as work starts, pop past rest on completion, small throb on strokes.
                        scale[p] *= 1 - Anticipation * c.idle + Kick * c.work + pulse * c.idle * .3f; break;
                }
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                parts[i].localPosition = position[i]; parts[i].localRotation = rotation[i]; parts[i].localScale = scale[i];
            }
        }

        private void UpdateAmbientRates()
        {
            if (!Application.isPlaying) return;
            bool dormant = source != null && source.Dormant;
            if (dormant == playing) SetAmbientPlaying(!dormant);
            foreach (var a in ambients)
            {
                if (a.effect == null) continue;
                var emission = a.effect.emission;
                emission.rateOverTime = Mathf.Lerp(a.idleRate, a.workRate, activity) * (1 + Gust * .5f);
            }
        }

        private void SetAmbientPlaying(bool on)
        {
            playing = on;
            foreach (var a in ambients)
            {
                if (a.effect == null) continue;
                if (on) { var main = a.effect.main; main.loop = true; a.effect.Play(); }
                else a.effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

#if UNITY_EDITOR
        /// <summary>Edit Mode preview: advance the real runtime motion by dt, reading the station's live activity.</summary>
        public void StepEditor(float dt) { if (Application.isPlaying) return; Initialize(); Step(dt); ApplyPose(); }

        /// <summary>Edit Mode sample for previews and tests: time, activity, and seconds since the last completion (negative: none).</summary>
        public void SampleEditorPose(float seconds, float sampleActivity, float sinceCompletion = -1, float sinceStart = -1)
        {
            if (Application.isPlaying) return;
            Initialize();
            elapsed = seconds; activity = Mathf.Clamp01(sampleActivity);
            kickAge = sinceCompletion >= 0 ? sinceCompletion : KickDuration;
            startAge = sinceStart >= 0 ? sinceStart : KickDuration;
            float t = elapsed + seed;
            Gust = Mathf.Clamp01(Mathf.PerlinNoise(t * .23f, seed) * 1.6f - .55f) * gustStrength;
            softPulse = source?.Impulse ?? 0;
            for (int i = 0; i < channels.Length; i++)
            {
                angles[i] = channels[i].kind == Kind.Spin ? seconds * Mathf.Lerp(channels[i].idle, channels[i].work, activity) : 0;
                phases[i] = (seconds + seed) * channels[i].rate * (1 + activity * .5f);
            }
            ApplyPose();
        }

        /// <summary>Edit Mode preview: set the ambient emission rates for an activity and advance the effects by dt.</summary>
        public void SimulateEffectsForPreview(float dt, float sampleActivity, bool dormant = false)
        {
            if (Application.isPlaying) return;
            foreach (var a in ambients)
            {
                if (a.effect == null) continue;
                var main = a.effect.main; main.loop = true;
                var emission = a.effect.emission;
                emission.rateOverTime = dormant ? 0 : Mathf.Lerp(a.idleRate, a.workRate, Mathf.Clamp01(sampleActivity));
                a.effect.Simulate(dt, true, false, false);
            }
        }

        public void RestoreRestPose()
        {
            Initialize();
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = restPosition[i]; parts[i].localRotation = restRotation[i]; parts[i].localScale = restScale[i]; }
        }
#endif
    }
}
