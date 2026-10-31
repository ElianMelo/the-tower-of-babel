using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Falling rain around the camera: URP particle port of DYNAMIC Falling Rain (drop streaks, collision splashes,
    /// rain loop audio) plus an overcast sky. Follows AeroWetnessController.RainAmount so the falling rain,
    /// the surface film and the sky fade in and out together. Visual only; the weather itself is decided by
    /// NetworkWeatherService and applied to the wetness controller on every client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AeroRainController : MonoBehaviour
    {
        public static AeroRainController Instance { get; private set; }

        public AeroRainProfile profile;
        public AeroWetnessController wetness;
        public AeroDayNightCycle dayNight;
        [Tooltip("Tower shape and built floors: no drops spawn under a roof, and drops stop on the first deck below.")]
        public AeroTowerAtmosphere tower;
        [Tooltip("Camera the rain follows; Camera.main when empty.")]
        public Transform follow;

        private ParticleSystem drops, splashes;
        private AudioSource audioSource;
        private float shelterTimer, shelterVolume = 1;
        private bool sheltered;
        private float emitCarry, clock, currentRate;
        private readonly System.Collections.Generic.List<Vector4> pendingSplashes = new();
        private System.Random random = new(20261031);

        /// <summary>Current falling rain 0..1.</summary>
        public float Amount { get; private set; }
        public bool Sheltered => sheltered;
        public ParticleSystem Drops => drops;
        public ParticleSystem Splashes => splashes;
        public AudioSource Audio => audioSource;
        public float EmissionRate => currentRate;
        /// <summary>Drops skipped last tick because their column is under a roof.</summary>
        public int RejectedLastTick { get; private set; }
        public int EmittedLastTick { get; private set; }

        private void OnEnable()
        {
            Instance = this;
            if (wetness == null) wetness = GetComponent<AeroWetnessController>();
            if (dayNight == null) dayNight = GetComponent<AeroDayNightCycle>();
            if (tower == null) tower = wetness != null && wetness.tower != null ? wetness.tower : GetComponent<AeroTowerAtmosphere>();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            if (dayNight != null) dayNight.overcast = 0;
            if (drops != null) drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (audioSource != null) audioSource.Stop();
        }

        private void OnDestroy()
        {
            if (drops == null) return;
            if (Application.isPlaying) Destroy(drops.gameObject); else DestroyImmediate(drops.gameObject);
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            Transform target = follow != null ? follow : Camera.main != null ? Camera.main.transform : null;
            Tick(Time.deltaTime, target != null ? target.position : transform.position, target != null ? target.forward : Vector3.forward);
        }

        /// <summary>Follows the camera and applies the current rain amount. Public for tests.</summary>
        public void Tick(float dt, Vector3 cameraPosition, Vector3 cameraForward)
        {
            if (profile == null || !profile.Ready) return;
            Amount = wetness != null ? wetness.RainAmount : 0;
            if (dayNight != null)
            {
                dayNight.overcastSunScale = profile.sunScale; dayNight.overcastSkyScale = profile.skyScale; dayNight.overcastFogScale = profile.fogScale;
                dayNight.overcast = Amount * profile.overcast;
            }
            if (Amount <= 0 && drops == null) return;
            EnsureSystems();

            Vector3 ahead = Vector3.ProjectOnPlane(cameraForward, Vector3.up);
            ahead = ahead.sqrMagnitude > .001f ? ahead.normalized * profile.lookAhead : Vector3.zero;
            drops.transform.SetPositionAndRotation(cameraPosition + ahead + Vector3.up * profile.spawnHeight, Quaternion.identity);

            float daylight = dayNight != null ? dayNight.Daylight : 1;
            var main = drops.main; main.startColor = Color.Lerp(profile.nightTint, profile.dayTint, daylight);
            var splashMain = splashes.main; splashMain.startColor = Color.Lerp(profile.splashNightTint, profile.splashDayTint, daylight);
            if (Amount > 0 && !drops.isPlaying) drops.Play(true);
            clock += dt;
            Emit(dt, cameraPosition, cameraPosition + ahead);

            shelterTimer -= dt;
            if (shelterTimer <= 0)
            {
                shelterTimer = .25f;
                sheltered = (tower != null && tower.IsUnderRoof(cameraPosition)) || Physics.Raycast(cameraPosition + Vector3.up * .5f, Vector3.up, profile.shelterCheckDistance, profile.collidesWith, QueryTriggerInteraction.Ignore);
            }
            shelterVolume = Mathf.MoveTowards(shelterVolume, sheltered ? profile.shelteredVolume : profile.outsideVolume, dt * .8f);
            if (audioSource != null)
            {
                audioSource.volume = shelterVolume * Amount;
                if (Amount > 0 && !audioSource.isPlaying && Application.isPlaying) audioSource.Play();
                else if (Amount <= 0 && audioSource.isPlaying) audioSource.Stop();
            }
        }

        /// <summary>
        /// Plans one drop. Returns false when its column is under a built roof (inside the tower), so it is never
        /// spawned; otherwise <paramref name="life"/> ends on the first deck below, or after the normal fall.
        /// Deterministic geometry, so rain never leaks into the interior through thin or unloaded colliders.
        /// </summary>
        public static bool PlanDrop(AeroTowerAtmosphere tower, Vector3 spawn, Vector3 velocity, float maxLife, out float life, out bool hitsDeck)
        {
            life = maxLife; hitsDeck = false;
            if (tower == null) return true;
            // Wind drifts a drop sideways while it falls; the margin keeps drifting drops out of the arches.
            float drift = new Vector2(velocity.x, velocity.z).magnitude * maxLife;
            if (tower.IsUnderRoof(spawn, drift + .5f)) return false;
            float fall = Mathf.Max(.1f, -velocity.y);
            if (tower.TryDeckBelow(spawn, out float deck) && deck <= spawn.y)
            {
                float t = (spawn.y - deck) / fall;
                if (t < maxLife) { life = t; hitsDeck = true; }
            }
            return true;
        }

        private float Range(float a, float b) => a + (b - a) * (float)random.NextDouble();

        private void Emit(float dt, Vector3 cameraPosition, Vector3 areaCenter)
        {
            currentRate = profile.dropsPerSecond * Amount;
            emitCarry += currentRate * Mathf.Max(0, dt);
            int count = Mathf.Min((int)emitCarry, 4000); emitCarry -= count;
            if (count > 0 && emitCarry > currentRate) emitCarry = 0;
            float spawnY = cameraPosition.y + profile.spawnHeight, floorY = cameraPosition.y - profile.fallBelow;
            int rejected = 0, emitted = 0;
            var emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = 0; i < count; i++)
            {
                var spawn = new Vector3(areaCenter.x + Range(-profile.areaRadius, profile.areaRadius), spawnY, areaCenter.z + Range(-profile.areaRadius, profile.areaRadius));
                var velocity = new Vector3(profile.wind.x * Range(.8f, 1.2f), -profile.dropSpeed * Range(.9f, 1.1f), profile.wind.y * Range(.8f, 1.2f));
                float maxLife = (spawnY - floorY) / -velocity.y;
                if (!PlanDrop(tower, spawn, velocity, maxLife, out float life, out bool hitsDeck)) { rejected++; continue; }
                emit.position = spawn; emit.velocity = velocity; emit.startLifetime = life;
                drops.Emit(emit, 1); emitted++;
                if (hitsDeck && random.NextDouble() < profile.splashChance)
                {
                    Vector3 land = spawn + velocity * life;
                    pendingSplashes.Add(new Vector4(land.x, land.y + .02f, land.z, clock + life));
                }
            }
            RejectedLastTick = rejected; EmittedLastTick = emitted;
            var splash = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            for (int i = pendingSplashes.Count - 1; i >= 0; i--)
            {
                if (pendingSplashes[i].w > clock) continue;
                splash.position = pendingSplashes[i]; splashes.Emit(splash, 1);
                pendingSplashes.RemoveAt(i);
            }
        }

        private void EnsureSystems()
        {
            if (drops != null) return;
            shelterVolume = profile.outsideVolume;
            var root = new GameObject("Aero falling rain") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(transform, false);
            root.SetActive(false);
            drops = root.AddComponent<ParticleSystem>();
            drops.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = drops.main;
            main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = profile.maxDrops; main.startSpeed = 0; main.gravityModifier = 0;
            main.startLifetime = profile.DropLifetime;
            main.startSize3D = true;
            main.startSizeX = new ParticleSystem.MinMaxCurve(profile.dropWidth.x, profile.dropWidth.y);
            main.startSizeY = new ParticleSystem.MinMaxCurve(profile.dropLength.x, profile.dropLength.y);
            main.startSizeZ = 1;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            // Drops are emitted from script (see Emit/PlanDrop) so none spawn under the tower's roofs.
            var shape = drops.shape; shape.enabled = false;
            var emission = drops.emission; emission.rateOverTime = 0;
            var collision = drops.collision; collision.enabled = true; collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D; collision.quality = ParticleSystemCollisionQuality.Low;
            collision.collidesWith = profile.collidesWith; collision.lifetimeLoss = 1; collision.bounce = 0; collision.radiusScale = .1f;
            collision.enableDynamicColliders = false;
            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.VerticalBillboard;
            renderer.sharedMaterial = profile.dropMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
            renderer.sortingFudge = 10;

            var splashObject = new GameObject("Aero rain splashes") { hideFlags = HideFlags.DontSave };
            splashObject.transform.SetParent(root.transform, false);
            splashes = splashObject.AddComponent<ParticleSystem>();
            splashes.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var splashMain = splashes.main;
            splashMain.loop = false; splashMain.playOnAwake = false; splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.maxParticles = profile.maxSplashes; splashMain.startSpeed = 0; splashMain.startLifetime = profile.splashLifetime;
            splashMain.startSize = new ParticleSystem.MinMaxCurve(profile.splashSize.x, profile.splashSize.y);
            var splashEmission = splashes.emission; splashEmission.rateOverTime = 0;
            splashEmission.SetBursts(new[] { new ParticleSystem.Burst(0, 1) });
            var splashShape = splashes.shape; splashShape.enabled = false;
            var grow = splashes.sizeOverLifetime; grow.enabled = true; grow.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.EaseInOut(0, .35f, 1, 1));
            var fade = splashes.colorOverLifetime; fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var splashRenderer = splashObject.GetComponent<ParticleSystemRenderer>();
            splashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            splashRenderer.sharedMaterial = profile.splashMaterial != null ? profile.splashMaterial : profile.dropMaterial;
            splashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; splashRenderer.receiveShadows = false;
            var sub = drops.subEmitters; sub.enabled = true;
            sub.AddSubEmitter(splashes, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, profile.splashChance);

            if (profile.rainLoop != null)
            {
                audioSource = root.AddComponent<AudioSource>();
                audioSource.clip = profile.rainLoop; audioSource.loop = true; audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0; audioSource.volume = 0; audioSource.priority = 64;
            }
            root.SetActive(true);
        }
    }
}
