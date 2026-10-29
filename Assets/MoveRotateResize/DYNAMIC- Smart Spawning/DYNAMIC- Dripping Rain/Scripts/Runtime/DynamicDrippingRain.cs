
//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using UnityEditor;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.MultiTag;
using NaughtyAttributes;
using System.IO;

namespace MoveRotateResize.Dynamic.DrippingRain
{   
    [System.Serializable]
    [RequireComponent(typeof(DynamicSystemsManager))]
    public class DynamicDrippingRain : MonoBehaviour
    {
        Dictionary<Tag, IObjectPool<SpawnDrippingRain>> pools = new Dictionary<Tag, IObjectPool<SpawnDrippingRain>>();   
        HashSet<DynamicMultiTag> allItems = new HashSet<DynamicMultiTag>();


        [MarkupAttributes.Foldout("Dripping Rain Pools")]
        [SerializeField] PoolConfigDrippingRain[] poolConfigs;


        [MarkupAttributes.Foldout("Dripping Rain Zones")]
        [SerializeField] Material drippingRainZoneMaterial;
        [SerializeField] [Range(0f, 1f)] public float drippingRainZoneMaterialAlpha = 1f;
        
        
        [MarkupAttributes.Foldout("Dripping Rain Edit Mode")]
        [InfoBox("If you move obejcts in the editor you'll have to delete then create edit mode spawns again", EInfoBoxType.Warning)]
        [SerializeField] GameObject editModeDrippingRainSpawns;
        [SerializeField] List<VisualizerObjectPairScriptableObject> spawns;


        void Start()
        {
            if(Application.isPlaying)
            {
                DestroyDrippingRainSpawns();
            }
            
            InitializePools();
        }
 
        void OnValidate()
        {       
    #if UNITY_EDITOR
            if(!Application.isPlaying)
            {
                LoadDrippingRainEditModeSpawns();
            }
    #endif

            if (drippingRainZoneMaterial == null)
            {
                Debug.LogWarning("Occlusion Zone Material is not assigned. Please assign a material in the Inspector.");
            }
            else
            {
                SetAlpha(drippingRainZoneMaterialAlpha);
            }
        }
        public void SetAlpha(float newAlpha)
        {
            Color color = drippingRainZoneMaterial.GetColor("_Color");
            color.a = newAlpha;
            drippingRainZoneMaterial.SetColor("_Color", color);
        }

        void OnDisable()
        {
            allItems.Clear();
        }

