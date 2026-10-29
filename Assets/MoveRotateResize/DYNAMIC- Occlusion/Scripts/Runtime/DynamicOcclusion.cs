//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.2
//------------------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using MoveRotateResize.Dynamic.MultiTag;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.RainWetness;
using MoveRotateResize.Dynamic.FallingRain;
using MoveRotateResize.Dynamic.SDFOcclusionZoneCreator;
using NaughtyAttributes;

namespace MoveRotateResize.Dynamic.Occlusion
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(DynamicSystemsManager))]
    public class DynamicOcclusion : MonoBehaviour
    {
        GameObject player;
        DynamicRainWetness dynamicRainWetness;
        DynamicFallingRain dynamicFallingRain;


        [InfoBox("DON'T Rotate Occlusion Zones. As the system currently isn't compatible with Occlusion Zones being rotated", EInfoBoxType.Warning)]
        [MarkupAttributes.Foldout("Occlusion Zones Settings")] 
        public LayerMask occludableGameObjectMask = ~0;  
        public Tag OcclusionZoneTag;
        public Tag RainOccludeeTag;
        [SerializeField] float occlusionTransitionDuration = 2.0f;


        [MarkupAttributes.Foldout("Occlusion Zones Material")] 
        [SerializeField] Material occlusionZoneMaterial;
        [SerializeField] [Range(0f, 1f)] public float occlusionZoneMaterialAlpha = 1f;


        [MarkupAttributes.Foldout("Occlusion Activation Sphere")]
        [SerializeField] float occlusionActivationSphereSize = 15f; 
#if UNITY_EDITOR
        [SerializeField] bool debugOcclusionSphereCast = true;
#endif


        [MarkupAttributes.Foldout("VFX Occlusion")] 
        public GameObject[] VFXOcclusionSpawns;


        [MarkupAttributes.Foldout("SDF Occlusion")] 
        [InfoBox("Assign YOUR created SDF gameobject that's located as a child of the SDF Occlusion Zone gameobject", EInfoBoxType.Warning)]
        public GameObject YourCreatedSDFOcclusionZone;
        Vector3 sdfTransform;
        Quaternion sdfQuaternion;
        Vector3 sdfScale;
        const string SDFTexture01 = "SDF_Texture_01";
        const string SDFBoxCenter01 = "SDF_BoxCenter_01";
        const string SDFBoxSize01 = "SDF_BoxSize_01";



        [MarkupAttributes.Foldout("Edit Mode Occlusion")]
        [InfoBox("In Edit Mode textures of the Occluded object can't be edited.", EInfoBoxType.Warning)]
        [InfoBox("1.) Press 'Toggle Editor Gizmos' in your scene view.", EInfoBoxType.Normal)]
        [InfoBox("2.) Enter Playmode once (only needs to be done once everytime you open the editor)", EInfoBoxType.Normal)]
        [InfoBox("3.) Rress 'Enter Edit Mode Occlusion'.", EInfoBoxType.Normal)]
        [InfoBox("4.) Press 'Exit Edit Mode Occlusion' to stop using.", EInfoBoxType.Normal)]
        public bool editModeOcclusionDebug = true;
        [SerializeField] bool debugShowEditModeUpdateWarning = true;
        public float editModeOcclusionDebugSize = 0.5f;
        [HideInInspector] public bool editModeOcclusion = false;

        Dictionary<GameObject, Material> originalMaterials = new Dictionary<GameObject, Material>();
        [HideInInspector] public int globalShaderRainOcclusionID;
        float rainOcclusionValueInside = 0.0f;
        float rainOcclusionValueOutside = 1.0f;
        Dictionary<Renderer, float> rendererOcclusionValues = new Dictionary<Renderer, float>();

        void OnValidate()
        {
            CacheGlobalVariables();

#if UNITY_EDITOR
            if(!Application.isPlaying && debugShowEditModeUpdateWarning)
            {
                Debug.Log("If using Edit Mode Occlusion. Please re-press 'Enter Edit Mode Occlusion' to update gameobjects.");
            }
#endif
            globalShaderRainOcclusionID = Shader.PropertyToID("Rain_Occlusion");

            if (occlusionZoneMaterial == null)
            {
                Debug.LogWarning("Occlusion Zone Material is not assigned. Please assign a material in the Inspector.");
            }
            else
            {
                SetAlpha(occlusionZoneMaterialAlpha);
            }

            //UpdateVisualEffectsGraphSDF();
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (occlusionZoneMaterial == null)
            {
                Debug.LogWarning("Occlusion Zone Material is not assigned. Please assign a material in the Inspector.");
            }
            else
            {
                SetAlpha(occlusionZoneMaterialAlpha);
            }
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;

            if (!Application.isPlaying)
            {
                editModeOcclusion = false;
            }
        }

        void OnDestroy()
        {
            rendererOcclusionValues.Clear();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!Application.isPlaying)
            {
                Debug.Log("New scene loaded. You may need to save materials for new objects.");
            } 
        }

        private void OnTransformChildrenChanged()
        {
            if (!Application.isPlaying)
            {
                Debug.Log("A new object was added to the scene. Please update the saved materials.");
            }     
        }
        
