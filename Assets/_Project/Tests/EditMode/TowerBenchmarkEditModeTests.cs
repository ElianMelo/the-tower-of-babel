using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Performance;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class TowerBenchmarkEditModeTests
    {
        [Test]
        public void Launcher_OnlyRunsWhenAskedOnTheCommandLine()
        {
            Assert.That(TowerBenchmarkLauncher.Requested(new[] { "Tower.exe", "-towerBenchmark" }), Is.True);
            Assert.That(TowerBenchmarkLauncher.Requested(new[] { "Tower.exe" }), Is.False);
            Assert.That(TowerBenchmarkLauncher.Requested(null), Is.False);
            Assert.That(TowerBenchmarkLauncher.OutputPath, Does.EndWith(TowerBenchmarkLauncher.FileName));
        }

        [Test]
        public void Views_AreUniqueAndTheProfilerSnapshotRunsLast()
        {
            var names = TowerFrameBenchmark.Views.Select(v => v.name).ToArray();
            Assert.That(names, Is.Unique);
            Assert.That(names, Does.Contain("outside-ground-160m").And.Contain("inside-floor15-across").And.Contain("outside-ground-160m-towerHidden"));
            Assert.That(TowerFrameBenchmark.Views.Last().Profiled, Is.True, "The snapshot enables every recorder, so it must not slow measured views.");
            Assert.That(TowerFrameBenchmark.Views.Count(v => v.Profiled), Is.EqualTo(1));
            Assert.That(TowerFrameBenchmark.Markers.Select(m => m.key), Is.Unique);
        }
    }
}
