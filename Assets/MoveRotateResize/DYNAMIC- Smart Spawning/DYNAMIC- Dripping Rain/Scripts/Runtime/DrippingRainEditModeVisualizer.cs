//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEditor;
using UnityEngine;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [System.Serializable]
    [ExecuteInEditMode]
    [RequireComponent(typeof(Collider))]
    public class DrippingRainEditModeVisualizer : MonoBehaviour
    {
        public GameObject vfxPrefab;
        GameObject vfxInstance;
        BoxCollider spawnPoint;

        private void OnEnable()
        {
            spawnPoint = GetComponent<BoxCollider>();
            if (spawnPoint == null)
            {
                Debug.LogError("DrippingRainEditModeVisualizer requires a BoxCollider component.");
                enabled = false;
            }
        }

        public GameObject CreateVFXInstance()
        {
            if (vfxInstance != null)
            {
                return vfxInstance;  
            }

            if (vfxPrefab == null)
            {
                Debug.LogWarning("DrippingRainEditModeVisualizer: No VFX Prefab assigned.");
                return null; 
            }

#if UNITY_EDITOR
            vfxInstance = PrefabUtility.InstantiatePrefab(vfxPrefab) as GameObject;
#else
            vfxInstance = Instantiate(vfxPrefab);
#endif

            if (vfxInstance != null)
            {
                SpawnDrippingRain spawn = vfxInstance.GetComponent<SpawnDrippingRain>();

                if (spawn != null)
                {
                    var newTransform = spawn.transform;
                    newTransform.position = spawnPoint.transform.position;
                    newTransform.rotation = spawnPoint.transform.rotation;
                    newTransform.localScale = spawnPoint.transform.localScale;

                    spawn.UpdateSpawnSize(spawnPoint, spawn);
                }
                else
                {
                    Debug.LogWarning("DrippingRainEditModeVisualizer: No SpawnDrippingRain component found on VFX instance.");
                }
            }

            return vfxInstance; 
        }
    }
}