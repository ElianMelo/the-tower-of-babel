using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerOfBabel.Interface
{
    [RequireComponent(typeof(UnityEngine.UI.ScrollRect))]
    public sealed class UpgradeTreePan : MonoBehaviour, IBeginDragHandler
    {
        [SerializeField] private UpgradeTooltip tooltip;
        public void OnBeginDrag(PointerEventData data) => tooltip?.Hide();
    }
}
