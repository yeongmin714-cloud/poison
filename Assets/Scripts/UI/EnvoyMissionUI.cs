using System.Collections.Generic;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;
using UnityEngine;
using ProjectName.UI.Themes;

namespace ProjectName.UI
{
    /// <summary>
    /// [5.3.9.2] 특사 파견 UI (IMGUI)
    /// 
    /// 영지 방문 시 E키 → "특사 파견" 버튼
    /// 특사 선택: 소유 병사 목록 중 Lv.5+ 표시
    /// 임무 선택: 선물/우호/동맹/독살 (EnvoyMission enum)
    /// 독살 선택 시: 인벤토리에서 음식 아이템 선택
    /// 발각 확률: 호감도+레벨 기반 계산 → "발각 위험: X%" 표시
    /// 특사 파견 → 이동 시간 표시 (거리 비례 초)
    /// </summary>
    public class EnvoyMissionUI : MonoBehaviour
    {
        public static EnvoyMissionUI Instance { get; private set; }

        [Header("설정")]
        [SerializeField] private KeyCode _openKey = KeyCode.E;
        [SerializeField] private float _interactRange = 5f;
        [SerializeField] private float _travelSpeed = 1f; // 단위 거리당 초

        // ===== UI 상태 =====
        private bool _isVisible = false;
        private enum UIStep { SelectEnvoy, SelectMission, SelectPoisonFood, Confirm, Result }
        private UIStep _currentStep = UIStep.SelectEnvoy;
        private Vector2 _scrollPos;

        // ===== 선택 데이터 =====
        private GuardPlaceholder _selectedEnvoy;
        private EnvoySystem.EnvoyMission _selectedMission;
        private TerritoryId _currentTerritoryId;
        private string _selectedFoodItemId;
        private string _selectedFoodName;

        // ===== 결과 =====
        private EnvoySystem.EnvoyResult _lastResult;
        private float _resultTimer;

        // ===== 스타일 =====
        private GUIStyle _styleTitle;
        private GUIStyle _styleLabel;
        private GUIStyle _styleValue;
        private GUIStyle _styleWarning;

        private const float PANEL_WIDTH = 520f;
        private const float PANEL_HEIGHT = 540f;
        private const float LIST_ITEM_HEIGHT = 36f;

        private UIDesignTheme _theme;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            _theme = Phase33_Themes.EnvoyTheme();
        }

        private void Update()
        {
            // E키로 열기/닫기 토글 (영지 내에서만)
            if (Input.GetKeyDown(_openKey))
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player == null) return;

