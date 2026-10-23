using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel.Interface
{
    public sealed class AeroDock : MonoBehaviour
    {
        [SerializeField] private GameObject menuList;
        [SerializeField] private CanvasGroup listGroup;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private float transitionDuration = .16f;
        private InputAction toggleAction;
        private CursorStateConfig? previousCursor;
        private PlayerControlStateMachine controls;
        private float visibility;
        public bool IsExpanded { get; private set; }

        private void Awake() => ApplyVisibility(0);
        private void OnEnable()
        {
            toggleAction = inputActions?.FindAction("Player/Menus", false);
            if (toggleAction == null) return;
            toggleAction.performed += HandleToggle;
            toggleAction.Enable();
        }
        private void OnDisable()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= HandleToggle;
                toggleAction.Disable();
                toggleAction = null;
            }
            Close();
            ApplyVisibility(0);
        }
        private void Update()
        {
            if (IsExpanded && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
            visibility = Mathf.MoveTowards(visibility, IsExpanded ? 1 : 0, Time.unscaledDeltaTime / Mathf.Max(.01f, transitionDuration));
            ApplyVisibility(visibility);
        }
        private void HandleToggle(InputAction.CallbackContext _) => Toggle();
        public void Toggle()
        {
            if (IsExpanded) { Close(); return; }
            var manager = InterfaceManager.Instance;
            TowerOfBabel.Cheats.CheatMenuUI.Instance?.Hide();
            manager?.HideInventory(); manager?.HideUpgrade(); manager?.HideEmotes();
            previousCursor = CursorStateConfig.Current;
            CursorStateConfig.UnlockedVisible.Apply();
            controls = FindFirstObjectByType<PlayerControlStateMachine>();
            controls?.SetModalInputLocked(true);
            IsExpanded = true;
            AeroUIAudio.Play(AeroUICue.Open);
            if (menuList != null) menuList.SetActive(true);
        }
        public void Close()
        {
            if (!IsExpanded) return;
            IsExpanded = false;
            AeroUIAudio.Play(AeroUICue.Close);
            if (listGroup != null) listGroup.blocksRaycasts = false;
            controls?.SetModalInputLocked(false);
            if (previousCursor.HasValue) previousCursor.Value.Apply();
            previousCursor = null;
        }
        private void ApplyVisibility(float amount)
        {
            if (menuList == null) return;
            menuList.SetActive(amount > 0 || IsExpanded);
            float eased = Mathf.SmoothStep(0, 1, amount);
            // Fade the list without squashing its text or changing its hit rectangles.
            menuList.transform.localScale = Vector3.one;
            if (listGroup == null) return;
            listGroup.alpha = eased;
            listGroup.interactable = IsExpanded;
            listGroup.blocksRaycasts = IsExpanded;
        }
        public void Upgrades() { Close(); InterfaceManager.Instance?.ToggleUpgrade(); }
        public void Inventory() { Close(); InterfaceManager.Instance?.ToggleInventory(); }
        public void Emotes() { Close(); InterfaceManager.Instance?.ToggleEmotes(); }
    }
}
