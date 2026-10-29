//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
#if USING_HDRP
using UnityEngine.Rendering.HighDefinition;
#elif USING_URP
using UnityEngine.Rendering.Universal;
#endif
#if ENVIRO_HDRP || ENVIRO_3
using Enviro;
#endif
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.DrippingRain;
using NaughtyAttributes;

namespace MoveRotateResize.Dynamic.RainWetness
{
    public enum MainRainAudioType
    {
        Light,
        Medium,
        Heavy
    }

    [ExecuteInEditMode]
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(DynamicSystemsManager))]
    public class DynamicRainWetness : MonoBehaviour 
    {  
        public static DynamicRainWetness Instance { get; private set; }

        GameObject player;
        AudioSource mainRainAudioSource;
        DynamicDrippingRain dynamicDrippingRain;


        [MarkupAttributes.Foldout("Global Sliders and Durations")]
        [Range(0f, 1f)] public float globalRain = 1f; 
        [Range(0f, 1f)] public float globalRainExtended = 1f;
        [HideInInspector][Range(0f, 1f)] public float currentGlobalRain;
        float targetRainValue;
        [HideInInspector][Range(0f, 1f)] public float currentGlobalRainExtended;
        float targetRainExtenderValue;
        [SerializeField] float globalRainTransitionDuration = 10f;
        [SerializeField] float globalRainExtendedTransitionDuration = 50f;
        bool isRainOn = false;


        [MarkupAttributes.Foldout("Global Rain Shader Appearance")]
        [SerializeField] Vector2 globalRainStreakSize = new Vector2(2.59f, 0.21f);
        [SerializeField] float globalRainStreakDistortion = 0.05f;
        [SerializeField] float globalRainStreakSpeed = 1f;
        [SerializeField] float globalRainDropSize = 1f;
        [SerializeField] float globalRainDropSpeed = 0.2f;
        float transitionStartTime;
        float transitionStartTimeWetnessExtended;


        [MarkupAttributes.Foldout("Audio- Rain")]
        [SerializeField] public float mainRainInsideVolume  = 0.2f;
        public float mainRainOutsideVolume  = 1f;
        public float mainRainAudioTransitionDuration = 2.0f;
        AudioClip lightRainClip;
        AudioClip mediumRainClip;
        [SerializeField]  AudioClip heavyRainClip;
        float transitionTime = 1f;
        [HideInInspector]public bool isPlayerOccluded = false;
        #pragma warning disable CS0414
        MainRainAudioType rainIntensity = MainRainAudioType.Heavy;
        #pragma warning restore CS0414


        [MarkupAttributes.Foldout("VFX- Fog")]
        [SerializeField] Volume globalFog;
        VolumeProfile volumeProfileHDRP;
#if USING_HDRP
        Fog fog;
#endif
        [SerializeField] float fogDensityNoRain = 1500f; 
        [SerializeField] float fogDensityRaining = 500f; 

        
        [MarkupAttributes.Foldout("Enviro 3- Integration")]
        public bool enableEnviroIntegration = false;
        [InfoBox("The Controls for Enviro's Global Transition Durations are in the Enviro Systems gameobject under... Weather/Transition Controls.", EInfoBoxType.Warning)]
        [InfoBox("Please use that section to control that setting instead of using our Global Rain Transtion Duration.", EInfoBoxType.Warning)]
        [InfoBox("Please use our 'enviroGlobalRainExtendedTransitionDuration' variable to control the Global Rain Extended Duration when using Enviro.", EInfoBoxType.Warning)]
        [Range(0f, 1f)][HideInInspector] public float enviroIntegrationCurrentWetness;
#if ENVIRO_HDRP || ENVIRO_3
        [SerializeField] float enviroGlobalRainExtendedTransitionDuration = 3f;
        EnviroWeatherType weatherType;
        float enviroCurrentWetness;
        Coroutine extendedRainTransitionCoroutine;
#endif

        [MarkupAttributes.Foldout("UI")]
        [SerializeField] GameObject toggleUI;
        [SerializeField] KeyCode toggleRainKey = KeyCode.F1;
        [SerializeField] KeyCode toggleUIKey = KeyCode.F2;
        bool isToggledUI = true;
        

        const string GlobalRainString = "_Global_Rain";
        const string globalRainExtendedString = "_Global_Rain_Extended";
        const string globalRainStreakSizeString = "_Global_Rain_Streak_Size";
        const string globalRainStreakDistortionStrengthString = "_Global_Rain_Streak_Distortion_Strength";
        const string globalRainStreakSpeedString = "_Global_Rain_Streak_Speed";
        const string globalRainNewDropSizeString = "_Global_Rain_Drop_Size";
        const string globalRainDropSpeedString = "_Global_Rain_Drop_Speed";


        private float previousDPadX = 0;
        private float previousDPadY = 0;

        void OnValidate() 
        {
            #if USING_HDRP
                GetGlobalFogPostProcessingVolume();
            #else
                Debug.LogWarning("USING_HDRP is not defined! Please Perform the First Time Setup as per the Install Section of the Manual.");
            #endif
        }

        void Start()
        { 
            isRainOn = globalRain == 1f;
            CacheGlobalVariables();
             #if USING_HDRP
                GetGlobalFogPostProcessingVolume();
            #else
                Debug.LogWarning("USING_HDRP is not defined! Please Perform the First Time Setup as per the Install Section of the Manual.");
            #endif

            if (mainRainAudioSource != null && !enableEnviroIntegration)
            {
                PlayRainSound(rainIntensity);
                mainRainAudioSource.volume = globalRain;
            }

            InspectorSliderGlobalRainStreakSize();
            InspectorSliderGlobalRainStreakSpeed();
            InspectorSliderGlobalRainStreakDistortionStrength();
            InspectorSliderGlobalRainNewDropSize();
            InspectorSliderGlobalRainDropSpeed();
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player;
            mainRainAudioSource = DynamicSystemsManager.mainAudioSource;
            dynamicDrippingRain = DynamicSystemsManager.dynamicDrippingRain;
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            float dPadX = Input.GetAxis("DPadX");
            float dPadY = Input.GetAxis("DPadY");

            if (Input.GetKeyDown(toggleRainKey))
            {
                OnRainToggle();
            }
            if (Input.GetKeyDown(toggleUIKey))
            {
                OnUIToggle();
            }       

            if (dPadX != 0 && dPadX != previousDPadX)
            {
                if (dPadX < 0)
                {
                    // Debug.Log("D-Pad Left (Press)");
                }
                else if (dPadX > 0)
                {
                    OnUIToggle();
                    // Debug.Log("D-Pad Right (Press)");
                }
            }
            if (dPadY != 0 && dPadY != previousDPadY)
            {
                if (dPadY > 0)
                {
                    OnRainToggle();
                    // Debug.Log("D-Pad Up (Press)");
                }
                else if (dPadY < 0)
                {
                    // Debug.Log("D-Pad Down (Press)");
                }
            }

            previousDPadX = dPadX;
            previousDPadY = dPadY;

            if (enableEnviroIntegration)
            {
                #if ENVIRO_HDRP || ENVIRO_3
                EnviroIntegration();
                mainRainAudioSource.volume = 0f;
                EnviroUpdateRainAudio(); 
                #endif
            }
            else
            {
                UpdateGlobalRain(targetRainValue, globalRainTransitionDuration);
                UpdateGlobalRainExtended(targetRainExtenderValue, globalRainExtendedTransitionDuration);
                UpdateMainRainAudio();
            }

            InspectorSliderGlobalRain();
            InspectorSliderGlobalRainExtender();

#if UNITY_EDITOR
            InspectorSliderGlobalRainStreakSize();
            InspectorSliderGlobalRainStreakSpeed();
            InspectorSliderGlobalRainStreakDistortionStrength();
            InspectorSliderGlobalRainNewDropSize();
            InspectorSliderGlobalRainDropSpeed();
#endif

#if USING_HDRP
            if (fog != null)
            {
                UpdateFog();
            }
#endif
        }

        void InspectorSliderGlobalRain()
        {
            globalRain = Mathf.Clamp(globalRain, 0.0f, 1.0f);

            Shader.SetGlobalFloat(GlobalRainString, globalRain);

            if (dynamicDrippingRain != null)
            {
                //dynamicDrippingRain.SetGlobalRainVFX(globalRain);
            }
            else
            {
                //Debug.LogWarning("dynamicDrippingRain is null. Parameter update skipped.");
            }
        }

        private void InspectorSliderGlobalRainExtender()
        {
            globalRainExtended = Mathf.Clamp(globalRainExtended, 0.0f, 1.0f);
            Shader.SetGlobalFloat(globalRainExtendedString, globalRainExtended);
        }

        // Rain Streaks
        private void InspectorSliderGlobalRainStreakSize()
        {
            Shader.SetGlobalVector(globalRainStreakSizeString, globalRainStreakSize);
        }
        
        private void InspectorSliderGlobalRainStreakSpeed()
        {
            Shader.SetGlobalFloat(globalRainStreakSpeedString, globalRainStreakSpeed);
        }

        // Rain Drops
        private void InspectorSliderGlobalRainStreakDistortionStrength()
        {
            Shader.SetGlobalFloat(globalRainStreakDistortionStrengthString, globalRainStreakDistortion);
        }
        private void InspectorSliderGlobalRainNewDropSize()
        {
            Shader.SetGlobalFloat(globalRainNewDropSizeString, globalRainDropSize);
        }
        private void InspectorSliderGlobalRainDropSpeed()
        {
            Shader.SetGlobalFloat(globalRainDropSpeedString, globalRainDropSpeed);
        }

        public void UpdateGlobalRainExtended(float targetValue, float transitionDuration)
        {
            if (transitionStartTimeWetnessExtended > 0f)
            {
                float elapsedTime = Time.time - transitionStartTimeWetnessExtended;
                float t = Mathf.Clamp01(elapsedTime / transitionDuration);
                float newGlobalWetnessExtenderDuration = Mathf.Lerp(currentGlobalRainExtended, targetValue, t);

                globalRainExtended = newGlobalWetnessExtenderDuration;
                Shader.SetGlobalFloat(globalRainExtendedString, globalRainExtended);

                if (t >= 1.0f)
                {
                    transitionStartTimeWetnessExtended = 0f;
                }
            }
        }

        void HandleEffect(GameObject effect, params GameObject[] otherEffects)
        {
            if (effect != null && !effect.activeSelf)
            {
                effect.SetActive(true);
            }

            foreach (var otherEffect in otherEffects)
            {
                if (otherEffect != null && otherEffect.activeSelf)
                {
                    otherEffect.SetActive(false);
                }
            }
        }

        public void UpdateGlobalRain(float targetValue, float transitionDuration)
        {
            if (transitionStartTime > 0f)
            {
                float elapsedTime = Time.time - transitionStartTime;
                float t = Mathf.Clamp01(elapsedTime / transitionDuration);
                float newGlobalRain = Mathf.Lerp(currentGlobalRain, targetValue, t);

                globalRain = newGlobalRain;
                Shader.SetGlobalFloat(GlobalRainString, globalRain);
                
                if (isPlayerOccluded && globalRain > mainRainInsideVolume ) {
                    float newVolume = Mathf.Lerp(mainRainAudioSource.volume, mainRainInsideVolume , transitionTime * Time.deltaTime);
                    mainRainAudioSource.volume = newVolume;
                } else {
                    mainRainAudioSource.volume = newGlobalRain;
                }
                if (isPlayerOccluded && globalRain < mainRainInsideVolume )
                {
                    float newVolume = Mathf.Lerp(mainRainAudioSource.volume, globalRain, transitionTime * Time.deltaTime);
                    mainRainAudioSource.volume = newVolume;
                }
                
                if (t >= 1.0f)
                {
                    transitionStartTime = 0f;
                }
            }
        }

#if USING_HDRP
        void GetGlobalFogPostProcessingVolume()
        {
            if (globalFog == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Global default volume not assigned. Please assign under Fog in the DYNAMIC Rain Wetness Script.");
                #endif
                return;
            }

            volumeProfileHDRP = globalFog.sharedProfile;

            if (volumeProfileHDRP == null)
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Global volume shared profile is not assigned.");
                #endif
                return;
            }

            if (!volumeProfileHDRP.TryGet<Fog>(out fog))
            {
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                Debug.LogWarning("Fog component not found in the global volume shared profile.");
                #endif
                return;
            }

            UpdateFog();
        }

        void UpdateFog()
        {
            float newFogDensity = Mathf.Lerp(fogDensityNoRain, fogDensityRaining, globalRain);
            if (!Mathf.Approximately(fog.meanFreePath.value, newFogDensity))
            {
                fog.meanFreePath.Override(newFogDensity);
            }
        }