#endif
        void Awake()
        {
            ExitEditModeOcclusion();

            if (Application.isPlaying)
            {
                foreach (var spawn in VFXOcclusionSpawns)
                {
                    if (spawn != null)
                    {
                        spawn.SetActive(false);
                    }
                }
            }
        }

        void Start()
        { 
            CacheGlobalVariables();
            globalShaderRainOcclusionID = Shader.PropertyToID("Rain_Occlusion");
            UpdateVisualEffectsGraphSDF();

            if (occlusionZoneMaterial == null)
            {
                Debug.LogError("occlusionZoneMaterial is not assigned. Please assign it in the Inspector.");
                return;
            }
            else
            {
                SetAlpha(occlusionZoneMaterialAlpha);
            }      
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player;
            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
            dynamicFallingRain = DynamicSystemsManager.dynamicFallingRain;
        }

        public void TriggerEnterStartUp(Collider other)
        {
            if (other == null || !other.gameObject)
            {
                return;
            }

            if (!other.HasTag(RainOccludeeTag))
            {
                // Debug.Log("Object does not have the RainOccludeeTag: " + other.gameObject.name);
                return;
            }

            Renderer renderer = other.GetComponent<Renderer>();

            if (renderer != null)
            {
                if (!rendererOcclusionValues.ContainsKey(renderer))
                {
                    float distanceToPlayer = Vector3.Distance(player.transform.position, renderer.transform.position);
                    //Debug.Log("Distance to player (" + other.gameObject.name + "): " + distanceToPlayer);

                    if (distanceToPlayer < occlusionActivationSphereSize)
                    {
                        // Debug.Log(
                        //     "Distance is within occlusion activation sphere size (" +
                        //     occlusionActivationSphereSize + "): " + other.gameObject.name
                        // );
                        rendererOcclusionValues[renderer] = rainOcclusionValueOutside;

                        Material material = renderer.material;
                        if (material != null)
                        {
                            //Debug.Log("Renderer material found for object (" + other.gameObject.name + "): " + material.name);
                            if (material.HasProperty(globalShaderRainOcclusionID))
                            {
                                material.SetFloat(globalShaderRainOcclusionID, rainOcclusionValueInside);
                            }
                            else
                            {
                                // Debug.LogWarning(
                                //     "Material (" + material.name + ") for object (" + other.gameObject.name + ") 
                                //    does not have property for rain occlusion ID: " + globalShaderRainOcclusionID
                                // );
                            }
                        }
                        else
                        {
                            //Debug.LogWarning("Renderer material is null for object: " + other.gameObject.name);
                        }
                    }
                    else
                    {
                        //Debug.Log("Distance exceeds occlusion activation sphere size for object: " + other.gameObject.name);
                    }
                }
                else
                {
                    //Debug.Log("Renderer is already in rendererOcclusionValues dictionary for object: " + other.gameObject.name);
                }
            }
            else
            {
                //Debug.LogWarning("Renderer component is null for object: " + other.gameObject.name);
            }

            if (other.gameObject == player)
            {
                dynamicRainWetness.isPlayerOccluded = true;
                //Debug.Log("Player is now occluded by trigger.");
            }
        }

        public void TriggerEnter(Collider other)
        {          
            if (other.gameObject.HasTag(RainOccludeeTag))
            {
                Renderer renderer = other.GetComponent<Renderer>();
                if (renderer != null && !rendererOcclusionValues.ContainsKey(renderer) &&
                    Vector3.Distance(player.transform.position, renderer.transform.position) < occlusionActivationSphereSize)
                {
                    rendererOcclusionValues.Add(renderer, rainOcclusionValueOutside);
                    StartCoroutine(EnterTriggerUpdateShader(renderer, rainOcclusionValueInside));
                    //Debug.Log("Entered trigger: " + other.gameObject.name);
                }

                if (other.gameObject == player)
                {
                    dynamicRainWetness.isPlayerOccluded = true;
                    //Debug.Log("Inside trigger zone set to true.");
                }
            }
        }

        public void TriggerExit(Collider other)
        {
            if (other.gameObject.HasTag(RainOccludeeTag))
            {
                Renderer renderer = other.GetComponent<Renderer>();
                if (renderer != null && rendererOcclusionValues.ContainsKey(renderer))
                {
                    StartCoroutine(ExitTriggerUpdateShader(renderer, rainOcclusionValueOutside));
                    //Debug.Log("Exited trigger: " + other.gameObject.name);
                }

                if (other.gameObject == player)
                {
                    dynamicRainWetness.isPlayerOccluded = false;
                    //Debug.Log("Inside trigger zone set to false.");
                }
            }
        }

        private IEnumerator EnterTriggerUpdateShader(Renderer renderer, float target)
        {
            if (!renderer)
            {
                Debug.LogWarning("Renderer is null!");
                yield break;
            }

            Material material = renderer.material;
            if (!material || !material.HasProperty(globalShaderRainOcclusionID))
            {
                //Debug.LogWarning("Material or shader property not found!");
                yield break;
            }

            float startValue = material.GetFloat(globalShaderRainOcclusionID);
            float elapsedTime = 0f;

            while (elapsedTime < occlusionTransitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / occlusionTransitionDuration);
                float newValue = Mathf.Lerp(startValue, target, t);
                material.SetFloat(globalShaderRainOcclusionID, newValue);
                yield return null;
            }

            material.SetFloat(globalShaderRainOcclusionID, target);

            if (target == rainOcclusionValueOutside)
            {
                if (rendererOcclusionValues.ContainsKey(renderer))
                {
                    rendererOcclusionValues.Remove(renderer);
                }
            }
        }

        private IEnumerator ExitTriggerUpdateShader(Renderer renderer, float target)
        {
            if (!renderer)
            {
                Debug.LogWarning("Renderer is null!");
                yield break;
            }

            Material material = renderer.material;
            if (!material || !material.HasProperty(globalShaderRainOcclusionID))
            {
                //Debug.LogWarning("Material or shader property not found!");
                yield break;
            }

            float startValue = material.GetFloat(globalShaderRainOcclusionID);
            float elapsedTime = 0f;

            while (elapsedTime < occlusionTransitionDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / occlusionTransitionDuration);
                float newValue = Mathf.Lerp(startValue, target, t);
                material.SetFloat(globalShaderRainOcclusionID, newValue);
                yield return null;
            }

            material.SetFloat(globalShaderRainOcclusionID, target);

            if (target == rainOcclusionValueOutside)
            {
                if (rendererOcclusionValues.ContainsKey(renderer))
                {
                    rendererOcclusionValues.Remove(renderer);
                }
            }
        }

        public void EnterEditModeOcclusion()
        {
            editModeOcclusion = false;

            Renderer[] renderers = FindObjectsOfType<Renderer>();
            foreach (var renderer in renderers)
            {
                if (!originalMaterials.ContainsKey(renderer.gameObject))
                {
                    originalMaterials[renderer.gameObject] = renderer.sharedMaterial;
                    //Debug.Log($"Saved original material for {renderer.gameObject.name}.");
                }
            }

            editModeOcclusion = true;
            Debug.Log("Entered Edit Mode Occlusion.");
        }

        public void ExitEditModeOcclusion()
        {
            editModeOcclusion = false;

            if (originalMaterials.Count > 0)
            {
                foreach (var entry in originalMaterials)
                {
                    GameObject obj = entry.Key;
                    Material originalMaterial = entry.Value;

                    Renderer renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material = originalMaterial;
                        //Debug.Log($"Restored material for {obj.name}.");
                    }
                }

                originalMaterials.Clear();
            }
            else
            {
                //Debug.Log("No original materials to restore.");
            }

            Debug.Log("Exited Edit Mode Occlusion.");
        }

        public void SetAlpha(float newAlpha)
        {
            newAlpha = Mathf.Clamp01(newAlpha); 

            Color color = occlusionZoneMaterial.GetColor("_Color");
            color.a = newAlpha;
            occlusionZoneMaterial.SetColor("_Color", color);
        }

        public void UpdateVisualEffectsGraphSDF()
        {
            if (!dynamicFallingRain.enableSDFOcclusion) return;
            
            string scriptName = "Dynamic Occlusion"; 
            //Debug.Log($"{scriptName}: Updating Visual Effects Graph SDF.");

            if (dynamicFallingRain.fallingRainVFXGraph == null)
            {
                //Debug.LogWarning($"{scriptName}: failingRainVFXGraph is not assigned.");
                return;
            }


            if (YourCreatedSDFOcclusionZone == null)
            {
                Debug.LogError($"{scriptName}: You're using a Falling Rain VFX that requires an SDF. Please create and assign an SDF in the Dynamic Occlusion script and place in the 'Your Created SDF Occlusion' slot.");
                return;
            }
            sdfTransform = YourCreatedSDFOcclusionZone.transform.position; 
            sdfQuaternion = YourCreatedSDFOcclusionZone.transform.rotation; 
            sdfScale = YourCreatedSDFOcclusionZone.transform.localScale; 

            dynamicFallingRain.fallingRainVFXGraph.SetVector3("SDF_Position", sdfTransform);
            dynamicFallingRain.fallingRainVFXGraph.SetVector3("SDF_Rotation", sdfQuaternion.eulerAngles);
            dynamicFallingRain.fallingRainVFXGraph.SetVector3("SDF_Scale", sdfScale); 


            SDFInfo sdfInfo = YourCreatedSDFOcclusionZone.GetComponent<SDFInfo>();
            if (sdfInfo == null)
            {
                Debug.LogWarning($"{scriptName}: SDFInfo component is missing on YourCreatedSDFOcclusionZone.");
                return;
            }

            try
            {
                sdfInfo.GetBoxColliderInfo();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{scriptName}: Failed to get box collider info: {ex.Message}");
                return;
            }

            Vector3 boxColliderCenter = sdfInfo.boxColliderCenter;
            Vector3 boxColliderSize = sdfInfo.boxColliderSize;

            if (boxColliderCenter == Vector3.zero || boxColliderSize == Vector3.zero)
            {
                Debug.LogWarning($"{scriptName}: Invalid box collider information (center or size is zero).");
                return;
            }

            Texture3D sdfTexture = sdfInfo.texture3D;
            if (sdfTexture == null)
            {
                Debug.LogWarning($"{scriptName}: SDF Texture is missing or invalid.");
                return;
            }
            
            dynamicFallingRain.fallingRainVFXGraph.SetTexture(SDFTexture01, sdfTexture);
            dynamicFallingRain.fallingRainVFXGraph.SetTexture(SDFTexture01, sdfTexture);
            dynamicFallingRain.fallingRainVFXGraph.SetVector3(SDFBoxCenter01, boxColliderCenter);
            dynamicFallingRain.fallingRainVFXGraph.SetVector3(SDFBoxSize01, boxColliderSize);
        }

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
                            if (material == occlusionZoneMaterial)
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

#if UNITY_EDITOR            
        private void OnDrawGizmos()
        {
            if (debugOcclusionSphereCast && player != null)
            {
                Gizmos.color = Color.yellow;  
                Gizmos.DrawWireSphere(player.transform.position, occlusionActivationSphereSize);  
            }
        }
#endif
    }
}