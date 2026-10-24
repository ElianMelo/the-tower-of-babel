using TowerOfBabel.Rendering;
using UnityEngine;

namespace TowerOfBabel.Buildings
{
    /// <summary>Registers near glass geometry. Chunk data owns the pooled projection lights.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Building))]
    public sealed class GlassWaterFloorLight : MonoBehaviour, IRiverGlassNearSource
    {
        public AeroWaterLightRegion region;
        public Matrix4x4 RiverObjectToWorld => transform.localToWorldMatrix;
        public float RiverHeight => transform.position.y;
        public float RiverReveal
        {
            get
            {
                if (surface == null) return 1;
                properties ??= new MaterialPropertyBlock(); surface.GetPropertyBlock(properties);
                return properties.GetFloat("_BuildMode") > .5f ? properties.GetFloat("_BuildReveal") : 1;
            }
        }
        private Building building;
        private MeshFilter filter;
        private Renderer surface;
        private MaterialPropertyBlock properties;
        private void OnEnable()
        {
            if (region != null) region.enabled = false;
            RiverGlassRenderRegistry.Register(this);
            RefreshPresentation();
        }
        private void OnDisable()
        {
            if (region != null) region.enabled = false;
            RiverGlassRenderRegistry.Unregister(this);
        }
        public void RefreshPresentation()
        {
            building ??= GetComponent<Building>(); filter ??= GetComponent<MeshFilter>(); surface ??= GetComponent<Renderer>();
            if (surface == null || surface.sharedMaterial == null) return;
            properties ??= new MaterialPropertyBlock(); surface.GetPropertyBlock(properties);
            properties.SetFloat("_RiverStage", building.CurrentStage);
            properties.SetFloat("_RiverFloorIndex", building.IsBound ? building.ChunkKey.FloorIndex : -1);
            surface.SetPropertyBlock(properties);
        }
        public bool TryGetDraw(Camera camera, out Mesh mesh, out Renderer renderer, out int floor,
            out float radial, out Vector2 center)
        {
            mesh = null; renderer = surface; floor = 0; radial = 0; center = default;
            if (building == null || filter == null || surface == null || !isActiveAndEnabled ||
                !surface.enabled || !building.IsBound || !building.IsComplete ||
                (camera.scene.IsValid() && camera.scene != gameObject.scene) ||
                (camera.cullingMask & (1 << gameObject.layer)) == 0) return false;
            mesh = filter.sharedMesh; floor = building.ChunkKey.FloorIndex;
            var mat = surface.sharedMaterial;
            radial = mat.GetFloat("_RadialFloor"); var p = mat.GetVector("_RadialFloorCenter"); center = new Vector2(p.x, p.y);
            return mesh != null;
        }
    }
}
