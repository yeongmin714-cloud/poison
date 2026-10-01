using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // CombatLog, CombatLogEntry, ProjectName.Systems.LogType

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C → [P5 Figma daily-combat-log 카드형 재구성] — 전투 로그 창.
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/CombatLogUI.cs, 데이터: Systems/CombatLog.cs — 절대 수정 금지.
    ///
    /// [기능 — 보존]
    ///   ① 원본 CombatLog.GetRecentEntries(100) 실측 — 별도 큐 불필요 (원본이 최대100 유지).
    ///   ② L키 토글 / ESC 닫기 (원본 입력 규약 동일). 상시 HUD — 순수 VisualElement.
    ///   ③ 전체 지우기 버튼 → CombatLog.Clear()
    ///   ④ 편의 정적 AddLog → 원본 CombatLog.AddEntry 위임
    ///   ⑤ MonoBehaviour Updater 300ms 폴링
    ///
    /// [P5 Figma daily-combat-log 카드형 재배열 — 데이터 소스 동일]
    ///  - LogList: BattleLogItem 카드 — MetaInfo(타임스탬프 HH:MM:SS + 지역/메시지 굵게)
    ///    + BattleResultRow(교전 세력 + 결과 배지). 최신 로그 테두리 강조.
    ///  - SummaryFooter: "기록된 최근 교전 수" + "N / 50 세션 로드됨"
    ///  - 게임 CombatLog가 단일 message 문자열이라 Figma SF '교전세력/결과 2열'을 강제 파싱하지 않고,
    ///    카드형 재배열 + 타입 결과 배지(Damage/Heal/Kill/Warning/Normal)로 매핑한다.
    /// </summary>
    public class CombatLogUTK : VisualElement
    {
        private static CombatLogUTK _instance;
        public static CombatLogUTK Instance => _instance;

        private const float PollInterval = 0.3f;           // 300ms 폴링
        private const int MaxVisible = 100;                // 원본 MAX_VISIBLE_ENTRIES

        private readonly ScrollView _scroll;
        private readonly Button _clearBtn;
        private Label _footerCount;
        private bool _isVisible;
        private int _lastRenderCount = -1;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 전투로그 창 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  Theme.uss / 공용 UTKButton·타 UTK 창은 절대 수정하지 않는다.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/활성/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 레드(GitHub-dark 토큰)
            public static readonly Color Success  = Hex(0x3FB950);   // success 그린
            public static readonly Color Warn     = Hex(0xD29922);   // attention 옐로

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — IStyle에 borderWidth 쇼트핸드가
        ///   없어 4면 개별 대입. 호버는 진입/이탈 콜백으로 밝기 계층만 토글(시각만).</summary>
        private static void StyleButton(Button btn, UTKButton.Variant variant)
        {
            if (btn == null) return;
            Color baseBg, hoverBg, textColor;
            switch (variant)
            {
                case UTKButton.Variant.Primary:
                    baseBg = GitHubDark.Accent; hoverBg = new Color32(0x79, 0xC0, 0xFF, 0xFF); textColor = GitHubDark.BgBase; break;
                case UTKButton.Variant.Danger:
                    baseBg = GitHubDark.Danger; hoverBg = new Color32(0xDA, 0x36, 0x33, 0xFF); textColor = GitHubDark.TextMain; break;
                default:
                    baseBg = GitHubDark.PanelSub; hoverBg = GitHubDark.Stroke; textColor = GitHubDark.TextMain; break;
            }

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);   // 우드 베이크 이미지 제거
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;   // 서브 반경 r6

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            Ensure();
            EnsureUpdater();
        }

        /// <summary>싱글턴 보장 — UIRoot에 부재 시 생성·부착. 멱등.</summary>
        public static void Ensure()
        {
            if (_instance != null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            _instance = new CombatLogUTK();
            root.Add(_instance);
        }

        private static void EnsureUpdater()
        {
            if (_instance != null) return;
            var go = new GameObject("CombatLogUTK");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
            Debug.Log("[CombatLogUTK] 부트스트랩 완료 — Updater 등록, 전투 로그 L키 준비");
        }

        private CombatLogUTK()
        {
            name = "CombatLogUIBar";
            style.position = Position.Absolute;
            style.left = 24f;
            style.top = 40f;
            style.width = 620f;    // Figma daily-combat-log(94:6) BattleLogPanel 620×820
            style.height = 820f;
            // [GitHub-dark] 창 본체 — 브론즈 베벨 제거, 다크 패널 + 1px 스트로크 + r8 (이 창 한정)
            style.backgroundColor = new StyleColor(GitHubDark.Panel);
            style.borderTopWidth = 1f;
            style.borderBottomWidth = 1f;
            style.borderLeftWidth = 1f;
            style.borderRightWidth = 1f;
            style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.display = DisplayStyle.None;

            // 제목 [P5] — 전투 소식 + 세션 수
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            var title = new Label("⚔️ 전투 소식");
            title.style.fontSize = 16f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(GitHubDark.TextMain);
            titleRow.Add(title);
            var titleSpacer = new VisualElement();
            titleSpacer.style.flexGrow = 1f;
            titleRow.Add(titleSpacer);
            var sessionLbl = new Label("DAILY COMBAT LOG");
            sessionLbl.style.fontSize = 11f;
            sessionLbl.style.color = new StyleColor(GitHubDark.TextSub);
            titleRow.Add(sessionLbl);
            Add(titleRow);

            // 스크롤 로그 [P5] — BattleLogItem 카드 목록
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.name = "LogList";
            _scroll.style.flexGrow = 1f;
            _scroll.style.marginTop = 4f;
            Add(_scroll);

            // [P5] 요약 풋터 — 기록된 최근 교전 수
            var footerRow = new VisualElement();
            footerRow.name = "SummaryFooter";
            footerRow.style.flexDirection = FlexDirection.Row;
            footerRow.style.alignItems = Align.Center;
            footerRow.style.marginTop = 4f;
            footerRow.style.height = 1f;
            footerRow.style.backgroundColor = new StyleColor(GitHubDark.Stroke);
            Add(footerRow);

            var footerInner = new VisualElement();
            footerInner.style.flexDirection = FlexDirection.Row;
            footerInner.style.alignItems = Align.Center;
            footerInner.style.marginTop = 3f;
            var fLabel = new Label("기록된 최근 교전 수");
            fLabel.style.fontSize = 13f;
            fLabel.style.color = new StyleColor(GitHubDark.TextSub);
            footerInner.Add(fLabel);
            var fPad = new VisualElement();
            fPad.style.flexGrow = 1f;
            footerInner.Add(fPad);
            _footerCount = new Label("0 / 50 세션 로드됨");
            _footerCount.style.fontSize = 13f;
            _footerCount.style.color = new StyleColor(GitHubDark.Accent);
            footerInner.Add(_footerCount);
            Add(footerInner);

            // 전체 지우기
            _clearBtn = UTKButton.Create("전체 지우기", () =>
            {
                CombatLog.Clear();
                _lastRenderCount = -1;
                Refresh();
                Debug.Log("[CombatLogUTK] 전체 로그 지움 (원본 CombatLog.Clear)");
            }, UTKButton.Variant.Danger);
            _clearBtn.style.marginTop = 5f;
            StyleButton(_clearBtn, UTKButton.Variant.Danger);
            Add(_clearBtn);

            UTKWindowBase.ApplyUIToolkitFont(this);
            Refresh();
        }

        /// <summary>원본 피드 직접 접근 — CombatLog.AddEntry 위임.</summary>
        public static void AddLog(string message, ProjectName.Systems.LogType type = ProjectName.Systems.LogType.Normal)
        {
            CombatLog.AddEntry(message, type);
        }

        // ─────────────────────────── 입력 (L/ESC) ───────────────────────────

        private void HandleInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
            {
                _isVisible = !_isVisible;
                style.display = _isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                Debug.Log($"[CombatLogUTK] L키 — 로그 {(_isVisible ? "열림" : "닫힘")}");
                _lastRenderCount = -1;
                Refresh();
            }
            else if (_isVisible && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                _isVisible = false;
                style.display = DisplayStyle.None;
                Debug.Log("[CombatLogUTK] ESC — 로그 닫힘");
            }
        }

        // ─────────────────────────── 폴링 갱신 ───────────────────────────

        private void Refresh()
        {
            if (!_isVisible) return;
            var entries = CombatLog.GetRecentEntries(MaxVisible);
            if (entries == null) return;

            // [P5] 풋터 카운트 갱신
            if (_footerCount != null)
                _footerCount.text = $"{entries.Count} / {MaxVisible} 세션 로드됨";

            if (entries.Count == _lastRenderCount) return;

            _lastRenderCount = entries.Count;
            BuildList(entries);
        }

        private void BuildList(List<CombatLogEntry> entries)
        {
            _scroll.Clear();

            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                // [P5 Figma] BattleLogItem 카드 — MetaInfo(타임스탬프+메시지/지역) + 결과 배지
                var card = new VisualElement();
                card.name = "BattleLogItem";
                card.style.flexDirection = FlexDirection.Column;
                card.style.width = 580f;
                card.style.height = 90f;   // [Figma 94:66] BattleLogItem 580×90 고정
                card.style.minHeight = 90f;
                card.style.marginTop = 4f;
                card.style.marginBottom = 4f;   // [Figma 94:65] LogList gap8 (카드 사이 8)
                card.style.paddingLeft = 12f;   // [Figma] pad 12/8
                card.style.paddingRight = 12f;
                card.style.paddingTop = 8f;
                card.style.paddingBottom = 8f;
                card.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
                card.style.borderTopWidth = 1f;
                card.style.borderBottomWidth = 1f;
                card.style.borderLeftWidth = 1f;
                card.style.borderRightWidth = 1f;
                // 최신(상단) 로그 — 파란 테두리 강조
                Color border = (i == 0) ? GitHubDark.Accent : GitHubDark.Stroke;
                card.style.borderTopColor = card.style.borderBottomColor = card.style.borderLeftColor = card.style.borderRightColor = new StyleColor(border);
                card.style.borderTopLeftRadius = 6f;
                card.style.borderTopRightRadius = 6f;
                card.style.borderBottomLeftRadius = 6f;
                card.style.borderBottomRightRadius = 6f;

                // MetaInfo: HH:MM:SS(파랑) + 메시지(굵게) — Figma 시간/지역
                var meta = new VisualElement();
                meta.style.flexDirection = FlexDirection.Row;
                meta.style.alignItems = Align.Center;

                int hours = Mathf.FloorToInt(entry.timestamp / 3600f);
                int minutes = Mathf.FloorToInt((entry.timestamp % 3600f) / 60f);
                int seconds = Mathf.FloorToInt(entry.timestamp % 60f);
                var ts = new Label($"{hours:D2}:{minutes:D2}:{seconds:D2}");
                ts.style.fontSize = 12f;
                ts.style.color = new StyleColor(GitHubDark.Accent);
                ts.style.width = 76f;
                meta.Add(ts);

                var region = new Label(entry.message);
                region.style.flexGrow = 1f;
                region.style.fontSize = 14f;
                region.style.unityFontStyleAndWeight = FontStyle.Bold;
                region.style.color = new StyleColor(GitHubDark.TextMain);
                region.style.whiteSpace = WhiteSpace.Normal;
                meta.Add(region);
                card.Add(meta);

                // 결과 배지 (타입별)
                var resultRow = new VisualElement();
                resultRow.style.flexDirection = FlexDirection.Row;
                resultRow.style.alignItems = Align.Center;
                resultRow.style.marginTop = 6f;   // [Figma 94:66] card gap6 (메타↔결과)

                var fLabel = new Label("교전 결과");
                fLabel.style.fontSize = 11f;
                fLabel.style.color = new StyleColor(GitHubDark.TextSub);
                fLabel.style.width = 64f;
                resultRow.Add(fLabel);

                var badge = new Label(TypeLabel(entry.type));
                badge.style.fontSize = 11f;
                badge.style.unityFontStyleAndWeight = FontStyle.Bold;
                Color bc = ColorForType(entry.type);
                badge.style.backgroundColor = new StyleColor(bc);
                badge.style.color = new StyleColor(GitHubDark.TextSub);
                badge.style.paddingLeft = 6f;
                badge.style.paddingRight = 6f;
                badge.style.borderTopLeftRadius = 4f;
                badge.style.borderTopRightRadius = 4f;
                badge.style.borderBottomLeftRadius = 4f;
                badge.style.borderBottomRightRadius = 4f;
                resultRow.Add(badge);

                var spacer = new VisualElement();
                spacer.style.flexGrow = 1f;
                resultRow.Add(spacer);

                var swatch = new Label("●");
                swatch.style.fontSize = 10f;
                swatch.style.color = new StyleColor(bc);
                swatch.style.width = 20f;
                resultRow.Add(swatch);
                card.Add(resultRow);

                _scroll.Add(card);
            }
            _scroll.scrollOffset = new Vector2(0f, 0f);   // [P5] 최신(상단)부터 — Figma는 최신 위
        }

        private static string TypeLabel(ProjectName.Systems.LogType type)
        {
            switch (type)
            {
                case ProjectName.Systems.LogType.Damage:  return "데미지";
                case ProjectName.Systems.LogType.Heal:    return "회복";
                case ProjectName.Systems.LogType.Kill:    return "처치";
                case ProjectName.Systems.LogType.Warning: return "경고";
                default:                                  return "정보";
            }
        }

        private static Color ColorForType(ProjectName.Systems.LogType type)
        {
            switch (type)
            {
                case ProjectName.Systems.LogType.Damage:  return GitHubDark.Danger;    // danger 레드 #F85149
                case ProjectName.Systems.LogType.Heal:    return GitHubDark.Success;   // success 그린 #3FB950
                case ProjectName.Systems.LogType.Kill:    return GitHubDark.Gold;      // 골드 #E3B341
                case ProjectName.Systems.LogType.Warning: return GitHubDark.Warn;      // attention #D29922
                default:              return GitHubDark.TextSub;                       // 노멀 — 보조 텍스트 #8B949E
            }
        }

        // ─────────────────────────── Updater ───────────────────────────

        /// <summary>MonoBehaviour 폴링 — UIRoot 부착 + 300ms 갱신 + 입력 처리.</summary>
        public class Updater : MonoBehaviour
        {
            private float _tick;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && _instance != null && _instance.parent == null)
                    root.Add(_instance);

                if (_instance == null) return;

                _instance.HandleInput();

                _tick -= Time.unscaledDeltaTime;
                if (_tick <= 0f)
                {
                    _tick = PollInterval;
                    if (_instance.parent != null)
                        _instance.Refresh();
                }
            }
        }
    }
}