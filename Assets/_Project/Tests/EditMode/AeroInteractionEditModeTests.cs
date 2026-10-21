using System.Linq;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Interface;
using TowerOfBabel.Upgrades;
using UnityEditor;
using UnityEngine;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class AeroInteractionEditModeTests
    {
        private const string Root="Assets/_Project/UI/FrutigerAero/Prefabs/";
        [Test]
        public void RevisedArtworkWheelAndScrollableTreeAreBoundToReusablePrefabs()
        {
            var ui=AssetDatabase.LoadAssetAtPath<GameObject>(Root+"FrutigerInterface.prefab");
            var inventory=ui.transform.Find("Inventory/Visuals/Titlebar/InventoryIcon").GetComponent<UnityEngine.UI.Image>();
            Assert.That(AssetDatabase.GetAssetPath(inventory.sprite),Does.EndWith("InventorySmall.png"));
            Assert.That(inventory.preserveAspect,Is.True);
            var upgrade=ui.transform.Find("Upgrade/Visuals");
            Assert.That(upgrade.Find("SkillRail/Landscape"),Is.Null);
            Assert.That(upgrade.Find("Titlebar/Title").GetComponent<TMP_Text>().text,Is.EqualTo("Upgrades"));
            Assert.That(upgrade.Find("Titlebar/UpgradesIcon"),Is.Not.Null);
            var upgradeIcon=upgrade.Find("Titlebar/UpgradesIcon").GetComponent<UnityEngine.UI.Image>();
            Assert.That(AssetDatabase.GetAssetPath(upgradeIcon.sprite),Does.EndWith("UpgradesSmall.png"));
            Assert.That(ui.transform.Find("HUD/MenuLauncher/MenuList/Upgrades/UpgradesIcon").GetComponent<UnityEngine.UI.Image>().sprite,Is.EqualTo(upgradeIcon.sprite));
            Assert.That(UpgradeBoardLayout.NodeSize,Is.EqualTo(new Vector2(200,96)));
            var scroll=upgrade.Find("BoardWell").GetComponent<UnityEngine.UI.ScrollRect>();
            Assert.That(scroll.horizontal && scroll.vertical,Is.True);
            Assert.That(scroll.movementType,Is.EqualTo(UnityEngine.UI.ScrollRect.MovementType.Clamped));
            Assert.That(scroll.viewport.GetComponent<UnityEngine.UI.Mask>().showMaskGraphic,Is.False);
            Assert.That(scroll.content.sizeDelta,Is.EqualTo(UpgradeBoardLayout.TreeSize));
            Assert.That(scroll.content.parent,Is.EqualTo(scroll.viewport));
            var tooltip=new SerializedObject(ui.GetComponentInChildren<UpgradeUI>(true)).FindProperty("tooltip").objectReferenceValue;
            Assert.That(tooltip,Is.Not.Null);
            foreach(string name in new[]{"GatherButton","ProcessorButton","BuilderButton"})
            {
                var tab=(RectTransform)upgrade.Find("SkillRail/"+name);
                Assert.That(tab.sizeDelta.y,Is.GreaterThanOrEqualTo(90));
                Assert.That(tab.GetComponentInChildren<TMP_Text>().fontSize,Is.GreaterThanOrEqualTo(28));
            }
            var emote=ui.transform.Find("AnimationEmote/Visuals");
            Assert.That(emote.Find("Titlebar"),Is.Null); Assert.That(emote.Find("Options/Hint"),Is.Null);
            var center=emote.Find("Options/CenterClose").GetComponent<UnityEngine.UI.Button>();
            Assert.That(center.onClick.GetPersistentMethodName(0),Is.EqualTo("Hide"));
            var option=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/OptionButton.prefab");
            Assert.That(new SerializedObject(option.GetComponent<OptionButton>()).FindProperty("hoverEffect").objectReferenceValue,Is.Null);
            var material=option.GetComponent<UnityEngine.UI.Image>().material;
            Assert.That(material.GetColor("_Color").a,Is.EqualTo(1));
            Assert.That(material.GetFloat("_SceneBlur"),Is.Zero);
            Assert.That(ui.transform.Find("HUD/MenuLauncher/MenusButton/Label").GetComponent<TMP_Text>().text,Does.Contain("<color="));
            foreach(var button in ui.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                Assert.That(button.GetComponent<AeroButtonJuice>(),Is.Not.Null,button.name);
            foreach(var control in ui.GetComponentsInChildren<UnityEngine.UI.Selectable>(true))
            {
                Assert.That(control.GetComponent<AeroHoverFeedback>(),Is.Not.Null,control.name);
                Assert.That(control.GetComponent<AeroHoverFeedback>().Surface,Is.Not.Null,control.name);
            }
            foreach(var area in ui.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
                Assert.That(area.GetComponent<AeroHoverFeedback>().Surface,Is.Not.Null,area.name);
        }

        [Test]
        public void ConnectionsAreDrawnOnlyBetweenRevealedAdjacentSkills()
        {
            var board=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"UpgradeBoard.prefab"));
            try
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Interface/UpgradeButton.prefab");
                var a=Object.Instantiate(prefab,board.transform).GetComponent<UpgradeButton>();
                var b=Object.Instantiate(prefab,board.transform).GetComponent<UpgradeButton>();
                a.Configure(new UpgradeData("a","Center",3,3,UpgradeEffectType.Production,1),null,UpgradeButtonState.Purchased);
                b.Configure(new UpgradeData("b","Neighbor",3,4,UpgradeEffectType.Production,1),null,UpgradeButtonState.CanBuy);
                ((RectTransform)a.transform).anchoredPosition=UpgradeBoardLayout.TreePosition(a.Data);
                ((RectTransform)b.transform).anchoredPosition=UpgradeBoardLayout.TreePosition(b.Data);
                var lines=board.GetComponent<AeroTreeConnections>();
                var populate=typeof(AeroTreeConnections).GetMethod("OnPopulateMesh",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);
                using(var mesh=new UnityEngine.UI.VertexHelper())
                {
                    b.gameObject.SetActive(false); populate.Invoke(lines,new object[]{mesh}); Assert.That(mesh.currentVertCount,Is.Zero);
                    b.gameObject.SetActive(true); populate.Invoke(lines,new object[]{mesh}); Assert.That(mesh.currentVertCount,Is.EqualTo(4));
                }
            }
            finally { Object.DestroyImmediate(board); }
        }

        [TestCase(UpgradeEffectType.Efficiency,.25f,"Reduces action time by 0.25 seconds.")]
        [TestCase(UpgradeEffectType.Cost,-3f,"Reduces resource cost by 3.")]
        [TestCase(UpgradeEffectType.Production,4f,"Increases production by 4 per action.")]
        public void TooltipExplainsActualGameplayValuesAndRequirements(UpgradeEffectType effect,float value,string expected)
        {
            var data=new UpgradeData("sample","Skill",3,3,effect,value);
            string body=UpgradeTooltip.Describe(data);
            Assert.That(body,Does.Contain(expected)); Assert.That(body,Does.Contain("1 upgrade point"));
            Assert.That(body,Does.Contain("Starting skill"));
            var capstone=new UpgradeData("capstone","Capstone",6,3,effect,value,true);
            Assert.That(UpgradeTooltip.Describe(capstone),Does.Contain("preceding skill"));
        }

        [Test]
        public void ButtonFeedbackTravelsThroughVertexDataWithoutCloningMaterials()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"GlassButton.prefab"));
            try
            {
                var geometry=go.GetComponent<AeroGlassGeometry>();
                var material=go.GetComponent<UnityEngine.UI.Image>().material;
                using(var vertices=new UnityEngine.UI.VertexHelper())
                {
                    vertices.AddVert(UIVertex.simpleVert);
                    geometry.SetFeedback(.8f,new(.2f,.7f)); geometry.ModifyMesh(vertices);
                    geometry.SetHover(.6f,new(.3f,.4f)); geometry.ModifyMesh(vertices);
                    var vertex=new UIVertex(); vertices.PopulateUIVertex(ref vertex,0);
                    Assert.That(vertex.uv1.z,Is.EqualTo(.8f));
                    Assert.That(vertex.uv2.z,Is.EqualTo(.2f)); Assert.That(vertex.uv2.w,Is.EqualTo(.7f));
                    Assert.That(vertex.uv1.w,Is.EqualTo(.6f));
                    Assert.That(vertex.uv3.x,Is.EqualTo(.3f)); Assert.That(vertex.uv3.y,Is.EqualTo(.4f));
                }
                Assert.That(go.GetComponent<UnityEngine.UI.Image>().material,Is.EqualTo(material));
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
