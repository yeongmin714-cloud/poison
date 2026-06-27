using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.5] 몬스터 머리 위 레벨 표시 라벨
    /// World Space IMGUI 기반 몬스터 레벨 표시.
    /// ILevelLabel 인터페이스 구현 (Systems에서 간접 접근용).
    /// </summary>
    [RequireComponent(typeof(AnimalAI))]
    public class MonsterLevelLabel : MonoBehaviour, ILevelLabel
    {
        // 정적 생성자: LabelFactory에 등록
        static MonsterLevelLabel()
        {
            if (LabelFactory.CreateLabel == null)
            {
                LabelFactory.Register((go, level) =>
                {
                    var label = go.GetComponent<MonsterLevelLabel>();
                    if (label == null)
                        label = go.AddComponent<MonsterLevelLabel>();
                    label.SetLevel(level);
                });
            }
        }
        [Header("Label Settings")]
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private float _maxDisplayDistance = 20f;
        [SerializeField] private int _fontSize = 56;

        [Header("Color Settings")]
        [SerializeField] private Color _lowColor = Color.green;
        [SerializeField] private Color _midColor = Color.yellow;
        [SerializeField] private Color _highColor = Color.red;

        private AnimalAI _ai;
        private Transform _playerCam;
        private Camera _cachedCamera;
        private int _level;
        private string _levelText;
        private Color _labelColor;
        private GUIStyle _guiStyle;
        private bool _styleInitialized = false;

        public int CurrentLevel => _level;

        private void Start()
        {
            _ai = GetComponent<AnimalAI>();
            if (_ai == null)
            {
                Debug.LogWarning("[MonsterLevelLabel] AnimalAI 없음");
                enabled = false;
                return;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cachedCamera = mainCam;
                _playerCam = mainCam.transform;
            }

            _level = _ai.Level;
            UpdateLevelDisplay();
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
            if (dist > _maxDisplayDistance) return;
        }

        /// <summary>ILevelLabel 구현: 레벨 설정</summary>
        public void SetLevel(int level)
        {
            _level = level;
            UpdateLevelDisplay();
        }

        private void UpdateLevelDisplay()
        {
            _levelText = $"Lv.{_level}";
            _labelColor = GetLevelColor(_level);

            // GUIStyle이 이미 초기화된 경우에도 색상 업데이트 (SetLevel 재호출 대응)
            if (_styleInitialized && _guiStyle != null)
            {
                _guiStyle.normal.textColor = _labelColor;
            }
        }

        private Color GetLevelColor(int level)
        {
            // MonsterLevelData의 GreenThreshold/YellowThreshold 참조 (데이터 드리븐)
            MonsterLevelManager lvlMgr = MonsterLevelManager.Instance;
            if (lvlMgr != null && lvlMgr.Data != null)
            {
                var data = lvlMgr.Data;
                if (level <= data.GreenThreshold) return _lowColor;
                if (level <= data.YellowThreshold) return _midColor;
                return _highColor;
            }
            // Fallback: Manager/Data 없을 시 하드코딩 임계값
            if (level <= 10) return _lowColor;
            if (level <= 20) return _midColor;
            return _highColor;
        }

        private void OnGUI()
        {
            if (_playerCam == null || _cachedCamera == null) return;

            Vector3 worldPos = transform.position + _labelOffset;
            Vector3 screenPos = _cachedCamera.WorldToScreenPoint(worldPos);

            if (screenPos.z < 0) return;
            screenPos.y = Screen.height - screenPos.y;

            if (!_styleInitialized)
            {
                _guiStyle = new GUIStyle(GUI.skin.label);
                _guiStyle.fontSize = _fontSize;
                _guiStyle.alignment = TextAnchor.MiddleCenter;
                _guiStyle.normal.textColor = _labelColor;
                _guiStyle.fontStyle = FontStyle.Bold;
                _styleInitialized = true;
            }

            float width = 80f;
            float height = 24f;
            GUI.Label(new Rect(screenPos.x - width / 2, screenPos.y - height, width, height), _levelText, _guiStyle);
        }
    }
}