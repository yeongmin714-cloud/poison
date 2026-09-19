// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // EquipmentManager
using ProjectName.UI;        // ItemIconDatabase
using ProjectName.Core;      // PlayerInventory

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U2 Round 2B — 장비창 (EquipmentWindow 620줄 IMGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/EquipmentWindow.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 8개 장비 슬롯(투구/갑옷/무기/방패/신발/장갑/가면/가방) 표시 — 헬멧~배낭 Equip enum과 대응.
    ///   ② 각 슬롯에 장착 아이템 아이콘(ItemIconDatabase)+이름+내구도 표시.
    ///   ③ 해제 버튼 / 우클릭 → EquipmentManager.UnequipSlot(slot) 실제 호출 → 인벤토리 복귀.
    ///   ④ OnEquipmentChanged 구독 + 폴링 갱신으로 즉시 반영.
    ///   드래그 소스(장비 슬롯 드래그 → 해제 드롭)는 후속 라운드 — 이번엔 해제 버튼/우클릭만.
    ///
    /// 원본 좌측(인벤) 관례에 맞춰 본 창은 우측 배치(TryRenderEmbedded가 통합 인벤 우측에 안착).
    /// 데이터 경로는 EquipmentManager.GetSlotData / UnequipSlot을 직접 호출.
    /// 각 경로에 [EquipUTK] Debug.Log 실측 로그.
    /// </summary>
    public class EquipmentWindowUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static EquipmentWindowUTK _instance;
        public static EquipmentWindowUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new EquipmentWindowUTK();
        }

        /// <summary>장비창 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
        }

        // ===== 설정 =====
        private const float WinW = 400f;
        private const float WinH = 560f;
        private const float SlotSize = 56f;
        private const long RefreshMs = 400L;

        // ===== 슬롯 정의 — 원본 _slotDefs(헬멧/갑옷/무기/신발/장갑/Back) + 후속 Mask/Bag 확장 ====
        private class SlotDef
        {
            public string label;                      // 좌측 슬롯명 (투구/갑옷/...)
            public EquipmentManager.EquipmentSlot slot;
        }

        private static readonly SlotDef[] SlotDefs = new SlotDef[]
        {
            new SlotDef { label = "투구", slot = EquipmentManager.EquipmentSlot.Helmet },
            new SlotDef { label = "갑옷", slot = EquipmentManager.EquipmentSlot.Armor },
            new SlotDef { label = "무기", slot = EquipmentManager.EquipmentSlot.Weapon },
            new SlotDef { label = "방패", slot = EquipmentManager.EquipmentSlot.Back },   // Back = 방패
            new SlotDef { label = "신발", slot = EquipmentManager.EquipmentSlot.Shoes },
            new SlotDef { label = "장갑", slot = EquipmentManager.EquipmentSlot.Gloves },
            new SlotDef { label = "가면", slot = EquipmentManager.EquipmentSlot.Mask },   // [TEST28-69] Mask 추가
            new SlotDef { label = "가방", slot = EquipmentManager.EquipmentSlot.Bag },    // [TEST28-69] Bag 추가
        };

        // ===== 갱신 참조 =====
        private readonly VisualElement _grid;
        private readonly Label _statusLabel;
        private readonly RowView[] _rows;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;
        private EquipmentManager _subscribedEquip;

        // 각 슬롯 행의 동적 UI (아이콘/이름/내구도/해제 버튼)
        private class RowView
        {
            public UTKSlot slot;
            public Label nameLabel;
            public Label durLabel;
            public Button unequipBtn;
        }

        private EquipmentWindowUTK() : base("장비", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            var title = new Label("장비 목록 (우클릭/버튼 → 해제)");
            title.AddToClassList("utk-title-label");
            title.style.fontSize = 18f;
            _content.Add(title);

            _grid = new VisualElement();
            _grid.name = "EquipGrid";
            _grid.style.flexGrow = 1f;
            _grid.style.marginTop = 8f;
            _content.Add(_grid);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 13f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_statusLabel);

            _rows = new RowView[SlotDefs.Length];
            for (int i = 0; i < SlotDefs.Length; i++)
            {
                _rows[i] = BuildRow(_grid, SlotDefs[i], i);
            }

            ApplyUIToolkitFont(this);

            // 기본 숨김 + 우측 배치 관례 (원본 TryRenderEmbedded = 통합 인벤 우측 안착)
            style.display = DisplayStyle.None;
            style.right = 16f;
            style.top = 96f;
        }

        // ===== 행 구성 =====
        private RowView BuildRow(VisualElement parent, SlotDef def, int index)
        {
            var row = new VisualElement();
            row.name = "EquipRow_" + def.slot;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.width = new Length(100f, LengthUnit.Percent);
            row.style.marginBottom = 6f;

            var slotLabel = new Label(def.label);
            slotLabel.style.width = 46f;
            slotLabel.style.fontSize = 15f;
            slotLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            row.Add(slotLabel);

            var utkSlot = new UTKSlot();
            utkSlot.name = "Slot_" + def.slot;
            utkSlot.style.width = SlotSize;
            utkSlot.style.height = SlotSize;
            utkSlot.style.flexShrink = 0f;
            row.Add(utkSlot);

            var textCol = new VisualElement();
            textCol.style.flexGrow = 1f;
            textCol.style.flexDirection = FlexDirection.Column;
            textCol.style.marginLeft = 8f;

            var nameLabel = new Label("");
            nameLabel.name = "Name_" + index;
            nameLabel.style.fontSize = 15f;
            nameLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            textCol.Add(nameLabel);

            var durLabel = new Label("");
            durLabel.name = "Dur_" + index;
            durLabel.style.fontSize = 12f;
            durLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            textCol.Add(durLabel);

            row.Add(textCol);

            var unequipBtn = UTKButton.Create("해제", () => TryUnequip(def.slot), UTKButton.Variant.Danger);
            unequipBtn.name = "Unequip_" + index;
            unequipBtn.style.flexShrink = 0f;
            unequipBtn.style.marginLeft = 6f;
            row.Add(unequipBtn);

            // 우클릭(button==1) → 해제
            utkSlot.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button == 1) TryUnequip(def.slot);
            });

            parent.Add(row);

            return new RowView { slot = utkSlot, nameLabel = nameLabel, durLabel = durLabel, unequipBtn = unequipBtn };
        }

        // =====================================================================
        //  생명주기 (UTKWindowBase 훅) — InventoryWindowUTK 패턴
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.right = 16f;   // 우측 고정
            style.top = 96f;
            EnsureEquipSubscription();
            StartRefreshLoop();
            RefreshAll();
            Debug.Log("[EquipUTK] 장비창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[EquipUTK] 장비창 닫힘");
        }

        protected override void OnWindowClosed()
        {
            UnsubscribeEquip();
        }

        // =====================================================================
        //  갱신 — 이벤트 구독 + 폴링
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshAll();
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

        private void EnsureEquipSubscription()
        {
            var em = EquipmentManager.Get();
            if (!ReferenceEquals(_subscribedEquip, em))
            {
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = em;
                if (_subscribedEquip != null) _subscribedEquip.OnEquipmentChanged += OnEquipmentChanged;
            }
        }

        private void UnsubscribeEquip()
        {
            if (_subscribedEquip != null)
            {
                _subscribedEquip.OnEquipmentChanged -= OnEquipmentChanged;
                _subscribedEquip = null;
            }
        }

        private void OnEquipmentChanged(EquipmentManager.EquipmentSlot slot, string itemId)
        {
            if (!IsOpen) return;
            Debug.Log($"[EquipUTK] 장비 변경 수신(slot={slot}, item={itemId ?? "none"}) — 갱신");
            RefreshAll();
        }

        // =====================================================================
        //  ①~② 슬롯 표시 — GetSlotData 직접 조회
        // =====================================================================

        private void RefreshAll()
        {
            var em = EquipmentManager.Get();
            for (int i = 0; i < SlotDefs.Length; i++)
            {
                RefreshRow(em, _rows[i], SlotDefs[i], i);
            }
        }

        private void RefreshRow(EquipmentManager em, RowView rowView, SlotDef def, int index)
        {
            var slotData = em != null ? em.GetSlotData(def.slot) : null;
            bool empty = slotData == null || string.IsNullOrEmpty(slotData.itemId);

            if (empty)
            {
                rowView.slot.SetIcon(null);
                rowView.slot.SetRank("common");
                rowView.nameLabel.text = "[비어있음]";
                rowView.durLabel.text = "";
                rowView.unequipBtn.SetEnabled(false);
                return;
            }

            var item = slotData.itemData;
            if (item == null)
                item = PlayerInventory.GetItemById(slotData.itemId);

            if (item != null)
            {
                rowView.slot.SetIcon(ItemIconDatabase.GetOrCreateIcon(item));
                rowView.slot.SetRank(UTKRarity.ClassForIndex((int)item.rarity));
                rowView.nameLabel.text = item.displayName;
            }
            else
            {
                rowView.slot.SetIcon(null);
                rowView.slot.SetRank("common");
                rowView.nameLabel.text = slotData.itemId;
            }

            if (item != null && item.maxDurability > 0)
            {
                float ratio = item.maxDurability > 0
                    ? (float)slotData.currentDurability / item.maxDurability
                    : 1f;
                Color durColor = ratio >= 0.6f ? Color.green : (ratio >= 0.3f ? Color.yellow : Color.red);
                rowView.durLabel.text = $"내구도 {slotData.currentDurability}/{item.maxDurability}";
                rowView.durLabel.style.color = new StyleColor(durColor);
            }
            else
            {
                rowView.durLabel.text = "내구도 ∞";
                rowView.durLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            }

            rowView.unequipBtn.SetEnabled(true);
        }

        // =====================================================================
        //  ③ 해제 — UnequipSlot 실제 호출 → 인벤토리 복귀
        // =====================================================================

        private void TryUnequip(EquipmentManager.EquipmentSlot slot)
        {
            var em = EquipmentManager.Get();
            if (em == null)
            {
                Debug.LogWarning("[EquipUTK] EquipmentManager 없음 — 해제 스킵");
                return;
            }
            var slotData = em.GetSlotData(slot);
            if (slotData == null || string.IsNullOrEmpty(slotData.itemId))
            {
                Debug.Log($"[EquipUTK] {slot} 슬롯이 비어있어 해제 스킵");
                return;
            }

            bool success = em.UnequipSlot(slot);
            if (success)
            {
                _statusLabel.text = $"{slot} 해제 완료 — 인벤토리로 복귀";
                Debug.Log($"[EquipUTK] {slot} 장비 해제 완료 (인벤 복귀) — UnequipSlot 성공");
            }
            else
            {
                _statusLabel.text = $"{slot} 해제 실패 (인벤토리 가득 찼을 수 있음)";
                Debug.LogWarning($"[EquipUTK] {slot} 장비 해제 실패 — UnequipSlot 실패");
            }
            RefreshAll();
        }
    }
}