using UnityEngine;

namespace TowerOfBabel.World
{
    /// <summary>Light editor foliage and a bounded runtime view, without rebuilding terrain each frame.</summary>
    [ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(Terrain))]
    public sealed class TerrainPresentationQuality : MonoBehaviour
    {
        [SerializeField, Range(0, 1)] private float editorGrassDensity = .08f;
        [SerializeField, Min(0)] private float editorGrassDistance = 35;
        [SerializeField, Range(0, 1)] private float runtimeGrassDensity = .55f;
        [SerializeField, Min(0)] private float runtimeGrassDistance = 95;
        private Terrain terrain;
        private bool lastPlaying;
        public float RuntimeGrassDensity => runtimeGrassDensity;
        public float RuntimeGrassDistance => runtimeGrassDistance;
        public float EditorGrassDensity => editorGrassDensity;
        public float EditorGrassDistance => editorGrassDistance;

        private void OnEnable() => ApplyQuality(Application.isPlaying);
        private void OnValidate() => ApplyQuality(Application.isPlaying);
        private void Update()
        {
            if (lastPlaying != Application.isPlaying) ApplyQuality(Application.isPlaying);
        }
        public void ApplyQuality(bool playing)
        {
            terrain ??= GetComponent<Terrain>();
            if (terrain == null) return;
            lastPlaying = playing;
            terrain.drawInstanced = true;
            terrain.detailObjectDensity = playing ? runtimeGrassDensity : editorGrassDensity;
            terrain.detailObjectDistance = playing ? runtimeGrassDistance : editorGrassDistance;
            terrain.heightmapPixelError = playing ? 8 : 12;
        }
    }
}
