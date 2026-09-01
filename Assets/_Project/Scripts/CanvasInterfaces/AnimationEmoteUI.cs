using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel
{
    public class AnimationEmoteUI : MonoBehaviour
    {
        private readonly struct EmoteOption
        {
            public readonly string Label;
            public readonly PlayerAnimationTrigger Trigger;

            public EmoteOption(string label, PlayerAnimationTrigger trigger)
            {
                Label = label;
                Trigger = trigger;
            }
        }

        private static readonly EmoteOption[] EmoteOptions =
        {
            new("Dance", PlayerAnimationTrigger.Dancing),
            new("Hip Hop 1", PlayerAnimationTrigger.HipHopOne),
            new("Hip Hop 2", PlayerAnimationTrigger.HipHopTwo),
            new("Hip Hop 3", PlayerAnimationTrigger.HipHopThree),
            new("Rumba", PlayerAnimationTrigger.RumbaDance),
            new("Silly Dance", PlayerAnimationTrigger.SillyDancing)
        };

        [Header("References")]
        [SerializeField] private GameObject visuals;
        [SerializeField] private List<OptionButton> options = new();
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string toggleActionName = "Player/Emote";
        [SerializeField] private PlayerVisuals playerVisuals;

        [Header("Radial Layout")]
        [SerializeField, Min(1f)] private float radialRadius = 300f;
        [SerializeField] private float startAngleDegrees = 90f;

        private InputAction toggleAction;
        private PlayerControlStateMachine controlStateMachine;
        private CursorLockMode previousCursorLockMode;
        private bool previousCursorVisible;
        private bool cursorStateCaptured;
        private bool cameraLockApplied;

        public bool IsVisible => visuals != null && visuals.activeSelf;

        private void Awake()
        {
            ResolveReferences();
            BuildOptions();
            Hide();
        }

        private void OnEnable()
        {
            toggleAction = inputActions?.FindAction(toggleActionName, false);
            if (toggleAction == null)
            {
                Debug.LogWarning($"Input action '{toggleActionName}' was not found for the emote menu.", this);
                return;
            }

            toggleAction.performed += HandleTogglePerformed;
            toggleAction.Enable();
        }

        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= HandleTogglePerformed;
                toggleAction.Disable();
                toggleAction = null;
            }

            if (cursorStateCaptured)
                RestoreCursorState();
            ReleaseCameraLock();
        }

        public void Show()
        {
            ResolveReferences();
            if (visuals == null || IsVisible)
                return;

            foreach (var option in options)
            {
                option.SetInteractable(CanSelectEmote());
                option.ToggleHover(false);
            }

            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            cursorStateCaptured = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (controlStateMachine != null)
            {
                controlStateMachine.SetCameraInputLocked(true);
                cameraLockApplied = true;
            }
            visuals.SetActive(true);
        }

        public void Hide()
        {
            foreach (var option in options)
            {
                if (option != null)
                    option.ToggleHover(false);
            }

            if (visuals != null)
                visuals.SetActive(false);
            if (cursorStateCaptured)
                RestoreCursorState();
            ReleaseCameraLock();
        }

        public void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        private void BuildOptions()
        {
            options.RemoveAll(option => option == null);
            if (options.Count == 0)
            {
                Debug.LogError("AnimationEmoteUI requires one OptionButton to use as its runtime template.", this);
                return;
            }

            if (visuals == null)
                visuals = options[0].transform.parent.gameObject;

            OptionButton template = options[0];
            Transform optionParent = template.transform.parent;
            while (options.Count < EmoteOptions.Length)
                options.Add(Instantiate(template, optionParent));

            for (int i = 0; i < options.Count; i++)
            {
                bool isUsed = i < EmoteOptions.Length;
                options[i].gameObject.SetActive(isUsed);
                if (!isUsed)
                    continue;

                EmoteOption definition = EmoteOptions[i];
                options[i].name = $"EmoteOption_{definition.Trigger}";
                options[i].Configure(definition.Label, () => SelectEmote(definition.Trigger));
            }

            LayoutOptions();
        }

        private void LayoutOptions()
        {
            float angleStep = 360f / EmoteOptions.Length;
            for (int i = 0; i < EmoteOptions.Length; i++)
            {
                if (options[i].transform is not RectTransform rectTransform)
                    continue;

                float angleRadians = (startAngleDegrees - angleStep * i) * Mathf.Deg2Rad;
                rectTransform.anchoredPosition = new Vector2(
                    Mathf.Cos(angleRadians), Mathf.Sin(angleRadians)) * radialRadius;
            }
        }

        private void SelectEmote(PlayerAnimationTrigger trigger)
        {
            ResolveReferences();
            if (!CanSelectEmote())
                return;

            playerVisuals.PlayEmote(trigger);
            Hide();
        }

        private bool CanSelectEmote() =>
            playerVisuals != null &&
            (controlStateMachine == null || controlStateMachine.CurrentState == PlayerControlState.Moving);

        private void ResolveReferences()
        {
            if (inputActions == null)
            {
                InventoryUI inventory = FindFirstObjectByType<InventoryUI>();
                if (inventory != null)
                    inputActions = inventory.InputActions;
            }

            if (playerVisuals != null && controlStateMachine != null)
                return;

            PlayerController playerController = FindFirstObjectByType<PlayerController>();
            if (playerController == null)
                return;
            if (playerVisuals == null)
                playerVisuals = playerController.GetComponentInChildren<PlayerVisuals>(true);
            if (controlStateMachine == null)
                controlStateMachine = playerController.GetComponent<PlayerControlStateMachine>();
        }

        private void HandleTogglePerformed(InputAction.CallbackContext context)
        {
            Toggle();
        }

        private void RestoreCursorState()
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            cursorStateCaptured = false;
        }

        private void ReleaseCameraLock()
        {
            if (!cameraLockApplied)
                return;

            controlStateMachine?.SetCameraInputLocked(false);
            cameraLockApplied = false;
        }
    }
}
