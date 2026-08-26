using UnityEngine;

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
    [SerializeField] private float gravity = -19.62f;
    [SerializeField] private float groundedGravity = -2f;
    [SerializeField] private int extraJumps = 0;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float groundCheckDistance = 0.15f;
    [Tooltip("Radius offset for the ground check sphere. Slightly smaller than controller.radius prevents false positives on walls.")]
    [SerializeField] private float groundCheckRadiusOffset = 0.05f;

    [Header("Jump Feel")]
    [Tooltip("Time after leaving ground where you can still jump (seconds).")]
    [SerializeField] private float coyoteTime = 0.1f;
    [Tooltip("Time before landing where a jump press is buffered (seconds).")]
    [SerializeField] private float jumpBufferTime = 0.1f;

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

    private CharacterController controller;
    private Vector3 velocity;
    private Vector3 currentHorizontalVelocity;
    private float pitch;
    private bool isCrouching;
    private int jumpsRemaining;
    private float targetHeight;
    private Vector3 originalCameraLocalPos;

    // Ground state
    private bool isGrounded;
    private float coyoteTimeCounter;
    private float jumpBufferCounter;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();

        if (playerCamera != null)
            originalCameraLocalPos = playerCamera.transform.localPosition;

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
        // HandleMouseLook();
        // HandleCrouch();

        // Update ground state BEFORE movement
        UpdateGroundState();

        HandleJumpAndGravity();
        HandleMovement();

        // Apply combined movement in a single Move() call
        Vector3 finalMotion = currentHorizontalVelocity + velocity;
        controller.Move(finalMotion * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Escape))
            ToggleCursorLock();
    }

    private void UpdateGroundState()
    {
        // SphereCast from slightly above the bottom of the controller downward
        Vector3 sphereOrigin = transform.position + Vector3.up * (controller.radius - groundCheckRadiusOffset);
        float checkDistance = groundCheckDistance + groundCheckRadiusOffset;

        isGrounded = Physics.SphereCast(
            sphereOrigin,
            controller.radius - groundCheckRadiusOffset,
            Vector3.down,
            out _,
            checkDistance,
            groundMask,
            QueryTriggerInteraction.Ignore
        );

        // Also check if the controller itself thinks it's grounded as a fallback
        if (!isGrounded && controller.isGrounded)
            isGrounded = true;
    }

    private void HandleMouseLook()
    {
        if (playerCamera == null) return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(Vector3.up * mouseX);

        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        float inputZ = Input.GetAxisRaw("Vertical");

        // Use playerCamera instead of Camera.main for consistency
        Transform camTransform = playerCamera != null ? playerCamera.transform : Camera.main.transform;
        Vector3 inputDirection = (camTransform.right * inputX + camTransform.forward * inputZ);
        inputDirection.y = 0f; // Flatten to XZ plane

        if (inputDirection.sqrMagnitude > 1f)
            inputDirection.Normalize();

        float targetSpeed = walkSpeed;
        if (isCrouching)
            targetSpeed = crouchSpeed;
        else if (Input.GetKey(KeyCode.LeftShift) && inputZ > 0f)
            targetSpeed = sprintSpeed;

        Vector3 targetHorizontalVelocity = inputDirection * targetSpeed;

        // Smooth acceleration; less control while airborne
        float accel = acceleration * (isGrounded ? 1f : airControlMultiplier);

        // BUG FIX: Removed erroneous * targetSpeed multiplication
        currentHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            targetHorizontalVelocity,
            accel * Time.deltaTime
        );
    }

    private void HandleJumpAndGravity()
    {
        // Decrement coyote time when airborne
        if (isGrounded)
            coyoteTimeCounter = coyoteTime;
        else
            coyoteTimeCounter -= Time.deltaTime;

        // Buffer jump input
        if (Input.GetButtonDown("Jump"))
            jumpBufferCounter = jumpBufferTime;
        else
            jumpBufferCounter -= Time.deltaTime;

        // Reset jumps when grounded
        if (isGrounded && velocity.y <= 0f)
        {
            jumpsRemaining = extraJumps;
            velocity.y = groundedGravity;
        }

        // Execute jump if buffered and we have coyote time or extra jumps
        bool canJump = (coyoteTimeCounter > 0f && jumpsRemaining == extraJumps) || (jumpsRemaining > 0);

        if (jumpBufferCounter > 0f && canJump)
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            jumpBufferCounter = 0f; // Consume the buffer

            // Only consume extra jumps if we were actually airborne (coyote time expired)
            if (!isGrounded && coyoteTimeCounter <= 0f && jumpsRemaining > 0)
                jumpsRemaining--;
            else if (isGrounded)
                jumpsRemaining = extraJumps; // Reset on ground jump
        }

        // Apply gravity
        if (!isGrounded)
            velocity.y += gravity * Time.deltaTime;

        // Hard cap falling speed to prevent tunneling
        if (velocity.y < -50f)
            velocity.y = -50f;
    }

    private void HandleCrouch()
    {
        if (!allowCrouch) return;

        if (Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C))
            isCrouching = !isCrouching;

        targetHeight = isCrouching ? crouchingHeight : standingHeight;

        // Prevent standing up if something is above
        if (!isCrouching)
        {
            Vector3 sphereOrigin = transform.position + Vector3.up * controller.height;
            float checkDist = standingHeight - controller.height + 0.1f;
            if (Physics.SphereCast(sphereOrigin, controller.radius, Vector3.up, out _, checkDist, groundMask))
            {
                targetHeight = crouchingHeight;
                isCrouching = true;
            }
        }

        float previousHeight = controller.height;
        controller.height = Mathf.Lerp(controller.height, targetHeight, crouchTransitionSpeed * Time.deltaTime);

        // Keep feet anchored to ground
        float heightDelta = controller.height - previousHeight;
        transform.position += new Vector3(0f, heightDelta * 0.5f, 0f);

        // Adjust camera height proportionally
        if (playerCamera != null)
        {
            Vector3 camPos = originalCameraLocalPos;
            camPos.y *= controller.height / standingHeight;
            playerCamera.transform.localPosition = Vector3.Lerp(
                playerCamera.transform.localPosition,
                camPos,
                crouchTransitionSpeed * Time.deltaTime
            );
        }
    }

    private void ToggleCursorLock()
    {
        bool isLocked = Cursor.lockState == CursorLockMode.Locked;
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isLocked;
    }

    // Optional: visualize ground check in Scene view
    private void OnDrawGizmosSelected()
    {
        if (controller == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * (controller.radius - groundCheckRadiusOffset);
        Gizmos.DrawLine(origin, origin + Vector3.down * (groundCheckDistance + groundCheckRadiusOffset));
        Gizmos.DrawWireSphere(origin + Vector3.down * (groundCheckDistance + groundCheckRadiusOffset), controller.radius - groundCheckRadiusOffset);
    }
}