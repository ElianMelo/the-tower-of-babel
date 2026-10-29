//------------------------------------------------------------------------------
//     This code is the copyright of MoveRotateResize
//     Created by Gregory Wells-Bruneau
//     Version: 1.3
//------------------------------------------------------------------------------
using System.Collections;
using UnityEngine;
using NaughtyAttributes;
// using Unity.Entities;
// using Unity.Physics;
// using Unity.Physics.Extensions;
// using Unity.Jobs;
// using Unity.Transforms;
// using Unity.Collections;
// using Unity.Mathematics; 
// using Unity.Burst;

namespace MoveRotateResize.DynamicControllers
{
    public class DynamicControllerFirstPerson : MonoBehaviour
    {
        [Header("Game Objects")]
        public GameObject cameraOffsets; 
        public Camera playerCamera;
    
        [Header("Movement Settings")]
        public float walkSpeed = 5f;
        public float rotationSpeed = 2f;
        private float currentSpeed;
        private float horizontal;
        private float vertical;
        
        [Header("Jump Settings")]
        public KeyCode jumpKey = KeyCode.Space;
        public float jumpForce = 5f;

        [Header("Sprinting Settings")]
        public bool enableSprinting = true;
        public KeyCode sprintKey = KeyCode.LeftShift;
        public float sprintSpeedModifier = 1.5f;
        public float sprintCrouchSpeedMultiplier = 0.5f;
        public float sprintBobbingAmountXMultiplier = 2f;
        public float sprintBobbingAmountYMultiplier = 2f;
        public float sprintBobbingAmountZMultiplier = 2f;
        public float sprintBobbingRotationAmountMultiplier = 2f;
        public float sprintBobbingSpeedMultiplier = 1.5f; 
        private bool isSprinting = false;
    
        [Header("Crouch Settings")]
        public bool enableCrouching = true;
        [SerializeField] private LayerMask layerMask; 
        public bool holdToCrouch = false;
        public KeyCode crouchKey = KeyCode.C; 
        public float crouchHeight = 0.5f; 
        public float crouchSpeedModifier = 2f; 
        public float crouchBobbingAmountXMultiplier = 2f;
        public float crouchBobbingAmountYMultiplier = 2f;
        public float crouchBobbingAmountZMultiplier = 2f;
        public float crouchBobbingRotationAmountMultiplier = 2f;
        public float crouchBobbingSpeedMultiplier = 1.5f; 
        [HideInInspector] public bool isCrouched = false;


        [Header("Camera Settings")]
        public bool smoothCameraRotation = true;
        public float cameraRotationSmoothness = 5f;
        private Vector3 cameraOffsetsPositionStart;
        private Quaternion cameraOffsetsRotationStart;

        [Header("Rotation Limits")]
        public float minVerticalAngle = -90f;
        public float maxVerticalAngle = 90f;
        public float maxPitch = 90f;
        public float minYaw = -90f;
        public float maxYaw = 90f;
        private float currentPitch = 0f;
        private float currentYaw = 0f;

        [Header("Head Bobbing Settings")]
        public bool enableHeadBobbing = true;
        public float bobbingSpeed = 0.1f;
        public float transitionSpeed = 0.5f;
        public float bobbingOffsetSpeed = 1f;
        public float bobbingOffsetX = 0.1f;
        public float bobbingOffsetY = 0.1f;
        public float bobbingOffsetZ = 0.1f;
        public float bobbingAmountZ = 0.1f; 
        public float bobbingAmountX = 0.1f; 
        public float bobbingAmountY = 0.1f;
        public float bobbingRotationAmount = 1f;

        [Header("Breathing Effect Settings")]
        public bool enableBreathingEffect = true;
        public float breathingSpeed = 0.5f;
        public float breathingAmount = 0.1f;
        public float breathingSwayAmount = 0.05f;
        public float breathingRotationAmount = 1f;
        public float breathingSwaySpeed = 1f;
        private float currentSway; 
        private float targetSway; 

        // private NativeQueue<CollisionEvent> collisionEvents;
        // private EntityManager entityManager;
        // public float mass = 1f; // Mass of the player
        // public float pushForce = 10f; // Base push force

        //Misc Variables
        private float midpoint;
        private float timer = 0.0f;
        private float bobbingOffset;
        private bool isGrounded = true;
        private Rigidbody rb;
        private bool isMoving = false;
        private float breathingTimer = 0.0f;
        // Game Loaded
        private bool gameLoaded = false;
        private Transform thePlayer;
        Vector3 thePosition;

