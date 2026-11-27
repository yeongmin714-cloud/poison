using UnityEngine;
using UnityEngine.AI;
using TimeWarp.Core;

namespace StarterAssets
{
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyTimeChaser : MonoBehaviour
    {
        [Header("AI Setup")]
        public Transform Target;
        public float StopDistance = 1.5f;

        [Header("Vision & Aggro")]
        [Tooltip("From what distance can the enemy spot you?")]
        public float DetectionRange = 15.0f;
        
        [Tooltip("From what distance does the enemy give up the chase? (Should be larger than DetectionRange)")]
        public float LoseTargetRange = 25.0f;
        
        [Tooltip("The enemy's field of view in degrees (e.g., 120 means they see what's in front of them).")]
        public float FieldOfView = 120f;
        
        [Tooltip("The Transform from which the line of sight originates (e.g., enemy's head). Leave empty to use the object's center.")]
        public Transform EyePosition;
        
        [Tooltip("Layers that block the enemy's vision (e.g., Default, Walls, Ground).")]
        public LayerMask ObstacleLayers;

        [Header("Movement Setup")]
        public float MoveSpeed = 4.0f;
        public float SpeedChangeRate = 10.0f;
        [Range(0.0f, 0.3f)] public float RotationSmoothTime = 0.12f;

        [Header("Gravity Setup")]
        public float Gravity = -15.0f;

        [Header("Ground Detection")]
        public bool Grounded = true;
        public float GroundedOffset = -0.14f;
        public float GroundedRadius = 0.28f;
        public LayerMask GroundLayers;

        // ==========================================
        // NEW: AI Pathfinding Optimization
        // ==========================================
        [Header("AI Pathfinding Optimization")]
        [Tooltip("Time in seconds between path calculations. Higher values improve performance but reduce responsiveness.")]
        public float PathUpdateInterval = 0.2f;

        [HideInInspector] public bool IsStunned = false;

        // Internal State
        public float _currentSpeed;
        public float _animationBlend;
        public float _targetRotation;
        public float _rotationVelocity;
        private float _verticalVelocity;
        public bool _hasAggro = false; // Is the enemy currently chasing us?
        public float _pathUpdateTimer = 0f; // Timer to control path recalculation frequency
        
        // References
        private Animator _animator;
        private CharacterController _controller;
        private NavMeshAgent _agent;
        private TimeWarpIdentity _timeIdentity;

        // Animation Hashes 
        private readonly int _animSpeed = Animator.StringToHash("Speed");
        private readonly int _animGrounded = Animator.StringToHash("IsGrounded");

        private void Start()
        {
            _animator = GetComponent<Animator>();
            _controller = GetComponent<CharacterController>();
            _agent = GetComponent<NavMeshAgent>();
            _timeIdentity = GetComponent<TimeWarpIdentity>();

            if (Target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) Target = player.transform;
            }

            // We handle movement and rotation manually using CharacterController
            _agent.updatePosition = false; 
            _agent.updateRotation = false; 
            _controller.minMoveDistance = 0f; 
        }

        public void SetStunState(bool isStunned)
        {
            IsStunned = isStunned;
            if (isStunned)
            {
                if (_controller.enabled) _controller.enabled = false;
                if (_agent.enabled) _agent.enabled = false;
            }
        }

        private void Update()
        {
            if (IsStunned) return;

            // 1. TIME MANAGEMENT
            float localTimeScale = 1f;
            bool shouldLockController = false;

            if (_timeIdentity != null && _timeIdentity.IsRegistered)
            {
                localTimeScale = _timeIdentity.EffectiveTimeScale;
                shouldLockController = _timeIdentity.IsRewinding || _timeIdentity.FractionalOffset > 0.001f || localTimeScale <= 0.001f;
            }

            if (shouldLockController)
            {
                if (_controller.enabled) _controller.enabled = false;
                if (_agent.enabled) _agent.enabled = false;
                return;
            }

            if (!_controller.enabled) _controller.enabled = true;
            if (!_agent.enabled) _agent.enabled = true;

            float dt = Time.deltaTime * localTimeScale;

            // 2. AI PATHFINDING & VISION
            Vector3 moveDirection = Vector3.zero;
            float targetSpeed = 0f;

            if (Target != null && _agent.isOnNavMesh)
            {
                float distanceToTarget = Vector3.Distance(transform.position, Target.position);

                // Check whether to enable or disable the chase
                if (!_hasAggro)
                {
                    // To spot the player, they must be in range AND be visible
                    if (distanceToTarget <= DetectionRange && HasLineOfSight())
                    {
                        _hasAggro = true;
                        _pathUpdateTimer = 0f; // Force immediate path calculation upon spotting
                    }
                }
                else
                {
                    // Give up the chase if the player has run too far away
                    if (distanceToTarget > LoseTargetRange)
                    {
                        _hasAggro = false;
                        _agent.ResetPath(); // Clear the path so the enemy stands still
                    }
                }

                // Generate movement only when the enemy has a target in their sights
                if (_hasAggro)
                {
                    // Decrease the timer using scaled delta time (slows down during TimeWarp)
                    _pathUpdateTimer -= dt;

                    // Update the path only when the timer reaches zero
                    if (_pathUpdateTimer <= 0f)
                    {
                        _agent.SetDestination(Target.position);
                        _pathUpdateTimer = PathUpdateInterval; // Reset the timer
                    }
                    
                    if (distanceToTarget > StopDistance)
                    {
                        targetSpeed = MoveSpeed;
                        Vector3 desiredVelocity = _agent.desiredVelocity;
                        moveDirection = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z).normalized;
                    }
                }
            }

