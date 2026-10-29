//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor;
using NaughtyAttributes;

namespace MoveRotateResize.Dynamic.ReflectionProbes
{
    [ExecuteInEditMode]
    public class DynamicReflectionProbesSpawner : MonoBehaviour
    {
#if UNITY_EDITOR

        [MarkupAttributes.Foldout("Box Collider")]
        [SerializeField] BoxCollider reflectionProbeSpawner;
        [SerializeField] Vector3 boxSize = new Vector3(10, 10, 10);


        [MarkupAttributes.Foldout("Reflection Probe Spawns")]
        [SerializeField] GameObject reflectionProbePrefab;
        [SerializeField] LayerMask spawnLayer = 1 << 0; 
        [SerializeField] float spawnYOffset = 9f; 
        [SerializeField] float rowSpacing = 40f;
        [SerializeField] float columnSpacing = 40f; 


        [MarkupAttributes.Foldout("Debugs")]
        [SerializeField] bool debugReflectionProbeSpawn = true; 
        [SerializeField] bool debugHitRays = true; 
        [SerializeField] bool debugMissedRays = true;


        void OnValidate()
        {
            if (reflectionProbeSpawner != null)
                reflectionProbeSpawner.size = boxSize;
        }

         void Start()
        {
            if (Application.isPlaying)
            {
                Destroy(this);
            }
        }

        public void SpawnReflectionProbes()
        {
            if (reflectionProbeSpawner == null)
            {
                Debug.LogError("No BoxCollider attached to the GameObject.");
                return;
            }

            if (reflectionProbePrefab == null)
            {
                Debug.LogError("Reflection Probe Prefab is not assigned.");
                return;
            }

            Vector3 startPos = reflectionProbeSpawner.bounds.min;

            int gridSizeX = Mathf.CeilToInt(reflectionProbeSpawner.size.x / rowSpacing);
            int gridSizeZ = Mathf.CeilToInt(reflectionProbeSpawner.size.z / columnSpacing);
            int probeCount = 0;

            for (int x = 0; x < gridSizeX; x++)
            {
                for (int z = 0; z < gridSizeZ; z++)
                {
                    Vector3 probePosition = startPos + new Vector3(x * rowSpacing, 0f, z * columnSpacing);

                    if (Physics.Raycast(new Vector3(probePosition.x, reflectionProbeSpawner.bounds.max.y + 1000f, probePosition.z), Vector3.down, out RaycastHit hit, Mathf.Infinity, spawnLayer))
                    {
                        probePosition.y = hit.point.y + spawnYOffset;
                    }
                    else
                    {
                        continue;
                    }

                    var newProbe = PrefabUtility.InstantiatePrefab(reflectionProbePrefab) as GameObject;
                    newProbe.transform.position = probePosition;
                    newProbe.transform.rotation = Quaternion.identity;

                    probeCount++;
                    newProbe.name = reflectionProbePrefab.name + "_" + probeCount.ToString("00");
                    newProbe.transform.parent = reflectionProbeSpawner.transform;
                }
            }
        }

        public void RemoveReflectionProbes()
        {
            for (int i = reflectionProbeSpawner.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = reflectionProbeSpawner.transform.GetChild(i);
                if (child.name.StartsWith(reflectionProbePrefab.name))
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        void OnDrawGizmosSelected()
        {
            if (reflectionProbeSpawner == null)
                reflectionProbeSpawner = GetComponent<BoxCollider>();

            if (reflectionProbeSpawner != null && debugReflectionProbeSpawn)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(reflectionProbeSpawner.bounds.center, reflectionProbeSpawner.size);
            }

            if (reflectionProbeSpawner != null && debugHitRays)
            {
                Vector3 startPos = reflectionProbeSpawner.bounds.min;

                int gridSizeX = Mathf.CeilToInt(reflectionProbeSpawner.size.x / rowSpacing);
                int gridSizeZ = Mathf.CeilToInt(reflectionProbeSpawner.size.z / columnSpacing);

                for (int x = 0; x < gridSizeX; x++)
                {
                    for (int z = 0; z < gridSizeZ; z++)
                    {
                        Vector3 rayPosition = startPos + new Vector3(x * rowSpacing, 0f, z * columnSpacing);

                        if (Physics.Raycast(new Vector3(rayPosition.x, reflectionProbeSpawner.bounds.max.y + 100f, rayPosition.z), Vector3.down, out RaycastHit hit, Mathf.Infinity, spawnLayer))
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawLine(hit.point + Vector3.up * spawnYOffset, hit.point);
                        }
                        else if (debugMissedRays)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawLine(rayPosition + Vector3.up * 100f, rayPosition + Vector3.up * spawnYOffset);
                        }
                    }
                }
            }

            if (reflectionProbeSpawner != null && debugReflectionProbeSpawn)
            {
                Vector3 startPos = reflectionProbeSpawner.bounds.min;
                int gridSizeX = Mathf.CeilToInt(reflectionProbeSpawner.size.x / rowSpacing);
                int gridSizeZ = Mathf.CeilToInt(reflectionProbeSpawner.size.z / columnSpacing);

                for (int x = 0; x < gridSizeX; x++)
                {
                    for (int z = 0; z < gridSizeZ; z++)
                    {
                        Vector3 rayPosition = startPos + new Vector3(x * rowSpacing, 0f, z * columnSpacing);

                        if (Physics.Raycast(new Vector3(rayPosition.x, reflectionProbeSpawner.bounds.max.y + 100f, rayPosition.z), Vector3.down, out RaycastHit hit, Mathf.Infinity, spawnLayer))
                        {
                            BoxCollider prefabBoxCollider = reflectionProbePrefab.GetComponent<BoxCollider>();
                            if (prefabBoxCollider != null)
                            {
                                Vector3 colliderPosition = hit.point + Vector3.up * spawnYOffset;
                                Gizmos.color = Color.cyan;
                                Gizmos.DrawWireCube(colliderPosition + prefabBoxCollider.center, prefabBoxCollider.size);
                            }
                        }
                    }
                }
            }
        }
#endif
    }
}
