using UnityEngine;
using UnityEngine.EventSystems;

namespace TowerOfBabel.Interface
{
    /// <summary>Small springy click response, using vertex data so glass materials stay shared.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(UnityEngine.UI.Button))]
    public sealed class AeroButtonJuice : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField, Range(.15f,.6f)] private float duration = .34f;
        private UnityEngine.UI.Button button;
        private AeroGlassGeometry glass;
        private Vector3 restScale;
        private Quaternion restRotation;
        private Vector2 origin = new(.5f,.5f);
        private float elapsed;
        public bool IsPulsing { get; private set; }

        private void Awake() { button=GetComponent<UnityEngine.UI.Button>(); glass=GetComponent<AeroGlassGeometry>(); }
        private void OnEnable()
        {
            if(button==null) button=GetComponent<UnityEngine.UI.Button>();
            restScale=transform.localScale; restRotation=transform.localRotation;
            button.onClick.AddListener(Pulse);
        }
        private void OnDisable()
        {
            if(button!=null) button.onClick.RemoveListener(Pulse);
            ResetFeedback();
        }
        public void OnPointerDown(PointerEventData data)
        {
            var rect=(RectTransform)transform;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out var point))
                origin=new Vector2(Mathf.InverseLerp(rect.rect.xMin,rect.rect.xMax,point.x),Mathf.InverseLerp(rect.rect.yMin,rect.rect.yMax,point.y));
        }
        public void Pulse()
        {
            if(button==null || !button.IsInteractable() || !isActiveAndEnabled) return;
            elapsed=0; IsPulsing=true;
            if(glass==null) glass=GetComponent<AeroGlassGeometry>();
            glass?.SetFeedback(1,origin);
        }
        private void Update()
        {
            if(!IsPulsing) return;
            elapsed+=Time.unscaledDeltaTime;
            float t=Mathf.Clamp01(elapsed/duration), envelope=(1-t)*(1-t);
            transform.localRotation=restRotation*Quaternion.Euler(0,0,Mathf.Sin(t*25)*.85f*envelope);
            transform.localScale=restScale*(1-Mathf.Sin(t*16)*.018f*envelope);
            glass?.SetFeedback(1-t,origin);
            if(t>=1) ResetFeedback();
        }
        private void ResetFeedback()
        {
            if(IsPulsing) { transform.localScale=restScale; transform.localRotation=restRotation; }
            IsPulsing=false; glass?.SetFeedback(0,origin);
        }
    }
}
