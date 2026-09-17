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

        private static MissionResultUTK _instance;
        private static readonly Queue<(float timestamp, string text)> _entries = new Queue<(float, string)>();

        private readonly ScrollView _list;
        private readonly Label _countLabel;

        private MissionResultUTK()
        {
            style.position = Position.Absolute;
            style.right = 12; style.top = 120; style.width = 340; style.bottom = 200;
            style.backgroundColor = new StyleColor(UTKColor.BgPanel);
            style.borderTopWidth = 2; style.borderBottomWidth = 2;
            style.borderLeftWidth = 2; style.borderRightWidth = 2;
            style.borderTopColor = new StyleColor(UTKColor.BorderBronze);
            style.borderBottomColor = new StyleColor(UTKColor.BorderBronze);
            style.borderLeftColor = new StyleColor(UTKColor.BorderBronze);
            style.borderRightColor = new StyleColor(UTKColor.BorderBronze);
            style.display = DisplayStyle.None;
            pickingMode = PickingMode.Position;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 6; header.style.paddingBottom = 6;
            header.style.paddingLeft = 8; header.style.paddingRight = 8;
            header.style.backgroundColor = new StyleColor(UTKColor.BgPanelDark);
            Add(header);

            var title = new Label("📜 임무 결과");
            title.style.flexGrow = 1f;
            title.style.fontSize = 24;
            title.style.color = new StyleColor(UTKColor.TextPrimary);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            _countLabel = new Label("0");
            _countLabel.style.fontSize = 17;
            _countLabel.style.color = new StyleColor(UTKColor.TextSecondary);
            header.Add(_countLabel);

            var closeBtn = UTKButton.Create("✕", Hide, UTKButton.Variant.Danger);
            closeBtn.style.width = 28; closeBtn.style.height = 24;
            closeBtn.style.marginLeft = 8;
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
                row.style.color = new StyleColor(UTKColor.TextPrimary);
                row.style.marginBottom = 4;
                row.style.whiteSpace = WhiteSpace.Normal;
                _list.Add(row);
            }
            _countLabel.text = _entries.Count.ToString();
        }
    }
}
