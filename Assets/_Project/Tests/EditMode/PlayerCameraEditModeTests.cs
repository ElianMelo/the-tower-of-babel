using System.Reflection;
using NUnit.Framework;
using TMPro;
using TowerOfBabel.Resources.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel.Resources.Tests
{
    /// <summary>
    /// Mouse-wheel zoom between first and third person, free camera look during timed actions, cancelling an action by
    /// moving, and the loading ring at the crosshair.
    /// </summary>
    public sealed class PlayerCameraEditModeTests
    {
        // Far from anything in whichever scene is open, so only the colliders a test creates are in the way.
        private static readonly Vector3 Away = new(24000f, 3000f, -24000f);
        private const float MinDistance = 1.75f, MaxDistance = 6f, Step = .75f;

        [Test]
        public void Zoom_ScrollingBackFromFirstPersonStartsTheThirdPersonOrbit()
        {
            Assert.That(PlayerCameraZoom.Step(0f, -1f, MinDistance, MaxDistance, Step), Is.EqualTo(MinDistance));
            Assert.That(PlayerCameraZoom.Step(0f, -2f, MinDistance, MaxDistance, Step), Is.EqualTo(MinDistance + Step).Within(1e-4f));
            Assert.That(PlayerCameraZoom.Step(MinDistance, -1f, MinDistance, MaxDistance, Step), Is.EqualTo(MinDistance + Step).Within(1e-4f));
            Assert.That(PlayerCameraZoom.Step(0f, -.1f, MinDistance, MaxDistance, Step), Is.EqualTo(MinDistance),
                "A small trackpad scroll still leaves first person.");
        }

        [Test]
        public void Zoom_ScrollingForwardPastTheNearestOrbitSwitchesToFirstPerson()
        {
            Assert.That(PlayerCameraZoom.Step(MinDistance + Step, 1f, MinDistance, MaxDistance, Step), Is.EqualTo(MinDistance).Within(1e-4f));
            Assert.That(PlayerCameraZoom.Step(MinDistance, 1f, MinDistance, MaxDistance, Step), Is.EqualTo(0f));
            Assert.That(PlayerCameraZoom.Step(MinDistance + .3f, 1f, MinDistance, MaxDistance, Step), Is.EqualTo(0f));
            Assert.That(PlayerCameraZoom.Step(0f, 1f, MinDistance, MaxDistance, Step), Is.EqualTo(0f));
        }

        [Test]
        public void Zoom_StaysWithinTheMaximumAndIgnoresNoScroll()
        {
            float distance = 0f;
            for (int i = 0; i < 20; i++)
                distance = PlayerCameraZoom.Step(distance, -1f, MinDistance, MaxDistance, Step);
            Assert.That(distance, Is.EqualTo(MaxDistance));
            Assert.That(PlayerCameraZoom.Step(3f, 0f, MinDistance, MaxDistance, Step), Is.EqualTo(3f));
        }

        [Test]
        public void Orbit_SitsBehindTheEyesAndOverTheRightShoulder()
        {
            var shoulder = new Vector2(.5f, .2f);
            AssertNear(PlayerCameraZoom.OrbitPosition(Vector3.zero, Quaternion.identity, 2f, shoulder, 1f), new Vector3(.5f, .2f, -2f));
            AssertNear(PlayerCameraZoom.OrbitPosition(Vector3.zero, Quaternion.Euler(0f, 90f, 0f), 2f, shoulder, 1f), new Vector3(-2f, .2f, -.5f));
            AssertNear(PlayerCameraZoom.OrbitPosition(Vector3.one, Quaternion.identity, 0f, shoulder, 0f), Vector3.one);

            // Looking down keeps the shoulder offset level and pulls the camera up behind the player.
            Quaternion down = Quaternion.Euler(40f, 0f, 0f);
            Vector3 tilted = PlayerCameraZoom.OrbitPosition(Vector3.zero, down, 2f, shoulder, 1f);
            AssertNear(tilted, new Vector3(.5f, .2f, 0f) - down * Vector3.forward * 2f);
            Assert.That(tilted.y, Is.GreaterThan(1f));

            Assert.That(PlayerCameraZoom.ThirdPersonBlend(0f, MinDistance), Is.EqualTo(0f));
            Assert.That(PlayerCameraZoom.ThirdPersonBlend(MinDistance * .5f, MinDistance), Is.EqualTo(.5f).Within(1e-4f));
            Assert.That(PlayerCameraZoom.ThirdPersonBlend(MaxDistance, MinDistance), Is.EqualTo(1f));
        }

        [Test]
        public void InteractionRay_StartsWhereTheCameraRayPassesTheEyes()
        {
            var cameraRay = new Ray(new Vector3(0f, 0f, -3f), Vector3.forward);
            AssertNear(PlayerCameraZoom.StartNear(cameraRay, new Vector3(.4f, .1f, 0f)).origin, Vector3.zero);
            AssertNear(PlayerCameraZoom.StartNear(cameraRay, new Vector3(0f, 0f, -5f)).origin, cameraRay.origin);
        }

        [Test]
        public void Follower_SitsAtTheEyesInFirstPersonAndOrbitsBehindInThirdPerson()
        {
            var player = new GameObject("Zoom test player");
            var cameraRoot = new GameObject("Zoom test camera");
            try
            {
                player.transform.position = Away;
                var follower = cameraRoot.AddComponent<PlayerFollower>();
                follower.SetupPlayer(player.transform);
                Vector3 eyes = Away + Vector3.up * .75f;

                LateUpdate(follower);
                AssertNear(cameraRoot.transform.position, eyes);
                Assert.That(follower.IsThirdPerson, Is.False);
                Assert.That(follower.CameraDistance, Is.EqualTo(0f));

                follower.Zoom(-1f);
                LateUpdate(follower);
                Assert.That(follower.IsThirdPerson, Is.True);
                Assert.That(follower.TargetDistance, Is.EqualTo(follower.MinThirdPersonDistance));
                Vector3 expected = PlayerCameraZoom.OrbitPosition(eyes, Quaternion.identity, follower.MinThirdPersonDistance, new Vector2(.45f, .25f), 1f);
                AssertNear(cameraRoot.transform.position, expected);
                Assert.That(follower.CameraDistance, Is.EqualTo((expected - eyes).magnitude).Within(1e-3f));

                follower.Zoom(1f);
                LateUpdate(follower);
                AssertNear(cameraRoot.transform.position, eyes);
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Follower_PullsInFrontOfWallsButIgnoresThePlayersOwnBody()
        {
            var player = new GameObject("Collision test player");
            var cameraRoot = new GameObject("Collision test camera");
            GameObject wall = null;
            try
            {
                player.transform.position = Away;
                var body = player.AddComponent<CapsuleCollider>();
                body.height = 2f; body.radius = .52f; body.center = Vector3.up;
                var follower = cameraRoot.AddComponent<PlayerFollower>();
                follower.SetupPlayer(player.transform);
                follower.SetDistanceImmediate(4f);
                Physics.SyncTransforms();

                LateUpdate(follower);
                Assert.That(follower.CameraDistance, Is.GreaterThan(3.9f), "The player's own capsule must not push the camera in.");

                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position = Away + new Vector3(0f, 1f, -2f);
                wall.transform.localScale = new Vector3(6f, 6f, .2f);
                Physics.SyncTransforms();

                LateUpdate(follower);
                Assert.That(follower.CameraDistance, Is.LessThan(2f));
                Assert.That(cameraRoot.transform.position.z, Is.GreaterThan(Away.z - 1.9f), "The camera stays in front of the wall.");
            }
            finally
            {
                if (wall != null) Object.DestroyImmediate(wall);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void Follower_ShowsTheLocalHeadOnlyInThirdPerson()
        {
            var player = new GameObject("Head test player");
            var cameraRoot = new GameObject("Head test camera");
            try
            {
                player.transform.position = Away;
                player.AddComponent<PlayerController>();
                var model = new GameObject("Model");
                model.transform.SetParent(player.transform, false);
                var visuals = model.AddComponent<PlayerVisuals>();
                var head = new GameObject("Head").transform;
                head.SetParent(model.transform, false);
                head.localPosition = Vector3.up * 1.6f;
                head.localScale = new Vector3(1.2f, 1.2f, 1.2f);
                Invoke(visuals, "HideLocalPlayerHead");
                Assert.That(visuals.IsLocalHeadVisible, Is.False);
                Physics.SyncTransforms();

                var follower = cameraRoot.AddComponent<PlayerFollower>();
                follower.SetupPlayer(player.transform);
                LateUpdate(follower);
                Assert.That(head.localScale, Is.EqualTo(Vector3.zero), "First person keeps the head collapsed.");

                follower.Zoom(-1f);
                LateUpdate(follower);
                Assert.That(visuals.IsLocalHeadVisible, Is.True);
                Assert.That(head.localScale, Is.EqualTo(new Vector3(1.2f, 1.2f, 1.2f)));

                follower.Zoom(1f);
                LateUpdate(follower);
                Assert.That(head.localScale, Is.EqualTo(Vector3.zero));
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void StateMachine_KeepsTheCameraFreeDuringTimedActions()
        {
            var player = new GameObject("Lock test player");
            var cameraRoot = new GameObject("Lock test camera");
            try
            {
                var controller = player.AddComponent<PlayerController>();
                var machine = player.AddComponent<PlayerControlStateMachine>();
                var rotator = cameraRoot.AddComponent<MouseRotator>();
                ResourceInteractionEditModeTests.SetField(machine, "playerController", controller);
                ResourceInteractionEditModeTests.SetField(machine, "mouseRotator", rotator);
                Invoke(machine, "Awake");
                Assert.That(machine.CanLook, Is.False);
                Assert.That(rotator.CanLook, Is.False);

                machine.SetConnected(true);
                Assert.That(machine.CanMove && machine.CanLook && rotator.CanLook, Is.True);
                Assert.That(MovementLocked(controller), Is.False);

                Assert.That(machine.BeginInteraction(true), Is.True);
                Assert.That(machine.CurrentState, Is.EqualTo(PlayerControlState.Interacting));
                Assert.That(machine.CanMove, Is.False);
                Assert.That(MovementLocked(controller), Is.True, "Gathering, processing and building keep the player in place.");
                Assert.That(machine.CanLook, Is.True);
                Assert.That(rotator.CanLook, Is.True, "The camera can still look around and zoom during the action.");

                machine.SetModalInputLocked(true);
                Assert.That(rotator.CanLook, Is.False, "An open menu takes the mouse.");
                machine.SetModalInputLocked(false);
                Assert.That(rotator.CanLook, Is.True);

                machine.EndInteraction();
                Assert.That(MovementLocked(controller), Is.False);
                machine.SetConnected(false);
                Assert.That(rotator.CanLook, Is.False);
                Assert.That(MovementLocked(controller), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void MovementCancel_IgnoresKeysHeldAtTheStartUntilReleased()
        {
            var gate = new InteractionMovementCancel();
            gate.Begin(Vector2.up);
            Assert.That(gate.ShouldCancel(Vector2.up, false), Is.False, "Walking up and pressing interact must not cancel at once.");
            Assert.That(gate.ShouldCancel(Vector2.zero, false), Is.False);
            Assert.That(gate.ShouldCancel(Vector2.up, false), Is.True, "Pressing a movement key again cancels.");

            gate.Begin(Vector2.zero);
            Assert.That(gate.ShouldCancel(new Vector2(.1f, 0f), false), Is.False, "Stick noise inside the dead zone is not movement.");
            Assert.That(gate.ShouldCancel(Vector2.left, false), Is.True);

            gate.Begin(Vector2.up);
            Assert.That(gate.ShouldCancel(Vector2.up, true), Is.True, "Jumping cancels.");
        }

        [Test]
        public void Raycaster_MovingCancelsButMenusDoNot()
        {
            var player = new GameObject("Cancel test player");
            var target = new GameObject("Cancel test target");
            try
            {
                var machine = player.AddComponent<PlayerControlStateMachine>();
                var raycaster = player.AddComponent<PlayerInteractionRaycaster>();
                ResourceInteractionEditModeTests.SetField(raycaster, "playerStateMachine", machine);
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.up, true), Is.False, "Nothing to cancel.");

                var interactable = target.AddComponent<TestInteractable>();
                ResourceInteractionEditModeTests.SetField(raycaster, "activeInteraction", interactable);
                ResourceInteractionEditModeTests.SetField(raycaster, "activeInteractionBehaviour", interactable);
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.zero, false), Is.False, "Standing still (or only looking) keeps going.");

                machine.SetModalInputLocked(true);
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.up, false), Is.False, "Keys typed into a menu do not cancel.");
                machine.SetModalInputLocked(false);
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.up, false), Is.False, "Still held from the menu.");
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.zero, false), Is.False);
                Assert.That(raycaster.MovementCancelsInteraction(Vector2.right, false), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(target);
            }
        }

        [Test]
        public void Raycaster_ThirdPersonReachesAsFarAsFirstPersonAndSkipsThePlayer()
        {
            var player = new GameObject("Reach test player");
            var cameraRoot = new GameObject("Reach test camera");
            GameObject node = null;
            PlayerFollower follower = null;
            try
            {
                player.transform.position = Away;
                var body = player.AddComponent<CapsuleCollider>();
                body.height = 2f; body.radius = .52f; body.center = Vector3.up;
                var raycaster = player.AddComponent<PlayerInteractionRaycaster>();
                Vector3 eyes = Away + Vector3.up * .75f;
                node = GameObject.CreatePrimitive(PrimitiveType.Cube);
                node.transform.position = eyes + Vector3.forward * 3.5f;
                Physics.SyncTransforms();

                // A camera 4 m behind the eyes: the node is 7.5 m from the camera but only 3.5 m from the player.
                var cameraRay = new Ray(eyes - Vector3.forward * 4f, Vector3.forward);
                Assert.That(raycaster.TryFindTarget(cameraRay, out _), Is.False, "Without a follower the reach counts from the camera.");

                ResourceInteractionEditModeTests.SetField(raycaster, "interactionDistance", 10f);
                Assert.That(raycaster.TryFindTarget(cameraRay, out GameObject behindBody), Is.True);
                Assert.That(behindBody, Is.SameAs(node), "The player's own capsule between camera and node is skipped.");
                ResourceInteractionEditModeTests.SetField(raycaster, "interactionDistance", 5f);

                follower = cameraRoot.AddComponent<PlayerFollower>();
                follower.SetupPlayer(player.transform);
                Invoke(follower, "OnEnable");
                follower.SetDistanceImmediate(4f);
                LateUpdate(follower);
                Assert.That(PlayerFollower.For(player.transform), Is.SameAs(follower));
                Assert.That(raycaster.TryFindTarget(cameraRay, out GameObject found), Is.True);
                Assert.That(found, Is.SameAs(node));
            }
            finally
            {
                if (follower != null) Invoke(follower, "OnDisable");
                if (node != null) Object.DestroyImmediate(node);
                Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void InteractionUI_ShowsALoadingRingAtTheCrosshairDuringProgress()
        {
            var root = new GameObject("Ring test interaction", typeof(RectTransform));
            try
            {
                InteractionUI ui = CreateInteractionUI(root);
                Assert.That(ui.CrosshairRing, Is.Null);

                ui.SetProgress(.25f);
                Interface.CrosshairProgressRing ring = ui.CrosshairRing;
                Assert.That(ring, Is.Not.Null);
                Assert.That(ring.transform.parent, Is.SameAs(root.transform));
                var rect = (RectTransform)ring.transform;
                Assert.That(rect.anchorMin, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(rect.anchorMax, Is.EqualTo(new Vector2(.5f, .5f)));
                Assert.That(rect.anchoredPosition, Is.EqualTo(Vector2.zero), "The ring sits at the screen centre, where the crosshair aims.");
                Assert.That(ring.gameObject.activeSelf && ring.IsVisible, Is.True);
                Assert.That(ring.Progress, Is.EqualTo(.25f));

                Image fill = ring.transform.Find("Body/Fill").GetComponent<Image>();
                Assert.That(fill.type, Is.EqualTo(Image.Type.Filled));
                Assert.That(fill.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
                Assert.That(fill.fillOrigin, Is.EqualTo((int)Image.Origin360.Top));
                Assert.That(fill.fillClockwise, Is.True);
                Assert.That(fill.sprite, Is.Not.Null);
                Assert.That(fill.raycastTarget, Is.False, "The ring never blocks clicks.");

                ui.SetProgress(2f);
                Assert.That(ring.Progress, Is.EqualTo(1f));

                ui.HideProgress();
                Assert.That(ring.IsVisible || ring.gameObject.activeSelf, Is.False);
                Assert.That(ring.Progress, Is.EqualTo(0f));

                ui.SetProgress(.5f);
                Assert.That(ui.CrosshairRing, Is.SameAs(ring), "The ring is built once and reused.");
                ui.Hide();
                Assert.That(ring.gameObject.activeSelf, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static InteractionUI CreateInteractionUI(GameObject root)
        {
            InteractionUI ui = root.AddComponent<InteractionUI>();
            var visuals = new GameObject("Visuals", typeof(RectTransform));
            visuals.transform.SetParent(root.transform, false);
            var progress = new GameObject("Progress", typeof(RectTransform));
            progress.transform.SetParent(visuals.transform, false);
            Image bar = new GameObject("Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image)).GetComponent<Image>();
            bar.transform.SetParent(progress.transform, false);
            ResourceInteractionEditModeTests.SetField(ui, "visuals", visuals);
            ResourceInteractionEditModeTests.SetField(ui, "objectNameText", Text(visuals.transform, "Object"));
            ResourceInteractionEditModeTests.SetField(ui, "detailText", Text(visuals.transform, "Detail"));
            ResourceInteractionEditModeTests.SetField(ui, "promptText", Text(visuals.transform, "Prompt"));
            ResourceInteractionEditModeTests.SetField(ui, "progressVisuals", progress);
            ResourceInteractionEditModeTests.SetField(ui, "progressFill", bar);
            ResourceInteractionEditModeTests.SetField(ui, "progressText", Text(progress.transform, "Percent"));
            return ui;
        }

        private static TMP_Text Text(Transform parent, string name)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            return text;
        }

        private static bool MovementLocked(PlayerController controller) =>
            (bool)typeof(PlayerController).GetField("controlsLocked", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);

        private static void LateUpdate(PlayerFollower follower) => Invoke(follower, "LateUpdate");

        private static void Invoke(object target, string method) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

        private static void AssertNear(Vector3 actual, Vector3 expected)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"Expected {expected:F4} but was {actual:F4}.");
        }

        private sealed class TestInteractable : MonoBehaviour, IInteractable
        {
            public string ObjectName => "Test node";
            public string DetailText => string.Empty;
            public Color DetailColor => Color.white;
            public string PromptText => string.Empty;
            public float Duration => 1f;
            public bool CanInteract => true;
            public void BeginInteraction(GameObject interactor) { }
            public void UpdateInteraction(float normalizedProgress) { }
            public void CancelInteraction() { }
            public void CompleteInteraction(GameObject interactor) { }
        }
    }
}
