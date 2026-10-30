using System;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace TowerOfBabel.Rendering.Tests
{
    /// <summary>
    /// Reads the Tower scene without disturbing it. Opening a second copy and closing it
    /// would run OnDisable on the open scene's atmosphere/finish drivers and reset shared
    /// shader globals for every later test.
    /// </summary>
    public static class TowerSceneAccess
    {
        public const string Path = "Assets/_Project/Scenes/Tower.unity";

        public static void With(Action<Scene> inspect)
        {
            var loaded = SceneManager.GetSceneByPath(Path);
            if (loaded.IsValid() && loaded.isLoaded) { inspect(loaded); return; }
            var scene = EditorSceneManager.OpenScene(Path, OpenSceneMode.Additive);
            try { inspect(scene); }
            finally { EditorSceneManager.CloseScene(scene, true); }
        }
    }
}
