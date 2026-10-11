using UnityEngine;
using UnityEngine.InputSystem;

namespace ProjectName.UI
{
    /// <summary>
    /// [40차] 공격대 배치(전투 배치 편성) 상호작용 지점 — BarracksMapTable(병사 관리) 아래에
    /// 배치된 전술 배치판. E키 → AttackFormationUTK(피그마 공격조 전투 배치 204:1963) 오픈.
    /// </summary>
    public sealed class CastleAttackFormationStation : MonoBehaviour
    {
        [SerializeField] private string _stationName = "공격대 배치";
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
                OpenFormation();
        }

        public void Configure(string stationName = null, float? interactRange = null)
        {
            if (!string.IsNullOrEmpty(stationName))
                _stationName = stationName;
            if (interactRange.HasValue && interactRange.Value > 0f)
                _interactRange = interactRange.Value;
        }

        private void OpenFormation()
        {
            if (ProjectName.UI.Toolkit.UIToolkitBootstrap.UIRoot == null)
            {
                Debug.LogWarning("[CastleAttackFormationStation] UIToolkit UI root is unavailable.");
                return;
            }

            ProjectName.UI.Toolkit.AttackFormationUTK.Open();
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
    }
}
