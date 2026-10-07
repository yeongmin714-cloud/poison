using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;    // GuardPlaceholder, GuardSquadHotbar
using ProjectName.Core;   // PlayerInventory
using ProjectName.UI;     // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 2B — 하단 핫바 (HotbarUI 849줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/HotbarUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 8슬롯 하단 중앙 핫바 (원본 SlotCount=8, Alpha1~8 대응).
    ///   ② 각 슬롯 숫자 라벨(1~8) + 아이콘 — UTKSlot.SetIcon(ItemIconDatabase.GetOrCreateIcon).
    ///   ③ 데이터 소스 재사용 — 원본 HotbarUI와 동일한 PlayerPrefs 키(poison_hotbar_{i})를
    ///      읽고 써서 상호 연동 (등록/해제가 양쪽에 반영).
    ///   ④ 등록 해제 — 슬롯 우클릭(button==1) → PlayerPrefs 클리어 + 아이콘 제거.
    ///   ⑤ GLB 아이콘은 비동기 베이크라 즉시 null일 수 있음 → MonoBehaviour 1초 주기 재시도 (원본 Update 선례).
    ///
    /// 상시 노출 바 — UTKWindowBase(윈도우/닫기)가 아닌 순수 VisualElement(rootVisualElement 직속).
    /// 셀프 부트스트랩 + Updater로 UIRoot 부착 (StatusWindowUTK/ HotbarUI Bootstrap 선례).
    /// </summary>
    public class HotbarUIUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static HotbarUIUTK _instance;
        public static HotbarUIUTK Instance => _instance;

        private const string PrefsIdKey   = "poison_hotbar_{0}";        // 원본과 동일 키
        private const string PrefsNameKey = "poison_hotbar_name_{0}";   // 원본과 동일 키

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>씬 셋업에서 명시적으로 보장할 수 있는 멱등 UTK 핫바 부트스트랩.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new HotbarUIUTK();
            var go = new GameObject("HotbarUIUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().bar = _instance;
            Debug.Log("[HotbarUTK] Ensure 완료 — 하단 핫바(8슬롯) 준비");
        }

        // ===== 설정 =====
        private const int   SlotCount   = 8;        // 원본 SlotCount 대응
        private const float SlotSize    = 96f; // Archived 23:6 SlotBox
        private const float SlotGap     = 19.2f;
        private const float FigmaLeft   = 629.6f;
        private const float FigmaTop    = 861.6f;
        private const float FigmaWidth  = 902.4f;
        private const float FigmaHeight = 124.8f;

        private readonly UTKSlot[] _slots;
        private readonly Label[]   _keyLabels;
        private readonly string[]  _assignedIds;    // 슬롯별 등록 itemId (PlayerPrefs 반영본)
        private readonly string[]  _assignedNames;
        private bool _squadMode;                    // [U8 요구] Tab 전환 모드 — false=아이템, true=부대 지정
        private GuardPlaceholder[] _squadReps;      // [U8 배선] 부대 대표 병사 (정보창 진입용)
        private IVisualElementScheduledItem _pulseSchedule;

        private HotbarUIUTK()
        {
            name = "Hotbar";
            AddToClassList("utk-hotbar");
            style.position = Position.Absolute;
            style.left = FigmaLeft;
            style.top = FigmaTop;
            style.width = FigmaWidth;
            style.height = FigmaHeight;
            style.alignItems = Align.FlexStart;
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Position;

            _slots = new UTKSlot[SlotCount];
            _keyLabels = new Label[SlotCount];
            _assignedIds = new string[SlotCount];
            _assignedNames = new string[SlotCount];

            var row = new VisualElement();
            row.name = "HotbarRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexStart;
            row.style.width = FigmaWidth;
            row.style.height = FigmaHeight;
            Add(row);

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = new VisualElement();
                cell.name = "HotbarCell_" + i;
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.width = SlotSize;
                cell.style.height = FigmaHeight;
                cell.style.marginRight = i < SlotCount - 1 ? SlotGap : 0f;
                cell.style.alignItems = Align.FlexStart;
                cell.style.flexShrink = 0f;
                row.Add(cell);

                var slot = new UTKSlot();
                slot.name = "Slot_" + i;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                cell.Add(slot);

                var keyLabel = new Label((i + 1).ToString());
                keyLabel.name = "Key_" + i;
                keyLabel.style.position = Position.Absolute;
                keyLabel.style.left = 36f;
                keyLabel.style.top = 84f;
                keyLabel.style.width = 24f;
                keyLabel.style.height = 24f;
                keyLabel.style.fontSize = UTKTheme.FontSubtitle;   // [Figma 23:6] 13.2/900
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                keyLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                keyLabel.style.backgroundColor = new StyleColor(new Color(0.10f, 0.10f, 0.12f, 0.9f));
                cell.Add(keyLabel);

                _slots[i] = slot;
                _keyLabels[i] = keyLabel;

                UTKSlot capturedSlot = slot;
                int capturedIndex = i;
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    // [U8 배선] 부대 모드 좌클릭 = 병사 정보창 (UTK)
                    if (_squadMode && evt.button == 0 && _squadReps != null && capturedIndex < _squadReps.Length && _squadReps[capturedIndex] != null)
                    {
                        GuardInfoUTK.Open(_squadReps[capturedIndex]);
                        return;
                    }
                    if (evt.button == 1 && !_squadMode) UnregisterSlot(capturedIndex);
                });

                // [U8 요구] 좌클릭 드래그 드롭 = 핫바에 아이템 등록 (마인크래프트식)
                UTKDragDrop.RegisterDropTarget(slot, new HotbarSlotDropTarget(capturedIndex));
            }

            var tabHint = new VisualElement { name = "TabRotationHint" };
            tabHint.style.position = Position.Absolute;
            tabHint.style.left = 926.4f;
            tabHint.style.top = 28.8f;
            tabHint.style.width = 57.6f;
            tabHint.style.height = 67.2f;
            tabHint.style.alignItems = Align.Center;
            tabHint.style.backgroundColor = new StyleColor(new Color(0.07f, 0.09f, 0.12f, 0.82f));
            tabHint.style.borderTopLeftRadius = 6f; tabHint.style.borderTopRightRadius = 6f;
            tabHint.style.borderBottomLeftRadius = 6f; tabHint.style.borderBottomRightRadius = 6f;
            tabHint.pickingMode = PickingMode.Ignore;
            var rotateHint = new Label("↻");
            rotateHint.style.position = Position.Absolute;
            rotateHint.style.left = 19.2f;
            rotateHint.style.top = 24f;
            rotateHint.style.width = 19.2f;
            rotateHint.style.height = 19.2f;
            rotateHint.style.fontSize = 15f;
            rotateHint.style.color = new StyleColor(UTKColor.TextSecondary);
            rotateHint.style.unityTextAlign = TextAnchor.MiddleCenter;
            tabHint.Add(rotateHint);
            var tabLabel = new Label("TAB");
            tabLabel.style.position = Position.Absolute;
            tabLabel.style.left = 14.4f;
            tabLabel.style.top = 45.6f;
            tabLabel.style.width = 28.8f;
            tabLabel.style.height = 21.6f;
            tabLabel.style.fontSize = UTKTheme.FontBadge;   // [Figma] 12/900
            tabLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            tabLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            tabLabel.style.backgroundColor = new StyleColor(new Color(0.15f, 0.17f, 0.2f, 0.95f));
            tabLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            tabHint.Add(tabLabel);
            Add(tabHint);

            // 원본 PlayerPrefs 데이터 소스 로드
            LoadAssignedFromPrefs();
            RefreshAllIcons();
            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // =====================================================================
        //  데이터 소스 — 원본 HotbarUI와 동일 PlayerPrefs 키 연동
        // =====================================================================

        private void LoadAssignedFromPrefs()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                _assignedIds[i] = PlayerPrefs.GetString(string.Format(PrefsIdKey, i), null);
                _assignedNames[i] = PlayerPrefs.GetString(string.Format(PrefsNameKey, i), null);
            }
        }

        private static string IdKey(int index)   => string.Format(PrefsIdKey, index);
        private static string NameKey(int index) => string.Format(PrefsNameKey, index);

        public bool IsSquadMode => _squadMode;

        /// <summary>[U8 요구] Tab 전환 — 아이템 모드 ↔ 부대 지정 모드 (동일 8슬롯 정렬).</summary>
        public void ToggleSquadMode()
        {
            _squadMode = !_squadMode;

            // [2026-09-27 부대 드래그 연동] 부대 모드 상태를 GuardSelectionManager에 동기 —
            //   부대 모드(true)일 때 Ctrl 없이 드래그로 병사 지정 · 우클릭 이동이 되도록.
            //   (U8 게이트로 Tab 전환은 UTK가 담당 — 원본 GuardSquadHotbar.SetSquadMode와 이중 동기,
            //    동일 값을 쓰므로 충돌 없음. 평상 시 false 유지해 좌클릭 공격 보존)
            ProjectName.Systems.GuardSelectionManager.squadModeActive = _squadMode;

            RefreshAllIcons();

            // 전환 펄스 애니 — 스케일 1.15 → 1.0 (150ms, unscaled)
            if (_pulseSchedule != null)
            {
                _pulseSchedule.Pause();
                _pulseSchedule = null;
            }
            style.scale = new StyleScale(new Scale(new Vector2(1.15f, 1.15f)));
            float t = 0f;
            IVisualElementScheduledItem pulseSchedule = null;
            pulseSchedule = schedule.Execute(() =>
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(1f - t / 0.15f);
                float s = 1f + 0.15f * k;
                style.scale = new StyleScale(new Scale(new Vector2(s, s)));
                if (k <= 0f)
                {
                    style.scale = StyleKeyword.Null;
                    pulseSchedule.Pause();
                    if (_pulseSchedule == pulseSchedule) _pulseSchedule = null;
                }
            });
            pulseSchedule.Every(16L);
            pulseSchedule.ExecuteLater(0);
            _pulseSchedule = pulseSchedule;

            Debug.Log($"[HotbarUTK] 모드 전환 → {(_squadMode ? "부대 지정" : "아이템")}");
        }

        /// <summary>[U8 요구] 부대 모드 렌더 — 원본 GuardSquadHotbar._slots 리플렉션 데이터(병사 아바타).</summary>
        private void RefreshSquadSlots()
        {
            if (_squadReps == null || _squadReps.Length != SlotCount) _squadReps = new GuardPlaceholder[SlotCount];
            var original = Object.FindAnyObjectByType<ProjectName.UI.GuardSquadHotbar>();
            if (original == null)
            {
                for (int i = 0; i < SlotCount; i++)
                {
                    _slots[i].SetIcon(null);
                    _slots[i].SetCount(0);
                    _slots[i].SetRank("common");
                }
                return;
            }

            // 원본 GuardSquadHotbar private _slots 리플렉션 실측 (GuardSquadHotbarUTK 검증 경로와 동일)
            var field = typeof(GuardSquadHotbar).GetField("_slots",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var groups = field != null ? field.GetValue(original) as GuardPlaceholder[][] : null;
            for (int i = 0; i < SlotCount; i++)
            {
                var members = (groups != null && i < groups.Length) ? groups[i] : null;
                int alive = 0;
                GuardPlaceholder representative = null;
                if (members != null)
                {
                    foreach (var g in members)
                    {
                        if (g == null || !g.IsAlive) continue;
                        alive++;
                        if (representative == null) representative = g;
                    }
                }

                _squadReps[i] = representative;
                if (representative != null)
                {
                    var icon = GuardIconRenderer.GetOrCreateIcon(representative);
                    _slots[i].SetIcon(icon);
                    _slots[i].SetCount(alive);
                    _slots[i].SetRank("common");
                }
                else
                {
                    _slots[i].SetIcon(null);
                    _slots[i].SetCount(0);
                    _slots[i].SetRank("common");
                }
            }
        }

        /// <summary>
        /// 등록 API — 원본 HotbarUI.AssignItem과 동일 데이터 경로(PlayerPrefs 키)로 아이템 지정.
        /// 몸 장비(방어구)는 핫바 지정 금지 (원본 AssignItem 가드 동일).
        /// </summary>
        public static void AssignItem(int index, PlayerInventory.ItemData item)
        {
            if (_instance == null || item == null) return;
            if (index < 0 || index >= SlotCount) return;
            if (item.category == PlayerInventory.ItemCategory.Armor)
            {
                Debug.Log($"[HotbarUTK] 몸 장비는 핫바 지정 불가 — 장비창으로: {item.displayName}");
                return;
            }

            PlayerPrefs.SetString(IdKey(index), item.id);
            PlayerPrefs.SetString(NameKey(index), item.displayName);
            PlayerPrefs.Save();

            _instance._assignedIds[index] = item.id;
            _instance._assignedNames[index] = item.displayName;
            _instance.RefreshSlot(index);
            Debug.Log($"[HotbarUTK] 슬롯 {index + 1}에 '{item.displayName}' 등록");
        }

        /// <summary>등록 해제 — PlayerPrefs 클리어 + 아이콘 제거 (아이템은 인벤에 유지).</summary>
        public static void UnregisterSlot(int index)
        {
            if (_instance == null) return;
            if (index < 0 || index >= SlotCount) return;
            if (string.IsNullOrEmpty(_instance._assignedIds[index])) return;

            string removed = _instance._assignedNames[index] ?? _instance._assignedIds[index];

            PlayerPrefs.DeleteKey(IdKey(index));
            PlayerPrefs.DeleteKey(NameKey(index));
            PlayerPrefs.Save();

            _instance._assignedIds[index] = null;
            _instance._assignedNames[index] = null;
            _instance.RefreshSlot(index);
            Debug.Log($"[HotbarUTK] 슬롯 {index + 1} 등록 해제 — '{removed}' (아이템 인벤 유지)");
        }

        // =====================================================================
        //  아이콘 갱신
        // =====================================================================

        private void RefreshAllIcons()
        {
            // [U8 요구] 부대 모드 슬롯 색상 차별화 — 청록 틴트 배경 (아이템 모드는 다크 브라운)
            var squadBg = new StyleColor(new Color(0.10f, 0.22f, 0.30f, 0.92f));
            var itemBg = new StyleColor(new Color(0.07f, 0.05f, 0.03f, 0.85f));
            for (int i = 0; i < SlotCount; i++)
                _slots[i].style.backgroundColor = _squadMode ? squadBg : itemBg;

            if (_squadMode) { RefreshSquadSlots(); return; }   // [U8 요구] 부대 모드 — 동일 8슬롯에 부대 렌더
            for (int i = 0; i < SlotCount; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            string itemId = _assignedIds[index];
            var slot = _slots[index];
            if (slot == null) return;

            if (string.IsNullOrEmpty(itemId))
            {
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
                return;
            }

            // 인벤 슬롯에서 ItemData 우선 조회 (수량 표기 + 아이콘), 실패 시 정적 정의 폴백
            PlayerInventory.ItemData item = FindInventoryItem(itemId);
            if (item == null)
                item = PlayerInventory.GetItemById(itemId);

            if (item != null)
            {
                slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                // 스택(>1)만 수량 표기 — 단독 무기/방어구 "1" 노이즈 방지
                slot.SetCount(GetInventoryCount(itemId));
            }
            else
            {
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
            }
        }

        // =====================================================================
        //  인벤토리 조회 헬퍼
        // =====================================================================

        private static PlayerInventory.ItemData FindInventoryItem(string itemId)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return null;
            var slots = inv.GetAllSlots();
            if (slots == null) return null;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && s.item != null && s.item.id == itemId)
                    return s.item;
            }
            return null;
        }

        private static int GetInventoryCount(string itemId)
        {
            var inv = PlayerInventory.Instance;
            if (inv == null) return 0;
            var slots = inv.GetAllSlots();
            if (slots == null) return 0;
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                if (s != null && s.item != null && s.item.id == itemId)
                    total += s.count;
            }
            return total;
        }

        // =====================================================================
        //  갱신 / 부착용 Updater — 원본 Update() (1초 아이콘 재시도 + count 반영)
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public HotbarUIUTK bar;
            private float _tick = 1f;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && bar != null && bar.parent == null)
                    root.Add(bar);
                if (root != null && bar != null)
                    bar.ApplyFigmaBounds(root);

                if (bar == null) return;

                // [U8 요구] Tab 직접 폴링 — 아이템 모드 ↔ 부대 지정 모드 전환 (동일 8슬롯 정렬)
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && kb.tabKey.wasPressedThisFrame)
                {
                    bar.ToggleSquadMode();
                    // 원본 GuardSquadHotbar 모드 동기 (1~8 선택 로직 단일소스 유지)
                    var original = Object.FindAnyObjectByType<ProjectName.UI.GuardSquadHotbar>();
                    if (original != null)
                    {
                        var f = typeof(ProjectName.UI.GuardSquadHotbar).GetField("_squadMode",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (f != null) f.SetValue(original, bar.IsSquadMode);
                    }
                }

                // GLB 아이콘 비동기 베이크 재시도 + 인벤 수량 변동 반영 — 1초 주기 (원본 Update 관례)
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = 1f;
                    if (bar.parent != null)
                        bar.RefreshAllIcons();
                }
            }
        }

        private void ApplyFigmaBounds(VisualElement root)
        {
            if (root == null) return;
            Vector2 rootSize = new Vector2(root.resolvedStyle.width, root.resolvedStyle.height);
            if (rootSize.x <= 0f || rootSize.y <= 0f) return;
            Rect bounds = FigmaCanvasLayout.ScaleRect(new Rect(FigmaLeft, FigmaTop, FigmaWidth, FigmaHeight), rootSize);
            style.position = Position.Absolute;
            style.left = bounds.x;
            style.top = bounds.y;
            style.width = bounds.width;
            style.height = bounds.height;
            float sx = rootSize.x / FigmaCanvasLayout.CanvasWidth;
            float sy = rootSize.y / FigmaCanvasLayout.CanvasHeight;
            float cellWidth = SlotSize * sx;
            float slotHeight = SlotSize * sy;
            float cellHeight = FigmaHeight * sy;
            float horizontalGap = Mathf.Max(0f, (FigmaWidth * sx - SlotCount * cellWidth) / (SlotCount - 1));
            var row = this.Q<VisualElement>("HotbarRow");
            if (row != null)
            {
                row.style.width = FigmaWidth * sx;
                row.style.height = FigmaHeight * sy;
            }

            var hint = this.Q<VisualElement>("TabRotationHint");
            if (hint != null)
            {
                hint.style.left = 926.4f * sx; hint.style.top = 28.8f * sy;
                hint.style.width = 57.6f * sx; hint.style.height = 67.2f * sy;
                var rotation = hint.Q<Label>();
                if (rotation != null)
                {
                    rotation.style.left = 19.2f * sx;
                    rotation.style.top = 24f * sy;
                    rotation.style.width = 19.2f * sx;
                    rotation.style.height = 19.2f * sy;
                }
                var badge = hint.childCount > 1 ? hint.ElementAt(1) as Label : null;
                if (badge != null)
                {
                    badge.style.left = 14.4f * sx;
                    badge.style.top = 45.6f * sy;
                    badge.style.width = 28.8f * sx;
                    badge.style.height = 21.6f * sy;
                }
            }

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = this.Q<VisualElement>("HotbarCell_" + i);
                if (cell == null) continue;
                cell.style.width = cellWidth;
                cell.style.height = cellHeight;
                cell.style.marginRight = i < SlotCount - 1 ? horizontalGap : 0f;
                _slots[i].style.width = cellWidth;
                _slots[i].style.height = slotHeight;
                _keyLabels[i].style.left = 36f * sx;
                _keyLabels[i].style.top = 84f * sy;
                _keyLabels[i].style.width = 24f * sx;
                _keyLabels[i].style.height = 24f * sy;
            }
        }
    }
}

namespace ProjectName.UI.Toolkit
{
    /// <summary>[U8 요구] 핫바 슬롯 드롭 타겟 — 가방에서 좌클릭 드래그로 아이템 등록 (마인크래프트식).</summary>
    public class HotbarSlotDropTarget : IUTKDropTarget
    {
        private readonly int _index;

        public HotbarSlotDropTarget(int index) { _index = index; }

        public bool CanDrop(UTKDragPayload payload)
            => payload != null && payload.Item != null && payload.Source == UTKDragSourceKind.Inventory;

        public bool Drop(UTKDragPayload payload)
        {
            HotbarUIUTK.AssignItem(_index, payload.Item);
            return true;
        }
    }
}
