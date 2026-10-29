//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.VFX;
using MoveRotateResize.Dynamic.FallingRain;
using NaughtyAttributes;
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.Wind
{
    [ExecuteInEditMode]
    public class DynamicWind : MonoBehaviour
    {
        DynamicFallingRain dynamicFallingRain;


        [MarkupAttributes.Foldout("Wind Zone")]
        [SerializeField] WindZone windZone;
        [SerializeField] float windStrength = 12f;


        [MarkupAttributes.Foldout("Randomness")]
        [InfoBox("If not using Random Rotation then manually adjust the Wind Zone gameobject rotation", EInfoBoxType.Normal)]
        [InfoBox("Random Rotation ONLY works in Playmode", EInfoBoxType.Normal)]
        [SerializeField] bool enableRandomRotation = true;      
        [SerializeField] Vector2 rotationAmountRange = new Vector2(-45f, 45f);
        [SerializeField] Vector2 rotationDurationRange = new Vector2(1f, 3f);
        [SerializeField] Vector2 waitTimeRange = new Vector2(1f, 3f);


        [HideInInspector] public VisualEffect vfx;
        Vector3 lastWindDirection;
        float lastWindMain, lastWindPulseFrequency, lastWindPulseMagnitude, lastWindTurbulence;
        Quaternion initialRotation, targetRotation;
        float rotationDuration, rotationTimer, waitTimer;
        bool rotatingForward;
        float lastUpdateTimestamp;
        const float updateInterval = 0.1f;

        static readonly int DirectionPropertyID = Shader.PropertyToID("_WINDZONE_Direction");
        static readonly int MainPropertyID = Shader.PropertyToID("_WINDZONE_Main");
        static readonly int TurbulencePropertyID = Shader.PropertyToID("_WINDZONE_Turbulence");
        static readonly int PulseFrequencyPropertyID = Shader.PropertyToID("_WINDZONE_Pulse_Frequency");
        static readonly int PulseMagnitudePropertyID = Shader.PropertyToID("_WINDZONE_Pulse_Magnitude");
        const string WindDirectionString = "_WIND_DIRECTION";


        void Awake()
        {
            if (windZone == null)
            {
                Debug.LogError("WindZone component not found.");
                return;
            }

            initialRotation = windZone.transform.rotation;
            UpdateLastValues();
            SetNewRotation();
        }

        void Start()
        {
            if (dynamicFallingRain == null)
            {
                CacheGlobalVariables();  
            }

            vfx = dynamicFallingRain.fallingRainVFXGraph;
            if (vfx == null)
            {

            }
        }

        void CacheGlobalVariables() 
        {
            dynamicFallingRain = DynamicSystemsManager.dynamicFallingRain;
        }

        void OnValidate()
        {
            if (dynamicFallingRain == null)
            {
                CacheGlobalVariables();  
            }

            ApplySettingsIfNeeded();

            vfx = dynamicFallingRain?.fallingRainVFXGraph;
            if (vfx == null)
            {
                
            }
        }

        void Update()
        {
            if (enableRandomRotation && Application.isPlaying)
            {
                RotateBackAndForth();
            }

            if (Time.time - lastUpdateTimestamp > updateInterval)
            {
                ApplySettingsIfNeeded();
                UpdateVFXDirection();
                lastUpdateTimestamp = Time.time;
            }
        }

        void ApplySettingsIfNeeded()
        {
            if (windZone == null) return;

            var currentWindDirection = windZone.transform.forward;
            var currentWindMain = windZone.windMain;
            var currentWindPulseFrequency = windZone.windPulseFrequency;
            var currentWindPulseMagnitude = windZone.windPulseMagnitude;
            var currentWindTurbulence = windZone.windTurbulence;

            bool hasChanged = currentWindDirection != lastWindDirection ||
                              Mathf.Abs(currentWindMain - lastWindMain) > Mathf.Epsilon ||
                              Mathf.Abs(currentWindPulseFrequency - lastWindPulseFrequency) > Mathf.Epsilon ||
                              Mathf.Abs(currentWindPulseMagnitude - lastWindPulseMagnitude) > Mathf.Epsilon ||
                              Mathf.Abs(currentWindTurbulence - lastWindTurbulence) > Mathf.Epsilon;

            if (hasChanged)
            {
                Shader.SetGlobalVector(DirectionPropertyID, currentWindDirection);
                Shader.SetGlobalFloat(MainPropertyID, currentWindMain);
                Shader.SetGlobalFloat(PulseFrequencyPropertyID, currentWindPulseFrequency);
                Shader.SetGlobalFloat(PulseMagnitudePropertyID, currentWindPulseMagnitude);
                Shader.SetGlobalFloat(TurbulencePropertyID, currentWindTurbulence);

                lastWindDirection = currentWindDirection;
                lastWindMain = currentWindMain;
                lastWindPulseFrequency = currentWindPulseFrequency;
                lastWindPulseMagnitude = currentWindPulseMagnitude;
                lastWindTurbulence = currentWindTurbulence;
            }
        }

        void RotateBackAndForth()
        {
            if (windZone == null) return;

            if (rotationTimer < rotationDuration)
            {
                rotationTimer += Time.deltaTime;
                float progress = rotationTimer / rotationDuration;
                windZone.transform.rotation = Quaternion.Lerp(
                    rotatingForward ? initialRotation : targetRotation,
                    rotatingForward ? targetRotation : initialRotation,
                    progress);

                if (progress >= 1f)
                {
                    rotationTimer = 0f;
                    rotatingForward = !rotatingForward;
                    waitTimer = Random.Range(waitTimeRange.x, waitTimeRange.y);
                }
            }
            else
            {
                waitTimer -= Time.deltaTime;
                if (waitTimer <= 0f)
                {
                    SetNewRotation();
                }
            }
        }

        void SetNewRotation()
        {
            float targetRotationAmount = Random.Range(rotationAmountRange.x, rotationAmountRange.y);
            targetRotation = initialRotation * Quaternion.Euler(0, targetRotationAmount, 0);
            rotationDuration = Random.Range(rotationDurationRange.x, rotationDurationRange.y);
        }

        void UpdateLastValues()
        {
            if (windZone == null) return;

            lastWindDirection = windZone.transform.forward;
            lastWindMain = windZone.windMain;
            lastWindPulseFrequency = windZone.windPulseFrequency;
            lastWindPulseMagnitude = windZone.windPulseMagnitude;
            lastWindTurbulence = windZone.windTurbulence;
        }

       void UpdateVFXDirection()
        {
            if (windZone == null) return; 

            Vector3 forwardDirection = windZone.transform.forward * windStrength;

            if (vfx != null)
            {
                vfx.SetVector3(WindDirectionString, forwardDirection);
            }

            Shader.SetGlobalVector(WindDirectionString, forwardDirection);
        }
    }
}