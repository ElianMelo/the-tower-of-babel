using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;
using TowerOfBabel.Resources;
using TowerOfBabel.Resources.Interaction;

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
    [SerializeField] private Image resourceIcon;
    [SerializeField] private GameObject fallbackIcon;
    [SerializeField] private ResourceDefinitionCollection definitions;
    [SerializeField] private GameObject processAction;
    [SerializeField] private GameObject buildAction;
    [SerializeField] private GameObject resourceSummary;
    [SerializeField] private GameObject sourceSlot;
    [SerializeField] private Image sourceIcon;
    [SerializeField] private TMP_Text sourceAmount;
    [SerializeField] private TMP_Text resultAmount;
    [SerializeField] private TMP_Text resultCaption;
    [SerializeField] private GameObject recipeArrow;

    public InteractionActionKind Action { get; private set; }

    public void Show(string objectName, string detail, Color detailColor, string prompt,ResourceType? resourceType=null)
    {
        Show(objectName,detail,detailColor,prompt,resourceType.HasValue ? InteractionResourceData.Gather(resourceType.Value,1) : default);
    }

    public void Show(string objectName,string detail,Color detailColor,string prompt,InteractionResourceData resources)
    {
        objectNameText.text = objectName;
        // The recipe direction is drawn as an icon; the supplied Frutiger face has no arrow glyph.
        detailText.text = (detail ?? string.Empty).Replace("→","to");
        detailText.color = detailColor;
        detailText.fontStyle = FontStyles.Bold;
        promptText.text = prompt;
        Action=resources.Action;
        bool process=Action==InteractionActionKind.Process;
        bool build=Action==InteractionActionKind.Build;
        var definition=FindDefinition(build ? resources.Source : resources.Target);
        if(resourceIcon!=null) { resourceIcon.sprite=definition!=null ? definition.Icon : null; resourceIcon.enabled=resourceIcon.sprite!=null; }
        var input=FindDefinition(resources.Source);
        if(sourceIcon!=null) { sourceIcon.sprite=input!=null ? input.Icon : null; sourceIcon.enabled=process && sourceIcon.sprite!=null; }
        if(sourceSlot!=null) sourceSlot.SetActive(process);
        if(recipeArrow!=null) recipeArrow.SetActive(process);
        if(resourceSummary!=null) resourceSummary.SetActive(Action!=InteractionActionKind.None);
        if(sourceAmount!=null) sourceAmount.text=resources.SourceAmount.ToString();
        if(resultAmount!=null) resultAmount.text=(build ? resources.SourceAmount : resources.TargetAmount).ToString();
        if(resultCaption!=null) resultCaption.text=process ? "Output" : build ? "Consume" : "Gathered";
        if(fallbackIcon!=null) fallbackIcon.SetActive(!process && !build);
        if(processAction!=null) processAction.SetActive(process);
        if(buildAction!=null) buildAction.SetActive(build);
        visuals.SetActive(true);
    }

    private ResourceDefinition FindDefinition(ResourceType? type) => type.HasValue && definitions!=null
        ? definitions.Definitions.FirstOrDefault(d=>d!=null && d.ResourceType==type.Value) : null;

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
        Action=InteractionActionKind.None;
        if(sourceIcon!=null) { sourceIcon.sprite=null; sourceIcon.enabled=false; }
        if(resourceSummary!=null) resourceSummary.SetActive(false);
        if(resourceIcon!=null) { resourceIcon.sprite=null; resourceIcon.enabled=false; }
        objectNameText.text = string.Empty;
        detailText.text = string.Empty;
        promptText.text = string.Empty;
        HideProgress();
        visuals.SetActive(false);
    }
}
