using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;
using UnityEngine.TestTools;

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
