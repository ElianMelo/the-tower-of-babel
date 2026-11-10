using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using FishNet;
using TowerOfBabel.Cheats;
using TowerOfBabel.Networking;
using TowerOfBabel.World.Chunks;
using Unity.Profiling;
using UnityEngine;

namespace TowerOfBabel.Performance
{
    /// <summary>
    /// The tower frame-time benchmark, shared by the Editor PlayMode test and the built game (-towerBenchmark), so both
    /// measure the same views the same way. With the Tower scene loaded it starts a host, builds the whole tower,
    /// then measures fixed views inside and outside: frame time (median, p90), main/render thread and GPU time,
    /// and per-system markers (far tower, river glass, atmosphere, particles, cloud trace).
    /// </summary>
    public sealed class TowerFrameBenchmark
    {
        public const int Frames = 90;
        /// <summary>
        /// Markers reported per view: name in the JSON, the profiler sample name, and whether its GPU time is reported
        /// (otherwise CPU). The main camera's GPU time excludes other Editor windows, unlike the whole-frame GPU time.
        /// </summary>
        public static readonly (string key, string marker, bool gpu)[] Markers =
        {
            ("cameraGpuMs", "UniversalRenderPipeline.RenderSingleCameraInternal: Main Camera", true),
            ("farRendererMs", "Tower.FarRenderer.Render", false),
            ("riverGlassMs", "River glass floor composition", false),
            ("atmosphereMs", "Aero.Atmosphere.Tick", false),
            ("particlesMs", "Aero.Atmosphere.Particles", false),
            ("cloudTraceMs", "Aero cloud trace", true)
        };

        public readonly struct View
        {
            public readonly string name; public readonly bool inside; public readonly Vector3 offset, lookOffset;
            public View(string name, bool inside, Vector3 offset, Vector3 lookOffset) { this.name = name; this.inside = inside; this.offset = offset; this.lookOffset = lookOffset; }
            public bool HidesTower => name.EndsWith("towerHidden", StringComparison.Ordinal);
            public bool Profiled => name.EndsWith("profiled", StringComparison.Ordinal);
        }

        /// <summary>Views relative to the tower centre. Last: the profiler snapshot, which must not slow the measured views.</summary>
        public static readonly View[] Views =
        {
            new("inside-floor15-across", true, Vector3.zero, Vector3.zero),
            new("inside-floor15-outward", true, Vector3.zero, Vector3.zero),
            new("outside-ground-160m", false, new Vector3(0, 3, -160), new Vector3(0, 60, 0)),
            // Diagnostic: the same view without the far tower (and its river glass), to split its cost from the rest.
            new("outside-ground-160m-towerHidden", false, new Vector3(0, 3, -160), new Vector3(0, 60, 0)),
            new("outside-ground-260m", false, new Vector3(-180, 4, -190), new Vector3(0, 90, 0)),
            new("outside-air-300m", false, new Vector3(220, 160, -220), new Vector3(0, 100, 0)),
            new("outside-ground-160m-profiled", false, new Vector3(0, 3, -160), new Vector3(0, 60, 0)),
        };

        /// <summary>Optional per-view hook after measuring (the Editor test takes review captures here).</summary>
        public Func<View, Camera, IEnumerator> AfterView;
        public readonly List<string> Rows = new();
        public string Hotspots = "{}";
        public string Failure { get; private set; }

        /// <summary>Runs the benchmark in the loaded Tower scene. Check <see cref="Failure"/> afterwards.</summary>
        public IEnumerator Run()
        {
            var menu = UnityEngine.Object.FindFirstObjectByType<CheatMenuUI>();
            var bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
            if (menu == null || bootstrap == null) { Failure = "The Tower scene's cheat menu or network bootstrap is missing."; yield break; }
            int vsync = QualitySettings.vSyncCount, target = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            Behaviour[] brains = Array.Empty<Behaviour>();
            try
            {
                if (!InstanceFinder.IsClientStarted && !bootstrap.StartHost()) { Failure = "Could not start a local host."; yield break; }
                float deadline = Time.realtimeSinceStartup + 20;
                while (!InstanceFinder.IsClientStarted && Time.realtimeSinceStartup < deadline) yield return null;
                if (!InstanceFinder.IsClientStarted) { Failure = "The local host did not start."; yield break; }
                for (int i = 0; i < 30; i++) yield return null;

                var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
                menu.Show(); menu.SelectCategory("Construction");
                menu.ActionButtons["Instantly build tower"].onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 30;
                while (!chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 10)) && Time.realtimeSinceStartup < deadline) yield return null;
                menu.Hide();
                yield return Seconds(4);
                var far = UnityEngine.Object.FindFirstObjectByType<InstancedFarChunkRenderer>();
                var camera = Camera.main;
                if (far == null || camera == null) { Failure = "No far tower renderer or main camera."; yield break; }
                // Cinemachine aims the camera from input; hold it still and aim it ourselves for repeatable views.
                brains = camera.GetComponents<Behaviour>().Where(b => b.GetType().Name == "CinemachineBrain" && b.enabled).ToArray();
                foreach (var brain in brains) brain.enabled = false;
                Vector3 center = new(chunks.FloorCenter.x, 0, chunks.FloorCenter.y);