                // 현재 영역(territory) 확인
                TerritoryId? currentTerritory = GetCurrentTerritory(player.transform.position);
                if (currentTerritory == null)
                {
                    // 영지 밖이면 닫기
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

            // 결과 표시 타이머
            if (_currentStep == UIStep.Result)
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
            Color bgColor = _theme != null ? _theme.BgColor : new Color(0.05f, 0.05f, 0.15f, 0.92f);
            Color borderColor = _theme != null ? _theme.BorderColor : new Color(0.5f, 0.5f, 0.6f, 0.85f);
            var oldGuiColor = GUI.color;
            GUI.color = bgColor;
            GUI.Box(new Rect(x, y, PANEL_WIDTH, PANEL_HEIGHT), "");
            GUI.color = borderColor;
            GUI.Box(new Rect(x, y, PANEL_WIDTH, PANEL_HEIGHT), "");
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
                case UIStep.SelectEnvoy:
                    DrawEnvoySelection(x, ref cy);
                    break;
                case UIStep.SelectMission:
                    DrawMissionSelection(x, ref cy);
                    break;
                case UIStep.SelectPoisonFood:
                    DrawPoisonFoodSelection(x, ref cy);
                    break;
                case UIStep.Confirm:
                    DrawConfirmation(x, ref cy);
                    break;
                case UIStep.Result:
                    DrawResult(x, ref cy);
                    break;
            }
        }

        // ================================================================
        // 특사 선택 화면
        // ================================================================
        private void DrawEnvoySelection(float x, ref float cy)
        {
            float _startY = cy;
            string territoryName = GetTerritoryName(_currentTerritoryId);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"📜 {territoryName} — 특사 파견", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "파견할 특사를 선택하세요 (Lv.5+ 필요)", _styleLabel);
            cy += 26f;

            var availableEnvoys = GetAvailableEnvoysForUI();
            float listY = cy;
            float listH = PANEL_HEIGHT - (cy - _startY) - 70f;

            GUI.BeginGroup(new Rect(x + 10, listY, PANEL_WIDTH - 20, listH));

            if (availableEnvoys.Count == 0)
            {
                GUI.Label(new Rect(10, 10, PANEL_WIDTH - 40, 24),
                    "⚠️ 파견 가능한 특사가 없습니다.\nLv.5 이상 포섭된 병사가 필요합니다.", _styleWarning);
            }
            else
            {
                float viewH = availableEnvoys.Count * LIST_ITEM_HEIGHT;
                _scrollPos = GUI.BeginScrollView(
                    new Rect(0, 0, PANEL_WIDTH - 20, listH),
                    _scrollPos,
                    new Rect(0, 0, PANEL_WIDTH - 40, viewH)
                );

                for (int i = 0; i < availableEnvoys.Count; i++)
                {
                    var guard = availableEnvoys[i];
                    float iy = i * LIST_ITEM_HEIGHT;
                    GUI.Box(new Rect(0, iy, PANEL_WIDTH - 40, LIST_ITEM_HEIGHT - 2), "");

                    string roleStr = GuardStatusSystem.GetRoleName(guard.Role);
                    GUI.Label(new Rect(10, iy + 2, 225, 20),
                        $"{guard.GuardName} (Lv.{guard.Level})", _styleLabel);
                    GUI.Label(new Rect(10, iy + 20, 180, 16),
                        $"{roleStr} | 호감도 {guard.Loyalty:F0}", _styleValue);

                    if (GUI.Button(new Rect(PANEL_WIDTH - 170, iy + 4, 165, 28), "특사 선택"))
                    {
                        _selectedEnvoy = guard;
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
            float _startY = cy;
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                $"🎯 임무 선택 — {_selectedEnvoy.GuardName}", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                "파견할 임무를 선택하세요", _styleLabel);
            cy += 30f;

            var missions = new (EnvoySystem.EnvoyMission mission, int requiredLevel)[]
            {
                (EnvoySystem.EnvoyMission.Gift, EnvoySystem.GIFT_REQUIRED_LEVEL),
                (EnvoySystem.EnvoyMission.Friendship, EnvoySystem.FRIENDSHIP_REQUIRED_LEVEL),
                (EnvoySystem.EnvoyMission.Alliance, EnvoySystem.ALLIANCE_REQUIRED_LEVEL),
                (EnvoySystem.EnvoyMission.Assassinate, EnvoySystem.ASSASSINATE_REQUIRED_LEVEL)
            };

            foreach (var (mission, reqLv) in missions)
            {
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

                float miy = cy;
                GUI.Box(new Rect(x + 15, miy, PANEL_WIDTH - 30, 56), "");

                GUI.Label(new Rect(x + 25, miy + 4, PANEL_WIDTH - 50, 22),
                    $"{name}{lockStr}{detectStr}",
                    canDo ? _styleLabel : _styleWarning);
                GUI.Label(new Rect(x + 25, miy + 26, PANEL_WIDTH - 50, 20),
                    desc, _styleValue);

                if (canDo && GUI.Button(new Rect(x + PANEL_WIDTH - 130, miy + 12, 150, 30), "선택"))
                {
                    _selectedMission = mission;
                    if (mission == EnvoySystem.EnvoyMission.Assassinate)
                    {
                        _currentStep = UIStep.SelectPoisonFood;
                    }
                    else
                    {
                        _currentStep = UIStep.Confirm;
                    }
                    _scrollPos = Vector2.zero;
                    return;
                }

                cy = miy + 60f + 6f;
            }

            // 뒤로가기
            cy += 6f;
            if (GUI.Button(new Rect(x + PANEL_WIDTH / 2 - 60, cy, 180, 30), "← 뒤로"))
            {
                _currentStep = UIStep.SelectEnvoy;
                _selectedEnvoy = null;
                _scrollPos = Vector2.zero;
            }
        }

        // ================================================================
        // 독든 음식 선택 화면 (Phase 4.2 조합 시스템 연동)
        // ================================================================
        private void DrawPoisonFoodSelection(float x, ref float cy)
        {
            float _startY = cy;
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                "☠️ 독든 음식 선택", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"특사: {_selectedEnvoy.GuardName} → {GetTerritoryName(_currentTerritoryId)}", _styleLabel);
            cy += 26f;

            // 인벤토리에서 음식 아이템 목록 가져오기
            var foodItems = GetFoodItemsFromInventory();
            float listY = cy;
            float listH = PANEL_HEIGHT - (cy - _startY) - 80f;

            GUI.BeginGroup(new Rect(x + 10, listY, PANEL_WIDTH - 20, listH));

            if (foodItems.Count == 0)
            {
                GUI.Label(new Rect(10, 10, PANEL_WIDTH - 40, 24),
                    "⚠️ 보유한 음식 아이템이 없습니다.\n인벤토리에서 음식을 준비하세요.", _styleWarning);
            }
            else
            {
                float viewH = foodItems.Count * LIST_ITEM_HEIGHT;
                _scrollPos = GUI.BeginScrollView(
                    new Rect(0, 0, PANEL_WIDTH - 20, listH),
                    _scrollPos,
                    new Rect(0, 0, PANEL_WIDTH - 40, viewH)
                );

                for (int i = 0; i < foodItems.Count; i++)
                {
                    var pair = foodItems[i];
                    float iy = i * LIST_ITEM_HEIGHT;
                    GUI.Box(new Rect(0, iy, PANEL_WIDTH - 40, LIST_ITEM_HEIGHT - 2), "");

                    bool isPoisoned = IsItemPoisoned(pair.Key.id);
                    string poisonTag = isPoisoned ? " ☠️(독)" : "";

                    GUI.Label(new Rect(10, iy + 2, 270, 20),
                        $"{pair.Key.displayName}{poisonTag}", _styleLabel);
                    GUI.Label(new Rect(10, iy + 20, 120, 16),
                        $"x{pair.Value}", _styleValue);

                    if (GUI.Button(new Rect(PANEL_WIDTH - 170, iy + 4, 165, 28), "선택"))
                    {
                        _selectedFoodItemId = pair.Key.id;
                        _selectedFoodName = pair.Key.displayName;
                        _currentStep = UIStep.Confirm;
                        _scrollPos = Vector2.zero;
                        return;
                    }
                }

                GUI.EndScrollView();
            }

            GUI.EndGroup();

            // 뒤로가기
            float btnY = cy + listH + 6f;
            if (GUI.Button(new Rect(x + PANEL_WIDTH / 2 - 60, btnY, 180, 30), "← 뒤로"))
            {
                _currentStep = UIStep.SelectMission;
                _scrollPos = Vector2.zero;
            }
        }

        // ================================================================
        // 확인 화면
        // ================================================================
        private void DrawConfirmation(float x, ref float cy)
        {
            float _startY = cy;
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                "📋 파견 확인", _styleTitle);
            cy += 34f;

            // 특사 정보 (null 안전)
            string envoyName = _selectedEnvoy != null
                ? $"{_selectedEnvoy.GuardName} (Lv.{_selectedEnvoy.Level})"
                : "선택된 특사 없음";
            DrawInfoRow(x, ref cy, "특사:", envoyName);
            DrawInfoRow(x, ref cy, "목적지:", GetTerritoryName(_currentTerritoryId));
            DrawInfoRow(x, ref cy, "임무:", EnvoySystem.GetMissionName(_selectedMission));
            DrawInfoRow(x, ref cy, "임무 설명:", EnvoySystem.GetMissionDescription(_selectedMission));

            if (_selectedMission == EnvoySystem.EnvoyMission.Assassinate)
            {
                DrawInfoRow(x, ref cy, "음식:", _selectedFoodName);
            }

            // 발각 확률 표시
            float detectChance = _selectedEnvoy != null
                ? EnvoySystem.CalculateDetectChance(_selectedEnvoy, _currentTerritoryId)
                : 0f;
            string detectColor = detectChance >= 0.4f ? "🔴" : (detectChance >= 0.2f ? "🟡" : "🟢");
            DrawInfoRow(x, ref cy, "발각 위험:", $"{detectColor} {detectChance * 100:F0}%");

            // 이동 시간 표시 (거리 비례)
            float travelTime = CalculateTravelTime(_currentTerritoryId);
            int minutes = Mathf.FloorToInt(travelTime / 60f);
            int seconds = Mathf.FloorToInt(travelTime % 60f);
            string timeStr = minutes > 0 ? $"{minutes}분 {seconds}초" : $"{seconds}초";
            DrawInfoRow(x, ref cy, "이동 시간:", timeStr);

            cy += 12f;

            // 버튼
            if (GUI.Button(new Rect(x + 30, cy, 225, 36), "✅ 파견하기"))
            {
                ExecuteMission();
                return;
            }

            if (GUI.Button(new Rect(x + PANEL_WIDTH - 180, cy, 225, 36), "← 취소"))
            {
                Close();
            }
        }

        // ================================================================
        // 결과 화면
        // ================================================================
        private void DrawResult(float x, ref float cy)
        {
            float _startY = cy;
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 28),
                _lastResult.success ? "✅ 임무 완료" : "❌ 임무 실패", _styleTitle);
            cy += 34f;

            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 60),
                _lastResult.message, _styleLabel);
            cy += 70f;

            if (_lastResult.success)
            {
                DrawInfoRow(x, ref cy, "호감도 변화:",
                    _lastResult.loyaltyChange >= 0
                        ? $"+{_lastResult.loyaltyChange}"
                        : $"{_lastResult.loyaltyChange}");
            }

            if (_lastResult.detected)
            {
                GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 24),
                    "⚠️ 발각! 특사가 처형되었습니다.", _styleWarning);
                cy += 28f;
            }

            cy += 20f;
            float remaining = Mathf.Max(0, _resultTimer);
            GUI.Label(new Rect(x + 15, cy, PANEL_WIDTH - 30, 22),
                $"({remaining:F1}초 후 창 닫힘)", _styleValue);
        }

        // ================================================================
        // 공개 API
        // ================================================================

        /// <summary>특사 파견 UI 열기</summary>
        public void Open()
        {
            _isVisible = true;
            _currentStep = UIStep.SelectEnvoy;
            _selectedEnvoy = null;
            _selectedMission = EnvoySystem.EnvoyMission.Gift;
            _selectedFoodItemId = null;
            _selectedFoodName = null;
            _scrollPos = Vector2.zero;
        }

        /// <summary>특사 파견 UI 닫기</summary>
        public void Close()
        {
            _isVisible = false;
            _selectedEnvoy = null;
            _selectedFoodItemId = null;
            _selectedFoodName = null;
            _currentStep = UIStep.SelectEnvoy;
            _scrollPos = Vector2.zero;
        }

        /// <summary>UI 표시 여부</summary>
        public bool IsVisible => _isVisible;

        // ================================================================
        // 내부 로직
        // ================================================================

        private void ExecuteMission()
        {
            // 인벤토리에서 아이템 제거 (독살 시)
            if (_selectedMission == EnvoySystem.EnvoyMission.Assassinate
                && !string.IsNullOrEmpty(_selectedFoodItemId))
            {
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.RemoveItem(_selectedFoodItemId);
            }

            _lastResult = EnvoySystem.SendEnvoy(
                _selectedEnvoy,
                _currentTerritoryId,
                _selectedMission
            );

            _currentStep = UIStep.Result;
            _resultTimer = 5f; // 5초 후 자동 닫힘

            Debug.Log($"[EnvoyMissionUI] 특사 파견 결과: {_lastResult.message}");
        }

        private List<GuardPlaceholder> GetAvailableEnvoysForUI()
        {
            var result = new List<GuardPlaceholder>();
            var guards = EnvoySystem.GetAvailableEnvoys();
            foreach (var g in guards)
            {
                // 특사 UI에서는 Lv.5+만 표시 (Gift가 최소 요구 레벨)
                if (g.Level >= EnvoySystem.GIFT_REQUIRED_LEVEL)
                    result.Add(g);
            }
            return result;
        }

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

        /// <summary>
        /// 음식 아이템에 독이 첨가되었는지 확인 (Phase 4.2 조합 시스템 연동)
        /// 현재는 PoisonTakeoverSystem의 독 처리 상태를 확인
        /// </summary>
        private bool IsItemPoisoned(string itemId)
        {
            // Phase 4.2: PoisonTakeoverSystem 또는 CookingSystem에 독 조합 확인 로직 연동
            // 현재는 간단히 false 반환 (향후 확장)
            // Phase 4.2 integration - currently disabled
            if (false) { }
            return false;
        }

        /// <summary>
        /// 이동 시간 계산 — 영지 간 거리 비례
        /// TerritoryManager의 중심점 거리 사용
        /// </summary>
        private float CalculateTravelTime(TerritoryId targetId)
        {
            if (TerritoryManager.Instance == null)
                return 30f; // 기본 30초

            Vector3 currentCenter = TerritoryManager.Instance.GetTerritoryCenter();
            Vector3 targetCenter = TerritoryManager.Instance.GetTerritoryCenter(targetId);

            float distance = Vector3.Distance(currentCenter, targetCenter);
            // 거리 1당 _travelSpeed초, 최소 5초
            float time = distance * _travelSpeed;
            return Mathf.Max(5f, time);
        }

        /// <summary>현재 위치의 영지 ID 반환</summary>
        private TerritoryId? GetCurrentTerritory(Vector3 position)
        {
            if (TerritoryManager.Instance != null)
            {
                TerritoryId currentId = TerritoryManager.Instance.CurrentTerritoryId;
                if (TerritoryDatabase.Instance != null)
                {
                    var def = TerritoryDatabase.Instance.GetDefinition(currentId);
                    if (def != null && def.territoryName != null)
                    {
                        return currentId;
                    }
                }
            }
            else
            {
                return null;
            }

            // TerritoryManager에 없으면 가장 가까운 영지 찾기
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
            if (TerritoryDatabase.Instance == null)
                return "알 수 없는 영지";
            var def = TerritoryDatabase.Instance.GetDefinition(id);
            return def != null && def.territoryName != null ? def.territoryName : "알 수 없는 영지";
        }

        // ================================================================
        // UI 헬퍼
        // ================================================================

        private void DrawInfoRow(float x, ref float cy, string label, string value)
        {
            GUI.Label(new Rect(x + 20, cy, 150, 22), label, _styleLabel);
            GUI.Label(new Rect(x + 120, cy, PANEL_WIDTH - 140, 22), value, _styleValue);
            cy += 26f;
        }

        private void EnsureStyles()
        {
            if (_styleTitle != null) return;

            _styleTitle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 64,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                normal = { textColor = Color.white },
                alignment = TextAnchor.MiddleLeft
            };

            _styleValue = new GUIStyle(GUI.skin.label)
            {
                fontSize = 48,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.yellow },
                alignment = TextAnchor.MiddleLeft
            };

            _styleWarning = new GUIStyle(GUI.skin.label)
            {
                fontSize = 52,
                fontStyle = FontStyle.Bold,
                normal = { textColor = Color.red },
                alignment = TextAnchor.MiddleLeft
            };
        }
    }
}