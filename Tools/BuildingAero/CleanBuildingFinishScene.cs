// Restore pre-existing prefab overrides from the pre-change scene snapshot.
// Invoke through Unity eval_file after the disposable review has closed.
if (UnityEngine.Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets")
    throw new System.InvalidOperationException("Wrong project.");
var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
if (scene.path != "Assets/_Project/Scenes/Tower.unity") throw new System.InvalidOperationException("Open Tower first.");
var original = System.IO.File.ReadAllText("Temp/FinishOriginalTower.unity");
var blocks = new System.Collections.Generic.Dictionary<ulong,System.Collections.Generic.Dictionary<string,string>>();
foreach(System.Text.RegularExpressions.Match block in System.Text.RegularExpressions.Regex.Matches(original,@"(?ms)^--- !u!1001 &(\d+)\r?\n(.*?)(?=^---|\z)"))
{
    var entries=new System.Collections.Generic.Dictionary<string,string>();
    foreach(System.Text.RegularExpressions.Match entry in System.Text.RegularExpressions.Regex.Matches(block.Groups[2].Value,
        @"(?m)^    - target: \{fileID: (-?\d+), guid: ([a-f0-9]+), type: \d+\}\r?\n      propertyPath: (.+)\r?\n      value:(?: (.*))?\r?\n      objectReference:"))
        entries[entry.Groups[1].Value+"|"+entry.Groups[2].Value+"|"+entry.Groups[3].Value.Trim('\'', '\r')]=entry.Groups[4].Value.TrimEnd('\r');
    blocks[ulong.Parse(block.Groups[1].Value)]=entries;
}
int changed=0;
var seen=new System.Collections.Generic.HashSet<ulong>();
foreach(var root in scene.GetRootGameObjects())foreach(var child in root.GetComponentsInChildren<UnityEngine.Transform>(true))
{
    if(!UnityEditor.PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))continue;
    var handle=UnityEditor.PrefabUtility.GetPrefabInstanceHandle(child.gameObject);
    ulong id=UnityEditor.GlobalObjectId.GetGlobalObjectIdSlow(handle).targetObjectId;
    if(!seen.Add(id) || !blocks.TryGetValue(id,out var entries))continue;
    var mods=UnityEditor.PrefabUtility.GetPropertyModifications(child.gameObject);
    bool dirty=false;
    foreach(var mod in mods)
    {
        if(mod.target==null || !UnityEditor.AssetDatabase.TryGetGUIDAndLocalFileIdentifier(mod.target,out string guid,out long local))continue;
        if(entries.TryGetValue(local+"|"+guid+"|"+mod.propertyPath,out string value) && mod.value!=value)
        {mod.value=value;dirty=true;changed++;}
    }
    if(dirty)UnityEditor.PrefabUtility.SetPropertyModifications(child.gameObject,mods);
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return new { restoredOverrides=changed, scene=scene.path, dirty=scene.isDirty };
