using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerOfBabel.Interface
{
    /// <summary>Pointer-following glass light for buttons and draggable or scrollable surfaces.</summary>
    [DisallowMultipleComponent]
    public sealed class AeroHoverFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
    {
        [SerializeField] private AeroGlassGeometry surface;
        [SerializeField] private bool trackScreenPointer;
        private UnityEngine.UI.Selectable selectable;
        private UnityEngine.UI.ScrollRect scroll;
        private bool inside;
        private Vector2 origin=new(.5f,.5f);
        private bool originChanged;
        public float Strength { get; private set; }
        public AeroGlassGeometry Surface => surface;
        public bool TracksBackground => trackScreenPointer;

        private void Awake() => Resolve();
        private void Resolve()
        {
            if(surface==null) surface=GetComponent<AeroGlassGeometry>();
            selectable=GetComponent<UnityEngine.UI.Selectable>();
            scroll=GetComponent<UnityEngine.UI.ScrollRect>();
        }
        public void OnPointerEnter(PointerEventData data) { inside=true; Track(data); }
        public void OnPointerMove(PointerEventData data) => Track(data);
        public void OnPointerExit(PointerEventData data) => inside=false;
        private void Track(PointerEventData data)
        {
            Track(data.position,data.enterEventCamera);
        }
        private void Track(Vector2 screenPosition,Camera camera)
        {
            if(surface==null) Resolve();
            if(surface==null) return;
            var rect=(RectTransform)surface.transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,screenPosition,camera,out var point))
            {
                var next=new Vector2(Mathf.InverseLerp(rect.rect.xMin,rect.rect.xMax,point.x),Mathf.InverseLerp(rect.rect.yMin,rect.rect.yMax,point.y));
                if((next-origin).sqrMagnitude>.000001f) { origin=next; originChanged=true; }
            }
        }
        private void Update()
        {
            if(trackScreenPointer && surface!=null)
            {
                var mouse=UnityEngine.InputSystem.Mouse.current;
                var canvas=surface.GetComponentInParent<Canvas>();
                var camera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                inside=mouse!=null && RectTransformUtility.RectangleContainsScreenPoint((RectTransform)surface.transform,mouse.position.ReadValue(),camera);
                if(inside) Track(mouse.position.ReadValue(),camera);
            }
            bool enabledControl=(selectable==null || selectable.IsInteractable()) && (scroll==null || scroll.isActiveAndEnabled);
            float target=inside && enabledControl ? 1 : 0;
            float next=Mathf.MoveTowards(Strength,target,Time.unscaledDeltaTime/(target>Strength ? .12f : .18f));
            if(!Mathf.Approximately(next,Strength) || originChanged)
            { Strength=next; surface?.SetHover(Strength,origin); originChanged=false; }
        }
        private void OnDisable()
        { inside=false; Strength=0; originChanged=false; surface?.SetHover(0,origin); }
    }
}
