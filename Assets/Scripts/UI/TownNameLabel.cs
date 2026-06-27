using ProjectName.Core;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.5] 월드 스페이스 마을 이름 라벨
    /// World Space IMGUI 기반 마을/지역 이름 표시.
    /// 플레이어가 마을 반경 내 진입 시 이름을 표시합니다.
    /// </summary>
    public class TownNameLabel : MonoBehaviour
    {
        [Header("Label Settings")]
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 3.5f, 0f);
        [SerializeField] private float _maxDisplayDistance = 30f;
        [SerializeField] private int _fontSize = 64;

        [Header("Name Settings")]
        [SerializeField] private string _townName = "마을";

        [Header("Color Settings")]
        [SerializeField] private Color _nameColor = Color.white;
        [SerializeField] private Color _shadowColor = new Color(0f, 0f, 0f, 0.6f);

        [Header("Radius Settings")]
        [SerializeField] private float _enterRadius = 15f;

        private Transform _playerCam;
        private Camera _cachedCamera;
        private GUIStyle _guiStyle;
        private GUIStyle _shadowStyle;
        private bool _styleInitialized;
        private bool _isInRange;

        private void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cachedCamera = mainCam;
                _playerCam = mainCam.transform;
            }

            if (string.IsNullOrEmpty(_townName))
            {
                _townName = gameObject.name;
            }
        }

        private void Update()
        {
            if (_playerCam == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null)
                {
                    _cachedCamera = mainCam;
                    _playerCam = mainCam.transform;
                }
                if (_playerCam == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerCam.position);
            _isInRange = dist <= _maxDisplayDistance;
        }

        private void OnGUI()
        {
            if (_playerCam == null || _cachedCamera == null) return;
            if (!_isInRange) return;

            Vector3 worldPos = transform.position + _labelOffset;
            Vector3 screenPos = _cachedCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            if (!_styleInitialized)
            {
                _guiStyle = new GUIStyle(GUI.skin.label);
                _guiStyle.fontSize = _fontSize;
                _guiStyle.alignment = TextAnchor.MiddleCenter;
                _guiStyle.normal.textColor = _nameColor;
                _guiStyle.fontStyle = FontStyle.Bold;

                _shadowStyle = new GUIStyle(_guiStyle);
                _shadowStyle.normal.textColor = _shadowColor;

                _styleInitialized = true;
            }

            float width = 200f;
            float height = 32f;
            float shadowOffset = 2f;

            // 그림자
            GUI.Label(new Rect(screenPos.x - width / 2 + shadowOffset, screenPos.y - height + shadowOffset, width, height), _townName, _shadowStyle);
            // 본 텍스트
            GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y - height, width, height), _townName, _guiStyle);
        }
    }
}
