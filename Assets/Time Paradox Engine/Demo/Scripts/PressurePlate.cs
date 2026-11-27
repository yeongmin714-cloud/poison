using UnityEngine;
using TimeWarp.Core;
using TimeWarp.Core.Managers;

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    public class TimeWarpPressurePlate : MonoBehaviour, ITimeWarpModule
    {
        [Header("References")]
        public TimeWarpSequentialDoor doorController;
        public Transform plateVisual;

        [Header("Visual & Movement Settings")]
        public Vector3 pressOffset = new Vector3(0, -0.2f, 0);
        public float movementSpeed = 5.0f;

        [Header("Logic Settings")]
        public string triggerTag = "Player";
        public float releaseDelay = 2.0f;

        private TimeWarpIdentity _identity;
        private Vector3 _unpressedPosition;
        private Vector3 _targetPosition;
        
        private int _objectsOnPlate = 0;
        private float _currentDelayTimer = 0f;
        private bool _isStayDown = false;
        private bool _isTimerActive = false;
        private bool _isPressedState = false;

        // NEW: Variable to track time offset for logic rewinding
        private float _previousOffset = 0f;

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            if (plateVisual != null)
            {
                _unpressedPosition = plateVisual.localPosition;
                _targetPosition = _unpressedPosition;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(triggerTag))
            {
                _objectsOnPlate++;

                if (_objectsOnPlate == 1 && !_isStayDown)
                {
                    _isTimerActive = false;
                    _targetPosition = _unpressedPosition + pressOffset;
                    
                    if (!_isPressedState)
                    {
                        _isPressedState = true;
                        doorController.PlatePressed();
                    }
                }
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(triggerTag))
            {
                _objectsOnPlate--;

                if (_objectsOnPlate <= 0 && !_isStayDown)
                {
                    _objectsOnPlate = 0;
                    _isTimerActive = true;
                    _currentDelayTimer = releaseDelay;
                }
            }
        }

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered) return;

            float currentOffset = _identity.FractionalOffset;
            bool isRewinding = _identity.IsRewinding || currentOffset > 0.001f;

            if (isRewinding)
            {
                // === REWINDING PRESSURE PLATE LOGIC ===
                float framerate = TimeWarpManager.Instance.CaptureFramerate;
                float dt = (_previousOffset - currentOffset) / framerate;
                _previousOffset = currentOffset;

                // If the door was locked (puzzle solved), and we rewind before that moment:
                if (_isStayDown && !doorController.IsPuzzleSolved())
                {
                    _isStayDown = false;
                    _targetPosition = _unpressedPosition;
                    _isPressedState = false;
                    Debug.Log("[PressurePlate] Logic rewound: Plate is no longer locked down.");
                }

                return; 
            }

            // === NORMAL TIME LOGIC ===
            _previousOffset = 0f;
            float timeScale = _identity.EffectiveTimeScale;

            if (_isTimerActive && !_isStayDown)
            {
                _currentDelayTimer -= Time.deltaTime * timeScale;

                if (_currentDelayTimer <= 0f)
                {
                    _isTimerActive = false;

                    if (doorController.IsPuzzleSolved())
                    {
                        _isStayDown = true;
                    }
                    else
                    {
                        _targetPosition = _unpressedPosition;
                        if (_isPressedState)
                        {
                            _isPressedState = false;
                            doorController.PlateReleased();
                        }
                    }
                }
            }

            if (plateVisual != null && plateVisual.localPosition != _targetPosition)
            {
                float moveStep = movementSpeed * Time.deltaTime * Mathf.Max(timeScale, 0f);
                plateVisual.localPosition = Vector3.MoveTowards(plateVisual.localPosition, _targetPosition, moveStep);
            }
        }
    }
}