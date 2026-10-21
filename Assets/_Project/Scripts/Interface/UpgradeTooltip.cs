using System.Globalization;
using TMPro;
using TowerOfBabel.Upgrades;
using UnityEngine;

namespace TowerOfBabel.Interface
{
    public sealed class UpgradeTooltip : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text description;
        [SerializeField] private TMP_Text status;
        private RectTransform rect;
        private UpgradeButton owner;
        public bool IsVisible => group!=null && group.alpha>0;
        public string Description => description!=null ? description.text : string.Empty;
        private void Awake() { rect=(RectTransform)transform; Hide(); }
        private void OnDisable() => Hide();
        public void Show(UpgradeButton source,Vector2 screenPosition)
        {
            if(source==null || source.Data==null) return;
            owner=source;
            if(rect==null) rect=(RectTransform)transform;
            title.text=source.Data.DisplayName.Split('\n')[0];
            description.text=Describe(source.Data);
            status.text=source.State==UpgradeButtonState.Purchased ? "OWNED  •  Permanent bonus" :
                source.State==UpgradeButtonState.CanBuy ? "AVAILABLE  •  Click to unlock" : "REVEALED  •  Earn an upgrade point";
            group.alpha=1; group.blocksRaycasts=false; group.interactable=false;
            transform.SetAsLastSibling();
            Move(screenPosition);
        }
        public void Move(Vector2 screenPosition)
        {
            if(!IsVisible) return;
            var parent=(RectTransform)rect.parent;
            var canvas=GetComponentInParent<Canvas>();
            var camera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screenPosition,camera,out var local);
            Vector2 size=rect.rect.size;
            rect.anchoredPosition=new Vector2(Mathf.Clamp(local.x+20,parent.rect.xMin+12,parent.rect.xMax-size.x-12),
                Mathf.Clamp(local.y-18,parent.rect.yMin+size.y+12,parent.rect.yMax-12));
        }
        public void Hide(UpgradeButton source=null)
        {
            if(source!=null && source!=owner) return;
            owner=null; if(group!=null) group.alpha=0;
        }
        public static string Describe(UpgradeData data)
        {
            string value=Mathf.Abs(data.Value).ToString("0.###",CultureInfo.InvariantCulture);
            string effect=data.EffectType switch
            {
                UpgradeEffectType.Efficiency => $"Reduces action time by {value} seconds.",
                UpgradeEffectType.Cost => data.Value<0 ? $"Reduces resource cost by {value}." : $"Increases resource cost by {value}.",
                _ => $"Increases production by {value} per action."
            };
            string unlock=data.IsLevelFiftyUpgrade ? "Capstone: requires the preceding skill." :
                data.Row==3 && data.Column==3 ? "Starting skill for this profession." : "Revealed by purchasing an adjacent skill.";
            return $"{data.EffectType}\n{effect}\n\nCost: 1 upgrade point\n{unlock}";
        }
    }
}
