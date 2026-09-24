using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // CombatLog, CombatLogEntry, ProjectName.Systems.LogType

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U7 Round C — 전투 로그 창 (L 토글 / ESC 닫기, 스크롤 목록).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    /// 원본: Assets/Scripts/UI/CombatLogUI.cs, 데이터: Systems/CombatLog.cs — 절대 수정 금지.
    ///
    /// [기능]
    ///   ① 원본 CombatLog.GetRecentEntries(100) 실측 — 별도 큐 불필요 (원본이 최대100 유지).
    ///   ② L키 토글 / ESC 닫기 (원본 입력 규약 동일). UTKWindowBase에 등록하지 않는 상시 HUD — 순수 VisualElement.
    ///   ③ 로그 타입(Normal/Damage/Heal/Kill/Warning)별 색상 라벨 + [MM:SS] 타임스탬프.
    ///   ④ "전체 지우기" 버튼 → CombatLog.Clear() (원본 데이터 동일 경로).
    ///   ⑤ 편의 정적 AddLog → 원본 CombatLog.AddEntry 위임 (피드 API 직접 접근).
    ///   ⑥ MonoBehaviour Updater 300ms 폴링 — 새 로그 반영.
    /// </summary>
    public class CombatLogUTK : VisualElement
    {
        private static CombatLogUTK _instance;
        public static CombatLogUTK Instance => _instance;

        private const float PollInterval = 0.3f;           // 300ms 폴링
        private const int MaxVisible = 100;                // 원본 MAX_VISIBLE_ENTRIES
        private const float EntryHeight = 22f;

        private readonly ScrollView _scroll;
        private readonly Button _clearBtn;
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
            style.right = 20f;
            style.top = 60f;
            style.width = 500f;
            style.height = 450f;
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

            // 제목
            var title = new Label("⚔️ 전투 기록");
            title.style.fontSize = 15f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 기본 텍스트
            title.style.paddingTop = 5f;
            title.style.paddingBottom = 4f;
            Add(title);

            // 스크롤 로그
            _scroll = new ScrollView(ScrollViewMode.Vertical);
            _scroll.name = "LogList";
            _scroll.style.flexGrow = 1f;
            Add(_scroll);

            var _tmp = new Label();   // 플렉스/높이 안정화용 스페이서
            _tmp.style.height = 0f;
            Add(_tmp);

            // 전체 지우기
            _clearBtn = UTKButton.Create("전체 지우기", () =>
            {
                CombatLog.Clear();
                _lastRenderCount = -1;
                Refresh();
                Debug.Log("[CombatLogUTK] 전체 로그 지움 (원본 CombatLog.Clear)");
            }, UTKButton.Variant.Danger);
            _clearBtn.style.marginTop = 6f;
            StyleButton(_clearBtn, UTKButton.Variant.Danger);   // [GitHub-dark] danger 버튼 인라인 리스타일
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
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.height = EntryHeight;
                row.style.alignItems = Align.Center;
                row.name = "LogRow_";

                // [F5-3] 타입 컬러 마커 — GitHub-dark 형판의 row 아이콘 슬롯(빈 아이콘 대신 컬러 점)
                var dot = new Label();
                dot.style.width = 8f;
                dot.style.height = 8f;
                dot.style.marginLeft = 2f;
                dot.style.marginRight = 6f;
                dot.style.flexShrink = 0f;
                dot.style.borderTopLeftRadius = 4f;
                dot.style.borderTopRightRadius = 4f;
                dot.style.borderBottomLeftRadius = 4f;
                dot.style.borderBottomRightRadius = 4f;
                dot.style.backgroundColor = new StyleColor(ColorForType(entry.type));
                row.Add(dot);

                // [MM:SS] 타임스탬프
                int minutes = Mathf.FloorToInt(entry.timestamp / 60f);
                int seconds = Mathf.FloorToInt(entry.timestamp % 60f);
                var ts = new Label($"[{minutes:D2}:{seconds:D2}]");
                ts.style.width = 62f;
                ts.style.fontSize = 11f;
                ts.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 타임스탬프 — 보조 텍스트/모노 톤
                row.Add(ts);

                // 메시지 (타입별 색상)
                var msg = new Label(entry.message);
                msg.style.flexGrow = 1f;
                msg.style.fontSize = 13f;
                msg.style.color = new StyleColor(ColorForType(entry.type));
                row.Add(msg);

                _scroll.Add(row);
            }
            _scroll.scrollOffset = new Vector2(0f, float.MaxValue);   // 최신(하단)으로
        }

        private static Color ColorForType(ProjectName.Systems.LogType type)
        {
            switch (type)
            {
                case ProjectName.Systems.LogType.Damage:  return GitHubDark.Danger;    // [GitHub-dark] danger 레드 #F85149
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