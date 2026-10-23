using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerOfBabel.Interface
{
    /// <summary>Liquid press/release feedback with a buoyant icon and a stationary hit rectangle.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class AeroButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        [SerializeField, Range(.15f,.6f)] private float duration = .34f;
        [SerializeField] private RectTransform icon;
        private UnityEngine.UI.Button button;
        private AeroGlassGeometry glass;
        private AeroHoverFeedback hover;
        private Vector3 restScale;
        private Quaternion restRotation;
        private Vector2 restPosition;
        private Vector2 origin = new(.5f,.5f);
        private float elapsed;
        private bool pressed, celebrating;
        private bool iconInitialized;
        public bool IsPulsing { get; private set; }

        private void Awake() => Resolve();
        private void Resolve()
        {
            button=GetComponent<UnityEngine.UI.Button>(); glass=GetComponent<AeroGlassGeometry>(); hover=GetComponent<AeroHoverFeedback>();
        }
        private void OnEnable()
        {
            Resolve();
            CaptureIcon();
            button.onClick.AddListener(Pulse);
        }
        private void CaptureIcon()
        {
            if(icon==null || iconInitialized) return;
            iconInitialized=true; restScale=icon.localScale; restRotation=icon.localRotation; restPosition=icon.anchoredPosition;
        }
        private void OnDisable()
        {
            if(button!=null) button.onClick.RemoveListener(Pulse);
            ResetFeedback();
        }
        public void OnPointerDown(PointerEventData data)
        {
            if(button==null || !button.IsInteractable() || data.button!=PointerEventData.InputButton.Left) return;
            pressed=true;
            var rect=(RectTransform)transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out var point))
                origin=new Vector2(Mathf.InverseLerp(rect.rect.xMin,rect.rect.xMax,point.x),Mathf.InverseLerp(rect.rect.yMin,rect.rect.yMax,point.y));
            glass?.SetFeedback(.6f,origin);
        }
        public void OnPointerUp(PointerEventData data) { if(pressed) { pressed=false; elapsed=0; IsPulsing=true; } }
        public void OnPointerExit(PointerEventData data) { if(pressed) { pressed=false; elapsed=0; IsPulsing=true; } }
        public void Pulse()
        {
            if(button==null || !button.IsInteractable() || !isActiveAndEnabled) return;
            elapsed=0; IsPulsing=true; celebrating=false; pressed=false;
            if(glass==null) glass=GetComponent<AeroGlassGeometry>();
            glass?.SetFeedback(1,origin);
            AeroUIAudio.Play(AeroUICue.Press);
        }
        public void Celebrate()
        {
            if(!isActiveAndEnabled) return;
            Resolve(); elapsed=0; IsPulsing=true; celebrating=true; pressed=false; origin=new(.5f,.5f);
            glass?.SetFeedback(1,origin);
            AeroUIAudio.Play(AeroUICue.Success);
        }
        private void Update()
        {
            Advance(Time.unscaledDeltaTime,Time.unscaledTime);
        }
        private void Advance(float dt,float clock)
        {
            if(button==null) Resolve();
            if(button==null || !button.IsInteractable() && !celebrating) { ResetFeedback(); return; }
            float t=1;
            if(IsPulsing)
            {
                elapsed+=dt; t=Mathf.Clamp01(elapsed/duration);
                glass?.SetFeedback(1-t,origin);
                if(t>=1) { IsPulsing=false; celebrating=false; }
            }
            else if(pressed) glass?.SetFeedback(.6f,origin);
            float light=hover!=null ? hover.Strength : 0;
            float buoyancy=Mathf.Sin(t*Mathf.PI)*Mathf.Exp(-t*3);
            if(icon!=null)
            {
                icon.anchoredPosition=restPosition+new Vector2(Mathf.Sin(clock*1.4f)*.45f*light,2.5f*light+buoyancy*3);
                icon.localScale=restScale*(pressed ? .94f : 1+light*.035f+buoyancy*.08f);
                icon.localRotation=restRotation*Quaternion.Euler(0,0,Mathf.Sin(clock*1.2f)*1.2f*light);
            }
        }
        private void ResetFeedback()
        {
            if(icon!=null && iconInitialized) { icon.localScale=restScale; icon.localRotation=restRotation; icon.anchoredPosition=restPosition; }
            IsPulsing=false; pressed=celebrating=false; glass?.SetFeedback(0,origin);
        }
#if UNITY_EDITOR
        public void SampleEditorPulse(float seconds, float clock = -1)
        {
            if(Application.isPlaying) return;
            Resolve();
            CaptureIcon();
            elapsed=0; IsPulsing=true; Advance(seconds,clock >= 0 ? clock : seconds);
        }
#endif
    }
}
