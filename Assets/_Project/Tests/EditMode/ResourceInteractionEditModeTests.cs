using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Resources.Interaction;
using TowerOfBabel.Buildings;
using TowerOfBabel.World.Chunks;
using UnityEngine;
using UnityEngine.UI;
using TowerOfBabel.Networking.Resources;
using UnityEditor;

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
        public void Building_ExposesElevenStagesAndAppliesConfiguredMesh()
        {
            GameObject root = new("Pillar");
            MeshFilter filter = root.AddComponent<MeshFilter>();
            Mesh stageZero = new();
            Mesh stageFive = new();
            Mesh stageTen = new();
            Mesh[] meshes = new Mesh[Building.StageCount];
            meshes[0] = stageZero;
            meshes[5] = stageFive;
            meshes[10] = stageTen;
            Building building = root.AddComponent<Building>();
            SetField(building, "meshFilter", filter);
            SetField(building, "stageMeshes", meshes);
            int presentationChanges = 0;
            building.InteractionPresentationChanged += () => presentationChanges++;

            building.ApplyStage(5);
            Assert.That(Building.StageCount, Is.EqualTo(11));
            Assert.That(building.CurrentStage, Is.EqualTo(5));
            Assert.That(filter.sharedMesh, Is.SameAs(stageFive));
            Assert.That(building.ShouldShowInteraction, Is.True);

            building.ApplyStage(99);
            Assert.That(building.CurrentStage, Is.EqualTo(ChunkAssetData.CompletedStage));
            Assert.That(filter.sharedMesh, Is.SameAs(stageTen));
            Assert.That(building.ShouldShowInteraction, Is.False);
            Assert.That(presentationChanges, Is.EqualTo(2));

            Object.DestroyImmediate(root);
            Object.DestroyImmediate(stageZero);
            Object.DestroyImmediate(stageFive);
            Object.DestroyImmediate(stageTen);
        }

        [Test]
        public void TowerOutline_UsesSharedLineworkSettingsAndRestoresRenderingLayer()
        {
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(
                "Assets/_Project/Scripts/Buildings/OutlineSettings.asset");
            Assert.That(settings, Is.Not.Null);

            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Renderer renderer = root.GetComponent<Renderer>();
            renderer.renderingLayerMask = 1u;
            TowerOfBabel.Outline outline = root.AddComponent<TowerOfBabel.Outline>();
            SetField(outline, "settings", settings);

            outline.SetVisible(true);
            Assert.That(outline.IsVisible, Is.True);
            Assert.That(renderer.renderingLayerMask, Is.EqualTo(3u));

            outline.SetVisible(false);
            Assert.That(outline.IsVisible, Is.False);
            Assert.That(renderer.renderingLayerMask, Is.EqualTo(1u));

            Object.DestroyImmediate(root);
        }

        [TestCase("Assets/_Project/Prefabs/Buildings/Arch.prefab")]
        [TestCase("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab")]
        [TestCase("Assets/_Project/Prefabs/Buildings/Pillar.prefab")]
        [TestCase("Assets/_Project/Prefabs/Buildings/Step_Tile.prefab")]
        public void TowerPrefab_HasWorkingSharedOutline(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject instance = Object.Instantiate(prefab);
            TowerOfBabel.Outline outline = instance.GetComponent<TowerOfBabel.Outline>();
            Assert.That(outline, Is.Not.Null);

            outline.SetVisible(true);
            Assert.That(outline.IsVisible, Is.True);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.renderingLayerMask & 2u, Is.EqualTo(2u));

            outline.SetVisible(false);
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                Assert.That(renderer.renderingLayerMask & 2u, Is.Zero);

            Object.DestroyImmediate(instance);
        }

        [TestCase("Assets/_Project/Prefabs/Buildings/Arch.prefab")]
        [TestCase("Assets/_Project/Prefabs/Buildings/Floor_Tile.prefab")]
        [TestCase("Assets/_Project/Prefabs/Buildings/Step_Tile.prefab")]
        public void TowerPrefab_ColliderResolvesBuildingAndWinsAgainstGround(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null);

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            GameObject instance = Object.Instantiate(prefab);
            GameObject player = new("Test Player");
            try
            {
                instance.transform.position = new Vector3(0f, 1000f, 0f);
                ground.name = "Test Ground";
                ground.transform.position = new Vector3(0f, 999.9f, 0f);
                ground.transform.localScale = new Vector3(20f, 0.2f, 20f);

                Collider interactionCollider = instance.GetComponentInChildren<Collider>(true);
                Assert.That(interactionCollider, Is.Not.Null);
                Assert.That(interactionCollider.enabled, Is.True);
                Assert.That(interactionCollider.isTrigger, Is.False);
                Assert.That(interactionCollider.GetComponentInParent<Building>(), Is.SameAs(instance.GetComponent<Building>()));

                Physics.SyncTransforms();
                Vector3 origin = interactionCollider.bounds.center + Vector3.up * 10f;
                Assert.That(Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20f, ~0,
                    QueryTriggerInteraction.Ignore), Is.True);
                Assert.That(hit.collider, Is.SameAs(interactionCollider),
                    "The interaction collider must sit above coplanar ground so the player raycast can focus the building.");

                PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
                Invoke(raycaster, "SetTarget", hit.collider.gameObject);
                Assert.That(raycaster.CurrentTarget, Is.SameAs(interactionCollider.gameObject));
                foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
                    Assert.That(renderer.renderingLayerMask & 2u, Is.EqualTo(2u));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(ground);
            }
        }

        [Test]
        public void Raycaster_RefreshesFocusedPresentationWithoutChangingTarget()
        {
            GameObject player = new("Player");
            PlayerInteractionRaycaster raycaster = player.AddComponent<PlayerInteractionRaycaster>();
            GameObject target = new("Tower Asset");
            FakePresentationInteractable interactable = target.AddComponent<FakePresentationInteractable>();

            Invoke(raycaster, "SetTarget", target);
            Assert.That(interactable.IsFocused, Is.True);

            SetField(raycaster, "activeInteraction", interactable);
            SetField(raycaster, "activeInteractionBehaviour", interactable);
            Invoke(raycaster, "CompleteInteraction");
            Assert.That(raycaster.CurrentTarget, Is.SameAs(target));
            Assert.That(interactable.IsFocused, Is.True);

            interactable.SetShouldShow(false);
            Assert.That(interactable.IsFocused, Is.False);
            Assert.That(raycaster.CurrentTarget, Is.SameAs(target));

            Object.DestroyImmediate(player);
            Object.DestroyImmediate(target);
        }

        [Test]
        public void ResourceDefinitionCollection_ExposesConfiguredDefinitions()
        {
            ResourceDefinition definition = CreateDefinition(0.02f, 0.03f, 1);
            ResourceDefinitionCollection collection = ScriptableObject.CreateInstance<ResourceDefinitionCollection>();
            SetField(collection, "definitions", new List<ResourceDefinition> { definition });

            Assert.That(collection.Definitions, Has.Count.EqualTo(1));
            Assert.That(collection.Definitions[0], Is.SameAs(definition));

            Object.DestroyImmediate(collection);
            Object.DestroyImmediate(definition);
        }

        [Test]
        public void PlayerResourceWallet_RaisesChangedAmount()
        {
            GameObject player = new("Player");
            PlayerResourceWallet wallet = player.AddComponent<PlayerResourceWallet>();
            ResourceType changedType = default;
            int changedAmount = -1;
            wallet.ResourceAmountChanged += (type, amount) =>
            {
                changedType = type;
                changedAmount = amount;
            };

            wallet.Add(ResourceType.Stone, 2);

            Assert.That(changedType, Is.EqualTo(ResourceType.Stone));
            Assert.That(changedAmount, Is.EqualTo(2));
            Object.DestroyImmediate(player);
        }

        [Test]
        public void ServerPlayerResourceStore_EnforcesPerResourceCapacity()
        {
            ServerPlayerResourceStore store = new(50);

            Assert.That(store.TryAdd(7, ResourceType.Stone, 49, out int firstAmount), Is.True);
            Assert.That(firstAmount, Is.EqualTo(49));
            Assert.That(store.TryAdd(7, ResourceType.Stone, 3, out int cappedAmount), Is.True);
            Assert.That(cappedAmount, Is.EqualTo(50));
            Assert.That(store.TryAdd(7, ResourceType.Stone, 1, out int rejectedAmount), Is.False);
            Assert.That(rejectedAmount, Is.EqualTo(50));
        }

        [Test]
        public void ServerPlayerResourceStore_CanGatherAfterSpendingAtCapacity()
        {
            ServerPlayerResourceStore store = new(50);
            Assert.That(store.TryAdd(7, ResourceType.Stone, 50, out _), Is.True);
            Assert.That(store.TryAdd(7, ResourceType.Stone, 1, out _), Is.False);

            Assert.That(store.TryConsume(7, ResourceType.Stone, 3, out int remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(47));
            Assert.That(store.TryAdd(7, ResourceType.Stone, 2, out int gathered), Is.True);
            Assert.That(gathered, Is.EqualTo(49));
        }

        [Test]
        public void ServerPlayerResourceStore_ConsumesOnlyWhenFullCostIsAvailable()
        {
            ServerPlayerResourceStore store = new(50);
            store.TryAdd(7, ResourceType.Stone, 2, out _);

            Assert.That(store.TryConsume(7, ResourceType.Stone, 1, out int remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(1));
            Assert.That(store.TryConsume(7, ResourceType.Stone, 2, out remaining), Is.False);
            Assert.That(remaining, Is.EqualTo(1));
        }

        [Test]
        public void ServerPlayerResourceStore_SeparatesPlayers()
        {
            ServerPlayerResourceStore store = new(50);
            store.TryAdd(1, ResourceType.Stone, 4, out _);
            store.TryAdd(2, ResourceType.Stone, 2, out _);

            Assert.That(store.GetAmount(1, ResourceType.Stone), Is.EqualTo(4));
            Assert.That(store.GetAmount(2, ResourceType.Stone), Is.EqualTo(2));
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

        [Test]
        public void ServerStatusUI_ShowsConnectionStatesAndHidesWhenConnected()
        {
            GameObject root = new("ServerStatus");
            TowerOfBabel.ServerStatusUI ui = root.AddComponent<TowerOfBabel.ServerStatusUI>();
            GameObject visuals = new("Visuals");
            visuals.transform.SetParent(root.transform);
            TMP_Text status = CreateText(visuals.transform, "Status");
            SetField(ui, "visuals", visuals);
            SetField(ui, "statusText", status);

            ui.ShowDisconnected();
            Assert.That(ui.IsVisible, Is.True);
            Assert.That(ui.StatusText, Is.EqualTo("Not connected to server"));
            Assert.That(status.color, Is.EqualTo(Color.red));

            ui.ShowConnecting();
            Assert.That(ui.StatusText, Is.EqualTo("Connecting..."));
            Assert.That(status.color, Is.EqualTo(Color.yellow));

            ui.Hide();
            Assert.That(ui.IsVisible, Is.False);
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

        private sealed class FakePresentationInteractable : MonoBehaviour, IInteractable,
            IInteractionPresentation
        {
            public string ObjectName => "Tower Asset";
            public string DetailText => "Stage 1/10";
            public Color DetailColor => Color.yellow;
            public string PromptText => "Press 'E'";
            public float Duration => 1f;
            public bool CanInteract => true;
            public bool ShouldShowInteraction { get; private set; } = true;
            public bool IsFocused { get; private set; }
            public event System.Action InteractionPresentationChanged;

            public void SetShouldShow(bool shouldShow)
            {
                ShouldShowInteraction = shouldShow;
                InteractionPresentationChanged?.Invoke();
            }

            public void SetInteractionFocused(bool focused) => IsFocused = focused;
            public void BeginInteraction(GameObject interactor) { }
            public void UpdateInteraction(float normalizedProgress) { }
            public void CancelInteraction() { }
            public void CompleteInteraction(GameObject interactor) { }
        }
    }
}
