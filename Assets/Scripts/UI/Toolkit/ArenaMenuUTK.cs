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
    /// UI Toolkit Phase U6 Round C — 아레나 메뉴 윈도우 (UTK).
    /// 원본: Assets/Scripts/UI/ArenaMenuUI.cs (610줄, IMGUI) — 본 파일은 데이터 경로만
    /// UTKWindowBase 파생으로 이식한 별도 파일. 원본은 절대 수정하지 않는다.
    ///
    /// [데이터 경로 — 원본 실측]
    ///  - 아레나 상태: ArenaSystem.Instance
    ///    🔥 연승: CurrentWinStreak / BestWinStreak / TotalWins
    ///    💰 참가비: GetCurrentEntryFee()
    ///    ✨ 연승 보너스: GetWinStreakMultiplier()
    ///    ⚠️ 참가 가능: CanParticipate() (null이면 가능) + IsInBattle
    ///    👤 플레이어: PlayerStats.Instance.Level
    ///    🪖 병사 목록: GuardManager.Instance.GetAllPlayerGuards() → GuardName/Level/HP/MaxHP/IsAlive
    ///    ⭐ 용병 목록: MercenaryManager.Instance.GetHiredMercenaries() → data.mercenaryName/GradeStars/maxHP, currentHP, isAlive
    ///    ⚔️ 전투 시작: ArenaSystem.StartPlayerFight() / StartGuardFight(guard) / StartMercenaryFight(merc)
    ///      (IEnumerator 코루틴 — Updater MonoBehaviour 경유로 실행)
    ///    📋 최근 전투 로그: GetCurrentBattleLog() → ArenaBattleLog
    ///
    /// 구조: [메인] 연승/참가비/보너스/참가조건 요약 + [직접 싸우기][병사/용병 출전],
    ///       [선택] 병사/용병 출전 골라 ⚔️ 출전, [로그] 최근 전투 결과 요약 + 확인.
    /// 폴링 500ms(schedule.Execute().Every, Pause 정지) — ESC 닫기 (Updater).
    /// [ArenaMenuUTK] 로그.
    /// </summary>
    public class ArenaMenuUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static ArenaMenuUTK _instance;
        public static ArenaMenuUTK Instance => _instance;

        /// <summary>팩토리 — 멱등 생성 + Updater 컴포넌트 부착.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new ArenaMenuUTK();
            var go = new GameObject("ArenaMenuUTK_UPD");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>().window = _instance;
            Debug.Log("[ArenaMenuUTK] 인스턴스 생성");
        }

        /// <summary>아레나 메뉴 열기.</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>토글(닫혀있으면 열고, 열려있으면 닫음).</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Close(); return; }
            Open();
        }

        // ===== 설정 =====
        private const float WinW = 620f;
        private const float WinH = 560f;
        private const long RefreshMs = 500L;

        // ===== 상태 =====
        private enum ArenaMenuTab { Main, GuardSelect }
        private ArenaMenuTab _tab = ArenaMenuTab.Main;
        private int _selectedIndex = -1;
        private readonly List<GuardPlaceholder> _guards = new List<GuardPlaceholder>();
        private MercenaryInstance[] _mercs;
        private VisualElement _list;
        private UnityEngine.UIElements.IVisualElementScheduledItem _refreshTask;

        private ArenaMenuUTK() : base("⚔️ 아레나", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;

            _list = new VisualElement { name = "ArenaList" };
            _list.style.flexGrow = 1f;
            _list.style.flexDirection = FlexDirection.Column;
            _content.Add(_list);

            ApplyUIToolkitFont(this);
            style.display = DisplayStyle.None;
            style.left = 340f;
            style.top = 80f;
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            _tab = ArenaMenuTab.Main;
            _selectedIndex = -1;
            StartRefreshLoop();
            RefreshDisplay();
            Debug.Log("[ArenaMenuUTK] 아레나 메뉴 열림");
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
            Debug.Log("[ArenaMenuUTK] 아레나 메뉴 닫힘");
        }

        // ===== 폴링 루프 =====

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (IsOpen) RefreshDisplay();
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

        // ===== 데이터 경로 (원본 실측) =====

        private void RefreshDisplay()
        {
            _list.Clear();
            if (_tab == ArenaMenuTab.GuardSelect) DrawGuardSelect();
            else DrawMain();
        }

        // ---------- 메인 탭 ----------

        private void DrawMain()
        {
            var arena = ArenaSystem.Instance;
            if (arena == null)
            {
                _list.Add(MakeLabel("(ArenaSystem 미생성)", UTKColor.TextSecondary));
                return;
            }

            _list.Add(SectionLabel("⚔️ 아레나"));

            // 🔥 연승 기록
            int streak = arena.CurrentWinStreak;
            int best = arena.BestWinStreak;
            int total = arena.TotalWins;
            _list.Add(ValueRow($"🔥 현재 연승: {streak}연승 | 최고: {best}연승 | 총 승리: {total}회"));

            // 💰 참가비
            int fee = arena.GetCurrentEntryFee();
            _list.Add(TextRow($"💰 참가비: {fee}G"));

            // ✨ 연승 보너스
            float bonus = arena.GetWinStreakMultiplier();
            _list.Add(TextRow(bonus > 1f
                ? $"✨ 현재 연승 보너스: x{bonus:F1}"
                : "⚡ 2연승 이상 시 보너스 적용!"));

            // 👤 플레이어 레벨
            int level = PlayerStats.Instance != null ? PlayerStats.Instance.Level : 1;
            _list.Add(TextRow($"👤 플레이어 Lv.{level}"));

            // ⚠️ 참가 가능 여부
            string checkMsg = arena.CanParticipate();
            if (checkMsg != null)
                _list.Add(TextRow($"⚠️ {checkMsg}", UTKColor.HealthRed));

            bool canParticipate = checkMsg == null && !arena.IsInBattle;

            _list.Add(new VisualElement { style = { height = 8f } });

            // 버튼 행
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            if (canParticipate)
            {
                row.Add(UTKButton.Create("⚔️ 직접 싸우기", () => OnPlayerFight(arena), UTKButton.Variant.Primary));
                row.Add(UTKButton.Create("🪖 병사/용병 출전", () => { OpenGuardSelect(); }, UTKButton.Variant.Secondary));
            }
            else
            {
                row.Add(MakeLabel("(전투 진행 중이거나 참가 불가)", UTKColor.TextSecondary));
            }
            _list.Add(row);

            _list.Add(new VisualElement { style = { height = 6f } });

            // 최근 전투 결과 요약 (있다면)
            var log = arena.GetCurrentBattleLog();
            if (log != null)
            {
                string title = log.isVictory ? "⚔️ 전투 결과 — 승리!" : "⚔️ 전투 결과 — 패배...";
                _list.Add(SectionLabel(title, log.isVictory ? UTKColor.GuildGreen : UTKColor.HealthRed));
                _list.Add(TextRow($"🥊 {log.fighterName} vs {log.opponentName}"));
                _list.Add(TextRow($"📊 총 {log.totalRounds}라운드 진행"));
                if (log.isVictory)
                {
                    _list.Add(ValueRow($"💰 보상: {log.rewardGold}G"));
                    if (log.bonusMultiplier > 1f)
                        _list.Add(TextRow($"✨ 연승 보너스 x{log.bonusMultiplier:F1} 적용!", UTKColor.AccentRare));
                    if (log.legendaryReward)
                        _list.Add(TextRow("🏆 전설 보상: 아레나 챔피언 토큰 획득!", UTKColor.HoverGold));
                }
            }
        }

        private void OnPlayerFight(ArenaSystem arena)
        {
            if (arena.CanParticipate() != null) return;
            Updater.StartFight(arena, null, null);
            Debug.Log("[ArenaMenuUTK] ⚔️ 직접 싸우기 시작");
        }

        // ---------- 병사/용병 선택 탭 ----------

        private void OpenGuardSelect()
        {
            _tab = ArenaMenuTab.GuardSelect;
            _selectedIndex = -1;
            RefreshDisplay();
        }

        private void DrawGuardSelect()
        {
            _list.Add(SectionLabel("🪖 병사/용병 선택"));

            _guards.Clear();
            if (GuardManager.Instance != null)
            {
                foreach (var g in GuardManager.Instance.GetAllPlayerGuards())
                {
                    if (g != null && g.IsAlive)
                        _guards.Add(g);
                }
            }
            _mercs = MercenaryManager.Instance != null
                ? MercenaryManager.Instance.GetHiredMercenaries()
                : new MercenaryInstance[0];

            int totalItems = _guards.Count + CountAliveMercs(_mercs);
            if (totalItems == 0)
            {
                _list.Add(MakeLabel("출전 가능한 병사나 용병이 없습니다.", UTKColor.TextSecondary));
                return;
            }

            int index = 0;
            // 병사
            foreach (var guard in _guards)
            {
                int idx = index;
                index++;
                string label = $"🪖 {guard.GuardName} Lv.{guard.Level}  ❤️{guard.HP:F0}/{guard.MaxHP:F0}";
                AddSelectableRow(label, idx, idx == _selectedIndex);
            }

            // 용병
            foreach (var merc in _mercs)
            {
                if (!merc.isAlive) continue;
                int idx = index;
                index++;
                string label = $"⭐ {merc.data.mercenaryName} {merc.data.GradeStars}  ❤️{merc.currentHP:F0}/{merc.data.maxHP:F0}";
                AddSelectableRow(label, idx, idx == _selectedIndex);
            }

            _list.Add(new VisualElement { style = { height = 8f } });

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;

            bool canFight = _selectedIndex >= 0 && _selectedIndex < totalItems;
            if (canFight)
            {
                row.Add(UTKButton.Create("⚔️ 출전!", () => StartFightSelected(), UTKButton.Variant.Primary));
            }
            row.Add(UTKButton.Create("🔙 뒤로", () => { _tab = ArenaMenuTab.Main; RefreshDisplay(); }, UTKButton.Variant.Secondary));
            _list.Add(row);
        }

        private static int CountAliveMercs(MercenaryInstance[] mercs)
        {
            int n = 0;
            foreach (var m in mercs) { if (m.isAlive) n++; }
            return n;
        }

        private void AddSelectableRow(string label, int idx, bool isSelected)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2f;

            var text = MakeLabel(label, isSelected ? UTKColor.AccentRare : UTKColor.TextPrimary);
            text.style.flexGrow = 1f;
            row.Add(text);

            row.Add(UTKButton.Create(isSelected ? "✓ 선택됨" : "선택",
                () =>
                {
                    _selectedIndex = idx;
                    Debug.Log($"[ArenaMenuUTK] 출전 대상 선택 → index {idx}");
                    RefreshDisplay();
                },
                isSelected ? UTKButton.Variant.Primary : UTKButton.Variant.Secondary));

            _list.Add(row);
        }

        private void StartFightSelected()
        {
            var arena = ArenaSystem.Instance;
            if (arena == null || _selectedIndex < 0) return;

            int guardCount = _guards.Count;
            if (_selectedIndex < guardCount)
            {
                var guard = _guards[_selectedIndex];
                if (guard != null)
                {
                    Updater.StartFight(arena, guard, null);
                    Debug.Log($"[ArenaMenuUTK] 🪖 출전: {guard.GuardName}");
                }
            }
            else
            {
                int mercIndex = _selectedIndex - guardCount;
                if (_mercs != null && mercIndex >= 0 && mercIndex < _mercs.Length)
                {
                    Updater.StartFight(arena, null, _mercs[mercIndex]);
                    Debug.Log($"[ArenaMenuUTK] ⭐ 출전: {_mercs[mercIndex].data.mercenaryName}");
                }
            }
            _tab = ArenaMenuTab.Main;
        }

        // ===== 헬퍼 (UI) =====

        private static Label MakeLabel(string text, Color color, float size = 13f)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        private static Label SectionLabel(string text, Color? color = null)
        {
            var l = new Label(text);
            l.style.fontSize = 17f;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.color = new StyleColor(color ?? UTKColor.AccentMagic);
            l.style.marginTop = 6f;
            l.style.marginBottom = 2f;
            return l;
        }

        private static Label TextRow(string text, Color? color = null)
        {
            var l = MakeLabel(text, color ?? UTKColor.TextPrimary);
            l.style.marginBottom = 2f;
            return l;
        }

        private static VisualElement ValueRow(string text)
        {
            return MakeLabel(text, UTKColor.HoverGold, 14f);
        }

        // ===== 키/ESC + 코루틴 실행용 Updater =====

        private class Updater : MonoBehaviour
        {
            public ArenaMenuUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);

                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb != null && window != null && kb.escapeKey.wasPressedThisFrame && window.IsOpen)
                    window.Close();
            }

            /// <summary>원본 ArenaSystem의 IEnumerator 전투 시작 메서드를 코루틴으로 실행.</summary>
            public static void StartFight(ArenaSystem arena, GuardPlaceholder guard, MercenaryInstance? merc)
            {
                if (arena == null) return;
                if (guard != null)
                {
                    arena.StartCoroutine(arena.StartGuardFight(guard));
                }
                else if (merc.HasValue)
                {
                    arena.StartCoroutine(arena.StartMercenaryFight(merc.Value));
                }
                else
                {
                    arena.StartCoroutine(arena.StartPlayerFight());
                }
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopRefreshLoop();
                    window.RemoveFromHierarchy();
                }
            }
        }
    }
}