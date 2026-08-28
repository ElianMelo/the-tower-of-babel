using TMPro;
using UnityEngine;

public class InteractionUI : MonoBehaviour
{
    // Handle visuals without deactivating the interface component itself.
    [SerializeField] private GameObject visuals;
    [SerializeField] private TMP_Text text;

    public void Show(string objectName)
    {
        text.text = objectName;
        visuals.SetActive(true);
    }

    public void Hide()
    {
        text.text = string.Empty;
        visuals.SetActive(false);
    }
}
