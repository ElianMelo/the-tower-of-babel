#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using TowerOfBabel.Performance;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Performance diagnostic: plays the saved Tower as host with the whole tower built and measures the fixed views of
    /// <see cref="TowerFrameBenchmark"/> (shared with the built game's -towerBenchmark), with main-thread, render-thread
    /// and GPU times, per-system markers and a profiler snapshot of the heaviest outside view.
    /// Writes Documentation/Performance/FrameRateBenchmark.json. Editor numbers include Editor overhead (Game view
    /// repaint); compare runs on the same machine. Explicit: run on demand through the automated runner.
    /// </summary>
    [Explicit("Performance diagnostic; run on demand."), Category("Performance")]
    public sealed class FrameRateBenchmarkPlayModeTests
    {
        const string ReportPath = "Documentation/Performance/FrameRateBenchmark.json";
        const string CaptureFolder = "_Screenshots-and-previews-to-delete/Performance/";

        [UnityTest]
        public IEnumerator Tower_FrameTimes_InsideAndOutside()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            // GPU and thread timings come from FrameTimingManager; restored afterwards.
            bool timing = UnityEditor.PlayerSettings.enableFrameTimingStats;
            UnityEditor.PlayerSettings.enableFrameTimingStats = true;
            var benchmark = new TowerFrameBenchmark { AfterView = Captures };
            // Maximize the Game view so the Scene view and other Editor windows stop rendering during the run;
            // otherwise their cameras add GPU time and double-count shared passes such as the river glass.
            var gameView = GameView();
            bool wasMaximized = gameView != null && gameView.maximized;
            if (gameView != null) { gameView.Focus(); gameView.maximized = true; }
            yield return null;
            try
            {
                yield return benchmark.Run();
                Assert.That(benchmark.Failure, Is.Null);
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllText(ReportPath, benchmark.Report($"Editor Play Mode, vsync off; medians of {TowerFrameBenchmark.Frames} frames per view. Editor overhead included."));
            }
            finally
            {
                if (gameView != null) gameView.maximized = wasMaximized;
                UnityEditor.PlayerSettings.enableFrameTimingStats = timing;
                if (manager != null)
                {
                    manager.ClientManager.StopConnection();
                    manager.ServerManager.StopConnection(true);
                    Object.Destroy(manager.gameObject);
                }
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }

        static UnityEditor.EditorWindow GameView()
        {
            var type = typeof(UnityEditor.EditorWindow).Assembly.GetType("UnityEditor.GameView");
            return type != null ? UnityEditor.EditorWindow.GetWindow(type, false, null, false) : null;
        }

        // Review captures: the resident far path against the per-frame instanced path, same view.
        static IEnumerator Captures(TowerFrameBenchmark.View view, Camera camera)
        {
            if (view.name != "outside-ground-160m" && view.name != "inside-floor15-across") yield break;
            yield return Capture($"{view.name}-resident.png");
            InstancedFarChunkRenderer.GpuResidentInstancing = false;
            yield return TowerFrameBenchmark.Seconds(.5f);
            yield return Capture($"{view.name}-instanced.png");
            InstancedFarChunkRenderer.GpuResidentInstancing = true;
        }

        static IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(CaptureFolder);
            File.WriteAllBytes(CaptureFolder + name, image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
#endif
