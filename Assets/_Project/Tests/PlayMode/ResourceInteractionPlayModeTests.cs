using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TowerOfBabel;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ResourceInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResourceCompletion_GrantsStone_HidesAndRespawnsVisuals()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.04f, 3);
            GameObject player = new("Player");
            PlayerResourceWallet wallet = player.AddComponent<PlayerResourceWallet>();
            GameObject root = new("Stone");
            GameObject visuals = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visuals.transform.SetParent(root.transform);
            Resource resource = root.AddComponent<Resource>();
            SetField(resource, "definition", definition);
            SetField(resource, "visuals", visuals);
            yield return null;

            resource.BeginInteraction(player);
            resource.CompleteInteraction(player);

            Assert.That(wallet.GetAmount(ResourceType.Stone), Is.EqualTo(3));
            Assert.That(visuals.activeSelf, Is.False);
            Assert.That(resource.CanInteract, Is.False);

            yield return new WaitForSeconds(0.06f);
            Assert.That(visuals.activeSelf, Is.True);
            Assert.That(resource.CanInteract, Is.True);

            Object.Destroy(root);
            Object.Destroy(player);
            Object.Destroy(definition);
        }

        [UnityTest]
        public IEnumerator Raycaster_StartAndSecondActionCancel_LocksAndUnlocksControls()
        {
            GameObject player = new("Player");
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeControlLock control = player.AddComponent<FakeControlLock>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;

            SetField(raycaster, "controlLocks", new IPlayerControlLock[] { control });
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);

            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True);
            Assert.That(control.IsLocked, Is.True);
            Assert.That(interactable.BeginCount, Is.EqualTo(1));

            raycaster.CancelCurrentInteraction();
            Assert.That(control.IsLocked, Is.False);
            Assert.That(interactable.CancelCount, Is.EqualTo(1));

            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator DisabledResource_CancelsActiveInteraction()
        {
            GameObject player = new("Player");
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeControlLock control = player.AddComponent<FakeControlLock>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
            SetField(raycaster, "controlLocks", new IPlayerControlLock[] { control });
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);
            raycaster.TryBeginCurrentInteraction();

            interactable.enabled = false;
            Invoke(raycaster, "UpdateActiveInteraction");

            Assert.That(control.IsLocked, Is.False);
            Assert.That(interactable.CancelCount, Is.EqualTo(1));
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator InventoryToggle_DoesNotCancelActiveGathering()
        {
            GameObject player = new("Player");
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeControlLock control = player.AddComponent<FakeControlLock>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
            SetField(raycaster, "controlLocks", new IPlayerControlLock[] { control });
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);
            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True);

            GameObject inventoryRoot = new("InventoryUI");
            inventoryRoot.SetActive(false);
            InventoryUI inventory = inventoryRoot.AddComponent<InventoryUI>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(inventoryRoot.transform);
            SetField(inventory, "visuals", visuals);
            inventoryRoot.SetActive(true);
            inventory.Hide();
            inventory.Toggle();

            Assert.That(inventory.IsVisible, Is.True);
            Assert.That(control.IsLocked, Is.True, "Gathering must remain active while inventory is toggled.");
            Assert.That(interactable.CancelCount, Is.Zero);

            raycaster.CancelCurrentInteraction();
            Object.Destroy(inventoryRoot);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator Inventory_BuildsZeroAmountRow_AndUpdatesFromWalletEvent()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.03f, 1);
            ResourceDefinitionCollection collection = ScriptableObject.CreateInstance<ResourceDefinitionCollection>();
            SetField(collection, "definitions", new List<ResourceDefinition> { definition });
            GameObject player = new("Player");
            PlayerResourceWallet wallet = player.AddComponent<PlayerResourceWallet>();

            GameObject rowPrefab = new("ResourceInventory", typeof(RectTransform), typeof(ResourceInventoryDisplay));
            ResourceInventoryDisplay row = rowPrefab.GetComponent<ResourceInventoryDisplay>();
            Image icon = new GameObject("Icon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            TMP_Text name = CreateText(rowPrefab.transform, "Name");
            TMP_Text amount = CreateText(rowPrefab.transform, "Amount");
            SetField(row, "resourceIcon", icon);
            SetField(row, "resourceName", name);
            SetField(row, "resourceAmount", amount);

            GameObject inventoryRoot = new("InventoryUI");
            inventoryRoot.SetActive(false);
            InventoryUI inventory = inventoryRoot.AddComponent<InventoryUI>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(inventoryRoot.transform);
            GameObject list = new("List");
            list.transform.SetParent(visuals.transform);
            SetField(inventory, "visuals", visuals);
            SetField(inventory, "resourcePrefab", rowPrefab);
            SetField(inventory, "targetListResources", list.transform);
            SetField(inventory, "definitionCollection", collection);
            SetField(inventory, "wallet", wallet);
            inventoryRoot.SetActive(true);
            yield return null;

            Assert.That(inventory.TryGetDisplay(ResourceType.Stone, out ResourceInventoryDisplay display), Is.True);
            TMP_Text displayedAmount = (TMP_Text)display.GetType()
                .GetField("resourceAmount", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(display);
            Assert.That(displayedAmount.text, Is.EqualTo("0"));

            wallet.Add(ResourceType.Stone, 4);
            Assert.That(displayedAmount.text, Is.EqualTo("4"));

            Object.Destroy(inventoryRoot);
            Object.Destroy(rowPrefab);
            Object.Destroy(player);
            Object.Destroy(collection);
            Object.Destroy(definition);
        }

        private static ResourceDefinition CreateDefinition(float duration, float cooldown, int amount)
        {
            ResourceDefinition definition = ScriptableObject.CreateInstance<ResourceDefinition>();
            SetField(definition, "interactionDuration", duration);
            SetField(definition, "respawnCooldown", cooldown);
            SetField(definition, "amountGathered", amount);
            return definition;
        }

        private static void SetField(object target, string name, object value)
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

        private sealed class FakeControlLock : MonoBehaviour, IPlayerControlLock
        {
            public bool IsLocked { get; private set; }
            public void SetControlLocked(bool locked) => IsLocked = locked;
        }

        private sealed class FakeInteractable : MonoBehaviour, IInteractable
        {
            public string ObjectName => "Stone";
            public string DetailText => "Stone";
            public Color DetailColor => Color.blue;
            public string PromptText => "Press 'E'";
            public float Duration => 0.02f;
            public bool CanInteract => enabled;
            public int BeginCount { get; private set; }
            public int CancelCount { get; private set; }
            public void BeginInteraction(GameObject interactor) => BeginCount++;
            public void UpdateInteraction(float normalizedProgress) { }
            public void CancelInteraction() => CancelCount++;
            public void CompleteInteraction(GameObject interactor) { }
        }
    }
}
