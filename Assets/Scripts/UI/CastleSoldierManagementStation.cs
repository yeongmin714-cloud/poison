using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.UI
{
    /// <summary>
    /// Barracks interaction point for opening the soldier-management Toolkit window.
    /// </summary>
    public sealed class CastleSoldierManagementStation : MonoBehaviour
    {
        [SerializeField] private string _stationName = "병사 관리";
        [SerializeField] private float _interactRange = 1.8f;

        private GameObject _player;
        private bool _isPlayerNearby;

        public string StationName => _stationName;

        private void Update()
        {
            if (_player == null)
            {
                _player = GameObject.FindGameObjectWithTag("Player");
                if (_player == null) return;
            }

            _isPlayerNearby = (transform.position - _player.transform.position).sqrMagnitude <=
                              _interactRange * _interactRange;
            if (_isPlayerNearby && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                OpenSoldierManagement();
        }

        public void Configure(string stationName = null, float? interactRange = null)
        {
            if (!string.IsNullOrEmpty(stationName))
                _stationName = stationName;
            if (interactRange.HasValue && interactRange.Value > 0f)
                _interactRange = interactRange.Value;
        }

        private void OpenSoldierManagement()
        {
            if (ProjectName.UI.Toolkit.UIToolkitBootstrap.UIRoot == null)
            {
                Debug.LogWarning("[CastleSoldierManagementStation] UIToolkit UI root is unavailable.");
                return;
            }

            ProjectName.UI.Toolkit.SoldierManagementUTK.Open();
        }

        private void OnGUI()
        {
            if (ProjectName.Core.UITransitionState.AnyWindowOpen) return;
            if (!_isPlayerNearby || _player == null) return;

            const float labelWidth = 240f;
            const float labelHeight = 32f;
            float x = (Screen.width - labelWidth) * 0.5f;
            float y = Screen.height - 82f;
            GUI.Box(new Rect(x, y, labelWidth, labelHeight), $"[E] {_stationName}");
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.85f, 0.7f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, _interactRange);
        }
    }
}
