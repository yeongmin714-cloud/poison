using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using TimeWarp.Core;

namespace TimeWarp.Modules.Navigation
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    [RequireComponent(typeof(NavMeshAgent))]
    [DefaultExecutionOrder(-150)]
    [AddComponentMenu("TimeWarp/Navigation/Time NavMesh Agent")]
    public class TimeNavMeshAgent : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private NavMeshAgent _agent;

        private float _baseSpeed;
        private float _baseAngularSpeed;
        private float _baseAcceleration;

        private bool _wasSuspended = false;
        private Vector3 _lastDestination;
        private bool _hadPath;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            _agent = GetComponent<NavMeshAgent>();

            _baseSpeed = _agent.speed;
            _baseAngularSpeed = _agent.angularSpeed;
            _baseAcceleration = _agent.acceleration;
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || (_identity.Channels & TimeChannels.Navigation) == 0) return;

            float scale = _identity.EffectiveTimeScale;
            bool isRewinding = _identity.IsRewinding;
            bool shouldBeSuspended = isRewinding || scale <= 0.001f;

            if (shouldBeSuspended)
            {
                if (!_wasSuspended)
                {
                    if (_agent.isOnNavMesh)
                    {
                        _hadPath = _agent.hasPath;
                        _lastDestination = _agent.destination;
                    }
                    
                    _agent.enabled = false; 
                    _wasSuspended = true;
                }
            }
            else
            {
                if (_wasSuspended)
                {
                    _wasSuspended = false;
                    StartCoroutine(RecoverAgentRoutine());
                }

                float safeScale = Mathf.Max(0f, scale);
                _agent.speed = _baseSpeed * safeScale;
                _agent.angularSpeed = _baseAngularSpeed * safeScale;
                // Keep the multiplication so the agent correctly builds up speed in slow-mo
                _agent.acceleration = _baseAcceleration * safeScale;

                // --- BRAKE FIX (ANTI-SLIP) ---
                // Ensure the agent is active and on the navmesh
                if (_agent.isActiveAndEnabled && _agent.isOnNavMesh)
                {
                    // If its physical momentum is still higher than its new, slowed maximum speed...
                    if (_agent.velocity.magnitude > _agent.speed)
                    {
                        if (safeScale <= 0.001f)
                        {
                            _agent.velocity = Vector3.zero; // Full freeze
                        }
                        else
                        {
                            // Cut the momentum immediately, matching it to the new speed limit
                            _agent.velocity = _agent.velocity.normalized * _agent.speed;
                        }
                    }
                }
            }
        }

        private IEnumerator RecoverAgentRoutine()
        {
            _agent.enabled = true;
            
            yield return null; 

            if (_agent != null && _agent.isOnNavMesh)
            {
                _agent.Warp(transform.position); 

                if (_identity.NavigationSettings.RestoreDestination && _hadPath)
                {
                    _agent.SetDestination(_lastDestination);
                }
            }
        }
    }
}