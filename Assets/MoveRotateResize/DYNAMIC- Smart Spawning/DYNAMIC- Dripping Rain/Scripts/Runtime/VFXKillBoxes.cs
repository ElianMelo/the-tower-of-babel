using UnityEngine;
using UnityEngine.VFX;

namespace MoveRotateResize.Dynamic.DrippingRain
{
    [ExecuteInEditMode]
    public class VFXKillBoxes : MonoBehaviour
    {
        public GameObject visualEffectObject; // Reference to the GameObject containing the VisualEffect
        private VisualEffect visualEffect;    // Internal reference to the VisualEffect component

        const string vfxKillBoxPosition = "_VFXKillBox_Position_01";
        const string vfxKillBoxScale = "_VFXKillBox_Scale_01";

        void Awake()
        {
            AssignVisualEffect();
            UpdateCollider();
        }

        void Update()
        {
            UpdateCollider();
        }

        public void AssignVisualEffect()
        {
            if (visualEffectObject != null)
            {
                visualEffect = visualEffectObject.GetComponent<VisualEffect>();
                if (visualEffect == null)
                {
                    Debug.LogError($"No VisualEffect component found on GameObject: {visualEffectObject.name}");
                }
            }
            else
            {
                Debug.LogError("No GameObject assigned to visualEffectObject in VFXKillBoxes script.");
            }
        }

        public void UpdateCollider()
        {
            if (visualEffect != null)
            {
                // Get inverted world-space position
                Vector3 invertedWorldPosition = GetInvertedWorldPosition();

                // Log the original and inverted positions for debugging
                Debug.Log($"Original Position: {transform.position}");
                Debug.Log($"Inverted Position: {invertedWorldPosition}");

                // Update the VisualEffect properties
                visualEffect.SetVector3(vfxKillBoxPosition, invertedWorldPosition); // Inverted world space position
                visualEffect.SetVector3(vfxKillBoxScale, transform.localScale);    // Local scale remains unchanged
            }
            else
            {
                Debug.LogWarning("VisualEffect reference is missing. UpdateCollider() cannot update values.");
            }
        }

        Vector3 GetInvertedWorldPosition()
        {
            Vector3 worldPosition = transform.position; // Get the world position

            // Invert X and Z positions
            return new Vector3(
                -worldPosition.x, // Invert X
                worldPosition.y,  // Y remains as-is
                -worldPosition.z  // Invert Z
            );
        }
    }
}
