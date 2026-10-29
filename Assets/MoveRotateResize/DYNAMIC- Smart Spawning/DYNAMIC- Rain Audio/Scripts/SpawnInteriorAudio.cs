//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.Pool;
using MoveRotateResize.Dynamic.RainWetness;

namespace MoveRotateResize.Dynamic.RainAudio
{
    public class SpawnInteriorAudio : MonoBehaviour
    {
        private DynamicRainWetness dynamicRainWetness;
        private AudioSource interiorRainAudioSource;

        [SerializeField] private float transitionDuration = 1.0f;
        private bool isFading = false;

        private IObjectPool<SpawnInteriorAudio> spawnInteriorAudio;
        public IObjectPool<SpawnInteriorAudio> ObjectPool
        {
            set => spawnInteriorAudio = value;
        }


        private void Awake()
        {
            interiorRainAudioSource = GetComponent<AudioSource>();
            if (interiorRainAudioSource == null)
            {
                Debug.LogError("AudioSource component not found. Make sure this GameObject has an AudioSource component.");
            }
        }

        public void InitializeRainWetness(DynamicRainWetness rainWetness)
        {
            if (rainWetness == null)
            {
                Debug.LogWarning("DynamicRainWetness is null. Default behavior will be used.");
                return;
            }

            dynamicRainWetness = rainWetness;
            StartCoroutine(FadeInOrOutVolume(true, dynamicRainWetness.globalRain));
        }

        private void OnDisable()
        {
            StopAllCoroutines(); 
            isFading = false;
        }

        void Update()
        {
            if (isFading || dynamicRainWetness == null) return;

            float targetVolume = dynamicRainWetness.enableEnviroIntegration 
                ? dynamicRainWetness.enviroIntegrationCurrentWetness
                : dynamicRainWetness.globalRain;

            if (!Mathf.Approximately(interiorRainAudioSource.volume, targetVolume))
            {
                interiorRainAudioSource.volume = Mathf.Lerp(interiorRainAudioSource.volume, targetVolume, Time.deltaTime * 5f);
            }
        }

        private IEnumerator FadeInOrOutVolume(bool fadeIn, float targetVolume)
        {
            if (isFading) yield break;
            
            isFading = true;

            float startVolume = fadeIn ? 0f : interiorRainAudioSource.volume;
            float elapsedTime = 0f;

            if (fadeIn)
            {
                interiorRainAudioSource.volume = startVolume;
                interiorRainAudioSource.Play();
            }

            while (elapsedTime < transitionDuration)
            {
                elapsedTime += Time.deltaTime;
                interiorRainAudioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsedTime / transitionDuration);
                yield return null;
            }

            interiorRainAudioSource.volume = targetVolume;
            if (!fadeIn)
            {
                interiorRainAudioSource.Stop();
            }

            isFading = false;
        }

        public void Release()
        {
            if (!isFading)
            {
                StartCoroutine(FadeInOrOutVolume(false, 0f));
                StartCoroutine(DelayedRelease());
            }
        }

        private IEnumerator DelayedRelease()
        {
            yield return new WaitForSeconds(transitionDuration);
            spawnInteriorAudio.Release(this);
        }
    }
}