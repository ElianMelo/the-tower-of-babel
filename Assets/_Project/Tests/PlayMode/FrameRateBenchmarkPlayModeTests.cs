#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using FishNet;
using NUnit.Framework;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.World.Chunks;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Performance diagnostic: plays the saved Tower as host with the whole tower built and measures frame time from
    /// fixed views inside the tower and outside it looking at the whole tower, with main-thread, render-thread and GPU
    /// times and a profiler snapshot of the heaviest outside view. Writes Documentation/Performance/FrameRateBenchmark.json.
    /// Editor numbers include Editor overhead (Game view repaint); compare runs on the same machine.
    /// Explicit: run on demand through the automated runner (include explicit tests).
    /// </summary>
    [Explicit("Performance diagnostic; run on demand."), Category("Performance")]
    public sealed class FrameRateBenchmarkPlayModeTests
    {
        const string ReportPath = "Documentation/Performance/FrameRateBenchmark.json";
        const int Frames = 90;

        [UnityTest]
        public IEnumerator Tower_FrameTimes_InsideAndOutside()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            var menu = Object.FindFirstObjectByType<CheatMenuUI>();
            int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            // GPU and thread timings come from FrameTimingManager; restored afterwards.
            bool timing = UnityEditor.PlayerSettings.enableFrameTimingStats;
            UnityEditor.PlayerSettings.enableFrameTimingStats = true;
            var rows = new List<string>();
            string hotspots = "{}";
            Behaviour[] brains = new Behaviour[0];
            try
            {
                var bootstrap = Object.FindFirstObjectByType<NetworkBootstrap>();
                if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!InstanceFinder.IsClientStarted && Time.realtimeSinceStartup < deadline) yield return null;
                for (int i = 0; i < 30; i++) yield return null;

                var chunks = Object.FindFirstObjectByType<ChunkManager>();
                menu.Show(); menu.SelectCategory("Construction");
                menu.ActionButtons["Instantly build tower"].onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 30;
                while (!chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 10)) && Time.realtimeSinceStartup < deadline) yield return null;
                menu.Hide();
                yield return Seconds(4);
                var far = Object.FindFirstObjectByType<InstancedFarChunkRenderer>();
                Assert.That(far, Is.Not.Null);
                // Cinemachine aims the camera from input; hold it still and aim it ourselves for repeatable views.
                var camera = Camera.main;
                Assert.That(camera, Is.Not.Null);
                brains = camera.GetComponents<Behaviour>().Where(b => b.GetType().Name == "CinemachineBrain" && b.enabled).ToArray();
                foreach (var brain in brains) brain.enabled = false;
                Vector3 center = new(chunks.FloorCenter.x, 0, chunks.FloorCenter.y);

                // Inside: stand on a floor and look across the tower.
                menu.Show(); menu.SelectCategory("World"); menu.SetFloor(15);
                menu.ActionButtons["Teleport to selected floor"].onClick.Invoke();
                menu.Hide();
                yield return Seconds(3);
                var views = new List<(string name, bool inside, Vector3 position, Vector3 look)>
                {
                    ("inside-floor15-across", true, Vector3.zero, Vector3.zero),
                    ("inside-floor15-outward", true, Vector3.zero, Vector3.zero),
                    ("outside-ground-160m", false, center + new Vector3(0, 3, -160), center + new Vector3(0, 60, 0)),
                    // Diagnostic: the same view without the far tower (and its river glass), to split its cost from the rest.
                    ("outside-ground-160m-towerHidden", false, center + new Vector3(0, 3, -160), center + new Vector3(0, 60, 0)),
                    ("outside-ground-260m", false, center + new Vector3(-180, 4, -190), center + new Vector3(0, 90, 0)),
                    ("outside-air-300m", false, center + new Vector3(220, 160, -220), center + new Vector3(0, 100, 0)),
                    // Last: the profiler snapshot enables every recorder, which must not slow the measured views.
                    ("outside-ground-160m-profiled", false, center + new Vector3(0, 3, -160), center + new Vector3(0, 60, 0)),
                };
                Vector3 eye = camera.transform.position;
                foreach (var view in views)
                {
                    if (view.inside)
                    {
                        // The follower keeps the camera on the player's head; only the heading changes.
                        Vector3 across = center - eye; across.y = 0;
                        if (view.name.EndsWith("outward")) across = -across;
                        camera.transform.rotation = Quaternion.LookRotation(across.normalized + Vector3.down * .12f);
                    }
                    else
                    {
                        var follower = camera.GetComponent<PlayerFollower>();
                        if (follower != null) follower.enabled = false;
                        camera.transform.position = view.position;
                        camera.transform.LookAt(view.look);
                    }
                    far.SetVisible(!view.name.EndsWith("towerHidden"));
                    yield return Seconds(1);
                    var times = new List<float>();
                    using (var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", Frames))
                    using (var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time", Frames))
                    using (var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time", Frames))
                    using (var farMarker = ProfilerRecorder.StartNew(ProfilerCategory.Scripts, "Tower.FarRenderer.Render", Frames))
                    {
                        for (int i = 0; i < Frames; i++) { yield return null; times.Add(Time.unscaledDeltaTime * 1000); }
                        times.Sort();
                        rows.Add(string.Format(CultureInfo.InvariantCulture,
                            "    {{\"view\": \"{0}\", \"medianMs\": {1:F2}, \"p90Ms\": {2:F2}, \"mainThreadMs\": {3:F2}, \"renderThreadMs\": {4:F2}, \"gpuMs\": {5:F2}, " +
                            "\"farRendererMs\": {6:F2}, \"visiblePages\": {7}, \"drawCalls\": {8} }}",
                            view.name, times[times.Count / 2], times[(int)(times.Count * .9f)], MedianMs(main), MedianMs(render), MedianMs(gpu),
                            MedianMs(farMarker), far.VisiblePageCount, far.DrawCallCount));
                    }
                    far.SetVisible(true);
                    if (view.name == "outside-ground-160m" || view.name == "inside-floor15-across")
                    {
                        // Review captures: the resident far path against the per-frame instanced path, same view.
                        yield return Capture(camera, $"{view.name}-resident.png");
                        InstancedFarChunkRenderer.GpuResidentInstancing = false;
                        yield return Seconds(.5f);
                        yield return Capture(camera, $"{view.name}-instanced.png");
                        InstancedFarChunkRenderer.GpuResidentInstancing = true;
                    }
                    if (view.name == "outside-ground-160m-profiled")
                    {
                        // Profiler snapshot: CPU and GPU time per marker, averaged over 30 frames.
                        var names = new List<string>();
                        UnityEngine.Profiling.Sampler.GetNames(names);
                        var recorders = names.Where(n => !n.Contains("Memory") && !n.Contains("Bytes") && !n.Contains("Count") && n != "Idle")
                            .Select(n => (n, r: UnityEngine.Profiling.Recorder.Get(n))).Where(x => x.r.isValid).ToList();
                        foreach (var x in recorders) x.r.enabled = true;
                        var cpu = new Dictionary<string, double>(); var gpuTimes = new Dictionary<string, double>();
                        for (int i = 0; i < 30; i++)
                        {
                            yield return null;
                            foreach (var x in recorders)
                            {
                                cpu[x.n] = (cpu.TryGetValue(x.n, out var c) ? c : 0) + x.r.elapsedNanoseconds / 1e6 / 30;
                                gpuTimes[x.n] = (gpuTimes.TryGetValue(x.n, out var g) ? g : 0) + x.r.gpuElapsedNanoseconds / 1e6 / 30;
                            }
                        }
                        foreach (var x in recorders) x.r.enabled = false;
                        string Top(Dictionary<string, double> d) => string.Join(", ", d.Where(e => e.Value > .05).OrderByDescending(e => e.Value).Take(40)
                            .Select(e => string.Format(CultureInfo.InvariantCulture, "{{\"name\": \"{0}\", \"ms\": {1:F2} }}", e.Key.Replace("\\", "/").Replace("\"", "'"), e.Value)));
                        hotspots = "{\"cpu\": [" + Top(cpu) + "], \"gpu\": [" + Top(gpuTimes) + "]}";
                    }
                }
                WriteReport(rows, hotspots);
            }
            finally
            {
                foreach (var brain in brains) if (brain != null) brain.enabled = true;
                QualitySettings.vSyncCount = vsync; Application.targetFrameRate = target;
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

        const string CaptureFolder = "_Screenshots-and-previews-to-delete/Performance/";

        static IEnumerator Capture(Camera camera, string name)
        {
            yield return new WaitForEndOfFrame();
            var image = ScreenCapture.CaptureScreenshotAsTexture();
            Directory.CreateDirectory(CaptureFolder);
            File.WriteAllBytes(CaptureFolder + name, image.EncodeToPNG());
            Object.Destroy(image);
        }

        // Median of a recorder's samples in milliseconds (0 when the counter is unavailable).
        static float MedianMs(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0;
            var samples = new List<long>(recorder.Count);
            for (int i = 0; i < recorder.Count; i++) samples.Add(recorder.GetSample(i).Value);
            samples.Sort();
            return samples[samples.Count / 2] / 1e6f;
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        static void WriteReport(List<string> rows, string hotspots)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"date\": \"" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\",");
            json.AppendLine("  \"note\": \"Editor Play Mode, vsync off; medians of 90 frames per view. Editor overhead included.\",");
            json.AppendLine("  \"gameView\": \"" + Screen.width + "x" + Screen.height + "\",");
            json.AppendLine("  \"hotspotsOutsideGround160m\": " + hotspots + ",");
            json.AppendLine("  \"views\": [");
            json.AppendLine(string.Join(",\n", rows));
            json.AppendLine("  ]");
            json.AppendLine("}");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, json.ToString());
        }
    }
}
#endif
