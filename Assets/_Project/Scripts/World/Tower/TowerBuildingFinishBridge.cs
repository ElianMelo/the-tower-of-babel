using System.Collections.Generic;
using TowerOfBabel.Rendering;
using TowerOfBabel.World.Chunks;
using UnityEngine;

namespace TowerOfBabel.World.Tower
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class TowerBuildingFinishBridge : MonoBehaviour, IAeroReflectionDeckSource
    {
        public ChunkManager chunks;
        private readonly Dictionary<ChunkKey, DeckChunk> deckCache = new();
        public int CachedDeckChunks => deckCache.Count;
        private readonly struct DeckSurface
        {
            public readonly Bounds Bounds;
            public readonly float Height;
            public DeckSurface(Bounds bounds, float height) { Bounds = bounds; Height = height; }
        }
        private sealed class DeckChunk
        {
            public ChunkSceneCache Source;
            public int Version;
            public Bounds Bounds;
            public DeckSurface[] Surfaces;
        }
        private void OnEnable() => AeroBuildingReflectionRegistry.Register(this);
        private void OnDisable() { AeroBuildingReflectionRegistry.Unregister(this); deckCache.Clear(); }
        private void OnValidate() => deckCache.Clear();

        private DeckChunk GetDecks(ChunkSceneCache chunk)
        {
            if (deckCache.TryGetValue(chunk.Key, out var cached) && cached.Source == chunk && cached.Version == chunk.Version) return cached;
            var surfaces = new List<DeckSurface>();
            Bounds combined = default;
            foreach (var asset in chunk.Assets)
            {
                if (!asset.AssetType.IsFloor() || asset.Stage != ChunkAssetData.CompletedStage) continue;
                var prefab = chunks.AssetPrefabs.GetPrefab(asset.AssetType);
                if (prefab == null) continue;
                Bounds local = prefab.GetComponent<MeshFilter>().sharedMesh.bounds;
                if (chunks.RadialFloors) local = TowerFloorLayout.ConservativeBounds(local, chunks.FloorRadialDepth, chunks.FloorArcLength);
                Matrix4x4 matrix = asset.ObjectToWorld;
                Vector3 e = local.extents;
                Vector3 x = matrix.MultiplyVector(new Vector3(e.x, 0, 0));
                Vector3 y = matrix.MultiplyVector(new Vector3(0, e.y, 0));
                Vector3 z = matrix.MultiplyVector(new Vector3(0, 0, e.z));
                var bounds = new Bounds(matrix.MultiplyPoint3x4(local.center), 2 * new Vector3(
                    Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z)));
                if (surfaces.Count == 0) combined = bounds; else combined.Encapsulate(bounds);
                surfaces.Add(new DeckSurface(bounds, asset.Position.y));
            }
            cached = new DeckChunk { Source = chunk, Version = chunk.Version, Bounds = combined, Surfaces = surfaces.ToArray() };
            deckCache[chunk.Key] = cached;
            return cached;
        }

        public bool FindReflectionDeck(Camera camera, Plane[] planes, float range, out int floor, out float height, out float pitch)
        {
            floor = -1; height = float.NegativeInfinity; pitch = chunks != null ? chunks.FloorHeight : 6;
            if (chunks == null || !isActiveAndEnabled ||
                (camera.scene.IsValid() && camera.scene != gameObject.scene) ||
                (camera.cullingMask & (1 << chunks.gameObject.layer)) == 0) return false;
            Vector3 eye = camera.transform.position;
            int top = Mathf.FloorToInt((eye.y - .21f) / pitch);
            int last = top - Mathf.CeilToInt(range / pitch);
            float padding = Mathf.Max(chunks.FloorRadialDepth, chunks.FloorArcLength) + 2;
            for (int candidate = top; candidate >= Mathf.Max(0, last); candidate--)
            {
                foreach (var chunk in chunks.CachedChunks)
                {
                    int f = chunk.Key.FloorIndex;
                    if (f != candidate || !chunks.IsFloorVisible(f)) continue;
                    Bounds chunkBounds = ChunkGrid.GetWorldBounds(chunk.Key, chunks.ChunkSizeMeters, pitch);
                    chunkBounds.Expand(padding * 2);
                    if (chunkBounds.SqrDistance(eye) > range * range) continue;
                    var decks = GetDecks(chunk);
                    if (decks.Surfaces.Length == 0 || decks.Bounds.SqrDistance(eye) > range * range || !GeometryUtility.TestPlanesAABB(planes, decks.Bounds)) continue;
                    foreach (var surface in decks.Surfaces)
                        if (surface.Height < eye.y - .05f && surface.Height > height && surface.Bounds.SqrDistance(eye) <= range * range &&
                            GeometryUtility.TestPlanesAABB(planes, surface.Bounds))
                        { floor = f; height = surface.Height; }
                }
                if (floor >= 0) return true;
            }
            return floor >= 0;
        }
    }
}
