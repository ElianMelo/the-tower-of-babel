using System.Linq;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Interface;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class FrutigerAeroEditModeTests
    {
        private const string PrefabPath="Assets/_Project/UI/FrutigerAero/Prefabs/FrutigerInterface.prefab";

        [TestCase(1348,545)]
        [TestCase(1120,600)]
        [TestCase(1348,905)]
        [TestCase(1980,545)]
        public void EveryUpgradeIncludingCapstoneFitsInsideBoard(float width,float height)
        {
            var available=new Rect(-width/2,-height/2,width,height);
            var size=UpgradeBoardLayout.CellSize(available);
            var config=AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>("Assets/_Project/Resources/UpgradeTreeConfig.asset");
            foreach(var board in config.Boards)
            foreach(var data in board.Upgrades)
            {
                var center=UpgradeBoardLayout.Position(data,available,size);
                var bounds=new Rect(center-size/2,size);
                Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(available.xMin+13),data.Id);
                Assert.That(bounds.xMax,Is.LessThanOrEqualTo(available.xMax-13),data.Id);
                Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(available.yMin+13),data.Id);
                Assert.That(bounds.yMax,Is.LessThanOrEqualTo(available.yMax-13),data.Id);
                Assert.That(size.y,Is.GreaterThan(48));
            }
        }

        [Test]
        public void MainPrefabHasAllScreensAndFunctionalWindowCloseButtons()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.That(prefab,Is.Not.Null);
            Assert.That(prefab.GetComponent<UnityEngine.UI.GraphicRaycaster>(),Is.Not.Null);
            foreach(string screen in new[]{"Inventory","Upgrade","AnimationEmote"})
            {
                var visuals=prefab.transform.Find(screen+"/Visuals");
                Assert.That(visuals,Is.Not.Null,screen);
                Assert.That(visuals.gameObject.activeSelf,Is.False,screen);
                Assert.That(visuals.GetComponent<AeroWindow>(),Is.Not.Null);
                var button=visuals.Find("Titlebar/CloseButton").GetComponent<UnityEngine.UI.Button>();
                Assert.That(button.onClick.GetPersistentEventCount(),Is.EqualTo(1));
                Assert.That(button.onClick.GetPersistentTarget(0),Is.Not.Null);
                Assert.That(button.onClick.GetPersistentMethodName(0),Is.EqualTo("Hide"));
            }
            Assert.That(prefab.GetComponentInChildren<InteractionUI>(true),Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<InformationUI>(true),Is.Not.Null);
            Assert.That(prefab.GetComponentInChildren<ServerStatusUI>(true),Is.Not.Null);
        }

        [Test]
        public void VisualsReusePrefabSourcesAndDoNotBlockButtons()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            foreach(var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.raycastTarget,Is.False,text.name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(text),Is.Not.Null,text.name);
            }
            foreach(var button in prefab.GetComponentsInChildren<UnityEngine.UI.Button>(true))
            {
                Assert.That(button.targetGraphic,Is.Not.Null,button.name);
                Assert.That(button.targetGraphic.raycastTarget,Is.True,button.name);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(button),Is.Not.Null,button.name);
            }
            foreach(var icon in prefab.GetComponentsInChildren<AeroIcon>(true))
                Assert.That(icon.GetComponent<CanvasRenderer>(),Is.Not.Null,icon.name);
        }

        [Test]
        public void InventoryScrollIsClippedAndDataBindingsAreIntact()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var scroll=prefab.GetComponentInChildren<UnityEngine.UI.ScrollRect>(true);
            Assert.That(scroll.content,Is.Not.Null); Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>(),Is.Not.Null);
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic,Is.False);
            Assert.That(scroll.content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit,Is.EqualTo(UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize));
            var inventory=new SerializedObject(prefab.GetComponentInChildren<InventoryUI>(true));
            Assert.That(inventory.FindProperty("resourcePrefab").objectReferenceValue,Is.Not.Null);
            Assert.That(inventory.FindProperty("definitionCollection").objectReferenceValue,Is.Not.Null);
            var upgrade=new SerializedObject(prefab.GetComponentInChildren<UpgradeUI>(true));
            foreach(string field in new[]{"prefab","gatherBoard","processorBoard","builderBoard","gatherButton","processorButton","builderButton","experienceFill"})
                Assert.That(upgrade.FindProperty(field).objectReferenceValue,Is.Not.Null,field);
        }

        [Test]
        public void NodeStatesRemainReadableAndOnlyAvailableNodesCanBeClicked()
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/UpgradeButton.prefab");
            var go=Object.Instantiate(asset);
            try
            {
                var view=go.GetComponent<UpgradeButton>();
                view.SetState(UpgradeButtonState.CanBuy); Assert.That(view.IsInteractable,Is.True);
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("1 POINT"));
                view.SetState(UpgradeButtonState.Purchased); Assert.That(view.IsInteractable,Is.False);
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("OWNED"));
                view.SetState(UpgradeButtonState.CannotBuy); Assert.That(view.IsInteractable,Is.False);
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("LOCKED"));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
