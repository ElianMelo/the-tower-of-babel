using System;
using System.Globalization;
using TMPro;
using TowerOfBabel.Upgrades;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TowerOfBabel
{
    public enum UpgradeButtonState : byte
    {
        CannotBuy,
        CanBuy,
        Purchased
    }

    public sealed class UpgradeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject hoverEffect;
        [SerializeField] private Image hoverEffectImage;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text upgradeText;

        [SerializeField] private Color canBuy;
        [SerializeField] private Color cannotBuy;
        [SerializeField] private Color alreadyBrought;

        private UpgradeData upgradeData;
        private UpgradeButtonState state;
        private Action selectionCallback;
        private bool listenerRegistered;

        public UpgradeData Data => upgradeData;
        public UpgradeButtonState State => state;
        public string Label => upgradeText != null ? upgradeText.text : string.Empty;
        public bool IsInteractable => button != null && button.interactable;

        private void Awake()
        {
            EnsureListener();
            ApplyVisualState(false);
        }

        private void OnDestroy()
        {
            if (listenerRegistered && button != null)
                button.onClick.RemoveListener(HandleSelected);
        }

        public void Configure(UpgradeData data, Action onSelected, UpgradeButtonState buttonState)
        {
            EnsureListener();
            upgradeData = data;
            selectionCallback = onSelected;
            SetLabel(FormatLabel(data));
            SetState(buttonState);
        }

        public void SetupUpgradeData(UpgradeData data)
        {
            upgradeData = data;
            SetLabel(FormatLabel(data));
        }

        private static string FormatLabel(UpgradeData data)
        {
            if (data == null)
                return string.Empty;

            // Existing display names include an authored amount after the title.
            // Keep the title, but derive the amount from the gameplay data.
            string title = string.IsNullOrWhiteSpace(data.DisplayName)
                ? data.IsLevelFiftyUpgrade ? "Level 50" : data.EffectType.ToString()
                : data.DisplayName.Split('\n')[0].TrimEnd('\r');
            float displayedValue = data.EffectType == UpgradeEffectType.Efficiency
                ? -data.Value : data.Value;
            string amount = displayedValue.ToString("+0.###;-0.###;0", CultureInfo.InvariantCulture);
            if (data.EffectType == UpgradeEffectType.Efficiency)
                amount += "s";

            return data.IsLevelFiftyUpgrade
                ? $"{title}\n{data.EffectType} {amount}"
                : $"{title}\n{amount}";
        }

        public void SetState(UpgradeButtonState buttonState)
        {
            state = buttonState;
            if (button != null)
                button.interactable = state == UpgradeButtonState.CanBuy;
            ApplyVisualState(false);
        }

        public void SetLabel(string label)
        {
            if (upgradeText != null)
                upgradeText.text = label ?? string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyVisualState(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyVisualState(false);
        }

        public void ToggleHover(bool isActive)
        {
            ApplyVisualState(isActive);
        }

        private void ApplyVisualState(bool pointerInside)
        {
            if (hoverEffect == null)
                return;

            bool purchased = state == UpgradeButtonState.Purchased;
            hoverEffect.SetActive(purchased || pointerInside);
            if (hoverEffectImage == null || !hoverEffect.activeSelf)
                return;

            hoverEffectImage.color = purchased
                ? alreadyBrought
                : state == UpgradeButtonState.CanBuy ? canBuy : cannotBuy;
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
            if (state == UpgradeButtonState.CanBuy)
                selectionCallback?.Invoke();
        }
    }
}
