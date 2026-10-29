//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Rendering;
using MoveRotateResize.Dynamic.MultiTag;
using System.Collections;
using System.Collections.Generic;
using NaughtyAttributes;
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.ReflectionProbes
{
    public class DynamicReflectionProbes : MonoBehaviour
    {
        [Foldout("Sphere Cast")][SerializeField] LayerMask triggerLayer;
        [Foldout("Sphere Cast")][SerializeField] Tag ReflectionProbeTag = null;
        [Foldout("Sphere Cast")][SerializeField] float detectionRadius = 25f;
    #if UNITY_EDITOR
        [Foldout("Sphere Cast")][SerializeField] bool debugDetectionRadius = true; 
    #endif
        Transform player;
        [SerializeField] int maxActiveProbesLimit = 8;
        int maxActiveProbes;
        Dictionary<ReflectionProbe, bool> probeStates = new Dictionary<ReflectionProbe, bool>();
        List<ReflectionProbe> detectedProbes = new List<ReflectionProbe>();
        WaitForSeconds waitTime = new WaitForSeconds(2f);


        private void Start()
        {
            CacheGlobalVariables();

            // Ported from HDRP: URP has no on-screen cube reflection limit, so it is set here.
            maxActiveProbes = Mathf.Max(0, maxActiveProbesLimit);

            ReflectionProbe[] allProbes = FindObjectsOfType<ReflectionProbe>();
            foreach (var probe in allProbes)
            {
                probeStates[probe] = probe.enabled;
            }

            StartCoroutine(CheckReflectionProbesRoutine());
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player.transform;
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }

        private IEnumerator CheckReflectionProbesRoutine()
        {
            while (true)
            {
                CheckAndSetReflectionProbes();
                yield return waitTime;
            }
        }

        private void CheckAndSetReflectionProbes()
        {
            detectedProbes.Clear();

            Collider[] hitColliders = Physics.OverlapSphere(transform.position, detectionRadius, triggerLayer);
            foreach (Collider col in hitColliders)
            {
                ReflectionProbe probe = col.GetComponent<ReflectionProbe>();
                if (probe != null && probeStates.ContainsKey(probe))
                {
                    if (col.HasTag(ReflectionProbeTag))
                    {
                        detectedProbes.Add(probe);
                        #if DEVELOPMENT_BUILD || UNITY_EDITOR
                        Debug.Log("Found reflection probe with correct tag: " + col.gameObject.name);
                        #endif
                    }
                }
            }

            detectedProbes.Sort((p1, p2) => Vector3.Distance(player.position, p1.transform.position)
                                        .CompareTo(Vector3.Distance(player.position, p2.transform.position)));

            int activeProbesCount = 0;

            foreach (ReflectionProbe probe in detectedProbes)
            {
                float distance = Vector3.Distance(player.position, probe.transform.position);
                bool shouldBeActive = distance <= detectionRadius;

                if (shouldBeActive && !probeStates[probe])
                {
                    probe.enabled = true;
                    probeStates[probe] = true;
                    #if DEVELOPMENT_BUILD || UNITY_EDITOR
                    Debug.Log("Enabling Reflection Probe for: " + probe.gameObject.name);
                    #endif

                }
                else if (!shouldBeActive && probeStates[probe])
                {
                    probe.enabled = false;
                    probeStates[probe] = false;
                    #if DEVELOPMENT_BUILD || UNITY_EDITOR
                    Debug.Log("Disabling Reflection Probe for: " + probe.gameObject.name);
                    #endif
                }

                if (shouldBeActive)
                {
                    activeProbesCount++;
                    if (activeProbesCount >= maxActiveProbes)
                        break;
                }
            }

            #if DEVELOPMENT_BUILD || UNITY_EDITOR
            //Debug.Log("Total active reflection probes count: " + activeProbesCount);
            #endif
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if(debugDetectionRadius)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position, detectionRadius);
            }
        }      
#endif
    }
}