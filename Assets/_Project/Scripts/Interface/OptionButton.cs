using System;
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

        private Action selectionCallback;
        private bool listenerRegistered;

        public string Label => optionText != null ? optionText.text : string.Empty;
        public bool IsInteractable => button != null && button.interactable;

        private void Awake()
        {
            EnsureListener();
            ToggleHover(false);
        }

        private void OnDestroy()
        {
            if (listenerRegistered && button != null)
                button.onClick.RemoveListener(HandleSelected);
        }

        public void Configure(string label, Action onSelected, bool interactable = true)
        {
            EnsureListener();
            selectionCallback = onSelected;
            SetLabel(label);
            SetInteractable(interactable);
            ToggleHover(false);
        }

        public void SetLabel(string label)
        {
            if (optionText != null)
                optionText.text = label ?? string.Empty;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
            if (!interactable)
                ToggleHover(false);
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
            if (hoverEffect != null)
                hoverEffect.SetActive(isActive && IsInteractable);
        }

        private void EnsureListener()
        {
            if (listenerRegistered || button == null)
                return;

            button.onClick.AddListener(HandleSelected);
            listenerRegistered = true;
        }

        private void HandleSelected()
        {
            if (IsInteractable)
                selectionCallback?.Invoke();
        }
    }
}
