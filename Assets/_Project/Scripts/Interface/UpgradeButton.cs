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

    public sealed class UpgradeButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private GameObject hoverEffect;
        [SerializeField] private Image hoverEffectImage;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text upgradeText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Image stateSurface;
        [SerializeField] private Material availableMaterial;
        [SerializeField] private Material lockedMaterial;
        [SerializeField] private Material purchasedMaterial;
        [SerializeField] private UnityEngine.UI.Outline availableOutline;

        [SerializeField] private Color canBuy;
        [SerializeField] private Color cannotBuy;
        [SerializeField] private Color alreadyBrought;

        private UpgradeData upgradeData;
        private UpgradeButtonState state;
        private Action selectionCallback;
        private bool listenerRegistered;
        private bool hasState;
        private TowerOfBabel.Interface.UpgradeTooltip tooltip;
        public void BindTooltip(TowerOfBabel.Interface.UpgradeTooltip value) => tooltip=value;
        private void OnDisable() => tooltip?.Hide(this);

        private void Update()
        {
            if (state == UpgradeButtonState.CanBuy && availableOutline != null)
                availableOutline.effectColor = new Color(.65f, .96f, 1f, .7f + .25f * Mathf.Sin(Time.unscaledTime * 4f));
        }

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
            hasState = false;
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
            bool purchasedNow = hasState && state == UpgradeButtonState.CanBuy && buttonState == UpgradeButtonState.Purchased;
            state = buttonState;
            hasState = true;
            if (button != null)
                button.interactable = state == UpgradeButtonState.CanBuy;
            ApplyVisualState(false);
            if (purchasedNow) GetComponent<TowerOfBabel.Interface.AeroButtonJuice>()?.Celebrate();
        }

        public void SetLabel(string label)
        {
            if (upgradeText != null)
                upgradeText.text = label ?? string.Empty;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            ApplyVisualState(true);
            tooltip?.Show(this,eventData.position);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ApplyVisualState(false);
            tooltip?.Hide(this);
        }

        public void OnPointerMove(PointerEventData eventData) => tooltip?.Move(eventData.position);

        public void ToggleHover(bool isActive)
        {
            ApplyVisualState(isActive);
        }

        private void ApplyVisualState(bool pointerInside)
        {
            bool available = state == UpgradeButtonState.CanBuy;
            if (availableOutline != null) availableOutline.enabled = available;
            if (upgradeText != null) upgradeText.color = available ? Color.white : new Color(.035f,.22f,.34f,1);
            if (stateText != null) stateText.color = available ? Color.white : new Color(.14f,.37f,.47f,1);
            if (stateSurface != null && availableMaterial != null)
                stateSurface.material = available ? availableMaterial : state == UpgradeButtonState.Purchased ? purchasedMaterial : lockedMaterial;
            if (stateText != null)
                stateText.text = state == UpgradeButtonState.Purchased ? "OWNED" :
                    state == UpgradeButtonState.CanBuy ? "1 POINT" : "LOCKED";
            if (stateSurface != null)
                stateSurface.color = state == UpgradeButtonState.Purchased ? new Color(.38f, .94f, .55f, .95f) :
                    state == UpgradeButtonState.CanBuy ? Color.white : new Color(.8f, .94f, 1f, .72f);
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
