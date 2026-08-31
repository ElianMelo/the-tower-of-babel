using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerOfBabel
{
    public class OptionButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject hoverEffect;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text optionText;

        // todo: refactor when implement upgrade system
        private bool isUnlocked = true;

        private void Start()
        {
            button.interactable = isUnlocked;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ToggleHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ToggleHover(false);
        }

        public void ToggleHover(bool isActive)
        {
            hoverEffect.SetActive(isActive);
        }
    }
}
