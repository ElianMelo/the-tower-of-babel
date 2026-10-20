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

        public float CornerRadius { get => cornerRadius; set { cornerRadius = Mathf.Max(0, value); graphic.SetVerticesDirty(); } }

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
                graphic.canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;
        }

        public override void ModifyMesh(VertexHelper helper)
        {
            if (!IsActive()) return;
            Rect rect = ((RectTransform)transform).rect;
            UIVertex vertex = default;
            for (int i = 0; i < helper.currentVertCount; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                vertex.uv1 = new Vector4(rect.width, rect.height, 0, 0);
                vertex.uv2 = new Vector4(cornerRadius, borderWidth, 0, 0);
                helper.SetUIVertex(vertex, i);
            }
        }
    }
}
