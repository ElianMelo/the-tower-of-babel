using System.Collections;
using System.Reflection;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TowerOfBabel;
using TowerOfBabel.Networking;
using TowerOfBabel.Networking.Resources;
using FishNet.Managing;
using FishNet.Managing.Object;

namespace TowerOfBabel.Resources.Tests
{
    public sealed class ResourceInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator ResourceCompletion_DoesNotPredictWalletOrCooldown()
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

            Assert.That(wallet.GetAmount(ResourceType.Stone), Is.Zero);
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
            PlayerControlStateMachine stateMachine = CreateConnectedStateMachine(player);
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;

            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);

            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Gathering));
            Assert.That(interactable.BeginCount, Is.EqualTo(1));

            raycaster.CancelCurrentInteraction();
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Moving));
            Assert.That(interactable.CancelCount, Is.EqualTo(1));

            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator DisabledResource_CancelsActiveInteraction()
        {
            GameObject player = new("Player");
            PlayerControlStateMachine stateMachine = CreateConnectedStateMachine(player);
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);
            raycaster.TryBeginCurrentInteraction();

            interactable.enabled = false;
            Invoke(raycaster, "UpdateActiveInteraction");

            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Moving));
            Assert.That(interactable.CancelCount, Is.EqualTo(1));
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator ClientCancel_SendsServerCancelRequest()
        {
            GameObject player = new("Player");
            CreateConnectedStateMachine(player);
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);

            Assert.That(raycaster.TryBeginCurrentInteraction(), Is.True);
            raycaster.CancelCurrentInteraction();

            Assert.That(interactable.ServerCancelRequestCount, Is.EqualTo(1));
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator ServerRejection_CancelsLocallyWithoutCancelEcho()
        {
            GameObject player = new("Player");
            PlayerControlStateMachine stateMachine = CreateConnectedStateMachine(player);
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
            SetField(raycaster, "currentInteractable", interactable);
            SetField(raycaster, "currentInteractableBehaviour", interactable);
            raycaster.TryBeginCurrentInteraction();

            interactable.RejectFromServer();

            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Moving));
            Assert.That(interactable.CancelCount, Is.EqualTo(1));
            Assert.That(interactable.ServerCancelRequestCount, Is.Zero);
            Object.Destroy(player);
        }

        [UnityTest]
        public IEnumerator AuthoritativeCooldown_DisablesAndRestoresResource()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.03f, 1);
            GameObject root = new("Stone");
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform);
            Resource resource = root.AddComponent<Resource>();
            SetField(resource, "definition", definition);
            SetField(resource, "visuals", visuals);
            yield return null;

            resource.BeginAuthoritativeCooldown(0.03f);
            Assert.That(resource.ServerCanGather, Is.False);
            Assert.That(visuals.activeSelf, Is.False);
            yield return new WaitForSeconds(0.05f);
            Assert.That(resource.ServerCanGather, Is.True);
            Assert.That(visuals.activeSelf, Is.True);

            Object.Destroy(root);
            Object.Destroy(definition);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void GatherRejection_ReleasesLocalGatherWithOrWithoutListener(bool hasListener)
        {
            GameObject serviceRoot = new("Resource Service");
            GameObject resourceRoot = new("Stone");
            try
            {
                NetworkResourceService service = serviceRoot.AddComponent<NetworkResourceService>();
                Resource resource = resourceRoot.AddComponent<Resource>();
                SetField(resource, "nodeId", 1UL);
                SetField(service, "localActiveResource", resource);
                FieldInfo activeResource = typeof(NetworkResourceService).GetField(
                    "localActiveResource", BindingFlags.Instance | BindingFlags.NonPublic);
                int rejectionCount = 0;
                if (hasListener)
                {
                    resource.ServerRejected += () =>
                    {
                        rejectionCount++;
                        Assert.That(activeResource.GetValue(service), Is.Null,
                            "Release the gather before notifying the interaction controller.");
                    };
                }

                Invoke(service, "HandleGatherRejected", 2UL);
                Assert.That(activeResource.GetValue(service), Is.SameAs(resource),
                    "A rejection for another node must not cancel this gather.");
                Assert.That(rejectionCount, Is.Zero);

                Invoke(service, "HandleGatherRejected", 1UL);
                Assert.That(activeResource.GetValue(service), Is.Null,
                    "A rejected gather must not block subsequent gathering attempts.");
                Assert.That(rejectionCount, Is.EqualTo(hasListener ? 1 : 0));

                Invoke(service, "HandleGatherRejected", 1UL);
                Assert.That(rejectionCount, Is.EqualTo(hasListener ? 1 : 0));
            }
            finally
            {
                Object.DestroyImmediate(resourceRoot);
                Object.DestroyImmediate(serviceRoot);
            }
        }

        [UnityTest]
        public IEnumerator PlayerControlStateMachine_DisconnectInterruptsGatheringAndRelock()
        {
            GameObject player = new("Player");
            PlayerControlStateMachine stateMachine = player.AddComponent<PlayerControlStateMachine>();
            bool interrupted = false;
            stateMachine.GatheringInterrupted += () => interrupted = true;

            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Locked));
            stateMachine.SetConnected(true);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Moving));
            Assert.That(stateMachine.BeginGathering(), Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Gathering));

            stateMachine.SetConnected(false);
            Assert.That(interrupted, Is.True);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Locked));
            stateMachine.SetConnected(true);
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Moving));

            Object.Destroy(player);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InventoryToggle_DoesNotCancelActiveGathering()
        {
            GameObject player = new("Player");
            PlayerControlStateMachine stateMachine = CreateConnectedStateMachine(player);
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            FakeInteractable interactable = player.AddComponent<FakeInteractable>();
            yield return null;
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
            Assert.That(stateMachine.CurrentState, Is.EqualTo(PlayerControlState.Gathering), "Gathering must remain active while inventory is toggled.");
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

        [UnityTest]
        public IEnumerator NetworkBootstrap_StartHost_StartsServerAndClient_AndSecondCallDoesNothing()
        {
            NetworkManager manager = Object.FindFirstObjectByType<NetworkManager>(FindObjectsInactive.Include);
            GameObject root = manager != null ? manager.gameObject : new GameObject("NetworkSystem");
            if (manager == null)
            {
                root.SetActive(false);
                LogAssert.Expect(LogType.Error, new Regex("SpawnablePrefabs is null.*"));
                manager = root.AddComponent<NetworkManager>();
                SetField(manager, "_spawnablePrefabs", ScriptableObject.CreateInstance<DefaultPrefabObjects>());
                root.SetActive(true);
            }
            NetworkBootstrap bootstrap = root.GetComponent<NetworkBootstrap>();
            if (bootstrap == null)
                bootstrap = root.AddComponent<NetworkBootstrap>();
            SetField(bootstrap, "networkManager", manager);

            Assert.That(bootstrap.StartHost(), Is.True);
            yield return new WaitUntil(() => manager.ServerManager.Started && manager.ClientManager.Started);
            Assert.That(bootstrap.StartHost(), Is.False);

            manager.ClientManager.StopConnection();
            manager.ServerManager.StopConnection(true);
            yield return null;
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

        private static void Invoke(object target, string method, params object[] arguments)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        }

        private static TMP_Text CreateText(Transform parent, string name)
        {
            TextMeshProUGUI text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI))
                .GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent);
            return text;
        }

        private static PlayerControlStateMachine CreateConnectedStateMachine(GameObject player)
        {
            PlayerControlStateMachine stateMachine = player.AddComponent<PlayerControlStateMachine>();
            stateMachine.SetConnected(true);
            return stateMachine;
        }

        private sealed class FakeInteractable : MonoBehaviour, IInteractable, IServerAuthoritativeInteractable
        {
            public string ObjectName => "Stone";
            public string DetailText => "Stone";
            public Color DetailColor => Color.blue;
            public string PromptText => "Press 'E'";
            public float Duration => 0.02f;
            public bool CanInteract => enabled;
            public int BeginCount { get; private set; }
            public int CancelCount { get; private set; }
            public int ServerCancelRequestCount { get; private set; }
            public event System.Action ServerRejected;
            public void BeginInteraction(GameObject interactor) => BeginCount++;
            public void UpdateInteraction(float normalizedProgress) { }
            public void CancelInteraction() => CancelCount++;
            public void CompleteInteraction(GameObject interactor) { }
            public bool RequestServerStart(GameObject interactor) => true;
            public void RequestServerCancel() => ServerCancelRequestCount++;
            public void RejectFromServer() => ServerRejected?.Invoke();
        }
    }
}
