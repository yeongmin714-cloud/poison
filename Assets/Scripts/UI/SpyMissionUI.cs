using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using System.Linq;
using UnityEngine;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.9.3] 정보원 파견 UI (IMGUI)
    /// 
    /// 정보원 선택 → 임무 선택 (영주정보/병력정보/영지약도/방해공작)
    /// 미션 진행 중 상태 표시
    /// 완료 시 수집된 정보 UI 표시
    /// 발각 시 메시지: "정보원이 체포/처형되었습니다"
    /// 수집된 정보: 영주 선호음식/지병, 병력, 내부구조
    /// 
    /// ROADMAP L951-966:
    /// - 🔍 영주 정보 (Lv.5+)
    /// - 📋 병력 정보 (Lv.10+)
    /// - 🗺️ 영지 약도 (Lv.15+)
    /// - 💣 방해 공작 (Lv.20+)
    /// </summary>
    public class SpyMissionUI : MonoBehaviour
    {
        public static SpyMissionUI Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private KeyCode _openKey = KeyCode.E;
        [SerializeField] private float _interactRange = 5f;

        // ===== UI 상태 =====
        private bool _isVisible = false;
        private enum UIStep { SelectSpy, SelectMission, InProgress, Result, Detected }
        private UIStep _currentStep = UIStep.SelectSpy;
        private Vector2 _scrollPos;

        // ===== 선택 데이터 =====
        private GuardPlaceholder _selectedSpy;
        private SpySystem.SpyMission _selectedMission;
        private TerritoryId _currentTerritoryId;

        // ===== 진행/결과 =====
        private float _missionTimer;
        private float _missionDuration;
        private SpySystem.SpyResult _lastResult;
        private bool _missionCompleted;
        private float _resultTimer;

        // ===== 스타일 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleWarning;
        private GUIStyle _styleInfo;

        // ===== 캐시 =====
        private UIDesignTheme _theme;
        private static Texture2D _cachedTexWhite;
        private const float PANEL_WIDTH = 560f;
        private const float PANEL_HEIGHT = 600f;
        private const float LIST_ITEM_HEIGHT = 36f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _theme = Phase33_Themes.SpyTheme();
        }

        private void Update()
        {
            // E키로 열기/닫기 토글 (영지 내에서만)
            if (Input.GetKeyDown(_openKey))
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;

                TerritoryId? currentTerritory = GetCurrentTerritory(player.transform.position);
                if (currentTerritory == null)
                {
                    if (_isVisible) Close();
                    return;
                }

                _currentTerritoryId = currentTerritory.Value;

                if (!_isVisible)
                {
                    Open();
                }
                else
                {
                    Close();
                }
            }

            // 진행 중 타이머 업데이트
            if (_currentStep == UIStep.InProgress && !_missionCompleted)
            {
                _missionTimer -= Time.deltaTime;
                if (_missionTimer <= 0f)
                {
                    CompleteMission();
                }
            }

            // 결과 표시 타이머
            if ((_currentStep == UIStep.Result || _currentStep == UIStep.Detected))
            {
                _resultTimer -= Time.deltaTime;
                if (_resultTimer <= 0f)
                {
                    Close();
                }
            }
        }

        private void OnGUI()
        {
            if (!_isVisible) return;
            EnsureStyles();

            float x = (Screen.width - PANEL_WIDTH) / 2f;
            float y = (Screen.height - PANEL_HEIGHT) / 2f;

            // 배경
            Color bgColor = _theme != null ? _theme.BgColor : new Color(0.08f, 0.08f, 0.1f, 0.92f);
            Color borderColor = _theme != null ? _theme.BorderColor : new Color(0.3f, 0.8f, 0.3f, 0.85f);
            var oldGuiColor = GUI.color;

            // 바탕
            GUI.color = bgColor;
            GUI.Box(new Rect(x, y, PANEL_WIDTH, PANEL_HEIGHT), "");
            // 테두리 (2px 두께)
            GUI.color = borderColor;
            const float bw = 2f;
            GUI.Box(new Rect(x, y, PANEL_WIDTH, bw), "");                          // 상단
            GUI.Box(new Rect(x, y + PANEL_HEIGHT - bw, PANEL_WIDTH, bw), "");      // 하단
            GUI.Box(new Rect(x, y, bw, PANEL_HEIGHT), "");                        // 좌측
            GUI.Box(new Rect(x + PANEL_WIDTH - bw, y, bw, PANEL_HEIGHT), "");     // 우측
            GUI.color = oldGuiColor;

            // 닫기 버튼
            if (GUI.Button(new Rect(x + PANEL_WIDTH - 30, y + 5, 24, 24), "X"))
            {
                Close();
                return;
            }

            float cy = y + 15f;

            switch (_currentStep)
            {
                case UIStep.SelectSpy:
                    DrawSpySelection(x, ref cy);
                    break;
                case UIStep.SelectMission:
                    DrawMissionSelection(x, ref cy);
                    break;
                case UIStep.InProgress:
                    DrawInProgress(x, ref cy);
                    break;
                case UIStep.Result:
                    DrawResult(x, ref cy);
                    break;
                case UIStep.Detected:
                    DrawDetected(x, ref cy);
                    break;
            }
        }

        // ================================================================
        // 정보원 선택 화면
        // ================================================================
        private void DrawSpySelection(float x, ref float cy)
        {
            string territoryName = GetTerritoryName(_currentTerritoryId);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"🕵️ {territoryName} — 정보원 파견", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "파견할 정보원을 선택하세요 (Lv.5+ 필요)", _styleLabel);
            cy += 26f;

            var availableSpies = GetAvailableSpiesForUI();
            float listY = cy;
            float listH = PANEL_HEIGHT - (cy - 0f) - 70f;

            GUI.BeginGroup(new Rect(x + 10, listY, PANEL_WIDTH - 20, listH));

            if (availableSpies.Count == 0)
            {
                GUI.Label(new Rect(10, 10, PANEL_WIDTH - 40, 24),
                    "⚠️ 파견 가능한 정보원이 없습니다.\nLv.5 이상 포섭된 병사가 필요합니다.", _styleWarning);

                // 뒤로가기
                float btnY2 = listH - 40f;
                if (GUI.Button(new Rect(PANEL_WIDTH / 2 - 60, btnY2, 180, 30), "← 뒤로"))
                {
                    Close();
                }
            }
            else
            {
                float viewH = availableSpies.Count * LIST_ITEM_HEIGHT;
                _scrollPos = GUI.BeginScrollView(
                    new Rect(0, 0, PANEL_WIDTH - 20, listH),
                    _scrollPos,
                    new Rect(0, 0, PANEL_WIDTH - 40, viewH)
                );

                for (int i = 0; i < availableSpies.Count; i++)
                {
                    var guard = availableSpies[i];
                    float iy = i * LIST_ITEM_HEIGHT;
                    GUI.Box(new Rect(0, iy, PANEL_WIDTH - 40, LIST_ITEM_HEIGHT - 2), "");

                    string roleStr = GuardStatusSystem.GetRoleName(guard.Role);
                    GUI.Label(new Rect(10, iy + 2, 225, 20),
                        $"{guard.GuardName} (Lv.{guard.Level})", _styleLabel);
                    GUI.Label(new Rect(10, iy + 20, 180, 16),
                        $"{roleStr} | 호감도 {guard.Loyalty:F0}", _styleValue);

                    if (GUI.Button(new Rect(PANEL_WIDTH - 170, iy + 4, 165, 28), "정보원 선택"))
                    {
                        _selectedSpy = guard;
                        _currentStep = UIStep.SelectMission;
                        _scrollPos = Vector2.zero;
                        return;
                    }
                }

                GUI.EndScrollView();
            }

            GUI.EndGroup();
        }

        // ================================================================
        // 임무 선택 화면 — ROADMAP 4종 임무
        // ================================================================
        private void DrawMissionSelection(float x, ref float cy)
        {
            if (_selectedSpy == null)
            {
                _currentStep = UIStep.SelectSpy;
                return;
            }

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"🎯 임무 선택 — {_selectedSpy.GuardName}", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "수행할 정보 수집 임무를 선택하세요", _styleLabel);
            cy += 30f;

            // ROADMAP 4종 임무
            var missions = new (SpySystem.SpyMission mission, int requiredLevel, string name, string desc, float duration, string icon)[]
            {
                (SpySystem.SpyMission.LordInfo, SpySystem.LORDINFO_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.LordInfo),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.LordInfo),
                    SpySystem.LORDINFO_DURATION, "🔍"),
                (SpySystem.SpyMission.TroopInfo, SpySystem.TROOPINFO_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.TroopInfo),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.TroopInfo),
                    SpySystem.TROOPINFO_DURATION, "📋"),
                (SpySystem.SpyMission.TerritoryMap, SpySystem.TERRITORYMAP_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.TerritoryMap),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.TerritoryMap),
                    SpySystem.TERRITORYMAP_DURATION, "🗺️"),
                (SpySystem.SpyMission.Sabotage, SpySystem.SABOTAGE_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.Sabotage),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.Sabotage),
                    SpySystem.SABOTAGE_DURATION, "💣")
            };

            foreach (var (mission, reqLv, name, desc, duration, icon) in missions)
            {
                bool canDo = _selectedSpy.Level >= reqLv;

                // 발각 확률 계산 (방해 공작은 추가 발각 위험)
                float extraDetect = (mission == SpySystem.SpyMission.Sabotage) ? 0.1f : 0f;
                float detectChance = SpySystem.CalculateDetectChance(_selectedSpy, _currentTerritoryId) + extraDetect;
                detectChance = Mathf.Clamp01(detectChance);

                float miy = cy;
                GUI.Box(new Rect(x + 15, miy, PANEL_WIDTH - 30, 86), "");

                string lockStr = canDo ? "" : $" (Lv.{reqLv} 필요)";
                GUI.Label(new Rect(x + 25, miy + 4, PANEL_WIDTH - 50, 22),
                    $"{name}{lockStr}", canDo ? _styleLabel : _styleWarning);

                GUI.Label(new Rect(x + 25, miy + 26, PANEL_WIDTH - 50, 20),
                    desc, _styleValue);

                // 소요 시간 + 발각 확률
                int min = Mathf.FloorToInt(duration / 60f);
                int sec = Mathf.FloorToInt(duration % 60f);
                string timeStr = min > 0 ? $"{min}분 {sec}초" : $"{sec}초";
                string detectText = (mission == SpySystem.SpyMission.Sabotage) ? "⚠️ " : "";
                GUI.Label(new Rect(x + 25, miy + 46, PANEL_WIDTH - 50, 22),
                    $"⏱ {timeStr}  |  발각 위험: {detectText}{detectChance * 100:F0}%",
                    _styleValue);

                // 필요한 소모품 표시 (방해 공작)
                if (mission == SpySystem.SpyMission.Sabotage)
                {
                    GUI.Label(new Rect(x + 25, miy + 66, PANEL_WIDTH - 50, 18),
                        "소모품 필요: 독약/마약", _styleInfo);
                }

                if (canDo && GUI.Button(new Rect(x + PANEL_WIDTH - 130, miy + 26, 150, 30), "선택"))
                {
                    // 방해 공작: 소모품 체크 및 소비
                    if (mission == SpySystem.SpyMission.Sabotage)
                    {
                        if (!HasSabotageConsumables())
                        {
                            Debug.Log("[SpyMissionUI] 방해 공작에 필요한 소모품(독약/마약)이 부족합니다.");
                            continue;
                        }
                        // 소모품 1개 소비
                        ConsumeSabotageItem();
                    }
                    _selectedMission = mission;
                    _missionDuration = duration;
                    _missionTimer = duration;
                    _missionCompleted = false;
                    _currentStep = UIStep.InProgress;
                    _scrollPos = Vector2.zero;
                    return;
                }

                cy = miy + 90f + 6f;
            }

            // 뒤로가기
            cy += 6f;
            if (GUI.Button(new Rect(x + PANEL_WIDTH / 2 - 60, cy, 180, 30), "← 뒤로"))
            {
                _currentStep = UIStep.SelectSpy;
                _selectedSpy = null;
                _scrollPos = Vector2.zero;
            }
        }

        // ================================================================
        // 진행 중 화면
        // ================================================================
        private void DrawInProgress(float x, ref float cy)
        {
            if (_selectedSpy == null)
            {
                _currentStep = UIStep.SelectSpy;
                return;
            }

            string missionName = SpySystem.GetMissionName(_selectedMission);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"⏳ 정보 수집 중...", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"임무: {missionName}", _styleLabel);
            cy += 26f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"정보원: {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})", _styleLabel);
            cy += 26f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"목적지: {GetTerritoryName(_currentTerritoryId)}", _styleLabel);
            cy += 32f;

            // 진행바
            float progress = 1f - (_missionTimer / _missionDuration);
            DrawBar(x + 30, cy, PANEL_WIDTH - 60, 24, progress, Color.cyan, Color.gray);

            int remainingSec = Mathf.CeilToInt(_missionTimer);
            GUI.Label(new Rect(x + 30, cy + 28, PANEL_WIDTH - 60, 22),
                $"진행률: {progress * 100:F0}%  (남은 시간: {remainingSec}초)", _styleValue);
            cy += 56f;

            // 로딩 애니메이션 효과
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            string dots = new string('.', Mathf.FloorToInt(pulse * 6));
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"정보 수집 중{dots}", _styleLabel);
            cy += 30f;

            // 취소 버튼
            if (GUI.Button(new Rect(x + PANEL_WIDTH / 2 - 60, cy, 180, 30), "취소"))
            {
                Close();
            }
        }

        // ================================================================
        // 결과 화면 (성공)
        // ================================================================
        private void DrawResult(float x, ref float cy)
        {
            if (_selectedSpy == null)
            {
                _currentStep = UIStep.SelectSpy;
                return;
            }

            string missionName = SpySystem.GetMissionName(_selectedMission);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"✅ 정보 수집 완료 — {missionName}", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"정보원: {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})", _styleLabel);
            cy += 26f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"대상 영지: {GetTerritoryName(_currentTerritoryId)}", _styleLabel);
            cy += 30f;

            // 구분선
            GUI.Box(new Rect(x + 15, cy, PANEL_WIDTH - 30, 2), "");
            cy += 10f;

            // 수집된 정보 표시
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "📋 수집된 정보:", _styleTitle);
            cy += 28f;

            string infoText = _lastResult.infoGathered ?? "";
            if (!string.IsNullOrEmpty(infoText))
            {
                GUI.Label(new Rect(x + 25, cy, PANEL_WIDTH - 50, 80),
                    infoText, _styleInfo);
                cy += 85f;
            }

            // 추가 정보 표시 (임무 타입별)
            var def = TerritoryDatabase.Instance?.GetDefinition(_currentTerritoryId);
            var state = TerritoryDatabase.Instance?.GetState(_currentTerritoryId);

            if (def != null && _selectedMission == SpySystem.SpyMission.LordInfo && def.Value.territoryName != null)
            {
                DrawInfoRow(x, ref cy, "영주:", def.Value.lord.lordName);
                DrawInfoRow(x, ref cy, "선호 음식:", string.IsNullOrEmpty(def.Value.lord.preferredFood) ? "알 수 없음" : def.Value.lord.preferredFood);
                DrawInfoRow(x, ref cy, "지병:", string.IsNullOrEmpty(def.Value.lord.chronicDisease) ? "없음" : def.Value.lord.chronicDisease);
                DrawInfoRow(x, ref cy, "성격:", GetPersonalityText(def.Value.lord.personality));
                DrawInfoRow(x, ref cy, "충성심:", $"{def.Value.lord.loyalty}/100");
            }
            else if (def != null && _selectedMission == SpySystem.SpyMission.TroopInfo && def.Value.territoryName != null)
            {
                DrawInfoRow(x, ref cy, "병력 수:", $"{def.Value.guardCount}명");
                DrawInfoRow(x, ref cy, "방어 상태:", GetDefenseStatusText(def.Value.guardCount));
                DrawInfoRow(x, ref cy, "배치:", GetDeploymentInfoText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "난이도:", GetDifficultyText(def.Value.difficulty));
            }
            else if (def != null && _selectedMission == SpySystem.SpyMission.TerritoryMap && def.Value.territoryName != null)
            {
                DrawInfoRow(x, ref cy, "지형:", GetDifficultyTerrainName2(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "접근 경로:", GetApproachPathText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "은신처:", GetHideoutText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "취약점:", GetWeakPointText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "소유 상태:", GetOwnershipText(state));
            }
            else if (def != null && _selectedMission == SpySystem.SpyMission.Sabotage)
            {
                int casualties = Mathf.Max(1, Mathf.FloorToInt(def.Value.guardCount * 0.3f));
                int remaining = Mathf.Max(0, def.Value.guardCount - casualties);
                DrawInfoRow(x, ref cy, "피해 병사:", $"{casualties}명 (잔여: {remaining}명)");
                DrawInfoRow(x, ref cy, "영지 혼란:", "방해 공작 성공적");
                DrawInfoRow(x, ref cy, "효과 지속:", "24시간");
            }

            cy += 10f;
            float remainingTime = Mathf.Max(0, _resultTimer);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"({remainingTime:F1}초 후 창 닫힘)", _styleValue);
        }

        // ================================================================
        // 발각 화면
        // ================================================================
        private void DrawDetected(float x, ref float cy)
        {
            if (_selectedSpy == null)
            {
                _currentStep = UIStep.SelectSpy;
                return;
            }

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                "💀 정보원이 체포/처형되었습니다", _styleWarning);
            cy += 38f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 40),
                $"정보원 {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})이(가)\\n" +
                $"{GetTerritoryName(_currentTerritoryId)}에서 발각되어 처형되었습니다.",
                _styleLabel);
            cy += 50f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "⚠️ 해당 병사는 영구 소실됩니다.", _styleWarning);
            cy += 30f;

            // 처형 메시지 세부정보
            string msg = _lastResult.message ?? "발각되어 처형되었습니다.";
            GUI.Label(new Rect(x + 20, cy, PANEL_WIDTH - 40, 60),
                msg, _styleInfo);
            cy += 70f;

            float remaining = Mathf.Max(0, _resultTimer);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"({remaining:F1}초 후 창 닫힘)", _styleValue);
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>정보원 파견 UI 열기</summary>
        public bool IsVisible => _isVisible;

        public void Open()
        {
            _isVisible = true;
            _currentStep = UIStep.SelectSpy;
            _selectedSpy = null;
            _selectedMission = SpySystem.SpyMission.LordInfo;
            _scrollPos = Vector2.zero;
            _missionCompleted = false;
        }

        /// <summary>정보원 파견 UI 닫기</summary>
        public void Close()
        {
            _isVisible = false;
            _currentStep = UIStep.SelectSpy;
            _selectedSpy = null;
            _scrollPos = Vector2.zero;
        }

        // ================================================================
        // 임무 완료 처리
        // ================================================================

        private void CompleteMission()
        {
            _missionCompleted = true;
            _resultTimer = 5f; // 결과 표시 5초

            // SpySystem.SendSpy 호출
            _lastResult = SpySystem.SendSpy(_selectedSpy, _currentTerritoryId, _selectedMission);

            if (_lastResult.detected)
            {
                _currentStep = UIStep.Detected;
            }
            else if (_lastResult.success)
            {
                _currentStep = UIStep.Result;
            }
            else
            {
                // 실패 시 닫기
                Close();
            }
        }

        // ================================================================
        // UI 렌더링 헬퍼
        // ================================================================

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            _styleWarning = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(1f, 0.4f, 0.4f) }
            };

            _styleInfo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.9f, 0.6f) }
            };
        }

        private static void DrawBar(float x, float y, float w, float h, float fill, Color fillColor, Color bgColor)
        {
            var oldColor = GUI.color;

            GUI.color = bgColor;
            GUI.Box(new Rect(x, y, w, h), "");

            GUI.color = fillColor;
            if (fill > 0f)
                GUI.Box(new Rect(x + 1, y + 1, (w - 2) * fill, h - 2), "");

            GUI.color = oldColor;
        }

        private static void DrawInfoRow(float x, ref float cy, string label, string value)
        {
            GUI.Label(new Rect(x + 25, cy, 120, 20), label, GUI.skin.label);
            GUI.Label(new Rect(x + 145, cy, PANEL_WIDTH - 170, 20), value, GUI.skin.label);
            cy += 22f;
        }

        // ================================================================
        // 데이터 접근 헬퍼
        // ================================================================

        private List<GuardPlaceholder> GetAvailableSpiesForUI()
        {
            var all = SpySystem.GetAvailableSpies();
            // 최소 Lv.5 이상만 표시 (가장 낮은 임무 요구 레벨)
            var filtered = new List<GuardPlaceholder>();
            foreach (var g in all)
            {
                if (g.Level >= 5)
                    filtered.Add(g);
            }
            return filtered;
        }

        /// <summary>방해 공작에 필요한 소모품(독약/마약) 보유 여부 확인</summary>
        private static bool HasSabotageConsumables()
        {
            if (PlayerInventory.Instance == null) return false;
            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.category == PlayerInventory.ItemCategory.Potion ||
                    slot.item.category == PlayerInventory.ItemCategory.Drug)
                    return true;
            }
            return false;
        }

        /// <summary>방해 공작 소모품 1개 소비</summary>
        private static void ConsumeSabotageItem()
        {
            if (PlayerInventory.Instance == null) return;
            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.category == PlayerInventory.ItemCategory.Potion ||
                    slot.item.category == PlayerInventory.ItemCategory.Drug)
                {
                    PlayerInventory.Instance.RemoveItem(slot.item.id, 1);
                    Debug.Log($"[SpyMissionUI] 방해 공작 소모품 {slot.item.displayName} x1 소비됨");
                    return;
                }
            }
        }

        private static TerritoryId? GetCurrentTerritory(Vector3 playerPos)
        {
            // TODO: 실제 영지 판정 로직으로 교체
            // 현재는 TerritoryDatabase의 첫 번째 영지 반환 (데모용)
            if (TerritoryDatabase.Instance == null) return null;
            var allTerritories = TerritoryDatabase.Instance.GetAllDefinitions().ToList();
            if (allTerritories == null || allTerritories.Count == 0) return null;
            return allTerritories[0].id;
        }

        private static string GetTerritoryName(TerritoryId id)
        {
            var def = TerritoryDatabase.Instance?.GetDefinition(id);
            return string.IsNullOrEmpty(def?.territoryName) ? id.ToString() : def.Value.territoryName;
        }

        private static string GetDefenseStatusText(int guardCount)
        {
            if (guardCount <= 3) return "약함";
            if (guardCount <= 6) return "보통";
            if (guardCount <= 10) return "강함";
            return "매우 강함";
        }

        private static string GetDifficultyText(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "⭐";
                case TerritoryDifficulty.Ring2: return "⭐⭐";
                case TerritoryDifficulty.Ring3: return "⭐⭐⭐";
                case TerritoryDifficulty.Ring4: return "⭐⭐⭐⭐";
                case TerritoryDifficulty.Empire: return "👑";
                default: return "?";
            }
        }

        private static string GetDeploymentInfoText(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "정문 집중 배치";
                case TerritoryDifficulty.Ring2: return "정문 + 성벽 순찰";
                case TerritoryDifficulty.Ring3: return "다중 초소 분산 배치";
                case TerritoryDifficulty.Ring4: return "전 방위 밀집 배치";
                case TerritoryDifficulty.Empire: return "계층적 방어 체계";
                default: return "알 수 없음";
            }
        }

        private static string GetPersonalityText(LordPersonality p)
        {
            switch (p)
            {
                case LordPersonality.Neutral: return "보통";
                case LordPersonality.Greedy: return "탐욕스러움";
                case LordPersonality.Suspicious: return "의심 많음";
                case LordPersonality.Brave: return "용감함";
                case LordPersonality.Cowardly: return "겁많음";
                case LordPersonality.Wise: return "현명함";
                case LordPersonality.Cruel: return "잔인함";
                default: return "알 수 없음";
            }
        }

        private static string GetDifficultyTerrainName2(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "초원";
                case TerritoryDifficulty.Ring2: return "구릉";
                case TerritoryDifficulty.Ring3: return "산악";
                case TerritoryDifficulty.Ring4: return "협곡";
                case TerritoryDifficulty.Empire: return "황성";
                default: return "알 수 없음";
            }
        }

        private static string GetApproachPathText(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "남쪽에서 접근 용이";
                case TerritoryDifficulty.Ring2: return "동쪽 숲길 우회 가능";
                case TerritoryDifficulty.Ring3: return "북서쪽 절벽 경로";
                case TerritoryDifficulty.Ring4: return "지하 통로 존재";
                case TerritoryDifficulty.Empire: return "비밀 통로 확인 필요";
                default: return "알 수 없음";
            }
        }

        private static string GetHideoutText(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "북쪽 바위 뒤 은신 가능";
                case TerritoryDifficulty.Ring2: return "동쪽 숲에 은신 가능";
                case TerritoryDifficulty.Ring3: return "서쪽 동굴에 은신 가능";
                case TerritoryDifficulty.Ring4: return "남쪽 폐허에 은신 가능";
                case TerritoryDifficulty.Empire: return "지하 비밀 방에 은신 가능";
                default: return "없음";
            }
        }

        private static string GetWeakPointText(TerritoryDifficulty d)
        {
            switch (d)
            {
                case TerritoryDifficulty.Ring1: return "야간 경계 허술";
                case TerritoryDifficulty.Ring2: return "동쪽 담장 낮음";
                case TerritoryDifficulty.Ring3: return "서쪽 성벽 균열";
                case TerritoryDifficulty.Ring4: return "식량 비축 장소 노출";
                case TerritoryDifficulty.Empire: return "내부 분열 징후";
                default: return "확인되지 않음";
            }
        }

        private static string GetOwnershipText(TerritoryState? state)
        {
            if (state == null) return "알 수 없음";
            return "미점령"; // TODO: 실제 점령 상태 반영
        }
    }
}