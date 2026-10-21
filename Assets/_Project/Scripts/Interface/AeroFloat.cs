using UnityEngine;

namespace TowerOfBabel.Interface
{
    [DisallowMultipleComponent]
    public sealed class AeroFloat : MonoBehaviour
    {
        private RectTransform rect;
        private Vector2 rest;
        private float phase;
        private void OnEnable()
        {
            rect=(RectTransform)transform; rest=rect.anchoredPosition; phase=Time.unscaledTime;
        }
        private void LateUpdate()
        {
            float t=Time.unscaledTime-phase;
            rect.anchoredPosition=rest+new Vector2(Mathf.Sin(t*.85f)*1.1f,Mathf.Sin(t*1.35f)*4f);
        }
        private void OnDisable() { if(rect!=null) rect.anchoredPosition=rest; }
    }
}
