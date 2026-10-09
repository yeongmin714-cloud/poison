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
        private Label _costHeaderLabel;
        private IVisualElementScheduledItem _refreshTask;

        private FastTravelUTK() : base("⚡ 빠른 이동", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            var headerCard = new VisualElement();
            ApplyCardStyle(headerCard, UTKTheme.Panel, 10f);
            headerCard.style.marginBottom = 10f;

            _summaryLabel = new Label("⚡ 빠른 이동");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 16.8f;
            _summaryLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            headerCard.Add(_summaryLabel);

            _goldLabel = new Label("💰 보유 골드: 0G");
            _goldLabel.style.fontSize = 14.4f;
            _goldLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _goldLabel.style.marginTop = 4f;
            headerCard.Add(_goldLabel);

            _costHeaderLabel = new Label("이동 비용은 목적지 난이도에 따라 달라집니다.");
            _costHeaderLabel.style.fontSize = 12f;
            _costHeaderLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _costHeaderLabel.style.marginTop = 3f;
            headerCard.Add(_costHeaderLabel);
            _content.Add(headerCard);

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
                int cost = FastTravelSystem.Instance != null
                    ? FastTravelSystem.Instance.GetTravelCost(def.difficulty)
                    : 5;
                bool canAfford = FastTravelSystem.Instance != null
                    && FastTravelSystem.Instance.CanAffordTravel(cost);

                var card = new VisualElement();
                ApplyCardStyle(card, UTKTheme.PanelSub, 10f);
                card.style.marginTop = 5f;
                card.style.marginBottom = 5f;

                var info = new VisualElement();
                info.style.flexGrow = 1f;
                info.style.flexDirection = FlexDirection.Column;
                info.style.justifyContent = Justify.Center;

                var name = MakeLabel(def.territoryName, UTKColor.TextPrimary);
                name.style.fontSize = 15.6f;
                name.style.unityFontStyleAndWeight = FontStyle.Bold;
                info.Add(name);

                var details = MakeLabel($"{GetRingText(def.difficulty)}  ·  이동 비용 {cost}G", UTKColor.TextSecondary);
                details.style.marginTop = 3f;
                info.Add(details);

                var actionRow = new VisualElement();
                actionRow.style.flexDirection = FlexDirection.Row;
                actionRow.style.alignItems = Align.Center;
                actionRow.style.marginTop = 8f;
                actionRow.Add(info);
                actionRow.Add(UTKButton.Create(canAfford ? "이동" : "골드 부족", () =>
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

                card.Add(actionRow);
                _list.Add(card);
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

        private static void ApplyCardStyle(VisualElement card, Color background, float padding)
        {
            card.style.backgroundColor = new StyleColor(background);
            card.style.paddingTop = padding;
            card.style.paddingBottom = padding;
            card.style.paddingLeft = padding;
            card.style.paddingRight = padding;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            card.style.borderTopColor = new StyleColor(UTKTheme.Stroke);
            card.style.borderBottomColor = new StyleColor(UTKTheme.Stroke);
            card.style.borderLeftColor = new StyleColor(UTKTheme.Stroke);
            card.style.borderRightColor = new StyleColor(UTKTheme.Stroke);
            card.style.borderTopLeftRadius = 8f;
            card.style.borderTopRightRadius = 8f;
            card.style.borderBottomLeftRadius = 8f;
            card.style.borderBottomRightRadius = 8f;
        }

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 13.2f;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }
    }
}