using System;
using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>
    /// Work cycle for every processor. Each machine combines motion channels (spinning parts, strokes,
    /// pulses, fill levels, dials) into its own process; the finished material appears on the tray only
    /// after the server confirms the exchange. Directions are in this station's space (Y up, -Z toward
    /// the tray), so imported models that keep Blender's Z-up local axes still move correctly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ProcessorMachineAnimation : MonoBehaviour
    {
        public enum ProcessKind : byte { Crush, Melt, Mill, Press, Spin }

        [Serializable] public sealed class Spin
        {
            public Transform part;
            public Vector3 axis = Vector3.up;
            [Tooltip("Degrees per second at full work.")] public float speed = 180;
            [Tooltip("Speed builds from 35% to 100% across the cycle.")] public bool rampWithProgress = true;
        }

        [Serializable] public sealed class Travel
        {
            public Transform part;
            public Vector3 direction = Vector3.down;
            public float distance = .1f;
            [Tooltip("Progress range in which the part moves.")] public Vector2 window = new(0, 1);
            [Tooltip("0 eases once across the window; more makes repeated strikes, each one an impact.")] public int strokes;
            [Tooltip("Small vibration while the part is under load.")] public float shake;
        }

        [Serializable] public sealed class Pulse
        {
            public Transform part;
            public Vector3 axis = Vector3.right;
            public float amount = .15f;
            [Tooltip("Breaths per second, in radians.")] public float rate = 8;
        }

        [Serializable] public sealed class Fill
        {
            public Transform part;
            public Vector3 axis = Vector3.up;
            [Tooltip("Progress range in which the level rises from empty to full.")] public Vector2 window = new(0, 1);
            [Tooltip("Optional later range in which the level drops to Drain To. Ignored when y <= x.")] public Vector2 drainWindow;
            [Range(0, 1)] public float drainTo = .3f;
            public bool hideWhenIdle = true;
            [Tooltip("Optional surface that rides on top of the level (pivot at the full level).")] public Transform surface;
            [Tooltip("Height of the full level, used to move the surface.")] public float surfaceHeight;
        }

        [Serializable] public sealed class Dial
        {
            public Transform part;
            public Vector3 axis = Vector3.forward;
            [Tooltip("Degrees turned at full progress.")] public float angle = -220;
        }

        [SerializeField] private ProcessKind process;
        [SerializeField] private Spin[] spins = Array.Empty<Spin>();
        [SerializeField] private Travel[] travels = Array.Empty<Travel>();
        [SerializeField] private Pulse[] pulses = Array.Empty<Pulse>();
        [SerializeField] private Fill[] fills = Array.Empty<Fill>();
        [SerializeField] private Dial[] dials = Array.Empty<Dial>();
        [Header("Material flow")]
        [SerializeField] private Transform feed;
        [Tooltip("Where the input starts, relative to its rest point in this station's space (in front of the machine).")]
        [SerializeField] private Vector3 feedEntry = new(0, -.4f, -1.3f);
        [Tooltip("Optional intake point it passes through on the way in, relative to its rest point. Zero skips it.")]
        [SerializeField] private Vector3 feedVia;
        [SerializeField, Min(0)] private float feedArc = .5f;
        [Tooltip("Entry runs from 0 to x; the input is consumed from x to y.")]
        [SerializeField] private Vector2 consumeWindow = new(.28f, .82f);
        [Tooltip("Where the input travels while it is consumed, relative to its rest point.")]
        [SerializeField] private Vector3 feedConsume;
        [SerializeField] private float feedSwirl;
        [SerializeField] private Transform output;
        [Header("Lights and effects")]
        [SerializeField] private Renderer[] statusLights = Array.Empty<Renderer>();
        [SerializeField, ColorUsage(false, true)] private Color glowColor = new(.3f, .9f, 1);
        [SerializeField] private Renderer[] heatSurfaces = Array.Empty<Renderer>();
        [SerializeField, ColorUsage(false, true)] private Color heatColor = new(1, .3f, .04f);
        [SerializeField] private ParticleSystem[] workEffects = Array.Empty<ParticleSystem>();
        [SerializeField] private ParticleSystem strikeEffect;
        [SerializeField] private ParticleSystem[] completionEffects = Array.Empty<ParticleSystem>();
        [SerializeField] private AudioSource loop, accents;
        [SerializeField] private AudioClip strikeClip, completionClip;

        public const float OutputDuration = 1.3f;

        private struct Rest
        {
            public Vector3 Position, Scale, Up, Out, SideA, SideB;
            public Quaternion Rotation;
        }

        private Transform[] parts;
        private Rest[] rest;
        private float[] spinAngles;
        private int[] strikes;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock[] lightBlocks, heatBlocks;
        private bool initialized, working, confirmed;
        private float progress, elapsed, blend, celebration;

        public ProcessKind Process => process;
        public bool IsWorking => working;
        public bool IsFeedVisible => feed != null && feed.gameObject.activeSelf;
        public bool IsOutputVisible => output != null && output.gameObject.activeSelf;
        public int CompletionCount { get; private set; }
        /// <summary>Impacts made by striking parts during the current cycle.</summary>
        public int StrikeCount { get; private set; }
        public Transform Feed => feed;
        public Vector2 ConsumeWindow => consumeWindow;
        public Transform Output => output;
        public Vector3 FeedbackPosition => output != null ? output.position + Vector3.up * .15f : transform.position;
        public int SpinCount => spins.Length;
        public int TravelCount => travels.Length;
        public int PulseCount => pulses.Length;
        public int FillCount => fills.Length;
        public int DialCount => dials.Length;

        private void Awake() { Initialize(); ResetImmediately(); }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            spins ??= Array.Empty<Spin>(); travels ??= Array.Empty<Travel>(); pulses ??= Array.Empty<Pulse>();
            fills ??= Array.Empty<Fill>(); dials ??= Array.Empty<Dial>();
            int count = 2 + spins.Length + travels.Length + pulses.Length + fills.Length + dials.Length + fills.Length;
            parts = new Transform[count]; rest = new Rest[count];
            int index = 0;
            parts[index++] = feed; parts[index++] = output;
            foreach (var c in spins) parts[index++] = c.part;
            foreach (var c in travels) parts[index++] = c.part;
            foreach (var c in pulses) parts[index++] = c.part;
            foreach (var c in fills) parts[index++] = c.part;
            foreach (var c in dials) parts[index++] = c.part;
            foreach (var c in fills) parts[index++] = c.surface;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i]; if (part == null) continue;
                rest[i] = new Rest
                {
                    Position = part.localPosition, Scale = part.localScale, Rotation = part.localRotation,
                    Up = ToParent(part, Vector3.up), Out = ToParent(part, Vector3.back),
                    SideA = ToParent(part, Vector3.right), SideB = ToParent(part, Vector3.forward)
                };
            }
            spinAngles = new float[spins.Length]; strikes = new int[travels.Length];
            statusLights ??= Array.Empty<Renderer>(); heatSurfaces ??= Array.Empty<Renderer>();
            block = new MaterialPropertyBlock();
            lightBlocks = Capture(statusLights); heatBlocks = Capture(heatSurfaces);
        }

        private static MaterialPropertyBlock[] Capture(Renderer[] renderers)
        {
            var blocks = new MaterialPropertyBlock[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                blocks[i] = new MaterialPropertyBlock();
                if (renderers[i] != null) renderers[i].GetPropertyBlock(blocks[i]);
            }
            return blocks;
        }

        /// <summary>A station-space direction expressed in the part's parent space.</summary>
        private Vector3 ToParent(Transform part, Vector3 stationDirection)
        {
            Vector3 world = transform.TransformDirection(stationDirection);
            return (part.parent != null ? part.parent.InverseTransformDirection(world) : world).normalized;
        }

        /// <summary>A station-space offset (with length) expressed in the part's parent space.</summary>
        private Vector3 ToParentOffset(Transform part, Vector3 stationOffset)
        {
            Vector3 world = transform.TransformVector(stationOffset);
            return part.parent != null ? part.parent.InverseTransformVector(world) : world;
        }

        /// <summary>Which of the part's own axes lines up with a station-space direction (absolute, 0..1).</summary>
        private Vector3 LocalMask(Transform part, Vector3 stationDirection)
        {
            Vector3 local = part.InverseTransformDirection(transform.TransformDirection(stationDirection)).normalized;
            return new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
        }

        public void BeginWork()
        {
            Initialize();
            working = true; confirmed = false; progress = elapsed = celebration = 0; StrikeCount = 0;
            for (int i = 0; i < strikes.Length; i++) strikes[i] = 0;
            foreach (var effect in workEffects) if (effect != null) effect.Play();
            if (loop != null && !loop.isPlaying && Application.isPlaying) loop.Play();
            ApplyPose();
        }

        public void SetProgress(float value) { progress = Mathf.Clamp01(value); ApplyPose(); }

        public void StopWork()
        {
            Initialize();
            working = false;
            foreach (var effect in workEffects) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (!confirmed) progress = 0;
            ApplyPose();
        }

        public void ConfirmCompletion()
        {
            Initialize();
            if (confirmed) return;
            StopWork();
            confirmed = true; progress = 1; celebration = OutputDuration; CompletionCount++;
            foreach (var effect in completionEffects) if (effect != null) { effect.Play(); effect.Emit(18); }
            if (accents != null && completionClip != null && Application.isPlaying) accents.PlayOneShot(completionClip, .45f);
            ApplyPose();
        }

        private void Update()
        {
            if (!initialized || !working && blend <= 0 && celebration <= 0) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            blend = Mathf.MoveTowards(blend, working ? 1 : 0, dt / .25f);
            AdvanceSpins(dt);
            celebration = Mathf.Max(0, celebration - dt);
            ApplyPose();
        }

        private void AdvanceSpins(float dt)
        {
            float charge = Mathf.SmoothStep(0, 1, progress);
            for (int i = 0; i < spins.Length; i++)
            {
                float speed = spins[i].speed * blend * (spins[i].rampWithProgress ? Mathf.Lerp(.35f, 1, charge) : 1);
                spinAngles[i] = Mathf.Repeat(spinAngles[i] + speed * dt, 360);
            }
        }

        private void ApplyPose()
        {
            if (!initialized) return;
            float charge = Mathf.SmoothStep(0, 1, progress);
            int p = 2;
            for (int i = 0; i < spins.Length; i++, p++)
            {
                var part = spins[i].part; if (part == null) continue;
                part.localRotation = Quaternion.AngleAxis(spinAngles[i], ToParent(part, spins[i].axis)) * rest[p].Rotation;
            }
            for (int i = 0; i < travels.Length; i++, p++)
            {
                var travel = travels[i]; var part = travel.part; if (part == null) continue;
                float local = working ? Mathf.InverseLerp(travel.window.x, travel.window.y, progress) : 0;
                bool active = working && progress >= travel.window.x && progress <= travel.window.y;
                float amount;
                if (travel.strokes > 0)
                {
                    float phase = local * travel.strokes;
                    // Each stroke lands at 18% of its own cycle (see StrikeCurve).
                    int stroke = phase < .18f ? 0 : Mathf.Min(travel.strokes, Mathf.FloorToInt(phase - .18f) + 1);
                    if (working && stroke > strikes[i]) { strikes[i] = stroke; Strike(); }
                    amount = active ? StrikeCurve(Mathf.Repeat(phase, 1)) : 0;
                }
                else amount = working ? Mathf.SmoothStep(0, 1, local) : 0;
                Vector3 shake = active ? rest[p].SideA * (Mathf.Sin(elapsed * 47 + i) * travel.shake) : Vector3.zero;
                part.localPosition = rest[p].Position + ToParent(part, travel.direction) * (travel.distance * amount) + shake;
            }
            for (int i = 0; i < pulses.Length; i++, p++)
            {
                var pulse = pulses[i]; var part = pulse.part; if (part == null) continue;
                float wave = Mathf.Sin(elapsed * pulse.rate) * pulse.amount * blend;
                part.localScale = Vector3.Scale(rest[p].Scale, Vector3.one + LocalMask(part, pulse.axis) * wave);
            }
            for (int i = 0; i < fills.Length; i++, p++)
            {
                var fill = fills[i]; var part = fill.part; if (part == null) continue;
                float level = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fill.window.x, fill.window.y, progress));
                if (fill.drainWindow.y > fill.drainWindow.x)
                    level *= Mathf.Lerp(1, fill.drainTo, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(fill.drainWindow.x, fill.drainWindow.y, progress)));
                bool show = working && level > .005f || !fill.hideWhenIdle;
                part.gameObject.SetActive(show);
                if (!working) level = fill.hideWhenIdle ? 0 : 1;
                Vector3 mask = LocalMask(part, fill.axis);
                part.localScale = Vector3.Scale(rest[p].Scale, Vector3.one + mask * (Mathf.Max(.02f, level) - 1));
                var surface = fill.surface;
                if (surface != null)
                {
                    int s = parts.Length - fills.Length + i;
                    surface.gameObject.SetActive(show);
                    surface.localPosition = rest[s].Position + ToParent(surface, fill.axis) * (fill.surfaceHeight * (Mathf.Max(.02f, level) - 1));
                }
            }
            for (int i = 0; i < dials.Length; i++, p++)
            {
                var dial = dials[i]; var part = dial.part; if (part == null) continue;
                float turn = working ? charge * dial.angle + Mathf.Sin(elapsed * 23) * 2 * blend : 0;
                part.localRotation = Quaternion.AngleAxis(turn, ToParent(part, dial.axis)) * rest[p].Rotation;
            }
            if (feed != null)
            {
                bool visible = working && progress < consumeWindow.y;
                feed.gameObject.SetActive(visible);
                // Entry: from in front of the machine, arcing (through the intake when set) into its rest point.
                float enter = Mathf.SmoothStep(0, 1, consumeWindow.x > 0 ? Mathf.Clamp01(progress / consumeWindow.x) : 1);
                float consume = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(consumeWindow.x, consumeWindow.y, progress));
                Vector3 path = FeedPath(enter) + ToParentOffset(feed, feedConsume) * consume;
                float orbit = elapsed * 6;
                Vector3 swirl = (rest[0].SideA * Mathf.Cos(orbit) + rest[0].SideB * Mathf.Sin(orbit)) * (feedSwirl * consume * (1 - consume) * 4);
                feed.localPosition = rest[0].Position + path + swirl;
                feed.localRotation = Quaternion.AngleAxis((1 - enter) * 540, rest[0].SideA) * rest[0].Rotation;
                feed.localScale = rest[0].Scale * (1 - consume * .94f);
            }
            if (output != null)
            {
                bool visible = confirmed && celebration > 0;
                output.gameObject.SetActive(visible);
                float t = 1 - celebration / OutputDuration;
                float reveal = 1 - Mathf.Pow(1 - Mathf.Clamp01(t / .2f), 3);
                float collect = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.72f, 1, t));
                output.localScale = rest[1].Scale * (visible ? reveal * (1 - collect) : 1);
                output.localPosition = rest[1].Position + (visible ? rest[1].Up * (Mathf.Sin(t * Mathf.PI) * .2f) + rest[1].Out * (reveal * .18f + collect * .3f) : Vector3.zero);
                output.localRotation = Quaternion.AngleAxis(visible ? Mathf.Sin(t * Mathf.PI) * 24 : 0, rest[1].Up) * rest[1].Rotation;
            }
            for (int i = 0; i < statusLights.Length; i++)
            {
                var renderer = statusLights[i]; if (renderer == null) continue;
                float lit = Mathf.Clamp01((progress - i / (float)statusLights.Length) * statusLights.Length);
                float intensity = celebration > 0 ? 2.6f : .15f + blend * (.25f + lit * 1.6f + Mathf.Sin(elapsed * 9 + i) * .1f);
                SetEmission(renderer, glowColor * intensity);
            }
            for (int i = 0; i < heatSurfaces.Length; i++)
            {
                var renderer = heatSurfaces[i]; if (renderer == null) continue;
                float intensity = celebration > 0 ? 2.4f : .18f + blend * (.4f + charge * 1.8f + Mathf.Sin(elapsed * 8) * .15f);
                SetEmission(renderer, heatColor * intensity);
            }
            if (loop != null)
            {
                loop.volume = .13f * blend; loop.pitch = .72f + charge * .45f;
                if (!working && blend <= 0) loop.Stop();
            }
        }

        /// <summary>Offset from the feed's rest point along its entry path; t runs 0 (outside) to 1 (inside).</summary>
        private Vector3 FeedPath(float t)
        {
            Vector3 start = ToParentOffset(feed, feedEntry), lift = rest[0].Up * feedArc;
            if (feedVia == Vector3.zero) return Arc(start, Vector3.zero, lift, t);
            Vector3 via = ToParentOffset(feed, feedVia);
            // Most of the time is spent on the arc to the intake; the last part drops straight in.
            return t < .7f ? Arc(start, via, lift, t / .7f) : Vector3.Lerp(via, Vector3.zero, (t - .7f) / .3f);
        }

        private static Vector3 Arc(Vector3 from, Vector3 to, Vector3 lift, float t)
        {
            Vector3 control = (from + to) * .5f + lift;
            return Vector3.Lerp(Vector3.Lerp(from, control, t), Vector3.Lerp(control, to, t), t);
        }

        /// <summary>Fast drive into the impact, then a slower lift back.</summary>
        private static float StrikeCurve(float t) => t < .18f ? Mathf.Pow(t / .18f, 2) : 1 - Mathf.SmoothStep(0, 1, (t - .18f) / .82f);

        private void Strike()
        {
            StrikeCount++;
            if (strikeEffect != null) { if (!strikeEffect.isPlaying) strikeEffect.Play(); strikeEffect.Emit(6); }
            if (accents != null && strikeClip != null && Application.isPlaying)
            {
                accents.pitch = UnityEngine.Random.Range(.92f, 1.08f);
                accents.PlayOneShot(strikeClip, .3f);
            }
        }

        private void SetEmission(Renderer renderer, Color color)
        {
            renderer.GetPropertyBlock(block);
            block.SetColor("_EmissionColor", color);
            renderer.SetPropertyBlock(block);
        }

        public void ResetImmediately()
        {
            if (!initialized) return;
            working = confirmed = false; progress = elapsed = blend = celebration = 0; StrikeCount = 0;
            for (int i = 0; i < spinAngles.Length; i++) spinAngles[i] = 0;
            for (int i = 0; i < strikes.Length; i++) strikes[i] = 0;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = rest[i].Position; parts[i].localScale = rest[i].Scale; parts[i].localRotation = rest[i].Rotation; }
            if (feed != null) feed.gameObject.SetActive(false);
            if (output != null) output.gameObject.SetActive(false);
            foreach (var fill in fills)
                if (fill.hideWhenIdle)
                {
                    if (fill.part != null) fill.part.gameObject.SetActive(false);
                    if (fill.surface != null) fill.surface.gameObject.SetActive(false);
                }
            foreach (var effect in workEffects) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            foreach (var effect in completionEffects) if (effect != null) effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (strikeEffect != null) strikeEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            for (int i = 0; i < statusLights.Length; i++) if (statusLights[i] != null) statusLights[i].SetPropertyBlock(lightBlocks[i]);
            for (int i = 0; i < heatSurfaces.Length; i++) if (heatSurfaces[i] != null) heatSurfaces[i].SetPropertyBlock(heatBlocks[i]);
            loop?.Stop(); accents?.Stop();
        }

        private void OnDisable() => ResetImmediately();

#if UNITY_EDITOR
        /// <summary>Edit Mode preview sample. A non-negative completionElapsed samples the confirmed output.</summary>
        public void SampleEditorPose(float seconds, float normalizedProgress, float completionElapsed = -1)
        {
            if (Application.isPlaying) return;
            Initialize();
            working = completionElapsed < 0; confirmed = !working;
            elapsed = seconds; blend = 1; progress = Mathf.Clamp01(normalizedProgress);
            celebration = confirmed ? Mathf.Max(0, OutputDuration - completionElapsed) : 0;
            float charge = Mathf.SmoothStep(0, 1, progress);
            for (int i = 0; i < spins.Length; i++)
                spinAngles[i] = Mathf.Repeat(seconds * spins[i].speed * (spins[i].rampWithProgress ? Mathf.Lerp(.35f, 1, charge) : 1), 360);
            ApplyPose();
        }
#endif
    }
}
