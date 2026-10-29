//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MoveRotateResize.General
{
    public class CameraLookAt : MonoBehaviour
    {
        [Header("References")]
        public Transform player;

        [Header("Rotation Settings")]
        public float lookSpeed = 5f;

        [Header("Rotation Limits")]
        public float minXRotation = -45f;
        public float maxXRotation = 45f;
        public float minYRotation = -45f;
        public float maxYRotation = 45f;
        public float minZRotation = -10f;
        public float maxZRotation = 10f;

        [Header("Debug")]
        public bool showDebug = false;
        public float debugDiscRadius = 2f;

        private void Start()
        {
            SnapToPlayer();
        }

        private void LateUpdate()
        {
            if (player == null) return;

            RotateTowardsPlayer();
        }

        private void RotateTowardsPlayer()
        {
            Vector3 direction = player.position - transform.position;
            if (Vector3.Angle(transform.forward, direction) <= 0.1f) return;

            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, lookSpeed * Time.deltaTime);

            ClampRotation();
        }

        private void SnapToPlayer()
        {
            if (player == null) return;

            Vector3 direction = player.position - transform.position;
            transform.rotation = Quaternion.LookRotation(direction);
        }

        private void ClampRotation()
        {
            Vector3 eulerRotation = transform.rotation.eulerAngles;

            eulerRotation.x = ClampAngle(eulerRotation.x, minXRotation, maxXRotation);
            eulerRotation.y = ClampAngle(eulerRotation.y, minYRotation, maxYRotation);
            eulerRotation.z = ClampAngle(eulerRotation.z, minZRotation, maxZRotation);

            transform.rotation = Quaternion.Euler(eulerRotation);
        }

        private float ClampAngle(float angle, float min, float max)
        {
            angle = angle > 180f ? angle - 360f : angle;
            return Mathf.Clamp(angle, min, max);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!showDebug || player == null) return;

            DrawRotationLimitDiscs();
        }

        private void DrawRotationLimitDiscs()
        {
            Vector3 position = transform.position;

            DrawSolidDisc(position, Vector3.right, minXRotation, maxXRotation, new Color(1, 0, 0, 0.25f));
            DrawSolidDisc(position, Vector3.up, minYRotation, maxYRotation, new Color(0, 1, 0, 0.25f));
            DrawSolidDisc(position, Vector3.forward, minZRotation, maxZRotation, new Color(0, 0, 1, 0.25f));
        }

        private void DrawSolidDisc(Vector3 center, Vector3 axis, float minAngle, float maxAngle, Color color)
        {
            Handles.color = color;
            Handles.DrawSolidArc(center, axis, Quaternion.AngleAxis(minAngle, axis) * Vector3.forward, maxAngle - minAngle, debugDiscRadius);
        }
#endif
    }
}