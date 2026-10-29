//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using System.Collections.Generic;
using UnityEngine;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.RainWetness;
using MoveRotateResize.Dynamic.MultiTag;
using NaughtyAttributes;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MoveRotateResize.Dynamic.Occlusion
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Collider))]
    public class OcclusionZone : MonoBehaviour
    {
        GameObject player;
        DynamicRainWetness dynamicRainWetness;
        DynamicOcclusion dynamicOcclusion;
        Tag rainOccludeeTag;


        Collider zoneCollider;
        HashSet<Collider> activeColliders = new HashSet<Collider>();
        Dictionary<Collider, Material> originalMaterials = new Dictionary<Collider, Material>(); 
        HashSet<Collider> processedColliders = new HashSet<Collider>();
        LayerMask occludableGameObjectMask; 


    #if UNITY_EDITOR
        bool editModeOcclusion = false;
        bool editModeOcclusionDebug = false;
        float editModeOcclusionDebugSize;
    #endif

        void Start()
        {
            zoneCollider = GetComponent<Collider>();
            if (zoneCollider == null)
            {
                Debug.LogWarning("Zone Collider component not found!");
                return;
            }
            
            DetectObjectsInsideTrigger(); 
            CacheGlobalVariables();
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player;
            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
            dynamicOcclusion = DynamicSystemsManager.dynamicOcclusion;

            if (player == null || dynamicRainWetness == null || dynamicOcclusion == null)
            {
                Debug.LogWarning("One or more required components are missing.");
                return;
            }

            rainOccludeeTag = dynamicOcclusion.RainOccludeeTag;
            if (rainOccludeeTag == null)
            {
                Debug.LogWarning("Dynamic Occlusion Multi Tag is null!");
                return;
            }

            occludableGameObjectMask = dynamicOcclusion.occludableGameObjectMask;
            if (occludableGameObjectMask == 0)
            {
                Debug.LogWarning("Dynamic Occlusion Layer Mask is invalid!");
                return;
            }

            if (zoneCollider != null && player == null)
            {
                zoneCollider.isTrigger = true;
            }

            if (rainOccludeeTag != null && Application.isPlaying)
            {
                DetectObjectsInsideTrigger();
            }
        }

        void DetectObjectsInsideTrigger()
        {
            Collider[] colliders = Physics.OverlapBox(zoneCollider.bounds.center, zoneCollider.bounds.size / 2, Quaternion.identity, occludableGameObjectMask);
            foreach (var collider in colliders)
            {
                if (collider != null && collider.gameObject != null)
                {
                    // Debug.Log($"Object '{collider.gameObject.name}' detected inside trigger at start.");
                    OcclusionStartUp(collider, true);
                }
            }

            if (colliders.Length == 0)
            {
                // Debug.Log("No objects detected within trigger at start.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            HandleTrigger(other, true);
        }

        private void OnTriggerExit(Collider other)
        {
            HandleTrigger(other, false);
        }

        private void HandleTrigger(Collider other, bool isEntering)
        {
            if (other == null)
            {
                //Debug.LogError($"OcclusionZone '{gameObject.name}' - Collider 'other' is null.");
                return;
            }

            if ((occludableGameObjectMask & (1 << other.gameObject.layer)) == 0)
            {
                //Debug.Log($"OcclusionZone '{gameObject.name}' - Ignored '{other.gameObject.name}' (not in trigger layer mask).");
                return; 
            }

            if (isEntering)
            {
                //Debug.Log($"Triggering Enter for '{other.gameObject.name}' in OcclusionZone '{gameObject.name}'.");
                dynamicOcclusion.TriggerEnter(other);
            }
            else
            {
                //Debug.Log($"Triggering Exit for '{other.gameObject.name}' in OcclusionZone '{gameObject.name}'.");
                dynamicOcclusion.TriggerExit(other);
            }
        }

        private void OcclusionStartUp(Collider other, bool isEntering)
        {
            if (other == null || other.gameObject == null || !other.gameObject.activeInHierarchy)
            {
                return;
            }

            if (processedColliders.Contains(other))
            {
                return; 
            }

            if ((occludableGameObjectMask & (1 << other.gameObject.layer)) == 0)
            {
                return; 
            }

            if (isEntering)
            {
                if (other != null && other.gameObject != null)
                {
                    dynamicOcclusion.TriggerEnterStartUp(other);
                    processedColliders.Add(other);
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (EditorApplication.isPlaying)
            {
                //Debug.Log("Editor is in play mode, skipping Gizmos drawing.");
                return;
            }

            if (dynamicOcclusion == null || dynamicRainWetness == null)
            {
                dynamicOcclusion = DynamicSystemsManager.dynamicOcclusion;
                dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;

                if (dynamicOcclusion == null || dynamicRainWetness == null)
                {
                    Debug.LogWarning("dynamicOcclusion or dynamicRainWetness is still null after assignment.");
                    return;
                }
            }

            editModeOcclusion = dynamicOcclusion.editModeOcclusion;

            if (!editModeOcclusion)
            {
                return;
            }

            if (zoneCollider == null)
            {
                Debug.LogWarning($"OcclusionZone '{gameObject.name}' - zoneCollider is not assigned.");
                return;
            }

            editModeOcclusionDebug = dynamicOcclusion.editModeOcclusionDebug;
            editModeOcclusionDebugSize = dynamicOcclusion.editModeOcclusionDebugSize;

            Collider[] colliders = Physics.OverlapBox(zoneCollider.bounds.center, zoneCollider.bounds.size / 2, Quaternion.identity);
            HashSet<Collider> currentColliders = new HashSet<Collider>(colliders);

            foreach (var collider in colliders)
            {
                bool isOccludee = collider != zoneCollider && collider.gameObject.HasTag(rainOccludeeTag);
                if (isOccludee)
                {
                    //Debug.Log($"Occludee detected: {collider.gameObject.name}");

                    if (editModeOcclusionDebug)
                    {
                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(collider.bounds.center, editModeOcclusionDebugSize);
                        //Debug.Log($"Drawing debug sphere at {collider.bounds.center} with size {editModeOcclusionDebugSize}.");
                    }

                    if (!originalMaterials.ContainsKey(collider))
                    {
                        Renderer renderer = collider.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            originalMaterials[collider] = renderer.sharedMaterial;
                            //Debug.Log($"Saved original material for collider {collider.gameObject.name}.");
                        }
                        else
                        {
                            Debug.LogWarning($"Renderer not found for collider {collider.gameObject.name}, skipping material update.");
                        }
                    }

                    UpdateMaterial(collider, 0f);
                    activeColliders.Add(collider);
                }
            }

            foreach (var activeCollider in activeColliders)
            {
                if (!currentColliders.Contains(activeCollider))
                {
                    //Debug.Log($"Restoring original material for collider {activeCollider.gameObject.name}.");
                    if (originalMaterials.ContainsKey(activeCollider))
                    {
                        activeCollider.GetComponent<Renderer>().material = originalMaterials[activeCollider];
                    }
                    UpdateMaterial(activeCollider, 1f);
                }
            }

            activeColliders = currentColliders;
        }

        private void UpdateMaterial(Collider collider, float occlusionValue)
        {
            Renderer renderer = collider.GetComponent<Renderer>();
            if (renderer == null)
            {
                Debug.LogWarning($"Renderer not found for collider {collider.gameObject.name}, skipping material update.");
                return;
            }

            Material material = renderer.sharedMaterial;
            if (material == null || !material.HasProperty(dynamicOcclusion.globalShaderRainOcclusionID))
            {
                //Debug.LogWarning($"Material is null or doesn't have the property {dynamicOcclusion.globalShaderRainOcclusionID}, skipping.");
                return;
            }

            //Debug.Log($"Updating material for {collider.gameObject.name} with occlusion value {occlusionValue}.");
            
            // Create a new material instance only if occlusionValue is not 0 or 1
            Material instancedMaterial = new Material(material);
            renderer.material = instancedMaterial;
            instancedMaterial.SetFloat(dynamicOcclusion.globalShaderRainOcclusionID, occlusionValue);
        }
#endif
    }
}