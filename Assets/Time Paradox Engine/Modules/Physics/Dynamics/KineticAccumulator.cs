using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TimeWarp.Core.Managers;
using TimeWarp.Core;

namespace TimeWarp.Modules.Physics.Dynamics
{
    public enum KineticReleaseMode
    {
        CombinedImpulse, // Original: one massive combined hit
        SequentialHits   // New: releases hits one by one (anime/Jojo effect)
    }

    /// <summary>
    /// Accumulates kinetic force during time stop and releases it upon physics resumption.
    /// Catches both scripted attacks (Raycast) and physical collisions (OnCollisionEnter).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [AddComponentMenu("TimeWarp/Physics/Kinetic Accumulator")]
    public class KineticAccumulator : MonoBehaviour
    {
        [Header("Release Settings")]
        public KineticReleaseMode ReleaseMode = KineticReleaseMode.CombinedImpulse;
        
        [Tooltip("Maximum force for a single burst (Combined) OR for a single hit (Sequential).")]
        public float MaxForceCap = 500f;
        
        [Tooltip("Delay in seconds between consecutive hits in Sequential mode.")]
        public float SequentialDelay = 0.1f;

        private Rigidbody _rb;
        
        // Memory for Combined mode
        private Vector3 _accumulatedForce = Vector3.zero;
        
        // Memory for Sequential mode (FIFO Queue)
        private Queue<Vector3> _impactQueue = new Queue<Vector3>();
        private Coroutine _sequentialReleaseCoroutine;

        private void Awake() => _rb = GetComponent<Rigidbody>();

        // =========================================================
        // MANUAL FORCE ADDITION (e.g., from fist script, Raycast weapon)
        // =========================================================
        public void AddKineticImpact(Vector3 forceVector)
        {
            // If time flows normally, pass the force straight to physics
            if (TimeWarpManager.Instance.GlobalTimeState == TimeState.Normal)
            {
                _rb.AddForce(forceVector, ForceMode.Impulse);
                return;
            }

            // Time is stopped! Save the hit to BOTH memories 
            // (Protects against errors if someone changes ReleaseMode during pause)
            _accumulatedForce += forceVector;
            _impactQueue.Enqueue(forceVector);
        }

        // =========================================================
        // AUTO-DETECTION OF PHYSICS (Pushing, throwing barrels, physical projectiles)
        // =========================================================
        private void OnCollisionEnter(Collision collision)
        {
            // We are interested ONLY in collisions during stopped time
            if (TimeWarpManager.Instance.GlobalTimeState == TimeState.Normal) return;

            Vector3 impactForce = Vector3.zero;

            if (collision.rigidbody != null)
            {
                // Hitting object has its own mass (e.g., physical projectile or barrel)
                impactForce = collision.relativeVelocity * collision.rigidbody.mass;
            }
            else
            {
                // Hitting object has no physical mass (e.g., player's CharacterController)
                impactForce = collision.relativeVelocity * 50f; // 50 is a default, artificial multiplier
            }

            // Ignore micro-collisions (so lying on the ground doesn't charge the accumulator)
            if (impactForce.sqrMagnitude > 0.1f)
            {
                AddKineticImpact(impactForce);
            }
        }

        // =========================================================
        // ENERGY RELEASE (You must call this when you resume time!)
        // =========================================================
        public void ReleaseEnergy()
        {
            if (ReleaseMode == KineticReleaseMode.CombinedImpulse)
            {
                if (_accumulatedForce.sqrMagnitude > 0.01f)
                {
                    Vector3 safeForce = Vector3.ClampMagnitude(_accumulatedForce, MaxForceCap);
                    
                    // Wake up physics before impact so Unity doesn't ignore AddForce
                    if (_rb.isKinematic) _rb.isKinematic = false;
                    _rb.WakeUp();
                    
                    _rb.AddForce(safeForce, ForceMode.Impulse);
                }
                
                // Clear memory after a massive hit
                _accumulatedForce = Vector3.zero;
                _impactQueue.Clear(); 
            }
            else if (ReleaseMode == KineticReleaseMode.SequentialHits)
            {
                _accumulatedForce = Vector3.zero; // Clear unused memory
                
                if (_impactQueue.Count > 0)
                {
                    if (_sequentialReleaseCoroutine != null) StopCoroutine(_sequentialReleaseCoroutine);
                    _sequentialReleaseCoroutine = StartCoroutine(ReleaseSequentialRoutine());
                }
            }
        }

        // =========================================================
        // COROUTINE FOR SEQUENTIAL MODE (Delivering hits over time)
        // =========================================================
        private IEnumerator ReleaseSequentialRoutine()
        {
            while (_impactQueue.Count > 0)
            {
                // FAILSAFE: If the player stops time AGAIN (which puts physics to sleep),
                // interrupt playback. The rest of the hits politely wait in the queue for the next resumption!
                if (_rb.isKinematic)
                {
                    yield break; 
                }

                // Wake up physics just to be sure before each hit
                _rb.WakeUp();

                Vector3 hit = _impactQueue.Dequeue();
                Vector3 safeForce = Vector3.ClampMagnitude(hit, MaxForceCap);
                
                _rb.AddForce(safeForce, ForceMode.Impulse);

                yield return new WaitForSeconds(SequentialDelay);
            }
            
            _sequentialReleaseCoroutine = null;
        }
    }
}