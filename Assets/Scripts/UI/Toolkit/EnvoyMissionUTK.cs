// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-1 — 특사 파견 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/EnvoyMissionUI.cs (673줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 특사 선택 — EnvoySystem.GetAvailableEnvoys 실측 API 직접 호출 (Lv.5+ = GIFT_REQUIRED_LEVEL 필터).
    ///  ② 임무 선택 — 4종 (선물/우호/동맹/독살), GetMissionName/GetMissionDescription/GetRequiredLevel,
    ///     독살 시만 발각 확률(CalculateDetectChance) 표시.
    ///  ③ 독살 — 인벤토리 Food 아이템 선택 (원본 GetFoodItemsFromInventory 차용).
    ///  ④ 확인 — 특사/목적지/임무/음식/발각확률/이동시간(TerritoryManager 중심점 거리) 표시.
    ///  ⑤ 파견 — 독살 시 인벤토리 Food 1개 소비 후 EnvoySystem.SendEnvoy 실측 호출, 결과 표시.
    ///  각 경로에 [EnvoyUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리, 400ms 폴링.
    /// </summary>
    public class EnvoyMissionUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static EnvoyMissionUTK _instance;
        public static EnvoyMissionUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new EnvoyMissionUTK();
        }

        /// <summary>특사 파견 창 열기.</summary>
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
        private const float WinW = 520f;
        private const float WinH = 540f;
        private const long RefreshMs = 400L;
        private const float TravelSpeed = 1f;        // 단위 거리당 초 (원본 _travelSpeed)
        private const float TerritorySearchRadius = 100f;

        private enum UIStep { SelectEnvoy, SelectMission, SelectPoisonFood, Confirm, Result }

        // ===== 선택 데이터 (원본 상태 미러) =====
        private UIStep _currentStep = UIStep.SelectEnvoy;
        private GuardPlaceholder _selectedEnvoy;
        private EnvoySystem.EnvoyMission _selectedMission;
        private TerritoryId _currentTerritoryId;
        private string _selectedFoodItemId;
        private string _selectedFoodName;
        private EnvoySystem.EnvoyResult _lastResult;
        private float _resultTimer;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private EnvoyMissionUTK() : base("📜 특사 파견", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("📜 특사 파견");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _list = new VisualElement();
            _list.name = "EnvoyMissionList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 590f;
            style.top = 96f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 590f;
            style.top = 96f;
            ResetToSelectEnvoy();
            StartRefreshLoop();
            Debug.Log("[EnvoyUTK] 특사 파견 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[EnvoyUTK] 특사 파견 창 닫힘");
        }

        // ===== 폴링 갱신 (400ms) — 결과 타이머 구동 =====

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

        /// <summary>결과 표시 카운트다운 (원본 Update 로직).</summary>
        private void TickTimers()
        {
            if (_currentStep == UIStep.Result)
            {
                _resultTimer -= RefreshMs / 1000f;
                if (_resultTimer <= 0f)
                    Close();
            }
        }

        /// <summary>상태 초기화 (원본 Close() 대응 — 선택 상태 리셋).</summary>
        private void ResetToSelectEnvoy()
        {
            _currentStep = UIStep.SelectEnvoy;
            _selectedEnvoy = null;
            _selectedFoodItemId = null;
            _selectedFoodName = null;
            _selectedMission = EnvoySystem.EnvoyMission.Gift;
        }

        // ===== 목록 갱신 (매 갱신 재조회) =====

        private void RefreshList()
        {
            _list.Clear();

            switch (_currentStep)
            {
                case UIStep.SelectEnvoy: DrawEnvoySelection(); break;
                case UIStep.SelectMission: DrawMissionSelection(); break;
                case UIStep.SelectPoisonFood: DrawPoisonFoodSelection(); break;
                case UIStep.Confirm: DrawConfirmation(); break;
                case UIStep.Result: DrawResult(); break;
            }
        }

        private void DrawEnvoySelection()
        {
            _summaryLabel.text = $"📜 {GetTerritoryName(_currentTerritoryId)} — 특사 파견";
            _list.Add(MakeLabel("파견할 특사를 선택하세요 (Lv.5+ 필요)", UTKColor.TextSecondary));

            var envoys = EnvoySystem.GetAvailableEnvoys();
            var filtered = new List<GuardPlaceholder>();
            foreach (var g in envoys)
            {
                if (g.Level >= EnvoySystem.GIFT_REQUIRED_LEVEL)
                    filtered.Add(g);
            }

            if (filtered.Count == 0)
            {
                _list.Add(MakeLabel("⚠️ 파견 가능한 특사가 없습니다.\nLv.5 이상 포섭된 병사가 필요합니다.", UTKColor.HealthRed));
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
                var info = MakeLabel($"{guard.GuardName} (Lv.{guard.Level})  {roleStr} | 호감도 {guard.Loyalty:F0}",
                    UTKColor.TextPrimary);
                info.style.flexGrow = 1f;
                row.Add(info);

                row.Add(UTKButton.Create("특사 선택", () =>
                {
                    _selectedEnvoy = guard;
                    _currentStep = UIStep.SelectMission;
                    Debug.Log($"[EnvoyUTK] 특사 선택 → {guard.GuardName} (Lv.{guard.Level})");
                    RefreshList();
                }, UTKButton.Variant.Primary));

                _list.Add(row);
            }
        }

        private void DrawMissionSelection()
        {
            if (_selectedEnvoy == null) { ResetToSelectEnvoy(); return; }

            _summaryLabel.text = $"🎯 임무 선택 — {_selectedEnvoy.GuardName}";
            _list.Add(MakeLabel("파견할 임무를 선택하세요", UTKColor.TextSecondary));

            var missions = new EnvoySystem.EnvoyMission[]
            {
                EnvoySystem.EnvoyMission.Gift,
                EnvoySystem.EnvoyMission.Friendship,
                EnvoySystem.EnvoyMission.Alliance,
                EnvoySystem.EnvoyMission.Assassinate
            };

            foreach (var mission in missions)
            {
                int reqLv = EnvoySystem.GetRequiredLevel(mission);
                bool canDo = _selectedEnvoy.Level >= reqLv;
                string name = EnvoySystem.GetMissionName(mission);
                string desc = EnvoySystem.GetMissionDescription(mission);
                string lockStr = canDo ? "" : $" (Lv.{reqLv} 필요)";
                string detectStr = "";

                if (mission == EnvoySystem.EnvoyMission.Assassinate && canDo)
                {
                    float detectChance = EnvoySystem.CalculateDetectChance(_selectedEnvoy, _currentTerritoryId);
                    detectStr = $"  |  발각 위험: {detectChance * 100:F0}%";
                }

                var box = new VisualElement();
                box.style.flexDirection = FlexDirection.Column;
                box.style.borderTopWidth = 1f;
                box.style.borderTopColor = new StyleColor(UTKColor.IronLine);
                box.style.paddingTop = 4f;
                box.style.paddingBottom = 6f;

                var nameLabel = MakeLabel($"{name}{lockStr}{detectStr}", canDo ? UTKColor.TextPrimary : UTKColor.HealthRed);
                nameLabel.style.fontSize = 14f;
                box.Add(nameLabel);
                box.Add(MakeLabel(desc, UTKColor.TextSecondary));

                if (canDo)
                {
                    box.Add(UTKButton.Create("선택", () =>
                    {
                        _selectedMission = mission;
                        _currentStep = mission == EnvoySystem.EnvoyMission.Assassinate
                            ? UIStep.SelectPoisonFood
                            : UIStep.Confirm;
                        Debug.Log($"[EnvoyUTK] 임무 선택 → {EnvoySystem.GetMissionName(mission)}");
                        RefreshList();
                    }, UTKButton.Variant.Secondary));
                }

                _list.Add(box);
            }

            _list.Add(UTKButton.Create("← 뒤로", () =>
            {
                _selectedEnvoy = null;
                _currentStep = UIStep.SelectEnvoy;
                RefreshList();
            }, UTKButton.Variant.Danger));
        }

        private void DrawPoisonFoodSelection()
        {
            if (_selectedEnvoy == null) { ResetToSelectEnvoy(); return; }

            _summaryLabel.text = $"☠️ 독든 음식 선택";
            _list.Add(MakeLabel($"특사: {_selectedEnvoy.GuardName} → {GetTerritoryName(_currentTerritoryId)}", UTKColor.TextSecondary));

            var foodItems = GetFoodItemsFromInventory();
            if (foodItems.Count == 0)
            {
                _list.Add(MakeLabel("⚠️ 보유한 음식 아이템이 없습니다.\n인벤토리에서 음식을 준비하세요.", UTKColor.HealthRed));
            }
            else
            {
                foreach (var pair in foodItems)
                {
                    bool isPoisoned = IsItemPoisoned(pair.Key.id);
                    string poisonTag = isPoisoned ? " ☠️(독)" : "";

                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.borderTopWidth = 1f;
                    row.style.borderTopColor = new StyleColor(UTKColor.IronLine);
                    row.style.paddingTop = 4f;
                    row.style.paddingBottom = 4f;
                    row.style.alignItems = Align.Center;

                    var info = MakeLabel($"{pair.Key.displayName}{poisonTag}  x{pair.Value}", UTKColor.TextPrimary);
                    info.style.flexGrow = 1f;
                    row.Add(info);

                    row.Add(UTKButton.Create("선택", () =>
                    {
                        _selectedFoodItemId = pair.Key.id;
                        _selectedFoodName = pair.Key.displayName;
                        _currentStep = UIStep.Confirm;
                        Debug.Log($"[EnvoyUTK] 독든 음식 선택 → {pair.Key.displayName}");
                        RefreshList();
                    }, UTKButton.Variant.Secondary));

                    _list.Add(row);
                }
            }

            _list.Add(UTKButton.Create("← 뒤로", () =>
            {
                _currentStep = UIStep.SelectMission;
                RefreshList();
            }, UTKButton.Variant.Danger));
        }

        private void DrawConfirmation()
        {
            if (_selectedEnvoy == null) { ResetToSelectEnvoy(); return; }

            _summaryLabel.text = "📋 파견 확인";

            string envoyName = $"{_selectedEnvoy.GuardName} (Lv.{_selectedEnvoy.Level})";
            AddInfoRow("특사:", envoyName);
            AddInfoRow("목적지:", GetTerritoryName(_currentTerritoryId));
            AddInfoRow("임무:", EnvoySystem.GetMissionName(_selectedMission));
            AddInfoRow("임무 설명:", EnvoySystem.GetMissionDescription(_selectedMission));

            if (_selectedMission == EnvoySystem.EnvoyMission.Assassinate)
                AddInfoRow("음식:", _selectedFoodName ?? "(없음)");

            float detectChance = EnvoySystem.CalculateDetectChance(_selectedEnvoy, _currentTerritoryId);
            string detectColor = detectChance >= 0.4f ? "🔴" : (detectChance >= 0.2f ? "🟡" : "🟢");
            AddInfoRow("발각 위험:", $"{detectColor} {detectChance * 100:F0}%");

            float travelTime = CalculateTravelTime(_currentTerritoryId);
            int minutes = Mathf.FloorToInt(travelTime / 60f);
            int seconds = Mathf.FloorToInt(travelTime % 60f);
            string timeStr = minutes > 0 ? $"{minutes}분 {seconds}초" : $"{seconds}초";
            AddInfoRow("이동 시간:", timeStr);

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 8f;
            btnRow.Add(UTKButton.Create("✅ 파견하기", ExecuteMission, UTKButton.Variant.Primary));
            btnRow.Add(UTKButton.Create("← 취소", () => Close(), UTKButton.Variant.Danger));
            _list.Add(btnRow);
        }

        private void DrawResult()
        {
            _summaryLabel.text = _lastResult.success ? "✅ 임무 완료" : "❌ 임무 실패";
            _list.Add(MakeLabel(_lastResult.message ?? "", UTKColor.TextPrimary));

            if (_lastResult.success)
            {
                string change = _lastResult.loyaltyChange >= 0
                    ? $"+{_lastResult.loyaltyChange}"
                    : $"{_lastResult.loyaltyChange}";
                AddInfoRow("호감도 변화:", change);
            }

            if (_lastResult.detected)
                _list.Add(MakeLabel("⚠️ 발각! 특사가 처형되었습니다.", UTKColor.HealthRed));

            _list.Add(MakeLabel($"({Mathf.Max(0, _resultTimer):F1}초 후 창 닫힘)", UTKColor.TextSecondary));
        }

        // ===== 파견 실행 =====

        /// <summary>특사 파견 — 독살 시 Food 1개 소비 후 EnvoySystem.SendEnvoy 실측 호출 (원본 ExecuteMission 대응).</summary>
        private void ExecuteMission()
        {
            if (_selectedEnvoy == null) { ResetToSelectEnvoy(); return; }

            if (_selectedMission == EnvoySystem.EnvoyMission.Assassinate
                && !string.IsNullOrEmpty(_selectedFoodItemId)
                && PlayerInventory.Instance != null)
            {
                PlayerInventory.Instance.RemoveItem(_selectedFoodItemId);
                Debug.Log($"[EnvoyUTK] 독살 소모품 소비 → {_selectedFoodName} (id={_selectedFoodItemId})");
            }

            _lastResult = EnvoySystem.SendEnvoy(_selectedEnvoy, _currentTerritoryId, _selectedMission);
            _currentStep = UIStep.Result;
            _resultTimer = 5f;
            Debug.Log($"[EnvoyUTK] 특사 파견 결과: mission={_selectedMission} success={_lastResult.success} "
                + $"detected={_lastResult.detected} loyaltyChange={_lastResult.loyaltyChange} msg={_lastResult.message}");
            RefreshList();
        }

        // ===== 하위 목록/헬퍼 =====

        private List<KeyValuePair<PlayerInventory.ItemData, int>> GetFoodItemsFromInventory()
        {
            var result = new List<KeyValuePair<PlayerInventory.ItemData, int>>();
            if (PlayerInventory.Instance == null) return result;
            var slots = PlayerInventory.Instance.GetAllSlots();
            foreach (var slot in slots)
            {
                if (slot == null || slot.item == null || slot.count <= 0) continue;
                if (slot.item.category == PlayerInventory.ItemCategory.Food)
                    result.Add(new KeyValuePair<PlayerInventory.ItemData, int>(slot.item, slot.count));
            }
            return result;
        }

        /// <summary>음식 독 처리 상태 (원본 IsItemPoisoned 대응 — Phase 4.2 통합 예정, 현재 false).</summary>
        private static bool IsItemPoisoned(string itemId)
        {
            return false;
        }

        /// <summary>이동 시간 — TerritoryManager 중심점 거리 비례 (원본 CalculateTravelTime 차용).</summary>
        private float CalculateTravelTime(TerritoryId targetId)
        {
            var tm = TerritoryManager.Instance;
            if (tm == null)
                return 30f;

            Vector3 currentCenter = tm.GetTerritoryCenter();
            Vector3 targetCenter = tm.GetTerritoryCenter(targetId);
            float distance = Vector3.Distance(currentCenter, targetCenter);
            return Mathf.Max(5f, distance * TravelSpeed);
        }

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

        /// <summary>현재 영지 결정 (원본 GetCurrentTerritory 차용 — TerritoryManager/최근접 영지).</summary>
        private static TerritoryId GetCurrentTerritory()
        {
            if (TerritoryManager.Instance != null && TerritoryDatabase.Instance != null)
            {
                TerritoryId currentId = TerritoryManager.Instance.CurrentTerritoryId;
                var def = TerritoryDatabase.Instance.GetDefinition(currentId);
                if (def.territoryName != null)
                    return currentId;
            }

            if (TerritoryDatabase.Instance == null || TerritoryManager.Instance == null)
            {
                if (TerritoryDatabase.Instance == null) return default;
                var all = TerritoryDatabase.Instance.GetAllDefinitions();
                foreach (var def in all)
                {
                    if (def.territoryName != null)
                        return def.id;
                }
                return default;
            }

            // TerritoryManager 존재하나 현재 유효치 못하면 최근접 영지 검색
            float nearestDist = TerritorySearchRadius;
            TerritoryId? nearest = null;
            Vector3 pos = TerritoryManager.Instance.GetTerritoryCenter();
            foreach (var def in TerritoryDatabase.Instance.GetAllDefinitions())
            {
                float dist = Vector3.Distance(pos, TerritoryManager.Instance.GetTerritoryCenter(def.id));
                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = def.id;
                }
            }
            return nearest ?? default;
        }

        private static string GetTerritoryName(TerritoryId id)
        {
            if (TerritoryDatabase.Instance == null)
                return "알 수 없는 영지";
            var def = TerritoryDatabase.Instance.GetDefinition(id);
            return def.territoryName != null ? def.territoryName : "알 수 없는 영지";
        }
    }
}