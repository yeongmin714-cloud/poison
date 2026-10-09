// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U4 Round B-2a — 복수명부 윈도우 (UTK).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/RevengeListWindow.cs (478줄) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///  ① RevengeListManager 실측 API 직접 호출 (복제 금지):
    ///     - Entries / IsInitialized / GetCompletionCount() / GetRevealedPoisonConspiratorCount()
    ///     - GetPoisonConspirators() / TryGetEntry(id, out) / RevealReason(id) / CompleteEntry(id)
    ///  ② 영주 목록: 미발견 "???" / 공개 "이름 + ☠️" / 완료 "✅ 이름" (원본 상태 분기 재현).
    ///  ③ 상세 패널: 영주/국가/난이도/영지/복수 이유/상태 — TerritoryDatabase 실측 조회.
    ///  ④ ⚔️ 도전 버튼 → TerritoryBattleManager.StartBattle(TerritoryId) (신규, 원본 미보유 기능).
    ///  ⑤ 500ms 폴링 갱신 (공개/완료 상태 실시간 반영).
    ///  각 경로에 [RevengeUTK] Debug.Log 실측 로그.
    /// [진입점] static Open() / Ensure() / Toggle(). 순수 VisualElement 트리.
    /// </summary>
    public class RevengeListUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static RevengeListUTK _instance;
        public static RevengeListUTK Instance => _instance;

        /// <summary>팩토리 — UIRoot 중앙 배치. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new RevengeListUTK();
        }

        /// <summary>복수명부 열기.</summary>
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
        private const float WinW = 600f;
        private const float WinH = 640f;
        private const long RefreshMs = 500L;

        // ===== 레퍼런스 =====
        private readonly VisualElement _list;
        private Label _statsLabel;
        private Label _warningLabel;
        private Label _detailLabel;
        private int _selectedIndex = -1;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private RevengeListUTK() : base("🗡️ 복수명부", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 12f;
            _content.style.paddingRight = 12f;
            _content.style.paddingTop = 10f;
            _content.style.paddingBottom = 10f;

            // 경고 헤더 카드 — 통계와 원한 경고를 하나의 시각적 그룹으로 구성.
            var headerCard = new VisualElement();
            headerCard.name = "RevengeHeaderCard";
            StyleCard(headerCard, new Color32(0x16, 0x1B, 0x22, 0xFF), 8f);
            headerCard.style.marginBottom = 8f;

            _warningLabel = new Label("⚠️ 복수 대상이 남아 있습니다");
            _warningLabel.style.fontSize = 15.6f;
            _warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _warningLabel.style.color = new StyleColor(UTKColor.HealthRed);
            _warningLabel.style.marginBottom = 4f;
            headerCard.Add(_warningLabel);

            _statsLabel = new Label("🗡️ 복수명부");
            _statsLabel.style.fontSize = 12f;
            _statsLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _statsLabel.style.whiteSpace = WhiteSpace.Normal;
            headerCard.Add(_statsLabel);
            _content.Add(headerCard);

            var listScroll = new ScrollView(ScrollViewMode.Vertical);
            listScroll.name = "RevengeLordList";
            listScroll.style.flexGrow = 1f;
            listScroll.style.flexShrink = 1f;
            _list = listScroll.contentContainer;
            _list.name = "RevengeLordListContent";
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(listScroll);

            var detailCard = new VisualElement();
            detailCard.name = "RevengeDetailCard";
            StyleCard(detailCard, new Color32(0x21, 0x26, 0x2D, 0xFF), 8f);
            detailCard.style.marginTop = 8f;
            _detailLabel = new Label("영주를 선택하세요.");
            _detailLabel.style.fontSize = 13.2f;
            _detailLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            detailCard.Add(_detailLabel);
            _content.Add(detailCard);

            ApplyUIToolkitFont(this);

            // 기본 숨김 + 위치 (중앙)
            style.display = DisplayStyle.None;
            style.left = 320f;
            style.top = 120f;
        }

        // =================== 생명주기 ===================

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 320f;
            style.top = 120f;

            var mgr = RevengeListManager.Instance;
            if (mgr != null && !mgr.IsInitialized)
            {
                mgr.Initialize();
                Debug.Log("[RevengeUTK] RevengeListManager 초기화");
            }
            _selectedIndex = -1;

            StartRefreshLoop();
            RefreshList();
            Debug.Log("[RevengeUTK] 복수명부 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[RevengeUTK] 복수명부 닫힘");
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

            var mgr = RevengeListManager.Instance;
            if (mgr == null || !mgr.IsInitialized)
            {
                _warningLabel.text = "⚠️ 복수명부가 아직 준비되지 않았습니다";
                _warningLabel.style.color = new StyleColor(UTKColor.HealthRed);
                _statsLabel.text = "복수 기록을 불러올 수 없습니다.";
                _list.Add(MakeLabel("(복수명부 미초기화)", UTKColor.TextSecondary));
                return;
            }

            var all = mgr.Entries;
            int total = all.Count;
            if (total == 0)
            {
                _warningLabel.text = "✅ 처리할 원한이 없습니다";
                _warningLabel.style.color = new StyleColor(UTKColor.TextSecondary);
                _statsLabel.text = "복수명부가 비어 있습니다.";
                _list.Add(MakeLabel("복수 대상이 없습니다.", UTKColor.TextSecondary));
                _detailLabel.text = "영주를 선택하세요.";
                return;
            }

            int completed = mgr.GetCompletionCount();
            int revealedPoison = mgr.GetRevealedPoisonConspiratorCount();
            int pending = total - completed;
            _warningLabel.text = pending > 0
                ? $"⚠️ 미처리 원한 {pending}건이 남아 있습니다"
                : "✅ 모든 원한이 처리되었습니다";
            _warningLabel.style.color = new StyleColor(pending > 0 ? UTKColor.HealthRed : UTKColor.TextSecondary);
            int totalPoison = mgr.GetPoisonConspirators().Count;
            _statsLabel.text = $"🗡️ 복수명부 — 발견: {revealedPoison}/{totalPoison} 독살 공모자 | 완료: {completed}/{total}";

            if (_selectedIndex >= total) _selectedIndex = -1;

            int i = 0;
            foreach (var entry in all)
            {
                int idx = i;
                i++;
                AddLordRow(mgr, entry, idx == _selectedIndex, idx);
            }

            // 상세 갱신
            if (_selectedIndex >= 0 && _selectedIndex < total)
                RefreshDetail(mgr, all[_selectedIndex]);
            else
                _detailLabel.text = "영주를 선택하세요.";
        }

        // ===== 좌측 영주 목록 행 =====

        private void AddLordRow(RevengeListManager mgr, RevengeListEntry entry, bool isSelected, int idx)
        {
            var card = new VisualElement();
            card.name = "RevengeRow_" + entry.territoryId;
            StyleCard(card, new Color32(0x21, 0x26, 0x2D, 0xFF), 8f);
            card.style.marginBottom = 6f;
            if (isSelected)
                card.style.borderLeftColor = new StyleColor(UTKColor.HealthRed);

            // 대상 이름 / 발견 상태는 기존 분기를 그대로 유지.
            string label;
            Color color;
            if (!entry.isRevealed && !entry.isCompleted)
            {
                label = "???";
                color = UTKColor.TextSecondary;
            }
            else if (entry.isCompleted)
            {
                label = "✅ " + entry.lordName;
                color = UTKColor.TextSecondary;
            }
            else
            {
                string poisonMark = entry.isPoisonConspirator ? " ☠️" : "";
                label = entry.lordName + poisonMark;
                color = UTKColor.TextPrimary;
            }

            var heading = new VisualElement();
            heading.style.flexDirection = FlexDirection.Row;
            heading.style.alignItems = Align.Center;
            var name = MakeLabel("🗡️ " + label, color);
            name.style.fontSize = 14.4f;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            name.style.flexGrow = 1f;
            heading.Add(name);
            heading.Add(UTKButton.Create("🔎 상세", () =>
            {
                _selectedIndex = idx;
                Debug.Log($"[RevengeUTK] 대상 선택 → {entry.lordName} ({entry.territoryId})");
                RefreshList();
            }, isSelected ? UTKButton.Variant.Primary : UTKButton.Variant.Secondary));
            card.Add(heading);

            // 원한 내용도 카드 안에 표시하되, 미공개 상태에서는 내용을 감춘다.
            string reason = entry.isRevealed || entry.isCompleted
                ? (entry.isPoisonConspirator ? "☠️ " : "") + entry.revengeReason
                : "복수 이유: ???";
            var content = MakeLabel(reason, UTKColor.TextSecondary);
            content.style.marginTop = 4f;
            card.Add(content);

            if (!entry.isCompleted)
            {
                var actions = new VisualElement();
                actions.style.flexDirection = FlexDirection.Row;
                actions.style.marginTop = 6f;
                actions.Add(UTKButton.Create("⚔️ 도전", () => OnChallenge(entry), UTKButton.Variant.Primary));
                actions.Add(UTKButton.Create("🔍 추궁", () => OnInterrogate(mgr, entry), UTKButton.Variant.Secondary));
                card.Add(actions);
            }

            _list.Add(card);
        }

        // ===== 우측 상세 패널 =====

        private void RefreshDetail(RevengeListManager mgr, RevengeListEntry entry)
        {
            var sb = new StringBuilder();
            string nameStr = entry.isRevealed || entry.isCompleted ? entry.lordName : "???";
            string poisonBadge = entry.isPoisonConspirator ? " ☠️" : "";
            sb.Append($"👤 {nameStr}{poisonBadge}\n");

            // 영지 정보 — TerritoryDatabase 실측 조회
            var db = TerritoryDatabase.Instance;
            if (!TryResolveDefinition(db, entry.territoryId, out TerritoryDefinition def))
            {
                sb.Append($"(영지 정보 없음: {entry.territoryId})");
                _detailLabel.text = sb.ToString();
                return;
            }

            sb.Append($"국가: {GetNationDisplayName(def.nation)}\n");
            sb.Append($"난이도: {GetDifficultyDisplayName(def.difficulty)}\n");
            var terrStr = !string.IsNullOrEmpty(def.territoryName)
                ? def.territoryName : entry.territoryId;
            sb.Append($"영지: {terrStr}\n");

            // 복수 이유 (공개 시에만)
            if (entry.isRevealed || entry.isCompleted)
            {
                string reason = entry.isPoisonConspirator ? "☠️ " + entry.revengeReason : entry.revengeReason;
                sb.Append($"복수 이유: {reason}\n");
            }
            else
            {
                sb.Append("복수 이유: ???\n");
            }

            // 상태
            string statusText;
            Color statusColor;
            if (entry.isCompleted)
            {
                statusText = "✅ 복수 완료";
                statusColor = UTKColor.TextSecondary;
            }
            else if (entry.isRevealed)
            {
                statusText = "🔍 공개됨 (영주 사망)";
                statusColor = UTKColor.HoverGold;
            }
            else
            {
                statusText = "❓ 미발견";
                statusColor = UTKColor.TextSecondary;
            }

            // 상세 정보는 선택 항목 카드 안에 표시. 도전/추궁 버튼은 AddLordRow가 구성한다.
            _detailLabel.text = sb.ToString() + $"상태: {statusText}";
        }

        // ===== 도전 / 추궁 =====

        /// <summary>TerritoryBattleManager.StartBattle 실측 — territoryId 문자열을 TerritoryId로 역매칭.</summary>
        private void OnChallenge(RevengeListEntry entry)
        {
            var battleMgr = TerritoryBattleManager.Instance;
            if (battleMgr == null)
            {
                Debug.LogWarning("[RevengeUTK] TerritoryBattleManager 미생성 — 도전 실패");
                return;
            }

            if (!TryResolveDefinition(TerritoryDatabase.Instance, entry.territoryId, out TerritoryDefinition def))
            {
                Debug.LogWarning($"[RevengeUTK] 영지 정의 없음 — 도전 실패 ({entry.territoryId})");
                return;
            }

            battleMgr.StartBattle(def.id);
            Debug.Log($"[RevengeUTK] ⚔️ 도전 → {entry.lordName} ({def.id})");
        }

        /// <summary>RevengeListManager.Interrogate 실측 — 추궁 성공 시 이유 공개.</summary>
        private void OnInterrogate(RevengeListManager mgr, RevengeListEntry entry)
        {
            bool success = mgr.Interrogate(entry.territoryId);
            Debug.Log($"[RevengeUTK] 🔍 추궁 {(success ? "성공" : "실패")}: {entry.lordName}");
            RefreshList();
        }

        // ===== 헬퍼 =====

        /// <summary>entry.territoryId("<nation>_<index>") → TerritoryDefinition 역매칭.</summary>
        private static bool TryResolveDefinition(TerritoryDatabase db, string territoryId, out TerritoryDefinition def)
        {
            def = default;
            if (db == null || string.IsNullOrEmpty(territoryId)) return false;
            foreach (var cand in db.GetAllDefinitions())
            {
                if (cand.id.ToString() == territoryId)
                {
                    def = cand;
                    return true;
                }
            }
            return false;
        }

        private static void StyleCard(VisualElement card, Color background, float radius)
        {
            card.style.flexDirection = FlexDirection.Column;
            card.style.backgroundColor = new StyleColor(background);
            card.style.paddingTop = 10f;
            card.style.paddingBottom = 10f;
            card.style.paddingLeft = 12f;
            card.style.paddingRight = 12f;
            card.style.borderTopWidth = 1f;
            card.style.borderBottomWidth = 1f;
            card.style.borderLeftWidth = 1f;
            card.style.borderRightWidth = 1f;
            var stroke = new StyleColor(new Color32(0x2E, 0x34, 0x3D, 0xFF));
            card.style.borderTopColor = stroke;
            card.style.borderBottomColor = stroke;
            card.style.borderLeftColor = stroke;
            card.style.borderRightColor = stroke;
            card.style.borderTopLeftRadius = radius;
            card.style.borderTopRightRadius = radius;
            card.style.borderBottomLeftRadius = radius;
            card.style.borderBottomRightRadius = radius;
        }

        private static Label MakeLabel(string text, Color color)
        {
            var l = new Label(text);
            l.style.fontSize = 12f;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        private static string GetNationDisplayName(NationType nation)
        {
            return nation switch
            {
                NationType.East => "동 (East)",
                NationType.West => "서 (West)",
                NationType.South => "남 (South)",
                NationType.North => "북 (North)",
                NationType.Empire => "황제국 (Empire)",
                _ => "미소속"
            };
        }

        private static string GetDifficultyDisplayName(TerritoryDifficulty diff)
        {
            return diff switch
            {
                TerritoryDifficulty.Ring1 => "🟢 쉬움 (Ring 1)",
                TerritoryDifficulty.Ring2 => "🟡 보통 (Ring 2)",
                TerritoryDifficulty.Ring3 => "🟠 어려움 (Ring 3)",
                TerritoryDifficulty.Ring4 => "🔴 매우 어려움 (Ring 4)",
                TerritoryDifficulty.Empire => "👑 최종 (Empire)",
                _ => "알 수 없음"
            };
        }
    }
}