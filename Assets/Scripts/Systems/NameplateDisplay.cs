using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// FIX-01: NPC 위에 이름표를 표시하는 간단한 OnGUI 컴포넌트.
    /// GameObject 위쪽(float offset)에 이름 문자열을 표시합니다.
    /// </summary>
    public class NameplateDisplay : MonoBehaviour
    {
        [Header("설정")]
        [SerializeField] private string _displayName = "NPC";
        [SerializeField] private float _floatOffset = 2.0f;
        [SerializeField] private float _interactRange = 3f;

        private Transform _player;
        private bool _playerNearby;
        private Camera _mainCamera;
        private GUIStyle _nameStyle;

        /// <summary>표시할 이름</summary>
        public string DisplayName
        {
            get => _displayName;
            set => _displayName = value;
        }

        private void Start()
        {
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            _mainCamera = Camera.main;
            _nameStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
        }

        private void Update()
        {
            if (_player == null) return;
            float distSqr = (transform.position - _player.position).sqrMagnitude;
            _playerNearby = distSqr <= _interactRange * _interactRange;
        }

        private void OnGUI()
        {
            if (!_playerNearby) return;
            if (_mainCamera == null) return;

            Vector3 screenPos = _mainCamera.WorldToScreenPoint(transform.position + Vector3.up * _floatOffset);
            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            float labelW = 120f;
            float labelH = 24f;
            Rect rect = new Rect(screenPos.x - labelW / 2f, screenPos.y - labelH, labelW, labelH);

            // 배경 그림자 효과
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.Box(new Rect(rect.x - 1, rect.y - 1, rect.width + 2, rect.height + 2), "");
            GUI.color = Color.white;

            GUI.Label(rect, _displayName, _nameStyle);
        }
    }
}