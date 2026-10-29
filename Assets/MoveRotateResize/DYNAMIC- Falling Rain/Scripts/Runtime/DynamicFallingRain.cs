//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using System.Collections.Generic;
using MoveRotateResize.Dynamic.RainWetness;
using MoveRotateResize.Dynamic.SystemsManager;
using UnityEngine;
using UnityEngine.VFX; 

namespace MoveRotateResize.Dynamic.FallingRain
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-500)] 
    public class DynamicFallingRain : MonoBehaviour
    {
        DynamicRainWetness dynamicRainWetness;

        Dictionary<string, FallingRainEffect> rainEffects;
        [HideInInspector] public VisualEffect fallingRainVFXGraph;
        ParticleSystem fallingRainParticleSystem;
        [System.Serializable]
        public struct FallingRainEffect
        {
            public GameObject ActiveEffect;
            public List<GameObject> InactiveEffects;
        }


        [MarkupAttributes.Foldout("VFX Holder")]
        public GameObject fallingRainVFXS;


        [MarkupAttributes.Foldout("Particle System Settings")]
        [SerializeField] Vector2 particleSystemParticlesMinMax = new Vector2(50, 5000);
        [SerializeField] Vector2 particleSystemEmissionRateMinMax = new Vector2(10, 1000);
        

        [HideInInspector]public bool enableCameraOcclusion = false;
        [HideInInspector]public bool enableVFXOcclusion = false;
        [HideInInspector]public bool enableSDFOcclusion = false;
        float currentGlobalRain;
        float previousGlobalRain = -1f;
        const string GlobalRainString = "_Global_Rain";
        [HideInInspector] public string selectedEffectName;
        [HideInInspector] public FallingRainEffect rainEffect;


        void Start()
        {
            CacheGlobalVariables();

            if (string.IsNullOrEmpty(selectedEffectName))
            {
                Debug.LogError("selectedEffectName is null or empty.");
                return;
            }

            if (fallingRainVFXS != null)
            {
                InitializeRainEffectsFromChildren(fallingRainVFXS);
            }
            else
            {
                Debug.LogError("FallingRainVFXs is not assigned.");
            }

            ApplyFallingRainVFX(selectedEffectName);
        }


        void CacheGlobalVariables()
        {
            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
        }

       void OnValidate()
        {
            if (Application.isEditor && !Application.isPlaying)
            {
                if (fallingRainVFXS != null)
                {
                    InitializeRainEffectsFromChildren(fallingRainVFXS);
                }

                ApplyFallingRainVFX(selectedEffectName);
            }
        }

        void Update()
        {
            UpdateRainVFX();
        }

        public void ApplyFallingRainVFX(string effectName)
        {
            if (rainEffects.TryGetValue(effectName, out rainEffect))
            {
                HandleEffect(rainEffect.ActiveEffect, rainEffect.InactiveEffects);

                if (rainEffect.ActiveEffect.TryGetComponent(out VisualEffect vfx))
                {
                    fallingRainVFXGraph = vfx;
                    fallingRainParticleSystem = null; 
                    fallingRainVFXGraph.Play(); 
                }
                else if (rainEffect.ActiveEffect.TryGetComponent(out ParticleSystem ps))
                {
                    fallingRainParticleSystem = ps;
                    fallingRainVFXGraph = null; 
                    fallingRainParticleSystem.Play();
                }
                else
                {
                    fallingRainVFXGraph = null;
                    fallingRainParticleSystem = null;
                }
            }
            else
            {
                DisableAllFallingRainObjects();
                fallingRainVFXGraph = null;
                fallingRainParticleSystem = null;
            }
        }

        public void UpdateEffectBooleans(string effectName)
        {
            enableVFXOcclusion = false;
            enableCameraOcclusion = false;
            enableSDFOcclusion = false;

            switch (effectName)
            {
                case "Falling Rain- VFX Graph- VFX + SDF Occlusion":
                    enableVFXOcclusion = true;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = true;
                    break;

                case "Falling Rain- VFX Graph- VFX Occlusion":
                    enableVFXOcclusion = true;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = false;
                    break;

                case "Falling Rain- VFX Graph- Camera Occlusion":
                    enableVFXOcclusion = false;
                    enableCameraOcclusion = true;
                    enableSDFOcclusion = false;
                    break;
                    
                case "Falling Rain- VFX Graph- NO Occlusion":
                    enableVFXOcclusion = false;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = false;
                    break;

                case "Falling Rain- Particle System":
                    enableVFXOcclusion = false;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = false;
                    break;

                case "None":
                    enableVFXOcclusion = false;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = false;
                    break;

                default:
                    enableVFXOcclusion = false;
                    enableCameraOcclusion = false;
                    enableSDFOcclusion = false;
                    break;
            }
        }

        void HandleEffect(GameObject activeEffect, List<GameObject> inactiveEffects)
        {
            if (activeEffect != null && !activeEffect.activeSelf)
            {
                activeEffect.SetActive(true);
            }

            foreach (var otherEffect in inactiveEffects)
            {
                if (otherEffect != null && otherEffect.activeSelf)
                {
                    otherEffect.SetActive(false);
                }
            }
        }

        public void DisableAllFallingRainObjects()
        {
            foreach (var rainEffect in rainEffects.Values)
            {
                if (rainEffect.ActiveEffect != null && rainEffect.ActiveEffect.activeSelf)
                {
                    rainEffect.ActiveEffect.SetActive(false);
                    
                    if (rainEffect.ActiveEffect.TryGetComponent(out VisualEffect vfx))
                    {
                        vfx.Stop();
                    }
                    else if (rainEffect.ActiveEffect.TryGetComponent(out ParticleSystem ps))
                    {
                        ps.Stop();
                    }
                }
            }
        }

        void InitializeRainEffectsFromChildren(GameObject parent)
        {
            rainEffects = new Dictionary<string, FallingRainEffect>();

            foreach (Transform child in parent.transform)
            {
                var activeEffect = child.gameObject;
                string effectName = child.name;
                var inactiveEffects = new List<GameObject>();

                foreach (Transform sibling in parent.transform)
                {
                    if (sibling != child)
                    {
                        inactiveEffects.Add(sibling.gameObject);
                    }
                }

                rainEffects[effectName] = new FallingRainEffect
                {
                    ActiveEffect = activeEffect, 
                    InactiveEffects = inactiveEffects
                };

                //Debug.Log($"Effect '{effectName}' initialized.");
            }

            if (rainEffects.Count == 0)
            {
                Debug.LogError("No rain effects found in the parent.");
            }
        }

        void UpdateRainVFX()
        {
            if (dynamicRainWetness == null)
                return;

            currentGlobalRain = dynamicRainWetness.globalRain;

            float rainThreshold = 0.01f;

            if (Mathf.Abs(currentGlobalRain) < rainThreshold)
            {
                DisableAllFallingRainObjects();
            }
            else
            {
                if (rainEffects != null && rainEffects.Count > 0)
                {
                    ApplyFallingRainVFX(selectedEffectName);
                }
            }

            float newParticleCount = Mathf.Lerp(particleSystemParticlesMinMax.x, particleSystemParticlesMinMax.y, currentGlobalRain);
            float newEmissionRate = Mathf.Lerp(particleSystemEmissionRateMinMax.x, particleSystemEmissionRateMinMax.y, currentGlobalRain);

            if (!Mathf.Approximately(currentGlobalRain, previousGlobalRain))
            {
                if (fallingRainVFXGraph != null)
                {
                    fallingRainVFXGraph.SetFloat(GlobalRainString, currentGlobalRain);
                }

                if (fallingRainParticleSystem != null)
                {
                    var main = fallingRainParticleSystem.main;
                    var emission = fallingRainParticleSystem.emission;

                    if (main.maxParticles != newParticleCount)
                    {
                        main.maxParticles = Mathf.RoundToInt(newParticleCount);
                    }

                    if (emission.rateOverTime.constant != newEmissionRate)
                    {
                        emission.rateOverTime = newEmissionRate;
                    }
                }
            }

            previousGlobalRain = currentGlobalRain;
        }
    }
}