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
        [SerializeField] private OptionButton optionPrefab;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string toggleActionName = "Player/Emote";
        [SerializeField] private PlayerVisuals playerVisuals;

        [Header("Radial Layout")]
        [SerializeField, Min(1f)] private float radialRadius = 300f;
        [SerializeField] private float startAngleDegrees = 90f;

        private readonly List<OptionButton> options = new();
        private InputAction toggleAction;
        private PlayerControlStateMachine controlStateMachine;
        private CursorStateConfig? previousCursorState;
        private bool inputLockApplied;

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

            RestoreCursorState();
            ReleaseInputLock();
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

            previousCursorState = CursorStateConfig.Current;
            CursorStateConfig.UnlockedVisible.Apply();
            if (controlStateMachine != null)
            {
                controlStateMachine.SetModalInputLocked(true);
                inputLockApplied = true;
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
            RestoreCursorState();
            ReleaseInputLock();
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
            if (optionPrefab == null)
            {
                Debug.LogError("AnimationEmoteUI requires an OptionButton prefab.", this);
                return;
            }

            if (visuals == null)
            {
                Debug.LogError("AnimationEmoteUI requires a visuals container for its options.", this);
                return;
            }

            options.Clear();

            for (int i = 0; i < EmoteOptions.Length; i++)
            {
                EmoteOption definition = EmoteOptions[i];
                OptionButton option = Instantiate(optionPrefab, visuals.transform);
                option.name = $"EmoteOption_{definition.Trigger}";
                option.Configure(definition.Label, () => SelectEmote(definition.Trigger));
                options.Add(option);
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
            if (!previousCursorState.HasValue)
                return;

            previousCursorState.Value.Apply();
            previousCursorState = null;
        }

        private void ReleaseInputLock()
        {
            if (!inputLockApplied)
                return;

            controlStateMachine?.SetModalInputLocked(false);
            inputLockApplied = false;
        }
    }
}