#endif

        public void UpdateMainRainAudio()
        {       
            float interpolationFactor = mainRainAudioTransitionDuration * Time.deltaTime;
           
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                mainRainAudioSource.Stop();
                return;
            }
#endif
            if (isPlayerOccluded && globalRain == 1f)
            {
                float newVolume = Mathf.Lerp(mainRainAudioSource.volume, mainRainInsideVolume , interpolationFactor);
                mainRainAudioSource.volume = newVolume;
            }
            else if (!isPlayerOccluded && globalRain == 1f)
            {
                float newVolume = Mathf.Lerp(mainRainAudioSource.volume, mainRainOutsideVolume , interpolationFactor);
                mainRainAudioSource.volume = newVolume;
            }
        }


        private IEnumerator EnterTriggerUpdateMainRainAudio(AudioSource mainRainAudioSource, float target)
        {
            float elapsedTime = 0f;
            float startValue = mainRainAudioSource.volume;

            while (elapsedTime < mainRainAudioTransitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / mainRainAudioTransitionDuration);
                float newVolume = Mathf.Lerp(startValue, target, t);
                mainRainAudioSource.volume = newVolume;
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                //Debug.Log("Updating audio volume, Value: " + newVolume);
                #endif

                yield return null;
            }

            mainRainAudioSource.volume = target;
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("Audio volume reached the target, Final Value: " + target);
            #endif
        }


        private IEnumerator ExitTriggerUpdateMainRainAudio(AudioSource mainRainAudioSource, float target)
        {
            float elapsedTime = 0f;
            float startValue = mainRainAudioSource.volume;

            while (elapsedTime < mainRainAudioTransitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / mainRainAudioTransitionDuration);
                float newVolume = Mathf.Lerp(startValue, target, t);
                mainRainAudioSource.volume = newVolume;
                #if DEVELOPMENT_BUILD || UNITY_EDITOR
                //Debug.Log("Updating audio volume, Value: " + newVolume);
                #endif

                yield return null;
            }

            mainRainAudioSource.volume = target;
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("Audio volume reached the target, Final Value: " + target);
            #endif
        }

        public void PlayRainSound(MainRainAudioType rainIntensity)
        {
            AudioClip clipToPlay = null;

            switch (rainIntensity)
            {
                case MainRainAudioType.Light:
                    clipToPlay = lightRainClip;
                    break;
                case MainRainAudioType.Medium:
                    clipToPlay = mediumRainClip;
                    break;
                case MainRainAudioType.Heavy:
                    clipToPlay = heavyRainClip;
                    break;
            }

            if (clipToPlay != null && mainRainAudioSource != null)
            {
                if (!mainRainAudioSource.isPlaying || mainRainAudioSource.clip != clipToPlay)
                {
                    mainRainAudioSource.clip = clipToPlay;
                    mainRainAudioSource.Play();
                }
            }
        }

        public void OnUIToggle()
        {
            isToggledUI = !isToggledUI;
            if (isToggledUI)
            {
                toggleUI.SetActive(true);;
            }
            else
            {
                toggleUI.SetActive(false);;
            }
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("UI Toggle");
            #endif
        }
        
        public void OnRainToggle()
        {
            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            Debug.Log("Rain Toggle");
            #endif

            isRainOn = !isRainOn;

            targetRainValue = isRainOn ? 1f : 0f;
            targetRainExtenderValue = isRainOn ? 1f : 0f;

            transitionStartTime = Time.time;
            transitionStartTimeWetnessExtended = Time.time;

            currentGlobalRain = globalRain;
            currentGlobalRainExtended = globalRainExtended;

            UpdateGlobalRain(targetRainValue, globalRainTransitionDuration);
            UpdateGlobalRainExtended(targetRainExtenderValue, globalRainExtendedTransitionDuration);
        }

