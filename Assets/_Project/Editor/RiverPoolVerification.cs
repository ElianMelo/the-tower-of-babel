#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>Runs the scoped pool regressions through Unity's runner and preserves results across Play Mode reloads.</summary>
[InitializeOnLoad]
public static class RiverPoolVerification
{
    private const string Key="RiverPoolVerification.Mode";
    static RiverPoolVerification()
    {
        if(!string.IsNullOrEmpty(SessionState.GetString(Key,""))) TestRunnerApi.RegisterTestCallback(new Results());
    }
    public static string Start(bool playMode=false, bool includeTowerCheats=false)
    {
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start verification from this project's Edit Mode.");
        string mode=playMode?"PlayMode":"EditMode";
        SessionState.SetString(Key,mode);
        File.WriteAllText($"Library/RiverPool-{mode}-Status.txt","Running");
        TestRunnerApi.RegisterTestCallback(new Results());
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();
        return api.Execute(new ExecutionSettings(new Filter {testMode=playMode?TestMode.PlayMode:TestMode.EditMode,
            groupNames=playMode?(includeTowerCheats?new[]{"GlassWaterFloorPlayModeTests","CheatMenuPlayModeTests","TowerProgressionPlayModeTests"}:new[]{"GlassWaterFloorPlayModeTests"}):new[]{"RiverGlassEditModeTests","RiverGlassGpuEditModeTests","RiverPoolGeometryTests","TowerTileVisibilityEditModeTests","GlassIslandEditModeTests"}}));
    }
    private sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(mode))return;
            var cases=new List<object>();Visit(result);
            string json=Newtonsoft.Json.JsonConvert.SerializeObject(new {mode,result.PassCount,result.FailCount,result.SkipCount,result.Duration,cases},Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText($"Documentation/FrutigerAero/Lighting/RiverPool-{mode}-Results.json",json);
            File.WriteAllText($"Library/RiverPool-{mode}-Status.txt",$"Completed: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
            SessionState.EraseString(Key);TestRunnerApi.UnregisterTestCallback(this);
            void Visit(ITestResultAdaptor node)
            {
                if(node.HasChildren)foreach(var child in node.Children)Visit(child);
                else cases.Add(new {node.FullName,node.ResultState,node.Message,node.StackTrace,node.Duration});
            }
        }
    }
}
#endif
