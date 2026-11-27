using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TimeWarp.Core.Managers;
using TimeWarp.Core;

namespace TimeWarp.Modules.Physics.Dynamics
{
    [RequireComponent(typeof(Rigidbody2D))]
    [AddComponentMenu("TimeWarp/Physics 2D/Kinetic Accumulator 2D")]
    public class KineticAccumulator2D : MonoBehaviour
    {
        [Header("Release Settings")]
        public KineticReleaseMode ReleaseMode = KineticReleaseMode.CombinedImpulse;
        
        [Tooltip("Maximum force that can be accumulated (prevents launching out of the map)")]
        public float MaxForceCap = 500f;
        
        [Tooltip("Delay (in seconds) between consecutive hits in Sequential mode.")]
        public float SequentialDelay = 0.1f;

        private Rigidbody2D _rb2d;
        
        // Pamięć dla trybu Combined
        private Vector2 _accumulatedForce = Vector2.zero;
        
        // Pamięć dla trybu Sequential (Kolejka FIFO)
        private Queue<Vector2> _impactQueue = new Queue<Vector2>();
        private Coroutine _sequentialReleaseCoroutine;

        private void Awake() => _rb2d = GetComponent<Rigidbody2D>();

        public void AddKineticImpact(Vector2 forceVector)
        {
            if (TimeWarpManager.Instance.GlobalTimeState == TimeState.Normal)
            {
                _rb2d.AddForce(forceVector, ForceMode2D.Impulse);
                return;
            }

            // Zapisujemy energię do obu systemów pamięci na czas zamrożenia
            _accumulatedForce += forceVector;
            _impactQueue.Enqueue(forceVector);
        }

        public void ReleaseEnergy()
        {
            if (ReleaseMode == KineticReleaseMode.CombinedImpulse)
            {
                if (_accumulatedForce.sqrMagnitude > 0.01f)
                {
                    Vector2 safeForce = Vector2.ClampMagnitude(_accumulatedForce, MaxForceCap);
                    _rb2d.AddForce(safeForce, ForceMode2D.Impulse);
                }
                _accumulatedForce = Vector2.zero;
                _impactQueue.Clear();
            }
            else if (ReleaseMode == KineticReleaseMode.SequentialHits)
            {
                _accumulatedForce = Vector2.zero;
                if (_impactQueue.Count > 0)
                {
                    if (_sequentialReleaseCoroutine != null) StopCoroutine(_sequentialReleaseCoroutine);
                    _sequentialReleaseCoroutine = StartCoroutine(ReleaseSequentialRoutine());
                }
            }
        }

        private IEnumerator ReleaseSequentialRoutine()
        {
            while (_impactQueue.Count > 0)
            {
                // Zabezpieczenie: jeśli gracz ponownie zatrzyma czas (co ustawi isKinematic), przerywamy zrzut!
                if (_rb2d.bodyType == RigidbodyType2D.Kinematic)
                {
                    yield break; 
                }

                Vector2 hit = _impactQueue.Dequeue();
                Vector2 safeForce = Vector2.ClampMagnitude(hit, MaxForceCap);
                
                _rb2d.AddForce(safeForce, ForceMode2D.Impulse);

                yield return new WaitForSeconds(SequentialDelay);
            }
            
            _sequentialReleaseCoroutine = null;
        }
    }
}