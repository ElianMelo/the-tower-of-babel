using TMPro;
using UnityEngine;

namespace TowerOfBabel
{
    [DisallowMultipleComponent]
    public class InformationUI : MonoBehaviour
    {
        [SerializeField] private GameObject visuals;
        [SerializeField] private TMP_Text fpsCounter;
        [SerializeField, Min(0.05f)] private float refreshInterval = 0.25f;

        private float elapsedTime;
        private int elapsedFrames;

        public float CurrentFps { get; private set; }

        private void OnEnable()
        {
            elapsedTime = 0f;
            elapsedFrames = 0;
            CurrentFps = 0f;

            visuals?.SetActive(true);
            fpsCounter?.SetText("FPS: --");
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            if (deltaTime <= 0f)
                return;

            elapsedTime += deltaTime;
            elapsedFrames++;
            if (elapsedTime < refreshInterval)
                return;

            CurrentFps = elapsedFrames / elapsedTime;
            fpsCounter?.SetText("FPS: {0:0}", CurrentFps);
            elapsedTime = 0f;
            elapsedFrames = 0;
        }

        private void OnValidate()
        {
            refreshInterval = Mathf.Max(0.05f, refreshInterval);
        }
    }
}
