// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-2a — 용병 고용소 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/MercenaryHireUI.cs (372줄) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① MercenaryManager 실측 API 직접 호출 (복제 금지):
    ///     - GetAllMercenaryData() / GetHiredMercenaries() / HiredCount / MaxMercenaries
    ///     - HireMercenary(id) / FireMercenary(id) / GetAffinity(id) / TryGetMercenaryData(id, out)
    ///     - PlayerInventory.GetItemCount("gold") / RemoveItem("gold", cost) — 골드 결제.
    ///  ② 용병 목록 행: 등급별 ★표시 + 이름/직업 + 능력치 + 특수능력 + 고용 비용.
    ///     - 미고용 → 📋 고용 버튼, 고용됨 → 🔴 해고 버튼 + 호감도 표시.
    ///  ③ 📖 상세 토글 → 배경 스토리 + 호감도(보너스) 확장.
    ///  ④ 250ms 폴링 갱신 (고용/해고/골드 실시간 반영).
    ///  각 경로에 [MercUTK] Debug.Log 실측 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class MercenaryHireUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static MercenaryHireUTK _instance;
        public static MercenaryHireUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 우상단 배치. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new MercenaryHireUTK();
        }

        /// <summary>용병 고용소 열기.</summary>
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
        private const float WinW = 480f;
        private const float WinH = 620f;
        private const long RefreshMs = 250L;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _summaryLabel;
        private Label _statusLabel;
        private readonly HashSet<string> _expanded = new HashSet<string>();
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private MercenaryHireUTK() : base("🍺 용병 고용소", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _summaryLabel = new Label("🍺 용병 고용소");
            _summaryLabel.AddToClassList("utk-title-label");
            _summaryLabel.style.fontSize = 18f;
            _content.Add(_summaryLabel);

            _statusLabel = new Label("");
            _statusLabel.style.fontSize = 12f;
            _statusLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _content.Add(_statusLabel);

            _list = new VisualElement();
            _list.name = "MercenaryList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);

            // 기본 숨김 + 위치
            style.display = DisplayStyle.None;
            style.left = 540f;
            style.top = 96f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 540f;
            style.top = 96f;
            StartRefreshLoop();
            RefreshList();
            Debug.Log("[MercUTK] 용병 고용소 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[MercUTK] 용병 고용소 닫힘");
        }

        // ===== 폴링 갱신 =====

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

        // ===== 목록 갱신 (매 갱신 재조회) =====

        private void RefreshList()
        {
            _list.Clear();

            var mgr = MercenaryManager.Instance;
            if (mgr == null)
            {
                _list.Add(MakeLabel("(MercenaryManager 없음)", UTKColor.TextSecondary));
                return;
            }

            // 보유 골드 + 고용 수 (요약)
            int gold = PlayerInventory.Instance != null
                ? PlayerInventory.Instance.GetItemCount("gold")
                : 0;
            _summaryLabel.text = $"🍺 용병 고용소 — 💰 {gold}G · 👥 {mgr.HiredCount}/{mgr.MaxMercenaries}";

            // 고용된 용병 ID 집합 → O(1) 조회 (원본 _cachedHiredIds 대응)
            var hiredIds = new HashSet<string>();
            var hired = mgr.GetHiredMercenaries();
            foreach (var inst in hired)
                hiredIds.Add(inst.data.id);

            var all = mgr.GetAllMercenaryData();
            if (all.Length == 0)
            {
                _list.Add(MakeLabel("목록이 비어 있습니다.", UTKColor.TextSecondary));
                return;
            }

            foreach (var merc in all)
            {
                bool isHired = hiredIds.Contains(merc.id);
                bool isSelected = _expanded.Contains(merc.id);
                AddMercRow(mgr, merc, isHired, isSelected);
            }
        }

        // ===== 한 용병 행 =====

        private void AddMercRow(MercenaryManager mgr, MercenaryData merc, bool isHired, bool isSelected)
        {
            var row = new VisualElement();
            row.name = "MercRow_" + merc.id;
            row.style.flexDirection = FlexDirection.Column;
            row.style.borderTopWidth = 1f;
            row.style.borderTopColor = new StyleColor(UTKColor.IronLine);
            row.style.paddingTop = 4f;
            row.style.paddingBottom = 6f;

            string jobIcon = merc.jobType == "Bard" ? "🎵" : "⚔️";
            string status = isHired ? "✅ 고용됨" : "";
            row.Add(MakeLabel(
                $"{merc.GradeStars} {merc.mercenaryName}  {jobIcon} {merc.jobType}  {status}",
                UTKColor.TextPrimary));

            row.Add(MakeLabel(
                $"❤️ {merc.maxHP:F0} ⚔️ {merc.attack:F0} 🛡️ {merc.defense:F0} 💨 {merc.moveSpeed:F1}",
                UTKColor.TextSecondary));
            row.Add(MakeLabel($"✨ {merc.specialAbility}", UTKColor.TextSecondary));

            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 3f;

            // 비용 표시
            row.Add(MakeLabel($"💰 고용 비용: {merc.hireCost}G", UTKColor.BorderGold));

            if (isHired)
            {
                actions.Add(UTKButton.Create("🔴 해고", () => OnFireMercenary(mgr, merc), UTKButton.Variant.Danger));
            }
            else
            {
                actions.Add(UTKButton.Create($"📋 고용 ({merc.hireCost}G)",
                    () => OnHireMercenary(mgr, merc), UTKButton.Variant.Primary));
            }

            string detailLabel = isSelected ? "📖 접기" : "📖 상세";
            actions.Add(UTKButton.Create(detailLabel, () =>
            {
                if (isSelected) _expanded.Remove(merc.id); else _expanded.Add(merc.id);
                RefreshList();
            }, UTKButton.Variant.Secondary));

            row.Add(actions);

            // 상세 확장 (배경 스토리 + 호감도)
            if (isSelected)
            {
                row.Add(MakeLabel($"📜 {merc.backStory}", UTKColor.TextSecondary));
                if (isHired)
                {
                    float aff = mgr.GetAffinity(merc.id);
                    float bonus = aff / 100f * 0.2f;
                    row.Add(MakeLabel(
                        $"❤️ 호감도: {(int)aff}% (보너스: +{bonus * 100f:F0}%)",
                        UTKColor.GuildGreen));
                }
            }

            _list.Add(row);
        }

        // ===== 고용 / 해고 =====

        /// <summary>원본 OnHireMercenary 실측 — 골드 확인 후 HireMercenary + RemoveItem.</summary>
        private void OnHireMercenary(MercenaryManager mgr, MercenaryData merc)
        {
            int gold = PlayerInventory.Instance != null ? PlayerInventory.Instance.GetItemCount("gold") : 0;
            if (gold < merc.hireCost)
            {
                SetStatus($"⚠️ 골드 부족! 필요: {merc.hireCost}G, 보유: {gold}G");
                return;
            }

            if (mgr.HireMercenary(merc.id))
            {
                if (PlayerInventory.Instance != null)
                    PlayerInventory.Instance.RemoveItem("gold", merc.hireCost);
                Debug.Log($"[MercUTK] 🎭 용병 고용 완료: {merc.mercenaryName} ({merc.hireCost}G 지불)");
                SetStatus($"✅ {merc.mercenaryName} 고용 완료! ({merc.hireCost}G 지불)");
            }
            else
            {
                SetStatus("⚠️ 더 이상 고용할 수 없습니다. (최대 인원 초과)");
            }
            RefreshList();
        }

        /// <summary>원본 OnFireMercenary 실측.</summary>
        private void OnFireMercenary(MercenaryManager mgr, MercenaryData merc)
        {
            if (mgr.FireMercenary(merc.id))
            {
                Debug.Log($"[MercUTK] 🔴 해고됨: {merc.mercenaryName}");
                SetStatus($"🔴 {merc.mercenaryName} 해고됨.");
            }
            _expanded.Remove(merc.id);
            RefreshList();
        }

        private void SetStatus(string text)
        {
            _statusLabel.text = text ?? "";
        }

        // ===== 헬퍼 =====

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