        //Tests if the player is on the ground
        public bool IsGrounded
        {
            get { return isGrounded; }
        }

        private void Start()
        {
            //collisionEvents = new NativeQueue<CollisionEvent>(Allocator.Persistent);
            //entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;

            if (cameraOffsets == null)
            {
                Debug.LogWarning("Camera Offsets object is not assigned. Please assign it in the inspector.");
                return;
            }
            
            gameLoaded = false;
                    
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            currentSpeed = walkSpeed;

            cameraOffsetsPositionStart = cameraOffsets.transform.localPosition;
            cameraOffsetsRotationStart = cameraOffsets.transform.localRotation;
            playerCamera.farClipPlane = 5000;

            // Store Camera Starting Position
            //cameraOffsets.transform.localPosition = cameraOffsetsPositionStart;
            //cameraOffsets.transform.localRotation = cameraOffsetsRotationStart;
                    
            currentPitch = playerCamera.transform.localEulerAngles.x;
            currentYaw = transform.localEulerAngles.y;

            midpoint = playerCamera.transform.localPosition.y;
            bobbingOffset = UnityEngine.Random.Range(0f, 1f);
            rb = GetComponent<Rigidbody>();
            rb.freezeRotation = true; 

            StartCoroutine(WaitForPhysicsToSettle());
        }
        
