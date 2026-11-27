using UnityEngine;
using TimeWarp.Core;

namespace TimeWarp.Modules.Physics.Joints
{
    [RequireComponent(typeof(HingeJoint))]
    [RequireComponent(typeof(TimeWarpIdentity))]
    [AddComponentMenu("TimeWarp/Physics/Time Hinge Joint")]
    public class TimeHingeJoint : MonoBehaviour, ITimeWarpModule
    {
        private HingeJoint _joint;
        private TimeWarpIdentity _identity;
        
        private bool _wasMotorEnabled;
        private bool _wasSpringEnabled;
        private bool _wasCollisionEnabled;
        private bool _isSuspended = false;

        private void Awake()
        {
            _joint = GetComponent<HingeJoint>();
            _identity = GetComponent<TimeWarpIdentity>();
        }

        public void OnWarpUpdate() { }

        public void OnWarpFixedUpdate()
        {
            if (!_identity.IsRegistered) return;

            // Suspend the joint's operation during time rewind or a complete time stop
            bool shouldSuspend = _identity.IsRewinding || _identity.EffectiveTimeScale <= 0.001f;

            if (shouldSuspend && !_isSuspended)
            {
                // STORE STATE
                _wasMotorEnabled = _joint.useMotor;
                _wasSpringEnabled = _joint.useSpring;
                _wasCollisionEnabled = _joint.enableCollision;
                
                // FREEZE (so the joint doesn't fight the teleportation from the buffer)
                _joint.useMotor = false;
                _joint.useSpring = false;
                _joint.enableCollision = false; 
                _isSuspended = true;
            }
            else if (!shouldSuspend && _isSuspended)
            {
                // RESTORE ORIGINAL STATE
                _joint.useMotor = _wasMotorEnabled;
                _joint.useSpring = _wasSpringEnabled;
                _joint.enableCollision = _wasCollisionEnabled;
                _isSuspended = false;
            }
        }
    }
}