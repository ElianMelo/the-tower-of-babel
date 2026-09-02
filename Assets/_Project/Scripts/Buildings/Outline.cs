namespace TowerOfBabel
{
    using LineworkLite.FreeOutline;
    using UnityEngine;
    using LineworkOutline = LineworkLite.FreeOutline.Outline;

    /// <summary>Locally toggles Linework's rendering layer without changing networked state.</summary>
    [DisallowMultipleComponent]
    public sealed class Outline : MonoBehaviour
    {
        [SerializeField] private FreeOutlineSettings settings;

        private Renderer[] renderers;
        private uint[] originalRenderingLayers;
        private bool isVisible;

        public FreeOutlineSettings Settings => settings;
        public bool IsVisible => isVisible;

        private void Awake()
        {
            CacheRenderers();
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            CacheRenderers();
            uint outlineLayer = GetOutlineLayer();
            isVisible = visible && outlineLayer != 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null)
                    continue;

                renderer.renderingLayerMask = isVisible
                    ? originalRenderingLayers[i] | outlineLayer
                    : originalRenderingLayers[i];
            }
        }

        private uint GetOutlineLayer()
        {
            if (settings == null || settings.Outlines == null || settings.Outlines.Count == 0)
                return 0;

            LineworkOutline definition = settings.Outlines[0];
            return definition != null ? definition.RenderingLayer.value : 0;
        }

        private void CacheRenderers()
        {
            Renderer[] found = GetComponentsInChildren<Renderer>(true);
            if (renderers != null && SameRenderers(renderers, found))
                return;

            renderers = found;
            originalRenderingLayers = new uint[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                originalRenderingLayers[i] = renderers[i].renderingLayerMask;
        }

        private static bool SameRenderers(Renderer[] left, Renderer[] right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i])
                    return false;
            }

            return true;
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
                isVisible = false;
        }
    }
}
