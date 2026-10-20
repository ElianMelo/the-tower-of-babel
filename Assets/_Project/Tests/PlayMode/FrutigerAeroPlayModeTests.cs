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
            root.transform.Find("AnimationEmote/Visuals/Titlebar/CloseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
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
                var board=(RectTransform)root.transform.Find("Upgrade/Visuals/BoardWell/"+name);
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
            var gather=root.transform.Find("Upgrade/Visuals/BoardWell/GatherBoard");
            Assert.That(gather.Find("Upgrade_3_3").GetComponent<UpgradeButton>().IsInteractable,Is.True);
            Assert.That(gather.Find("Upgrade_0_0").GetComponent<UpgradeButton>().IsInteractable,Is.False);
            local.Get(UpgradeJob.Gather).TryPurchase(service.Config,"gather_3_3");
            upgrade.ShowGatherBoard();
            Assert.That(gather.Find("Upgrade_3_3").GetComponent<UpgradeButton>().State,Is.EqualTo(UpgradeButtonState.Purchased));
            Assert.That(gather.Find("Upgrade_2_3").GetComponent<UpgradeButton>().IsInteractable,Is.True);
            root.transform.Find("Upgrade/Visuals/SkillRail/ProcessorButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(upgrade.SelectedJob,Is.EqualTo(UpgradeJob.Process));
            Assert.That(root.transform.Find("Upgrade/Visuals/BoardWell/ProcessorBoard").gameObject.activeSelf,Is.True);
            Assert.That(root.transform.Find("Upgrade/Visuals/BoardWell/GatherBoard").gameObject.activeSelf,Is.False);
            root.transform.Find("Upgrade/Visuals/Titlebar/CloseButton").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
            Assert.That(upgrade.IsVisible,Is.False);
        }

        private static void Set(object target,string field,object value)
        { target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value); }
    }
}
#endif