                menu.Show(); menu.SelectCategory("World"); menu.SetFloor(15);
                menu.ActionButtons["Teleport to selected floor"].onClick.Invoke();
                menu.Hide();
                yield return Seconds(3);
                Vector3 eye = camera.transform.position;
                foreach (var view in Views)
                {
                    if (view.inside)
                    {
                        // The follower keeps the camera on the player's head; only the heading changes.
                        Vector3 across = center - eye; across.y = 0;
                        if (view.name.EndsWith("outward", StringComparison.Ordinal)) across = -across;
                        camera.transform.rotation = Quaternion.LookRotation(across.normalized + Vector3.down * .12f);
                    }
                    else
                    {
                        var follower = camera.GetComponent<PlayerFollower>();
                        if (follower != null) follower.enabled = false;
                        camera.transform.position = center + view.offset;
                        camera.transform.LookAt(center + view.lookOffset);
                    }
                    far.SetVisible(!view.HidesTower);
                    yield return Seconds(1);
                    yield return Measure(view, far);
                    far.SetVisible(true);
                    if (AfterView != null) yield return AfterView(view, camera);
                    if (view.Profiled) yield return Snapshot();
                }
            }
            finally
            {
                foreach (var brain in brains) if (brain != null) brain.enabled = true;
                QualitySettings.vSyncCount = vsync; Application.targetFrameRate = target;
            }
        }

        private IEnumerator Measure(View view, InstancedFarChunkRenderer far)
        {
            var times = new List<float>(Frames);
            var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Main Thread Frame Time", Frames);
            var render = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "CPU Render Thread Frame Time", Frames);
            var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "GPU Frame Time", Frames);
            // Recorders give CPU and GPU time for script markers and render-graph samplers alike.
            var markers = Markers.Select(m => UnityEngine.Profiling.Recorder.Get(m.marker)).ToArray();
            var cpuTotals = new double[markers.Length]; var gpuTotals = new double[markers.Length];
            foreach (var r in markers) if (r.isValid) r.enabled = true;
            try
            {
                for (int i = 0; i < Frames; i++)
                {
                    yield return null;
                    times.Add(Time.unscaledDeltaTime * 1000);
                    for (int m = 0; m < markers.Length; m++)
                        if (markers[m].isValid) { cpuTotals[m] += markers[m].elapsedNanoseconds / 1e6; gpuTotals[m] += markers[m].gpuElapsedNanoseconds / 1e6; }
                }
                times.Sort();
                var row = new StringBuilder();
                row.AppendFormat(CultureInfo.InvariantCulture,
                    "    {{\"view\": \"{0}\", \"medianMs\": {1:F2}, \"p90Ms\": {2:F2}, \"mainThreadMs\": {3:F2}, \"renderThreadMs\": {4:F2}, \"gpuMs\": {5:F2}",
                    view.name, times[times.Count / 2], times[(int)(times.Count * .9f)], MedianMs(main), MedianMs(render), MedianMs(gpu));
                for (int m = 0; m < markers.Length; m++)
                {
                    double value = Markers[m].gpu ? gpuTotals[m] : cpuTotals[m];
                    row.AppendFormat(CultureInfo.InvariantCulture, ", \"{0}\": {1:F2}", Markers[m].key, value / Frames);
                }
                row.AppendFormat(CultureInfo.InvariantCulture, ", \"visiblePages\": {0}, \"drawCalls\": {1} }}", far.VisiblePageCount, far.DrawCallCount);
                Rows.Add(row.ToString());
            }
            finally
            {
                foreach (var r in markers) if (r.isValid) r.enabled = false;
                main.Dispose(); render.Dispose(); gpu.Dispose();
            }
        }

        /// <summary>Profiler snapshot: CPU and GPU time per marker, averaged over 30 frames.</summary>
        private IEnumerator Snapshot()
        {
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
            static string Top(Dictionary<string, double> d) => string.Join(", ", d.Where(e => e.Value > .05).OrderByDescending(e => e.Value).Take(40)
                .Select(e => string.Format(CultureInfo.InvariantCulture, "{{\"name\": \"{0}\", \"ms\": {1:F2} }}", e.Key.Replace("\\", "/").Replace("\"", "'"), e.Value)));
            Hotspots = "{\"cpu\": [" + Top(cpu) + "], \"gpu\": [" + Top(gpuTimes) + "]}";
        }

        /// <summary>The report JSON.</summary>
        public string Report(string note)
        {
            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"date\": \"" + DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "\",");
            json.AppendLine("  \"note\": \"" + note.Replace("\"", "'") + "\",");
            json.AppendLine("  \"resolution\": \"" + Screen.width + "x" + Screen.height + "\",");
            json.AppendLine("  \"hotspotsOutsideGround160m\": " + Hotspots + ",");
            json.AppendLine("  \"views\": [");
            json.AppendLine(string.Join(",\n", Rows));
            json.AppendLine("  ]");
            json.AppendLine("}");
            return json.ToString();
        }

        // Median of a recorder's samples in milliseconds (0 when the counter is unavailable).
        private static float MedianMs(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return 0;
            var samples = new List<long>(recorder.Count);
            for (int i = 0; i < recorder.Count; i++) samples.Add(recorder.GetSample(i).Value);
            samples.Sort();
            return samples[samples.Count / 2] / 1e6f;
        }

        public static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }
    }
}
