// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;        // PlayerInventory
using ProjectName.Systems;     // EquipmentRepairSystem

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U3 Round C — 장비 수리 스테이션 창 (RepairStationUI 348줄 uGUI → UTK 포팅).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/RepairStationUI.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 보유 골드 표시 (PlayerInventory.GetItemCount("gold")).
    ///   ② 파손 장비 목록 — GetDamagedEquipment 패리티: CanRepair + 내구도 손상(currentDurability &lt; maxDurability) 슬롯만.
    ///   ③ 수리 비용 표시 (EquipmentRepairSystem.GetRepairCost(slot)).
    ///   ④ 수리 버튼 → EquipmentRepairSystem.RepairInventorySlot(slotIndex) — 원본 TryRepairSlot 패리티.
    ///   ⑤ 골드 부족 시 버튼 비활성("골드부족").
    ///   [RepairUTK] 로그.
    ///
    /// 250~300ms 폴링 갱신 (수리 후 목록 갱신).
    /// </summary>
    public class RepairStationUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static RepairStationUTK _instance;
        public static RepairStationUTK Instance => _instance;

        /// <summary>팩토리 — 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new RepairStationUTK();
        }

        /// <summary>수리 창 열기(팩토리 겸용).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>닫혀있으면 열고, 열려있으면 닫음.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Ensure();
            _instance.Show();
        }

        // ===== 설정 =====
        private const float WinW = 520f;
        private const float WinH = 460f;
        private const long RefreshMs = 250L;

        // ===== 레퍼런스 =====
        private readonly Label _goldLabel;
        private readonly Label _statusLabel;
        private readonly VisualElement _list;
        private readonly ScrollView _scroll;
        private IVisualElementScheduledItem _refreshTask;

        private Color _statusColor = UTKColor.GuildGreen;
        private readonly List<RepairRowBinding> _rowBindings = new List<RepairRowBinding>();

        private RepairStationUTK() : base("장비 수리 스테이션", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _goldLabel = new Label("💰 보유 골드: 0G");
            _goldLabel.AddToClassList("utk-title-label");
            _goldLabel.style.fontSize = 18f;
            _content.Add(_goldLabel);

            _statusLabel = new Label("파손된 장비를 선택하여 수리하세요.");
            _statusLabel.style.fontSize = 14f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statusLabel.style.whiteSpace = WhiteSpace.Normal;
            _statusLabel.style.marginTop = 4f;
            _content.Add(_statusLabel);

            var header = new Label("─── 파손된 장비 목록 ───");
            header.style.fontSize = 15f;
            header.style.color = new StyleColor(UTKColor.TextPrimary);
            header.style.marginTop = 8f;
            _content.Add(header);

            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.style.flexGrow = 1f;
            _scroll.style.marginTop = 4f;
            _content.Add(_scroll);

            _list = new VisualElement();
            _list.style.flexDirection = FlexDirection.Column;
            _scroll.Add(_list);

            ApplyUIToolkitFont(this);
        }

        // =====================================================================
        //  생명주기 — UTKWindowBase 훅
        // =====================================================================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 420f;
            style.top = 96f;
            _statusLabel.text = "";
            RefreshList();
            StartRefreshLoop();
            Debug.Log("[RepairUTK] 수리 스테이션 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[RepairUTK] 수리 스테이션 닫힘");
        }

        // =====================================================================
        //  폴링 갱신
        // =====================================================================

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshList();
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

        // =====================================================================
        //  데이터 소스 — 파손 장비 목록 (원본 GetDamagedEquipment 패리티)
        // =====================================================================

        private int GetPlayerGold()
        {
            var inv = PlayerInventory.Instance;
            return inv != null ? inv.GetItemCount("gold") : 0;
        }

        private void RefreshList()
        {
            int gold = GetPlayerGold();
            _goldLabel.text = $"💰 보유 골드: {gold}G";

            // 이전 행 재생성 위해 목록 초기화
            _rowBindings.Clear();
            _list.Clear();

            var inventory = PlayerInventory.Instance;
            var damaged = new List<DamagedEntry>();
            if (inventory != null)
            {
                var slots = inventory.GetAllSlots();
                if (slots != null)
                {
                    for (int i = 0; i < slots.Length; i++)
                    {
                        var slot = slots[i];
                        if (slot == null || slot.item == null || slot.count <= 0) continue;
                        if (!EquipmentRepairSystem.CanRepair(slot.item.id)) continue;
                        if (slot.currentDurability >= slot.item.maxDurability) continue; // 완전 충전 제외
                        if (slot.item.maxDurability <= 0) continue;
                        int cost = EquipmentRepairSystem.GetRepairCost(slot);
                        damaged.Add(new DamagedEntry
                        {
                            slotIndex = i,
                            itemData = slot.item,
                            currentDurability = slot.currentDurability,
                            maxDurability = slot.item.maxDurability,
                            repairCost = cost
                        });
                    }
                }
            }

            if (damaged.Count == 0)
            {
                var empty = new Label("  수리할 장비가 없습니다.");
                empty.style.fontSize = 14f;
                empty.style.color = new StyleColor(UTKColor.TextSecondary);
                _list.Add(empty);
                return;
            }

            for (int i = 0; i < damaged.Count; i++)
                BuildRow(damaged[i], gold);
        }

        private void BuildRow(DamagedEntry entry, int gold)
        {
            var row = new VisualElement();
            row.name = "RepairRow_" + entry.slotIndex;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.20f, 0.9f));
            row.style.borderLeftWidth = 1f;
            row.style.borderRightWidth = 1f;
            row.style.borderTopWidth = 1f;
            row.style.borderBottomWidth = 1f;
            row.style.borderLeftColor = new StyleColor(UTKColor.IronLine);
            row.style.borderRightColor = new StyleColor(UTKColor.IronLine);
            row.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            row.style.borderBottomColor = new StyleColor(UTKColor.IronLine);
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            row.style.paddingTop = 6f;
            row.style.paddingBottom = 6f;
            row.style.marginBottom = 3f;
            _list.Add(row);

            float ratio = entry.maxDurability > 0 ? (float)entry.currentDurability / entry.maxDurability : 0f;
            string colorMark = ratio <= 0.25f ? "🔴" : (ratio <= 0.5f ? "🟡" : "🟢");

            var info = new Label($"{colorMark} {entry.itemData.displayName}  |  내구도: {entry.currentDurability}/{entry.maxDurability}");
            info.style.flexGrow = 1f;
            info.style.fontSize = 14f;
            info.style.color = new StyleColor(UTKColor.TextPrimary);
            info.style.whiteSpace = WhiteSpace.Normal;
            row.Add(info);

            var costLabel = new Label($"💰 {entry.repairCost}G");
            costLabel.style.width = 84f;
            costLabel.style.fontSize = 14f;
            costLabel.style.color = new StyleColor(UTKColor.AccentRare);
            row.Add(costLabel);

            bool canAfford = gold >= entry.repairCost;
            var btn = UTKButton.Create(canAfford ? "수리" : "골드부족",
                () => TryRepairSlot(entry.slotIndex),
                canAfford ? UTKButton.Variant.Primary : UTKButton.Variant.Danger);
            btn.style.width = 88f;
            btn.SetEnabled(canAfford);
            row.Add(btn);

            _rowBindings.Add(new RepairRowBinding { slotIndex = entry.slotIndex });
        }

        // =====================================================================
        //  수리 실행 — 원본 TryRepairSlot 패리티
        // =====================================================================

        private void TryRepairSlot(int slotIndex)
        {
            var result = EquipmentRepairSystem.RepairInventorySlot(slotIndex);
            _statusLabel.text = result.message;
            _statusColor = result.success ? UTKColor.GuildGreen : UTKColor.HealthRed;
            _statusLabel.style.color = new StyleColor(_statusColor);

            if (result.success)
            {
                Debug.Log($"[RepairUTK] {result.message}");
            }
            else
            {
                Debug.Log($"[RepairUTK] 수리 실패: {result.message}");
            }
            RefreshList();
        }

        // =====================================================================
        //  데이터 홀더
        // =====================================================================

        private struct DamagedEntry
        {
            public int slotIndex;
            public PlayerInventory.ItemData itemData;
            public int currentDurability;
            public int maxDurability;
            public int repairCost;
        }

        private sealed class RepairRowBinding
        {
            public int slotIndex;
        }
    }
}