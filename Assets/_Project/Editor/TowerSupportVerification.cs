#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

[InitializeOnLoad]
public static class TowerSupportVerification
{
    private const string Key="TowerSupportVerification.Mode";
    static TowerSupportVerification()
    {
        if(!string.IsNullOrEmpty(SessionState.GetString(Key,"")))TestRunnerApi.RegisterTestCallback(new Results());
    }
    public static string Start(bool playMode=false)
    {
        if(Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Start verification from this project's Edit Mode.");
        string mode=playMode?"PlayMode":"EditMode";SessionState.SetString(Key,mode);
        File.WriteAllText($"Library/TowerSupport-{mode}-Status.txt","Running");TestRunnerApi.RegisterTestCallback(new Results());
        var api=ScriptableObject.CreateInstance<TestRunnerApi>();
        return api.Execute(new ExecutionSettings(new Filter {testMode=playMode?TestMode.PlayMode:TestMode.EditMode,
            groupNames=playMode?new[]{"CheatMenuPlayModeTests","TowerProgressionPlayModeTests"}:new[]{"DebugSupportEditModeTests","TowerProgressionEditModeTests","TowerTileVisibilityEditModeTests"}}));
    }
    private sealed class Results:ICallbacks
    {
        public void RunStarted(ITestAdaptor tests){} public void TestStarted(ITestAdaptor test){} public void TestFinished(ITestResultAdaptor result){}
        public void RunFinished(ITestResultAdaptor result)
        {
            string mode=SessionState.GetString(Key,"");if(string.IsNullOrEmpty(mode))return;
            var cases=new List<object>();Visit(result);
            File.WriteAllText($"Documentation/FrutigerAero/Buildings/TowerSupport-{mode}-Results.json",
                Newtonsoft.Json.JsonConvert.SerializeObject(new {mode,result.PassCount,result.FailCount,result.SkipCount,result.Duration,cases},Newtonsoft.Json.Formatting.Indented));
            File.WriteAllText($"Library/TowerSupport-{mode}-Status.txt",$"Completed: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
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
