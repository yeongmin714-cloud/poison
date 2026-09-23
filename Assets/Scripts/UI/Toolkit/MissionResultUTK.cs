using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 — 임무 결과 히스토리 패널 (원본 MissionResultUI.cs 414줄 포팅, additive).
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 원본은 히스토리 엔트리(timestamp+text)를 내부 축적 — public 피드 API가 없어
    /// 본 UTK 버전은 자체 큐 + public static AddResult(string) 제공.
    /// 실제 임무 시스템 피드 배선은 U7 일괄 전환 라운드에서 연결.
    /// [MissionResultUTK] 실측 로그.
    /// </summary>
    public class MissionResultUTK : VisualElement
    {
        private const int MaxEntries = 50;

        // =====================================================================
        //  [Figma GitHub-dark 리스타일] 임무결과 패널 한정 인라인 오버라이드 — 기능 무수정, 시각 전용.
        //  =====================================================================
        private static class GitHubDark
        {
            public static readonly Color Panel    = Hex(0x161B22);   // 창 본체 패널
            public static readonly Color PanelSub = Hex(0x21262D);   // 보조 패널(헤더)
            public static readonly Color Gold     = Hex(0xE3B341);   // 희귀/활성/골드
            public static readonly Color TextMain = Hex(0xF0F6FC);   // 기본 텍스트
            public static readonly Color TextSub  = Hex(0x8B949E);   // 보조 텍스트
            public static readonly Color Stroke   = Hex(0x2E343D);   // 테두리/구분선
            public static readonly Color Danger   = Hex(0xF85149);   // danger 버튼
            public static readonly Color BgBase   = Hex(0x0B0E14);   // 최배경
            public static readonly Color Accent   = Hex(0x58A6FF);   // 강조(액센트)

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        /// <summary>GitHub-dark 버튼 인라인 오버라이드(이 창 한정) — 4면 개별 대입(IStyle 쇼트핸드 없음).</summary>
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

            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = baseBg;
            btn.style.color = textColor;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(baseBg);
            btn.style.borderTopLeftRadius = 6f;
            btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f;
            btn.style.borderBottomRightRadius = 6f;

            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = hoverBg);
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = baseBg);
        }

        private static MissionResultUTK _instance;
        private static readonly Queue<(float timestamp, string text)> _entries = new Queue<(float, string)>();

        private readonly ScrollView _list;
        private readonly Label _countLabel;

        private MissionResultUTK()
        {
            style.position = Position.Absolute;
            style.right = 12; style.top = 120; style.width = 340; style.bottom = 200;
            // [GitHub-dark] 창 본체 — 브론즈 베벨 2px 제거, 다크 패널 + 1px 스트로크 + r8
            style.backgroundColor = new StyleColor(GitHubDark.Panel);
            style.borderTopWidth = 1; style.borderBottomWidth = 1;
            style.borderLeftWidth = 1; style.borderRightWidth = 1;
            style.borderTopColor = new StyleColor(GitHubDark.Stroke);
            style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            style.borderLeftColor = new StyleColor(GitHubDark.Stroke);
            style.borderRightColor = new StyleColor(GitHubDark.Stroke);
            style.borderTopLeftRadius = 8f;
            style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f;
            style.borderBottomRightRadius = 8f;   // 메인 반경 r8
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Position;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 6; header.style.paddingBottom = 6;
            header.style.paddingLeft = 8; header.style.paddingRight = 8;
            // [GitHub-dark] 헤더 = 보조 패널 #21262D + 하단 1px 스트로크 (상단 코너 r8 — 창 클리핑 정합)
            header.style.backgroundColor = new StyleColor(GitHubDark.PanelSub);
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(GitHubDark.Stroke);
            Add(header);

            var title = new Label("📜 임무 결과");
            title.style.flexGrow = 1f;
            title.style.fontSize = 24;
            title.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 기본 텍스트
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _countLabel = new Label("0");
            _countLabel.style.fontSize = 17;
            _countLabel.style.color = new StyleColor(GitHubDark.TextSub);   // [GitHub-dark] 보조 텍스트
            header.Add(_countLabel);

            var closeBtn = UTKButton.Create("✕", Hide, UTKButton.Variant.Danger);
            closeBtn.style.width = 28; closeBtn.style.height = 24;
            closeBtn.style.marginLeft = 8;
            StyleButton(closeBtn, UTKButton.Variant.Danger);   // [GitHub-dark] danger 버튼 인라인 리스타일
            header.Add(closeBtn);

            _list = new ScrollView();
            _list.style.flexGrow = 1f;
            _list.style.paddingTop = 6; _list.style.paddingBottom = 6;
            _list.style.paddingLeft = 8; _list.style.paddingRight = 8;
            Add(_list);

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        /// <summary>UTK 루트 부착 인스턴스 보장(멱등).</summary>
        public static MissionResultUTK Ensure()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return null;
            if (_instance != null && _instance.panel != null) return _instance;
            if (_instance != null) _instance.RemoveFromHierarchy();
            _instance = new MissionResultUTK();
            root.Add(_instance);
            Debug.Log("[MissionResultUTK] 임무 결과 패널 생성");
            return _instance;
        }

        /// <summary>결과 추가(정적 — 어느 시스템에서든 호출 가능). 표시 중이면 즉시 반영.</summary>
        public static void AddResult(string text)
        {
            _entries.Enqueue((Time.time, text));
            while (_entries.Count > MaxEntries)
                _entries.Dequeue();
            Debug.Log("[MissionResultUTK] 결과 추가 — " + text);
            if (_instance != null && _instance.style.display == DisplayStyle.Flex)
                _instance.RebuildList();
        }

        /// <summary>결과 여러 건 일괄 추가.</summary>
        public static void AddResults(IEnumerable<string> texts)
        {
            foreach (var t in texts)
                AddResult(t);
        }

        public static void Show()
        {
            var i = Ensure();
            if (i == null) return;
            i.style.display = DisplayStyle.Flex;
            i.BringToFront();
            i.RebuildList();
        }

        public static void Hide()
        {
            if (_instance == null) return;
            _instance.style.display = DisplayStyle.None;
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.style.display == DisplayStyle.Flex) { Hide(); return; }
            Show();
        }

        private void RebuildList()
        {
            _list.Clear();
            var items = _entries.ToArray();
            for (int idx = items.Length - 1; idx >= 0; idx--) // 최신순
            {
                var e = items[idx];
                var row = new Label("• " + e.text);
                row.style.fontSize = 17;
                row.style.color = new StyleColor(GitHubDark.TextMain);   // [GitHub-dark] 항목 기본 텍스트
                row.style.marginBottom = 4;
                row.style.whiteSpace = WhiteSpace.Normal;
                _list.Add(row);
            }
            _countLabel.text = _entries.Count.ToString();
        }
    }
}
