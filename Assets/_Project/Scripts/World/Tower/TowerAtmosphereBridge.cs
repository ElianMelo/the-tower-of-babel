using System.Collections.Generic;
using TowerOfBabel.Buildings;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.Rendering;

namespace TowerOfBabel.World.Tower
{
    /// <summary>Construction-aware shadow cache. Only the viewed deck/ceiling (and outgoing transition) are resident.</summary>
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-40)]
    public sealed class TowerAtmosphereBridge : MonoBehaviour
    {
        public ChunkManager chunks;
        public NearTowerAssetPool near;
        public AeroTowerAtmosphere atmosphere;
        private readonly Dictionary<ChunkKey, Proxy> cache = new();
        private readonly List<ChunkKey> removals = new();
        private readonly List<AeroArchOpening> openings = new();
        private readonly Dictionary<TowerAssetType, Mesh[]> stages = new();
        private readonly RadialFloorMeshCache bentMeshes = new();
        private Material shadowMaterial;
        private Mesh slab, pillar;
        private float nextRefresh;
        private int floor = -100, previous = -100;
        private bool dirty = true;
        public int CachedShadowChunks => cache.Count;
        public int ShadowMeshBuilds { get; private set; }
        private sealed class Proxy { public GameObject Object; public Mesh Mesh; public int Version; }

        private void OnEnable()
        {
            if (chunks == null) return;
            chunks.AssetStageChanged += StageChanged;
            chunks.ConstructionFloorChanged += VisibilityChanged;
            dirty = true;
        }
        private void StageChanged(ChunkKey key, int index, byte stage) { if (cache.ContainsKey(key) || key.FloorIndex == floor || key.FloorIndex == floor + 1) dirty = true; }
        private void VisibilityChanged() => dirty = true;
        private void LateUpdate()
        {
            if (!Application.isPlaying || atmosphere == null || chunks == null) return;
            if (Time.unscaledTime < nextRefresh && !dirty) return;
            nextRefresh = Time.unscaledTime + .1f;
            Refresh(atmosphere.CurrentFloor, atmosphere.PreviousFloor);
        }
        public void Refresh(int viewedFloor, int outgoingFloor)
        {
            if (chunks == null || atmosphere == null || chunks.AssetPrefabs == null) return;
            bool changed = floor != viewedFloor || previous != outgoingFloor;
            floor = viewedFloor; previous = outgoingFloor;
            near?.SetShadowFloors(floor, floor + 1);
            removals.Clear();
            foreach (var entry in cache)
                if (!Selected(entry.Key.FloorIndex) || !chunks.IsFloorVisible(entry.Key.FloorIndex)) removals.Add(entry.Key);
            foreach (var key in removals) { Release(cache[key]); cache.Remove(key); }
            if (dirty || changed)
            {
                EnsureTemplates(); openings.Clear();
                // Stable chunk/local identity survives pool rentals and camera movement.
                int assetOffset = 0;
                foreach (var chunk in chunks.CachedChunks)
                {
                    int chunkOffset = assetOffset; assetOffset += chunk.Assets.Count;
                    int f = chunk.Key.FloorIndex;
                    if (!Selected(f) || !chunks.IsFloorVisible(f)) continue;
                    if (!cache.TryGetValue(chunk.Key, out var proxy) || proxy.Version != chunk.Version)
                    {
                        if (proxy != null) Release(proxy);
                        cache[chunk.Key] = Build(chunk);
                    }
                    if (f != floor) continue;
                    foreach (var asset in chunk.Assets)
                    {
                        // A source needs a built arch, never a locked/blueprint opening.
                        if (asset.AssetType != TowerAssetType.Arch || asset.Stage == 0) continue;
                        var complete = stages[TowerAssetType.Arch][10];
                        Vector3 point = asset.ObjectToWorld.MultiplyPoint3x4(new Vector3(complete.bounds.center.x, 0, 0));
                        point.y = f * chunks.FloorHeight + 3.6f;
                        Vector3 inward = new Vector3(chunks.FloorCenter.x - point.x, 0, chunks.FloorCenter.y - point.z).normalized;
                        point += inward * .25f;
                        int id = chunkOffset + asset.LocalIndex;
                        openings.Add(new AeroArchOpening(id, f, point, inward));
                    }
                }
                atmosphere.SetConstructionContext(chunks.IsFloorVisible(floor), openings);
                dirty = false;
            }
            // No mesh rebuilding when the near pool moves: activate cached distant shadows only.
            foreach (var entry in cache) entry.Value.Object.SetActive(near == null || !near.HasActiveChunk(entry.Key));
        }
        private bool Selected(int f) => f == floor || f == floor + 1 || f == previous || f == previous + 1;
        private void EnsureTemplates()
        {
            if (shadowMaterial != null) return;
            shadowMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
            foreach (TowerAssetType type in System.Enum.GetValues(typeof(TowerAssetType)))
            {
                var prefab = chunks.AssetPrefabs.GetPrefab(type); if (prefab == null) continue;
                var building = prefab.GetComponent<Building>(); var meshes = new Mesh[11];
                for (byte i = 0; i <= 10; i++) meshes[i] = building.GetStageMesh(i);
                stages[type] = meshes;
            }
            // Closed 12-triangle deck proxy; bent corners keep the same sector footprint.
            slab = Box(new Bounds(new Vector3(2, -.1f, -2), new Vector3(4, .2f, 4)));
            var bounds = stages[TowerAssetType.Pillar][10].bounds;
            pillar = Cylinder(bounds);
        }
        private Proxy Build(ChunkSceneCache chunk)
        {
            var instances = new List<CombineInstance>(chunk.Assets.Count);
            foreach (var asset in chunk.Assets)
            {
                if (asset.Stage == 0 || !stages.TryGetValue(asset.AssetType, out var models)) continue;
                Mesh mesh = models[asset.Stage];
                if (asset.Stage == 10 && asset.AssetType.IsFloor()) mesh = slab;
                if (asset.Stage == 10 && asset.AssetType == TowerAssetType.Pillar) mesh = pillar;
                if (asset.AssetType.IsFloor() && chunks.RadialFloors)
                    mesh = bentMeshes.Get(mesh, Vector2.Distance(new Vector2(asset.Position.x, asset.Position.z), chunks.FloorCenter), asset.Scale);
                // Partial stages use their actual render mesh; completed walking colliders are never occluders.
                instances.Add(new CombineInstance { mesh = mesh, transform = asset.ObjectToWorld });
            }
            var result = new Mesh { name = "Cached structural shadows " + chunk.Key, indexFormat = IndexFormat.UInt32, hideFlags = HideFlags.DontSave };
            if (instances.Count > 0) result.CombineMeshes(instances.ToArray(), true, true);
            var go = new GameObject(result.name, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            go.layer = gameObject.layer;
            go.transform.SetParent(transform, false);
            // Combined vertices are world-space; the bridge lives at the scene origin.
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); go.transform.localScale = Vector3.one;
            go.GetComponent<MeshFilter>().sharedMesh = result;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = shadowMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly; renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            ShadowMeshBuilds++;
            return new Proxy { Object = go, Mesh = result, Version = chunk.Version };
        }
        private static Mesh Box(Bounds bounds)
        {
            Vector3 a = bounds.min, b = bounds.max;
            var mesh = new Mesh { name = "Shadow deck slab", hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(a.x,a.y,a.z),new Vector3(b.x,a.y,a.z),new Vector3(b.x,b.y,a.z),new Vector3(a.x,b.y,a.z),new Vector3(a.x,a.y,b.z),new Vector3(b.x,a.y,b.z),new Vector3(b.x,b.y,b.z),new Vector3(a.x,b.y,b.z) };
            mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5};
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }
        private static Mesh Cylinder(Bounds bounds)
        {
            const int sides = 12; var vertices = new Vector3[sides * 2 + 2]; var triangles = new List<int>();
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z);
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2 / sides;
                var p = new Vector3(bounds.center.x + Mathf.Cos(angle)*radius, bounds.min.y, bounds.center.z + Mathf.Sin(angle)*radius);
                vertices[i] = p; p.y = bounds.max.y; vertices[i+sides] = p;
                int n=(i+1)%sides;
                triangles.AddRange(new[]{i,i+sides,n+sides,i,n+sides,n,sides*2,n,i,sides*2+1,i+sides,n+sides});
            }
            vertices[sides*2]=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            vertices[sides*2+1]=new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);
            var mesh=new Mesh {name="Shadow pillar",hideFlags=HideFlags.DontSave}; mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        private static void Delete(Object value) { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private static void Release(Proxy proxy)
        {
            // Destroy is deferred in Play Mode; hide immediately so rollback cannot leave a one-frame solid ceiling.
            if (proxy.Object != null) proxy.Object.SetActive(false);
            Delete(proxy.Object); Delete(proxy.Mesh);
        }
        private void OnDisable()
        {
            if (chunks != null) { chunks.AssetStageChanged -= StageChanged; chunks.ConstructionFloorChanged -= VisibilityChanged; }
            near?.SetShadowFloors(int.MaxValue, int.MinValue);
            foreach (var item in cache.Values) Release(item); cache.Clear(); stages.Clear(); bentMeshes.Dispose();
            Delete(shadowMaterial); Delete(slab); Delete(pillar); shadowMaterial=null;
            atmosphere?.SetConstructionContext(false, null);
        }
    }
}
