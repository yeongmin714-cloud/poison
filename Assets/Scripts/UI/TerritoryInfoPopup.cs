using UnityEngine;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 44: 영지 상세 정보 팝업 — IMGUI 기반 싱글톤.
    /// 영지 우클릭 → ℹ️ 영지 정보 선택 시 표시됩니다.
    /// ESC로 닫거나 5초 후 자동으로 닫힙니다.
    /// </summary>
    public class TerritoryInfoPopup : MonoBehaviour
    {
        private static TerritoryInfoPopup _instance;
        public static TerritoryInfoPopup Instance => _instance;

        [Header("UI Layout")]
        [SerializeField] private float _windowWidth = 380f;
        [SerializeField] private float _windowHeight = 340f;
        [SerializeField] private float _padding = 15f;

        // 상태
        private static bool _isOpen = false;
        private static TerritoryId _targetTerritoryId;
        private static float _openTime = 0f;
        private const float AUTO_CLOSE_DELAY = 5f; // 5초 후 자동 닫힘

        // 캐시된 스타일
        private GUIStyle _titleStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _valueStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;
        private const float _rowSpacing = 2f;

        // ===== 생명주기 =====

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        private void Update()
        {
            // ESC 키 감지
            if (_isOpen && Input.GetKeyDown(KeyCode.Escape))
            {
                Hide();
            }

            // 5초 후 자동 닫힘
            if (_isOpen && Time.realtimeSinceStartup - _openTime >= AUTO_CLOSE_DELAY)
            {
                Hide();
            }
        }

        private void OnGUI()
        {
            if (!_isOpen) return;

            InitializeStyles();

            // 창 영역 계산 (화면 중앙)
            float sw = Screen.width;
            float sh = Screen.height;
            float wx = (sw - _windowWidth) * 0.5f;
            float wy = (sh - _windowHeight) * 0.5f;
            Rect windowRect = new Rect(wx, wy, _windowWidth, _windowHeight);

            // 배경
            Color origBg = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.10f, 0.10f, 0.16f);
            GUI.Box(windowRect, "");
            GUI.backgroundColor = origBg;

            // 테두리
            Color origColor = GUI.color;
            GUI.color = new Color(0.35f, 0.30f, 0.50f);
            GUI.Box(windowRect, "");
            GUI.color = origColor;

            // 내부 영역
            Rect innerRect = new Rect(
                windowRect.x + _padding,
                windowRect.y + _padding,
                windowRect.width - _padding * 2,
                windowRect.height - _padding * 2
            );

            // 데이터 로드
            var db = TerritoryDatabase.Instance;
            if (db == null)
            {
                GUI.Label(innerRect, "데이터베이스 없음", _labelStyle);
                DrawCloseButton(windowRect);
                return;
            }

            TerritoryDefinition def = db.GetDefinition(_targetTerritoryId);
            TerritoryState state = db.GetState(_targetTerritoryId);

            if (def.id.nation == NationType.None)
            {
                GUI.Label(innerRect, "영지 데이터 없음", _labelStyle);
                DrawCloseButton(windowRect);
                return;
            }

            // ---- 콘텐츠 그리기 ----

            // 자동 닫힘 타이머 표시 (우측 상단)
            float remaining = AUTO_CLOSE_DELAY - (Time.realtimeSinceStartup - _openTime);
            string timerText = $"자동 닫힘: {Mathf.Max(0, remaining):F1}초";
            Rect timerRect = new Rect(windowRect.x + windowRect.width - 110f, windowRect.y + 5f, 105f, 20f);
            GUIStyle timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            GUI.Label(timerRect, timerText, timerStyle);

            float y = innerRect.y;
            float labelW = 80f;
            float valueX = innerRect.x + labelW;
            float valueW = innerRect.width - labelW;
            float rowHeight = 24f;

            // ---- 타이틀 ----
            string titleText = GetTerritoryTitle(def);
            Rect titleRect = new Rect(innerRect.x, y, innerRect.width, 30f);
            GUI.Label(titleRect, titleText, _titleStyle);
            y = titleRect.y + titleRect.height + 6f;

            // ---- 정보 행들 ----
            // 국가
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "국가:", GetNationDisplay(def.nation));

            // 난이도
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "난이도:", GetDifficultyDisplay(def.difficulty));

            // 소유주
            string ownerText = GetOwnerDisplay(def, state);
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "소유주:", ownerText);

            // 병사
            string guardText = GetGuardDisplay(def, state);
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "병사:", guardText);

            // 영주
            string lordText = $"{def.lord.lordName}  (입맛: {def.lord.preferredFood})";
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "영주:", lordText);

            // 지병
            string diseaseText = string.IsNullOrEmpty(def.lord.chronicDisease) ? "없음" : def.lord.chronicDisease;
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "지병:", diseaseText);

            // 상태
            string statusText = GetStatusDisplay(def, state);
            DrawInfoRow(ref y, innerRect.x, valueX, valueW, rowHeight, "상태:", statusText);

            // ---- 버튼 영역 ----
            y += 8f;
            float btnHeight = 28f;
            float btnSpacing = 6f;
            float btnWidth = (innerRect.width - btnSpacing * 2) / 3f;

            // 자동 이동
            Color origBtnColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0.4f, 0f);
            Rect autoMoveBtn = new Rect(innerRect.x, y, btnWidth, btnHeight);
            if (GUI.Button(autoMoveBtn, "📍 자동 이동"))
            {
                Vector3 worldPos = new Vector3(
                    def.id.index * 10f,
                    0f,
                    (int)def.nation * 10f
                );
                // MapWindow가 열려있으면 자동 이동 시작
                var mapWindow = Object.FindAnyObjectByType<MapWindow>();
                if (mapWindow != null)
                {
                    // MapWindow.StartAutoMoveToTerritory는 private이므로
                    // AutoMoveManager 직접 호출
                    if (AutoMoveManager.Instance != null)
                    {
                        Hide();
                        AutoMoveManager.Instance.SetDestination(worldPos);
                        Debug.Log($"[TerritoryInfoPopup] 🚶 자동 이동 시작 → {def.territoryName}");
                    }
                }
                else
                {
                    Debug.LogWarning("[TerritoryInfoPopup] MapWindow가 열려있지 않습니다.");
                }
                Hide();
            }
            GUI.backgroundColor = origBtnColor;

            // 빠른 이동 (소유한 경우만)
            bool isOwned = state != null && state.ownership == TerritoryOwnership.PlayerOwned;
            GUI.backgroundColor = isOwned ? new Color(0.5f, 0.3f, 0.0f) : new Color(0.2f, 0.2f, 0.2f);
            GUI.enabled = isOwned;
            Rect ftBtn = new Rect(innerRect.x + btnWidth + btnSpacing, y, btnWidth, btnHeight);
            if (GUI.Button(ftBtn, "⚡ 빠른 이동"))
            {
                if (isOwned)
                {
                    FastTravelUI.Hide();
                    FastTravelUI.Show();
                }
                Hide();
            }
            GUI.enabled = true;
            GUI.backgroundColor = origBtnColor;

            // 닫기
            GUI.backgroundColor = new Color(0.4f, 0.1f, 0.1f);
            Rect closeBtn = new Rect(innerRect.x + (btnWidth + btnSpacing) * 2, y, btnWidth, btnHeight);
            if (GUI.Button(closeBtn, "닫기"))
            {
                Hide();
            }
            GUI.backgroundColor = origBtnColor;
        }

        // ===== 헬퍼 메서드 =====

        private void DrawInfoRow(ref float y, float x, float valueX, float valueW, float rowHeight, string label, string value)
        {
            Rect labelRect = new Rect(x, y, valueX - x - 4f, rowHeight);
            GUI.Label(labelRect, label, _labelStyle);

            Rect valueRect = new Rect(valueX, y, valueW, rowHeight);
            GUI.Label(valueRect, value, _valueStyle);

            y += rowHeight + _rowSpacing;
        }

        private void DrawCloseButton(Rect windowRect)
        {
            float btnY = windowRect.y + windowRect.height - 30f - _padding;
            Rect closeBtn = new Rect(windowRect.x + (windowRect.width - 80f) * 0.5f, btnY, 80f, 30f);
            if (GUI.Button(closeBtn, "닫기"))
            {
                Hide();
            }
        }

        private static string GetTerritoryTitle(TerritoryDefinition def)
        {
            string prefix = def.nation switch
            {
                NationType.East => "동",
                NationType.West => "서",
                NationType.South => "남",
                NationType.North => "북",
                NationType.Empire => "황제국",
                NationType.Dracula => "드라큘라",
                _ => "?"
            };
            return $"┌─── [{prefix}] {def.territoryName} ───────┐";
        }

        private static string GetNationDisplay(NationType nation)
        {
            return nation switch
            {
                NationType.East => "🏁 동 (East)",
                NationType.West => "🏁 서 (West)",
                NationType.South => "🏁 남 (South)",
                NationType.North => "🏁 북 (North)",
                NationType.Empire => "👑 황제국 (Empire)",
                NationType.Dracula => "🧛 드라큘라",
                _ => "알 수 없음"
            };
        }

        private static string GetDifficultyDisplay(TerritoryDifficulty difficulty)
        {
            return difficulty switch
            {
                TerritoryDifficulty.Ring1 => "⭐ (Ring 1)",
                TerritoryDifficulty.Ring2 => "⭐⭐ (Ring 2)",
                TerritoryDifficulty.Ring3 => "⭐⭐⭐ (Ring 3)",
                TerritoryDifficulty.Ring4 => "⭐⭐⭐⭐ (Ring 4)",
                TerritoryDifficulty.Empire => "👑 (Empire)",
                _ => "알 수 없음"
            };
        }

        private static string GetOwnerDisplay(TerritoryDefinition def, TerritoryState state)
        {
            if (state == null)
                return "미점령";

            return state.ownership switch
            {
                TerritoryOwnership.Unoccupied => "미점령",
                TerritoryOwnership.PlayerOwned => "👤 플레이어",
                TerritoryOwnership.LordOwned => $"🔴 {def.lord.lordName}",
                TerritoryOwnership.Contested => "⚔️ 전쟁 중",
                _ => "알 수 없음"
            };
        }

        private static string GetGuardDisplay(TerritoryDefinition def, TerritoryState state)
        {
            int baseCount = def.guardCount;
            if (state == null)
                return $"{baseCount}명";

            float aliveRatio = state.guardAliveRatio;
            int alive = Mathf.Max(0, Mathf.RoundToInt(baseCount * aliveRatio));

            // 레벨 추정 (병사 수 기반)
            string levelRange = def.difficulty switch
            {
                TerritoryDifficulty.Ring1 => "Lv.1~10",
                TerritoryDifficulty.Ring2 => "Lv.11~20",
                TerritoryDifficulty.Ring3 => "Lv.21~30",
                TerritoryDifficulty.Ring4 => "Lv.31~40",
                TerritoryDifficulty.Empire => "Lv.50",
                _ => "Lv.?"
            };

            if (aliveRatio < 1f)
                return $"{alive}~{baseCount}명 ({levelRange})";
            else
                return $"{baseCount}명 ({levelRange})";
        }

        private static string GetStatusDisplay(TerritoryDefinition def, TerritoryState state)
        {
            if (state == null)
                return "평화";

            // 전쟁 상태
            if (state.isUnderAttack)
                return "⚔️ 전쟁 중";

            if (state.ownership == TerritoryOwnership.Contested)
                return "⚔️ 분쟁 중";

            // 축제 상태
            if (FestivalManager.Instance != null)
            {
                var festival = FestivalManager.Instance.GetActiveFestivalAtTerritory(def.id);
                if (festival != null)
                    return $"🎪 축제 중 ({festival.festivalName})";
            }

            // 점령 후 충성도 상태
            if (state.ownership == TerritoryOwnership.PlayerOwned)
            {
                float loyalty = state.loyaltyToPlayer;
                if (loyalty < 30f)
                    return "⚠️ 불안정 (충성도 낮음)";
                if (loyalty < 70f)
                    return "🔶 보통 (충성도 중간)";
                return "✅ 안정 (충성도 높음)";
            }

            return "평화";
        }

        /// <summary>
        /// GUI 스타일 초기화
        /// </summary>
        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.85f, 0.70f, 0.20f) } // 금색
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(0.7f, 0.7f, 0.9f) }
            };

            _valueStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = Color.white },
                richText = true,
                wordWrap = true
            };

            _stylesInitialized = true;
        }

        // ===== 공개 정적 메서드 =====

        /// <summary>
        /// 영지 상세 정보 팝업을 엽니다.
        /// </summary>
        public static void Show(TerritoryId territoryId)
        {
            if (_instance == null)
            {
                Debug.LogError("[TerritoryInfoPopup] TerritoryInfoPopup 인스턴스가 없습니다! Scene에 TerritoryInfoPopup을 추가해주세요.");
                return;
            }

            if (_isOpen) return;

            _targetTerritoryId = territoryId;
            _isOpen = true;
            _openTime = Time.realtimeSinceStartup;

            Debug.Log($"[TerritoryInfoPopup] ℹ️ 영지 정보 열림: {territoryId}");
        }

        /// <summary>
        /// 영지 상세 정보 팝업을 닫습니다.
        /// </summary>
        public static void Hide()
        {
            if (!_isOpen) return;
            _isOpen = false;
            Debug.Log("[TerritoryInfoPopup] 영지 정보 팝업 닫힘");
        }

        /// <summary>
        /// 팝업이 열려있는지 확인합니다.
        /// </summary>
        public static bool IsOpen => _isOpen;

        /// <summary>
        /// 현재 표시 중인 영지 ID
        /// </summary>
        public static TerritoryId CurrentTerritoryId => _targetTerritoryId;
    }
}