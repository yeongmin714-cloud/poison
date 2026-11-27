using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(CharacterController))]
    public class SimpleCharacterController : MonoBehaviour
    {
        [Header("Cinemachine Setup")]
        [Tooltip("Main Camera from the scene")]
        public Transform mainCameraTransform;
        [Tooltip("Empty GameObject inside the player at head height")]
        public GameObject cinemachineCameraTarget;
        
        [Tooltip("Mouse sensitivity")]
        public float mouseSensitivity = 2.0f;
        public float topClamp = 70.0f;
        public float bottomClamp = -30.0f;

        [Header("Movement Settings")]
        public float walkSpeed = 5.0f;
        public float runSpeedMultiplier = 1.5f; 
        public float jumpHeight = 2.0f;
        [Tooltip("Rotation smoothing time (smaller value means faster rotation)")]
        public float turnSmoothTime = 0.1f; 
        [Tooltip("How fast the character accelerates and decelerates (Momentum)")]
        public float speedChangeRate = 10.0f;

        [Header("Physics Settings")]
        public float gravity = -9.81f;
        public float gravityMultiplier = 2.0f; 

        [Header("Ground Detection")]
        public Transform groundCheck; 
        public float groundDistance = 0.4f;
        public LayerMask groundMask;

        [Header("Interaction Settings")]
        public float pushPower = 2.0f;

        [Header("Slow-Mo Sync")]
        [Tooltip("Jump animation transition duration in seconds. Decouples visual lag from Slow-Mo.")]
        public float jumpTransitionTime = 0.05f;

        [HideInInspector] 
        public Vector3 velocity;

        // --- Internal Variables ---
        private CharacterController controller;
        private bool isGrounded;
        private float currentAnimSpeed = 0f; 
        
        private float turnSmoothVelocity;
        private float _currentSpeed;
        private float _targetRotation = 0.0f;

        // Camera Variables
        private float _cinemachineTargetYaw;
        private float _cinemachineTargetPitch;

        // External System References
        private ITimeWarpCharacterAdapter _timeAdapter;
        private Animator _animator; 

        void Start()
        {
            controller = GetComponent<CharacterController>();
            _timeAdapter = GetComponent<ITimeWarpCharacterAdapter>();
            _animator = GetComponentInChildren<Animator>();

            // CRITICAL FOR SLOW-MO: Allows Unity to register microscopic movements!
            controller.minMoveDistance = 0f;

            // Lock and hide the mouse cursor in the center of the screen
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Initialize starting camera rotation (Gimbal)
            if (cinemachineCameraTarget != null)
            {
                _cinemachineTargetYaw = cinemachineCameraTarget.transform.rotation.eulerAngles.y;
            }
        }

        void Update()
        {
            // 1. TIME MANAGEMENT (Maintained for local Slow-Mo integration)
            float dt = _timeAdapter != null ? _timeAdapter.GetLocalDeltaTime() : Time.deltaTime;

            // --- ANIMATOR SYNCHRONIZATION ---
            // Calculate local time scale and pass it to the Animator
            float localScale = Time.deltaTime > 0f ? (dt / Time.deltaTime) : 1f;
            if (_animator != null)
            {
                _animator.speed = localScale;
            }

            // 2. GROUND DETECTION
            if (groundCheck != null)
            {
                isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask, QueryTriggerInteraction.Ignore);
            }
            else
            {
                isGrounded = controller.isGrounded;
            }

            if (_animator != null) _animator.SetBool("IsGrounded", isGrounded);

            // Stick to the ground
            if (isGrounded && velocity.y < 0)
            {
                velocity.y = -2f; 
            }

            // Read input
            float x = Input.GetAxisRaw("Horizontal");
            float z = Input.GetAxisRaw("Vertical");
            Vector3 inputDir = new Vector3(x, 0f, z).normalized;
            float inputMag = inputDir.magnitude;

            bool isRunning = Input.GetKey(KeyCode.LeftShift);

            // --- 3. ACCELERATION AND MOMENTUM (SLOW-MO ADJUSTED) ---
            float targetSpeed = isRunning ? (walkSpeed * runSpeedMultiplier) : walkSpeed;
            if (inputMag < 0.1f) targetSpeed = 0.0f;

            // Use custom speed variable instead of Unity's default velocity which behaves poorly in Slow-Mo
            if (Mathf.Abs(_currentSpeed - (targetSpeed * inputMag)) > 0.1f)
            {
                _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed * inputMag, dt * speedChangeRate);
            }
            else
            {
                _currentSpeed = targetSpeed * inputMag;
            }

            // --- 4. CHARACTER ROTATION ---
            float targetAnimSpeed = 0f;

            if (inputMag >= 0.1f && mainCameraTransform != null)
            {
                // Calculate angle based on camera and input direction
                _targetRotation = Mathf.Atan2(inputDir.x, inputDir.z) * Mathf.Rad2Deg + mainCameraTransform.eulerAngles.y;

                // Smooth character rotation (Mathematical spring)
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref turnSmoothVelocity, turnSmoothTime, Mathf.Infinity, dt);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);

                targetAnimSpeed = isRunning ? 1.0f : 0.5f;
            }

            // Smooth the Speed parameter for the Animator
            currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetAnimSpeed, dt * speedChangeRate);
            if (_animator != null) _animator.SetFloat("Speed", currentAnimSpeed);

            // --- 5. JUMP AND GRAVITY ---
            if (Input.GetButtonDown("Jump") && isGrounded)
            {
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * (gravity * gravityMultiplier));
                
                // --- LAG-FREE JUMP ---
                if (_animator != null) 
                {
                    // Using CrossFadeInFixedTime instead of SetTrigger. 
                    // This ignores scaled time and forces transition in a fixed duration (e.g. 0.05 seconds),
                    // ensuring the visual lift-off syncs perfectly with physics!
                    _animator.CrossFadeInFixedTime("Jump", jumpTransitionTime);
                }
            }

            velocity.y += (gravity * gravityMultiplier) * dt;

            // --- 6. MOVEMENT EXECUTION ---
            Vector3 moveDirection = Quaternion.Euler(0.0f, _targetRotation, 0.0f) * Vector3.forward;
            controller.Move((moveDirection.normalized * _currentSpeed + Vector3.up * velocity.y) * dt);
        }

        // --- 7. CAMERA HANDLING (GIMBAL) ---
        private void LateUpdate()
        {
            CameraRotation();
        }

        private void CameraRotation()
        {
            if (cinemachineCameraTarget == null) return;

            // Read mouse input (camera always operates in normal time, regardless of Time Stop)
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            _cinemachineTargetYaw += mouseX;
            _cinemachineTargetPitch -= mouseY;

            // Clamp rotation to prevent neck breaking
            _cinemachineTargetPitch = ClampAngle(_cinemachineTargetPitch, bottomClamp, topClamp);

            // Rotate Gimbal above player's head
            cinemachineCameraTarget.transform.rotation = Quaternion.Euler(_cinemachineTargetPitch, _cinemachineTargetYaw, 0.0f);
        }

        private float ClampAngle(float angle, float min, float max)
        {
            if (angle < -360f) angle += 360f;
            if (angle > 360f) angle -= 360f;
            return Mathf.Clamp(angle, min, max);
        }

        // --- 8. OBJECT PUSHING PHYSICS ---
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;

            // Ignore if there is no Rigidbody or if it is kinematic
            if (body == null || body.isKinematic) return;
            
            // Do not push the floor
            if (hit.moveDirection.y < -0.3f) return;

            // Calculate push direction and apply force
            Vector3 pushDir = new Vector3(hit.moveDirection.x, 0, hit.moveDirection.z);
            body.AddForceAtPosition(pushDir * pushPower, hit.point, ForceMode.Impulse);
        }
    }
}