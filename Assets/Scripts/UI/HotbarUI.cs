using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 퀵슬롯 핫바 UI (Phase M1) — 8슬롯, 1~8키, 장비 장착 토글.
    ///
    /// [스펙 (예시 스크린샷 분석)]
    /// - 1080p 하단 중앙, 다크 반투명 배경 + 라운드 코너
    /// - 슬롯 96px, 하단에 숫자(1~8) 소형 박스
    /// - 선택(장착 중) 슬롯 = 흰 두꺼운 테두리 하이라이트
    /// - 우상단 스택 수량 소형 숫자 (소비 아이템)
    ///
    /// [구현 방식]
    /// - InventoryWindow와 같은 "코드 생성 UI" 방식 (프리팹 불필요).
    ///   InventoryWindow는 IMGUI이지만, 핫바는 상시 노출 + 외곽 프레임 표현을 위해
    ///   uGUI(Canvas 하위, 런타임 코드 생성)로 구성. Canvas는 씬 편집 없이 부트스트랩에서 자동 생성.
    /// - 라운드 코너: 9-Slice용 라운드 스프라이트를 코드로 프로시저럴 생성 (에셋 의존성 0).
    ///
    /// [v1 슬롯 구성 (고정)]
    ///   [0] 검  steel_sword  → WeaponEquipManager.Equip("steel", player, Sword) / 재입력 시 Unequip
    ///   [1] 활  crystal_bow  → WeaponEquipManager.Equip("crystal", player, Bow) / 재입력 시 Unequip (M4)
    ///   [2] 창  wood_spear   → WeaponEquipManager.Equip("wood", player, Spear) / 재입력 시 Unequip (M4)
    ///   [3] 폭탄 bomb(소비)   → 인벤 수량 확인 후 투척 모드 진입 — PlayerWeaponModeBridge.ThrowSelected (M4)
    ///   [4~7] 빈 슬롯 (확장 예약)
    ///
    /// [소유권 주의] WeaponEquipManager는 타 에이전트 소유 — 본 파일은 호출만 수행(수정 금지).
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HotbarUI _instance;

        /// <summary>
        /// 씬 편집 없이 상시 핫바를 띄우기 위한 셀프 부트스트랩.
        /// 선례: UI/DynamicEventUI.cs의 RuntimeInitializeOnLoadMethod 패턴.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var existing = Object.FindAnyObjectByType<HotbarUI>();
            if (existing != null) { _instance = existing; return; }

            var go = new GameObject("HotbarUI");
            _instance = go.AddComponent<HotbarUI>();
        }

        // ===== 2026-09-11: Tab 토글용 표시 전환 (GuardSquadHotbar가 호출 — 하단 중앙 자리 공유) =====
        /// <summary>
        /// 아이템 핫바 표시/숨김 (인스턴스 미존재 시 안전 no-op).
        /// GO를 비활성화하므로 Update 정지 → 1~8 숫자키 처리(HandleNumberKeys)도 함께 차단된다.
        /// </summary>
        public static void SetVisible(bool visible)
        {
            if (_instance == null) return;
            if (_instance.gameObject.activeSelf == visible) return;
            _instance.gameObject.SetActive(visible);
        }

        /// <summary>아이템 핫바가 현재 표시 중인지.</summary>
        public static bool IsVisible => _instance != null && _instance.gameObject.activeSelf;

        // ===== 슬롯 정의 (v1 고정 8슬롯) =====
        private enum SlotKind { Weapon, Consumable, Empty }

        private class SlotDef
        {
            public SlotKind kind;
            public string specId;    // 스펙상 아이템 id (예: steel_sword)
            public string label;     // 슬롯 중앙 표시 텍스트 (v1: 아이콘 미구현 → 글자 대체)
            public string equipId;   // WeaponEquipManager.Equip에 넘길 id (검만: {id}_sword GLB)
            public WeaponType weaponType; // M4: 장착 타입 — 매니저가 타입별 GLB 접미사(_sword/_bow/_spear) 결정
        }

        private static readonly SlotDef[] SlotDefs = new SlotDef[8]
        {
            new SlotDef{ kind = SlotKind.Weapon,     specId = "steel_sword", label = "검", equipId = "steel",   weaponType = WeaponType.Sword },
            new SlotDef{ kind = SlotKind.Weapon,     specId = "crystal_bow", label = "활", equipId = "crystal", weaponType = WeaponType.Bow   }, // M4: {crystal}_bow GLB
            new SlotDef{ kind = SlotKind.Weapon,     specId = "wood_spear",  label = "창", equipId = "wood",    weaponType = WeaponType.Spear }, // M4: {wood}_spear GLB
            new SlotDef{ kind = SlotKind.Consumable, specId = "bomb",        label = "폭", equipId = null     }, // M4: 투척 모드 — PlayerWeaponModeBridge 연결
            new SlotDef{ kind = SlotKind.Empty }, // [4] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [5] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [6] 확장 예약
            new SlotDef{ kind = SlotKind.Empty }, // [7] 확장 예약
        };

        // ===== 레이아웃 상수 (1080p 디자인 기준 — CanvasScaler가 해상도 스케일) =====
        private const int   SlotCount      = 8;
        private const float SlotSize       = 96f;   // 슬롯 한 변
        private const float SlotGap        = 10f;   // 슬롯 간격
        private const float PanelPadX      = 14f;   // 패널 좌우 여백
        private const float NumBoxHeight   = 24f;   // 하단 숫자 박스 높이
        private const float NumBoxGap      = 6f;    // 슬롯~숫자박스 간격
        private const float PanelPadTop    = 12f;
        private const float PanelPadBottom = 12f;
        private const float BottomMargin   = 12f;   // 화면 하단에서 패널까지 거리

        private const float PanelWidth  = PanelPadX * 2f + SlotSize * SlotCount + SlotGap * (SlotCount - 1); // 866
        private const float PanelHeight = PanelPadTop + SlotSize + NumBoxGap + NumBoxHeight + PanelPadBottom; // 150

        // ===== 다크 테마 색상 (InventoryWindow 톤 유지) =====
        private static readonly Color ColorPanelBg   = new Color(0.06f, 0.06f, 0.08f, 0.72f); // 패널 다크 반투명
        private static readonly Color ColorSlotBg    = new Color(0.13f, 0.13f, 0.16f, 0.88f); // 슬롯 배경
        private static readonly Color ColorSlotEmpty = new Color(0.13f, 0.13f, 0.16f, 0.40f); // 빈 슬롯(어둡게)
        private static readonly Color ColorSelect    = new Color(1f, 1f, 1f, 0.95f);          // 흰 두꺼운 선택 테두리
        private static readonly Color ColorKeyBox    = new Color(0.10f, 0.10f, 0.12f, 0.90f); // 숫자 박스
        private static readonly Color ColorText      = new Color(0.88f, 0.88f, 0.88f, 1f);
        private static readonly Color ColorTextDim   = new Color(0.70f, 0.70f, 0.74f, 0.9f);
        private static readonly Color ColorCount     = new Color(1f, 0.95f, 0.75f, 1f);       // 수량(연금색 소형)

        // ===== 런타임 상태 =====
        private Canvas _canvas;
        private Font _font;
        private Sprite _roundedSprite;

        private readonly Image[] _slotBgs       = new Image[SlotCount];
        private readonly Image[] _selectBorders = new Image[SlotCount]; // 흰 두꺼운 테두리 (장착 중만 표시)
        private readonly Text[]  _countTexts    = new Text[SlotCount];  // 우상단 수량
        private float _countRefreshTimer;

        private Transform _cachedPlayerT;
        private float _playerCacheTime;
        private string _lastSyncedEquipId = "__none__"; // CurrentId 변화 감지용

        // ===== 생명주기 =====
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;

            BuildCanvas();
            BuildPanel();
            LoadAssignedItems(); // 2026-09-09: 저장된 핫바 지정 복원

            // 씬 전환에도 핫바 유지 (상시 UI)
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            HandleNumberKeys();
            SyncSelectionHighlight();
            RefreshCountsPeriodically();
        }

        // ===== 입력: Alpha1~Alpha8 =====
        private void HandleNumberKeys()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                // KeyCode.Alpha1(49)부터 연속 배치 → Alpha1 + i
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    ActivateSlot(i);
            }
        }

        /// <summary>슬롯 실행 (키 1~8 / 추후 클릭 확장)</summary>
        public void ActivateSlot(int index)
        {
            if (index < 0 || index >= SlotCount) return;

            // 2026-09-09: 인벤 드래그 지정 아이템 우선 (무기류는 장착 연동)
            string assigned = _assignedIds[index];
            if (!string.IsNullOrEmpty(assigned))
            {
                ActivateAssignedItem(index, assigned);
                return;
            }

            SlotDef def = SlotDefs[index];

            switch (def.kind)
            {
                case SlotKind.Weapon:
                    ActivateWeaponSlot(index, def);
                    break;

                case SlotKind.Consumable:
                    ActivateConsumableSlot(index, def);
                    break;

                case SlotKind.Empty:
                default:
                    Debug.Log($"[HotbarUI] 슬롯 {index + 1}: 빈 슬롯 (확장 예약, Phase M4)");
                    break;
            }
        }

        /// <summary>
        /// 장비 슬롯 — WeaponEquipManager.Equip(equipId, player, weaponType) 호출, 다시 누르면 Unequip(토글).
        /// equipId는 GLB 경로 결합용 기본 이름(steel/crystal/wood) — 매니저가 타입별 접미사 _sword/_bow/_spear를 붙임.
        /// </summary>
        private void ActivateWeaponSlot(int index, SlotDef def)
        {
            if (string.IsNullOrEmpty(def.equipId))
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 장착 id 미정 — 스킵");
                return;
            }

            // M4: 무기 장착 시 투척 모드 해제 (모드 상호배타)
            ProjectName.Systems.PlayerWeaponModeBridge.ThrowSelected = false;

            // 토글: 같은 무기가 이미 장착 중이면 해제
            if (WeaponEquipManager.CurrentId == def.equipId && WeaponEquipManager.IsEquipped)
            {
                WeaponEquipManager.Unequip();
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 장착 해제 (토글)");
                SyncSelectionHighlight();
                return;
            }

            Transform playerT = GetPlayerTransform();
            if (playerT == null)
            {
                Debug.LogWarning("[HotbarUI] 플레이어를 찾지 못해 장착 스킵");
                return;
            }

            WeaponEquipManager.Equip(def.equipId, playerT, def.weaponType);
            SyncSelectionHighlight();
        }

        /// <summary>
        /// 소비 슬롯 — 인벤 수량 확인 후 사용/투척 준비.
        /// v1은 로그 전용 (투척은 BombThrower/ConsumableSystem 연결이 필요 → Phase M4).
        /// </summary>
        private void ActivateConsumableSlot(int index, SlotDef def)
        {
            // TODO(M4): BombThrower.Throw() / ConsumableSystem.Use 연결
            var inv = PlayerInventory.Instance;
            int count = inv != null ? inv.GetItemCount(def.specId) : 0;

            if (count <= 0)
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 보유 수량 0 — 사용 불가");
                return;
            }

            // M4: 폭탄 투척 모드 진입 — 폭탄 소모는 발사 시점에 처리 예정
            ProjectName.Systems.PlayerWeaponModeBridge.ThrowSelected = true;
            ProjectName.Systems.WeaponEquipManager.Unequip(); // 다른 무기 해제(모드 상호배타)
            Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{def.specId}': 투척 모드 진입 (보유 x{count}) — 폭탄 소모는 발사 시점에 처리 예정");
        }

        // ===== 2026-09-09: 인벤 드래그 지정 슬롯 (설명 패널 미니패드 → AssignItem) =====
        private static readonly string[] _assignedIds = new string[SlotCount];
        private static readonly string[] _assignedNames = new string[SlotCount];
        private Text[] _assignedTexts = new Text[SlotCount];
        private static readonly Dictionary<string, (string equipId, WeaponType type)> _assignedWeaponMap =
            new Dictionary<string, (string, WeaponType)>
            {
                { "steel_sword", ("steel", WeaponType.Sword) },
                { "iron_sword", ("iron", WeaponType.Sword) },
                { "crystal_bow", ("crystal", WeaponType.Bow) },
                { "wood_bow", ("wood", WeaponType.Bow) },
                { "wood_spear", ("wood", WeaponType.Spear) },
                { "spear", ("wood", WeaponType.Spear) },
            };

        /// <summary>
        /// 2026-09-09(3): 화면 좌표가 속한 핫바 슬롯 인덱스 반환 (인벤 드래그 드롭 판정용).
        /// 오버레이 캔버스이므로 카메라 null. 미해당 시 -1.
        /// </summary>
        public static int GetSlotIndexAtScreenPoint(Vector2 screenPos)
        {
            if (_instance == null) return -1;
            // 2026-09-11: 부대 핫바 모드에서 숨겨진 핫바로의 드래그 드롭 판정 제외 (신규 모드에서의 혼선 방지)
            if (!_instance.gameObject.activeSelf) return -1;
            for (int i = 0; i < SlotCount; i++)
            {
                var bg = _instance._slotBgs[i];
                if (bg != null && RectTransformUtility.RectangleContainsScreenPoint(bg.rectTransform, screenPos, null))
                    return i;
            }
            return -1;
        }

        /// <summary>인벤토리 드래그로 슬롯에 아이템 지정 (InventoryWindow 설명 패널에서 호출). 무기류는 장착 연동, 그 외 표시 전용.</summary>
        public static void AssignItem(int index, string itemId, string displayName)
        {
            if (index < 0 || index >= SlotCount || string.IsNullOrEmpty(itemId)) return;
            _assignedIds[index] = itemId;
            _assignedNames[index] = displayName ?? itemId;
            PlayerPrefs.SetString($"poison_hotbar_{index}", itemId);
            PlayerPrefs.SetString($"poison_hotbar_name_{index}", _assignedNames[index]);
            PlayerPrefs.Save();
            if (_instance != null) _instance.ApplyAssignedVisual(index);
        }

        private void ApplyAssignedVisual(int index)
        {
            if (_assignedTexts[index] != null)
                _assignedTexts[index].text = _assignedNames[index] ?? string.Empty;
        }

        private void LoadAssignedItems()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _assignedIds[i] = PlayerPrefs.GetString($"poison_hotbar_{i}", null);
                _assignedNames[i] = PlayerPrefs.GetString($"poison_hotbar_name_{i}", null);
                ApplyAssignedVisual(i);
            }
        }

        private void ActivateAssignedItem(int index, string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            // ① 무기 — 기존 짧은 id 맵 + 신규 full-id(weapon_{type}_{tier}) 파싱 (우클릭 장착과 동일 규칙)
            //    무기는 모델/스탯 부착 방식이라 인벤 소유 확인 없이 토글 장착 (기존 동작 유지)
            if (TryResolveWeapon(itemId, out string equipId, out WeaponType wType))
            {
                // 무기 장착 시 투척 모드 해제 (모드 상호배타)
                ProjectName.Systems.PlayerWeaponModeBridge.ThrowSelected = false;
                Transform playerT = GetPlayerTransform();
                if (playerT == null)
                {
                    Debug.LogWarning("[HotbarUI] 플레이어를 찾지 못해 장착 스킵");
                    return;
                }
                if (WeaponEquipManager.CurrentId == equipId && WeaponEquipManager.IsEquipped)
                {
                    WeaponEquipManager.Unequip();
                    Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 무기 장착 해제 (토글)");
                }
                else
                {
                    WeaponEquipManager.Equip(equipId, playerT, wType);
                    Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 무기 장착 ({equipId}/{wType})");
                }
                SyncSelectionHighlight();
                return;
            }

            var inv = PlayerInventory.Instance;
            if (inv == null)
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 인벤토리 없음 — 스킵");
                return;
            }

            // 2026-09-11(7): 숫자키 장착/사용 — 인벤 소유 확인 후 카테고리 분기
            PlayerInventory.ItemSlot invSlot = FindInventorySlot(inv, itemId, out int globalIndex);
            if (invSlot == null)
            {
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 인벤에 아이템 없음 — 스킵");
                return;
            }

            // ② 방어구 — EquipmentManager.EquipItem (장착 중이면 해제 토글). 장착 시 인벤에서 1개 제거됨.
            if (invSlot.item.category == PlayerInventory.ItemCategory.Armor)
            {
                var em = ProjectName.Systems.EquipmentManager.Instance;
                if (em == null)
                {
                    Debug.LogWarning("[HotbarUI] 방어구 장착 실패 — EquipmentManager 없음");
                    return;
                }
                var equipSlot = MapArmorSlot(itemId);
                if (em.GetItemId(equipSlot) == itemId)
                {
                    if (em.UnequipSlot(equipSlot))
                        Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 방어구 해제 (토글 — {equipSlot})");
                    else
                        Debug.LogWarning($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 방어구 해제 실패 (인벤 가득)");
                }
                else if (em.EquipItem(invSlot, equipSlot))
                {
                    Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 방어구 장착 ({equipSlot})");
                }
                else
                {
                    Debug.LogWarning($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 방어구 장착 실패 (기존 장비 해제 실패 or 인벤 가득)");
                }
                // 장착/해제는 인벤 슬롯을 변경 — 인벤 창 열려 있으면 그리드 즉시 동기화
                if (InventoryWindow.Instance != null && InventoryWindow.Instance.IsOpen)
                    InventoryWindow.Instance.RefreshInventory();
                return;
            }

            // ③ 소모성 — PlayerInventory.UseItem (ConsumableSystem: Food/Potion/Drug만 효과 적용 → 해당 3종만 허용,
            //    그 외 카테고리는 무효 소모 방지 위해 스킵). QuickSlotUI의 키 사용 선례와 동일 경로.
            if (invSlot.item.category == PlayerInventory.ItemCategory.Food
                || invSlot.item.category == PlayerInventory.ItemCategory.Potion
                || invSlot.item.category == PlayerInventory.ItemCategory.Drug)
            {
                inv.UseItem(globalIndex);
                Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 사용 (남은 수량 {inv.GetItemCount(itemId)})");
                return;
            }

            Debug.Log($"[HotbarUI] 슬롯 {index + 1} '{itemId}': 장착/사용 불가 카테고리 ({invSlot.item.category}) — 표시 전용");
        }

        /// <summary>
        /// 2026-09-11(7): 무기 id → (equipId, WeaponType) 해석 — InventoryWindow.TryResolveWeaponEquip와 동일 규칙.
        /// 신규 full-id(weapon_{type}_{tier}, dagger 포함)는 전체 id를 그대로 전달
        /// (WeaponEquipManager._itemIdToGlb 매핑이 GLB명 결정, WeaponData.GetTierMultiplier가 티어 토큰 인식).
        /// </summary>
        private static bool TryResolveWeapon(string itemId, out string equipId, out WeaponType type)
        {
            if (_assignedWeaponMap.TryGetValue(itemId, out var w))
            {
                equipId = w.equipId;
                type = w.type;
                return true;
            }
            string s = (itemId ?? string.Empty).ToLowerInvariant();
            var parts = s.Split('_');
            if (parts.Length == 3 && parts[0] == "weapon")
            {
                switch (parts[1])
                {
                    case "bow":   type = WeaponType.Bow;   break;
                    case "spear": type = WeaponType.Spear; break;
                    default:      type = WeaponType.Sword; break;   // sword/dagger 등 근접 → Sword
                }
                equipId = s;
                return true;
            }
            // 기존 폴백: 티어 토큰 추출 (예: iron_sword → iron)
            type = s.Contains("bow") ? WeaponType.Bow
                 : s.Contains("spear") ? WeaponType.Spear
                 : WeaponType.Sword;
            equipId = null;
            foreach (var p in parts)
            {
                if (p == "weapon" || p == "sword" || p == "bow" || p == "spear" || p == "dagger") continue;
                if (p == "steel" || p == "iron" || p == "crystal" || p == "wood" || p == "stone") { equipId = p; break; }
            }
            if (string.IsNullOrEmpty(equipId) && parts.Length > 1)
                equipId = parts[parts.Length - 1];
            return !string.IsNullOrEmpty(equipId);
        }

        /// <summary>인벤에서 itemId에 해당하는 첫 슬롯을 반환 (전역 인덱스 out). 없으면 null.</summary>
        private static PlayerInventory.ItemSlot FindInventorySlot(PlayerInventory inv, string itemId, out int globalIndex)
        {
            globalIndex = -1;
            var all = inv.GetAllSlots();
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
            {
                var s = all[i];
                if (s != null && s.item != null && s.item.id == itemId)
                {
                    globalIndex = i;
                    return s;
                }
            }
            return null;
        }

        /// <summary>방어구 id → EquipmentSlot (InventoryWindow.MapArmorSlot과 동일 규칙).</summary>
        private static ProjectName.Systems.EquipmentManager.EquipmentSlot MapArmorSlot(string id)
        {
            string s = (id ?? "").ToLowerInvariant();
            if (s.Contains("helmet") || s.Contains("투구")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Helmet;
            if (s.Contains("shoe") || s.Contains("boot") || s.Contains("신발")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Shoes;
            if (s.Contains("glove") || s.Contains("장갑")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Gloves;
            if (s.Contains("cape") || s.Contains("망토") || s.EndsWith("_back")) return ProjectName.Systems.EquipmentManager.EquipmentSlot.Back;
            return ProjectName.Systems.EquipmentManager.EquipmentSlot.Armor;
        }

        // ===== 장착 상태 ↔ 하이라이트 동기화 =====
        private void SyncSelectionHighlight()
        {
            string currentId = WeaponEquipManager.IsEquipped ? WeaponEquipManager.CurrentId : null;
            if (currentId == _lastSyncedEquipId) return;
            _lastSyncedEquipId = currentId;

            for (int i = 0; i < SlotCount; i++)
            {
                var border = _selectBorders[i];
                if (border == null) continue;

                SlotDef def = SlotDefs[i];
                bool selected = def.kind == SlotKind.Weapon
                                && !string.IsNullOrEmpty(def.equipId)
                                && def.equipId == currentId;
                border.enabled = selected;
            }
        }

        // ===== 수량 텍스트 갱신 (0.5초 폴링 — 이벤트 시스템 미구현) =====
        private void RefreshCountsPeriodically()
        {
            _countRefreshTimer += Time.unscaledDeltaTime;
            if (_countRefreshTimer < 0.5f) return;
            _countRefreshTimer = 0f;

            var inv = PlayerInventory.Instance;
            for (int i = 0; i < SlotCount; i++)
            {
                var txt = _countTexts[i];
                if (txt == null) continue;

                SlotDef def = SlotDefs[i];
                if (def.kind == SlotKind.Consumable && inv != null)
                {
                    int count = inv.GetItemCount(def.specId);
                    txt.text = count > 0 ? $"x{count}" : string.Empty;
                }
                else
                {
                    txt.text = string.Empty;
                }
            }
        }

        // ===== 플레이어 Transform (InventoryWindow.GetPlayerTransform 선례 동일) =====
        private Transform GetPlayerTransform()
        {
            if (_cachedPlayerT != null && Time.unscaledTime - _playerCacheTime < 1f)
                return _cachedPlayerT;

            _cachedPlayerT = null;
            var pm = FindAnyObjectByType<PlayerMovement>();
            if (pm != null) _cachedPlayerT = pm.transform;
            if (_cachedPlayerT == null)
            {
                var tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null) _cachedPlayerT = tagged.transform;
            }
            _playerCacheTime = Time.unscaledTime;
            return _cachedPlayerT;
        }

        // =====================================================================
        //  UI 빌드 (코드 생성 — 프리팹/씬 편집 불필요)
        // =====================================================================

        private void BuildCanvas()
        {
            var canvasGO = new GameObject("HotbarCanvas");
            canvasGO.transform.SetParent(transform, false);

            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100; // 상시 UI (IMGUI 윈도우가 항상 위에 그려짐)

            // 1080p 디자인 해상도 기준 스케일
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            // 라운드 코너 스프라이트 (프로시저럴, 에셋 의존성 0)
            _roundedSprite = CreateRoundedSprite(64, 16);
            _font = LoadBuiltinFont();
        }

        private void BuildPanel()
        {
            RectTransform panel = CreateImage(
                _canvas.transform, "HotbarPanel", _roundedSprite, ColorPanelBg,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, BottomMargin), new Vector2(PanelWidth, PanelHeight));
            panel.pivot = new Vector2(0.5f, 0f); // 하단 중앙 기준 (CreateImage 기본 pivot(0,0)은 좌하단 기준 → 좌측 절반이 화면 중앙 기준으로 밀림)
            panel.GetComponent<Image>().type = Image.Type.Sliced;

            float slotTop = PanelPadTop;
            float slotBottom = slotTop + SlotSize;
            float numBoxY = slotBottom + NumBoxGap;

            for (int i = 0; i < SlotCount; i++)
            {
                SlotDef def = SlotDefs[i];
                bool isEmpty = def.kind == SlotKind.Empty;

                float x = PanelPadX + i * (SlotSize + SlotGap);

                // ① 흰 두꺼운 선택 테두리 (슬롯보다 8px 크게 뒤에 깔림 → 장착 중일 때만 표시)
                RectTransform border = CreateImage(
                    panel, $"Slot{i}_Select", _roundedSprite, ColorSelect,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x - 8f, slotTop - 8f), new Vector2(SlotSize + 16f, SlotSize + 16f));
                var borderImg = border.GetComponent<Image>();
                borderImg.type = Image.Type.Sliced;
                borderImg.raycastTarget = false;
                borderImg.enabled = false; // 초기 비장착
                _selectBorders[i] = borderImg;

                // ② 슬롯 배경 (다크 반투명, 라운드)
                RectTransform bg = CreateImage(
                    panel, $"Slot{i}_Bg", _roundedSprite, isEmpty ? ColorSlotEmpty : ColorSlotBg,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(x, slotTop), new Vector2(SlotSize, SlotSize));
                var bgImg = bg.GetComponent<Image>();
                bgImg.type = Image.Type.Sliced;
                bgImg.raycastTarget = false;
                _slotBgs[i] = bgImg;

                // ③ 중앙 라벨 — 2026-09-09 제거(유저 요청): 슬롯 내 텍스트 표기 폐지
                //    (지정 아이템명은 좌상단 소형 텍스트로 표시 — AssignItem 참조)
                var assignedLabel = CreateText(
                    bg, $"Slot{i}_Assigned", string.Empty, 15, ColorCount,
                    TextAnchor.UpperLeft, new Vector2(6f, -4f),
                    new Vector2(SlotSize - 12f, 20f));
                assignedLabel.GetComponent<Text>().raycastTarget = false;
                _assignedTexts[i] = assignedLabel.GetComponent<Text>();

                // ④ 우상단 수량 (소비 아이템만 갱신됨)
                if (def.kind == SlotKind.Consumable)
                {
                    RectTransform count = CreateText(
                        bg, $"Slot{i}_Count", string.Empty, 20, ColorCount,
                        TextAnchor.UpperRight, new Vector2(-6f, -4f),
                        new Vector2(70f, 24f), new Vector2(1f, 1f), new Vector2(1f, 1f));
                    count.GetComponent<Text>().raycastTarget = false;
                    _countTexts[i] = count.GetComponent<Text>();
                }

                // ⑤ 하단 숫자 박스 (1~8) — FIX: 앵커 (0,0)-(0.5,0)는 스트레치 모드라 sizeDelta가 앵커 rect 폭에 가산되어
                // 박스가 비정상적으로 커지고 우측으로 밀려 패널 밖까지 이어진 '유령 선'의 원인. 슬롯 BG와 동일한 점앵커로 통일.
                float numX = x + SlotSize * 0.5f;
                RectTransform keyBox = CreateImage(
                    panel, $"Slot{i}_KeyBox", _roundedSprite, ColorKeyBox,
                    new Vector2(0f, 0f), new Vector2(0f, 0f),
                    new Vector2(numX - 13f, numBoxY), new Vector2(26f, NumBoxHeight));
                var keyBoxImg = keyBox.GetComponent<Image>();
                keyBoxImg.type = Image.Type.Sliced;
                keyBoxImg.raycastTarget = false;

                RectTransform keyText = CreateText(
                    keyBox, $"Slot{i}_KeyText", (i + 1).ToString(), 16, ColorTextDim,
                    TextAnchor.MiddleCenter, new Vector2(13f, NumBoxHeight * 0.5f),
                    new Vector2(26f, NumBoxHeight));
                keyText.GetComponent<Text>().raycastTarget = false;
            }
        }

        // ===== 빌드 헬퍼 =====

        private static RectTransform CreateImage(
            Transform parent, string name, Sprite sprite, Color color,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = Vector2.zero; // anchoredPos = 좌하단 모서리 기준
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return rt;
        }

        private RectTransform CreateText(
            Transform parent, string name, string text, int fontSize, Color color,
            TextAnchor alignment, Vector2 anchoredPos, Vector2 size,
            Vector2 anchorPoint = default, Vector2 pivot = default)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = anchorPoint; // default (0,0) = 부모 좌하단 기준
            rt.pivot = pivot;                          // default (0.5,0.5) = anchoredPos가 중심
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.text = text;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = alignment;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.raycastTarget = false;
            return rt;
        }

        /// <summary>9-Slice용 라운드 사각 스프라이트 생성 (라운드 코너 + 슬라이스 확장).</summary>
        private static Sprite CreateRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[size * size];
            float radiusF = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    bool inside = IsInsideRoundedRect(px, py, size, radiusF);
                    // 코너 경계 1px 안티앨리어싱 (부드러운 라운드)
                    float alpha = inside ? 255f : 0f;
                    if (!inside && radiusF > 0f)
                    {
                        // 코너 바깥 1px 내부는 부분 알파로 부드럽게
                        float dist = DistanceToRoundedRect(px, py, size, radiusF);
                        if (dist < 1f) alpha = (1f - dist) * 255f;
                    }
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            int b = radius; // 9-Slice 보더 = 코너 반경
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f),
                size, 0u, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static bool IsInsideRoundedRect(float px, float py, float size, float radius)
        {
            return DistanceToRoundedRect(px, py, size, radius) <= 0f;
        }

        /// <summary>라운드 사각형 "바깥"까지의 거리(내부/경계는 0 이하) — 부드러운 코너용.</summary>
        private static float DistanceToRoundedRect(float px, float py, float size, float radius)
        {
            float maxX = size - radius;
            float cx = Mathf.Clamp(px, radius, maxX);
            float cy = Mathf.Clamp(py, radius, maxX);
            return Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy)) - radius;
        }

        /// <summary>폰트 로드 — P7-1: 한글 서포트 커스텀 폰트 우선(UIFont 캐시), 실패 시 빌트인 폴백.</summary>
        private static Font LoadBuiltinFont()
        {
            var uiFont = ProjectName.UI.UIFont.Load(); // NotoSansKR → malgun → 빌트인 (static 캐시)
            if (uiFont != null) return uiFont;

            try
            {
                var f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (f != null) return f;
            }
            catch { /* 구버전 Unity 폴백 */ }

            try
            {
                return Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            catch
            {
                Debug.LogWarning("[HotbarUI] 빌트인 폰트 로드 실패 — 텍스트 미표시");
                return null;
            }
        }
    }
}
