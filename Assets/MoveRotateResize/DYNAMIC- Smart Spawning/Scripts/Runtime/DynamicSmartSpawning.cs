//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.MultiTag;
using MoveRotateResize.Dynamic.RainWetness;
using MoveRotateResize.Dynamic.DrippingRain;
using MoveRotateResize.Dynamic.RainAudio;
using MoveRotateResize.Dynamic.Occlusion;
using MoveRotateResize.Dynamic.FallingRain;
using System.Linq;

namespace MoveRotateResize.Dynamic.SmartSpawning
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(DynamicSystemsManager))]
    public class DynamicSmartSpawning : MonoBehaviour
    {
        public enum SpawnProcessingMode
        {
            Sphere,
            CameraFrustum,
            Disabled,
        }

        DynamicRainWetness dynamicRainWetness;
        DynamicRainAudio dynamicRainAudio;
        DynamicOcclusion dynamicOcclusion;
        DynamicDrippingRain dynamicDrippingRain;
        DynamicFallingRain dynamicFallingRain;
        GameObject player;
        Camera mainCameraComponent;

        [MarkupAttributes.Foldout("Spawn Modes")] 
        [SerializeField] SpawnProcessingMode audioSpawnMode = SpawnProcessingMode.Sphere;
        [SerializeField] SpawnProcessingMode vfxSpawnMode = SpawnProcessingMode.CameraFrustum;
        [SerializeField] SpawnProcessingMode vfxOcclusionSpawnMode = SpawnProcessingMode.CameraFrustum;

        [MarkupAttributes.Foldout("Sphere Cast")] 
        [SerializeField] LayerMask detectionLayer;
        [SerializeField] float sphereCastRadius = 30.0f;
        [SerializeField] float sphereCastInterval = 1.0f;
        float nextSphereCastTime = 0.0f;
        Collider[] allItemsInSphere = new Collider[64];

        [MarkupAttributes.Foldout("Camera Frustum")] 
        [SerializeField] float maxSpawnAndRemovalDistance = 10.0f;

        [MarkupAttributes.Foldout("Tags")] 
        [SerializeField] Tag OcclusionZoneTag;  
        [SerializeField] Tag spawnPointAudioTag;
        [SerializeField] Tag spawnPointVFXTag;  

        [MarkupAttributes.Foldout("Debugs")] 
#if UNITY_EDITOR
        [SerializeField] bool debugCameraFrustum = true;
        [SerializeField] bool debugSphereCast = true;  
#endif
        bool spawnsAudioReleased = false;
        bool spawnsDrippingRainReleased = false;
        bool spawnsVFXOcclusionReleased = false;


        HashSet<Collider> emptyColliders = new();
        HashSet<Collider> emptyObjects = new();
        HashSet<Collider> currentAudioItemsInSphere = new HashSet<Collider>();
        HashSet<Collider> currentVFXItemsInSphere = new HashSet<Collider>();
        HashSet<Collider> currentVFXOcclusionZones = new HashSet<Collider>();


        Dictionary<Collider, SpawnInteriorAudioPair> spawnsInteriorAudio = new Dictionary<Collider, SpawnInteriorAudioPair>();
        class SpawnInteriorAudioPair
        {
            public Collider Item { get; private set; }
            public SpawnInteriorAudio Spawn { get; private set; }

            public SpawnInteriorAudioPair(Collider item, SpawnInteriorAudio spawn)
            {
                Item = item;
                Spawn = spawn;
            }
        }


        Dictionary<Collider, SpawnsDrippingRainPair> spawnsDrippingRain = new Dictionary<Collider, SpawnsDrippingRainPair>();
        class SpawnsDrippingRainPair
        {
            public Collider Item { get; private set; }
            public SpawnDrippingRain Spawn { get; private set; }

            public SpawnsDrippingRainPair(Collider item, SpawnDrippingRain spawn)
            {
                Item = item;
                Spawn = spawn;
            }
        }


        Dictionary<Collider, SpawnsVFXOcclusionPairs> spawnsVFXOcclusion = new Dictionary<Collider, SpawnsVFXOcclusionPairs>();
        class SpawnsVFXOcclusionPairs
        {
            public Collider OcclusionZone { get; private set; }
            public GameObject VFXOcclusion { get; private set; }

            public SpawnsVFXOcclusionPairs(Collider occlusionZone, GameObject vFXOcclusion)
            {
                OcclusionZone = occlusionZone;
                VFXOcclusion = vFXOcclusion;
            }
        }


        void Start()
        {
            CacheGlobalVariables();
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player;
            mainCameraComponent = DynamicSystemsManager.mainCameraComponent;
            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
            dynamicRainAudio = DynamicSystemsManager.dynamicRainAudio;
            dynamicOcclusion = DynamicSystemsManager.dynamicOcclusion;
            dynamicDrippingRain = DynamicSystemsManager.dynamicDrippingRain;
            dynamicFallingRain = DynamicSystemsManager.dynamicFallingRain;
        }

        void FixedUpdate()
        {
            if (player == null) return;

            float currentGlobalRain = dynamicRainWetness.globalRain;
            float currentGlobalRainExtended = dynamicRainWetness.globalRainExtended;

            if (Mathf.Abs(currentGlobalRainExtended) <= 0.01f)
            {
                if (!spawnsDrippingRainReleased)
                {
                    ReleaseAllDrippingRainSpawns();
                    spawnsDrippingRainReleased = true;
                }
                return;
            }

            if (Mathf.Abs(currentGlobalRain) <= 0.01f)
            {
                if (!spawnsAudioReleased)
                {
                    ReleaseAllAudioSpawns();
                    spawnsAudioReleased = true;
                }
                return;
            }

            if (Mathf.Abs(currentGlobalRain) <= 0.01f)
            {
                if (!spawnsVFXOcclusionReleased)
                {
                    ReleaseAllVFXOcclusion();
                    spawnsVFXOcclusionReleased = true;
                }
                return;
            }


            if (Mathf.Abs(currentGlobalRain) > 0.01f && spawnsAudioReleased)
            {
                spawnsAudioReleased = false;
            }

            if (Mathf.Abs(currentGlobalRainExtended) > 0.01f && spawnsDrippingRainReleased)
            {
                spawnsDrippingRainReleased = false;
            }

            if (Mathf.Abs(currentGlobalRainExtended) > 0.01f && spawnsVFXOcclusionReleased)
            {
                spawnsVFXOcclusionReleased = false;
            }

            SphereCast();
        }

        void SphereCast()
        {
            if (sphereCastRadius < Mathf.Epsilon || Time.time < nextSphereCastTime)
                return;

            nextSphereCastTime = Time.time + sphereCastInterval;
            Vector3 currentPosition = player.transform.position;

            int itemCount = Physics.OverlapSphereNonAlloc(currentPosition, sphereCastRadius, allItemsInSphere, detectionLayer);
            if (itemCount == 0)
            {
                emptyColliders.Clear();
                emptyObjects.Clear();

                HandleAudioRemoval(emptyColliders);
                HandleVFXRemoval(emptyColliders);
                HandleVFXOcclusionRemoval(emptyObjects);

                return;
            }

            currentAudioItemsInSphere.Clear();
            currentVFXItemsInSphere.Clear();
            currentVFXOcclusionZones.Clear();

            Plane[] cameraFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCameraComponent);

            for (int i = 0; i < itemCount; i++)
            {
                Collider collider = allItemsInSphere[i];
                if (collider != null)
                    ClassifyAndProcessDetection(collider, cameraFrustumPlanes);
            }

            ProcessAudioSpawningAndRemoval(currentAudioItemsInSphere, cameraFrustumPlanes);

            if (dynamicFallingRain.enableVFXOcclusion)
            {
                ProcessVFXSpawningAndRemoval(currentVFXItemsInSphere, currentVFXOcclusionZones, cameraFrustumPlanes);
            }
        }

        void ClassifyAndProcessDetection(Collider collider, Plane[] cameraFrustumPlanes)
        {
            Bounds bounds = collider.bounds;

            bool isInFrustum = GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, bounds);
            bool hasAudioTag = collider.HasTag(spawnPointAudioTag);
            bool hasVFXTag = collider.HasTag(spawnPointVFXTag);
            bool isVFXOcclusionZone = collider.HasTag(OcclusionZoneTag);

            //Debug.Log($"Collider: {collider.name}, HasVFXTag: {hasVFXTag}, IsInFrustum: {isInFrustum}");

            // Handle VFX occlusion zones
            if (isVFXOcclusionZone &&
                (vfxOcclusionSpawnMode == SpawnProcessingMode.Sphere || 
                (vfxOcclusionSpawnMode == SpawnProcessingMode.CameraFrustum && isInFrustum)))
            {
                currentVFXOcclusionZones.Add(collider);
            }

            // Handle general VFX items
            if (hasVFXTag &&
                (vfxSpawnMode == SpawnProcessingMode.Sphere || 
                (vfxSpawnMode == SpawnProcessingMode.CameraFrustum && isInFrustum)))
            {
                currentVFXItemsInSphere.Add(collider);
            }

            // Handle audio items
            if (hasAudioTag &&
                (audioSpawnMode == SpawnProcessingMode.Sphere || 
                (audioSpawnMode == SpawnProcessingMode.CameraFrustum && isInFrustum)))
            {
                currentAudioItemsInSphere.Add(collider);
            }
        }

        void ProcessAudioSpawningAndRemoval(HashSet<Collider> audioItems, Plane[] cameraFrustumPlanes)
        {
            var frustumFilteredItems = audioSpawnMode == SpawnProcessingMode.CameraFrustum 
                ? GetFrustumFilteredItems(audioItems, cameraFrustumPlanes) 
                : audioItems;

            // Spawn audio
            foreach (var collider in audioItems)
            {
                if (ShouldSpawnAudio(collider, cameraFrustumPlanes))
                    HandleAudioSpawns(collider);
            }

            // Remove audio
            HandleAudioRemoval(frustumFilteredItems);
        }

        void ProcessVFXSpawningAndRemoval(HashSet<Collider> vfxItems, HashSet<Collider> vfxOcclusionZones, Plane[] cameraFrustumPlanes)
        {
            var frustumFilteredObjects = vfxSpawnMode == SpawnProcessingMode.CameraFrustum 
                ? GetFrustumFilteredObjects(vfxItems, cameraFrustumPlanes) 
                : vfxItems;

            var frustumFilteredOcclusionZones = vfxOcclusionSpawnMode == SpawnProcessingMode.CameraFrustum 
                ? GetFrustumFilteredObjects(vfxOcclusionZones, cameraFrustumPlanes) 
                : vfxOcclusionZones;

            // Spawn general VFX
            foreach (var collider in vfxItems)
            {
                if (ShouldSpawnVFX(collider, cameraFrustumPlanes))
                    HandleVFXSpawns(collider);
            }

            // Spawn VFX occlusion
            foreach (var vfxLocation in vfxOcclusionZones)
            {
                if (ShouldSpawnVFXOcclusion(vfxLocation, cameraFrustumPlanes))
                    HandleVFXOcclusionSpawns(vfxLocation, vfxLocation.transform.rotation, vfxLocation.transform.localScale);
            }
            
            HandleVFXRemoval(frustumFilteredObjects);
            HandleVFXOcclusionRemoval(frustumFilteredOcclusionZones);
        }
        
        bool ShouldSpawnVFX(Collider collider, Plane[] cameraFrustumPlanes)
        {
            float distance = Vector3.Distance(mainCameraComponent.transform.position, collider.transform.position);
            return (vfxSpawnMode == SpawnProcessingMode.Sphere || 
                (vfxSpawnMode == SpawnProcessingMode.CameraFrustum && GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, collider.bounds))) &&
                distance <= maxSpawnAndRemovalDistance;
        }

        bool ShouldSpawnVFXOcclusion(Collider collider, Plane[] cameraFrustumPlanes)
        {
            return vfxOcclusionSpawnMode == SpawnProcessingMode.Sphere || 
                (vfxOcclusionSpawnMode == SpawnProcessingMode.CameraFrustum && GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, collider.bounds));
        }

        bool ShouldSpawnAudio(Collider collider, Plane[] cameraFrustumPlanes)
        {
            return audioSpawnMode == SpawnProcessingMode.Sphere || 
                (audioSpawnMode == SpawnProcessingMode.CameraFrustum && GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, collider.bounds));
        }

        HashSet<Collider> GetFrustumFilteredItems(HashSet<Collider> items, Plane[] cameraFrustumPlanes)
        {
            return items.Where(collider => GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, collider.bounds)).ToHashSet();
        }

        HashSet<Collider> GetFrustumFilteredObjects(HashSet<Collider> objects, Plane[] cameraFrustumPlanes)
        {
            return objects.Where(obj => 
                obj.GetComponent<Collider>() != null && 
                GeometryUtility.TestPlanesAABB(cameraFrustumPlanes, obj.GetComponent<Collider>().bounds)).ToHashSet();
        }

        void HandleVFXOcclusionSpawns(Collider occlusionZone, Quaternion rotation, Vector3 scale)
        {
            if (!spawnsVFXOcclusion.ContainsKey(occlusionZone))
            {
                int randomIndex = Random.Range(0, dynamicOcclusion.VFXOcclusionSpawns.Length);
                GameObject randomVFX = dynamicOcclusion.VFXOcclusionSpawns[randomIndex];

                if (!randomVFX.activeInHierarchy)
                {   
                    randomVFX.transform.position = occlusionZone.transform.position;

                    Vector3 targetWorldScale = occlusionZone.transform.lossyScale;
                    Transform parentTransform = randomVFX.transform.parent;
                    if (parentTransform != null)
                    {
                        Vector3 parentWorldScale = parentTransform.lossyScale;
                        targetWorldScale = new Vector3(
                            targetWorldScale.x / parentWorldScale.x,
                            targetWorldScale.y / parentWorldScale.y,
                            targetWorldScale.z / parentWorldScale.z
                        );
                    }
                    randomVFX.transform.localScale = targetWorldScale;

                    randomVFX.SetActive(true);

                    SpawnsVFXOcclusionPairs newVFXOcclusion = new SpawnsVFXOcclusionPairs(occlusionZone, randomVFX);
                    spawnsVFXOcclusion[occlusionZone] = newVFXOcclusion;
                }
            }
        }

        void HandleVFXOcclusionRemoval(HashSet<Collider> currentVFXLocations)
        {
            List<Collider> locationsToRemove = new List<Collider>();

            foreach (var location in spawnsVFXOcclusion.Keys)
            {
                bool isOutsideView = !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(mainCameraComponent), location.bounds);

                if (!currentVFXLocations.Contains(location) || isOutsideView)
                {
                    locationsToRemove.Add(location);
                }
            }

            foreach (var location in locationsToRemove)
            {
                if (spawnsVFXOcclusion.TryGetValue(location, out SpawnsVFXOcclusionPairs vfxObject))
                {
                    vfxObject.VFXOcclusion.SetActive(false); 
                    spawnsVFXOcclusion.Remove(location); 
                }
                else
                {
                    Debug.LogWarning($"[HandleVFXOcclusionRemoval] Failed to find VFX object for location: {location.name}");
                }
            }
        }



        void HandleAudioSpawns(Collider collider)
        {
            if (!spawnsInteriorAudio.ContainsKey(collider))
            {
                SpawnInteriorAudio newAudioInstance = dynamicRainAudio.SettingsForSpawn(collider);
                if (newAudioInstance != null)
                {
                    spawnsInteriorAudio[collider] = new SpawnInteriorAudioPair(collider, newAudioInstance);
                }
            }
        }

        void HandleVFXSpawns(Collider collider)
        {
            if (!spawnsDrippingRain.ContainsKey(collider))
            {
                SpawnDrippingRain newDrippingRainInstance = dynamicDrippingRain.SettingsForSpawn(collider);
                if (newDrippingRainInstance != null)
                {
                    spawnsDrippingRain[collider] = new SpawnsDrippingRainPair(collider, newDrippingRainInstance);
                }
            }
        }

        void HandleAudioRemoval(HashSet<Collider> currentItemsInSphere)
        {
            List<Collider> itemsToRemove = new List<Collider>();

            foreach (var lastItem in spawnsInteriorAudio.Keys)
            {
                if (!currentItemsInSphere.Contains(lastItem))
                {
                    itemsToRemove.Add(lastItem);
                }
            }

            foreach (var item in itemsToRemove)
            {
                if (spawnsInteriorAudio[item].Spawn.gameObject.activeInHierarchy)
                {
                    spawnsInteriorAudio[item].Spawn.Release();
                }
                else
                {
                    Debug.LogWarning($"Skipped releasing spawn {spawnsInteriorAudio[item].Spawn.gameObject.name} because it is inactive.");
                }
                spawnsInteriorAudio.Remove(item);
            }
        }

        void HandleVFXRemoval(HashSet<Collider> currentItemsInSphere)
        {
            List<Collider> itemsToRemove = new List<Collider>();
            Vector3 cameraPosition = mainCameraComponent.transform.position;

            foreach (var lastItem in spawnsDrippingRain.Keys)
            {
                float distance = Vector3.Distance(cameraPosition, lastItem.transform.position);
                bool isOutsideView = !GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(mainCameraComponent), lastItem.bounds);

                if ((!currentItemsInSphere.Contains(lastItem) || isOutsideView) && distance > maxSpawnAndRemovalDistance)
                {
                    itemsToRemove.Add(lastItem);
                }
            }

            foreach (var item in itemsToRemove)
            {
                spawnsDrippingRain[item].Spawn.Release();
                spawnsDrippingRain.Remove(item);
            }
        }


        void ReleaseAllAudioSpawns()
        {
            foreach (var spawn in spawnsInteriorAudio.Values)
            {
                spawn.Spawn.Release();
            }
            spawnsInteriorAudio.Clear();
        }

        void ReleaseAllDrippingRainSpawns()
        {
            foreach (var spawn in spawnsDrippingRain.Values)
            {
                spawn.Spawn.Release();
            }
            spawnsDrippingRain.Clear();
        }

        void ReleaseAllVFXOcclusion()
        {
            foreach (var spawn in spawnsVFXOcclusion.Values)
            {
                spawn.VFXOcclusion.SetActive(false);
            }
            spawnsVFXOcclusion.Clear();
        }

#if UNITY_EDITOR
        void OnDrawGizmos()
        {
            if (debugSphereCast && player != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawWireSphere(player.transform.position, sphereCastRadius);
            }

            if (debugCameraFrustum && mainCameraComponent != null)
            {
                Gizmos.color = Color.red;
                Matrix4x4 originalMatrix = Gizmos.matrix;
                Gizmos.matrix = mainCameraComponent.transform.localToWorldMatrix;
                Gizmos.DrawFrustum(Vector3.zero, mainCameraComponent.fieldOfView, mainCameraComponent.farClipPlane, mainCameraComponent.nearClipPlane, mainCameraComponent.aspect);
                Gizmos.matrix = originalMatrix;
            }
        }
#endif
    }
}