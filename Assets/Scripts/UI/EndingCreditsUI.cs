using UnityEngine;
using ProjectName.Systems;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// 🌅 엔딩 크레딧 UI — IMGUI 풀스크린 오버레이.
    /// 4개 Phase로 구성: 엔딩 텍스트 → 크레딧 스크롤 → 통계 요약 → 선택지.
    /// ESC로 바로 Phase 4로 스킵 가능.
    /// </summary>
    public class EndingCreditsUI : MonoBehaviour
    {
        public static EndingCreditsUI Instance { get; private set; }

        // ===== Phase 열거형 =====
        public enum EndingPhase
        {
            None,           // 비활성
            EndingText,     // Phase 1: 왕좌에 앉는 스토리 설명 (5초)
            CreditsScroll,  // Phase 2: 제작진 명단 스크롤 (15초)
            StatsSummary,   // Phase 3: 통계 요약
            Choice          // Phase 4: 뉴게임+ / 메인 메뉴
        }

        // ===== 상태 =====
        private EndingPhase _currentPhase = EndingPhase.None;
        private float _phaseTimer;
        private float _creditsScrollOffset;
        private bool _isActive;

        // ===== 타이밍 상수 =====
        private const float ENDING_TEXT_DURATION = 5f;
        private const float CREDITS_SCROLL_DURATION = 15f;
        private const float CREDITS_TOTAL_HEIGHT = 1200f;
        private const float STATS_DURATION = 5f;
        private const float SCROLL_SPEED = CREDITS_TOTAL_HEIGHT / CREDITS_SCROLL_DURATION;

        // ===== 스타일 =====
        private GUIStyle _bgStyle;
        private GUIStyle _titleStyle;
        private GUIStyle _textStyle;
        private GUIStyle _creditsTitleStyle;
        private GUIStyle _creditsTextStyle;
        private GUIStyle _statsTitleStyle;
        private GUIStyle _statsTextStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _hintStyle;
        private Texture2D _bgTexture;
        private bool _stylesInitialized;

        // ===== 콜백 =====
        public System.Action OnNewGamePlusClicked;
        public System.Action OnMainMenuClicked;

        // ===== 크레딧 텍스트 (하드코딩) =====
        private static readonly string[] CreditsLines = new string[]
        {
            "<size=32><b>KOREA 1420</b></size>",
            "",
            "<size=24><b>— 제작진 —</b></size>",
            "",
            "<size=20>기획 및 디자인</size>",
            "<size=18>Nous Research Team</size>",
            "",
            "<size=20>프로그래밍</size>",
            "<size=18>Hermes Agent AI</size>",
            "",
            "<size=20>게임 디자인</size>",
            "<size=18>Project Hermes</size>",
            "",
            "<size=20>레벨 디자인</size>",
            "<size=18>AI Generative Design</size>",
            "",
            "<size=24><b>— 특별 감사 —</b></size>",
            "",
            "<size=18>모든 테스터분들께 감사드립니다</size>",
            "<size=18>피드백을 주신 모든 분들께 감사드립니다</size>",
            "",
            "<size=24><b>— 에셋 출처 —</b></size>",
            "",
            "<size=18>Unity Asset Store</size>",
            "<size=18>Open Source Libraries</size>",
            "<size=18>CC0 License Assets</size>",
            "",
            "",
            "<size=28><b>감사합니다!</b></size>",
            "<size=18>끝까지 플레이해 주셔서 감사합니다.</size>",
        };

        // ===== 엔딩 텍스트 =====
        private static readonly string[] EndingTextLines = new string[]
        {
            "",
            "<size=36><b>👑 왕좌에 오르다</b></size>",
            "",
            "<size=22>드디어 모든 영지를 통일하였습니다.</size>",
            "<size=22>피와 땀으로 일군 조선의 새로운 시대가 열립니다.</size>",
            "",
            "<size=22>당신은 황제국의 왕좌에 앉아</size>",
            "<size=22>모든 백성의 환호를 받습니다.</size>",
            "",
            "<size=22>수많은 전투와 음모, 그리고 동맹을 거쳐</size>",
            "<size=22>마침내 진정한 왕이 되었습니다.</size>",
            "",
            "<size=20><i>\"한반도의 새로운 역사가 지금부터 시작된다.\"</i></size>",
        };

        // ===== 생명주기 =====

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            gameObject.SetActive(false); // 처음에는 비활성
        }

        private void OnDestroy()
        {
            if (_bgTexture != null)
                Destroy(_bgTexture);
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _bgTexture = new Texture2D(1, 1);
            _bgTexture.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.92f));
            _bgTexture.Apply();

            _bgStyle = new GUIStyle { normal = { background = _bgTexture } };

            _titleStyle = new GUIStyle
            {
                fontSize = 40,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.7f, 0.3f, 1f) }
            };

            _textStyle = new GUIStyle
            {
                fontSize = 22,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.85f, 0.8f, 1f) },
                richText = true
            };

            _creditsTitleStyle = new GUIStyle
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.7f, 0.3f, 1f) }
            };

            _creditsTextStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f, 1f) },
                richText = true
            };

            _statsTitleStyle = new GUIStyle
            {
                fontSize = 32,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.9f, 0.7f, 0.3f, 1f) }
            };

            _statsTextStyle = new GUIStyle
            {
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.85f, 0.85f, 0.8f, 1f) },
                richText = true
            };

            _buttonStyle = new GUIStyle
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            _hintStyle = new GUIStyle
            {
                fontSize = 14,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f, 0.8f) }
            };

            _stylesInitialized = true;
        }

        // ===== 공개 메서드 =====

        /// <summary>
        /// 엔딩 시퀀스를 시작합니다.
        /// </summary>
        public void StartEndingSequence()
        {
            _isActive = true;
            _currentPhase = EndingPhase.EndingText;
            _phaseTimer = 0f;
            _creditsScrollOffset = 0f;
            gameObject.SetActive(true);
            Debug.Log("[EndingCreditsUI] 🌅 엔딩 시퀀스 시작");
        }

        /// <summary>
        /// 현재 Phase 반환
        /// </summary>
        public EndingPhase CurrentPhase => _currentPhase;

        /// <summary>
        /// 활성 상태 여부
        /// </summary>
        public bool IsActive => _isActive;

        // ===== 업데이트 =====

        private void Update()
        {
            if (!_isActive) return;

            _phaseTimer += Time.unscaledDeltaTime;

            switch (_currentPhase)
            {
                case EndingPhase.EndingText:
                    if (_phaseTimer >= ENDING_TEXT_DURATION)
                        TransitionToPhase(EndingPhase.CreditsScroll);
                    break;

                case EndingPhase.CreditsScroll:
                    _creditsScrollOffset += Time.unscaledDeltaTime * SCROLL_SPEED;
                    if (_phaseTimer >= CREDITS_SCROLL_DURATION)
                        TransitionToPhase(EndingPhase.StatsSummary);
                    break;

                case EndingPhase.StatsSummary:
                    if (_phaseTimer >= STATS_DURATION)
                        TransitionToPhase(EndingPhase.Choice);
                    break;

                case EndingPhase.Choice:
                    // 아무 것도 안 함 — 버튼 대기
                    break;
            }

            // ESC 스킵
            if (Input.GetKeyDown(KeyCode.Escape) && _currentPhase != EndingPhase.Choice)
            {
                TransitionToPhase(EndingPhase.Choice);
            }
        }

        private void TransitionToPhase(EndingPhase nextPhase)
        {
            _currentPhase = nextPhase;
            _phaseTimer = 0f;
            Debug.Log($"[EndingCreditsUI] Phase 전환: {nextPhase}");
        }

        // ===== OnGUI =====

        private void OnGUI()
        {
            if (!_isActive) return;
            InitializeStyles();

            // 풀스크린 배경
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", _bgStyle);

            switch (_currentPhase)
            {
                case EndingPhase.EndingText:
                    DrawEndingText();
                    break;
                case EndingPhase.CreditsScroll:
                    DrawCreditsScroll();
                    break;
                case EndingPhase.StatsSummary:
                    DrawStatsSummary();
                    break;
                case EndingPhase.Choice:
                    DrawChoiceScreen();
                    break;
            }

            // ESC 힌트 (Choice 제외)
            if (_currentPhase != EndingPhase.Choice)
            {
                GUI.Label(new Rect(10, Screen.height - 30, 200, 30), "ESC: 건너뛰기", _hintStyle);
            }
        }

        // ===== Phase 1: 엔딩 텍스트 =====

        private void DrawEndingText()
        {
            int centerX = Screen.width / 2;
            int centerY = Screen.height / 2 - 100;

            GUIStyle style = new GUIStyle(_textStyle);
            style.richText = true;

            float yOffset = 0;
            for (int i = 0; i < EndingTextLines.Length; i++)
            {
                float lineHeight = 36;
                GUI.Label(new Rect(0, centerY + yOffset, Screen.width, lineHeight), EndingTextLines[i], style);
                yOffset += lineHeight;
            }
        }

        // ===== Phase 2: 크레딧 스크롤 =====

        private void DrawCreditsScroll()
        {
            int centerX = Screen.width / 2;
            float totalHeight = CreditsLines.Length * 40f;
            float startY = Screen.height - _creditsScrollOffset;

            GUIStyle style = new GUIStyle(_creditsTextStyle);
            style.richText = true;
            style.alignment = TextAnchor.MiddleCenter;

            float yOffset = 0;
            for (int i = 0; i < CreditsLines.Length; i++)
            {
                float lineHeight = 40;
                GUI.Label(new Rect(0, startY + yOffset, Screen.width, lineHeight), CreditsLines[i], style);
                yOffset += lineHeight;
            }
        }

        // ===== Phase 3: 통계 요약 =====

        private void DrawStatsSummary()
        {
            int centerX = Screen.width / 2;
            int startY = Screen.height / 2 - 200;
            int labelWidth = 500;
            int labelX = centerX - labelWidth / 2;

            // 제목
            GUI.Label(new Rect(0, startY, Screen.width, 50), "📊 통계 요약", _statsTitleStyle);

            // 통계 데이터
            int y = startY + 70;
            int lineHeight = 32;

            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "⏱️ 플레이 시간", GameStatsCollector.FormatTime(GameStatsCollector.PlayTime));
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "🧟 처치한 몬스터", $"{GameStatsCollector.Kills:N0}");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "💀 사망 횟수", $"{GameStatsCollector.Deaths:N0}");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "💰 획득 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldEarned));
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "💰 사용 골드", GameStatsCollector.FormatGold(GameStatsCollector.GoldSpent));
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "🐴 이동 거리", GameStatsCollector.FormatDistance(GameStatsCollector.DistanceTraveled));
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "🐟 획득 물고기", $"{GameStatsCollector.FishCaught:N0}");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "🏟️ 아레나 전적", $"{GameStatsCollector.ArenaWins}승 {GameStatsCollector.ArenaLosses}패");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "⚔️ 전쟁 참여", $"{GameStatsCollector.WarParticipations:N0}회");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "🏆 완료 퀘스트", $"{GameStatsCollector.CompletedQuests:N0}");
            DrawStatLine(labelX, ref y, lineHeight, labelWidth, "👑 점령 영지", $"{GameStatsCollector.OwnedTerritories}/82");
        }

        private void DrawStatLine(int x, ref int y, int lineHeight, int labelWidth, string label, string value)
        {
            GUI.Label(new Rect(x, y, labelWidth - 150, lineHeight), label, _statsTextStyle);

            GUIStyle valueStyle = new GUIStyle(_statsTextStyle);
            valueStyle.alignment = TextAnchor.MiddleRight;
            GUI.Label(new Rect(x + labelWidth - 150, y, 150, lineHeight), value, valueStyle);

            y += lineHeight;
        }

        // ===== Phase 4: 선택지 =====

        private void DrawChoiceScreen()
        {
            int centerX = Screen.width / 2;
            int centerY = Screen.height / 2;

            int buttonWidth = 320;
            int buttonHeight = 70;
            int spacing = 20;

            int startX = centerX - buttonWidth / 2;
            int startY = centerY - buttonHeight - spacing / 2;

            // 제목
            GUI.Label(new Rect(0, centerY - 120, Screen.width, 50), "🎉 축하합니다!", _titleStyle);
            GUI.Label(new Rect(0, centerY - 70, Screen.width, 40), "모든 영지를 통일하고 왕좌에 올랐습니다.", _textStyle);

            // New Game+ 버튼
            GUI.backgroundColor = new Color(0.3f, 0.5f, 0.2f, 0.9f);
            if (GUI.Button(new Rect(startX, startY, buttonWidth, buttonHeight), "🔄 뉴게임+ 시작", _buttonStyle))
            {
                OnNewGamePlusClicked?.Invoke();
            }

            // 메인 메뉴 버튼
            GUI.backgroundColor = new Color(0.4f, 0.2f, 0.2f, 0.9f);
            if (GUI.Button(new Rect(startX, startY + buttonHeight + spacing, buttonWidth, buttonHeight), "🏠 메인 메뉴", _buttonStyle))
            {
                OnMainMenuClicked?.Invoke();
            }

            GUI.backgroundColor = Color.white;

            // 설명
            int descY = startY + (buttonHeight + spacing) * 2 + 20;
            GUI.Label(new Rect(0, descY, Screen.width, 60),
                "🔄 뉴게임+: 레벨, 골드, 스탯, 레시피, 업적을 유지한 채 새 게임을 시작합니다.\n적들의 레벨이 +5 상승하며, 경험치 보너스가 적용됩니다.",
                _hintStyle);
        }

        // ===== 종료 =====

        /// <summary>
        /// 엔딩 UI를 종료합니다.
        /// </summary>
        public void Close()
        {
            _isActive = false;
            _currentPhase = EndingPhase.None;
            _phaseTimer = 0f;
            _creditsScrollOffset = 0f;
            gameObject.SetActive(false);
            Debug.Log("[EndingCreditsUI] 엔딩 UI 종료");
        }
    }
}
