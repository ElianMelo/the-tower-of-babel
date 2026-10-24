using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    public interface IRiverGlassDrawSource
    {
        void CollectRiverGlass(Camera camera, Plane[] planes, List<RiverGlassDraw> draws);
    }

    public interface IRiverGlassNearSource
    {
        Matrix4x4 RiverObjectToWorld { get; }
        float RiverHeight { get; }
        float RiverReveal { get; }
        bool TryGetDraw(Camera camera, out Mesh mesh, out Renderer renderer, out int floor, out float radial, out Vector2 center);
    }

    /// <summary>One floor and mesh per draw; bounds and matrices use the existing radial tile paths.</summary>
    public sealed class RiverGlassDraw
    {
        public Mesh mesh;
        public Matrix4x4[] matrices;
        public int count;
        public int floor;
        public float height;
        public float radial;
        public float reveal = 1;
        public Vector2 center;
        public MaterialPropertyBlock properties;
        public Bounds bounds;

        public void Configure()
        {
            properties ??= new MaterialPropertyBlock();
            properties.SetFloat("_RadialFloor", radial);
            properties.SetVector("_RadialFloorCenter", new Vector4(center.x, center.y, 0, 0));
            properties.SetFloat("_RiverFloorIndex", floor);
            properties.SetFloat("_RiverReveal", reveal);
        }
    }

    public static class RiverGlassRenderRegistry
    {
        private static readonly HashSet<IRiverGlassNearSource> near = new();
        private static readonly HashSet<IRiverGlassDrawSource> far = new();
        private static readonly List<RiverGlassDraw> nearBatches = new();
        private static readonly HashSet<Light> projectionLights = new();
        public static void RegisterProjection(Light light) => projectionLights.Add(light);
        public static void UnregisterProjection(Light light) => projectionLights.Remove(light);

        public static bool NeedsProjectionShadows(Bounds bounds, Camera camera, int layer)
        {
            foreach (var light in projectionLights)
                if (light != null && light.isActiveAndEnabled && light.shadows != UnityEngine.LightShadows.None &&
                    (light.cullingMask & (1 << layer)) != 0 &&
                    (!camera.scene.IsValid() || light.gameObject.scene == camera.scene) &&
                    bounds.SqrDistance(light.transform.position) < light.range * light.range) return true;
            return false;
        }
        public static void Register(IRiverGlassNearSource source) => near.Add(source);
        public static void Unregister(IRiverGlassNearSource source) => near.Remove(source);
        public static void Register(IRiverGlassDrawSource source) => far.Add(source);
        public static void Unregister(IRiverGlassDrawSource source) => far.Remove(source);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset() { near.Clear(); far.Clear(); nearBatches.Clear(); projectionLights.Clear(); }

        public static void Collect(Camera camera, Plane[] planes, List<RiverGlassDraw> draws)
        {
            draws.Clear();
            far.RemoveWhere(source => source is Object obj && obj == null);
            foreach (var source in far) source.CollectRiverGlass(camera, planes, draws);
            int used = 0;
            foreach (var source in near)
            {
                if (source == null || (source is Object obj && obj == null) || !source.TryGetDraw(camera, out Mesh mesh, out Renderer renderer,
                        out int floor, out float radial, out Vector2 center)) continue;
                if (planes != null && !GeometryUtility.TestPlanesAABB(planes, renderer.bounds)) continue;
                RiverGlassDraw batch = null;
                float reveal = source.RiverReveal;
                for (int i = 0; i < used; i++)
                {
                    var candidate = nearBatches[i];
                    if (candidate.mesh == mesh && candidate.floor == floor && candidate.radial == radial && candidate.reveal == reveal &&
                        candidate.center == center && candidate.count < 511)
                    { batch = candidate; break; }
                }
                if (batch == null)
                {
                    if (used == nearBatches.Count) nearBatches.Add(new RiverGlassDraw { matrices = new Matrix4x4[511] });
                    batch = nearBatches[used++];
                    batch.mesh = mesh; batch.count = 0; batch.floor = floor; batch.height = source.RiverHeight;
                    batch.radial = radial; batch.center = center; batch.bounds = renderer.bounds;
                    batch.reveal = reveal;
                    batch.Configure(); draws.Add(batch);
                }
                batch.matrices[batch.count++] = source.RiverObjectToWorld;
                batch.bounds.Encapsulate(renderer.bounds);
            }
        }
    }
}
