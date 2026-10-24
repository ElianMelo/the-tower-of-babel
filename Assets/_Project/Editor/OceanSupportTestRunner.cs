using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

[InitializeOnLoad]
public static class OceanSupportTestRunner
{
    static OceanSupportTestRunner() {TestRunnerApi.RegisterTestCallback(new Results());}
    public static void Run(bool play)
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Start verification from Edit Mode.");
        SessionState.SetString("OceanSupport.Mode",play?"PlayMode":"EditMode");
        SessionState.SetString("OceanSupport.Status","running");
        var api=UnityEngine.ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter {testMode=play?TestMode.PlayMode:TestMode.EditMode,
            testNames=play ? new[]{"TowerOfBabel.Rendering.Tests","TowerOfBabel.World.Tests.TowerProgressionPlayModeTests"} :
                new[]{"TowerOfBabel.Rendering.Tests","TowerOfBabel.World.Tests.DebugSupportEditModeTests","TowerOfBabel.World.Tests.TowerProgressionEditModeTests"}}));
    }
    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) {}
        public void TestStarted(ITestAdaptor test) {}
        public void TestFinished(ITestResultAdaptor result) {}
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode=SessionState.GetString("OceanSupport.Mode","unknown");
            TestRunnerApi.SaveResultToFile(result,"Temp/OceanSupport-"+mode+".xml");
            SessionState.SetString("OceanSupport.Status",$"{mode}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
        }
    }
}
