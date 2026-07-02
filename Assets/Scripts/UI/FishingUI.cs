using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// 낚시 미니게임 UI (IMGUI). FishingSystem 상태를 읽어 프로그레스바와
    /// 스위트스팟, 이동 핀을 표시하고 Space/ESC 입력을 처리합니다.
    /// </summary>
    public class FishingUI : MonoBehaviour
    {
        // 싱글톤
        public static FishingUI Instance { get; private set; }

        // 스타일 캐시
        private GUIStyle _stylePopup;
        private GUIStyle _styleHint;
        private Texture2D _whiteTex;

        // 상태
        private bool _stylesInitialized;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_whiteTex != null)
                Destroy(_whiteTex);
        }

        private void Update()
        {
            if (FishingSystem.Instance == null) return;

            // 미니게임 활성화 시 입력 처리
            if (FishingSystem.Instance.IsMinigameActive)
            {
                // 스페이스바: 잡기 시도
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    FishingSystem.Instance.TryCatch();
                }

                // ESC: 낚시 취소
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    FishingSystem.Instance.CancelFishing();
                }
            }
        }

        private void OnGUI()
        {
            if (FishingSystem.Instance == null) return;

            EnsureStyles();

            var fs = FishingSystem.Instance;

            // ===== 팝업 메시지 표시 (성공/실패) =====
            if (fs.PopupTimer > 0f && !string.IsNullOrEmpty(fs.PopupMessage))
            {
                DrawPopup(fs.PopupMessage);
            }

            // ===== 미니게임 활성화 시에만 프로그레스바 렌더링 =====
            if (!fs.IsMinigameActive) return;

            DrawMinigame(fs);
        }

        /// <summary>중앙 하단 프로그레스바 + 스위트스팟 + 이동 핀</summary>
        private void DrawMinigame(FishingSystem fs)
        {
            float barWidth = fs.ProgressBarWidth; // 300px
            float barHeight = 30f;
            float barX = (Screen.width - barWidth) / 2f;
            float barY = Screen.height - 80f;

            // ===== 1. 배경 (회색) =====
            var prevColor = GUI.color;
            GUI.color = Color.gray;
            GUI.DrawTexture(new Rect(barX, barY, barWidth, barHeight), _whiteTex);
            GUI.color = prevColor;

            // ===== 2. 스위트스팟 (초록 구간 30px) =====
            float sweetX = barX + fs.SweetSpotStart;
            GUI.color = new Color(0f, 0.8f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(sweetX, barY, fs.SweetSpotWidth, barHeight), _whiteTex);
            GUI.color = prevColor;

            // ===== 3. 이동 핀 (빨간색, 4px) =====
            float pinX = barX + fs.PinPosition - 2f; // 4px 너비
            GUI.color = Color.red;
            GUI.DrawTexture(new Rect(pinX, barY - 2f, 4f, barHeight + 4f), _whiteTex);
            GUI.color = prevColor;

            // ===== 4. 조작 안내 =====
            float hintY = barY + barHeight + 5f;
            GUI.Label(new Rect(barX, hintY, barWidth, 20f), "␣ 스페이스바: 잡기 | ESC: 취소", _styleHint);
        }

        /// <summary>중앙 팝업 메시지 (3초)</summary>
        private void DrawPopup(string message)
        {
            float popupWidth = 400f;
            float popupHeight = 50f;
            float popupX = (Screen.width - popupWidth) / 2f;
            float popupY = Screen.height / 2f - 100f;

            // 배경
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.7f);
            GUI.DrawTexture(new Rect(popupX, popupY, popupWidth, popupHeight), _whiteTex);
            GUI.color = prevColor;

            // 텍스트
            GUI.Label(new Rect(popupX + 10f, popupY + 5f, popupWidth - 20f, popupHeight - 10f),
                message, _stylePopup);
        }

        private void EnsureStyles()
        {
            if (_stylesInitialized) return;
            _stylesInitialized = true;

            // 더미 흰색 텍스처
            _whiteTex = new Texture2D(1, 1);
            _whiteTex.SetPixel(0, 0, Color.white);
            _whiteTex.Apply();

            // 팝업 스타일
            _stylePopup = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            // 조작 안내 스타일
            _styleHint = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
        }
    }
}