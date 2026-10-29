//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;
using UnityEditor; 
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.FallingRainPlayerFollower
{
    [ExecuteInEditMode]
    public class DynamicFallingRainPlayerFollower : MonoBehaviour
    {
        [MarkupAttributes.Foldout("Offsets")]
        [SerializeField] Vector3 positionOffset = Vector3.zero;
        [SerializeField] Vector3 cameraLookDirectionOffset = Vector3.zero;


        [MarkupAttributes.Foldout("Angle Limits")]
        [SerializeField] float minCameraLookOffsetAngle = 10f;
        [SerializeField] float maxCameraLookOffsetAngle = 100f;


        [MarkupAttributes.Foldout("Debug")]
#if UNITY_EDITOR
        [SerializeField] bool enableDebugAngles = true;
#endif


        GameObject mainCamera;
        Transform cameraTransform;       
        Vector3 cameraVelocity = Vector3.zero;
        Vector3 cameraForward, cameraRight, cameraUp;
        bool cameraHasMoved;
        float followThreshold = 0.1f;


        void Start()
        {
            CacheGlobalVariables();
            if (mainCamera != null)
            {
                cameraTransform = mainCamera.transform;
                UpdateCameraDirections();

                transform.position = cameraTransform.position + positionOffset;
            }
            else
            {
                Debug.LogWarning("Main camera is null.");
            }
        }

        void CacheGlobalVariables()
        {
            mainCamera = DynamicSystemsManager.mainCamera;
        }

        void UpdateCameraDirections()
        {
            cameraForward = cameraTransform.forward;
            cameraRight = cameraTransform.right;
            cameraUp = cameraTransform.up;
        }

        void Update()
        {
            if (!Application.isPlaying || mainCamera == null) return;

            if (cameraTransform.hasChanged)
            {
                UpdateCameraDirections();
                cameraHasMoved = true;
            }

            FollowCameraView();
        }

        private void FollowCameraView()
        {
            float cameraPitchAngle = Vector3.Angle(Vector3.up, cameraTransform.up);
            cameraPitchAngle = cameraTransform.forward.y < 0 ? -cameraPitchAngle : cameraPitchAngle;

            Vector3 directionOffset = GetDirectionOffset(cameraPitchAngle);
            Vector3 targetPosition = cameraTransform.position + directionOffset + positionOffset;

            if ((transform.position - targetPosition).sqrMagnitude > followThreshold * followThreshold)
            {
                transform.position = targetPosition;
            }
        }

        private Vector3 GetDirectionOffset(float cameraPitchAngle)
        {
            if (cameraPitchAngle >= minCameraLookOffsetAngle && cameraPitchAngle <= maxCameraLookOffsetAngle)
            {
                return cameraForward * cameraLookDirectionOffset.x +
                       cameraUp * cameraLookDirectionOffset.y +
                       cameraRight * cameraLookDirectionOffset.z;
            }
            return Vector3.zero;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!enableDebugAngles || mainCamera == null) return;

            if (cameraHasMoved)
            {
                Gizmos.color = Color.blue;

                float cameraPitchAngle = Vector3.Angle(Vector3.up, cameraTransform.up);
                cameraPitchAngle = cameraTransform.forward.y < 0 ? -cameraPitchAngle : cameraPitchAngle;

                Vector3 directionOffset = GetDirectionOffset(cameraPitchAngle);
                Vector3 offsetPosition = cameraTransform.position + directionOffset + positionOffset;

                Gizmos.DrawLine(cameraTransform.position, offsetPosition);
                Gizmos.DrawSphere(offsetPosition, 0.1f);
                cameraHasMoved = false;
            }

            DrawRotationLimitDiscs();
        }

        private void DrawRotationLimitDiscs()
        {
            if (mainCamera == null) return;

            Vector3 cameraPosition = cameraTransform.position;

            Handles.color = new Color(0, 1, 0, 0.25f);
            DrawSolidDisc(cameraPosition, Vector3.right, minCameraLookOffsetAngle, maxCameraLookOffsetAngle, Handles.color);
        }

        private void DrawSolidDisc(Vector3 center, Vector3 axis, float minAngle, float maxAngle, Color color)
        {
            Handles.color = color;
            Handles.DrawSolidArc(center, axis, Vector3.forward, minAngle - maxAngle, 2f);
        }
#endif
    }
}