using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-2b — ⚡ 빠른 이동 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/FastTravelUI.cs (421줄 IMGUI) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① 영지 목록 — FastTravelSystem.GetPlayerOwnedTerritories 실측 API 직접 호출.
    ///  ② 비용 — FastTravelSystem.GetTravelCost(def.difficulty) + CanAffordTravel(cost) 실측.
    ///  ③ 보유 골드 — PlayerInventory.GetItemCount("gold") 실측.
    ///  ④ 확인 — 목록 클릭 시 창 내부 확인 패널(목적지/Ring/비용) 표시, [✅ 이동] 실행.
    ///  ⑤ 이동 — FastTravelSystem.ExecuteFastTravel(def.id) 실측 호출 후 창 닫기.
    ///  400ms 폴링으로 골드/목록 갱신. 각 경로에 [FastUTK] UnityEngine.Debug 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class FastTravelUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static FastTravelUTK _instance;
        public static FastTravelUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new FastTravelUTK();
        }

        /// <summary>빠른 이동 창 열기.</summary>
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
            _instance.Show();
        }

        // ===== 설정 =====
        private const float WinW = 520f;
        private const float WinH = 600f;
        private const long RefreshMs = 400L;

        // ===== 상태 =====
        private List<TerritoryDefinition> _owned = new List<TerritoryDefinition>();
        private TerritoryDefinition? _confirmDef;    // 확인 패널 표시 여부

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _goldLabel;
        private Label _summaryLabel;
        private IVisualElementScheduledItem _refreshTask;

        private FastTravelUTK() : base("⚡ 빠른 이동", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("⚡ 빠른 이동");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _goldLabel = new Label("💰 보유 골드: 0G");
            _goldLabel.style.fontSize = 14f;
            _goldLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(_goldLabel);

            _list = new VisualElement();
            _list.name = "FastTravelList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 420f;
            style.top = 110f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 420f;
            style.top = 110f;
            _confirmDef = null;
            RefreshList();
            StartRefreshLoop();
            Debug.Log("[FastUTK] ⚡ 빠른 이동 창 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            _confirmDef = null;
            Debug.Log("[FastUTK] 빠른 이동 창 닫힘");
        }

        // ===== 폴링 갱신 (400ms) =====

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
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

        // ===== 목록 갱신 =====

        private void RefreshList()
        {
            RefreshOwnedTerritories();
            RefreshGold();

            _list.Clear();
            if (_confirmDef.HasValue)
            {
                DrawConfirm();
                return;
            }

            if (_owned.Count == 0)
            {
                _list.Add(MakeLabel("소유한 영지가 없습니다.\n영지를 정복한 후 이용하세요.", UTKColor.TextSecondary));
                return;
            }

            _list.Add(MakeLabel("이동할 영지를 선택하세요", UTKColor.TextSecondary));
            foreach (var def in _owned)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.borderTopWidth = 1f;
                row.style.borderTopColor = new StyleColor(UTKColor.IronLine);
                row.style.paddingTop = 4f;
                row.style.paddingBottom = 4f;
                row.style.alignItems = Align.Center;

                int cost = FastTravelSystem.Instance != null
                    ? FastTravelSystem.Instance.GetTravelCost(def.difficulty)
                    : 5;
                bool canAfford = FastTravelSystem.Instance != null
                    && FastTravelSystem.Instance.CanAffordTravel(cost);
                string text = $"{def.territoryName}  |  {GetRingText(def.difficulty)}  |  {cost}G";
                var info = MakeLabel(text, canAfford ? UTKColor.TextPrimary : UTKColor.TextSecondary);
                info.style.flexGrow = 1f;
                row.Add(info);

                row.Add(UTKButton.Create(canAfford ? "이동" : "골드 부족", () =>
                {
                    if (!canAfford)
                    {
                        Debug.Log("[FastUTK] ⚠️ 골드가 부족하여 이동할 수 없습니다.");
                        return;
                    }
                    _confirmDef = def;
                    Debug.Log($"[FastUTK] 영지 선택 → {def.territoryName} (비용 {cost}G)");
                    RefreshList();
                }, canAfford ? UTKButton.Variant.Primary : UTKButton.Variant.Danger));

                _list.Add(row);
            }

            _list.Add(UTKButton.Create("[ ESC ] 닫기", () => Close(), UTKButton.Variant.Secondary));
        }

        // ===== 확인 패널 =====

        private void DrawConfirm()
        {
            var def = _confirmDef.Value;
            _summaryLabel.text = "🚀 빠른 이동 확인";
            _list.Add(MakeLabel($"「{def.territoryName}」(으)로 이동하시겠습니까?", UTKColor.TextPrimary));

            int cost = FastTravelSystem.Instance != null
                ? FastTravelSystem.Instance.GetTravelCost(def.difficulty)
                : 5;
            AddInfoRow("영지:", def.territoryName);
            AddInfoRow("Ring:", GetRingText(def.difficulty));
            AddInfoRow("비용:", $"{cost}G");

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.marginTop = 8f;
            btnRow.Add(UTKButton.Create("✅ 이동", ExecuteFastTravel, UTKButton.Variant.Primary));
            btnRow.Add(UTKButton.Create("❌ 취소", () =>
            {
                _confirmDef = null;
                RefreshList();
            }, UTKButton.Variant.Danger));
            _list.Add(btnRow);
        }

        /// <summary>빠른 이동 실행 — FastTravelSystem.ExecuteFastTravel 실측 호출.</summary>
        private void ExecuteFastTravel()
        {
            var def = _confirmDef.Value;
            if (FastTravelSystem.Instance == null)
            {
                Debug.LogError("[FastUTK] FastTravelSystem.Instance가 null입니다!");
                return;
            }
            FastTravelSystem.Instance.ExecuteFastTravel(def.id);
            Debug.Log($"[FastUTK] 🚀 빠른 이동 실행 → {def.territoryName} ({def.id})");
            _confirmDef = null;
            Close();
        }

        // ===== 헬퍼 =====

        private void RefreshGold()
        {
            int gold = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetItemCount("gold")
                : 0;
            _goldLabel.text = $"💰 보유 골드: {gold}G";
        }

        /// <summary>소유 영지 새로고침 (FastTravelSystem 실측).</summary>
        private void RefreshOwnedTerritories()
        {
            if (FastTravelSystem.Instance == null) return;
            _owned = FastTravelSystem.Instance.GetPlayerOwnedTerritories();
        }

        /// <summary>Ring 난이도 표시 문자열 (원본 GetRingText).</summary>
        private static string GetRingText(TerritoryDifficulty difficulty)
        {
            switch (difficulty)
            {
                case TerritoryDifficulty.Ring1: return "Ring 1 🟢";
                case TerritoryDifficulty.Ring2: return "Ring 2 🟡";
                case TerritoryDifficulty.Ring3: return "Ring 3 🟠";
                case TerritoryDifficulty.Ring4: return "Ring 4 🔴";
                case TerritoryDifficulty.Empire: return "Empire 👑";
                default: return "알 수 없음";
            }
        }

        private void AddInfoRow(string label, string value)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 2f;
            var l = MakeLabel(label, UTKColor.TextSecondary);
            l.style.width = 70f;
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
    }
}