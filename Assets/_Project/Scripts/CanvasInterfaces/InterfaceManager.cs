using UnityEngine;

[DisallowMultipleComponent]
public class InterfaceManager : MonoBehaviour
{
    public static InterfaceManager Instance { get; private set; }

    [SerializeField] private InteractionUI interactionUI;
    [SerializeField] private TowerOfBabel.InventoryUI inventoryUI;
    [SerializeField] private TowerOfBabel.UpgradeUI upgradeUI;
    [SerializeField] private TowerOfBabel.ServerStatusUI serverStatusUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one InterfaceManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        if (upgradeUI == null)
            upgradeUI = FindFirstObjectByType<TowerOfBabel.UpgradeUI>(FindObjectsInactive.Include);
        interactionUI?.Hide();
        inventoryUI?.Hide();
        upgradeUI?.Hide();
        serverStatusUI?.ShowDisconnected();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowInteraction(string objectName, string detail, Color detailColor, string prompt)
    {
        interactionUI.Show(objectName, detail, detailColor, prompt);
    }

    public void SetInteractionProgress(float normalizedProgress)
    {
        interactionUI.SetProgress(normalizedProgress);
    }

    public void HideInteractionProgress()
    {
        interactionUI.HideProgress();
    }

    public void HideInteraction()
    {
        interactionUI.Hide();
    }

    public void ToggleInventory()
    {
        inventoryUI?.Toggle();
    }

    public void ShowInventory()
    {
        inventoryUI?.Show();
    }

    public void HideInventory()
    {
        inventoryUI?.Hide();
    }

    public void ToggleUpgrade()
    {
        upgradeUI?.Toggle();
    }

    public void ShowUpgrade()
    {
        upgradeUI?.Show();
    }

    public void HideUpgrade()
    {
        upgradeUI?.Hide();
    }

    public void ShowServerDisconnected() => serverStatusUI?.ShowDisconnected();
    public void ShowServerConnecting() => serverStatusUI?.ShowConnecting();
    public void HideServerStatus() => serverStatusUI?.Hide();
}
