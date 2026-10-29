//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//-------------------------------------------------------------------------
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.VFX;
using MoveRotateResize.Dynamic.SystemsManager;
using MoveRotateResize.Dynamic.RainWetness;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [ExecuteInEditMode]
    public class SpawnDrippingRain : MonoBehaviour
    {
        DynamicRainWetness dynamicRainWetness;
        [SerializeField, HideInInspector] VisualEffect visualEffect;
        [SerializeField, HideInInspector] BoxCollider boxCollider;
        [SerializeField, HideInInspector] SpawnDrippingRain spawnFollow;


        [SerializeField] bool moveableObject = false;


        bool isEditModeSpawn = false;
        bool isReleasing = false;
        float currentGlobalRain;
        float currentGlobalRainExtended;
        float previousGlobalRain = -1f;
        float previousGlobalRainExtended = -1f;
        Vector3 spawnScale;


        static readonly string GlobalRainParam = "_Global_Rain";
        static readonly string GlobalRainExtendedParam = "_Global_Rain_Extended";
        static readonly string ScaleParameter = "spawnScale";


        IObjectPool<SpawnDrippingRain> spawnDrippingRain;
        public IObjectPool<SpawnDrippingRain> ObjectPool
        {
            set => spawnDrippingRain = value;
        }

        private void Awake()
        {
            if (isEditModeSpawn)
            {
                Destroy(this); 
                return;
            }

            if (!TryGetComponent(out visualEffect))
            {
                DisableWithWarning("Missing VisualEffect component.");
                return;
            }

            dynamicRainWetness = DynamicSystemsManager.dynamicRainWetness;
            if (dynamicRainWetness == null)
            {
                DisableWithWarning("Missing DynamicRainWetness reference.");
            }
        }

        private void DisableWithWarning(string message)
        {
            Debug.LogWarning($"[SpawnDrippingRain] {gameObject.name}: {message}", gameObject);
            enabled = false;
        }

        void Update()
        {
            if (isEditModeSpawn) return;

            UpdateGlobalRain();
            UpdateGlobalRainExtended();

            if (moveableObject)
            {
                UpdateSpawnTransform();

                if (boxCollider != null && spawnFollow != null)
                {
                    Vector3 newScale = DetermineSpawnScale();
                    if (!newScale.Equals(spawnScale))
                    {
                        spawnScale = newScale;
                        UpdateSpawnSize(boxCollider, spawnFollow);
                    }
                }
            }
        }

        void OnDisable()
        {
            isReleasing = false;
            boxCollider = null;
        }

        public void Release()
        {
            if (spawnDrippingRain != null && !isReleasing)
            {
                isReleasing = true;
                spawnDrippingRain.Release(this);
            }
        }

        void UpdateSpawnTransform()
        {
            if (boxCollider == null || spawnFollow == null)
            {
                return;
            }

            spawnFollow.transform.SetPositionAndRotation(boxCollider.transform.position, boxCollider.transform.rotation);
        }

        void UpdateGlobalRain()
        {
            if (visualEffect?.isActiveAndEnabled != true || dynamicRainWetness == null)
                return;

            currentGlobalRain = dynamicRainWetness.globalRain;

            if (!Mathf.Approximately(currentGlobalRain, previousGlobalRain))
            {
                visualEffect.SetFloat(GlobalRainParam, currentGlobalRain);
                previousGlobalRain = currentGlobalRain;
            }
        }

        void UpdateGlobalRainExtended()
        {
            if (visualEffect?.isActiveAndEnabled != true || dynamicRainWetness == null)
                return;

            currentGlobalRainExtended = dynamicRainWetness.globalRainExtended;

            if (!Mathf.Approximately(currentGlobalRainExtended, previousGlobalRainExtended))
            {
                visualEffect.SetFloat(GlobalRainExtendedParam, currentGlobalRainExtended);
                previousGlobalRainExtended = currentGlobalRainExtended;
            }
        }

        public void UpdateSpawnSize(Collider spawnPoint, SpawnDrippingRain spawn)
        {
            if (visualEffect == null || spawnPoint == null)
            {
                return;
            }

            if (spawnPoint is not BoxCollider boxCollider)
            {
                return;
            }

            spawnFollow = spawn;
            this.boxCollider = boxCollider;

            spawnScale = DetermineSpawnScale();
            visualEffect.SetVector3(ScaleParameter, spawnScale);
            spawn.transform.localScale = Vector3.one;
        }

       private Vector3 DetermineSpawnScale()
        {
            if (moveableObject)
            {
                if (boxCollider.transform.parent != null && boxCollider.transform.parent.localScale != Vector3.one)
                {
                    return Vector3.one; 
                }
            }

            Vector3 calculatedScale = boxCollider.transform.localScale;
            return calculatedScale;
        }
    }
}