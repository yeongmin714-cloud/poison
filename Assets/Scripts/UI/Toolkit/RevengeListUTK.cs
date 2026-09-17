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
        private Label _detailLabel;
        private int _selectedIndex = -1;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private RevengeListUTK() : base("🗡️ 복수명부", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _statsLabel = new Label("🗡️ 복수명부");
            _statsLabel.AddToClassList("utk-title-label");
            _statsLabel.style.fontSize = 17f;
            _content.Add(_statsLabel);

            _list = new VisualElement();
            _list.name = "RevengeLordList";
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            _detailLabel = new Label("영주를 선택하세요.");
            _detailLabel.style.fontSize = 13f;
            _detailLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            _detailLabel.style.whiteSpace = WhiteSpace.Normal;
            _content.Add(_detailLabel);

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
                _list.Add(MakeLabel("(복수명부 미초기화)", UTKColor.TextSecondary));
                return;
            }

            var all = mgr.Entries;
            int total = all.Count;
            if (total == 0)
            {
                _list.Add(MakeLabel("복수 대상이 없습니다.", UTKColor.TextSecondary));
                return;
            }

            int completed = mgr.GetCompletionCount();
            int revealedPoison = mgr.GetRevealedPoisonConspiratorCount();
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
            var row = new VisualElement();
            row.name = "RevengeRow_" + entry.territoryId;
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 1f;

            // 상태 라벨 (원본 상태 분기 재현)
            string label;
            Color color;
            if (!entry.isRevealed && !entry.isCompleted)
            {
                label = "???";                         // 미발견 (회색)
                color = UTKColor.TextSecondary;
            }
            else if (entry.isCompleted)
            {
                label = "✅ " + entry.lordName;        // 완료 (묵은색)
                color = UTKColor.TextSecondary;
            }
            else
            {
                string poisonMark = entry.isPoisonConspirator ? " ☠️" : "";
                label = entry.lordName + poisonMark;   // 공개 (주황 계열)
                color = UTKColor.HoverGold;
            }

            var text = MakeLabel(label, color);
            text.style.flexGrow = 1f;
            row.Add(text);

            row.Add(UTKButton.Create("🔎", () =>
            {
                _selectedIndex = idx;
                Debug.Log($"[RevengeUTK] 대상 선택 → {entry.lordName} ({entry.territoryId})");
                RefreshList();
            }, isSelected ? UTKButton.Variant.Primary : UTKButton.Variant.Secondary));

            _list.Add(row);
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

            // ⚔️ 도전 버튼 행 (미완료 대상만)
            string challenge = entry.isCompleted ? "" : "\n\n[⚔️ 도전] 대상 영지에 전투를 시작합니다.";
            _detailLabel.text = sb.ToString() + $"상태: {statusText}{challenge}";

            // 도전 버튼 (미완료 시)
            if (!entry.isCompleted)
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginTop = 4f;
                row.Add(UTKButton.Create("⚔️ 도전",
                    () => OnChallenge(entry), UTKButton.Variant.Primary));
                row.Add(UTKButton.Create("🔍 추궁",
                    () => OnInterrogate(mgr, entry), UTKButton.Variant.Secondary));
                _list.Add(row);
            }
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