#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Interface;
using TowerOfBabel.Networking.Upgrades;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class FrutigerAeroPlayModeTests
    {
        private GameObject root;
        private GameObject player;
        private GameObject serviceObject;
        private CursorStateConfig cursorBefore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            cursorBefore=CursorStateConfig.Current;
            player=new GameObject("AeroTestPlayer");
            player.AddComponent<PlayerControlStateMachine>().SetConnected(true);
            serviceObject=new GameObject("AeroTestUpgradeService");
            serviceObject.SetActive(false);
            var service=serviceObject.AddComponent<NetworkUpgradeService>();
            Set(service,"config",AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>("Assets/_Project/Resources/UpgradeTreeConfig.asset"));
            serviceObject.SetActive(true);
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/UI/FrutigerAero/Prefabs/FrutigerInterface.prefab"));
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(root); Object.Destroy(player); Object.Destroy(serviceObject);
            yield return null;
            cursorBefore.Apply();
        }

        [UnityTest]
        public IEnumerator PanelsSwitchExclusivelyAndCloseRestoresControls()
        {
            var manager=root.GetComponent<InterfaceManager>();
            var inventory=root.GetComponentInChildren<InventoryUI>(true);
            var upgrade=root.GetComponentInChildren<UpgradeUI>(true);
            var emote=root.GetComponentInChildren<AnimationEmoteUI>(true);
            var controls=player.GetComponent<PlayerControlStateMachine>();
            manager.ShowUpgrade(); yield return null;
            Assert.That(upgrade.IsVisible,Is.True);
            Assert.That(controls.BeginInteraction(),Is.False);
            manager.ShowInventory(); yield return null;
            Assert.That(inventory.IsVisible,Is.True); Assert.That(upgrade.IsVisible,Is.False);
            Assert.That(controls.BeginInteraction(),Is.False);
            manager.ToggleEmotes(); yield return null;
            Assert.That(emote.IsVisible,Is.True); Assert.That(inventory.IsVisible,Is.False);
            root.transform.Find("AnimationEmote/Visuals/Options/CenterClose").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(emote.IsVisible,Is.False);
            Assert.That(controls.BeginInteraction(),Is.True);
            controls.EndInteraction();
        }

        [UnityTest]
        public IEnumerator RuntimeBoardsKeepAllFiftyNodesInsideBoundsAndTabsSelectTheCorrectJob()
        {
            var upgrade=root.GetComponentInChildren<UpgradeUI>(true);
            root.GetComponent<InterfaceManager>().ShowUpgrade(); yield return null;
            Canvas.ForceUpdateCanvases(); yield return null;
            foreach(string name in new[]{"GatherBoard","ProcessorBoard","BuilderBoard"})
            {
                var board=(RectTransform)root.transform.Find("Upgrade/Visuals/BoardWell/Viewport/"+name);
                var nodes=board.GetComponentsInChildren<UpgradeButton>(true);
                Assert.That(nodes.Length,Is.EqualTo(50),name);
                foreach(var node in nodes)
                {
                    var rect=(RectTransform)node.transform;
                    Assert.That(Mathf.Abs(rect.anchoredPosition.x)+rect.rect.width/2,Is.LessThanOrEqualTo(board.rect.width/2),node.name);
                    Assert.That(Mathf.Abs(rect.anchoredPosition.y)+rect.rect.height/2,Is.LessThanOrEqualTo(board.rect.height/2),node.name);
                }
            }
            var service=serviceObject.GetComponent<NetworkUpgradeService>();
            var local=(PlayerUpgradeProgress)typeof(NetworkUpgradeService).GetField("localProgress",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service);
            local.Get(UpgradeJob.Gather).SetLevel(3);
            upgrade.ShowGatherBoard();
            var gather=root.transform.Find("Upgrade/Visuals/BoardWell/Viewport/GatherBoard");
            Assert.That(gather.GetComponentsInChildren<UpgradeButton>(false).Length,Is.EqualTo(1));
            Assert.That(gather.Find("Upgrade_3_3").GetComponent<UpgradeButton>().IsInteractable,Is.True);
            Assert.That(gather.Find("Upgrade_0_0").GetComponent<UpgradeButton>().IsInteractable,Is.False);
            Assert.That(gather.Find("Upgrade_0_0").gameObject.activeSelf,Is.False);
            local.Get(UpgradeJob.Gather).TryPurchase(service.Config,"gather_3_3");
            upgrade.ShowGatherBoard();
            Assert.That(gather.Find("Upgrade_3_3").GetComponent<UpgradeButton>().State,Is.EqualTo(UpgradeButtonState.Purchased));
            Assert.That(gather.Find("Upgrade_2_3").GetComponent<UpgradeButton>().IsInteractable,Is.True);
            Assert.That(gather.GetComponentsInChildren<UpgradeButton>(false).Length,Is.EqualTo(5));
            root.transform.Find("Upgrade/Visuals/SkillRail/ProcessorButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(upgrade.SelectedJob,Is.EqualTo(UpgradeJob.Process));
            Assert.That(root.transform.Find("Upgrade/Visuals/BoardWell/Viewport/ProcessorBoard").gameObject.activeSelf,Is.True);
            Assert.That(root.transform.Find("Upgrade/Visuals/BoardWell/Viewport/GatherBoard").gameObject.activeSelf,Is.False);
            root.transform.Find("Upgrade/Visuals/Titlebar/CloseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(upgrade.IsVisible,Is.False);
        }

        [UnityTest]
        public IEnumerator SelectingEachProfessionKeepsTheOriginalBrightBlueGlass()
        {
            var manager=root.GetComponent<InterfaceManager>(); manager.ShowUpgrade(); yield return null;
            var upgrade=root.GetComponentInChildren<UpgradeUI>(true);
            string[] tabs={"GatherButton","ProcessorButton","BuilderButton"};
            for(int selected=0;selected<3;selected++)
            {
                if(selected==0) upgrade.ShowGatherBoard();
                else if(selected==1) upgrade.ShowProcessorBoard();
                else upgrade.ShowBuilderBoard();
                yield return null;
                for(int i=0;i<tabs.Length;i++)
                {
                    var tab=root.transform.Find("Upgrade/Visuals/SkillRail/"+tabs[i]);
                    Assert.That(tab.GetComponent<UnityEngine.UI.Image>().color,Is.EqualTo(Color.white),tabs[i]);
                    Assert.That(tab.GetComponent<UnityEngine.UI.Button>().colors.selectedColor,Is.EqualTo(Color.white),"Selection must not multiply the blue glass by cyan.");
                    Assert.That(tab.GetComponent<UnityEngine.UI.Button>().colors.highlightedColor,Is.EqualTo(Color.white));
                    Assert.That(tab.GetComponent<UnityEngine.UI.Outline>().enabled,Is.EqualTo(i==selected));
                }
            }
            manager.HideUpgrade();
        }

        [UnityTest]
        public IEnumerator MKeyTogglesListAndMenuSelectionTransfersInputLock()
        {
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                var dock=root.GetComponentInChildren<AeroDock>(true);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M)); InputSystem.Update(); yield return null;
                Assert.That(dock.IsExpanded,Is.True);
                Assert.That(player.GetComponent<PlayerControlStateMachine>().BeginInteraction(),Is.False);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.M)); InputSystem.Update(); yield return null;
                Assert.That(dock.IsExpanded,Is.False);
                Assert.That(player.GetComponent<PlayerControlStateMachine>().BeginInteraction(),Is.True);
                player.GetComponent<PlayerControlStateMachine>().EndInteraction();
                InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update();
                dock.Toggle(); dock.Inventory(); yield return null;
                Assert.That(dock.IsExpanded,Is.False);
                Assert.That(root.GetComponentInChildren<InventoryUI>(true).IsVisible,Is.True);
                Assert.That(player.GetComponent<PlayerControlStateMachine>().BeginInteraction(),Is.False);
                root.GetComponent<InterfaceManager>().HideInventory();
            }
            finally { InputSystem.RemoveDevice(keyboard); }
        }

        [UnityTest]
        public IEnumerator GeneratedMenuEntriesKeepFrutigerRegularAndBold()
        {
            var font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/_Project/Fonts/Frutiger SDF.asset");
            var manager=root.GetComponent<InterfaceManager>();
            manager.ShowInventory(); yield return null;
            manager.ShowUpgrade(); yield return null;
            manager.ToggleEmotes(); yield return null;
            var texts=root.GetComponentsInChildren<TMPro.TMP_Text>(true);
            Assert.That(texts.Length,Is.GreaterThan(80));
            foreach(var text in texts) Assert.That(text.font,Is.EqualTo(font),text.name);
            var bold=root.transform.Find("HUD/MenuLauncher/MenusButton/Label").GetComponent<TMPro.TMP_Text>();
            bold.ForceMeshUpdate();
            Assert.That(bold.textInfo.characterCount,Is.GreaterThan(0));
            Assert.That(bold.textInfo.characterInfo[0].fontAsset,Is.EqualTo(font.fontWeightTable[7].regularTypeface));
        }

        [UnityTest]
        public IEnumerator HoverDetailsClampInsideWindowAndDraggingMovesTheLargeTree()
        {
            var manager=root.GetComponent<InterfaceManager>(); manager.ShowUpgrade(); yield return null;
            Canvas.ForceUpdateCanvases();
            var well=root.transform.Find("Upgrade/Visuals/BoardWell");
            var scroll=well.GetComponent<UnityEngine.UI.ScrollRect>();
            var node=scroll.content.Find("Upgrade_3_3").GetComponent<UpgradeButton>();
            var tooltip=root.GetComponentInChildren<UpgradeTooltip>(true);
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
            { position=new Vector2(Screen.width-2,Screen.height-2),button=UnityEngine.EventSystems.PointerEventData.InputButton.Left };
            node.OnPointerEnter(data);
            Assert.That(tooltip.IsVisible,Is.True); Assert.That(tooltip.Description,Does.Contain("Cost: 1 upgrade point"));
            var parent=(RectTransform)tooltip.transform.parent;
            var bounds=RectTransformUtility.CalculateRelativeRectTransformBounds(parent,tooltip.transform);
            Assert.That(bounds.max.x,Is.LessThanOrEqualTo(parent.rect.xMax));
            Assert.That(bounds.min.y,Is.GreaterThanOrEqualTo(parent.rect.yMin));
            var old=scroll.content.anchoredPosition;
            data.position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.position); data.pressPosition=data.position;
            scroll.OnInitializePotentialDrag(data); scroll.OnBeginDrag(data);
            well.GetComponent<UpgradeTreePan>().OnBeginDrag(data);
            data.position+=new Vector2(230,120); scroll.OnDrag(data); scroll.OnEndDrag(data); scroll.StopMovement();
            Assert.That(Vector2.Distance(scroll.content.anchoredPosition,old),Is.GreaterThan(50));
            Assert.That(tooltip.IsVisible,Is.False);
            root.GetComponentInChildren<UpgradeUI>(true).ShowProcessorBoard();
            Assert.That(scroll.content.name,Is.EqualTo("ProcessorBoard"));
            Assert.That(scroll.content.anchoredPosition,Is.EqualTo(Vector2.zero));
        }

        [UnityTest]
        public IEnumerator EmoteCenterTracksScreenCenterAndClosesWithoutATitlebar()
        {
            root.GetComponent<InterfaceManager>().ToggleEmotes(); yield return null;
            var visuals=(RectTransform)root.transform.Find("AnimationEmote/Visuals");
            Assert.That(visuals.Find("Titlebar"),Is.Null);
            var screen=RectTransformUtility.WorldToScreenPoint(null,visuals.position);
            Assert.That(Vector2.Distance(screen,new Vector2(Screen.width*.5f,Screen.height*.5f)),Is.LessThan(2));
            visuals.Find("Options/CenterClose").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(root.GetComponentInChildren<AnimationEmoteUI>(true).IsVisible,Is.False);
        }

        [UnityTest]
        public IEnumerator ClickFeedbackShakesAndSettlesEvenWhileGameTimeIsPaused()
        {
            var option=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/OptionButton.prefab"),root.transform);
            var juice=option.GetComponent<AeroButtonJuice>();
            var rest=option.transform.localRotation; var scale=option.transform.localScale;
            float timeScale=Time.timeScale;
            try
            {
                Time.timeScale=0;
                option.GetComponent<UnityEngine.UI.Button>().onClick.Invoke(); Assert.That(juice.IsPulsing,Is.True);
                float peakAngle=0,peakScale=0,start=Time.realtimeSinceStartup;
                while(juice.IsPulsing && Time.realtimeSinceStartup-start<1)
                {
                    yield return null;
                    peakAngle=Mathf.Max(peakAngle,Quaternion.Angle(option.transform.localRotation,rest));
                    peakScale=Mathf.Max(peakScale,Mathf.Abs(option.transform.localScale.x/scale.x-1));
                }
                Assert.That(peakAngle,Is.GreaterThan(.01f).And.LessThan(1));
                Assert.That(peakScale,Is.LessThan(.02f));
                Assert.That(juice.IsPulsing,Is.False); Assert.That(option.transform.localScale,Is.EqualTo(scale));
                Assert.That(option.transform.localRotation,Is.EqualTo(rest));
            }
            finally { Time.timeScale=timeScale; Object.Destroy(option); }
        }

        [UnityTest]
        public IEnumerator HoverLightFadesAndRespectsDisabledControlsWhilePaused()
        {
            var option=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/OptionButton.prefab"),root.transform);
            var hover=option.GetComponent<AeroHoverFeedback>();
            var button=option.GetComponent<UnityEngine.UI.Button>();
            var data=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current)
                {position=RectTransformUtility.WorldToScreenPoint(null,option.transform.position)};
            float timeScale=Time.timeScale;
            try
            {
                Time.timeScale=0;
                hover.OnPointerEnter(data); yield return new WaitForSecondsRealtime(.17f);
                Assert.That(hover.Strength,Is.GreaterThan(.95f));
                button.interactable=false; yield return new WaitForSecondsRealtime(.22f);
                Assert.That(hover.Strength,Is.Zero);
                button.interactable=true; hover.OnPointerMove(data); yield return new WaitForSecondsRealtime(.17f);
                Assert.That(hover.Strength,Is.GreaterThan(.95f));
                hover.OnPointerExit(data); yield return new WaitForSecondsRealtime(.22f);
                Assert.That(hover.Strength,Is.Zero);
                hover.OnPointerEnter(data); yield return new WaitForSecondsRealtime(.17f);
                option.SetActive(false); Assert.That(hover.Strength,Is.Zero);
            }
            finally { Time.timeScale=timeScale; Object.Destroy(option); }
        }

        [UnityTest]
        public IEnumerator ResourcePromptsUseGeneratedStoneAndThePillarsActualConstructionResource()
        {
            var manager=root.GetComponent<InterfaceManager>();
            var stone=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Resources/Stone.prefab").GetComponent<TowerOfBabel.Resources.Resource>();
            var pillar=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Buildings/Pillar.prefab").GetComponent<TowerOfBabel.Buildings.Building>();
            var image=root.transform.Find("Interaction/Visuals/ResourceSummary/Result/Icon").GetComponent<UnityEngine.UI.Image>();
            manager.ShowInteraction("Stone","Stone",Color.blue,"Press E",stone.InteractionResources);
            Assert.That(image.sprite,Is.EqualTo(stone.Definition.Icon)); Assert.That(image.enabled,Is.True);
            manager.ShowInteraction("Pillar",pillar.DetailText,Color.blue,pillar.PromptText,pillar.InteractionResources);
            // Pillars are built from water blocks; the prompt shows that material's rendered icon and readable name.
            Assert.That(AssetDatabase.GetAssetPath(image.sprite),Does.EndWith("WaterBlock-Icon.png"));
            Assert.That(pillar.DetailText,Does.EndWith("Water Block"));
            manager.ShowInteraction("Other","Other",Color.blue,"Press E");
            Assert.That(image.enabled,Is.False);
            Assert.That(root.transform.Find("Interaction/Visuals/ActionIcon/PickaxeIcon").gameObject.activeSelf,Is.True);
            manager.HideInteraction(); yield return null;
        }

        [UnityTest]
        public IEnumerator PromptUsesActionOnLeftAndGatherRecipeOrConsumptionOnRight()
        {
            var manager=root.GetComponent<InterfaceManager>();
            var ui=root.GetComponentInChildren<InteractionUI>(true);
            var visual=root.transform.Find("Interaction/Visuals");
            var result=visual.Find("ResourceSummary/Result");
            manager.ShowInteraction("Stone","Stone",Color.blue,"Press E",TowerOfBabel.Resources.Interaction.InteractionResourceData.Gather(ResourceType.Stone,4));
            Assert.That(ui.Action,Is.EqualTo(TowerOfBabel.Resources.Interaction.InteractionActionKind.Gather));
            Assert.That(visual.Find("ActionIcon/PickaxeIcon").gameObject.activeSelf,Is.True);
            Assert.That(visual.Find("ResourceSummary/Source").gameObject.activeSelf,Is.False);
            Assert.That(result.Find("Amount").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("4"));
            manager.ShowInteraction("Processor","2 Stone → 3 Processed stone",Color.blue,"Press E",TowerOfBabel.Resources.Interaction.InteractionResourceData.Process(ResourceType.Stone,2,ResourceType.ProcessedStone,3));
            Assert.That(visual.Find("Detail").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("2 Stone to 3 Processed stone"));
            Assert.That(visual.Find("ActionIcon/ProcessIcon").gameObject.activeSelf,Is.True);
            Assert.That(visual.Find("ActionIcon/PickaxeIcon").gameObject.activeSelf,Is.False);
            Assert.That(visual.Find("ResourceSummary/RecipeArrow").gameObject.activeSelf,Is.True);
            Assert.That(visual.Find("ResourceSummary/Source/Amount").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("2"));
            Assert.That(result.Find("Amount").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("3"));
            Assert.That(AssetDatabase.GetAssetPath(result.Find("Icon").GetComponent<UnityEngine.UI.Image>().sprite),Does.EndWith("RefinedStone.png"));
            manager.ShowInteraction("Pillar","Cost",Color.blue,"Press E",TowerOfBabel.Resources.Interaction.InteractionResourceData.Build(ResourceType.ProcessedStone,5));
            Assert.That(visual.Find("ActionIcon/BuildIcon").gameObject.activeSelf,Is.True);
            Assert.That(visual.Find("ActionIcon/ProcessIcon").gameObject.activeSelf,Is.False);
            Assert.That(visual.Find("ResourceSummary/Source").gameObject.activeSelf,Is.False);
            Assert.That(visual.Find("ResourceSummary/RecipeArrow").gameObject.activeSelf,Is.False);
            Assert.That(result.Find("Caption").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("Consume"));
            Assert.That(result.Find("Amount").GetComponent<TMPro.TMP_Text>().text,Is.EqualTo("5"));
            manager.HideInteraction(); yield return null;
        }

        [UnityTest]
        public IEnumerator PromptFloatsGentlyWithoutDriftingWhenRepeatedlyRefreshed()
        {
            var manager=root.GetComponent<InterfaceManager>();
            var rect=(RectTransform)root.transform.Find("Interaction/Visuals"); var rest=rect.anchoredPosition;
            for(int i=0;i<20;i++)
            {
                manager.ShowInteraction("Stone","Stone",Color.blue,"Press E",ResourceType.Stone);
                yield return new WaitForSecondsRealtime(.015f);
            }
            Assert.That(Vector2.Distance(rect.anchoredPosition,rest),Is.GreaterThan(.2f).And.LessThan(4.3f));
            manager.HideInteraction(); Assert.That(rect.anchoredPosition,Is.EqualTo(rest));
        }

        [UnityTest]
        public IEnumerator LiquidClockAndPromptFloatContinueWhileGameTimeIsPaused()
        {
            float previousScale=Time.timeScale;
            var manager=root.GetComponent<InterfaceManager>();
            var rect=(RectTransform)root.transform.Find("Interaction/Visuals");
            try
            {
                Time.timeScale=0;
                manager.ShowInteraction("Stone","Stone",Color.blue,"Press E",ResourceType.Stone);
                yield return new WaitForSecondsRealtime(.08f);
                float first=Shader.GetGlobalFloat("_AeroUiTime"); var position=rect.anchoredPosition;
                yield return new WaitForSecondsRealtime(.2f);
                Assert.That(Shader.GetGlobalFloat("_AeroUiTime")-first,Is.GreaterThan(.12f));
                Assert.That(Vector2.Distance(position,rect.anchoredPosition),Is.GreaterThan(.2f));
                Assert.That(rect.GetComponent<AeroGlassGeometry>().BorderMotion,Is.GreaterThan(.1f));
            }
            finally { Time.timeScale=previousScale; manager.HideInteraction(); }
        }

        [UnityTest]
        public IEnumerator BackgroundHoverTracksTheMouseWithoutFillOrRaycastChanges()
        {
            var mouse=InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            var manager=root.GetComponent<InterfaceManager>(); manager.ShowInventory(); yield return null;
            var panel=root.transform.Find("Inventory/Visuals/WindowGlass");
            var hover=panel.GetComponent<AeroHoverFeedback>(); var image=panel.GetComponent<UnityEngine.UI.Image>();
            var color=image.color; bool raycast=image.raycastTarget;
            try
            {
                InputSystem.QueueStateEvent(mouse,new MouseState{position=RectTransformUtility.WorldToScreenPoint(null,panel.position)}); InputSystem.Update();
                yield return new WaitForSecondsRealtime(.17f);
                Assert.That(hover.Strength,Is.GreaterThan(.95f)); Assert.That(hover.Surface.HoverFill,Is.Zero);
                Assert.That(image.color,Is.EqualTo(color)); Assert.That(image.raycastTarget,Is.EqualTo(raycast));
                InputSystem.QueueStateEvent(mouse,new MouseState{position=new Vector2(-100,-100)}); InputSystem.Update();
                yield return new WaitForSecondsRealtime(.22f); Assert.That(hover.Strength,Is.Zero);
            }
            finally { manager.HideInventory(); InputSystem.RemoveDevice(mouse); }
        }

        private static void Set(object target,string field,object value)
        { target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value); }
    }
}
#endif