            // 3. MAIN LOGIC LOOP
            UpdateGroundStatus();
            ProcessGravity(dt);
            ProcessMovement(moveDirection, targetSpeed, dt);
            
            // Sync NavMeshAgent position with the CharacterController
            _agent.nextPosition = transform.position; 
        }

        // ==========================================
        // VISION SYSTEM: Checking field and line of sight
        // ==========================================
        private bool HasLineOfSight()
        {
            if (Target == null) return false;

            // 1. Check field of view
            Vector3 dirToTarget = (Target.position - transform.position).normalized;
            dirToTarget.y = 0; // Ignore height differences for the angle calculation
            float angleToTarget = Vector3.Angle(transform.forward, dirToTarget);

            if (angleToTarget > FieldOfView / 2f)
            {
                return false; // Player is outside the field of view (e.g., behind the enemy)
            }

            // 2. Check for obstacles (Raycast)
            // Use the defined eye position, or the character's center + offset
            Vector3 origin = EyePosition != null ? EyePosition.position : transform.position + Vector3.up * 1.5f;
            Vector3 targetCenter = Target.position + Vector3.up * 1.0f; // Aim roughly at the center of the player's chest
            Vector3 rayDirection = targetCenter - origin;

            // Cast a ray. If it hits a blocking layer (e.g., a wall), the enemy doesn't see the player
            if (Physics.Raycast(origin, rayDirection.normalized, out RaycastHit hit, DetectionRange, ObstacleLayers))
            {
                // We hit something! Check if it happens to be the player.
                if (hit.transform != Target && hit.transform.root != Target.root)
                {
                    // We hit a wall or another obstacle
                    return false; 
                }
            }

            return true; // No obstacles and the player is within the angle range
        }

        private void UpdateGroundStatus()
        {
            Vector3 spherePos = new Vector3(transform.position.x, transform.position.y - GroundedOffset, transform.position.z);
            Grounded = Physics.CheckSphere(spherePos, GroundedRadius, GroundLayers, QueryTriggerInteraction.Ignore);

            if (_animator != null) _animator.SetBool(_animGrounded, Grounded);
        }

        private void ProcessGravity(float dt)
        {
            if (Grounded)
            {
                if (_verticalVelocity < 0.0f) _verticalVelocity = -2f;
            }

            if (_verticalVelocity < 53.0f) _verticalVelocity += Gravity * dt;
        }

        private void ProcessMovement(Vector3 moveDirection, float targetSpeed, float dt)
        {
            if (Mathf.Abs(_currentSpeed - targetSpeed) > 0.1f)
            {
                _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * SpeedChangeRate);
            }
            else
            {
                _currentSpeed = targetSpeed;
            }

            _animationBlend = Mathf.Lerp(_animationBlend, targetSpeed, dt * SpeedChangeRate);
            if (_animationBlend < 0.01f) _animationBlend = 0f;

            if (moveDirection != Vector3.zero)
            {
                _targetRotation = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float rotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, _targetRotation, ref _rotationVelocity, RotationSmoothTime, float.MaxValue, dt);
                transform.rotation = Quaternion.Euler(0.0f, rotation, 0.0f);
            }

            _controller.Move((moveDirection.normalized * _currentSpeed + Vector3.up * _verticalVelocity) * dt);

            if (_animator != null)
            {
                _animator.SetFloat(_animSpeed, _animationBlend);
            }
        }
        
        private void OnFootstep(AnimationEvent animationEvent) { }
        private void OnLand(AnimationEvent animationEvent) { }
    }
}