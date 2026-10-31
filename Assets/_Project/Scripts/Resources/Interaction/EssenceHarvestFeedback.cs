using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>
    /// Natural essence node presentation, in the spirit of the sand mound: the essence body stays anchored
    /// to the ground and shrinks while it is gathered, loose pieces (wisps, droplets, cloud tufts) lift and
    /// fade away, and the base (pebbles, moss, crystals) stays through cooldown. Only render transforms
    /// move; the collider stays fixed. Directions come from this node's transform, not the model's axes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EssenceHarvestFeedback : MonoBehaviour, IHarvestFeedback
    {
        public enum EssenceStyle : byte { RiverSwirl, WaterWobble, SkyDrift }

        [SerializeField] private EssenceStyle style;
        [Tooltip("Ground-anchored essence mass (pool water, bubble clump, cloud). Pivot on the ground.")]
        [SerializeField] private Transform body;
        [Tooltip("Optional part that turns around the vertical axis, such as the river current.")]
        [SerializeField] private Transform spinner;
        [Tooltip("Loose pieces that bob at rest and lift away while gathered.")]
        [SerializeField] private Transform[] floaters;
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField, ColorUsage(false, true)] private Color glowColor = new(.3f, 1f, .8f);
        [SerializeField] private ParticleSystem motes, burst;
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip strokeClip, breakClip, respawnClip;

        private const int Strokes = 6;
        private const float RespawnDuration = .5f;
        private Transform[] parts;
        private Vector3[] positions, scales, ups;
        private Quaternion[] rotations;
        private Vector3 bodyUpMask = Vector3.up;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock[] originalBlocks;
        private bool initialized, working, depleted;
        private float progress, blend, impact, elapsed, spin, respawnTime;
        private int stroke;

        public EssenceStyle Style => style;
        public bool IsWorking => working;
        public bool IsDepleted => depleted;
        public int CompletionCount { get; private set; }
        /// <summary>Height multiplier on the body: 1 at rest, about 0.18 at the end of a gather.</summary>
        public float PresentationScale { get; private set; } = 1;
        /// <summary>Width multiplier on the body: 1 at rest, about 0.6 at the end of a gather.</summary>
        public float WidthScale { get; private set; } = 1;
        /// <summary>How far the loose pieces have lifted, 0 at rest.</summary>
        public float FloaterLift { get; private set; }
        public Transform Body => body;
        public int FloaterCount => floaters != null ? floaters.Length : 0;

        private void Awake() { Initialize(); ResetFeedback(); }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            floaters ??= System.Array.Empty<Transform>();
            parts = new Transform[2 + floaters.Length];
            parts[0] = body; parts[1] = spinner;
            for (int i = 0; i < floaters.Length; i++) parts[2 + i] = floaters[i];
            positions = new Vector3[parts.Length]; scales = new Vector3[parts.Length]; rotations = new Quaternion[parts.Length]; ups = new Vector3[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i]; if (part == null) continue;
                positions[i] = part.localPosition; scales[i] = part.localScale; rotations[i] = part.localRotation;
                ups[i] = (part.parent != null ? part.parent.InverseTransformDirection(transform.up) : transform.up).normalized;
            }
            if (body != null)
            {
                Vector3 local = body.InverseTransformDirection(transform.up).normalized;
                bodyUpMask = new Vector3(Mathf.Abs(local.x), Mathf.Abs(local.y), Mathf.Abs(local.z));
            }
            glowRenderers ??= System.Array.Empty<Renderer>();
            block = new MaterialPropertyBlock(); originalBlocks = new MaterialPropertyBlock[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (glowRenderers[i] != null) glowRenderers[i].GetPropertyBlock(originalBlocks[i]);
            }
        }

        public void BeginWork()
        {
            Initialize();
            working = true; depleted = false; progress = 0; stroke = 0; respawnTime = 0;
            Pulse();
            ApplyPose();
        }

        public void SetProgress(float value)
        {
            if (!working) return;
            progress = Mathf.Clamp01(value);
            int next = Mathf.FloorToInt(progress * Strokes);
            if (next > stroke) { stroke = next; Pulse(); }
            ApplyPose();
        }

        public void StopWork()
        {
            Initialize();
            working = false; progress = 0; impact = 0;
            ApplyPose();
        }

        public void Deplete()
        {
            Initialize();
            if (depleted) return;
            depleted = true; working = false; CompletionCount++; respawnTime = 0;
            Emit(burst, 24);
            Emit(motes, 10);
            Sound(breakClip, .45f, 1);
        }

        public void Respawn()
        {
            Initialize();
            depleted = false; progress = 0; respawnTime = RespawnDuration;
            Emit(motes, 8);
            Sound(respawnClip, .2f, 1.1f);
            ApplyPose();
        }

        private void Pulse()
        {
            impact = 1;
            Emit(motes, 5);
            Emit(burst, 3);
            float pitch = style switch { EssenceStyle.WaterWobble => 1.05f, EssenceStyle.SkyDrift => 1.2f, _ => .95f };
            Sound(strokeClip, .24f, pitch * Mathf.Lerp(.92f, 1.14f, progress));
        }

        private void Update()
        {
            if (!initialized) return;
            float dt = Time.deltaTime;
            elapsed += dt;
            blend = Mathf.MoveTowards(blend, working ? 1 : 0, dt / .3f);
            impact = Mathf.MoveTowards(impact, 0, dt * 5);
            spin = Mathf.Repeat(spin + dt * Mathf.Lerp(18, 200, blend), 360);
            if (respawnTime > 0) respawnTime = Mathf.Max(0, respawnTime - dt);
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (!initialized) return;
            float gather = working ? Mathf.SmoothStep(0, 1, progress) : 0;
            float regrow = respawnTime > 0 ? 1 - Mathf.Pow(respawnTime / RespawnDuration, 3) : 1;
            float grow = Mathf.Lerp(.08f, 1, regrow);
            // Same proportions as the sand pile: narrower and much lower, anchored on the ground.
            WidthScale = Mathf.Lerp(1, .6f, gather) * grow;
            PresentationScale = Mathf.Lerp(1, .18f, gather) * grow;
            FloaterLift = gather * .55f;
            if (body != null)
            {
                float width = WidthScale * (1 + impact * .03f), height = PresentationScale * (1 - impact * .05f);
                switch (style)
                {
                    case EssenceStyle.WaterWobble:
                        float wobble = Mathf.Sin(elapsed * (4.5f + blend * 6)) * (.035f + blend * .05f + impact * .07f);
                        width *= 1 + wobble; height *= 1 - wobble * 1.6f; break;
                    case EssenceStyle.SkyDrift:
                        float breathe = Mathf.Sin(elapsed * 1.2f) * .03f + blend * Mathf.Sin(elapsed * 5) * .03f;
                        width *= 1 + breathe; height *= 1 + breathe * .6f; break;
                    default:
                        height *= 1 + Mathf.Sin(elapsed * 3.1f) * .04f * (1 + blend * 2); break;
                }
                body.localScale = Vector3.Scale(scales[0], Vector3.one * width + bodyUpMask * (height - width));
                float turn = style == EssenceStyle.SkyDrift ? Mathf.Sin(elapsed * .4f) * 6 : 0;
                body.localRotation = Quaternion.AngleAxis(turn, ups[0]) * rotations[0];
            }
            if (spinner != null)
            {
                spinner.localRotation = Quaternion.AngleAxis(spin, ups[1]) * rotations[1];
                spinner.localScale = scales[1] * WidthScale;
                // Rides on the sinking surface.
                spinner.localPosition = positions[1] + ups[1] * (-gather * .12f);
            }
            for (int i = 0; i < floaters.Length; i++)
            {
                var floater = floaters[i]; if (floater == null) continue;
                int p = 2 + i;
                float bob = Mathf.Sin(elapsed * (1.4f + i * .31f) + i * 1.9f) * .04f;
                float sway = Mathf.Sin(elapsed * (.9f + i * .23f) + i) * .05f * (1 + blend);
                Vector3 side = Vector3.Cross(ups[p], positions[p]).normalized;
                float lift = FloaterLift * (1 + i * .15f);
                floater.localPosition = positions[p] + ups[p] * (bob + lift) + side * sway - positions[p] * (gather * .35f);
                floater.localScale = scales[p] * grow * Mathf.Lerp(1, .25f, gather);
            }
            float glow = .5f + .2f * Mathf.Sin(elapsed * 2.1f) + blend * (.5f + gather) + impact * .8f;
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                var renderer = glowRenderers[i]; if (renderer == null) continue;
                renderer.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", glowColor * glow);
                renderer.SetPropertyBlock(block);
            }
        }

        private void Sound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || audioSource == null || !Application.isPlaying) return;
            audioSource.pitch = pitch + Random.Range(-.04f, .04f);
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
            Initialize();
            working = depleted = false; progress = blend = impact = respawnTime = spin = 0;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = positions[i]; parts[i].localScale = scales[i]; parts[i].localRotation = rotations[i]; }
            for (int i = 0; i < glowRenderers.Length; i++) if (glowRenderers[i] != null) glowRenderers[i].SetPropertyBlock(originalBlocks[i]);
            PresentationScale = WidthScale = 1; FloaterLift = 0;
            if (motes != null) motes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (burst != null) burst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (audioSource != null) audioSource.Stop();
        }

        private void OnDisable()
        {
            // Resource hides only the essence container on cooldown, so this runs when the whole node is disabled.
            if (initialized) ResetFeedback();
        }

#if UNITY_EDITOR
        /// <summary>Edit Mode preview sample on a disposable instance, without Play Mode.</summary>
        public void SampleEditorPose(float seconds, float normalizedProgress, bool gathering = true)
        {
            if (Application.isPlaying) return;
            Initialize();
            elapsed = seconds; working = gathering; blend = gathering ? 1 : 0;
            progress = gathering ? Mathf.Clamp01(normalizedProgress) : 0;
            stroke = Mathf.FloorToInt(progress * Strokes);
            impact = gathering ? Mathf.Exp(-Mathf.Repeat(progress * Strokes, 1) * 4) : 0;
            spin = Mathf.Repeat(seconds * Mathf.Lerp(18, 200, blend), 360);
            ApplyPose();
        }
#endif
    }
}
