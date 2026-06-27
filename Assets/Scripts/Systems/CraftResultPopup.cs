using ProjectName.Core;
using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// CR-01~07: 제작 성공/실패 결과창 시스템.
    /// 조합 성공 시 환호 애니메이션 + 아이템 결과창 표시.
    /// 실패 시 재료 소실 결과 표시.
    /// </summary>
    public class CraftResultPopup : MonoBehaviour
    {
        public static CraftResultPopup Instance { get; private set; }

        /// <summary>실패 유형 열거형 — 매직 넘버 대체</summary>
        public enum CraftFailType
        {
            MaterialsPreserved = 0, // 재료보존
            MaterialsPartiallyLost = 1, // 재료일부소멸
            MaterialsCompletelyLost = 2, // 전소
        }

        [Header("표시 설정")]
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _fadeDuration = 0.5f;

        // ── 캐시된 IMGUI 스타일 (OnPopupGUI에서 매프레임 생성 방지) ──
        private GUIStyle _titleStyle;
        private GUIStyle _gradeStyle;
        private GUIStyle _failStyle;

        private string _currentMessage = "";
        private string _currentItemName = "";
        private string _currentGradeText = "";
        private string _failureDetailMessage = ""; // 실패 시 상세 메시지 (Replace 제거용)
        private Color _currentGradeColor = Color.gray;
        private float _showTimer = 0f;
        private float _fadeProgress = 1f;
        private bool _isShowing = false;
        private bool _isSuccess = false;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        /// <summary>제작 성공 결과창 표시</summary>
        public void ShowSuccess(string itemName, ItemRarity rarity, string effectDescription)
        {
            _isSuccess = true;
            _currentItemName = itemName;
            _isShowing = true;
            _showTimer = _displayDuration;
            _fadeProgress = 1f;

            // 등급 텍스트 및 색상
            _currentGradeColor = EquipmentRarityData.GetRarityColor(rarity);
            _currentGradeText = EquipmentRarityData.GetRarityDisplayName(rarity);

            _currentMessage = $"🎉 {itemName} 제작 성공!\n{_currentGradeText} 등급\n{effectDescription}";

            // 환호 효과음 (간단한 로그)
            Debug.Log($"[CraftResult] ✅ 성공: {_currentMessage}");
        }

        /// <summary>제작 실패 결과창 표시</summary>
        /// <param name="failType">실패 유형 (CraftFailType 열거형)</param>
        public void ShowFailure(CraftFailType failType)
        {
            _isSuccess = false;
            _isShowing = true;
            _showTimer = _displayDuration;
            _fadeProgress = 1f;

            string failMsg;
            switch (failType)
            {
                case CraftFailType.MaterialsPreserved:
                    failMsg = "재료가 보존되었습니다.";
                    _currentGradeColor = new Color(0.8f, 0.6f, 0.2f); // 노란색
                    break;
                case CraftFailType.MaterialsPartiallyLost:
                    failMsg = "재료 일부가 소멸되었습니다.";
                    _currentGradeColor = new Color(0.8f, 0.3f, 0.2f); // 주황색
                    break;
                case CraftFailType.MaterialsCompletelyLost:
                default:
                    failMsg = "재료가 모두 전소되었습니다!";
                    _currentGradeColor = new Color(0.8f, 0.1f, 0.1f); // 빨간색
                    break;
            }

            _failureDetailMessage = failMsg;
            _currentMessage = $"❌ 제작 실패!\n{failMsg}";
            _currentItemName = "";
            _currentGradeText = "";

            Debug.Log($"[CraftResult] ❌ 실패: {_currentMessage}");
        }

        private void Update()
        {
            if (!_isShowing) return;

            _showTimer -= Time.deltaTime;

            if (_showTimer <= _fadeDuration)
            {
                _fadeProgress = Mathf.Max(0f, _showTimer / _fadeDuration);
            }

            if (_showTimer <= 0f)
            {
                _isShowing = false;
                _fadeProgress = 1f;
            }
        }

        /// <summary>IMGUI로 결과창 렌더링 (UIManager 또는 OnGUI에서 호출)</summary>
        public void OnPopupGUI()
        {
            if (!_isShowing) return;

            float screenW = Screen.width;
            float screenH = Screen.height;

            // 배경 박스
            float boxW = 400f;
            float boxH = 180f;
            float boxX = (screenW - boxW) / 2f;
            float boxY = screenH * 0.6f; // 화면 중하단

            Color bgColor = _isSuccess
                ? new Color(0.1f, 0.15f, 0.1f, 0.9f * _fadeProgress)  // 어두운 초록
                : new Color(0.15f, 0.08f, 0.08f, 0.9f * _fadeProgress); // 어두운 빨강

            GUI.color = bgColor;
            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "");

            // ── 캐시된 스타일 초기화 (최초 1회) ──
            if (_titleStyle == null)
            {
                _titleStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.UpperCenter,
                    normal = { textColor = Color.white }
                };

                _gradeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };

                _failStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }

            // 타이틀
            GUI.color = new Color(1f, 1f, 1f, _fadeProgress);
            string title = _isSuccess ? "🎉 제작 완료!" : "❌ 제작 실패";
            GUI.Label(new Rect(boxX, boxY + 8, boxW, 30), title, _titleStyle);

            // 아이템명 + 등급
            if (_isSuccess && !string.IsNullOrEmpty(_currentItemName))
            {
                _gradeStyle.normal.textColor = _currentGradeColor;
                GUI.color = _currentGradeColor;
                GUI.Label(new Rect(boxX, boxY + 42, boxW, 24), _currentItemName, _gradeStyle);

                _gradeStyle.normal.textColor = new Color(_currentGradeColor.r, _currentGradeColor.g, _currentGradeColor.b, 0.8f);
                GUI.Label(new Rect(boxX, boxY + 68, boxW, 20), $"{_currentGradeText} 등급", _gradeStyle);
            }
            else if (!_isSuccess)
            {
                _failStyle.normal.textColor = _currentGradeColor;
                GUI.color = _currentGradeColor;
                GUI.Label(new Rect(boxX, boxY + 50, boxW, 50), _failureDetailMessage, _failStyle);
            }

            GUI.color = Color.white;
        }

        /// <summary>현재 표시 중인가?</summary>
        public bool IsShowing => _isShowing;
    }
}