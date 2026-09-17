using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-1 — 정보원(첩보) 파견 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/SpyMissionUI.cs (817줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 정보원 선택 — SpySystem.GetAvailableSpies 실측 API 직접 호출 (Lv.5+ 필터).
    ///  ② 임무 선택 — 4종 (영주정보/병력정보/영지약도/방해공작), SpySystem.GetMissionName/
    ///     GetMissionDescription/GetRequiredLevel/GetDuration + CalculateDetectChance 실측.
    ///  ③ 방해 공작 — Potion/Drug 소모품 체크 + 소비 (원본 HasSabotageConsumables 사용),
    ///     추가 발각 +10%.
    ///  ④ 진행 중 — 폴링 타이머 카운트다운 + 진행 표시, 완료 시 SpySystem.SendSpy 실측 호출.
    ///  ⑤ 결과/발각 — 수집 정보(infoGathered) + 임무 타입별 영지 정보 표시.
    ///  각 경로에 [SpyUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리, 400ms 폴링.
    /// </summary>
    public class SpyMissionUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static SpyMissionUTK _instance;
        public static SpyMissionUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new SpyMissionUTK();
        }

        /// <summary>정보원 파견 창 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(열려있으면 닫고, 닫혀있으면 염).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 560f;
        private const float WinH = 600f;
        private const long RefreshMs = 400L;

        private enum UIStep { SelectSpy, SelectMission, InProgress, Result, Detected }

        // ===== 선택 데이터 (원본 상태 미러) =====
        private UIStep _currentStep = UIStep.SelectSpy;
        private GuardPlaceholder _selectedSpy;
        private SpySystem.SpyMission _selectedMission;
        private TerritoryId _currentTerritoryId;
        private float _missionTimer;
        private float _missionDuration;
        private SpySystem.SpyResult _lastResult;
        private bool _missionCompleted;
        private float _resultTimer;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private SpyMissionUTK() : base("🕵️ 정보원 파견", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("🕵️ 정보원 파견");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _list = new VisualElement();
            _list.name = "SpyMissionList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 16f;
            style.top = 96f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 16f;
            style.top = 96f;
            ResetToSelectSpy();
            StartRefreshLoop();
            Debug.Log("[SpyUTK] 정보원 파견 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[SpyUTK] 정보원 파견 창 닫힘");
        }

        // ===== 폴링 갱신 (400ms) — 진행 타이머/결과 타이머 구동 =====

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                TickTimers();
                RefreshList();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        /// <summary>폴링 틱 단위(0.4s)로 임무 진행/결과 카운트다운 구동 (원본 Update 로직).</summary>
        private void TickTimers()
        {
            if (_currentStep == UIStep.InProgress && !_missionCompleted)
            {
                _missionTimer -= RefreshMs / 1000f;
                if (_missionTimer <= 0f)
                    CompleteMission();
            }
            else if (_currentStep == UIStep.Result || _currentStep == UIStep.Detected)
            {
                _resultTimer -= RefreshMs / 1000f;
                if (_resultTimer <= 0f)
                    Close();
            }
        }

        /// <summary>임무 완료 — SpySystem.SendSpy 실측 API 직접 호출 (원본 CompleteMission 대응).</summary>
        private void CompleteMission()
        {
            if (_selectedSpy == null) { ResetToSelectSpy(); return; }
            _missionCompleted = true;
            _resultTimer = 5f;

            _lastResult = SpySystem.SendSpy(_selectedSpy, _currentTerritoryId, _selectedMission);
            Debug.Log($"[SpyUTK] SendSpy 결과: mission={_selectedMission} success={_lastResult.success} "
                + $"detected={_lastResult.detected} spyLost={_lastResult.spyLost} msg={_lastResult.message}");

            if (_lastResult.detected)
                _currentStep = UIStep.Detected;
            else if (_lastResult.success)
                _currentStep = UIStep.Result;
            else
                ResetToSelectSpy();
        }

        /// <summary>상태 초기화 (원본 Open()/Close() 대응).</summary>
        private void ResetToSelectSpy()
        {
            _currentStep = UIStep.SelectSpy;
            _selectedSpy = null;
            _selectedMission = SpySystem.SpyMission.LordInfo;
            _missionCompleted = false;
            _missionTimer = 0f;
        }

        // ===== 목록 갱신 (매 갱신 재조회) =====

        private void RefreshList()
        {
            _list.Clear();

            switch (_currentStep)
            {
                case UIStep.SelectSpy: DrawSpySelection(); break;
                case UIStep.SelectMission: DrawMissionSelection(); break;
                case UIStep.InProgress: DrawInProgress(); break;
                case UIStep.Result: DrawResult(); break;
                case UIStep.Detected: DrawDetected(); break;
            }
        }

        private void DrawSpySelection()
        {
            _summaryLabel.text = $"🕵️ {GetTerritoryName(_currentTerritoryId)} — 정보원 파견";
            _list.Add(MakeLabel("파견할 정보원을 선택하세요 (Lv.5+ 필요)", UTKColor.TextSecondary));

            var spies = SpySystem.GetAvailableSpies();
            var filtered = new List<GuardPlaceholder>();
            foreach (var g in spies)
            {
                if (g.Level >= 5)
                    filtered.Add(g);
            }

            if (filtered.Count == 0)
            {
                _list.Add(MakeLabel("⚠️ 파견 가능한 정보원이 없습니다.\nLv.5 이상 포섭된 병사가 필요합니다.", UTKColor.HealthRed));
                return;
            }

            foreach (var guard in filtered)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = new StyleColor(UTKColor.IronLine);
                row.style.paddingTop = 4f;
                row.style.paddingBottom = 4f;
                row.style.alignItems = Align.Center;

                string roleStr = GuardStatusSystem.GetRoleName(guard.Role);
                var info = MakeLabel($"{guard.GuardName} (Lv.{guard.Level})  {roleStr} | 호감도 {guard.Loyalty:F0} | 민첩 {guard.GetAgility()}",
                    UTKColor.TextPrimary);
                info.style.flexGrow = 1f;
                row.Add(info);

                row.Add(UTKButton.Create("정보원 선택", () =>
                {
                    _selectedSpy = guard;
                    _currentStep = UIStep.SelectMission;
                    Debug.Log($"[SpyUTK] 정보원 선택 → {guard.GuardName} (Lv.{guard.Level})");
                    RefreshList();
                }, UTKButton.Variant.Primary));

                _list.Add(row);
            }
        }

        private void DrawMissionSelection()
        {
            if (_selectedSpy == null) { ResetToSelectSpy(); return; }

            _summaryLabel.text = $"🎯 임무 선택 — {_selectedSpy.GuardName}";
            _list.Add(MakeLabel("수행할 정보 수집 임무를 선택하세요", UTKColor.TextSecondary));

            var missions = new SpySystem.SpyMission[]
            {
                SpySystem.SpyMission.LordInfo,
                SpySystem.SpyMission.TroopInfo,
                SpySystem.SpyMission.TerritoryMap,
                SpySystem.SpyMission.Sabotage
            };

            foreach (var mission in missions)
            {
                int reqLv = SpySystem.GetRequiredLevel(mission);
                bool canDo = _selectedSpy.Level >= reqLv;
                string name = SpySystem.GetMissionName(mission);
                string desc = SpySystem.GetMissionDescription(mission);
                float duration = SpySystem.GetDuration(mission);

                float extraDetect = mission == SpySystem.SpyMission.Sabotage ? 0.1f : 0f;
                float detectChance = Mathf.Clamp01(
                    SpySystem.CalculateDetectChance(_selectedSpy, _currentTerritoryId) + extraDetect);

                int min = Mathf.FloorToInt(duration / 60f);
                int sec = Mathf.FloorToInt(duration % 60f);
                string timeStr = min > 0 ? $"{min}분 {sec}초" : $"{sec}초";
                string detectText = mission == SpySystem.SpyMission.Sabotage ? "⚠️ " : "";

                var box = new VisualElement();
                box.style.flexDirection = FlexDirection.Column;
                box.style.borderTopWidth = 1f;
                box.style.borderTopColor = new StyleColor(UTKColor.IronLine);
                box.style.paddingTop = 4f;
                box.style.paddingBottom = 6f;

                string lockStr = canDo ? "" : $" (Lv.{reqLv} 필요)";
                var nameLabel = MakeLabel($"{name}{lockStr}", canDo ? UTKColor.TextPrimary : UTKColor.HealthRed);
                nameLabel.style.fontSize = 14f;
                box.Add(nameLabel);
                box.Add(MakeLabel(desc, UTKColor.TextSecondary));
                box.Add(MakeLabel($"⏱ {timeStr}  |  발각 위험: {detectText}{detectChance * 100:F0}%", UTKColor.TextSecondary));

                if (mission == SpySystem.SpyMission.Sabotage)
                    box.Add(MakeLabel("소모품 필요: 독약/마약", UTKColor.AccentMagic));

                if (canDo)
                {
                    box.Add(UTKButton.Create("선택", () => StartMission(mission), UTKButton.Variant.Secondary));
                }

                _list.Add(box);
            }

            _list.Add(UTKButton.Create("← 뒤로", () =>
            {
                _selectedSpy = null;
                _currentStep = UIStep.SelectSpy;
                RefreshList();
            }, UTKButton.Variant.Danger));
        }

        /// <summary>임무 시작 (원본 mission 선택 핸들러 — 방해 공작 소모품 체크/소비).</summary>
        private void StartMission(SpySystem.SpyMission mission)
        {
            if (mission == SpySystem.SpyMission.Sabotage)
            {
                if (!HasSabotageConsumables())
                {
                    Debug.LogWarning("[SpyUTK] 방해 공작에 필요한 소모품(독약/마약)이 부족합니다.");
                    return;
                }
                ConsumeSabotageItem();
            }

            _selectedMission = mission;
            _missionDuration = SpySystem.GetDuration(mission);
            _missionTimer = _missionDuration;
            _missionCompleted = false;
            _currentStep = UIStep.InProgress;
            Debug.Log($"[SpyUTK] 임무 시작 → {SpySystem.GetMissionName(mission)} (대상 {GetTerritoryName(_currentTerritoryId)})");
            RefreshList();
        }

        private void DrawInProgress()
        {
            if (_selectedSpy == null) { ResetToSelectSpy(); return; }

            string missionName = SpySystem.GetMissionName(_selectedMission);
            _summaryLabel.text = $"⏳ 정보 수집 중...";
            _list.Add(MakeLabel($"임무: {missionName}", UTKColor.TextPrimary));
            _list.Add(MakeLabel($"정보원: {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})", UTKColor.TextPrimary));
            _list.Add(MakeLabel($"목적지: {GetTerritoryName(_currentTerritoryId)}", UTKColor.TextPrimary));

            // 진행 바 (원본 DrawBar 대응 — UTK 폭 백분율)
            float progress = Mathf.Clamp01(_missionDuration > 0f ? 1f - (_missionTimer / _missionDuration) : 1f);
            var barBg = new VisualElement();
            barBg.style.height = 18f;
            barBg.style.backgroundColor = new StyleColor(UTKColor.IronLine);
            barBg.style.borderTopWidth = 1f;
            barBg.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            var barFill = new VisualElement();
            barFill.style.height = 100f;
            barFill.style.width = new Length(progress * 100f, LengthUnit.Percent);
            barFill.style.backgroundColor = new StyleColor(UTKColor.AccentMagic);
            barBg.Add(barFill);
            _list.Add(barBg);

            int remainingSec = Mathf.CeilToInt(_missionTimer);
            _list.Add(MakeLabel($"진행률: {progress * 100:F0}%  (남은 시간: {remainingSec}초)", UTKColor.TextSecondary));

            _list.Add(UTKButton.Create("취소", () =>
            {
                Debug.Log("[SpyUTK] 임무 취소 — 창 닫음");
                Close();
            }, UTKButton.Variant.Danger));
        }

        private void DrawResult()
        {
            if (_selectedSpy == null) { ResetToSelectSpy(); return; }

            string missionName = SpySystem.GetMissionName(_selectedMission);
            _summaryLabel.text = $"✅ 정보 수집 완료 — {missionName}";
            _list.Add(MakeLabel($"정보원: {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})", UTKColor.TextPrimary));
            _list.Add(MakeLabel($"대상 영지: {GetTerritoryName(_currentTerritoryId)}", UTKColor.TextPrimary));

            _list.Add(MakeLabel("📋 수집된 정보:", UTKColor.TextPrimary));
            string infoText = _lastResult.infoGathered ?? "";
            if (!string.IsNullOrEmpty(infoText))
                _list.Add(MakeLabel(infoText, UTKColor.GuildGreen));

            AppendMissionDetailRows();

            _list.Add(MakeLabel($"({Mathf.Max(0, _resultTimer):F1}초 후 창 닫힘)", UTKColor.TextSecondary));
        }

        /// <summary>임무 타입별 영지 정보 행 (원본 DrawResult의 임무별 추가 정보 대응).</summary>
        private void AppendMissionDetailRows()
        {
            var def = TerritoryDatabase.Instance?.GetDefinition(_currentTerritoryId);
            var state = TerritoryDatabase.Instance?.GetState(_currentTerritoryId);
            if (def == null) return;

            if (_selectedMission == SpySystem.SpyMission.LordInfo && !string.IsNullOrEmpty(def.Value.territoryName))
            {
                AddInfoRow("영주:", def.Value.lord.lordName);
                AddInfoRow("선호 음식:", string.IsNullOrEmpty(def.Value.lord.preferredFood) ? "알 수 없음" : def.Value.lord.preferredFood);
                AddInfoRow("지병:", string.IsNullOrEmpty(def.Value.lord.chronicDisease) ? "없음" : def.Value.lord.chronicDisease);
                AddInfoRow("성격:", GetPersonalityText(def.Value.lord.personality));
                AddInfoRow("충성심:", $"{def.Value.lord.loyalty}/100");
            }
            else if (_selectedMission == SpySystem.SpyMission.TroopInfo && !string.IsNullOrEmpty(def.Value.territoryName))
            {
                AddInfoRow("병력 수:", $"{def.Value.guardCount}명");
                AddInfoRow("방어 상태:", GetDefenseStatusText(def.Value.guardCount));
                AddInfoRow("배치:", GetDeploymentInfoText(def.Value.difficulty));
                AddInfoRow("난이도:", GetDifficultyText(def.Value.difficulty));
            }
            else if (_selectedMission == SpySystem.SpyMission.TerritoryMap && !string.IsNullOrEmpty(def.Value.territoryName))
            {
                AddInfoRow("지형:", GetDifficultyTerrainName2(def.Value.difficulty));
                AddInfoRow("접근 경로:", GetApproachPathText(def.Value.difficulty));
                AddInfoRow("은신처:", GetHideoutText(def.Value.difficulty));
                AddInfoRow("취약점:", GetWeakPointText(def.Value.difficulty));
                AddInfoRow("소유 상태:", state == null ? "알 수 없음" : "미점령");
            }
            else if (_selectedMission == SpySystem.SpyMission.Sabotage)
            {
                int casualties = Mathf.Max(1, Mathf.FloorToInt(def.Value.guardCount * 0.3f));
                int remaining = Mathf.Max(0, def.Value.guardCount - casualties);
                AddInfoRow("피해 병사:", $"{casualties}명 (잔여: {remaining}명)");
                AddInfoRow("영지 혼란:", "방해 공작 성공적");
                AddInfoRow("효과 지속:", "24시간");
            }
        }

        private void DrawDetected()
        {
            if (_selectedSpy == null) { ResetToSelectSpy(); return; }

            _summaryLabel.text = "💀 정보원이 체포/처형되었습니다";
            var warn = MakeLabel($"정보원 {_selectedSpy.GuardName} (Lv.{_selectedSpy.Level})이(가) "
                + $"{GetTerritoryName(_currentTerritoryId)}에서 발각되어 처형되었습니다.", UTKColor.HealthRed);
            _list.Add(warn);
            _list.Add(MakeLabel("⚠️ 해당 병사는 영구 소실됩니다.", UTKColor.HealthRed));

            string msg = _lastResult.message ?? "발각되어 처형되었습니다.";
            _list.Add(MakeLabel(msg, UTKColor.HealthRed));

            _list.Add(MakeLabel($"({Mathf.Max(0, _resultTimer):F1}초 후 창 닫힘)", UTKColor.TextSecondary));
        }

        // ===== 방해 공작 소모품 (원본 헬퍼 차용) =====

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
                    Debug.Log($"[SpyUTK] 방해 공작 소모품 {slot.item.displayName} x1 소비됨");
                    return;
                }
            }
        }

        // ===== 헬퍼 =====

        private void AddInfoRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 2f;
            var l = MakeLabel(label, UTKColor.TextSecondary);
            l.style.width = 110f;
            var v = MakeLabel(value, UTKColor.TextPrimary);
            v.style.flexGrow = 1f;
            row.Add(l);
            row.Add(v);
            _list.Add(row);
        }

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        /// <summary>현재 영지 결정 — TerritoryManager 우선, 없으면 첫 정의(데모)로 대체 (원본 GetCurrentTerritory 차용).</summary>
        private static TerritoryId GetCurrentTerritory()
        {
            var db = TerritoryDatabase.Instance;
            if (db == null) return default;

            var tm = TerritoryManager.Instance;
            if (tm != null)
            {
                TerritoryId id = tm.CurrentTerritoryId;
                var def = db.GetDefinition(id);
                if (!string.IsNullOrEmpty(def.territoryName))
                    return id;
            }

            var all = db.GetAllDefinitions();
            foreach (var def in all)
            {
                if (!string.IsNullOrEmpty(def.territoryName))
                    return def.id;
            }
            return default;
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
    }
}