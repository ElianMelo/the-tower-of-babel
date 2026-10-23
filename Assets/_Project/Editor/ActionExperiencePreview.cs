using System;
using System.Linq;
using TowerOfBabel.Interface;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Render actual world bubbles and their HUD in a disposable Edit Mode scene.</summary>
public static class ActionExperiencePreview
{
    public static object Capture(bool animationFrames=false,int width=1920,int height=1080)
    {
        if (Application.dataPath.Replace('\\','/')!="D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output="Documentation/FrutigerAero/Previews";
        System.IO.Directory.CreateDirectory(output);
        var original=SceneManager.GetActiveScene(); var selection=Selection.objects;
        var oldActive=RenderTexture.active; bool asynchronous=ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation=false;
        var rendered=new RenderTexture(width,height,24); var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
        var scene=EditorSceneManager.NewPreviewScene();
        GameObject Create(string name,params Type[] types)
        {
            var go=new GameObject(name,types); SceneManager.MoveGameObjectToScene(go,scene); return go;
        }
        try
        {
            var camera=Create("World reward review camera",typeof(Camera)).GetComponent<Camera>();
            camera.scene=scene; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.55f,.79f,.93f);
            camera.targetTexture=rendered; camera.fieldOfView=55; camera.nearClipPlane=.03f; camera.farClipPlane=100;
            camera.transform.position=new Vector3(3.8f,3,-5.7f); camera.transform.LookAt(new Vector3(0,1,0));
            var key=Create("Soft sunlight",typeof(Light)).GetComponent<Light>(); key.type=LightType.Directional; key.intensity=1.3f;
            key.transform.rotation=Quaternion.Euler(40,-35,0); key.shadows=LightShadows.Soft;
            var fillLight=Create("Cool fill",typeof(Light)).GetComponent<Light>(); fillLight.type=LightType.Directional; fillLight.intensity=.45f;
            fillLight.transform.rotation=Quaternion.Euler(35,135,0); fillLight.color=new Color(.8f,.94f,1);
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube); SceneManager.MoveGameObjectToScene(ground,scene);
            ground.transform.position=new Vector3(0,-.05f,0); ground.transform.localScale=new Vector3(35,.1f,35);
            UnityEngine.Object.DestroyImmediate(ground.GetComponent<Collider>());
            ground.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Models/Buildings/FrutigerAero/Review_White.mat");
            var stone=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResourceWorkshopAuthoring.StonePrefab),scene);
            stone.transform.position=new Vector3(-.7f,1.5f,0);
            var processor=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ResourceWorkshopAuthoring.ProcessorPrefab),scene);
            processor.transform.position=new Vector3(1.7f,1.5f,.4f);
            var root=Create("Reward preview",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler));
            var canvas=root.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera; canvas.planeDistance=.1f;
            var scaler=root.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            var interactionObject=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FrutigerAeroBuilder.Root+"/Prefabs/Screens/Interaction.prefab"),root.transform);
            var interaction=interactionObject.GetComponent<InteractionUI>();
            var screen=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ActionExperienceBuilder.ScreenPath),root.transform);
            var ui=screen.GetComponent<ActionExperienceUI>();
            Canvas.ForceUpdateCanvases();
            Vector3 origin=stone.GetComponent<TowerOfBabel.Resources.Resource>().FeedbackPosition;
            var samples=new System.Collections.Generic.List<object>();
            string suffix=width==1920 && height==1080 ? "" : $"-{width}x{height}";
            void Render(string path)
            {
                Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render(); RenderTexture.active=rendered;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0); pixels.Apply(); System.IO.File.WriteAllBytes(path,pixels.EncodeToPNG());
            }
            foreach (var sample in new[]
            {
                new {Name="Float",Time=.55f,PreviousLevel=0,Level=0,Before=5,After=6},
                new {Name="Flight",Time=1.18f,PreviousLevel=0,Level=0,Before=5,After=6},
                new {Name="Arrival",Time=2.3f,PreviousLevel=0,Level=0,Before=5,After=6},
                new {Name="HigherLevelFloat",Time=.55f,PreviousLevel=2,Level=2,Before=5,After=6},
                new {Name="LevelUpFloat",Time=.55f,PreviousLevel=0,Level=1,Before=9,After=0},
                new {Name="LevelUp",Time=2.45f,PreviousLevel=0,Level=1,Before=9,After=0}
            })
            {
                interaction.Show("Stone","Ready to gather",new Color(.14f,.37f,.47f),"Press E to gather",
                    TowerOfBabel.Resources.Interaction.InteractionResourceData.Gather(TowerOfBabel.Resources.ResourceType.Stone,1));
                interaction.HideProgress();
                var reward=new ActionExperienceReward(UpgradeJob.Gather,origin,"Gathered 1 Stone",sample.PreviousLevel,sample.Before,sample.Level,sample.After);
                ui.SampleEditorPreview(reward,sample.Time,camera);
                Render(output+"/Experience-"+sample.Name+suffix+".png");
                var particles=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<MeshRenderer>())
                    .Where(r=>r.name.StartsWith("ExperienceWorldBubble")).ToArray();
                var positions=particles.Select(p=>p.transform.position).ToArray();
                var bar=(RectTransform)screen.transform.Find("ExperienceBar"); var prompt=(RectTransform)interactionObject.transform.Find("Visuals");
                Rect Bounds(RectTransform rect)
                {
                    var corners=new Vector3[4]; rect.GetWorldCorners(corners);
                    var a=root.transform.InverseTransformPoint(corners[0]); var b=root.transform.InverseTransformPoint(corners[2]);
                    return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
                }
                samples.Add(new {sample.Name,activeWorldBubbles=particles.Length,uiBubbles=screen.GetComponentsInChildren<UnityEngine.UI.Image>().Count(i=>i.name.StartsWith("ExperienceBubble")),
                    minDiameter=particles.Length>0?particles.Min(p=>p.transform.localScale.x):0,maxDiameter=particles.Length>0?particles.Max(p=>p.transform.localScale.x):0,
                    depthRange=positions.Length>0?positions.Max(p=>camera.WorldToScreenPoint(p).z)-positions.Min(p=>camera.WorldToScreenPoint(p).z):0,
                    overlap=Bounds(bar).Overlaps(Bounds(prompt)),experience=screen.GetComponentsInChildren<TMPro.TMP_Text>(true).First(t=>t.name=="Experience").text});
            }
            if (animationFrames)
            {
                System.IO.Directory.CreateDirectory("Temp/ExperienceFeedback");
                var reward=new ActionExperienceReward(UpgradeJob.Gather,origin,"Gathered 1 Stone",0,9,1,0);
                for (int frame=0; frame<72; frame++)
                {
                    ui.SampleEditorPreview(reward,frame/24f,camera);
                    Render($"Temp/ExperienceFeedback/frame-{frame:000}.png");
                }
            }
            var report=new {samples,shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/World/Experience Bubble")).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
                scene=original.path,original.isDirty,playing=EditorApplication.isPlaying,compileFailed=EditorUtility.scriptCompilationFailed};
            System.IO.File.WriteAllText("Documentation/FrutigerAero/ExperienceWorldInspection.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));
            return report;
        }
        finally
        {
            RenderTexture.active=oldActive; ShaderUtil.allowAsyncCompilation=asynchronous;
            EditorSceneManager.ClosePreviewScene(scene); Selection.objects=selection;
            UnityEngine.Object.DestroyImmediate(rendered); UnityEngine.Object.DestroyImmediate(pixels);
        }
    }
}
