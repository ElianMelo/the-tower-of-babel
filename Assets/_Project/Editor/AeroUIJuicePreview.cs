using System;
using System.Linq;
using TMPro;
using TowerOfBabel;
using TowerOfBabel.Interface;
using TowerOfBabel.Resources;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Deterministic visual samples in an isolated scene, without Play Mode or gameplay callbacks.</summary>
public static class AeroUIJuicePreview
{
    public static object Capture(bool frames = true)
    {
        if (Application.dataPath.Replace('\\','/') != "D:/PersonalProjects/the-tower-of-babel/Assets" || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Use this project in Edit Mode.");
        const string output = "Documentation/FrutigerAero/Previews";
        System.IO.Directory.CreateDirectory(output); System.IO.Directory.CreateDirectory("Temp/UIJuice");
        var originalScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var selection = Selection.objects;
        var main = Camera.main; var oldTarget = main.targetTexture; var oldActive = RenderTexture.active;
        var world = new RenderTexture(1600,900,24); var rendered = new RenderTexture(1600,900,24);
        var pixels = new Texture2D(1600,900,TextureFormat.RGB24,false);
        var scene = EditorSceneManager.NewPreviewScene();
        bool asynchronous = ShaderUtil.allowAsyncCompilation; ShaderUtil.allowAsyncCompilation = false;
        try
        {
            main.targetTexture = world; main.Render(); main.targetTexture = oldTarget;
            var cameraObject = new GameObject("UI motion preview camera",typeof(Camera));
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
            var camera = cameraObject.GetComponent<Camera>(); camera.scene = scene; camera.targetTexture = rendered;
            camera.orthographic = true; camera.orthographicSize = 450; camera.nearClipPlane = .01f; camera.farClipPlane = 100;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.2f,.4f,.6f);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FrutigerAeroBuilder.InterfacePath),scene);
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var background = new GameObject("World backdrop",typeof(RectTransform),typeof(RawImage)); background.transform.SetParent(root.transform,false); background.transform.SetAsFirstSibling();
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one; backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            background.GetComponent<RawImage>().texture = world; background.GetComponent<RawImage>().raycastTarget = false;
            foreach(string screen in new[]{"Inventory","Upgrade","AnimationEmote","Interaction","ServerStatus","Information"}) root.transform.Find(screen+"/Visuals").gameObject.SetActive(false);
            root.transform.Find("HUD/MenuLauncher/MenuList").gameObject.SetActive(false);
            var inventory = root.transform.Find("Inventory/Visuals"); var upgrade = root.transform.Find("Upgrade/Visuals");
            var emotes = root.transform.Find("AnimationEmote/Visuals");
            var definitions = AssetDatabase.LoadAssetAtPath<ResourceDefinitionCollection>("Assets/_Project/Resources/Definitions/ResourceDefinitions.asset");
            foreach(var definition in definitions.Definitions.Where(d=>d!=null))
                Spawn("ResourceInventory",inventory.Find("ResourceScroll/Viewport/Resources")).GetComponent<ResourceInventoryDisplay>().Bind(definition,24);
            var config = AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>("Assets/_Project/Resources/UpgradeTreeConfig.asset");
            var board = (RectTransform)upgrade.Find("BoardWell/Viewport/GatherBoard");
            UpgradeButton center = null;
            foreach(var data in config.GetUpgrades(UpgradeJob.Gather).Where(d=>!d.IsLevelFiftyUpgrade && Mathf.Abs(d.Row-3)+Mathf.Abs(d.Column-3)<=1))
            {
                var go = Spawn("UpgradeButton",board); var view = go.GetComponent<UpgradeButton>();
                view.Configure(data,null,UpgradeButtonState.CanBuy);
                var rect = (RectTransform)go.transform; rect.anchorMin = rect.anchorMax = rect.pivot = new(.5f,.5f);
                rect.sizeDelta = UpgradeBoardLayout.NodeSize; rect.anchoredPosition = UpgradeBoardLayout.TreePosition(data);
                if(data.Row==3 && data.Column==3) center = view;
            }
            board.GetComponent<AeroTreeConnections>()?.Refresh();
            string[] labels = {"Dance","Hip Hop 1","Hip Hop 2","Hip Hop 3","Rumba","Silly Dance"};
            for(int i=0;i<labels.Length;i++)
            {
                var option = Spawn("OptionButton",emotes.Find("Options")); option.GetComponent<OptionButton>().Configure(labels[i],null);
                float radians = (90-i*60)*Mathf.Deg2Rad;
                ((RectTransform)option.transform).anchoredPosition = new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*130;
            }
            var value = inventory.GetComponentsInChildren<AeroValueFeedback>(true).First();
            var tab = upgrade.Find("GatherButton") ?? upgrade.GetComponentsInChildren<Button>(true).First(b=>b.name=="GatherButton").transform;
            var icon = tab.GetComponent<AeroButtonJuice>();
            Vector3 buttonScale = tab.localScale; Quaternion buttonRotation = tab.localRotation; Vector2 buttonPosition = ((RectTransform)tab).anchoredPosition;
            Canvas.ForceUpdateCanvases();
            int frameCount = frames ? 96 : 1;
            for(int frame=0;frame<frameCount;frame++)
            {
                float time = frames ? frame/24f : 1.75f;
                bool showUpgrade = time < 1.5f, showInventory = time>=1.5f && time<3, showEmotes = time>=3;
                upgrade.gameObject.SetActive(showUpgrade); inventory.gameObject.SetActive(showInventory); emotes.gameObject.SetActive(showEmotes);
                float phase = showUpgrade ? time : showInventory ? time-1.5f : time-3;
                var active = showUpgrade ? upgrade : showInventory ? inventory : emotes;
                foreach(var motion in active.GetComponentsInChildren<AeroPanelMotion>(true))
                {
                    float stagger = new SerializedObject(motion).FindProperty("siblingStagger").floatValue;
                    float reveal = Mathf.Clamp01((phase-Mathf.Min(6,motion.transform.GetSiblingIndex())*stagger)/.24f);
                    float dismiss = Mathf.Clamp01(((showEmotes ? 1 : 1.5f)-phase)/.14f);
                    motion.SampleEditorReveal(Mathf.Min(reveal,dismiss));
                }
                if(showUpgrade)
                {
                    tab.GetComponent<AeroHoverFeedback>().SampleEditorHover(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.2f,.6f,phase)),new(.4f,.6f));
                    icon.SampleEditorPulse(phase>=.65f ? phase-.65f : 1,phase);
                    center.GetComponent<AeroGlassGeometry>().SetFeedback(phase>.9f ? Mathf.Clamp01(1-(phase-.9f)/.4f) : 0,new(.5f,.5f));
                    center.SetState(phase>.9f ? UpgradeButtonState.Purchased : UpgradeButtonState.CanBuy);
                }
                if(showInventory) value.SampleEditorChange(24,39,Mathf.Max(0,phase-.32f));
                if(showEmotes)
                {
                    var first = emotes.GetComponentsInChildren<OptionButton>(true).First();
                    first.GetComponent<AeroGlassGeometry>().SetHover(Mathf.Clamp01(phase*2),new(.5f,.5f));
                }
                Shader.SetGlobalFloat("_AeroUiTime",time);
                Canvas.ForceUpdateCanvases(); camera.Render(); camera.Render();
                RenderTexture.active = rendered; pixels.ReadPixels(new Rect(0,0,1600,900),0,0); pixels.Apply();
                if(frames) System.IO.File.WriteAllBytes($"Temp/UIJuice/frame-{frame:000}.png",pixels.EncodeToPNG());
                if(frame==26 || !frames) System.IO.File.WriteAllBytes(output+"/UI-Feedback-Upgrades.png",pixels.EncodeToPNG());
                if(frame==49) System.IO.File.WriteAllBytes(output+"/UI-Feedback-Inventory.png",pixels.EncodeToPNG());
                if(frame==86) System.IO.File.WriteAllBytes(output+"/UI-Feedback-Emotes.png",pixels.EncodeToPNG());
            }
            return new { sourcePrefab=FrutigerAeroBuilder.InterfacePath, panels=root.GetComponentsInChildren<AeroPanelMotion>(true).Length,
                buttons=root.GetComponentsInChildren<AeroButtonJuice>(true).Length,
                fixedButtonTransform=tab.localScale==buttonScale && tab.localRotation==buttonRotation && ((RectTransform)tab).anchoredPosition==buttonPosition,
                inventoryAmount=value.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="Amount").text,
                missingScripts=root.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)),
                shaderMessages=ShaderUtil.GetShaderMessages(Shader.Find("TowerOfBabel/UI/Liquid Glass")).Select(m=>new {m.message,severity=m.severity.ToString()}).ToArray(),
                originalScene.isDirty, playing=EditorApplication.isPlaying };
        }
        finally
        {
            main.targetTexture = oldTarget; RenderTexture.active = oldActive; ShaderUtil.allowAsyncCompilation = asynchronous;
            EditorSceneManager.ClosePreviewScene(scene); Selection.objects = selection;
            foreach(var item in new UnityEngine.Object[]{world,rendered,pixels}) UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static GameObject Spawn(string name,Transform parent)
        => (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/"+name+".prefab"),parent);
}
