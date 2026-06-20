using ProjectName.Core;
using UnityEngine;
using System.Collections;

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

        [Header("표시 설정")]
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _fadeDuration = 0.5f;

        private string _currentMessage = "";
        private string _currentItemName = "";
        private string _currentGradeText = "";
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

            // 환효 효과음 (간단한 로그)
            Debug.Log($"[CraftResult] ✅ 성공: {_currentMessage}");
        }

        /// <summary>제작 실패 결과창 표시</summary>
        /// <param name="failType">0=재료보존, 1=재료소멸, 2=전소</param>
        public void ShowFailure(int failType)
        {
            _isSuccess = false;
            _isShowing = true;
            _showTimer = _displayDuration;
            _fadeProgress = 1f;

            string failMsg;
            switch (failType)
            {
                case 0:
                    failMsg = "재료가 보존되었습니다.";
                    _currentGradeColor = new Color(0.8f, 0.6f, 0.2f); // 노란색
                    break;
                case 1:
                    failMsg = "재료 일부가 소멸되었습니다.";
                    _currentGradeColor = new Color(0.8f, 0.3f, 0.2f); // 주황색
                    break;
                case 2:
                default:
                    failMsg = "재료가 모두 전소되었습니다!";
                    _currentGradeColor = new Color(0.8f, 0.1f, 0.1f); // 빨간색
                    break;
            }

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

            // 타이틀
            GUI.color = new Color(1f, 1f, 1f, _fadeProgress);
            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperCenter,
                normal = { textColor = Color.white }
            };

            string title = _isSuccess ? "🎉 제작 완료!" : "❌ 제작 실패";
            GUI.Label(new Rect(boxX, boxY + 8, boxW, 30), title, titleStyle);

            // 아이템명 + 등급
            if (_isSuccess && !string.IsNullOrEmpty(_currentItemName))
            {
                GUI.color = _currentGradeColor;
                var gradeStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = _currentGradeColor }
                };
                GUI.Label(new Rect(boxX, boxY + 42, boxW, 24), $"{_currentItemName}", gradeStyle);

                GUI.color = new Color(_currentGradeColor.r, _currentGradeColor.g, _currentGradeColor.b, 0.8f);
                GUI.Label(new Rect(boxX, boxY + 68, boxW, 20), $"{_currentGradeText} 등급", gradeStyle);
            }
            else if (!_isSuccess)
            {
                GUI.color = _currentGradeColor;
                var failStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = _currentGradeColor }
                };
                GUI.Label(new Rect(boxX, boxY + 50, boxW, 50), _currentMessage.Replace("❌ 제작 실패!\n", ""), failStyle);
            }

            GUI.color = Color.white;
        }

        /// <summary>현재 표시 중인가?</summary>
        public bool IsShowing => _isShowing;
    }
}