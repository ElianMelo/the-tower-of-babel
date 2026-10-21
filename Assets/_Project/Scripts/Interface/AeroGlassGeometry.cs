using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Interface
{
    /// <summary>Supplies per-rectangle dimensions to the shared, mask-compatible glass material.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Graphic))]
    public sealed class AeroGlassGeometry : BaseMeshEffect
    {
        [SerializeField, Min(0)] private float cornerRadius = 18f;
        [SerializeField, Min(0)] private float borderWidth = 1.5f;
        [SerializeField, Range(0,1)] private float hoverFill = 1f;
        [SerializeField, Range(0,.35f)] private float borderMotion = .025f;
        [SerializeField, Range(0,1)] private float surfaceFlow = 0f;
        private static int clockFrame = -1;
        private float feedback;
        private Vector2 feedbackOrigin = new(.5f,.5f);
        private float hover;
        private Vector2 hoverOrigin = new(.5f,.5f);

        public void SetHover(float amount, Vector2 origin)
        {
            hover = Mathf.Clamp01(amount);
            hoverOrigin = origin;
            graphic.SetVerticesDirty();
        }

        public void SetFeedback(float amount, Vector2 origin)
        {
            feedback = Mathf.Clamp01(amount);
            feedbackOrigin = origin;
            graphic.SetVerticesDirty();
        }

        public float CornerRadius { get => cornerRadius; set { cornerRadius = Mathf.Max(0, value); graphic.SetVerticesDirty(); } }
        public float HoverFill => hoverFill;
        public float BorderMotion => borderMotion;
        public float SurfaceFlow => surfaceFlow;

        private void Update()
        {
            // One shared unscaled clock keeps paused menus alive without rebuilding UI meshes.
            if (Application.isPlaying && clockFrame == Time.frameCount) return;
            clockFrame = Time.frameCount;
            Shader.SetGlobalFloat("_AeroUiTime", Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            EnableChannels();
        }

        protected override void OnCanvasHierarchyChanged()
        {
            base.OnCanvasHierarchyChanged();
            EnableChannels();
        }

        private void EnableChannels()
        {
            if (graphic != null && graphic.canvas != null)
                graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2 | AdditionalCanvasShaderChannels.TexCoord3 | AdditionalCanvasShaderChannels.Tangent;
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            Rect rect = ((RectTransform)transform).rect;
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4(rect.width, rect.height, feedback, hover);
                vertex.uv2 = new Vector4(cornerRadius, borderWidth, feedbackOrigin.x, feedbackOrigin.y);
                vertex.uv3 = new Vector4(hoverOrigin.x,hoverOrigin.y,hoverFill,borderMotion);
                vertex.tangent = new Vector4(0,surfaceFlow,0,0);
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
