using UnityEngine;
using Unity.Cinemachine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Runtime camera zoom controller for Cinemachine 3.x Third Person Follow.
    /// Handles mouse wheel zoom input at runtime (not Editor-only).
    /// </summary>
    public class CameraZoomControllerRuntime : MonoBehaviour
    {
        [Header("Zoom Settings")]
        [SerializeField] private float minDistance = 15f;
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float zoomSpeed = 5f;
        [SerializeField] private float zoomSmoothing = 10f;

        private CinemachineThirdPersonFollow _tpFollow;
        private float _targetDistance;

        private void Awake()
        {
            _tpFollow = GetComponent<CinemachineThirdPersonFollow>();
            if (_tpFollow != null)
            {
                _targetDistance = _tpFollow.CameraDistance;
            }
            else
            {
                Debug.LogError("[CameraZoomControllerRuntime] CinemachineThirdPersonFollow not found on same GameObject!");
            }
        }

        private void LateUpdate()
        {
            if (_tpFollow == null) return;

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                _targetDistance -= scroll * zoomSpeed;
                _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);
            }

            // Smooth interpolation
            _tpFollow.CameraDistance = Mathf.Lerp(_tpFollow.CameraDistance, _targetDistance, Time.deltaTime * zoomSmoothing);
        }
    }
}