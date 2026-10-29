//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using UnityEngine;
using MoveRotateResize.Dynamic.RainWetness;
using MoveRotateResize.Dynamic.Occlusion;
using MoveRotateResize.Dynamic.RainAudio;
using MoveRotateResize.Dynamic.DrippingRain;
using MoveRotateResize.Dynamic.SmartSpawning;
using MoveRotateResize.Dynamic.ReflectionProbes;
using MoveRotateResize.Dynamic.FallingRain;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Reflection;

namespace MoveRotateResize.Dynamic.SystemsManager
{  

    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-1000000)]
    public class DynamicSystemsManager : MonoBehaviour
    {
        public static DynamicSystemsManager Instance { get; private set; }
        public static DynamicRainWetness dynamicRainWetness { get; private set; }
        public static DynamicFallingRain dynamicFallingRain { get; private set; }
        public static DynamicOcclusion dynamicOcclusion { get; private set; }
        public static DynamicDrippingRain dynamicDrippingRain { get; private set; }
        public static DynamicSmartSpawning dynamicSmartSpawning { get; private set; }
        public static DynamicRainAudio dynamicRainAudio { get; private set; }
        public static DynamicReflectionProbesSpawner dynamicReflectionProbesSpawner { get; private set; }

        public static GameObject player { get; private set; }
        public static GameObject mainCamera { get; private set; }
        public static Camera mainCameraComponent { get; private set; }
        public static AudioSource mainAudioSource { get; private set; }

        [SerializeField] bool doNotDestroyOnLoad = false;
    
        void Awake()
        {
            if (!Application.isPlaying) return;

            if (doNotDestroyOnLoad)
            {
                if (Instance != null && Instance != this)
                {
                    Destroy(gameObject);
                    return;
                }
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }

            InitializeComponents();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
            {
                InitializeComponents();
            }
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnDisable()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingEditMode)
            {
                InitializeComponents();
            }
        }
#endif

        void InitializeComponents()
        {
            player = null;
            mainCamera = null;
            mainAudioSource = null;
            dynamicRainWetness = null;
            dynamicFallingRain = null;
            dynamicOcclusion = null;
            dynamicDrippingRain = null;
            dynamicSmartSpawning = null;
            dynamicRainAudio = null;
            dynamicReflectionProbesSpawner = null;

            GameObject[] playerObjects = GameObject.FindGameObjectsWithTag("Player");
            if (playerObjects.Length == 1)
            {
                player = playerObjects[0];
                mainAudioSource = player.GetComponent<AudioSource>();

        #if UNITY_EDITOR
                if (mainAudioSource == null)
                {
                    Debug.LogWarning("Player does not have an AudioSource component. Please add one.");
                }
        #endif
            }
            else
            {
        #if UNITY_EDITOR
                if (playerObjects.Length > 1)
                    Debug.LogWarning("More than one GameObject with the 'Player' tag found.");
                else
                    Debug.LogWarning("No GameObject with the tag 'Player' found. Please add a player gameobject and set its tag to 'Player'.");
        #endif
            }

            mainCamera = Camera.main?.gameObject;
        #if UNITY_EDITOR
            if (mainCamera == null)
            {
                Debug.LogWarning("No GameObject with the 'MainCamera' tag found. Please tag your main camera as 'MainCamera'.");
            }
        #endif

            mainCameraComponent = mainCamera?.GetComponent<Camera>();
        #if UNITY_EDITOR
            if (mainCameraComponent == null)
            {
                Debug.LogWarning("No Camera component found on the main camera.");
            }
        #endif

            dynamicRainWetness = gameObject.GetComponent<DynamicRainWetness>();
        #if UNITY_EDITOR
            if (dynamicRainWetness == null)
            {
                Debug.LogWarning("DynamicRainWetness component is missing.");
            }
        #endif

          dynamicFallingRain = gameObject.GetComponent<DynamicFallingRain>();
        #if UNITY_EDITOR
            if (dynamicFallingRain == null)
            {
                Debug.LogWarning("DynamicFallingRain component is missing.");
            }
        #endif

            dynamicOcclusion = gameObject.GetComponent<DynamicOcclusion>();
        #if UNITY_EDITOR
            if (dynamicOcclusion == null)
            {
                Debug.LogWarning("DynamicOcclusion component is missing.");
            }
        #endif

            dynamicDrippingRain = gameObject.GetComponent<DynamicDrippingRain>();
        #if UNITY_EDITOR
            if (dynamicDrippingRain == null)
            {
                Debug.LogWarning("DynamicDrippingRain component is missing.");
            }
        #endif

            dynamicSmartSpawning = gameObject.GetComponent<DynamicSmartSpawning>();
        #if UNITY_EDITOR
            if (dynamicSmartSpawning == null)
            {
                Debug.LogWarning("DynamicSmartSpawning component is missing.");
            }
        #endif

            dynamicRainAudio = gameObject.GetComponent<DynamicRainAudio>();
        #if UNITY_EDITOR
            if (dynamicRainAudio == null)
            {
                Debug.LogWarning("DynamicRainAudio component is missing.");
            }
        #endif

            dynamicReflectionProbesSpawner = gameObject.GetComponent<DynamicReflectionProbesSpawner>();
        }


#if UNITY_EDITOR
        public void SetDebugForAllScriptsTrue()
        {
            SetDebugForAllScripts(true);
        }

        public void SetDebugForAllScriptsFalse()
        {
            SetDebugForAllScripts(false);
        }

        public void SetDebugForAllScripts(bool value)
        {
            var scripts = GetComponents<MonoBehaviour>();
            foreach (var script in scripts)
            {
                if (script == null) continue;

                var fields = script.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                foreach (var field in fields)
                {
                    if (field.Name.ToLower().Contains("debug") && field.FieldType == typeof(bool))
                    {
                        field.SetValue(script, value);
                    }
                }
            }
        }
#endif
    }
}