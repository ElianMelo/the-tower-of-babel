using TMPro;
using UnityEngine;

public class InteractionUI : MonoBehaviour
{
    // Handle visuals without deactive the actual game object
    [SerializeField] private GameObject visuals;

    // Fields used for this interface
    [SerializeField] private TMP_Text text;

    void Start()
    {
        
    }

    void Update()
    {
        
    }

    public void Show()
    {
        visuals.SetActive(true);
    }

    public void Hide()
    {
        visuals.SetActive(false);
    }
}
