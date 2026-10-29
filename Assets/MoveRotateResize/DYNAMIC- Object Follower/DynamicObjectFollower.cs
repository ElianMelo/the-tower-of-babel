//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3.1
//------------------------------------------------------------------------------
using UnityEngine;
using MoveRotateResize.Dynamic.SystemsManager;

namespace MoveRotateResize.Dynamic.ObjectFollower
{
    [ExecuteInEditMode]
    public class DynamicObjectFollower : MonoBehaviour
    {
        private GameObject player;
        private GameObject mainCamera;
        private Transform targetTransform;
        private Transform playerTransform;


        [MarkupAttributes.Foldout("General")][SerializeField] bool enableFollowPosition = true;
        [MarkupAttributes.Foldout("General")][SerializeField] bool enableFollowRotation = true;
        [MarkupAttributes.Foldout("General")][SerializeField] bool enableFollowScale = false;
        [MarkupAttributes.Foldout("General")]public Transform target;
        [MarkupAttributes.Foldout("General")][SerializeField] Vector3 offset;


        [MarkupAttributes.Foldout("Player")][SerializeField] bool enableFollowPlayer;


        [MarkupAttributes.Foldout("Camera Look Direction")][SerializeField] bool enableFollowCameraLookDirection = false;
        [MarkupAttributes.Foldout("Camera Look Direction")][SerializeField] Vector3 cameraLookDirectionOffset;
        [MarkupAttributes.Foldout("Camera Look Direction")][SerializeField] float cameraSmoothTime = 0.3f;
        [MarkupAttributes.Foldout("Camera Look Direction")] Vector3 cameraVelocity = Vector3.zero; 


        void Start()
        {           
            CacheGlobalVariables();

            playerTransform = player.transform;
            if (player == null)
            {
                Debug.LogWarning("Player is null in DynamicSystemsManager!");
                return;
            }

            if (enableFollowPlayer)
            {
                if (player != null)
                {
                    playerTransform = player.transform;
                }
                else
                {
                    Debug.LogWarning("Player instance is not set in DynamicSystemsManager.");
                }
            }
            else
            {
                targetTransform = target;
                if (targetTransform == null)
                {
                    Debug.LogWarning($"Target is not assigned. GameObject Name: {gameObject.name}");
                }
            }
        }

        void CacheGlobalVariables()
        {
            player = DynamicSystemsManager.player;
            mainCamera = DynamicSystemsManager.mainCamera;
        }


        private void Update()
        {
            Transform sourceTransform = enableFollowPlayer ? playerTransform : targetTransform;

            if (sourceTransform == null)
                return;

            if (enableFollowPosition)
            {
                if (enableFollowCameraLookDirection && mainCamera != null)
                {
                    ApplyCameraLookDirectionOffset();
                }
                else
                {
                    Vector3 newPosition = sourceTransform.position + offset;
                    transform.position = newPosition;
                }
            }

            if (enableFollowRotation)
            {
                transform.rotation = sourceTransform.rotation;
            }

            if (enableFollowScale)
            {
                transform.localScale = sourceTransform.localScale;
            }
        }
        private void ApplyCameraLookDirectionOffset()
        {
            if (mainCamera != null)
            {
                Vector3 cameraForward = mainCamera.transform.forward;
                Vector3 cameraRight = mainCamera.transform.right;
                Vector3 cameraUp = mainCamera.transform.up;

                Vector3 targetPosition = mainCamera.transform.position
                                        + cameraForward * cameraLookDirectionOffset.z
                                        + cameraRight * cameraLookDirectionOffset.x
                                        + cameraUp * cameraLookDirectionOffset.y;

                transform.position = Vector3.Lerp(transform.position, targetPosition, Time.deltaTime / cameraSmoothTime);
            }
        }

        private void OnDrawGizmos()
        {
            if (enableFollowCameraLookDirection && mainCamera != null)
            {
                Gizmos.color = Color.blue;
                Vector3 cameraForward = mainCamera.transform.forward;
                Vector3 cameraRight = mainCamera.transform.right;
                Vector3 cameraUp = mainCamera.transform.up;

                Vector3 cameraOffsetPosition = mainCamera.transform.position
                                                + cameraForward * cameraLookDirectionOffset.z
                                                + cameraRight * cameraLookDirectionOffset.x
                                                + cameraUp * cameraLookDirectionOffset.y;

                Gizmos.DrawLine(transform.position, cameraOffsetPosition);
                Gizmos.DrawSphere(cameraOffsetPosition, 0.1f);
            }
            else if (enableFollowPlayer && playerTransform != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(transform.position, playerTransform.position + offset);
                Gizmos.DrawSphere(playerTransform.position + offset, 0.1f);
            }
            else if (target != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, target.position + offset);
                Gizmos.DrawSphere(target.position + offset, 0.1f);
            }
        }
    }
}