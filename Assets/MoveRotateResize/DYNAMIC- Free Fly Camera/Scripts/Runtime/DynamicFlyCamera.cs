//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using UnityEngine;

namespace MoveRotateResize.Dynamic.FlyCamera
{
    public class DynamicFlyCamera : MonoBehaviour
    {
        public float mouseSensitivity = 10.0f;
        public float moveSpeed = 5.0f;
        public float zoomSpeed = 5.0f;
        public float boostMultiplier = 2.0f;
        public KeyCode resetKey = KeyCode.R;
        public KeyCode boostKey = KeyCode.LeftShift;
        public KeyCode moveUpKey = KeyCode.Q;
        public KeyCode moveDownKey = KeyCode.E;


        private Camera mainCamera;
        private bool isResetting = false;
        private float initialFOV;


        private Vector3 initialPosition;
        private Quaternion initialRotation;


        private float pitch = 0.0f;
        private float yaw = 0.0f;


        void Awake()
        {
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

        void Start()
        {
            isResetting = true;

            mainCamera = GetComponentInChildren<Camera>();
            if (mainCamera == null)
            {
                Debug.LogError("No Camera component found in child GameObject.");
                enabled = false;
                return;
            }

            initialPosition = transform.position;
            initialRotation = transform.rotation;
            initialFOV = mainCamera.fieldOfView; 

            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;
            initialFOV = mainCamera.fieldOfView;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            isResetting = false;

            mainCamera.farClipPlane = 10000;
        }

        void FixedUpdate()
        {
            if(Input.GetKey(resetKey) || Input.GetKeyDown(KeyCode.JoystickButton9) && !isResetting)
            {
                ResetCamera();
            }

            if (!isResetting)
            {
                HandleCameraRotation();
                HandleCameraMovement();
                //HandleCameraZoom();
            }
        }

        private void HandleCameraRotation()
        {
            float rotateHorizontal = Input.GetAxis("Right Stick X");
            float rotateVertical = -Input.GetAxis("Right Stick Y");  

            rotateHorizontal += Input.GetAxis("Mouse X");
            rotateVertical += Input.GetAxis("Mouse Y");

            yaw += rotateHorizontal * mouseSensitivity * Time.deltaTime;
            pitch -= rotateVertical * mouseSensitivity * Time.deltaTime; 
            pitch = Mathf.Clamp(pitch, -90f, 90f);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0);
        }

        private void HandleCameraMovement()
        {
            float horizontalMovement = Input.GetAxis("Horizontal");
            float verticalMovement = Input.GetAxis("Vertical");
            Vector3 moveDirection = (transform.right * horizontalMovement + transform.forward * verticalMovement).normalized;
            if (moveDirection.magnitude > 1f)
            {
                moveDirection.Normalize();
            }
            float currentMoveSpeed = Input.GetKey(boostKey) || Input.GetKey(KeyCode.JoystickButton8) ? moveSpeed * boostMultiplier : moveSpeed;
            transform.position += moveDirection * currentMoveSpeed * Time.deltaTime;

            if (Input.GetKey(moveUpKey) || Input.GetKey("joystick button 5"))
            {
                transform.position += Vector3.up * currentMoveSpeed * Time.deltaTime;
            }

            if (Input.GetKey(moveDownKey) || Input.GetKey("joystick button 4")) 
            {
                transform.position -= Vector3.up * currentMoveSpeed * Time.deltaTime;
            }
        }

        private void HandleCameraZoom()
        {
            float scrollWheelInput = Input.GetAxis("Mouse ScrollWheel");
            float newFOV = mainCamera.fieldOfView - (scrollWheelInput * zoomSpeed);
            mainCamera.fieldOfView = Mathf.Clamp(newFOV, 1.0f, 60.0f);
        }

        private void ResetCamera()
        {
            if (isResetting) return; 

            isResetting = true;
            Debug.Log($"Resetting camera. Initial Position: {initialPosition}, Initial Rotation: {initialRotation.eulerAngles}, Initial FOV: {initialFOV}");

            mainCamera.fieldOfView = initialFOV;

            transform.position = initialPosition;
            transform.rotation = initialRotation;

            Vector3 angles = initialRotation.eulerAngles;
            yaw = angles.y;
            pitch = angles.x;

            Debug.Log($"Camera reset complete. Position: {transform.position}, Rotation: {transform.rotation.eulerAngles}, FOV: {mainCamera.fieldOfView}"); 

            isResetting = false;
        }
    }
}