        private void Update()
        {
            // Calculate the bottom position of the capsule collider.
            //Vector3 bottomPosition = transform.position - Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius);

            // Now 'bottomPosition' holds the position of the bottom of the capsule collider.
            // You can use this position for whatever purposes you need in your script or shaders.

            // Interactable Grass Shader
            //Shader.SetGlobalVector("_Player", transform.position - Vector3.up * (capsuleCollider.height * 0.5f - capsuleCollider.radius)); 
            //Shader.SetGlobalVector("_Player", transform.position); 

            // Check and execute collision events
            // if (collisionEvents.Count > 0)
            // {
            //     var job = new ApplyCollisionJob
            //     {
            //         CollisionEvents = collisionEvents,
            //         EntityManager = entityManager
            //     };

            //     job.Schedule().Complete();
            // }

            Shader.SetGlobalVector("_Player", thePosition); 
        
            // Jump
            if (Input.GetKeyDown(jumpKey) && isGrounded)
            {
            Jump();
            }
                
            if (gameLoaded)
            {
                if (enableCrouching)
                {
                    if(Input.GetKeyDown(crouchKey) && !holdToCrouch)
                    {
                        Crouch();
                    }
                    
                    if(Input.GetKeyDown(crouchKey) && holdToCrouch)
                    {
                        isCrouched = false;
                        Crouch();
                    }
                    else if(Input.GetKeyUp(crouchKey) && holdToCrouch)
                    {
                        isCrouched = true;
                        Crouch();
                    }
                }

                if (enableSprinting)
                {
                    if(Input.GetKey(sprintKey) || Input.GetKey(KeyCode.JoystickButton8))
                    {
                        isSprinting = true;
                    }
                    else
                    {
                        isSprinting = false;
                    }
                }

                isGrounded = Physics.Raycast(transform.position, Vector3.down, 0.5f);

                Quaternion rotation = Quaternion.Euler(0f, currentYaw, 0f);
                rb.MoveRotation(rotation);

                Quaternion cameraRotation = Quaternion.Euler(currentPitch, 0f, 0f);
                playerCamera.transform.localRotation = cameraRotation;

                horizontal = Input.GetAxis("Horizontal");
                vertical = Input.GetAxis("Vertical"); 

            
                isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;

                float mouseX = Input.GetAxis("Right Stick X") * rotationSpeed;
                float mouseY = -Input.GetAxis("Right Stick Y") * rotationSpeed;  

                mouseX += Input.GetAxis("Mouse X") * rotationSpeed;
                mouseY += Input.GetAxis("Mouse Y") * rotationSpeed;

                currentYaw += mouseX;
                currentPitch -= mouseY;
                currentPitch = Mathf.Clamp(currentPitch, minVerticalAngle, maxVerticalAngle);

                Quaternion targetRotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
                playerCamera.transform.localRotation = Quaternion.Lerp(playerCamera.transform.localRotation, targetRotation, cameraRotationSmoothness * Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (enableHeadBobbing && isMoving)
            {
                HeadBobbingOn();
            }
            else
            {
                HeadBobbingOff();
            }

            if (enableBreathingEffect && !isMoving)
            {
                Breathing();
            }
            else
            {
                ResetBreathing();
            }    

            Vector3 movement = (transform.right * horizontal + transform.forward * vertical) * currentSpeed * Time.fixedDeltaTime;
            rb.MovePosition(rb.position + movement);

            
            Speed();
        }

        private IEnumerator WaitForPhysicsToSettle()
        {
            yield return null;

            yield return null;
            yield return null;
            yield return null;

            gameLoaded = true;
        }

        private void Speed()
        {
            // Speed state checks
            if (isSprinting && isCrouched)
            {
                currentSpeed = (walkSpeed / crouchSpeedModifier) * sprintCrouchSpeedMultiplier;
            }
            else if (isSprinting)
            {
                currentSpeed = walkSpeed * sprintSpeedModifier;
            }
            else if (isCrouched)
            {
                currentSpeed = walkSpeed / crouchSpeedModifier;
            }
            else
            {
                currentSpeed = walkSpeed;
            }

            isMoving = Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f;
        }


        private void HeadBobbingOn()
        {
            float floatBobbingAmountX = Mathf.Sin(timer) * bobbingAmountX;
            float floatBobbingAmountY = Mathf.Sin(timer) * bobbingAmountY;
            float floatBobbingAmountZ = Mathf.Sin(timer) * bobbingAmountZ;

            float currentBobbingRotationAmount = bobbingRotationAmount; 

            float currentBobbingSpeed = bobbingSpeed; 

            if (isSprinting && isCrouched)
            {
                floatBobbingAmountX *= crouchBobbingAmountXMultiplier;
                floatBobbingAmountY *= crouchBobbingAmountYMultiplier;
                floatBobbingAmountZ *= crouchBobbingAmountZMultiplier;
                currentBobbingRotationAmount *= crouchBobbingRotationAmountMultiplier;

                currentBobbingSpeed *= crouchBobbingSpeedMultiplier;
            }
            else if (isCrouched)
            {
                floatBobbingAmountX *= crouchBobbingAmountXMultiplier;
                floatBobbingAmountY *= crouchBobbingAmountYMultiplier;
                floatBobbingAmountZ *= crouchBobbingAmountZMultiplier;
                currentBobbingRotationAmount *= crouchBobbingRotationAmountMultiplier;

                currentBobbingSpeed *= crouchBobbingSpeedMultiplier;
            }

            else if (isSprinting)
            {
                floatBobbingAmountX *= sprintBobbingAmountXMultiplier;
                floatBobbingAmountY *= sprintBobbingAmountYMultiplier;
                floatBobbingAmountZ *= sprintBobbingAmountZMultiplier;
                currentBobbingRotationAmount *= sprintBobbingRotationAmountMultiplier;

                currentBobbingSpeed *= sprintBobbingSpeedMultiplier;
            }
            

            timer += currentBobbingSpeed;

            Vector3 bobbingPosition = cameraOffsets.transform.localPosition;
            bobbingPosition.x = cameraOffsetsPositionStart.x + Mathf.Sin(timer * bobbingOffsetSpeed + bobbingOffsetX) * floatBobbingAmountX;
            bobbingPosition.y = cameraOffsetsPositionStart.y + midpoint + Mathf.Sin(timer * bobbingOffsetSpeed + bobbingOffsetY) * floatBobbingAmountY + bobbingAmountY; 
            bobbingPosition.z = cameraOffsetsPositionStart.z + Mathf.Sin(timer * bobbingOffsetSpeed + bobbingOffsetZ) * floatBobbingAmountZ;

            cameraOffsets.transform.localPosition = bobbingPosition;

            Quaternion bobbingRotation = Quaternion.Euler(
                Mathf.Sin(timer * bobbingOffsetSpeed) * currentBobbingRotationAmount,
                Mathf.Sin(timer * bobbingOffsetSpeed * 2f) * currentBobbingRotationAmount,
                0f);
            cameraOffsets.transform.localRotation = cameraOffsetsRotationStart * bobbingRotation;
        }

        private void HeadBobbingOff()
        {
            if (cameraOffsets.transform.localPosition != cameraOffsetsPositionStart)
            {
                Vector3 targetPosition = cameraOffsetsPositionStart;
                cameraOffsets.transform.localPosition = Vector3.Lerp(cameraOffsets.transform.localPosition, targetPosition, Time.deltaTime * transitionSpeed);
            }
            else
            {
                timer = 0.0f;
                Vector3 bobbingPosition = cameraOffsets.transform.localPosition;
                bobbingPosition.y = cameraOffsetsPositionStart.y + midpoint;

                cameraOffsets.transform.localPosition = cameraOffsetsPositionStart;
            }
            isMoving = false;
        }

        private void Jump()
        {
            rb.AddForce(Vector3.up * jumpForce, UnityEngine.ForceMode.Impulse);
            isGrounded = false;
        }

        private void Crouch()
        {
            if (isCrouched)
            {
                // Check if there's space above the player to stand up
                RaycastHit hit;
                Vector3 startPosition = transform.position + Vector3.up * crouchHeight; // Start the raycast from the crouched height
                float standHeight = crouchHeight * 2; // The height the player needs to stand up

                // Draw the ray in the scene view for debugging
                Debug.DrawRay(startPosition, Vector3.up * standHeight, Color.red, 1.0f);

                // Use the layerMask to filter the layers the raycast checks
                if (Physics.Raycast(startPosition, Vector3.up, out hit, standHeight, layerMask))
                {
                    // Something is blocking the player from standing up, do not uncrouch
                    return;
                }

                // There's space to stand up, uncrouch
                cameraOffsetsPositionStart.y += crouchHeight;
                isCrouched = false;
            }
            else
            {
                // Crouch down
                cameraOffsetsPositionStart.y -= crouchHeight;
                isCrouched = true;
            }
        }

        private void Breathing()
        {
            breathingTimer += breathingSpeed * Time.deltaTime;

            float breathingScale = Mathf.Sin(breathingTimer) * breathingAmount;

            currentSway = Mathf.Lerp(currentSway, targetSway, Time.deltaTime * breathingSwaySpeed);

            if (Mathf.Abs(currentSway - targetSway) < 0.01f)
            {
                targetSway = UnityEngine.Random.Range(-1f, 1f);
            }

            Quaternion swayRotation = Quaternion.Euler(0f, 0f, currentSway * breathingRotationAmount);
            cameraOffsets.transform.localRotation = cameraOffsetsRotationStart * swayRotation;

            Vector3 cameraPosition = playerCamera.transform.localPosition;
            cameraPosition.y = midpoint + breathingScale;
            playerCamera.transform.localPosition = cameraPosition;
        }

        private void ResetBreathing()
        {
            Vector3 cameraPosition = playerCamera.transform.localPosition;
            cameraPosition.y = midpoint;
            playerCamera.transform.localPosition = cameraPosition;
            breathingTimer = 0f;
        }

        public Vector3 GetVelocity()
        {
            return rb.velocity;
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle > 180f)
                angle -= 360f;
            else if (angle < -180f)
                angle += 360f;

            return Mathf.Clamp(angle, min, max);
        }


    // private void OnCollisionEnter(Collision collision)
    //     {
    //         var entity = GetEntityFromGameObject(collision.gameObject);
    //         if (entity != Entity.Null)
    //         {
    //             var direction = collision.contacts[0].normal;
    //             collisionEvents.Enqueue(new CollisionEvent
    //             {
    //                 Entity = entity,
    //                 Direction = (float3)direction,
    //                 PlayerMass = mass,
    //                 PushForce = pushForce
    //             });
    //         }
    //     }

    //     private Entity GetEntityFromGameObject(GameObject gameObject)
    //     {
    //         // Assuming the GameObject has an associated entity via a companion component or similar setup.
    //         var entity = entityManager.CreateEntityQuery(typeof(LocalToWorld))
    //                                    .ToEntityArray(Allocator.TempJob)[0];
    //         return entity;
    //     }

    //         // old that works
    //     //[BurstCompile]
    //     private struct ApplyCollisionJob : IJob
    //     {
    //         public NativeQueue<CollisionEvent> CollisionEvents;
    //         public EntityManager EntityManager;

    //         public void Execute()
    //         {
    //             while (CollisionEvents.TryDequeue(out var collisionEvent))
    //             {
    //                 var physicsVelocity = EntityManager.GetComponentData<PhysicsVelocity>(collisionEvent.Entity);
    //                 var physicsMass = EntityManager.GetComponentData<PhysicsMass>(collisionEvent.Entity);
    //                 float entityMass = 1f / physicsMass.InverseMass;

    //                 float totalMass = collisionEvent.PlayerMass + entityMass;
    //                 float appliedForce = collisionEvent.PushForce * (collisionEvent.PlayerMass / totalMass);

    //                 physicsVelocity.Linear += collisionEvent.Direction * appliedForce;
    //                 EntityManager.SetComponentData(collisionEvent.Entity, physicsVelocity);
    //             }
    //         }
    //     }
    //     private void OnDestroy()
    //     {
    //         collisionEvents.Dispose();
    //     }
    }
}