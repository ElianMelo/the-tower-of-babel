using System.Collections.Generic;
using UnityEngine;

namespace TowerOfBabel.Rendering
{
    /// <summary>Rendering-only data. Gameplay supplies geometry and construction visibility through this contract.</summary>
    public readonly struct AeroArchOpening
    {
        public readonly int Id, Floor;
        public readonly Vector3 Position, Inward;
        public AeroArchOpening(int id, int floor, Vector3 position, Vector3 inward)
        { Id = id; Floor = floor; Position = position; Inward = inward.normalized; }
    }

    public static class AeroTowerLightContext
    {
        private static readonly List<AeroTowerAtmosphere> Sources = new();
        internal static void Register(AeroTowerAtmosphere source) { if (!Sources.Contains(source)) Sources.Add(source); }
        internal static void Unregister(AeroTowerAtmosphere source) => Sources.Remove(source);
        public static AeroTowerAtmosphere ForCamera(Camera camera)
        {
            var scene = camera.scene.IsValid() ? camera.scene : camera.gameObject.scene;
            if (camera.cameraType == CameraType.SceneView) scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            // Preview/test cameras must not inherit the gameplay tower's volume or lights.
            for (int i = Sources.Count - 1; i >= 0; i--)
                if (Sources[i] != null && Sources[i].isActiveAndEnabled && Sources[i].gameObject.scene == scene)
                    return Sources[i];
            if (!camera.scene.IsValid())
                for (int i = Sources.Count - 1; i >= 0; i--)
                    if (Sources[i] != null && Sources[i].isActiveAndEnabled && Sources[i].gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                        return Sources[i];
            return null;
        }
    }
}
