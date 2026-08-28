using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InteractionUI : MonoBehaviour
{
    // Handle visuals without deactivating the interface component itself.
    [SerializeField] private GameObject visuals;
    [SerializeField] private TMP_Text objectNameText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text promptText;
    [SerializeField] private GameObject progressVisuals;
    [SerializeField] private Image progressFill;
    [SerializeField] private TMP_Text progressText;

    public void Show(string objectName, string detail, Color detailColor, string prompt)
    {
        objectNameText.text = objectName;
        detailText.text = detail;
        detailText.color = detailColor;
        detailText.fontStyle = FontStyles.Bold;
        promptText.text = prompt;
        visuals.SetActive(true);
    }

    public void SetProgress(float normalizedProgress)
    {
        float clampedProgress = Mathf.Clamp01(normalizedProgress);
        progressVisuals.SetActive(true);
        progressFill.fillAmount = clampedProgress;
        progressText.text = $"{Mathf.RoundToInt(clampedProgress * 100f)}%";
    }

    public void HideProgress()
    {
        progressVisuals.SetActive(false);
        progressFill.fillAmount = 0f;
        progressText.text = "0%";
    }

    public void Hide()
    {
        objectNameText.text = string.Empty;
        detailText.text = string.Empty;
        promptText.text = string.Empty;
        HideProgress();
        visuals.SetActive(false);
    }
}
