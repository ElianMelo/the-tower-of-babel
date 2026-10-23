using UnityEngine;

[DisallowMultipleComponent]
public class InterfaceManager : MonoBehaviour
{
    public static InterfaceManager Instance { get; private set; }

    [SerializeField] private InteractionUI interactionUI;
    [SerializeField] private TowerOfBabel.InventoryUI inventoryUI;
    [SerializeField] private TowerOfBabel.UpgradeUI upgradeUI;
    [SerializeField] private TowerOfBabel.ServerStatusUI serverStatusUI;
    [SerializeField] private TowerOfBabel.AnimationEmoteUI animationEmoteUI;
    [SerializeField] private TowerOfBabel.Interface.AeroDock menuLauncher;

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

    public void ShowInteraction(string objectName, string detail, Color detailColor, string prompt,TowerOfBabel.Resources.ResourceType? resourceType=null)
    {
        interactionUI.Show(objectName, detail, detailColor, prompt,resourceType);
    }

    public void ShowInteraction(string objectName,string detail,Color detailColor,string prompt,TowerOfBabel.Resources.Interaction.InteractionResourceData resources)
    {
        interactionUI.Show(objectName,detail,detailColor,prompt,resources);
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
        if (inventoryUI != null && inventoryUI.IsVisible) HideInventory();
        else ShowInventory();
    }

    public void ShowInventory()
    {
        TowerOfBabel.Cheats.CheatMenuUI.Instance?.Hide();
        menuLauncher?.Close();
        upgradeUI?.Hide();
        animationEmoteUI?.Hide();
        inventoryUI?.Show();
    }

    public void HideInventory()
    {
        inventoryUI?.Hide();
    }

    public void ToggleUpgrade()
    {
        if (upgradeUI != null && upgradeUI.IsVisible) HideUpgrade();
        else ShowUpgrade();
    }

    public void ShowUpgrade()
    {
        TowerOfBabel.Cheats.CheatMenuUI.Instance?.Hide();
        menuLauncher?.Close();
        inventoryUI?.Hide();
        animationEmoteUI?.Hide();
        upgradeUI?.Show();
    }

    public void HideUpgrade()
    {
        upgradeUI?.Hide();
    }

    public void ToggleEmotes()
    {
        TowerOfBabel.Cheats.CheatMenuUI.Instance?.Hide();
        menuLauncher?.Close();
        if (animationEmoteUI == null) return;
        if (animationEmoteUI.IsVisible) animationEmoteUI.Hide();
        else
        {
            inventoryUI?.Hide();
            upgradeUI?.Hide();
            animationEmoteUI.Show();
        }
    }

    public void HideEmotes() => animationEmoteUI?.Hide();

    public void ShowServerDisconnected() => serverStatusUI?.ShowDisconnected();
    public void ShowServerConnecting() => serverStatusUI?.ShowConnecting();
    public void HideServerStatus() => serverStatusUI?.Hide();
}
