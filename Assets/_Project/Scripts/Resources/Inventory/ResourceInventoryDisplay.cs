using TMPro;
using TowerOfBabel.Resources;
using UnityEngine;
using UnityEngine.UI;

namespace TowerOfBabel
{
    public class ResourceInventoryDisplay : MonoBehaviour
    {
        [SerializeField] private Image resourceIcon;
        [SerializeField] private TMP_Text resourceName;
        [SerializeField] private TMP_Text resourceAmount;

        public ResourceType ResourceType { get; private set; }

        public void Bind(ResourceDefinition definition, int amount)
        {
            ResourceType = definition.ResourceType;
            resourceIcon.sprite = definition.Icon;
            resourceIcon.enabled = definition.Icon != null;
            resourceName.text = definition.DisplayName;
            var feedback = GetComponent<TowerOfBabel.Interface.AeroValueFeedback>();
            if (feedback != null) feedback.SetAmount(amount, true);
            else SetAmount(amount);
        }

        public void SetAmount(int amount)
        {
            var feedback = GetComponent<TowerOfBabel.Interface.AeroValueFeedback>();
            if (feedback != null) feedback.SetAmount(amount);
            else resourceAmount.text = Mathf.Max(0, amount).ToString();
        }
    }
}