#if ENVIRO_HDRP || ENVIRO_3
        private void EnviroIntegration()
        {
            enviroCurrentWetness = Enviro.EnviroManager.instance.Environment.Settings.wetness;
            enviroIntegrationCurrentWetness = Mathf.Round(enviroCurrentWetness * 100) / 100;

            if (enableEnviroIntegration)
            {
                globalRain = enviroIntegrationCurrentWetness;
                if (extendedRainTransitionCoroutine != null)
                {
                    StopCoroutine(extendedRainTransitionCoroutine);
                }
                extendedRainTransitionCoroutine = StartCoroutine(TransitionGlobalRainExtended(globalRain, enviroGlobalRainExtendedTransitionDuration));

                currentGlobalRain = globalRain;
            }
            else
            {
                StopCoroutine(extendedRainTransitionCoroutine);
                extendedRainTransitionCoroutine = null;
            }
        }

        private IEnumerator TransitionGlobalRainExtended(float targetValue, float duration)
        {
            float initialValue = globalRainExtended;
            float elapsedTime = 0f;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                globalRainExtended = Mathf.Lerp(initialValue, targetValue, elapsedTime / duration);
                yield return null;
            }

            globalRainExtended = targetValue;
            currentGlobalRainExtended = globalRainExtended;
        }

        private void OnDisable()
        {
            if (extendedRainTransitionCoroutine != null)
            {
                StopCoroutine(extendedRainTransitionCoroutine);
                extendedRainTransitionCoroutine = null; 
            }
        }

        private void OnDestroy()
        {
            if (extendedRainTransitionCoroutine != null)
            {
                StopCoroutine(extendedRainTransitionCoroutine);
                extendedRainTransitionCoroutine = null; 
            }
        }

        public void EnviroUpdateRainAudio()
        {    
#if UNITY_EDITOR
            if (!UnityEditor.EditorApplication.isPlaying)
            {
                mainRainAudioSource.Stop();
                return;
            }   
#endif
            weatherType = Enviro.EnviroManager.instance.Weather.targetWeatherType;
            float interpolationFactor = mainRainAudioTransitionDuration * Time.deltaTime;
           
            if (weatherType != null && weatherType.audioOverride != null) 
            {
                if (isPlayerOccluded && globalRain == 1f)
                {
                    foreach (var audioClipOverride in weatherType.audioOverride.weatherOverride)
                    {
                        float newVolume = Mathf.Lerp(audioClipOverride.volume, mainRainInsideVolume , interpolationFactor);
                        audioClipOverride.volume = newVolume;
                    }
                }
                else if(!isPlayerOccluded && globalRain == 1f)
                {
                    foreach (var audioClipOverride in weatherType.audioOverride.weatherOverride)
                    {
                        float newVolume = Mathf.Lerp(audioClipOverride.volume, mainRainOutsideVolume , interpolationFactor);
                        audioClipOverride.volume = newVolume;
                    }
                }
            }
        }
#endif
    }
}