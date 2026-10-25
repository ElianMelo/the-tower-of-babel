#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEngine;

/// <summary>Manually started Edit Mode camera sweep. No GPU profiling or Play Mode.</summary>
public static class AeroCameraTurnReview
{
    private static IDisposable session;
    private static Camera camera;
    private static AeroBuildingFinish finish;
    private static Action<bool,float,float> render;
    private static Quaternion rotation;
    private static int frame;
    private static string revision;
    private static readonly List<double>[] milliseconds={new(),new()};
    private static readonly List<long>[] allocations={new(),new()};
    public static string Status {get;private set;}="Idle";
    public static string Start(string label)
    {
        if(session!=null)throw new InvalidOperationException("Camera review already running.");
        if(AeroBuildingFinishReview.Status.StartsWith("Benchmark block") || AeroBuildingFinishReview.Status=="Benchmark warming up")
            throw new InvalidOperationException("Finish the other review first.");
        var type=typeof(AeroBuildingFinishReview).GetNestedType("Session",BindingFlags.NonPublic);
        session=(IDisposable)Activator.CreateInstance(type,new object[]{0});
        camera=(Camera)type.GetField("Camera").GetValue(session);
        finish=(AeroBuildingFinish)type.GetField("Finish").GetValue(session);
        render=(Action<bool,float,float>)Delegate.CreateDelegate(typeof(Action<bool,float,float>),session,type.GetMethod("Render"));
        rotation=camera.transform.rotation;frame=0;revision=label;
        foreach(var list in milliseconds)list.Clear();foreach(var list in allocations)list.Clear();
        EditorApplication.update+=Tick;AssemblyReloadEvents.beforeAssemblyReload+=Stop;EditorApplication.quitting+=Stop;
        Status="Turning review starting";return Status;
    }
    private static void Tick()
    {
        try
        {
            int mode=(frame/120)%2;
            camera.transform.rotation=rotation*Quaternion.Euler(0,(frame%120)*3,0);
            finish.liveReflections=mode==1;
            long allocated=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            render(true,10,1f/60);timer.Stop();allocated=GC.GetAllocatedBytesForCurrentThread()-allocated;
            if(frame%120>=10){milliseconds[mode].Add(timer.Elapsed.TotalMilliseconds);allocations[mode].Add(allocated);}
            Status=$"Turning review {(mode==0?"environment":"live")}, sweep {frame/240+1}/2, frame {frame%120+1}/120";
            if(++frame<480)return;
            object Summary(int i)
            {
                var values=milliseconds[i].OrderBy(v=>v).ToArray();var bytes=allocations[i].OrderBy(v=>v).ToArray();
                return new {samples=values.Length,medianMs=values[values.Length/2],p95Ms=values[(int)(values.Length*.95)],maxMs=values.Last(),
                    medianManagedBytes=bytes[bytes.Length/2],maxManagedBytes=bytes.Last()};
            }
            var report=new {revision,gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),
                scope="Manual CPU-only Edit Mode review of completed tower. Two 360-degree sweeps for environment and live reflections, 3 degrees/frame; first 10 frames of each sweep excluded. Synchronous editor render timings, not gameplay FPS.",
                environment=Summary(0),live=Summary(1)};
            string folder="Documentation/FrutigerAero/Lighting/CameraTurn";Directory.CreateDirectory(folder);
            File.WriteAllText($"{folder}/{revision}.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            Stop();Status="Turning review complete";
        }
        catch(Exception e){UnityEngine.Debug.LogException(e);Stop();Status="Turning review failed";}
    }
    public static void Stop()
    {
        EditorApplication.update-=Tick;AssemblyReloadEvents.beforeAssemblyReload-=Stop;EditorApplication.quitting-=Stop;
        session?.Dispose();session=null;
    }
}
#endif
