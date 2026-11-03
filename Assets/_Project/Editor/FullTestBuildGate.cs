using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// The whole project test suite runs once, right before a player build, instead of on every change.
/// Pressing Build (or the menu below) first runs every EditMode test, then every PlayMode test, through the
/// automated runner; the build starts by itself when they all pass and is cancelled when any fails.
/// While working, run only the tests related to the change (see AGENTS.md, "Automated tests").
/// Batch-mode builds (CI, the unity CLI) skip the gate: run the suite there with "unity test" first.
/// </summary>
[InitializeOnLoad]
public static class FullTestBuildGate
{
    /// <summary>The project's own test assemblies; third-party package tests are not part of the gate.</summary>
    public static readonly string[] EditModeAssemblies = { "TowerOfBabel.Resources.Tests.EditMode", "TowerOfBabel.Rendering.Tests.EditMode" };
    public static readonly string[] PlayModeAssemblies = { "TowerOfBabel.Resources.Tests.PlayMode", "TowerOfBabel.Rendering.Tests.PlayMode" };
    public const string SkipVariable = "TOWER_SKIP_TEST_GATE";

    // Survives the domain reload that entering Play Mode for the PlayMode tests causes.
    private sealed class State : ScriptableSingleton<State>
    {
        public string stage = "";
        public int failures, passed;
        public bool approved;
        public string outputPath = "";
        public BuildTarget target;
        public BuildTargetGroup group;
        public BuildOptions options;
    }

    public static bool IsRunning => !string.IsNullOrEmpty(State.instance.stage);

    static FullTestBuildGate()
    {
        TestRunnerApi.RegisterTestCallback(new Results());
    }

    [MenuItem("Tower of Babel/Build/Run full test suite, then build")]
    public static void TestThenBuildFromSettings()
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        string path = EditorUserBuildSettings.GetBuildLocation(target);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("[Test gate] No previous build location. Build once from File > Build Profiles to choose one.");
            return;
        }
        Start(path, target, BuildPipeline.GetBuildTargetGroup(target), BuildOptions.None);
    }

    [MenuItem("Tower of Babel/Build/Run full test suite only")]
    public static void TestOnly() => Start("", EditorUserBuildSettings.activeBuildTarget, BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget), BuildOptions.None);

    /// <summary>Runs every project EditMode test, then every PlayMode test; builds to outputPath when all pass.</summary>
    public static void Start(string outputPath, BuildTarget target, BuildTargetGroup group, BuildOptions options)
    {
        if (IsRunning) { Debug.LogWarning("[Test gate] The full test suite is already running."); return; }
        var s = State.instance;
        s.outputPath = outputPath; s.target = target; s.group = group; s.options = options;
        s.failures = 0; s.passed = 0; s.approved = false;
        Run("EditMode");
    }

    private static void Run(string stage)
    {
        State.instance.stage = stage;
        bool play = stage == "PlayMode";
        Debug.Log($"[Test gate] Running the full {stage} suite before the build.");
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter
        {
            testMode = play ? TestMode.PlayMode : TestMode.EditMode,
            assemblyNames = play ? PlayModeAssemblies : EditModeAssemblies
        }));
    }

    private static void Finish()
    {
        var s = State.instance;
        s.stage = "";
        if (s.failures > 0)
        {
            Debug.LogError($"[Test gate] {s.failures} test(s) failed ({s.passed} passed). The build was not started.");
            return;
        }
        Debug.Log($"[Test gate] All {s.passed} tests passed.");
        if (string.IsNullOrEmpty(s.outputPath)) return;
        EditorApplication.delayCall += () =>
        {
            var scenes = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path).ToArray();
            s.approved = true;
            try
            {
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = scenes, locationPathName = s.outputPath, target = s.target, targetGroup = s.group, options = s.options
                });
                Debug.Log($"[Test gate] Build {report.summary.result}: {report.summary.outputPath}");
            }
            finally { s.approved = false; }
        };
    }

    /// <summary>Lets a build through only right after a green full run; otherwise starts that run instead.</summary>
    public static bool ShouldGate(out string reason)
    {
        reason = "";
        if (State.instance.approved) return false;
        if (Application.isBatchMode) { reason = "batch mode"; return false; }
        if (Environment.GetEnvironmentVariable(SkipVariable) == "1") { reason = SkipVariable; return false; }
        return true;
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }

        public void RunFinished(ITestResultAdaptor result)
        {
            var s = State.instance;
            if (string.IsNullOrEmpty(s.stage)) return;
            s.failures += result.FailCount; s.passed += result.PassCount;
            if (s.stage == "EditMode" && s.failures == 0) EditorApplication.delayCall += () => Run("PlayMode");
            else Finish();
        }
    }

    private sealed class Preprocess : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!ShouldGate(out string reason))
            {
                if (reason.Length > 0) Debug.Log($"[Test gate] Skipped ({reason}).");
                return;
            }
            var summary = report.summary;
            EditorApplication.delayCall += () => Start(summary.outputPath, summary.platform, summary.platformGroup, summary.options);
            throw new BuildFailedException("Running the full test suite first. The build starts by itself when every test passes " +
                                           "(Tower of Babel/Build/Run full test suite, then build).");
        }
    }
}
