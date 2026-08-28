using System.Collections.Generic;
using TowerOfBabel.Resources;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TowerOfBabel
{
    public class InventoryUI : MonoBehaviour
    {
        [SerializeField] private GameObject visuals;

        [SerializeField] private GameObject resourcePrefab;
        [SerializeField] private Transform targetListResources;
        [SerializeField] private ResourceDefinitionCollection definitionCollection;
        [SerializeField] private PlayerResourceWallet wallet;
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string toggleActionName = "Player/Inventory";

        private readonly Dictionary<ResourceType, ResourceInventoryDisplay> displays = new();
        private InputAction toggleAction;

        public bool IsVisible => visuals != null && visuals.activeSelf;

        private void Awake()
        {
            Hide();
            BuildResourceList();
        }

        private void OnEnable()
        {
            if (wallet != null)
                wallet.ResourceAmountChanged += HandleResourceAmountChanged;

            toggleAction = inputActions?.FindAction(toggleActionName, false);
            if (toggleAction != null)
            {
                toggleAction.performed += HandleTogglePerformed;
                toggleAction.Enable();
            }
        }

        private void OnDisable()
        {
            if (wallet != null)
                wallet.ResourceAmountChanged -= HandleResourceAmountChanged;

            if (toggleAction != null)
            {
                toggleAction.performed -= HandleTogglePerformed;
                toggleAction.Disable();
            }
        }

        public void Show() => visuals.SetActive(true);
        public void Hide() => visuals.SetActive(false);

        public void Toggle()
        {
            if (IsVisible)
                Hide();
            else
                Show();
        }

        public void BuildResourceList()
        {
            foreach (ResourceInventoryDisplay display in displays.Values)
            {
                if (display != null)
                    Destroy(display.gameObject);
            }

            displays.Clear();
            if (definitionCollection == null || wallet == null || resourcePrefab == null || targetListResources == null)
                return;

            foreach (ResourceDefinition definition in definitionCollection.Definitions)
            {
                if (definition == null || displays.ContainsKey(definition.ResourceType))
                    continue;

                GameObject instance = Instantiate(resourcePrefab, targetListResources);
                ResourceInventoryDisplay display = instance.GetComponent<ResourceInventoryDisplay>();
                if (display == null)
                {
                    Destroy(instance);
                    continue;
                }

                display.Bind(definition, wallet.GetAmount(definition.ResourceType));
                displays.Add(definition.ResourceType, display);
            }
        }

        public bool TryGetDisplay(ResourceType resourceType, out ResourceInventoryDisplay display)
        {
            return displays.TryGetValue(resourceType, out display);
        }

        private void HandleTogglePerformed(InputAction.CallbackContext context)
        {
            InterfaceManager.Instance?.ToggleInventory();
        }

        private void HandleResourceAmountChanged(ResourceType resourceType, int amount)
        {
            if (displays.TryGetValue(resourceType, out ResourceInventoryDisplay display))
                display.SetAmount(amount);
        }
    }
}