        private void OnDestroy()
        {
            spawns.Clear();
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

                var pool = new ObjectPool<SpawnDrippingRain>(
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

        SpawnDrippingRain CreatePooledAudioSource(PoolConfigDrippingRain config)
        {
            if (!pools.ContainsKey(config.poolTag))
            {
                Debug.LogError($"PoolConfig with tag {config.poolTag} not found in pools dictionary.");
                return null;
            }

            var newAudioPoolRainInterior = Instantiate(config.spawn);
            newAudioPoolRainInterior.gameObject.SetActive(false);

            newAudioPoolRainInterior.ObjectPool = pools[config.poolTag];

            return newAudioPoolRainInterior;
        }

        void OnTakeFromPool(SpawnDrippingRain pooledObject)
        {
            pooledObject.gameObject.SetActive(true);
        }

        void OnReturnedToPool(SpawnDrippingRain pooledObject)
        {
            pooledObject.gameObject.SetActive(false);
        }

        void OnDestroyPoolObject(SpawnDrippingRain pooledObject)
        {
            Destroy(pooledObject.gameObject);
        }

        internal SpawnDrippingRain SettingsForSpawn(Collider spawnPoint)
        {
            SpawnDrippingRain newDrippingRainSpawn = null;
            foreach (var config in poolConfigs)
            {
                //Debug.Log($"Checking if spawnPoint: {spawnPoint.name} has tag: {config.poolTag}");
                if (spawnPoint.HasTag(config.poolTag))
                {
                    //Debug.Log($"Match found for tag: {config.poolTag}");
                    if (pools.TryGetValue(config.poolTag, out var pool))
                    {
                        //Debug.Log($"Pool found for tag: {config.poolTag}. Attempting to get an instance from the pool.");
                        newDrippingRainSpawn = pool.Get();
                        if (newDrippingRainSpawn != null)
                        {
                            //Debug.Log($"Successfully got an instance from the pool for tag: {config.poolTag}");
                            var newTransform = newDrippingRainSpawn.transform;
                            newTransform.position = spawnPoint.transform.position;
                            newTransform.rotation = spawnPoint.transform.rotation;
                            newTransform.localScale = spawnPoint.transform.localScale;
                            newDrippingRainSpawn.UpdateSpawnSize(spawnPoint, newDrippingRainSpawn);
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"No pool found for tag: {config.poolTag}. Unable to spawn for spawnPoint: {spawnPoint.name}");
                    }
                    break;
                }
            }

            if (newDrippingRainSpawn == null)
            {
                Debug.LogWarning($"No matching tag or available instance found for spawnPoint: {spawnPoint.name}");
            }

            return newDrippingRainSpawn;
        }


        internal void ReleaseAudioInstances(DynamicMultiTag item)
        {
            var audioInstances = item.GetComponentsInChildren<SpawnDrippingRain>();
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

#region Edit Mode
#if UNITY_EDITOR
        public void CreateDrippingRainEditModeSpawns()
        {
            var drippingRainEditModeVisualizers = FindObjectsOfType<DrippingRainEditModeVisualizer>();

            if (spawns == null)
            {
                spawns = new List<VisualizerObjectPairScriptableObject>();
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string assetFolderPath = $"Assets/MoveRotateResize/DYNAMIC- Smart Spawning/DYNAMIC- Dripping Rain/Edit Mode Dripping Rain/{sceneName}";
            string parentFolderPath = "Assets/MoveRotateResize/DYNAMIC- Smart Spawning/DYNAMIC- Dripping Rain/Edit Mode Dripping Rain";
            
            if (!AssetDatabase.IsValidFolder(parentFolderPath))
            {
                AssetDatabase.CreateFolder("Assets/MoveRotateResize/DYNAMIC- Smart Spawning/DYNAMIC- Dripping Rain", "Edit Mode Dripping Rain");
            }

            if (!AssetDatabase.IsValidFolder(assetFolderPath))
            {
                AssetDatabase.CreateFolder(parentFolderPath, sceneName);
            }

            foreach (var drippingRainEditModeVisualizer in drippingRainEditModeVisualizers)
            {
                if (drippingRainEditModeVisualizer == null)
                {
                    Debug.LogWarning("Visualizer is null or destroyed. Skipping.");
                    continue;
                }

                var existingPair = spawns.Find(pair => pair.drippingRainEditModeVisualizer == drippingRainEditModeVisualizer.gameObject);
                if (existingPair == null)
                {
                    VisualizerObjectPairScriptableObject newPair = ScriptableObject.CreateInstance<VisualizerObjectPairScriptableObject>();
                    newPair.drippingRainEditModeVisualizer = drippingRainEditModeVisualizer.gameObject;

                    GameObject vfxInstance = drippingRainEditModeVisualizer.CreateVFXInstance();
                    if (vfxInstance != null)
                    {
                        if (editModeDrippingRainSpawns != null)
                        {
                            vfxInstance.transform.SetParent(editModeDrippingRainSpawns.transform);
                        }

                        newPair.vfxInstance = vfxInstance;
                        spawns.Add(newPair);

                        string assetPath = $"{assetFolderPath}/{drippingRainEditModeVisualizer.name}_{drippingRainEditModeVisualizer.gameObject.GetInstanceID()}_Pair.asset";
                        AssetDatabase.CreateAsset(newPair, assetPath);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                        //Debug.Log($"Created and saved VisualizerObjectPairScriptableObject for {drippingRainEditModeVisualizer.name} at {assetPath}");
                    }
                    else
                    {
                        Debug.LogWarning($"Failed to create VFX instance for visualizer: {drippingRainEditModeVisualizer.name}");
                    }
                }
            }


            Debug.Log($"Finished spawning dripping rain spawns. Total spawns: {spawns.Count}");
        }


        public void LoadDrippingRainEditModeSpawns()    
        {
            spawns.Clear();

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string assetFolderPath = $"Assets/MoveRotateResize/DYNAMIC- Smart Spawning/DYNAMIC- Dripping Rain/Edit Mode Dripping Rain/{sceneName}";

            //Debug.Log($"Scene Name: {sceneName}");
            //Debug.Log($"Asset Folder Path: {assetFolderPath}");

            string fullPath = Path.Combine(Application.dataPath, assetFolderPath.Substring(7));
            if (!Directory.Exists(fullPath))
            {
                //Debug.LogError($"Folder not found: {assetFolderPath}");
                return;
            }

            string[] assetPaths = AssetDatabase.FindAssets("t:VisualizerObjectPairScriptableObject", new[] { assetFolderPath });

            if (assetPaths.Length == 0)
            {
                //Debug.LogWarning($"No assets found in {assetFolderPath}");
                return;
            }

            foreach (var guid in assetPaths)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                VisualizerObjectPairScriptableObject pair = AssetDatabase.LoadAssetAtPath<VisualizerObjectPairScriptableObject>(assetPath);

                if (pair != null)
                {
                    spawns.Add(pair);
                    Debug.Log($"Loaded VisualizerObjectPairScriptableObject from {assetPath}");
                }
                else
                {
                    Debug.LogWarning($"Failed to load VisualizerObjectPairScriptableObject at {assetPath}");
                }
            }

            Debug.Log($"Loaded {spawns.Count} VisualizerObjectPairScriptableObjects from {assetFolderPath}");
        }

#endif

        public void DestroyDrippingRainSpawns()
        {
            if (editModeDrippingRainSpawns != null)
            {
                List<Transform> childrenToDestroy = new List<Transform>();

                foreach (Transform child in editModeDrippingRainSpawns.transform)
                {
                    if (child != null)
                    {
                        childrenToDestroy.Add(child);
                    }
                }

                foreach (Transform child in childrenToDestroy)
                {
                    if (child != null)
                    {
                        child.gameObject.SetActive(true);

        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            DestroyImmediate(child.gameObject, true);
                        }
                        else
                        {
                            Destroy(child.gameObject);
                        }
        #else
                        Destroy(child.gameObject);
        #endif
                    }
                }

                spawns?.Clear();
            }
            else
            {
                Debug.LogWarning("editModeDrippingRainSpawns is not assigned, no children to delete.");
            }

        #if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string assetFolderPath = $"Assets/MoveRotateResize/DYNAMIC- Smart Spawning/DYNAMIC- Dripping Rain/Edit Mode Dripping Rain/{sceneName}";
                if (AssetDatabase.IsValidFolder(assetFolderPath))
                {
                    string[] assetPaths = AssetDatabase.FindAssets("", new[] { assetFolderPath });

                    foreach (var guid in assetPaths)
                    {
                        string assetPath = AssetDatabase.GUIDToAssetPath(guid);

                        if (System.IO.File.Exists(assetPath))
                        {
                            AssetDatabase.DeleteAsset(assetPath);
                        }
                        else
                        {
                            Debug.LogWarning($"Asset not found at: {assetPath}");
                        }
                    }

                    AssetDatabase.Refresh();
                }
                else
                {
                    Debug.LogWarning($"Asset folder not found at: {assetFolderPath}");
                }
            }
        #endif
        }

        
        public void EnableEditMode()
        {
            editModeDrippingRainSpawns.SetActive(true);
        }

        public void DisableEditMode()
        {
            editModeDrippingRainSpawns.SetActive(false);
        }
#endregion

        public void RemoveMeshComponentsInEditMode()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                GameObject[] allObjects = FindObjectsOfType<GameObject>();

                foreach (GameObject obj in allObjects)
                {
                    MeshRenderer meshRenderer = obj.GetComponent<MeshRenderer>();
                    if (meshRenderer != null)
                    {
                        foreach (var material in meshRenderer.sharedMaterials)
                        {
                            if (material == drippingRainZoneMaterial)
                            {
                                Undo.RecordObject(obj, "Remove Mesh Components");

                                if (meshRenderer != null)
                                {
                                    Undo.DestroyObjectImmediate(meshRenderer);
                                }

                                MeshFilter meshFilter = obj.GetComponent<MeshFilter>();
                                if (meshFilter != null)
                                {
                                    Undo.DestroyObjectImmediate(meshFilter);
                                }

                                break;
                            }
                        }
                    }
                }
            }
#endif
        }
    }
}