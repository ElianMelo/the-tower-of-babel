#if UNITY_EDITOR
using System;
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
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.TestTools;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Diagnostic, not a gate: plays the saved Tower (host, full tower, idle, floor
    /// teleports) under the Editor profiler and writes the worst frames with their most
    /// expensive PlayerLoop markers to Documentation/Performance/LagSpikeProfile.json.
    /// Run explicitly through the automated runner.
    /// </summary>
    [Explicit("Performance diagnostic; run on demand."), Category("Performance")]
    public sealed class LagSpikeProfilePlayModeTests
    {
        const string ReportPath = "Documentation/Performance/LagSpikeProfile.json";

        [UnityTest]
        public IEnumerator Tower_RecordsSpikeFramesAndTheirMarkers()
        {
            var operation = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Project/Scenes/Tower.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Additive));
            yield return operation;
            var tower = UnityEngine.SceneManagement.SceneManager.GetSceneByPath("Assets/_Project/Scenes/Tower.unity");
            var manager = UnityEngine.Object.FindFirstObjectByType<FishNet.Managing.NetworkManager>();
            var menu = UnityEngine.Object.FindFirstObjectByType<CheatMenuUI>();
            bool profilerWasEnabled = ProfilerDriver.enabled;
            var phases = new List<(string name, int firstFrame)>();
            try
            {
                var bootstrap = UnityEngine.Object.FindFirstObjectByType<NetworkBootstrap>();
                if (!InstanceFinder.IsClientStarted) Assert.That(bootstrap.StartHost(), Is.True);
                float deadline = Time.realtimeSinceStartup + 20;
                while (!InstanceFinder.IsClientStarted && Time.realtimeSinceStartup < deadline) yield return null;
                for (int i = 0; i < 30; i++) yield return null;

                ProfilerDriver.ClearAllFrames();
                ProfilerDriver.profileEditor = false;
                ProfilerDriver.enabled = true;
                yield return null;

                phases.Add(("idle-start", ProfilerDriver.lastFrameIndex));
                yield return Seconds(6);
                var chunks = UnityEngine.Object.FindFirstObjectByType<ChunkManager>();
                menu.Show(); menu.SelectCategory("Construction");
                phases.Add(("build-tower", ProfilerDriver.lastFrameIndex));
                menu.ActionButtons["Instantly build tower"].onClick.Invoke();
                deadline = Time.realtimeSinceStartup + 30;
                while (!chunks.CachedChunks.All(c => c.Assets.All(a => a.Stage == 10)) && Time.realtimeSinceStartup < deadline) yield return null;
                menu.Hide();
                phases.Add(("idle-complete", ProfilerDriver.lastFrameIndex));
                yield return Seconds(8);
                foreach (int floor in new[] { 5, 15, 25, 35, 2 })
                {
                    menu.Show(); menu.SelectCategory("World"); menu.SetFloor(floor);
                    phases.Add(("teleport-" + floor, ProfilerDriver.lastFrameIndex));
                    menu.ActionButtons["Teleport to selected floor"].onClick.Invoke();
                    menu.Hide();
                    yield return Seconds(3.5f);
                }
                phases.Add(("end", ProfilerDriver.lastFrameIndex));
                ProfilerDriver.enabled = false;
                WriteReport(phases);
            }
            finally
            {
                ProfilerDriver.enabled = profilerWasEnabled;
                if (manager != null)
                {
                    manager.ClientManager.StopConnection();
                    manager.ServerManager.StopConnection(true);
                    UnityEngine.Object.Destroy(manager.gameObject);
                }
            }
            yield return null;
            if (tower.IsValid() && tower.isLoaded) yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(tower);
        }

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        struct Marker { public string name; public float ms; public float self; public long gc; public int depth; }

        static void Collect(HierarchyFrameDataView view, int id, int depth, List<Marker> markers)
        {
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (int child in children)
            {
                string name = view.GetItemName(child);
                float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                if (total < .25f) continue;
                markers.Add(new Marker
                {
                    name = name, ms = total, depth = depth,
                    self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime),
                    gc = (long)view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnGcMemory)
                });
                if (depth < 7 && name != "EditorLoop") Collect(view, child, depth + 1, markers);
            }
        }

        static string Phase(List<(string name, int firstFrame)> phases, int frame) =>
            phases.LastOrDefault(p => p.firstFrame <= frame).name ?? "?";

        static void WriteReport(List<(string name, int firstFrame)> phases)
        {
            int first = ProfilerDriver.firstFrameIndex, last = ProfilerDriver.lastFrameIndex;
            var frames = new List<(int index, float ms, long gc)>();
            for (int f = first; f <= last; f++)
            {
                using var view = ProfilerDriver.GetHierarchyFrameDataView(f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid) continue;
                float player = 0; long gc = 0;
                var children = new List<int>(); view.GetItemChildren(view.GetRootItemID(), children);
                foreach (int c in children)
                    if (view.GetItemName(c) == "PlayerLoop") { player = view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnTotalTime); gc = (long)view.GetItemColumnDataAsFloat(c, HierarchyFrameDataView.columnGcMemory); }
                frames.Add((f, player, gc));
            }
            var sorted = frames.Select(x => x.ms).OrderBy(x => x).ToArray();
            float median = sorted.Length > 0 ? sorted[sorted.Length / 2] : 0;
            float p95 = sorted.Length > 0 ? sorted[(int)(sorted.Length * .95f)] : 0;
            float threshold = Mathf.Max(33, median * 2.5f);
            var spikes = frames.Where(x => x.ms > threshold).OrderByDescending(x => x.ms).Take(25).ToList();
            var sb = new StringBuilder();
            sb.Append("{\n  \"frames\": ").Append(frames.Count)
              .Append(",\n  \"medianPlayerLoopMs\": ").Append(median.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(",\n  \"p95PlayerLoopMs\": ").Append(p95.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(",\n  \"spikeThresholdMs\": ").Append(threshold.ToString("0.00", CultureInfo.InvariantCulture))
              .Append(",\n  \"spikeCount\": ").Append(frames.Count(x => x.ms > threshold))
              .Append(",\n  \"spikesByPhase\": {");
            sb.Append(string.Join(",", frames.Where(x => x.ms > threshold).GroupBy(x => Phase(phases, x.index)).Select(g => $"\"{g.Key}\": {g.Count()}")));
            sb.Append("},\n  \"worst\": [\n");
            for (int s = 0; s < spikes.Count; s++)
            {
                var frame = spikes[s];
                using var view = ProfilerDriver.GetHierarchyFrameDataView(frame.index, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                var markers = new List<Marker>();
                Collect(view, view.GetRootItemID(), 0, markers);
                var top = markers.Where(m => m.depth >= 1).OrderByDescending(m => m.self).Take(10)
                    .Select(m => $"{{\"name\": \"{m.name.Replace("\"", "'")}\", \"selfMs\": {m.self.ToString("0.00", CultureInfo.InvariantCulture)}, \"totalMs\": {m.ms.ToString("0.00", CultureInfo.InvariantCulture)}, \"gcBytes\": {m.gc}}}");
                sb.Append("    {\"frame\": ").Append(frame.index).Append(", \"phase\": \"").Append(Phase(phases, frame.index))
                  .Append("\", \"playerLoopMs\": ").Append(frame.ms.ToString("0.00", CultureInfo.InvariantCulture))
                  .Append(", \"gcBytes\": ").Append(frame.gc).Append(", \"topSelf\": [").Append(string.Join(", ", top)).Append("]}")
                  .Append(s < spikes.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log("Lag spike profile written to " + ReportPath);
        }
    }
}
#endif
