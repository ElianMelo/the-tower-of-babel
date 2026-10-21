using System.Linq;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Interface;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine.InputSystem;
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
            var tree=UpgradeBoardLayout.TreeSize;
            Assert.That(tree.x,Is.GreaterThan(width)); Assert.That(tree.y,Is.GreaterThan(height));
            var available=new Rect(-tree/2,tree);
            var size=UpgradeBoardLayout.NodeSize;
            var config=AssetDatabase.LoadAssetAtPath<UpgradeTreeConfig>("Assets/_Project/Resources/UpgradeTreeConfig.asset");
            foreach(var board in config.Boards)
            foreach(var data in board.Upgrades)
            {
                var center=UpgradeBoardLayout.TreePosition(data);
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
                var button=visuals.Find(screen=="AnimationEmote" ? "Options/CenterClose" : "Titlebar/CloseButton").GetComponent<UnityEngine.UI.Button>();
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
                Assert.That(go.GetComponent<UnityEngine.UI.Outline>().enabled,Is.True);
                Assert.That(go.GetComponent<UnityEngine.UI.Image>().material.name,Does.Contain("AvailableUpgrade"));
                Assert.That(go.transform.Find("Label").GetComponent<TMP_Text>().color,Is.EqualTo(Color.white));
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("1 POINT"));
                view.SetState(UpgradeButtonState.Purchased); Assert.That(view.IsInteractable,Is.False);
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("OWNED"));
                view.SetState(UpgradeButtonState.CannotBuy); Assert.That(view.IsInteractable,Is.False);
                Assert.That(go.transform.Find("State").GetComponent<TMP_Text>().text,Is.EqualTo("LOCKED"));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LauncherReplacesBrandAndDockAndUsesM()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var hud=prefab.transform.Find("HUD");
            Assert.That(hud.Find("Brand"),Is.Null); Assert.That(hud.Find("ShortcutDock"),Is.Null);
            var launcher=hud.Find("MenuLauncher"); Assert.That(launcher.GetComponent<AeroDock>(),Is.Not.Null);
            Assert.That(launcher.Find("MenuList").gameObject.activeSelf,Is.False);
            var button=launcher.Find("MenusButton").GetComponent<UnityEngine.UI.Button>();
            Assert.That(button.onClick.GetPersistentMethodName(0),Is.EqualTo("Toggle"));
            var input=AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            Assert.That(input.FindAction("Player/Menus",true).bindings.Any(b=>b.path=="<Keyboard>/m"),Is.True);
        }

        [Test]
        public void CompactScreensHaveDifferentLayoutsAndNearbyResourceAmounts()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            var inventory=(RectTransform)prefab.transform.Find("Inventory/Visuals");
            var emotes=(RectTransform)prefab.transform.Find("AnimationEmote/Visuals");
            Assert.That(inventory.sizeDelta.y,Is.LessThan(420)); Assert.That(inventory.anchorMax.x,Is.EqualTo(1));
            Assert.That(emotes.sizeDelta.x,Is.LessThan(550)); Assert.That(emotes.Find("WindowGlass"),Is.Null);
            Assert.That(emotes.Find("Titlebar"),Is.Null);
            Assert.That(emotes.anchoredPosition,Is.EqualTo(Vector2.zero));
            Assert.That(emotes.Find("Options/Hint"),Is.Null);
            var row=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/ResourceInventory.prefab");
            var icon=(RectTransform)row.transform.Find("IconWell"); var amount=(RectTransform)row.transform.Find("Amount");
            Assert.That(amount.anchoredPosition.x-icon.anchoredPosition.x,Is.LessThan(100));
            Assert.That(amount.GetComponent<TMP_Text>().alignment,Is.EqualTo(TextAlignmentOptions.MidlineLeft));
        }

        [Test]
        public void CharacterUsesNewRigForLocalAndRemoteAndEveryDanceUsesJump()
        {
            const string visualPath="Assets/_Project/Prefabs/Player/FrutigirGirl.prefab";
            foreach(string path in new[]{visualPath,"Assets/_Project/Prefabs/Player/Player.prefab","Assets/_Project/Prefabs/Player/RemotePlayer.prefab"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator=prefab.GetComponentInChildren<Animator>(true);
                Assert.That(animator.avatar.isValid,Is.True,path);
                Assert.That(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController),Does.Contain("FrutigirGirl"));
                Assert.That(prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length,Is.EqualTo(19));
                Assert.That(AssetDatabase.GetDependencies(path,true),Does.Not.Contain("Assets/_Project/Animations/PlayerAnimator.controller"));
                foreach(var skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                { Assert.That(skin.sharedMesh.boneWeights.Length,Is.EqualTo(skin.sharedMesh.vertexCount),skin.name); Assert.That(skin.bones.All(b=>b!=null),Is.True); }
            }
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Models/FrutigirGirl/Generated/FrutigirGirl.controller");
            var jump=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Models/FrutigirGirl/Generated/Jump.anim");
            foreach(string dance in new[]{"Dancing","HipHopOne","HipHopTwo","HipHopThree","RumbaDance","SillyDancing"})
                Assert.That(controller.layers[0].stateMachine.states.Single(s=>s.state.name==dance).state.motion,Is.EqualTo(jump),dance);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/PlayerVisuals.prefab"),Is.Null);
        }

        [Test]
        public void CameraFollowerRecognizesFrutigirGirlHead()
        {
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player/FrutigirGirl.prefab"));
            var cameraRoot=new GameObject("CameraFollowerTest");
            try
            {
                var follower=cameraRoot.AddComponent<PlayerFollower>(); follower.SetupPlayer(character.transform);
                typeof(PlayerFollower).GetMethod("LateUpdate",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(follower,null);
                var head=character.GetComponentsInChildren<Transform>().First(t=>t.name=="Bip001 Head");
                Assert.That(Vector3.Distance(cameraRoot.transform.position,head.position+character.transform.TransformVector(Vector3.up*.06f)),Is.LessThan(.001f));
            }
            finally { Object.DestroyImmediate(character); Object.DestroyImmediate(cameraRoot); }
        }
    }
}
