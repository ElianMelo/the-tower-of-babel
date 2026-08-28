using System.Reflection;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ResourceInteractionEditModeTests
    {
        [Test]
        public void ResourceDefinition_CanUseShortTestDurations()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.03f, 4);

            Assert.That(definition.ResourceType, Is.EqualTo(ResourceType.Stone));
            Assert.That(definition.DisplayName, Is.EqualTo("Stone"));
            Assert.That(definition.AmountGathered, Is.EqualTo(4));
            Assert.That(definition.InteractionDuration, Is.EqualTo(0.02f));
            Assert.That(definition.RespawnCooldown, Is.EqualTo(0.03f));

            Object.DestroyImmediate(definition);
        }

        [Test]
        public void PlayerResourceWallet_AccumulatesStone()
        {
            GameObject player = new("Player");
            PlayerResourceWallet wallet = player.AddComponent<PlayerResourceWallet>();

            wallet.Add(ResourceType.Stone, 2);
            wallet.Add(ResourceType.Stone, 3);

            Assert.That(wallet.GetAmount(ResourceType.Stone), Is.EqualTo(5));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void Resource_CancelRestoresVisualPosition()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.03f, 1);
            GameObject root = new("Stone");
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform);
            Resource resource = root.AddComponent<Resource>();
            SetField(resource, "definition", definition);
            SetField(resource, "visuals", visuals);
            Invoke(resource, "Awake");
            Vector3 original = visuals.transform.localPosition;

            resource.BeginInteraction(root);
            visuals.transform.localPosition = Vector3.one;
            resource.CancelInteraction();

            Assert.That(visuals.transform.localPosition, Is.EqualTo(original));
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void InteractionUI_ShowsAndHidesAuthoredFields()
        {
            GameObject root = new("InteractionUI");
            InteractionUI ui = root.AddComponent<InteractionUI>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform);
            TMP_Text objectName = CreateText(visuals.transform, "Object");
            TMP_Text detail = CreateText(visuals.transform, "Detail");
            TMP_Text prompt = CreateText(visuals.transform, "Prompt");
            GameObject progress = new("Progress");
            progress.transform.SetParent(visuals.transform);
            Image fill = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            fill.transform.SetParent(progress.transform);
            TMP_Text percent = CreateText(progress.transform, "Percent");
            SetField(ui, "visuals", visuals);
            SetField(ui, "objectNameText", objectName);
            SetField(ui, "detailText", detail);
            SetField(ui, "promptText", prompt);
            SetField(ui, "progressVisuals", progress);
            SetField(ui, "progressFill", fill);
            SetField(ui, "progressText", percent);

            ui.Show("Stone", "Stone", Color.blue, "Press 'E'");
            ui.SetProgress(0.5f);

            Assert.That(visuals.activeSelf, Is.True);
            Assert.That(detail.text, Is.EqualTo("Stone"));
            Assert.That(detail.color, Is.EqualTo(Color.blue));
            Assert.That(detail.fontStyle, Is.EqualTo(FontStyles.Bold));
            Assert.That(fill.fillAmount, Is.EqualTo(0.5f));
            Assert.That(percent.text, Is.EqualTo("50%"));

            ui.Hide();
            Assert.That(visuals.activeSelf, Is.False);
            Assert.That(progress.activeSelf, Is.False);
            Object.DestroyImmediate(root);
        }

        internal static ResourceDefinition CreateDefinition(float duration, float cooldown, int amount)
        {
            ResourceDefinition definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            SetField(definition, "interactionDuration", duration);
            SetField(definition, "respawnCooldown", cooldown);
            SetField(definition, "amountGathered", amount);
            return definition;
        }

        internal static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Invoke(object target, string method)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
        }

        private static TMP_Text CreateText(Transform parent, string name)
        {
            TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent);
            return text;
        }
    }
}
