using System.Collections;
using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;
#pragma warning disable 0414

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.9.3] 정보원 파견 UI (IMGUI)
    /// 
    /// 정보원 선택 → 임무 선택 (정찰/잠입/측량/방해공작)
    /// 미션 진행 중 상태 표시
    /// 완료 시 수집된 정보 UI 표시
    /// 발각 시 메시지: "정보원이 체포/처형되었습니다"
    /// 수집된 정보: 영주 선호음식/지병, 병력, 내부구조
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
        private const float PANEL_WIDTH = 520f;
        private const float PANEL_HEIGHT = 560f;
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
                "파견할 정보원을 선택하세요 (Lv.3+ 필요)", _styleLabel);
            cy += 26f;

            var availableSpies = GetAvailableSpiesForUI();
            float listY = cy;
            float listH = PANEL_HEIGHT - (cy - 0f) - 70f;

            GUI.BeginGroup(new Rect(x + 10, listY, PANEL_WIDTH - 20, listH));

            if (availableSpies.Count == 0)
            {
                GUI.Label(new Rect(10, 10, PANEL_WIDTH - 40, 24),
                    "⚠️ 파견 가능한 정보원이 없습니다.\nLv.3 이상 포섭된 병사가 필요합니다.", _styleWarning);

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
        // 임무 선택 화면
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

            // SpySystem의 기존 3종 임무 (Recon/Infiltrate/Survey)
            var missions = new (SpySystem.SpyMission mission, int requiredLevel, string name, string desc, float duration)[]
            {
                (SpySystem.SpyMission.Recon, SpySystem.RECON_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.Recon),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.Recon),
                    SpySystem.RECON_DURATION),
                (SpySystem.SpyMission.Infiltrate, SpySystem.INFILTRATE_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.Infiltrate),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.Infiltrate),
                    SpySystem.INFILTRATE_DURATION),
                (SpySystem.SpyMission.Survey, SpySystem.SURVEY_REQUIRED_LEVEL,
                    SpySystem.GetMissionName(SpySystem.SpyMission.Survey),
                    SpySystem.GetMissionDescription(SpySystem.SpyMission.Survey),
                    SpySystem.SURVEY_DURATION)
            };

            foreach (var (mission, reqLv, name, desc, duration) in missions)
            {
                bool canDo = _selectedSpy.Level >= reqLv;

                // 발각 확률 계산
                float detectChance = SpySystem.CalculateDetectChance(_selectedSpy, _currentTerritoryId);

                float miy = cy;
                GUI.Box(new Rect(x + 15, miy, PANEL_WIDTH - 30, 76), "");

                string lockStr = canDo ? "" : $" (Lv.{reqLv} 필요)";
                GUI.Label(new Rect(x + 25, miy + 4, PANEL_WIDTH - 50, 22),
                    $"{name}{lockStr}", canDo ? _styleLabel : _styleWarning);

                GUI.Label(new Rect(x + 25, miy + 26, PANEL_WIDTH - 50, 20),
                    desc, _styleValue);

                // 소요 시간 + 발각 확률
                int min = Mathf.FloorToInt(duration / 60f);
                int sec = Mathf.FloorToInt(duration % 60f);
                string timeStr = min > 0 ? $"{min}분 {sec}초" : $"{sec}초";
                GUI.Label(new Rect(x + 25, miy + 46, PANEL_WIDTH - 50, 22),
                    $"⏱ {timeStr}  |  발각 위험: {detectChance * 100:F0}%",
                    _styleValue);

                if (canDo && GUI.Button(new Rect(x + PANEL_WIDTH - 130, miy + 22, 150, 30), "선택"))
                {
                    _selectedMission = mission;
                    _missionDuration = duration;
                    _missionTimer = duration;
                    _missionCompleted = false;
                    _currentStep = UIStep.InProgress;
                    _scrollPos = Vector2.zero;
                    return;
                }

                cy = miy + 80f + 6f;
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
                GUI.Label(new Rect(x + 25, cy, PANEL_WIDTH - 50, 60),
                    infoText, _styleInfo);
                cy += 65f;
            }

            // 추가 정보 표시 (임무 타입별)
            var def = TerritoryDatabase.Instance?.GetDefinition(_currentTerritoryId);
            var state = TerritoryDatabase.Instance?.GetState(_currentTerritoryId);

            if (def != null && _selectedMission == SpySystem.SpyMission.Recon && def.Value.territoryName != null)
            {
                DrawInfoRow(x, ref cy, "병력 수:", $"{def.Value.guardCount}명");
                DrawInfoRow(x, ref cy, "방어 상태:", GetDefenseStatusText(def.Value.guardCount));
                DrawInfoRow(x, ref cy, "난이도:", GetDifficultyText(def.Value.difficulty));
            }
            else if (def != null && _selectedMission == SpySystem.SpyMission.Infiltrate && def.Value.lord.lordName != null)
            {
                DrawInfoRow(x, ref cy, "영주:", def.Value.lord.lordName);
                DrawInfoRow(x, ref cy, "선호 음식:", string.IsNullOrEmpty(def.Value.lord.preferredFood) ? "알 수 없음" : def.Value.lord.preferredFood);
                DrawInfoRow(x, ref cy, "지병:", string.IsNullOrEmpty(def.Value.lord.chronicDisease) ? "없음" : def.Value.lord.chronicDisease);
                DrawInfoRow(x, ref cy, "성격:", GetPersonalityText(def.Value.lord.personality));
                DrawInfoRow(x, ref cy, "충성심:", $"{def.Value.lord.loyalty}/100");
            }
            else if (def != null && _selectedMission == SpySystem.SpyMission.Survey && def.Value.territoryName != null)
            {
                DrawInfoRow(x, ref cy, "지형:", GetDifficultyTerrainName(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "접근 경로:", GetApproachPathText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "은신처:", GetHideoutText(def.Value.difficulty));
                DrawInfoRow(x, ref cy, "소유 상태:", GetOwnershipText(state));
            }

            cy += 10f;
            float remaining = Mathf.Max(0, _resultTimer);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"({remaining:F1}초 후 창 닫힘)", _styleValue);
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
                $"정보원 {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})이(가)\n" +
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
        public void Open()
        {
            _isVisible = true;
            _currentStep = UIStep.SelectSpy;
            _selectedSpy = null;
            _selectedMission = SpySystem.SpyMission.Recon;
            _scrollPos = Vector2.zero;
            _missionCompleted = false;
        }

        /// <summary>정보원 파견 UI 닫기</summary>
        public void Close()
        {
            _isVisible = false;
            _selectedSpy = null;
            _scrollPos = Vector2.zero;
        }

        /// <summary>UI 표시 여부</summary>
        public bool IsVisible => _isVisible;

        // ================================================================
        // 내부 로직
        // ================================================================

        private void CompleteMission()
        {
            if (_missionCompleted) return;
            _missionCompleted = true;

            // SpySystem.SendSpy 호출
            _lastResult = SpySystem.SendSpy(_selectedSpy, _currentTerritoryId, _selectedMission);

            if (_lastResult.detected || _lastResult.spyLost)
            {
                // 발각/처형
                _currentStep = UIStep.Detected;
                // 정보원 영구 소실 — 이미 SpySystem에서 TakeDamage(9999) 처리됨
                Debug.Log($"[SpyMissionUI] 💀 정보원 {_selectedSpy.GuardName} 처형됨!");
            }
            else
            {
                // 성공
                _currentStep = UIStep.Result;
                Debug.Log($"[SpyMissionUI] ✅ 정보 수집 성공: {_lastResult.infoGathered}");
            }

            _resultTimer = 8f; // 8초 후 자동 닫힘
        }

        private List<GuardPlaceholder> GetAvailableSpiesForUI()
        {
            var result = new List<GuardPlaceholder>();
            var spies = SpySystem.GetAvailableSpies();
            foreach (var g in spies)
            {
                // Lv.3+ 표시 (Recon이 최소 요구 레벨)
                if (g.Level >= SpySystem.RECON_REQUIRED_LEVEL)
                    result.Add(g);
            }
            return result;
        }

        /// <summary>현재 위치의 영지 ID 반환</summary>
        private TerritoryId? GetCurrentTerritory(Vector3 position)
        {
            if (TerritoryManager.Instance != null && TerritoryDatabase.Instance != null)
            {
                TerritoryId currentId = TerritoryManager.Instance.CurrentTerritoryId;
                var def = TerritoryDatabase.Instance.GetDefinition(currentId);
                if (def.territoryName != null)
                {
                    return currentId;
                }
            }

            if (TerritoryDatabase.Instance == null) return null;

            float nearestDist = _interactRange;
            TerritoryId? nearest = null;

            foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
            {
                Vector3 center = TerritoryManager.Instance != null
                    ? TerritoryManager.Instance.GetTerritoryCenter(def.id)
                    : Vector3.zero;

                float dist = Vector3.Distance(position, center);
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = def.id;
                }
            }

            return nearest;
        }

        private string GetTerritoryName(TerritoryId id)
        {
            var def = TerritoryDatabase.Instance.GetDefinition(id);
            return def.territoryName ?? "알 수 없는 영지";
        }

        // ================================================================
        // 텍스트 헬퍼
        // ================================================================

        private string GetDefenseStatusText(int guardCount)
        {
            if (guardCount <= 3) return "약함";
            if (guardCount <= 6) return "보통";
            if (guardCount <= 10) return "강함";
            return "매우 강함";
        }

        private string GetDifficultyText(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "🟢 Ring 1 (초원)";
                case TerritoryDifficulty.Ring2: return "🟡 Ring 2 (구릉)";
                case TerritoryDifficulty.Ring3: return "🟠 Ring 3 (산악)";
                case TerritoryDifficulty.Ring4: return "🔴 Ring 4 (협곡)";
                case TerritoryDifficulty.Empire: return "👑 황제국";
                default: return "알 수 없음";
            }
        }

        private string GetDifficultyTerrainName(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "초원 지형 — 평탄, 시야 좋음";
                case TerritoryDifficulty.Ring2: return "구릉 지형 — 완만한 언덕";
                case TerritoryDifficulty.Ring3: return "산악 지형 — 험난, 엄폐물 많음";
                case TerritoryDifficulty.Ring4: return "협곡 지형 — 좁은 통로, 매복 위험";
                case TerritoryDifficulty.Empire: return "황성 지형 — 정교한 건축물";
                default: return "알 수 없음";
            }
        }

        private string GetApproachPathText(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "남쪽에서 접근 용이";
                case TerritoryDifficulty.Ring2: return "동쪽 숲길로 접근";
                case TerritoryDifficulty.Ring3: return "북서쪽 암벽길로 접근";
                case TerritoryDifficulty.Ring4: return "지하 통로로 접근";
                case TerritoryDifficulty.Empire: return "비밀 통로 필요";
                default: return "정문에서 접근";
            }
        }

        private string GetHideoutText(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "북쪽 바위 뒤 은신 가능";
                case TerritoryDifficulty.Ring2: return "동쪽 숲에 은신 가능";
                case TerritoryDifficulty.Ring3: return "서쪽 동굴에 은신 가능";
                case TerritoryDifficulty.Ring4: return "남쪽 폐허에 은신 가능";
                case TerritoryDifficulty.Empire: return "지하 비밀 방에 은신 가능";
                default: return "은신처 없음";
            }
        }

        private string GetPersonalityText(LordPersonality personality)
        {
            switch (personality)
            {
                case LordPersonality.Neutral: return "보통";
                case LordPersonality.Greedy: return "탐욕스러움 — 선물에 약함";
                case LordPersonality.Suspicious: return "의심 많음 — 접근 어려움";
                case LordPersonality.Brave: return "용감함 — 협박에 안 통함";
                case LordPersonality.Cowardly: return "겁많음 — 협박에 약함";
                case LordPersonality.Wise: return "현명함 — 설득 어려움";
                case LordPersonality.Cruel: return "잔인함 — 위험";
                default: return "알 수 없음";
            }
        }

        private string GetOwnershipText(TerritoryState state)
        {
            if (state == null) return "알 수 없음";
            switch (state.ownership)
            {
                case TerritoryOwnership.Unoccupied: return "미점령";
                case TerritoryOwnership.PlayerOwned: return "플레이어 소유";
                case TerritoryOwnership.LordOwned: return "AI 영주 소유";
                case TerritoryOwnership.Contested: return "전쟁 중";
                default: return "알 수 없음";
            }
        }

        // ================================================================
        // UI 헬퍼
        // ================================================================

        private void DrawInfoRow(float x, ref float cy, string label, string value)
        {
            GUI.Label(new Rect(x + 20, cy, 150, 22), label, _styleLabel);
            GUI.Label(new Rect(x + 175, cy, PANEL_WIDTH - 195, 22), value, _styleValue);
            cy += 24f;
        }

        private void DrawBar(float x, float y, float width, float height, float ratio, Color fillColor, Color bgColor)
        {
            var tex = GetCachedTex();
            var oldColor = GUI.color;
            GUI.color = bgColor;
            GUI.DrawTexture(new Rect(x, y, width, height), tex);
            GUI.color = fillColor;
            GUI.DrawTexture(new Rect(x, y, width * Mathf.Clamp01(ratio), height), tex);
            GUI.color = oldColor;
        }

        private static Texture2D GetCachedTex()
        {
            if (_cachedTexWhite == null)
            {
                _cachedTexWhite = new Texture2D(1, 1);
                _cachedTexWhite.hideFlags = HideFlags.HideAndDontSave;
                _cachedTexWhite.SetPixel(0, 0, Color.white);
                _cachedTexWhite.Apply();
            }
            return _cachedTexWhite;
        }

        [System.Obsolete("Use GetCachedTex() instead — allocates every call")]
        private Texture2D MakeTex(int w, int h, Color c)
        {
            var tex = new Texture2D(w, h);
            for (int i = 0; i < w; i++)
                for (int j = 0; j < h; j++)
                    tex.SetPixel(i, j, c);
            tex.Apply();
            return tex;
        }

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleLeft
            };

            _styleWarning = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red },
                alignment = TextAnchor.MiddleLeft
            };

            _styleInfo = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.cyan },
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
        }
    }
}