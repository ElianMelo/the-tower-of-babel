using UnityEngine;

[DisallowMultipleComponent]
public class InterfaceManager : MonoBehaviour
{
    public static InterfaceManager Instance { get; private set; }

    [SerializeField] private InteractionUI interactionUI;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("Only one InterfaceManager can be active at a time.", this);
            enabled = false;
            return;
        }

        Instance = this;
        interactionUI.Hide();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ShowInteraction(string objectName)
    {
        interactionUI.Show(objectName);
    }

    public void HideInteraction()
    {
        interactionUI.Hide();
    }
}
