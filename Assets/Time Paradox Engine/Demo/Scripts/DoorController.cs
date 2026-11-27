using UnityEngine;
using System.Collections.Generic;
using System.Linq; 
using TimeWarp.Core;
using TimeWarp.Core.Managers; // Required to read the capture framerate

namespace TimeWarp.Gameplay
{
    [RequireComponent(typeof(TimeWarpIdentity))]
    public class TimeWarpSequentialDoor : MonoBehaviour, ITimeWarpModule
    {
        [Header("Door References (Cubes)")]
        [Tooltip("Leave this empty! The script will automatically fetch and sort all child objects at startup.")]
        public Transform[] doorPieces;

        [Header("Animation Settings (Direction & Speed)")]
        public Vector3 openOffset = new Vector3(0, 3f, 0); 
        public float moveSpeed = 4.0f; 
        [Tooltip("Time (in seconds) after which the NEXT piece starts moving. (e.g., 0.05 makes a fast wave)")]
        public float delayBetweenPieces = 0.05f; 

        [Header("Puzzle Requirements")]
        public int requiredPlates = 3;

        private TimeWarpIdentity _identity;
        private int _currentPressedPlates = 0;
        private bool _isLocked = false;
        
        private Vector3[] _basePositions;
        private Vector3[] _targetPositions;
        
        private float _timeSinceOpened = 0f;
        private bool _isOpening = false;
        
        // NEW: Variable to track how much time has been rewound
        private float _previousOffset = 0f; 

        private void Awake()
        {
            _identity = GetComponent<TimeWarpIdentity>();
            InitializeDoorPieces();
        }

        [ContextMenu("⚙️ Auto-fetch and Sort Cubes (Top-Left)")]
        public void AutoSetupPieces()
        {
            List<Transform> children = new List<Transform>();
            foreach (Transform child in transform) children.Add(child);

            // LINQ MAGIC: Sort by Y descending (top to bottom), then by X ascending (left to right)
            // Y is rounded to 1 decimal place to group slightly misaligned cubes into neat rows.
            doorPieces = children.OrderByDescending(t => Mathf.Round(t.localPosition.y * 10f) / 10f)
                                 .ThenBy(t => t.localPosition.x)
                                 .ToArray();
                                 
            Debug.Log($"[TimeWarp] Successfully fetched and sorted {doorPieces.Length} door pieces.");
        }

        private void InitializeDoorPieces()
        {
            // If the list is empty, fetch and sort children automatically
            if (doorPieces == null || doorPieces.Length == 0) AutoSetupPieces();
            if (doorPieces.Length == 0) return;

            _basePositions = new Vector3[doorPieces.Length];
            _targetPositions = new Vector3[doorPieces.Length];

            for (int i = 0; i < doorPieces.Length; i++)
            {
                _basePositions[i] = doorPieces[i].localPosition;
                _targetPositions[i] = _basePositions[i] + openOffset;
            }
        }

        public void PlatePressed()
        {
            if (_isLocked) return;
            _currentPressedPlates++;
            CheckDoorState();
        }

        public void PlateReleased()
        {
            if (_isLocked) return;
            _currentPressedPlates--;
        }

        private void CheckDoorState()
        {
            if (_currentPressedPlates >= requiredPlates && !_isLocked)
            {
                _isLocked = true;
                _isOpening = true;
                _timeSinceOpened = 0f; 
                Debug.Log("[Puzzle] Door is opening with a smooth wave!");
            }
        }

        public bool IsPuzzleSolved() => _isLocked;

        public void OnWarpFixedUpdate() { }

        public void OnWarpUpdate()
        {
            if (!_identity.IsRegistered || doorPieces == null || doorPieces.Length == 0) return;

            float timeScale = _identity.EffectiveTimeScale;
            bool isRewinding = _identity.IsRewinding || _identity.FractionalOffset > 0.001f;

            if (isRewinding)
            {
                // === TIME REWIND LOGIC ===
                
                // 1. Calculate exactly how many seconds the game rewound in this specific frame
                float currentOffset = _identity.FractionalOffset;
                float framerate = TimeWarpManager.Instance.CaptureFramerate;
                float dt = (_previousOffset - currentOffset) / framerate; 
                _previousOffset = currentOffset;

                // 2. Reverse our wave timer
                if (_isOpening)
                {
                    // dt will be negative (e.g., -0.02s), so adding it reduces our timer
                    _timeSinceOpened += dt; 

                    // 3. Paradox! If we rewound before the puzzle was solved, reset the puzzle state.
                    if (_timeSinceOpened <= 0f)
                    {
                        _timeSinceOpened = 0f;
                        _isOpening = false;
                        _isLocked = false;
                        _currentPressedPlates = 0; // Forces the player to step on plates again
                        Debug.Log("[Puzzle] Time rewound. Door is locked again!");
                    }
                }
                
                // Exit the method. The physical cubes are currently being moved by the C++ RAM manager!
                return; 
            }
            else
            {
                // === NORMAL TIME PROGRESSION LOGIC ===
                
                _previousOffset = 0f; // Reset safeguard for the next time we rewind
                
                if (_isOpening)
                {
                    _timeSinceOpened += Time.deltaTime * timeScale;
                    float currentMoveStep = moveSpeed * Time.deltaTime * Mathf.Max(timeScale, 0f);

                    for (int i = 0; i < doorPieces.Length; i++)
                    {
                        float startTimeForThisPiece = i * delayBetweenPieces;
                        
                        if (_timeSinceOpened >= startTimeForThisPiece)
                        {
                            Transform piece = doorPieces[i];
                            Vector3 targetPos = _targetPositions[i];
                            
                            if (piece.localPosition != targetPos)
                            {
                                piece.localPosition = Vector3.MoveTowards(piece.localPosition, targetPos, currentMoveStep);
                            }
                        }
                    }
                }
            }
        }
    }
}