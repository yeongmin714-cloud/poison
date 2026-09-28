using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // PlayerInventory
using ProjectName.Systems;     // QuickSlotManager
using ProjectName.UI;          // ItemIconDatabase

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round C — 퀵슬롯 바 (QuickSlotUI 506줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/QuickSlotUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 6슬롯 하단 퀵슬롯 바 (원본 QuickSlotManager.SLOT_COUNT=6 실측, 키 1~6).
    ///   ② 데이터 소스 재사용 — 원본 QuickSlotUI와 동일한 QuickSlotManager.Instance
    ///      (GetItemInSlot / HasItemInSlot / GetItemIdInSlot / SetSlot / ClearSlot / GetAllSlots).
    ///   ③ 슬롯 우클릭(button==1) → 등록 해제(QuickSlotManager.ClearSlot) — 원본 HandleRightClick 패리티.
    ///   ④ 아이콘 (ItemIconDatabase.GetOrCreateIcon) + 키 라벨(1~6) + 인벤 수량(xN).
    ///   ⑤ 인벤토리(I)가 열려있는 상태에서 우클릭 → 선택 아이템 등록 경로는 원본과 동일하게
    ///      InventoryWindow.HasSelectedItem()/GetSelectedItemData() 재사용.
    ///
    /// 상시 노출 바 — HotbarUIUTK와 동일하게 순수 VisualElement(rootVisualElement 직속), self-bootstrap Updater.
    /// 원본 QuickSlotUI와 동일 데이터 경로를 쓰므로 양쪽이 상호 연동된다.
    /// </summary>
    public class QuickSlotUTK : VisualElement
    {
        // ===== 싱글턴 / 부트스트랩 =====
        private static QuickSlotUTK _instance;
        public static QuickSlotUTK Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (ProjectName.Core.UITransitionState.UtkActive) return; // [U8 은퇴] 퀵슬롯 잔재 — 사용자 지정 제거
            if (_instance != null) return;
            _instance = new QuickSlotUTK();
            var go = new GameObject("QuickSlotUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().bar = _instance;
            Debug.Log("[QuickUTK] 부트스트랩 완료 — 퀵슬롯 6슬롯 바 준비");
        }

        // ===== 설정 =====
        private const float SlotSize = 56f;
        private const float SlotGap = 6f;
        private const float Bottom = 88f;          // 핫바(Hotbar bottom=12) 위에 배치

        private readonly UTKSlot[] _slots;
        private readonly Label[]   _keyLabels;
        private readonly string[]  _cachedIds;     // 반영본 캐시 (가비지 방지)

        private QuickSlotUTK()
        {
            name = "QuickSlot";
            AddToClassList("utk-hotbar");
            style.position = Position.Absolute;
            style.left = 0f;
            style.right = 0f;
            style.bottom = Bottom;
            style.alignItems = Align.Center;
            style.flexDirection = FlexDirection.Column;
            pickingMode = PickingMode.Position;

            int slotCount = QuickSlotManager.Instance != null ? QuickSlotManager.Instance.SlotCount : 6;
            _slots = new UTKSlot[slotCount];
            _keyLabels = new Label[slotCount];
            _cachedIds = new string[slotCount];

            // GitHub-dark surface containing the quick-slot grid.
            var surface = new VisualElement();
            surface.name = "QuickSlotCard";
            surface.style.flexDirection = FlexDirection.Column;
            surface.style.alignItems = Align.Stretch;
            surface.style.paddingLeft = 12f;
            surface.style.paddingRight = 12f;
            surface.style.paddingTop = 9f;
            surface.style.paddingBottom = 10f;
            surface.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            surface.style.borderTopWidth = 1f;
            surface.style.borderBottomWidth = 1f;
            surface.style.borderLeftWidth = 1f;
            surface.style.borderRightWidth = 1f;
            surface.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            surface.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            surface.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            surface.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            surface.style.borderTopLeftRadius = 10f;
            surface.style.borderTopRightRadius = 10f;
            surface.style.borderBottomLeftRadius = 10f;
            surface.style.borderBottomRightRadius = 10f;
            Add(surface);

            var heading = new VisualElement();
            heading.name = "QuickSlotHeading";
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            heading.style.marginBottom = 8f;
            surface.Add(heading);

            var title = new Label("QUICK SLOTS");
            title.style.fontSize = 11f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            heading.Add(title);

            var hint = new Label("우클릭: 등록 / 해제");
            hint.style.flexGrow = 1f;
            hint.style.fontSize = 10f;
            hint.style.color = new StyleColor(UTKColor.TextSecondary);
            hint.style.unityTextAlign = TextAnchor.MiddleRight;
            heading.Add(hint);

            var row = new VisualElement();
            row.name = "QuickRow";
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.justifyContent = Justify.Center;
            surface.Add(row);

            for (int i = 0; i < slotCount; i++)
            {
                var cell = new VisualElement();
                cell.name = "QuickCell_" + i;
                cell.AddToClassList("quick-slot-card");
                cell.style.flexDirection = FlexDirection.Column;
                cell.style.alignItems = Align.Center;
                cell.style.justifyContent = Justify.Center;
                cell.style.marginLeft = SlotGap * 0.5f;
                cell.style.marginRight = SlotGap * 0.5f;
                cell.style.paddingLeft = 4f;
                cell.style.paddingRight = 4f;
                cell.style.paddingTop = 4f;
                cell.style.paddingBottom = 4f;
                cell.style.backgroundColor = new StyleColor(UTKColor.BgPanel);
                cell.style.borderTopWidth = 1f;
                cell.style.borderBottomWidth = 1f;
                cell.style.borderLeftWidth = 1f;
                cell.style.borderRightWidth = 1f;
                cell.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
                cell.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
                cell.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
                cell.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
                cell.style.borderTopLeftRadius = 7f;
                cell.style.borderTopRightRadius = 7f;
                cell.style.borderBottomLeftRadius = 7f;
                cell.style.borderBottomRightRadius = 7f;
                row.Add(cell);

                var slot = new UTKSlot();
                slot.name = "Slot_" + i;
                slot.style.width = SlotSize;
                slot.style.height = SlotSize;
                cell.Add(slot);

                var keyLabel = new Label((i + 1).ToString());
                keyLabel.name = "Key_" + i;
                keyLabel.style.width = 18f;
                keyLabel.style.height = 18f;
                keyLabel.style.marginBottom = 3f;
                keyLabel.style.fontSize = 10f;
                keyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                keyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                keyLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                keyLabel.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
                keyLabel.style.borderTopWidth = 1f;
                keyLabel.style.borderBottomWidth = 1f;
                keyLabel.style.borderLeftWidth = 1f;
                keyLabel.style.borderRightWidth = 1f;
                keyLabel.style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
                keyLabel.style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
                keyLabel.style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
                keyLabel.style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
                keyLabel.style.borderTopLeftRadius = 5f; keyLabel.style.borderTopRightRadius = 5f;
                keyLabel.style.borderBottomLeftRadius = 5f; keyLabel.style.borderBottomRightRadius = 5f;
                cell.Add(keyLabel);

                _slots[i] = slot;
                _keyLabels[i] = keyLabel;

                int capturedIndex = i;
                slot.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button == 1) OnSlotRightClick(capturedIndex);
                });
            }

            RefreshAllSlots();
            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        // =====================================================================
        //  데이터 소스 — QuickSlotManager (원본 QuickSlotUI와 동일)
        // =====================================================================

        private static QuickSlotManager Mgr => QuickSlotManager.Instance;

        /// <summary>슬롯 우클릭 — 인벤토리 열린 상태면 등록/해제, 아니면 무시(원본 HandleRightClick 패리티).</summary>
        private void OnSlotRightClick(int index)
        {
            if (Mgr == null) return;
            if (index < 0 || index >= _slots.Length) return;

            if (Mgr.HasItemInSlot(index))
            {
                // 등록된 아이템이 있으면 제거
                Mgr.ClearSlot(index);
                Debug.Log($"[QuickUTK] 퀵슬롯 {index + 1} 등록 해제");
            }
            else
            {
                // 비어있으면 인벤토리(선택 항목) 등록
                RegisterSelectedInventoryItem(index);
            }
            RefreshAllSlots();
        }

        /// <summary>인벤토리(UTK)에서 선택된 아이템을 퀵슬롯에 등록 — 원본 RegisterSelectedInventoryItem 패리티.</summary>
        private void RegisterSelectedInventoryItem(int index)
        {
            if (index < 0 || index >= _slots.Length) return;
            if (PlayerInventory.Instance == null) return;

            var invWindow = InventoryWindowUTK.Instance;
            if (invWindow == null || !invWindow.HasSelectedInInventory())
            {
                Debug.Log("[QuickUTK] 인벤토리에서 선택된 아이템이 없음 — 등록 스킵");
                return;
            }

            var selectedItem = invWindow.GetSelectedInventoryItem();
            if (selectedItem == null) return;

            Mgr.SetSlot(index, selectedItem);
            Debug.Log($"[QuickUTK] 퀵슬롯 {index + 1}에 '{selectedItem.displayName}' 등록");
        }

        // =====================================================================
        //  갱신
        // =====================================================================

        private void RefreshAllSlots()
        {
            for (int i = 0; i < _slots.Length; i++)
                RefreshSlot(i);
        }

        private void RefreshSlot(int index)
        {
            var slot = _slots[index];
            if (slot == null) return;
            if (Mgr == null)
            {
                SetCardFilled(slot, false);
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
                return;
            }

            if (!Mgr.HasItemInSlot(index))
            {
                _cachedIds[index] = null;
                SetCardFilled(slot, false);
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
                return;
            }

            var item = Mgr.GetItemInSlot(index);
            _cachedIds[index] = item != null ? item.id : null;

            if (item != null)
            {
                SetCardFilled(slot, true);
                slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                int invCount = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount(item.id) : 0;
                slot.SetCount(invCount);
            }
            else
            {
                SetCardFilled(slot, false);
                slot.SetIcon(null);
                slot.SetCount(0);
                slot.SetRank("common");
            }
        }

        // 카드 외곽선만 점유 상태에 맞춰 바꾼다. 슬롯 데이터/렌더링 경로는 그대로 유지.
        private static void SetCardFilled(UTKSlot slot, bool filled)
        {
            var card = slot.parent;
            if (card == null) return;
            Color stroke = filled ? UTKColor.BorderGold : UTKColor.BorderBronze;
            card.style.borderTopColor = new StyleColor(stroke);
            card.style.borderBottomColor = new StyleColor(stroke);
            card.style.borderLeftColor = new StyleColor(stroke);
            card.style.borderRightColor = new StyleColor(stroke);
            card.style.backgroundColor = new StyleColor(filled ? UTKColor.BgPanel : UTKColor.BgPanelDark);
        }

        // =====================================================================
        //  부착용 Updater — HotbarUIUTK 관례 (아이콘 재시도 + 수량 반영)
        // =====================================================================

        private class Updater : MonoBehaviour
        {
            public QuickSlotUTK bar;
            private float _tick = 0.3f;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && bar != null && bar.parent == null)
                    root.Add(bar);

                if (bar == null) return;
                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = 0.3f;
                    if (bar.parent != null)
                        bar.RefreshAllSlots();
                }
            }
        }
    }
}