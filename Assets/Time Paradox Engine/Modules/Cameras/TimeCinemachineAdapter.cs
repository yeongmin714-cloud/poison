using UnityEngine;
using Unity.Cinemachine; // Namespace for Cinemachine 3.x (Unity 6.x)
using TimeWarp.Core;

namespace TimeWarp.Modules.Cameras
{
    /// <summary>
    /// Suspends Cinemachine camera logic during time anomalies (Rewind / Time Stop)
    /// so the camera does not fight the rewound transforms.
    ///
    /// This is the CANONICAL implementation. TimeCinemachineBrain derives from this
    /// class and adds nothing - it exists only so that scenes which already reference
    /// the old component name keep working.
    /// </summary>
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Cameras/Time Cinemachine Adapter")]
    public class TimeCinemachineAdapter : MonoBehaviour, ITimeWarpModule
    {
        private TimeWarpIdentity _identity;
        private CinemachineBrain _brain;
        private CinemachineCamera _vcam;

        private bool _isSuspended = false;

        // 'protected virtual' so the derived alias class inherits the message reliably.
        protected virtual void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            // Universal: can be attached to the Main Camera (Brain) or to a Virtual Camera (VCam)
            _brain = GetComponent<CinemachineBrain>();
            _vcam = GetComponent<CinemachineCamera>();
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered) return;

            bool shouldSuspend = _identity.IsRewinding || _identity.EffectiveTimeScale <= 0.001f;

            if (shouldSuspend && !_isSuspended)
            {
                if (_brain != null) _brain.enabled = false;
                if (_vcam != null) _vcam.enabled = false;
                _isSuspended = true;
            }
            else if (!shouldSuspend && _isSuspended)
            {
                if (_vcam != null)
                {
                    _vcam.enabled = true;
                    // Clears the camera's damping/velocity buffer, forcing an immediate cut
                    // instead of a long smear back from the rewound position.
                    _vcam.PreviousStateIsValid = false;
                }

                if (_brain != null)
                {
                    _brain.enabled = true;

                    // DELIBERATELY NOT calling _brain.ManualUpdate() here.
                    // In Cinemachine 3 that entry point is only valid when the Brain's
                    // update method is set to ManualUpdate; with the default setting it
                    // causes a second camera evaluation in the same frame (visible jitter).
                    // Cinemachine updates itself in its natural loop.
                }

                _isSuspended = false;
            }
        }
    }
}
