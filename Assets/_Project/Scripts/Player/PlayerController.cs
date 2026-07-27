using UnityEngine;

/// <summary>
/// First-person player controller built on Unity's CharacterController component.
/// Handles WASD movement, mouse look, jumping, sprinting, crouching and gravity.
/// Compatible with Unity 6.3 LTS (uses the new Input System is optional — this
/// version uses the legacy Input Manager for simplicity and portability).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The camera used for looking around. If left empty, will try to find a child camera.")]
    [SerializeField] private Camera playerCamera;

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float crouchSpeed = 2.5f;
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float airControlMultiplier = 0.5f;

    [Header("Jumping & Gravity")]
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -19.62f; // ~2x Unity default for snappier FPS feel
    [SerializeField] private float groundedGravity = -2f; // small downward force to keep grounded check stable
    [SerializeField] private int extraJumps = 0; // set >0 for double-jump style movement

    [Header("Crouching")]
    [SerializeField] private bool allowCrouch = true;
    [SerializeField] private float standingHeight = 2f;
    [SerializeField] private float crouchingHeight = 1f;
    [SerializeField] private float crouchTransitionSpeed = 10f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minPitch = -85f;
    [SerializeField] private float maxPitch = 85f;
    [SerializeField] private bool lockCursor = true;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.2f;

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 currentHorizontalVelocity;
    private float pitch;
    private bool isCrouching;
    private int jumpsRemaining;
    private float targetHeight;
    private Vector3 originalCameraLocalPos;

    private bool IsGrounded => controller.isGrounded;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
        {
            originalCameraLocalPos = playerCamera.transform.localPosition;
        }

        targetHeight = standingHeight;
        controller.height = standingHeight;
        jumpsRemaining = extraJumps;
    }

    private void Start()
    {
        if (lockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void Update()
    {
        HandleMouseLook();
        // HandleCrouch();
        HandleMovement();
        HandleJumpAndGravity();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursorLock();
        }
    }

    private void HandleMouseLook()
    {
        return;
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        // Yaw rotates the whole body (left/right)
        transform.Rotate(Vector3.up * mouseX);

        // Pitch rotates only the camera (up/down), clamped to avoid flipping
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        Vector3 inputDirection = (Camera.main.transform.right * inputX + Camera.main.transform.forward * inputZ);
        if (inputDirection.sqrMagnitude > 1f)
        {
            inputDirection.Normalize();
        }

        float targetSpeed = walkSpeed;
        if (isCrouching)
        {
            targetSpeed = crouchSpeed;
        }
        else if (Input.GetKey(KeyCode.LeftShift) && inputZ > 0f)
        {
            targetSpeed = sprintSpeed;
        }

        Vector3 targetHorizontalVelocity = inputDirection * targetSpeed;

        // Smooth acceleration; reduce control authority while airborne
        float accel = acceleration * (IsGrounded ? 1f : airControlMultiplier);
        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            accel * Time.deltaTime * targetSpeed
        );

        controller.Move(currentHorizontalVelocity * Time.deltaTime);
    }

    private void HandleJumpAndGravity()
    {
        if (IsGrounded)
        {
            jumpsRemaining = extraJumps;

            // Keep a small downward force so isGrounded stays reliable on slopes/steps
            if (velocity.y < 0f)
            {
                velocity.y = groundedGravity;
            }

            if (Input.GetButtonDown("Jump"))
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            if (Input.GetButtonDown("Jump") && jumpsRemaining > 0)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
                jumpsRemaining--;
            }

            velocity.y += gravity * Time.deltaTime;
        }

        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        if (!allowCrouch)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
        {
            isCrouching = !isCrouching;
        }

        targetHeight = isCrouching ? crouchingHeight : standingHeight;

        // Prevent standing up if something is above the player's head
        if (!isCrouching && Physics.SphereCast(
                transform.position, controller.radius, Vector3.up,
                out _, standingHeight - controller.height + 0.1f, groundMask))
        {
            targetHeight = crouchingHeight;
            isCrouching = true;
        }

        float previousHeight = controller.height;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        // Keep the controller's base anchored to the ground while height changes
        float heightDelta = controller.height - previousHeight;
        //controller.center = new Vector3(0f, controller.height / 2f, 0f);
        transform.position += new Vector3(0f, heightDelta / 2f, 0f);

        //if (playerCamera != null)
        //{
        //    Vector3 camPos = originalCameraLocalPos;
        //    camPos.y *= controller.height / standingHeight;
        //    playerCamera.transform.localPosition = Vector3.Lerp(
        //        playerCamera.transform.localPosition, camPos, crouchTransitionSpeed * Time.deltaTime);
        //}
    }

    private void ToggleCursorLock()
    {
        bool isLocked = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isLocked;
    }
}