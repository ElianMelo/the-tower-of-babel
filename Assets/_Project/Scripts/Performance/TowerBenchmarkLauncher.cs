using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace TowerOfBabel.Performance
{
    /// <summary>
    /// Built game only: launched with <c>-towerBenchmark</c>, runs <see cref="TowerFrameBenchmark"/> in the Tower
    /// scene, writes TowerBenchmark.json next to the executable and quits. Gives frame times without Editor overhead.
    /// Run a development build so GPU and thread timings are available.
    /// </summary>
    public sealed class TowerBenchmarkLauncher : MonoBehaviour
    {
        public const string Argument = "-towerBenchmark";
        public const string FileName = "TowerBenchmark.json";

        public static bool Requested(string[] args) => Array.IndexOf(args ?? Array.Empty<string>(), Argument) >= 0;

        /// <summary>The executable's folder (the parent of the _Data folder).</summary>
        public static string OutputPath => Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", FileName);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Application.isEditor || !Requested(Environment.GetCommandLineArgs())) return;
            var go = new GameObject("Tower benchmark");
            DontDestroyOnLoad(go);
            go.AddComponent<TowerBenchmarkLauncher>();
        }

        private IEnumerator Start()
        {
            FrameTimingManager.CaptureFrameTimings();
            yield return TowerFrameBenchmark.Seconds(2);
            var benchmark = new TowerFrameBenchmark();
            yield return benchmark.Run();
            string note = benchmark.Failure ?? $"Player build ({(Debug.isDebugBuild ? "development" : "release")}), vsync off; medians of {TowerFrameBenchmark.Frames} frames per view.";
            try { File.WriteAllText(OutputPath, benchmark.Report(note)); }
            catch (Exception e) { Debug.LogException(e); }
            Debug.Log("Tower benchmark written to " + OutputPath);
            Application.Quit(benchmark.Failure == null ? 0 : 1);
        }
    }
}
