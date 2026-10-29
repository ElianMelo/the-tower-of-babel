//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using MoveRotateResize.Dynamic.MultiTag;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.RainWetness;

namespace MoveRotateResize.Dynamic.RainAudio
{
    [RequireComponent(typeof(DynamicSystemsManager))]
    public class DynamicRainAudio : MonoBehaviour
    {
        DynamicRainWetness dynamicRainWetness;


        [SerializeField] private PoolConfig[] poolConfigs;
        private HashSet<DynamicMultiTag> allItems = new HashSet<DynamicMultiTag>();
        private Dictionary<Tag, IObjectPool<SpawnInteriorAudio>> pools = new Dictionary<Tag, IObjectPool<SpawnInteriorAudio>>();    


        void Start()
        {
            CacheGlobalVariables();
            InitializePools();
        }

        void CacheGlobalVariables()
        {
            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
        }

        void OnDisable()
        {
            allItems.Clear();
        }

        private void OnDestroy()
        {
            allItems.Clear();
        }

        private void InitializePools()
        {
            if (poolConfigs == null || poolConfigs.Length == 0)
            {
                Debug.LogError("PoolConfigs array is empty or not assigned.");
                return;
            }

            foreach (var config in poolConfigs)
            {
                if (config == null || config.spawn == null)
                {
                    Debug.LogError($"Invalid PoolConfig for tag {config?.poolTag}");
                    continue;
                }

                var pool = new ObjectPool<SpawnInteriorAudio>(
                    () => CreatePooledAudioSource(config),
                    OnTakeFromPool,
                    OnReturnedToPool,
                    OnDestroyPoolObject,
                    config.useCollectionChecks,
                    config.initialPoolSize,
                    config.maxPoolSize
                );

                pools[config.poolTag] = pool;

                for (int i = 0; i < config.initialPoolSize; i++)
                {
                    var audioInstance = CreatePooledAudioSource(config);
                    if (audioInstance != null)
                    {
                        pool.Release(audioInstance);
                    }
                }
            }
        }

        SpawnInteriorAudio CreatePooledAudioSource(PoolConfig config)
        {
            if (!pools.ContainsKey(config.poolTag))
            {
                Debug.LogError($"PoolConfig with tag {config.poolTag} not found in pools dictionary.");
                return null;
            }

            var newAudioPoolRainInterior = Instantiate(config.spawn);
            newAudioPoolRainInterior.gameObject.SetActive(false);
            var myAudioSource = newAudioPoolRainInterior.GetComponent<AudioSource>();

            newAudioPoolRainInterior.ObjectPool = pools[config.poolTag];
            if (config.audioClips.Count > 0)
            {
                var selectedAudioClip = config.audioClips[UnityEngine.Random.Range(0, config.audioClips.Count)];
                myAudioSource.clip = selectedAudioClip;
            }

            return newAudioPoolRainInterior;
        }

        void OnTakeFromPool(SpawnInteriorAudio pooledObject)
        {
            pooledObject.gameObject.SetActive(true);
            pooledObject.InitializeRainWetness(dynamicRainWetness);
        }

        void OnReturnedToPool(SpawnInteriorAudio pooledObject)
        {
            pooledObject.gameObject.SetActive(false);
        }

        void OnDestroyPoolObject(SpawnInteriorAudio pooledObject)
        {
            Destroy(pooledObject.gameObject);
        }

        internal SpawnInteriorAudio SettingsForSpawn(Collider item)
        {
            SpawnInteriorAudio newRainAudio = null;
            foreach (var config in poolConfigs)
            {
                if (item.HasTag(config.poolTag))
                {
                    if (pools.TryGetValue(config.poolTag, out var pool))
                    {
                        newRainAudio = pool.Get();
                        if (newRainAudio != null)
                        {
                            var newTransform = newRainAudio.transform;
                            newTransform.position = item.transform.position;
                            newTransform.rotation = item.transform.rotation; 

                            var audioSource = newRainAudio.GetComponent<AudioSource>();
                            if (audioSource != null && newRainAudio.gameObject.activeSelf)
                            {
                                audioSource.Play();
                            }
                            else
                            {
                                Debug.LogWarning($"No AudioSource component found on the audio instance attached to {item.name}.");
                            }
                        }
                        else
                        {
                            Debug.LogWarning($"Failed to get a new audio instance from the pool for item: {item.name}");
                        }
                    }
                    break;
                }  
            }

            return newRainAudio;
        }


        internal void ReleaseAudioInstances(DynamicMultiTag item)
        {
            Debug.Log($"ReleaseAudioInstances called for item: {item.name}"); 

            if (item == null)
            {
                Debug.LogWarning("Item is null, cannot release audio instances.");
                return;
            }

            var audioInstances = item.gameObject.GetComponents<SpawnInteriorAudio>();

            Debug.Log($"Found {audioInstances.Length} audio instance(s) attached to item: {item.name}");

            if (audioInstances.Length == 0)
            {
                Debug.LogWarning($"No audio instances found for item: {item.name}.");
            }

            foreach (var audioInstance in audioInstances)
            {
                if (audioInstance != null)
                {
                    item.SetAudioAttached(false);
                    audioInstance.Release();
                    Debug.Log($"Released audio instance from item: {item.name}"); 
                }
                else
                {
                    Debug.LogWarning($"Null audio instance found for item: {item.name}");
                }
            }
        }
    }
}