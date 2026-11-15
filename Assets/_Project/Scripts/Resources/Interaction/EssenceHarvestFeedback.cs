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
    public sealed class EssenceHarvestFeedback : MonoBehaviour, IHarvestFeedback, IAmbientActivity
    {
        /// <summary>Appended only: the value is serialized in the node prefabs.</summary>
        public enum EssenceStyle : byte { RiverSwirl, WaterWobble, SkyDrift, SkyBloom }

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
        [Header("Dandelions (SkyBloom)")]
        [Tooltip("Flowers in picking order. Each is its own stalk with the pivot at its base.")]
        [SerializeField] private Transform[] flowers;
        [Tooltip("Each flower's seed clock (Dandelion Seeds shader); the seeds fly as its _Release rises.")]
        [SerializeField] private Renderer[] seedHeads;
        [Tooltip("Where the breeze carries the seeds, in this node's space.")]
        [SerializeField] private Vector3 windDirection = new(1, 0, -.25f);

        private const int Strokes = 6;
        private const float RespawnDuration = .5f;
        // SkyBloom, in gather progress: each flower's seeds leave in a wave, fly, then the bare stalk sinks.
        // Flowers overlap so the gather reads as one continuous movement; all three are gone by progress 1.
        public const float FlowerWave = .22f, FlowerFlight = .2f, FlowerSpacing = .23f, SinkDelay = .36f, SinkLength = .2f;
        // After a cancel the picture eases back no faster than a 2.8 s gather moves forward (CancelRewind, progress per
        // second), landing softly (CancelEase): seeds drift back and stalks rise over one to three seconds.
        private const float BloomRegrow = .9f, FlowerBow = 9, SinkBow = 10, CompleteHold = 1.5f, CancelEase = 1.6f, CancelRewind = .35f;
        private static readonly int ReleaseId = Shader.PropertyToID("_Release"), FlightId = Shader.PropertyToID("_Flight"),
            WindId = Shader.PropertyToID("_SeedWind");
        private Vector3[] flowerScales, flowerAxes, nodAxes, floaterWind;
        private Quaternion[] flowerRotations;
        private float easedProgress, holdTime, seedDebt, settle;     // settle: the idle nod fades in after a reset
        private int flowersStarted;
        private Transform[] parts;
        private Vector3[] positions, scales, ups;
        private Quaternion[] rotations;
        private Vector3 bodyUpMask = Vector3.up, bodySide = Vector3.right;
        private MaterialPropertyBlock block;
        private MaterialPropertyBlock[] originalBlocks;
        private bool initialized, working, depleted;
        private float progress, blend, impact, elapsed, spin, respawnTime;
        // SkyBloom eases its release and seed puff so the stalks never jerk: progress already rises smoothly,
        // but a cancelled gather would otherwise snap the stalks upright in one frame.
        private float easedGather, easedImpact;
        private int stroke;

        public EssenceStyle Style => style;
        public bool IsWorking => working;
        public bool IsDepleted => depleted;
        public int CompletionCount { get; private set; }
        /// <summary>Height multiplier on the body: 1 at rest, about 0.18 at the end of a gather.
        /// SkyBloom: the share of the dandelions still standing, from 1 down to 0 as the flowers are picked.</summary>
        public float PresentationScale { get; private set; } = 1;
        /// <summary>Width multiplier on the body: 1 at rest, about 0.6 at the end of a gather.</summary>
        public float WidthScale { get; private set; } = 1;
        /// <summary>How far the loose pieces have lifted, 0 at rest.</summary>
        public float FloaterLift { get; private set; }
        public Transform Body => body;
        public int FloaterCount => floaters != null ? floaters.Length : 0;
        public float Activity => blend * Mathf.Lerp(.6f, 1, progress);
        public float Impulse => impact;
        public bool Dormant => depleted;

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
                bodySide = (body.parent != null ? body.parent.InverseTransformDirection(transform.right) : transform.right).normalized;
            }
            flowers ??= System.Array.Empty<Transform>(); seedHeads ??= System.Array.Empty<Renderer>();
            flowerScales = new Vector3[flowers.Length]; flowerRotations = new Quaternion[flowers.Length];
            flowerAxes = new Vector3[flowers.Length]; nodAxes = new Vector3[flowers.Length];
            Vector3 wind = transform.TransformDirection(windDirection).normalized;
            for (int i = 0; i < flowers.Length; i++)
            {
                var flower = flowers[i]; if (flower == null) continue;
                flowerScales[i] = flower.localScale; flowerRotations[i] = flower.localRotation;
                Vector3 up = Local(flower, transform.up), downwind = Local(flower, wind);
                // Turning about up x wind tips the flower's top toward the wind; the nod axis differs per flower.
                flowerAxes[i] = Vector3.Cross(up, downwind).normalized;
                nodAxes[i] = Quaternion.AngleAxis(55 * i + 20, up) * flowerAxes[i];
            }
            floaterWind = new Vector3[floaters.Length];
            for (int i = 0; i < floaters.Length; i++) if (floaters[i] != null) floaterWind[i] = Local(floaters[i], wind);
            glowRenderers ??= System.Array.Empty<Renderer>();
            block = new MaterialPropertyBlock(); originalBlocks = new MaterialPropertyBlock[glowRenderers.Length];
            for (int i = 0; i < glowRenderers.Length; i++)
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                if (glowRenderers[i] != null) glowRenderers[i].GetPropertyBlock(originalBlocks[i]);
            }
        }

        private bool Bloom => style == EssenceStyle.SkyBloom && flowers != null && flowers.Length > 0;
        public int FlowerCount => flowers != null ? flowers.Length : 0;
        /// <summary>SkyBloom: the gather progress the flowers show (eases back after a cancel, holds after completion).</summary>
        public float ShownProgress { get; private set; }

        /// <summary>SkyBloom: how far flower i's seed wave has gone, 0 before it starts, 1 when its last seed leaves.</summary>
        public static float FlowerRelease(int flower, float shownProgress) => (shownProgress - flower * FlowerSpacing) / FlowerWave;
        /// <summary>SkyBloom: 0 while flower i stands, 1 once its bare stalk has sunk into the rosette.</summary>
        public static float FlowerSink(int flower, float shownProgress) => Smooth01((shownProgress - flower * FlowerSpacing - SinkDelay) / SinkLength);

        public void BeginWork()
        {
            Initialize();
            working = true; depleted = false; progress = 0; stroke = 0; respawnTime = 0; holdTime = 0; flowersStarted = 0;
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
            // A finished gather keeps its picked pose until Deplete hides the essence (a network round trip later);
            // easing back first would flash the flowers regrowing. A cancelled gather eases back.
            if (Bloom && working && progress >= .97f) holdTime = CompleteHold;
            working = false; progress = 0; impact = 0;
            ApplyPose();
        }

        public void Deplete()
        {
            Initialize();
            if (depleted) return;
            depleted = true; working = false; CompletionCount++; respawnTime = 0;
            if (Bloom)
            {
                // The last seeds lift off the final head as particles, so the essence can hide without a pop.
                holdTime = 0; easedProgress = 1;
                var head = seedHeads.Length > 0 ? seedHeads[seedHeads.Length - 1] : null;
                for (int i = 0; i < 10; i++) EmitSeed(head != null ? head.transform.position : transform.position + transform.up);
            }
            else Emit(burst, 24);
            Emit(motes, 10);
            Sound(breakClip, .45f, 1);
        }

        public void Respawn()
        {
            Initialize();
            depleted = false; progress = 0; respawnTime = Bloom ? BloomRegrow : RespawnDuration;
            easedGather = easedImpact = easedProgress = holdTime = 0;
            Emit(motes, 8);
            Sound(respawnClip, .2f, 1.1f);
            ApplyPose();
        }

        private void Pulse()
        {
            // Dandelions have no per-pull kick: each flower's seeds blow away in one smooth wave instead.
            if (Bloom) return;
            impact = 1;
            Emit(motes, 5);
            Emit(burst, 3);
            float pitch = style switch { EssenceStyle.WaterWobble => 1.05f, EssenceStyle.SkyDrift => 1.2f, EssenceStyle.SkyBloom => 1.28f, _ => .95f };
            Sound(strokeClip, .24f, pitch * Mathf.Lerp(.92f, 1.14f, progress));
        }

        private void Update() => Tick(Time.deltaTime);

        private void Tick(float dt)
        {
            if (!initialized) return;
            elapsed += dt;
            blend = Mathf.MoveTowards(blend, working ? 1 : 0, dt / .3f);
            impact = Mathf.MoveTowards(impact, 0, dt * 5);
            spin = Mathf.Repeat(spin + dt * Mathf.Lerp(18, 200, blend), 360);
            float rawGather = working ? Mathf.SmoothStep(0, 1, progress) : 0;
            easedGather = Mathf.Max(rawGather, Mathf.Lerp(easedGather, rawGather, 1 - Mathf.Exp(-dt * 4)));
            easedImpact = Mathf.Lerp(easedImpact, impact, 1 - Mathf.Exp(-dt * 9));
            if (respawnTime > 0) respawnTime = Mathf.Max(0, respawnTime - dt);
            if (Bloom) TickFlowers(dt);
            ApplyPose();
        }

        private float RawShown => working ? progress : holdTime > 0 ? 1 : 0;

        private void TickFlowers(float dt)
        {
            if (holdTime > 0) holdTime = Mathf.Max(0, holdTime - dt);
            settle = Mathf.Min(1, settle + dt / 1.2f);
            float before = easedProgress, raw = RawShown;
            // Rises with the gather; after a cancel it eases back (seeds return, stalks rise) instead of snapping.
            float eased = Mathf.Lerp(easedProgress, raw, 1 - Mathf.Exp(-dt * CancelEase));
            easedProgress = Mathf.Max(raw, Mathf.Max(eased, easedProgress - CancelRewind * dt));
            if (!working || depleted) return;
            for (int i = 0; i < flowers.Length; i++)
            {
                float now = Mathf.Clamp01(FlowerRelease(i, easedProgress)), was = Mathf.Clamp01(FlowerRelease(i, before));
                if (now > .02f && flowersStarted <= i)
                {
                    // Each flower's seeds go with an airy whoosh that rises flower by flower.
                    flowersStarted = i + 1;
                    Sound(strokeClip, .24f, 1.18f + .1f * i);
                    Emit(motes, 4);
                }
                // A few loose seeds peel off as particles while the wave runs, then drift on after the essence hides.
                if (now > was && i < seedHeads.Length && seedHeads[i] != null)
                    for (seedDebt += (now - was) * 9; seedDebt >= 1; seedDebt--) EmitSeed(seedHeads[i].transform.position);
            }
        }

        private void EmitSeed(Vector3 position)
        {
            if (burst == null) return;
            if (!burst.isPlaying) burst.Play();
            Vector3 wind = transform.TransformDirection(windDirection).normalized;
            burst.Emit(new ParticleSystem.EmitParams
            {
                position = position + Random.insideUnitSphere * .09f, applyShapeToPosition = false,
                velocity = wind * Random.Range(.2f, .45f) + transform.up * Random.Range(.04f, .16f)
            }, 1);
        }

        private static float Smooth01(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        private static Vector3 Local(Transform part, Vector3 world) =>
            (part.parent != null ? part.parent.InverseTransformDirection(world) : world).normalized;

        /// <summary>
        /// SkyBloom: the three dandelions are picked one after another. Each flower bows into the breeze as its seeds
        /// leave in a wave (the Dandelion Seeds shader flies them), then the bare stalk sinks into the rosette while the
        /// next flower starts. Idle, each flower nods slowly on its own rhythm. All motion is eased; nothing pulses.
        /// </summary>
        private void PoseFlowers(float grow)
        {
            float shown = ShownProgress = Mathf.Max(RawShown, easedProgress);
            Vector3 wind = transform.TransformDirection(windDirection).normalized;
            var windData = new Vector4(wind.x, wind.y, wind.z, 1.4f);
            float raw = RawShown, gone = 0;
            for (int i = 0; i < flowers.Length; i++)
            {
                var flower = flowers[i]; if (flower == null) continue;
                float release = FlowerRelease(i, shown), sink = FlowerSink(i, shown);
                // Later flowers start bowing a little before their wave; every bow is exactly 0 at rest.
                float lead = Mathf.Min(.06f, i * FlowerSpacing);
                float bow = FlowerBow * Smooth01((shown - i * FlowerSpacing + lead) / .18f) + SinkBow * sink + blend * 1.5f;
                float nod = (Mathf.Sin(elapsed * (.8f + .13f * i) + i * 2.1f) * 1.8f + Mathf.Sin(elapsed * .37f + i) * 1.1f) * (1 - sink) * Smooth01(settle);
                flower.localRotation = Quaternion.AngleAxis(bow, flowerAxes[i]) * Quaternion.AngleAxis(nod, nodAxes[i]) * flowerRotations[i];
                flower.localScale = flowerScales[i] * (Mathf.Lerp(1, .06f, sink) * grow);
                // Gameplay-facing share still standing follows the gather itself, not the eased picture.
                gone += Mathf.Max(Mathf.Clamp01(FlowerRelease(i, raw)) * .6f, FlowerSink(i, raw));
                if (i >= seedHeads.Length || seedHeads[i] == null) continue;
                seedHeads[i].GetPropertyBlock(block);
                block.SetFloat(ReleaseId, release); block.SetFloat(FlightId, FlowerFlight / FlowerWave); block.SetVector(WindId, windData);
                seedHeads[i].SetPropertyBlock(block);
            }
            PresentationScale = WidthScale = (1 - gone / flowers.Length) * grow;
            // Loose seeds leave first, carried downwind as they shrink away.
            float away = Smooth01(shown / .4f), fade = 1 - Smooth01((shown - .12f) / .3f);
            FloaterLift = Smooth01(raw / .4f) * .45f;
            for (int i = 0; i < floaters.Length; i++)
            {
                var floater = floaters[i]; if (floater == null) continue;
                int p = 2 + i;
                float bob = Mathf.Sin(elapsed * (1.4f + i * .31f) + i * 1.9f) * .04f;
                float sway = Mathf.Sin(elapsed * (.9f + i * .23f) + i) * .05f * (1 + blend);
                Vector3 side = Vector3.Cross(ups[p], positions[p]).normalized;
                float idle = Smooth01(settle);                       // the bob fades in after a reset instead of jumping
                floater.localPosition = positions[p] + ups[p] * (bob * idle + away * .45f) + side * (sway * idle) + floaterWind[i] * (away * 1.1f);
                floater.localScale = scales[p] * grow * fade;
            }
        }

        private void ApplyPose()
        {
            if (!initialized) return;
            if (Bloom)
            {
                float bloomGrow = respawnTime > 0 ? 1 - Mathf.Pow(respawnTime / BloomRegrow, 3) : 1;
                PoseFlowers(Mathf.Lerp(.06f, 1, bloomGrow));
                // The silk brightens gently while it is gathered; the slow shimmer stays under 0.25 Hz.
                Glow(.85f + .1f * Mathf.Sin(elapsed * 1.3f) + blend * (.25f + ShownProgress * .45f));
                return;
            }
            bool bloom = style == EssenceStyle.SkyBloom;
            float gather = working ? Mathf.SmoothStep(0, 1, progress) : 0;
            // What the dandelions show: the gather, easing back after a cancel.
            float shown = bloom ? Mathf.Max(gather, easedGather) : gather;
            float hit = bloom ? easedImpact : impact;
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
                    case EssenceStyle.SkyBloom:
                        // Seed heads puff up a touch with each pull, as if the wind fills them.
                        width *= 1 + hit * .04f; break;
                    case EssenceStyle.SkyDrift:
                        float breathe = Mathf.Sin(elapsed * 1.2f) * .03f + blend * Mathf.Sin(elapsed * 5) * .03f;
                        width *= 1 + breathe; height *= 1 + breathe * .6f; break;
                    default:
                        height *= 1 + Mathf.Sin(elapsed * 3.1f) * .04f * (1 + blend * 2); break;
                }
                body.localScale = Vector3.Scale(scales[0], Vector3.one * width + bodyUpMask * (height - width));
                // Dandelions are not squashed: the stalks shrink back whole and lean downwind as their seeds blow away.
                if (bloom) body.localScale = scales[0] * (Mathf.Lerp(1, .5f, shown) * grow * (1 + hit * .03f));
                float turn = style == EssenceStyle.SkyDrift ? Mathf.Sin(elapsed * .4f) * 6 : 0;
                body.localRotation = Quaternion.AngleAxis(turn, ups[0]) * rotations[0];
                if (bloom)
                {
                    // Stalks nod slowly in the breeze and bow downwind as they are gathered; each pull is a gust, not a shake.
                    float nod = Mathf.Sin(elapsed * 1.1f) * 2.2f + Mathf.Sin(elapsed * .43f + 1) * 1.6f;
                    float bow = blend * 3 + shown * 21 + hit * 2.5f;
                    body.localRotation = Quaternion.AngleAxis(nod + bow, bodySide) * rotations[0];
                }
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
                float lift = shown * .55f * (1 + i * .15f);
                floater.localPosition = positions[p] + ups[p] * (bob + lift) + side * sway - positions[p] * (shown * .35f);
                floater.localScale = scales[p] * grow * Mathf.Lerp(1, .25f, shown);
            }
            Glow(.5f + .2f * Mathf.Sin(elapsed * 2.1f) + blend * (.5f + gather) + hit * .8f);
        }

        private void Glow(float glow)
        {
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
            audioSource.PlayOneShot(clip, volume * TowerOfBabel.Rendering.AeroAudio.Vfx);
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
            easedGather = easedImpact = easedProgress = holdTime = seedDebt = ShownProgress = settle = 0; flowersStarted = 0;
            for (int i = 0; i < parts.Length; i++)
                if (parts[i] != null) { parts[i].localPosition = positions[i]; parts[i].localScale = scales[i]; parts[i].localRotation = rotations[i]; }
            for (int i = 0; i < flowers.Length; i++)
                if (flowers[i] != null) { flowers[i].localScale = flowerScales[i]; flowers[i].localRotation = flowerRotations[i]; }
            for (int i = 0; i < seedHeads.Length; i++)
            {
                if (seedHeads[i] == null) continue;
                seedHeads[i].GetPropertyBlock(block); block.SetFloat(ReleaseId, 0); seedHeads[i].SetPropertyBlock(block);
            }
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
        /// <summary>Edit Mode preview: advance the real runtime update by dt (drive it with BeginWork, SetProgress, Deplete).</summary>
        public void StepEditor(float dt) { if (Application.isPlaying) return; Initialize(); Tick(dt); }

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
            easedGather = gathering ? Mathf.SmoothStep(0, 1, progress) : 0; easedImpact = impact;
            easedProgress = progress; holdTime = 0; settle = 1;
            if (Bloom) impact = easedImpact = 0;
            ApplyPose();
        }
#endif
    }
}
