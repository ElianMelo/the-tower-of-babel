#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TowerOfBabel.Rendering;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;

namespace TowerOfBabel.Rendering.Tests
{
    public sealed class TowerRayRuntimeReviewTests
    {
        [Serializable] private sealed class Measurement
        {
            public string preset;public int width=1920,height=1080,samples;
            public double medianCameraGpuMs, medianScatterGpuMs, medianCompositeGpuMs, medianMainShadowGpuMs, medianFillShadowGpuMs, medianCpuFrameMs;
            public bool gpuCameraAvailable, gpuPassesAvailable;
        }
        [Serializable] private sealed class Report
        {
            public string gpu,api,unity,scope="Automated PlayMode isolated 1920x1080 Review camera: completed floor + covered ceiling, production bounded shadow cache, fixed view, three alternating blocks per mode. GPU-collecting CustomSampler commands bracket begin/endCameraRendering; Recorder.gpuElapsedNanoseconds reads that envelope and GPU pass scopes. CPU frame time is separate.";
            public Measurement[] measurements;public double balancedIncrementMs,lowIncrementMs;
            public string gpuBudgetVerdict;
        }
        private static double Median(List<double> values) {if(values.Count==0)return 0;values.Sort();return values[values.Count/2];}

        [UnityTest, Timeout(180000)]
        public IEnumerator CompletedInterior_RecordsMatched1080pDisabledBalancedAndLowGpuCost()
        {
            var previous=SceneManager.GetActiveScene();var scene=SceneManager.CreateScene("Tower ray GPU review");SceneManager.SetActiveScene(scene);
            var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            var feature=renderer.rendererFeatures.OfType<AeroSunShaftRendererFeature>().Single();bool oldActive=feature.isActive;
            var oldSun=RenderSettings.sun;var oldAmbient=RenderSettings.ambientProbe;
            var otherCameras=Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Where(x=>x.enabled).ToArray();foreach(var c in otherCameras)c.enabled=false;
            var otherLights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(x=>x.enabled).ToArray();foreach(var l in otherLights)l.enabled=false;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Tests/Fixtures/TowerRayReview.prefab"));SceneManager.MoveGameObjectToScene(root,scene);
            var fx=root.GetComponent<AeroTowerAtmosphere>();var cycle=root.GetComponent<AeroDayNightCycle>();
            var settings=Object.Instantiate(fx.raySettings);fx.raySettings=settings;
            var day=Object.Instantiate(cycle.profile);day.advanceTime=false;day.startTime=.29f;cycle.profile=day;
            var sockets=root.transform.Find("Opening sockets");var openings=new List<AeroArchOpening>();
            foreach(Transform socket in sockets)openings.Add(new AeroArchOpening(openings.Count,0,socket.position,socket.forward));
            fx.SetConstructionContext(true,openings);
            var camera=root.GetComponentInChildren<Camera>();camera.scene=scene;camera.tag="MainCamera";
            camera.transform.position=new Vector3(85,2.05f,0);camera.transform.LookAt(new Vector3(68,2.1f,28));
            var target=new RenderTexture(1920,1080,24,RenderTextureFormat.ARGBHalf);camera.targetTexture=target;camera.enabled=true;
            var geometry=root.AddComponent<TowerRayReviewInstances>();geometry.Camera=camera;geometry.Initialize();geometry.CastShadows=true;
            bool oldProfiler=Profiler.enabled,oldGpu=ProfilerDriver.IsAreaEnabled(ProfilerArea.GPU),oldProfileGpu=ProfilerDriver.profileGPU;
            ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU,true);ProfilerDriver.profileGPU=true;Profiler.enabled=true;
            var scatter=Recorder.Get("Aero sun shadow scattering");var composite=Recorder.Get("Aero sun depth-aware composite");
            bool oldScatter=scatter.enabled,oldComposite=composite.enabled;scatter.enabled=true;composite.enabled=true;
            var cameraSampler=CustomSampler.Create("Tower review complete camera GPU",true);
            var cameraGpu=cameraSampler.GetRecorder();bool oldCameraGpu=cameraGpu.enabled;cameraGpu.enabled=true;
            var envelope=new CommandBuffer {name="Tower review GPU envelope"};
            Action<ScriptableRenderContext,Camera> begin=(context,view)=>{if(view!=camera)return;envelope.Clear();envelope.BeginSample(cameraSampler);context.ExecuteCommandBuffer(envelope);};
            Action<ScriptableRenderContext,Camera> end=(context,view)=>{if(view!=camera)return;envelope.Clear();envelope.EndSample(cameraSampler);context.ExecuteCommandBuffer(envelope);};
            RenderPipelineManager.beginCameraRendering+=begin;RenderPipelineManager.endCameraRendering+=end;
            var mainShadow=Recorder.Get("Draw Main Light Shadowmap");var fillShadow=Recorder.Get("Draw Additional Lights Shadowmap");
            bool oldMainShadow=mainShadow.enabled,oldFillShadow=fillShadow.enabled;mainShadow.enabled=true;fillShadow.enabled=true;
            var gpu=new[]{new List<double>(),new List<double>(),new List<double>()};var scatterMs=new[]{new List<double>(),new List<double>(),new List<double>()};
            var compositeMs=new[]{new List<double>(),new List<double>(),new List<double>()};var cpu=new[]{new List<double>(),new List<double>(),new List<double>()};
            var mainShadowMs=new[]{new List<double>(),new List<double>(),new List<double>()};var fillShadowMs=new[]{new List<double>(),new List<double>(),new List<double>()};
            try
            {
                // Warm shaders and light/shadow allocations before measuring either state.
                for(int i=0;i<45;i++)yield return null;
                for(int block=0;block<3;block++)for(int mode=0;mode<3;mode++)
                {
                    bool enabled=mode!=0;settings.quality=mode==2?AeroRaySettings.Quality.Low:AeroRaySettings.Quality.BalancedPC;
                    fx.enabled=enabled;feature.SetActive(enabled);
                    geometry.CastShadows=enabled;
                    geometry.Bridge.enabled=enabled;
                    for(float until=Time.realtimeSinceStartup+settings.transitionSeconds*2+.1f;Time.realtimeSinceStartup<until;)yield return null;
                    Assert.That(fx.bounceLights.Count(l=>l.enabled),Is.LessThanOrEqualTo(settings.FillCount));
                    if(enabled)Assert.That(fx.Openings.Select(o=>o.Id).Distinct().Count(),Is.EqualTo(fx.Openings.Count));
                    for(int sample=0;sample<60;sample++)
                    {
                        FrameTimingManager.CaptureFrameTimings();yield return null;
                        if(cameraGpu.gpuSampleBlockCount>0&&cameraGpu.gpuElapsedNanoseconds>0)gpu[mode].Add(cameraGpu.gpuElapsedNanoseconds/1e6);
                        if(enabled && scatter.gpuSampleBlockCount>0 && scatter.gpuElapsedNanoseconds>0)scatterMs[mode].Add(scatter.gpuElapsedNanoseconds/1e6);
                        if(enabled && composite.gpuSampleBlockCount>0 && composite.gpuElapsedNanoseconds>0)compositeMs[mode].Add(composite.gpuElapsedNanoseconds/1e6);
                        if(mainShadow.gpuSampleBlockCount>0)mainShadowMs[mode].Add(mainShadow.gpuElapsedNanoseconds/1e6);
                        if(fillShadow.gpuSampleBlockCount>0)fillShadowMs[mode].Add(fillShadow.gpuElapsedNanoseconds/1e6);
                        cpu[mode].Add(Time.unscaledDeltaTime*1000);
                    }
                }
                var report=new Report {gpu=SystemInfo.graphicsDeviceName,api=SystemInfo.graphicsDeviceType.ToString(),unity=Application.unityVersion,measurements=new Measurement[3]};
                for(int i=0;i<3;i++)report.measurements[i]=new Measurement {preset=new[]{"Disabled","Balanced PC","Low"}[i],samples=cpu[i].Count,medianCameraGpuMs=Median(gpu[i]),medianScatterGpuMs=Median(scatterMs[i]),medianCompositeGpuMs=Median(compositeMs[i]),medianMainShadowGpuMs=Median(mainShadowMs[i]),medianFillShadowGpuMs=Median(fillShadowMs[i]),medianCpuFrameMs=Median(cpu[i]),gpuCameraAvailable=gpu[i].Count>20,gpuPassesAvailable=scatterMs[i].Count>20&&compositeMs[i].Count>20};
                bool valid=report.measurements.All(x=>x.gpuCameraAvailable);
                report.balancedIncrementMs=valid?report.measurements[1].medianCameraGpuMs-report.measurements[0].medianCameraGpuMs:0;
                report.lowIncrementMs=valid?report.measurements[2].medianCameraGpuMs-report.measurements[0].medianCameraGpuMs:0;
                report.gpuBudgetVerdict=valid?(report.balancedIncrementMs<=2?"Within approximately 2 ms in this fixture; full gameplay profiling still depends on scene load.":"Exceeded approximately 2 ms in this fixture."):"Whole-camera GPU timestamps unavailable: the complete 2 ms target is unverified. Available GPU pass durations are measured separately; CPU frame timing is not GPU timing.";
                Directory.CreateDirectory("Documentation/FrutigerAero/Lighting/DynamicRays");File.WriteAllText("Documentation/FrutigerAero/Lighting/DynamicRays/Performance.json",JsonUtility.ToJson(report,true));
                TestContext.WriteLine(JsonUtility.ToJson(report,true));
                Assert.That(cpu.All(x=>x.Count==180),Is.True);
                Assert.That(fx.beams,Is.Null,"The review uses the new volume system, never legacy sheets.");
                var bridge=geometry.Bridge;var chunk=bridge.chunks.CachedChunks.First(c=>c.Key.FloorIndex==0&&c.Assets.Any(a=>a.AssetType==TowerOfBabel.World.Tower.TowerAssetType.Floor));
                int deck=chunk.Assets.First(a=>a.AssetType==TowerOfBabel.World.Tower.TowerAssetType.Floor).LocalIndex;
                bridge.chunks.SetAssetStage(chunk.Key,deck,9);bridge.Refresh(0,0);
                Assert.That(bridge.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(bridge.CachedShadowChunks),"Retired/hidden-floor shadows must disappear before deferred Destroy runs.");
                bridge.enabled=false;Assert.That(bridge.GetComponentsInChildren<MeshRenderer>().Length,Is.Zero,"Disabling the cache must stop all occlusion immediately.");
            }
            finally
            {
                scatter.enabled=oldScatter;composite.enabled=oldComposite;Profiler.enabled=oldProfiler;ProfilerDriver.profileGPU=oldProfileGpu;ProfilerDriver.SetAreaEnabled(ProfilerArea.GPU,oldGpu);
                cameraGpu.enabled=oldCameraGpu;mainShadow.enabled=oldMainShadow;fillShadow.enabled=oldFillShadow;
                RenderPipelineManager.beginCameraRendering-=begin;RenderPipelineManager.endCameraRendering-=end;envelope.Dispose();
                feature.SetActive(oldActive);camera.enabled=false;camera.targetTexture=null;
                Object.Destroy(root);Object.Destroy(target);Object.Destroy(settings);Object.Destroy(day);
                foreach(var c in otherCameras)if(c!=null)c.enabled=true;foreach(var l in otherLights)if(l!=null)l.enabled=true;
                RenderSettings.sun=oldSun;RenderSettings.ambientProbe=oldAmbient;SceneManager.SetActiveScene(previous);
            }
            yield return SceneManager.UnloadSceneAsync(scene);
        }
    }
}
#endif
