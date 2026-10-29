//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.Dynamic.LightFlicker
{
    [ExecuteInEditMode]
    public class DynamicLightFlicker : MonoBehaviour
    {
        Light hdrpLight;
        [SerializeField] bool enableInEditor = false;
        [SerializeField] Vector2 intensityRange = new Vector2(25f, 50f);
        [SerializeField] Vector2 flickerSpeedRange = new Vector2(0.1f, 0.4f);

        float targetIntensity;
        float startIntensity;
        float lerpTimer;
        float currentFlickerSpeed;

        void Start()
        {
            InitializeLight();
        }

        void OnEnable()
        {
            InitializeLight();
        }

        void InitializeLight()
        {
            hdrpLight = GetComponent<Light>();
            if (hdrpLight == null)
            {
                Debug.LogError("LightFlicker script requires a Light component on the same GameObject.");
                return;
            }

            startIntensity = hdrpLight.intensity;
            targetIntensity = Random.Range(intensityRange.x, intensityRange.y);
            currentFlickerSpeed = Random.Range(flickerSpeedRange.x, flickerSpeedRange.y);
            lerpTimer = 0f;
        }

        void Update()
        {
            if (!Application.isPlaying && !enableInEditor) return;
            if (hdrpLight == null) return;

            lerpTimer += Time.deltaTime;

            float t = Mathf.Clamp01(lerpTimer / currentFlickerSpeed);
            hdrpLight.intensity = Mathf.Lerp(startIntensity, targetIntensity, t);

            if (lerpTimer >= currentFlickerSpeed)
            {
                lerpTimer = 0f;
                startIntensity = targetIntensity;
                targetIntensity = Random.Range(intensityRange.x, intensityRange.y);
                currentFlickerSpeed = Random.Range(flickerSpeedRange.x, flickerSpeedRange.y);
            }
        }
    }
}