using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Resources.Interaction
{
    /// <summary>Buoyant figure-eight motion for visible meshes; source transforms and colliders stay fixed.</summary>
    [DisallowMultipleComponent]
    public sealed class AeroInteractionMotion : MonoBehaviour
    {
        private sealed class Visual
        {
            public MeshFilter SourceMesh, ProxyMesh;
            public MeshRenderer Source, Proxy;
            public Transform Anchor, Transform;
            public readonly List<Material> Materials = new();
            public bool WasForceRenderingOff;
        }

        private readonly List<Visual> visuals = new();
        private MaterialPropertyBlock properties;
        private Transform target;
        private float elapsed, strength, tempo, progress, blend;
        private bool interacting, presenting;

        public void Begin(Transform visualRoot, float amplitude, float speed)
        {
            if (visualRoot == null) return;
            properties ??= new MaterialPropertyBlock();
            Restore();
            if (target != visualRoot)
            {
                ReleaseProxies();
                target = visualRoot;
                foreach (var renderer in visualRoot.GetComponentsInChildren<MeshRenderer>(true))
                {
                    var mesh = renderer.GetComponent<MeshFilter>();
                    if (mesh == null || mesh.sharedMesh == null) continue;
                    // Keep render-only copies outside the owner hierarchy so outline caches,
                    // collider lookups and pooled-building bookkeeping still see the original asset.
                    var anchor = new GameObject("AeroInteractionPresentation");
                    anchor.hideFlags = HideFlags.DontSave;
                    UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(anchor, renderer.gameObject.scene);
                    var proxy = new GameObject("AeroInteractionVisual", typeof(MeshFilter), typeof(MeshRenderer));
                    proxy.transform.SetParent(anchor.transform, false);
                    proxy.layer = renderer.gameObject.layer;
                    proxy.SetActive(false);
                    var copy = proxy.GetComponent<MeshRenderer>();
                    copy.shadowCastingMode = renderer.shadowCastingMode;
                    copy.receiveShadows = renderer.receiveShadows;
                    copy.lightProbeUsage = renderer.lightProbeUsage;
                    copy.reflectionProbeUsage = renderer.reflectionProbeUsage;
                    copy.probeAnchor = renderer.probeAnchor;
                    copy.renderingLayerMask = renderer.renderingLayerMask;
                    copy.lightmapIndex = renderer.lightmapIndex;
                    copy.lightmapScaleOffset = renderer.lightmapScaleOffset;
                    copy.sortingLayerID = renderer.sortingLayerID;
                    copy.sortingOrder = renderer.sortingOrder;
                    visuals.Add(new Visual { Source = renderer, SourceMesh = mesh, Proxy = copy, Anchor = anchor.transform,
                        ProxyMesh = proxy.GetComponent<MeshFilter>(), Transform = proxy.transform });
                }
            }
            elapsed = progress = blend = 0;
            strength = Mathf.Max(0, amplitude);
            // Preserve existing authored tempo values: 24 now makes one smooth weave in ~2.6 seconds.
            tempo = Mathf.Max(0, speed) * .1f;
            interacting = presenting = true;
            foreach (var visual in visuals)
            {
                if (visual.Source == null || visual.Proxy == null) continue;
                visual.WasForceRenderingOff = visual.Source.forceRenderingOff;
                visual.Source.forceRenderingOff = true;
            }
            ApplyPose();
        }

        public void SetProgress(float normalizedProgress) => progress = Mathf.Clamp01(normalizedProgress);

        public void Stop(bool immediate = false)
        {
            interacting = false;
            if (immediate) Restore();
        }

        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float deltaTime)
        {
            if (!presenting) return;
            elapsed += deltaTime;
            blend = Mathf.MoveTowards(blend, interacting ? 1 : 0, deltaTime / .22f);
            ApplyPose();
            if (!interacting && blend <= 0) Restore();
        }

        private void ApplyPose()
        {
            float envelope = Mathf.SmoothStep(0, 1, blend) * Mathf.SmoothStep(0, 1, (1 - progress) / .18f);
            float phase = elapsed * tempo;
            Vector3 offset = new Vector3(Mathf.Sin(phase), (1 - Mathf.Cos(phase * 2)) * .25f,
                Mathf.Sin(phase * 2) * .35f) * (strength * envelope);
            float tilt = Mathf.Min(3, strength * 48) * envelope;
            Quaternion rotation = Quaternion.Euler(Mathf.Sin(phase * 2) * tilt * .3f, 0, -Mathf.Sin(phase) * tilt);
            foreach (var visual in visuals)
            {
                if (visual.Source == null || visual.Proxy == null || visual.SourceMesh == null) continue;
                // Stage changes and per-instance aquatic material overrides keep their normal owner.
                if (visual.ProxyMesh.sharedMesh != visual.SourceMesh.sharedMesh)
                    visual.ProxyMesh.sharedMesh = visual.SourceMesh.sharedMesh;
                visual.Anchor.SetPositionAndRotation(visual.Source.transform.position, visual.Source.transform.rotation);
                visual.Anchor.localScale = visual.Source.transform.lossyScale;
                visual.Source.GetSharedMaterials(visual.Materials);
                visual.Proxy.SetSharedMaterials(visual.Materials);
                visual.Proxy.renderingLayerMask = visual.Source.renderingLayerMask;
                visual.Proxy.gameObject.layer = visual.Source.gameObject.layer;
                visual.Source.GetPropertyBlock(properties);
                visual.Proxy.SetPropertyBlock(properties);
                visual.Proxy.enabled = visual.Source.enabled && visual.Source.gameObject.activeInHierarchy && !visual.WasForceRenderingOff;
                visual.Transform.gameObject.SetActive(true);
                // Amplitude stays consistent for scaled stones and radial building pieces.
                visual.Transform.position = visual.Anchor.position + visual.Anchor.rotation * offset;
                visual.Transform.localRotation = rotation;
                visual.Transform.localScale = Vector3.one;
            }
        }

        private void Restore()
        {
            foreach (var visual in visuals)
            {
                if (visual.Source != null && presenting) visual.Source.forceRenderingOff = visual.WasForceRenderingOff;
                if (visual.Proxy != null) visual.Proxy.gameObject.SetActive(false);
            }
            interacting = presenting = false;
            blend = 0;
        }

        private void ReleaseProxies()
        {
            foreach (var visual in visuals)
                if (visual.Anchor != null)
                {
                    if (Application.isPlaying) Destroy(visual.Anchor.gameObject);
                    else DestroyImmediate(visual.Anchor.gameObject);
                }
            visuals.Clear();
        }

        private void OnDisable() => Restore();
        private void OnDestroy() { Restore(); ReleaseProxies(); }

#if UNITY_EDITOR
        /// <summary>Visual authoring sample on a disposable preview instance, without Play Mode.</summary>
        public void SampleEditorPose(float seconds, float normalizedProgress)
        {
            if (Application.isPlaying || !presenting) return;
            elapsed = Mathf.Max(0, seconds);
            blend = Mathf.Clamp01(seconds / .22f);
            progress = Mathf.Clamp01(normalizedProgress);
            ApplyPose();
        }
#endif
    }
}
