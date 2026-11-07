using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>
    /// Seasonal floating particles around the camera: one particle system per shape (dot, petal, leaf, flake), so
    /// shapes already in the air keep their look while the season or time of day changes. Colour, size and motion
    /// are set at birth from the weighted atmosphere layers.
    /// </summary>
    public sealed class AeroAmbientParticles
    {
        public const int KindCount = 4;
        /// <summary>Spawns per second at density 1 for one shape.</summary>
        public const float MaxRate = 48;
        public const float Lifetime = 9;
        public static readonly Vector3 Area = new(40, 14, 40);

        private readonly ParticleSystem[] systems = new ParticleSystem[KindCount];
        private readonly Material[] materials = new Material[KindCount];
        private readonly GameObject root;

        /// <summary>Current spawn rate per shape, after shelter.</summary>
        public float[] Rates { get; } = new float[KindCount];
        public Color[] Colors { get; } = new Color[KindCount];
        public ParticleSystem System(AeroParticleKind kind) => systems[(int)kind];
        public GameObject Root => root;

        public AeroAmbientParticles(Transform parent, Shader shader)
        {
            root = new GameObject("Aero ambient particles") { hideFlags = HideFlags.DontSave };
            root.transform.SetParent(parent, false);
            for (int k = 0; k < KindCount; k++)
            {
                var go = new GameObject("Ambient " + AeroAtmosphereNames.Of((AeroParticleKind)k)) { hideFlags = HideFlags.DontSave };
                go.transform.SetParent(root.transform, false);
                var ps = go.AddComponent<ParticleSystem>();
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.loop = true; main.playOnAwake = false; main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(Lifetime * .7f, Lifetime);
                main.startSpeed = 0; main.maxParticles = 600; main.scalingMode = ParticleSystemScalingMode.Shape;
                main.startRotation = new ParticleSystem.MinMaxCurve(0, Mathf.PI * 2);
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = Area;
                var emission = ps.emission; emission.rateOverTime = 0;
                var fade = ps.colorOverLifetime; fade.enabled = true;
                var gradient = new Gradient();
                gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                    new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(1, .8f), new GradientAlphaKey(0, 1) });
                fade.color = gradient;
                var noise = ps.noise; noise.enabled = true; noise.frequency = .25f; noise.scrollSpeed = .3f; noise.quality = ParticleSystemNoiseQuality.Low;
                var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.World;
                bool tumbles = k == (int)AeroParticleKind.Petal || k == (int)AeroParticleKind.Leaf;
                var spin = ps.rotationOverLifetime; spin.enabled = k != (int)AeroParticleKind.Dot;
                float turn = tumbles ? 2.4f : .5f;
                spin.z = new ParticleSystem.MinMaxCurve(-turn, turn);
                var renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.sortingFudge = 5;
                if (shader != null)
                {
                    materials[k] = new Material(shader) { name = "Ambient " + (AeroParticleKind)k, hideFlags = HideFlags.DontSave };
                    materials[k].SetFloat("_Shape", k);
                    renderer.sharedMaterial = materials[k];
                }
                systems[k] = ps;
            }
        }

        /// <summary>
        /// Applies weighted layers. <paramref name="sheltered"/> (camera under a tower roof) keeps motes but almost
        /// stops falling shapes, which would otherwise fall through the floors above.
        /// </summary>
        public void Apply(AeroParticleLayer[] layers, Vector3 cameraPosition, Vector3 cameraForward, bool sheltered, bool enabled)
        {
            Vector3 flat = new Vector3(cameraForward.x, 0, cameraForward.z);
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            root.transform.position = cameraPosition + Vector3.up * 3 + flat * 8;
            for (int k = 0; k < KindCount; k++)
            {
                float weight = 0, size = 0, fall = 0, drift = 0; Color color = Color.black;
                if (layers != null)
                    foreach (var layer in layers)
                    {
                        if ((int)layer.kind != k || layer.density <= 0) continue;
                        float w = layer.density; weight += w;
                        color += layer.color * w; size += layer.size * w; fall += layer.fallSpeed * w; drift += layer.drift * w;
                    }
                var ps = systems[k];
                bool falls = k != (int)AeroParticleKind.Dot;
                float rate = enabled ? Mathf.Clamp01(weight) * MaxRate * (sheltered && falls ? .1f : 1) : 0;
                Rates[k] = rate;
                if (weight > 0)
                {
                    color /= weight; size /= weight; fall /= weight; drift /= weight; color.a = 1; Colors[k] = color;
                    var main = ps.main;
                    main.startColor = color;
                    main.startSize = new ParticleSystem.MinMaxCurve(size * .7f, size * 1.3f);
                    var velocity = ps.velocityOverLifetime;
                    velocity.x = new ParticleSystem.MinMaxCurve(-drift * .5f, drift * .5f);
                    velocity.y = new ParticleSystem.MinMaxCurve(-fall * 1.25f, -fall * .75f);
                    velocity.z = new ParticleSystem.MinMaxCurve(-drift * .5f, drift * .5f);
                    var noise = ps.noise; noise.strength = Mathf.Max(.05f, drift * .6f);
                }
                var emission = ps.emission; emission.rateOverTime = rate;
                if (rate > 0 && !ps.isPlaying) ps.Play(false);
            }
        }

        public void Destroy()
        {
            foreach (var m in materials) if (m != null) Release(m);
            if (root != null) Release(root);
        }

        private static void Release(Object o) { if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o); }
    }
}
