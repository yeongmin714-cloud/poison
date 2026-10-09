using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// Legacy secondary gas control retained only for its configured right-click hold input.
    /// G toggle input is exclusively owned by GasSprayer.Update (Input System).
    /// </summary>
    [RequireComponent(typeof(GasSprayerController))]
    public class SprayInputHandler : MonoBehaviour
    {
        private GasSprayerController _controller;
        private bool _startedSprayFromHold;

        [SerializeField, Tooltip("Spray hold key (right mouse button)")]
        private KeyCode _sprayHoldKey = KeyCode.Mouse1;

        private void Awake()
        {
            _controller = GetComponent<GasSprayerController>();
            if (_controller == null)
            {
                Debug.LogError("[SprayInputHandler] GasSprayerController not found.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (_controller == null) return;
            if (!_controller.IsEquipped || _controller.IsReloading)
            {
                _startedSprayFromHold = false;
                if (_controller.IsSpraying) _controller.StopSpray();
                return;
            }

            bool sprayHeld = Input.GetKey(_sprayHoldKey);
            if (sprayHeld && !_controller.IsSpraying)
            {
                _controller.StartSpray();
                _startedSprayFromHold = _controller.IsSpraying;
            }
            else if (!sprayHeld && _startedSprayFromHold && _controller.IsSpraying)
            {
                _controller.StopSpray();
                _startedSprayFromHold = false;
            }
            else if (!sprayHeld)
            {
                _startedSprayFromHold = false;
            }
        }
    }
}
