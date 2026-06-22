using ProjectName.Core;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.5] 몬스터 머리 위 레벨 표시 라벨
    ///
    /// World Space Canvas 기반으로 몬스터 머리 위에 레벨을 표시합니다.
    /// 🟢 Lv.1~10, 🟡 Lv.11~20, 🔴 Lv.21~30+
    /// 20m 거리 컬링 적용.
    /// AnimalAI에 연결하여 TakeDamage/Die에 레벨 보정 적용.
    /// </summary>
    [RequireComponent(typeof(AnimalAI))]
    public class MonsterLevelLabel : MonoBehaviour
    {
        [Header("Label Settings")]
        [SerializeField] private GameObject _labelPrefab;
        [SerializeField] private Vector3 _labelOffset = new Vector3(0f, 1.8f, 0f);
        [SerializeField] private float _maxDisplayDistance = 20f;
        [SerializeField] private float _labelScale = 0.02f;
        [SerializeField] private Font _labelFont;
        [SerializeField] private int _fontSize = 14;

        [Header("Color Settings")]
        [SerializeField] private Color _lowColor = Color.green;
        [SerializeField] private Color _midColor = Color.yellow;
        [SerializeField] private Color _highColor = Color.red;

        // 캐싱
        private UIDesignTheme _theme;
        private AnimalAI _ai;
        private Transform _playerCam;
        private int _level;
        private string _levelText;
        private Color _labelColor;
        private GUIStyle _guiStyle;
        private bool _styleInitialized = false;

        /// <summary>몬스터 레벨 (읽기 전용)</summary>
        public int Level => _level;

        private void Awake()
        {
            _ai = GetComponent<AnimalAI>();
            _theme = Phase33_Themes.MonsterLevelTheme();
        }

        private void Start()
        {
            // 플레이어 카메라 찾기
            Camera mainCam = Camera.main;
            if (mainCam != null)
                _playerCam = mainCam.transform;

            // MonsterLevelManager에서 레벨 정보 가져오기
            Systems.MonsterLevelManager mgr = Systems.MonsterLevelManager.Instance;
            if (mgr != null && _ai != null)
            {
                // _level은 AnimalAI가 설정했다고 가정
                // 레벨 표시 문자열 준비
                UpdateLevelDisplay();
            }
        }

        /// <summary>
        /// 외부에서 레벨 설정 (AnimalAI 또는 MonsterSpawner에서 호출)
        /// </summary>
        public void SetLevel(int level)
        {
            _level = level;
            UpdateLevelDisplay();
        }

        private void UpdateLevelDisplay()
        {
            Systems.MonsterLevelManager mgr = Systems.MonsterLevelManager.Instance;
            if (mgr != null)
            {
                _levelText = mgr.GetLevelDisplay(_level);
                // 색상 결정
                string tag = mgr.GetLevelColorTag(_level);
                if (tag == "🟢")
                    _labelColor = _lowColor;
                else if (tag == "🟡")
                    _labelColor = _midColor;
                else
                    _labelColor = _highColor;
            }
            else
            {
                _levelText = $"Lv.{_level}";
                _labelColor = _lowColor;
            }
        }

        private void OnGUI()
        {
            if (_ai == null || _ai.IsDead) return;

            // 거리 컬링: 플레이어 카메라로부터 20m 이상이면 표시 안 함
            if (_playerCam != null)
            {
                float dist = Vector3.Distance(_playerCam.position, transform.position);
                if (dist > _maxDisplayDistance) return;
            }

            // 월드 좌표 → 스크린 좌표 변환
            Vector3 worldPos = transform.position + _labelOffset;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPos);

            // 화면 뒤에 있으면 표시 안 함
            if (screenPos.z < 0) return;

            // GUI 스타일 초기화
            if (!_styleInitialized)
            {
                _guiStyle = new GUIStyle();
                if (_labelFont != null)
                    _guiStyle.font = _labelFont;
                _guiStyle.fontSize = _fontSize;
                _guiStyle.fontStyle = FontStyle.Bold;
                _guiStyle.normal.textColor = _labelColor;
                _guiStyle.alignment = TextAnchor.MiddleCenter;
                _guiStyle.contentOffset = Vector2.zero;

                // 외곽선 효과 (그림자)
                _guiStyle.normal.background = MakeTexture(2, 2, new Color(0, 0, 0, 0.5f));
                _styleInitialized = true;
            }

            // 색상 업데이트 (레벨 변경 대비)
            _guiStyle.normal.textColor = _labelColor;

            // 레이블 크기 계산
            GUIContent content = new GUIContent(_levelText);
            Vector2 size = _guiStyle.CalcSize(content);

            // 스크린 좌표 변환 (GUI는 y축 반전)
            Rect labelRect = new Rect(
                screenPos.x - size.x / 2f,
                Screen.height - screenPos.y - size.y / 2f,
                size.x,
                size.y
            );

            // 배경 그리기 (반투명 검정)
            Rect bgRect = new Rect(labelRect.x - 4, labelRect.y - 2, labelRect.width + 8, labelRect.height + 4);
            GUI.color = new Color(0, 0, 0, 0.5f);
            GUI.Box(bgRect, "");
            GUI.color = Color.white;

            // 텍스트 그리기
            GUI.Label(labelRect, content, _guiStyle);
        }

        /// <summary>
        /// 단색 텍스처 생성 (GUI 배경용)
        /// </summary>
        private Texture2D MakeTexture(int width, int height, Color color)
        {
            Texture2D tex = new Texture2D(width, height);
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                    tex.SetPixel(x, y, color);
            tex.Apply();
            return tex;
        }

        private void OnDrawGizmosSelected()
        {
            // 거리 컬링 범위 시각화
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position + _labelOffset, _maxDisplayDistance);

            // 레이블 위치 마커
            Gizmos.color = Color.white;
            Gizmos.DrawSphere(transform.position + _labelOffset, 0.1f);
        }
    }
}