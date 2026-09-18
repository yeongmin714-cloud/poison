using UnityEngine;
using UnityEngine.UIElements;
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
        private static void Bootstrap()
        {
            if (_instance != null) return;
            _instance = new HotbarUIUTK();
            var go = new GameObject("HotbarUIUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().bar = _instance;
            Debug.Log("[HotbarUTK] 부트스트랩 완료 — 하단 핫바(8슬롯) 준비");
        }

        // ===== 설정 =====
        private const int   SlotCount   = 8;        // 원본 SlotCount 대응
        private const float SlotSize    = 64f;
        private const float SlotGap     = 8f;
        private const float Bottom      = 12f;

        private readonly UTKSlot[] _slots;
        private readonly Label[]   _keyLabels;
        private readonly string[]  _assignedIds;    // 슬롯별 등록 itemId (PlayerPrefs 반영본)
        private readonly string[]  _assignedNames;

        private HotbarUIUTK()
        {
            name = "Hotbar";
            AddToClassList("utk-hotbar");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.bottom = Bottom;
            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Position;

            _slots = new UTKSlot[SlotCount];
            _keyLabels = new Label[SlotCount];
            _assignedIds = new string[SlotCount];
            _assignedNames = new string[SlotCount];

            var row = new VisualElement();
            row.name = "HotbarRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.FlexEnd;
            Add(row);

            for (int i = 0; i < SlotCount; i++)
            {
                var cell = new VisualElement();
                cell.name = "HotbarCell_" + i;
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.marginLeft = SlotGap * 0.5f;
                cell.style.marginRight = SlotGap * 0.5f;
                row.Add(cell);

                var slot = new UTKSlot();
                slot.name = "Slot_" + i;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                slot.style.marginBottom = 2f;
                cell.Add(slot);

                var keyLabel = new Label((i + 1).ToString());
                keyLabel.name = "Key_" + i;
                keyLabel.style.width = 22f;
                keyLabel.style.height = 20f;
                keyLabel.style.fontSize = 13f;
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
                    if (evt.button == 1) UnregisterSlot(capturedIndex);
                });
            }

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
            // [U8 요구] Tab 부대 모드 연동 — 부대 모드면 아이템 핫바 숨김(부대 핫바만)
            if (ProjectName.UI.GuardSquadHotbar.IsSquadMode && style.display == DisplayStyle.Flex)
                style.display = DisplayStyle.None;
            else if (!ProjectName.UI.GuardSquadHotbar.IsSquadMode && style.display == DisplayStyle.None)
                style.display = DisplayStyle.Flex;
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

                if (bar == null) return;
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
    }
}