using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.UI;   // UIFont

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U0 — 공통 컨트롤 라이브러리.
    /// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
    ///
    /// 포함:
    ///   ① UTKButton       — 팩토리 버튼 (primary/secondary/danger)
    ///   ② UTKSlot         — 슬롯: 아이콘 + 카운트 + 등급 테두리 + 호버 글로우
    ///   ③ UTKRarity       — 등급명 → USS 클래스 매핑 유틸
    ///   ④ UTKTooltip      — 커서 추적 툴팁
    ///   ⑤ UTKToastService — 하단 중앙 토스트 큐 + 페이드아웃
    ///   ⑥ UTKModal        — 확인/취소 2버튼 모달
    /// </summary>

    // ─────────────────────────────────────────────────────────────
    // ① UTKButton — 정적 팩토리
    // ─────────────────────────────────────────────────────────────
    public static class UTKButton
    {
        public enum Variant { Primary, Secondary, Danger }

        /// <summary>버튼 생성. variant에 따라 .utk-btn--primary/--secondary/--danger.</summary>
        public static Button Create(string label, Action onClick, Variant variant = Variant.Secondary)
        {
            var btn = new Button(onClick) { text = label ?? "" };
            btn.AddToClassList("utk-btn");
            switch (variant)
            {
                case Variant.Primary:   btn.AddToClassList("utk-btn--primary");   break;
                case Variant.Danger:    btn.AddToClassList("utk-btn--danger");    break;
                default:                btn.AddToClassList("utk-btn--secondary"); break;
            }
            return btn;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ③ UTKRarity — 등급명 → USS 클래스 매핑 (EquipmentRarityData 표기 통일)
    // ─────────────────────────────────────────────────────────────
    public static class UTKRarity
    {
        // 등급명(영문 키) → USS 클래스 접미사. ItemRarity 순서에 대응.
        private static readonly Dictionary<string, string> Map = new Dictionary<string, string>
        {
            { "common",    "common"    },
            { "uncommon",  "uncommon"  },
            { "rare",      "rare"      },
            { "epic",      "epic"      },
            { "legendary", "legendary" },
            { "unique",    "unique"    },
        };

        /// <summary>등급명(대소문자 무시) → "utk-rank--접미사". 미지정 시 "utk-rank--common".</summary>
        public static string ClassFor(string rank)
        {
            if (string.IsNullOrEmpty(rank)) return "utk-rank--common";
            string key = rank.Trim().ToLowerInvariant();
            return Map.TryGetValue(key, out string suffix)
                ? "utk-rank--" + suffix
                : "utk-rank--common";
        }

        /// <summary>enum:int 등급 → "utk-rank--..." (Common=0..Unique=5).</summary>
        public static string ClassForIndex(int rarityIndex)
        {
            switch (rarityIndex)
            {
                case 0: return "utk-rank--common";
                case 1: return "utk-rank--uncommon";
                case 2: return "utk-rank--rare";
                case 3: return "utk-rank--epic";
                case 4: return "utk-rank--legendary";
                case 5: return "utk-rank--unique";
                default: return "utk-rank--common";
            }
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ② UTKSlot — 아이콘 + 카운트 + 등급 테두리 + 호버 글로우
    // ─────────────────────────────────────────────────────────────
    public class UTKSlot : VisualElement
    {
        private readonly VisualElement _iconHost;
        private readonly Label _countLabel;
        private string _currentRankClass;

        public UTKSlot()
        {
            AddToClassList("utk-slot");

            // 아이콘 표시 영역 (background-image 방식)
            _iconHost = new VisualElement();
            _iconHost.name = "Icon";
            _iconHost.style.flexGrow = 1f;
            _iconHost.style.width = new Length(100f, LengthUnit.Percent);
            _iconHost.style.height = new Length(100f, LengthUnit.Percent);
            Add(_iconHost);

            // 카운트
            _countLabel = new Label("");
            _countLabel.AddToClassList("utk-slot__count");
            _countLabel.name = "Count";
            Add(_countLabel);

            // 호버 글로우
            RegisterCallback<PointerEnterEvent>(_ => AddToClassList("utk-slot--hover"));
            RegisterCallback<PointerLeaveEvent>(_ => RemoveFromClassList("utk-slot--hover"));
        }

        /// <summary>아이콘 텍스처 설정.</summary>
        public void SetIcon(Texture2D tex)
        {
            _iconHost.style.backgroundImage = tex != null
                ? new StyleBackground(Background.FromTexture2D(tex))
                : StyleKeyword.Null;
        }

        /// <summary>카운트 표시. value &lt;= 0이면 숨김.</summary>
        public void SetCount(int count)
        {
            _countLabel.text = count > 0 ? count.ToString() : "";
            _countLabel.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>등급 테두리 클래스 설정. 이전 등급 클래스 제거 후 새로 부여.</summary>
        public void SetRank(string rank)
        {
            if (!string.IsNullOrEmpty(_currentRankClass))
                RemoveFromClassList(_currentRankClass);
            _currentRankClass = UTKRarity.ClassFor(rank);
            AddToClassList(_currentRankClass);
        }

        /// <summary>아이콘 호스트 (외부에서 직접 조작 필요한 경우).</summary>
        public VisualElement IconHost => _iconHost;
    }

    // ─────────────────────────────────────────────────────────────
    // ④ UTKTooltip — 커서 추적 툴팁
    // ─────────────────────────────────────────────────────────────
    public static class UTKTooltip
    {
        private static Label _label;
        private static VisualElement _parent;

        private static void Ensure()
        {
            if (_label != null) return;
            _parent = UIToolkitBootstrap.UIRoot;
            if (_parent == null) return;

            _label = new Label();
            _label.AddToClassList("utk-tooltip");
            _label.pickingMode = PickingMode.Ignore;
            _label.style.display = DisplayStyle.None;
            _parent.Add(_label);
        }

        /// <summary>텍스트 툴팁을 화면 좌표 (panel 공간) 위치에 표시.</summary>
        public static void Show(string text, Vector2 panelPos)
        {
            Ensure();
            if (_label == null) return;
            _label.text = text ?? "";
            _label.style.display = DisplayStyle.Flex;
            _label.BringToFront();

            // 패널(퍼널) 상단 고정 — 드래그로 화면 밖에 안 나가게 클램프.
            float x = Mathf.Clamp(panelPos.x + 12f, 4f, 10000f);
            float y = Mathf.Clamp(panelPos.y + 12f, 4f, 10000f);
            _label.style.left = x;
            _label.style.top = y;
        }

        /// <summary>툴팁 숨김.</summary>
        public static void Hide()
        {
            if (_label != null)
                _label.style.display = DisplayStyle.None;
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ⑤ UTKToastService — 하단 중앙 토스트 큐 + 페이드아웃
    // ─────────────────────────────────────────────────────────────
    public static class UTKToastService
    {
        private static readonly Queue<(Label label, float duration)> _queue = new Queue<(Label, float)>();
        private static VisualElement _host;
        private static bool _processing;

        /// <summary>토스트 표시. 기본 2.5초 후 페이드아웃.</summary>
        public static void Show(string text, float duration = 2.5f)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;

            EnsureHost(root);
            var label = new Label(text ?? "") { name = "Toast" };
            label.AddToClassList("utk-toast");
            label.style.display = DisplayStyle.None;
            _host.Add(label);

            _queue.Enqueue((label, Mathf.Max(0.1f, duration)));
            if (!_processing)
                ProcessNext();
        }

        private static void EnsureHost(VisualElement root)
        {
            if (_host != null && _host.parent == root) return;
            RemoveExistingHost(root);
            _host = new VisualElement();
            _host.name = "ToastHost";
            _host.style.position = Position.Absolute;
            _host.style.left = 0;
            _host.style.right = 0;
            _host.style.bottom = 24f;
            _host.style.flexDirection = FlexDirection.ColumnReverse;
            _host.style.alignItems = Align.Center;
            _host.pickingMode = PickingMode.Ignore;
            root.Add(_host);
        }

        private static void RemoveExistingHost(VisualElement root)
        {
            foreach (var child in root.Children())
            {
                if (child.name == "ToastHost")
                    child.RemoveFromHierarchy();
            }
        }

        private static void ProcessNext()
        {
            if (_queue.Count == 0)
            {
                _processing = false;
                return;
            }
            _processing = true;
            var (label, duration) = _queue.Dequeue();
            label.style.display = DisplayStyle.Flex;

            var sched = label.schedule;
            sched.Execute(() => StartFadeOut(label)).StartingIn((long)(duration * 1000f));
            // 다음 토스트는 여유를 두고 처리
            sched.Execute(() => ProcessNext()).StartingIn((long)((duration + 0.6f) * 1000f));

            // 라벨 호버/포인터 무시 → 이벤트 흡수 방지
            label.pickingMode = PickingMode.Ignore;
        }

        private static void StartFadeOut(Label label)
        {
            if (label == null || label.parent == null) return;
            label.AddToClassList("utk-toast--fadeout");
            label.schedule.Execute(() =>
            {
                if (label.parent != null)
                    label.RemoveFromHierarchy();
            }).StartingIn(450L);
        }
    }

    // ─────────────────────────────────────────────────────────────
    // ⑥ UTKModal — 확인/취소 2버튼 모달
    // ─────────────────────────────────────────────────────────────
    public class UTKModal : VisualElement
    {
        private readonly Label _messageLabel;
        private readonly VisualElement _overlay;

        /// <summary>확인/취소 콜백 (확인 = callback).</summary>
        public Action OnConfirm;
        public Action OnCancel;

        public UTKModal(string title, string message,
                        string confirmText = "확인", string cancelText = "취소")
        {
            // 오버레이 (배경 딤드)
            _overlay = new VisualElement();
            _overlay.AddToClassList("utk-modal__overlay");
            _overlay.style.left = 0; _overlay.style.right = 0;
            _overlay.style.top = 0; _overlay.style.bottom = 0;
            _overlay.pickingMode = PickingMode.Position;
            Add(_overlay);

            // 모달 박스
            var box = new VisualElement();
            box.AddToClassList("utk-modal");
            Add(box);

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new Label(title);
                titleLabel.AddToClassList("utk-title-label");
                titleLabel.style.fontSize = new Length(24f, LengthUnit.Pixel);
                box.Add(titleLabel);
            }

            _messageLabel = new Label(message ?? "");
            _messageLabel.style.fontSize = new Length(17f, LengthUnit.Pixel);
            _messageLabel.style.color = new StyleColor(UTKColor.TextPrimary);
            box.Add(_messageLabel);

            // 버튼 행
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.justifyContent = Justify.FlexEnd;
            btnRow.style.marginTop = 12f;
            box.Add(btnRow);

            var confirmBtn = UTKButton.Create(confirmText ?? "확인", () => { OnConfirm?.Invoke(); Close(); }, UTKButton.Variant.Primary);
            var cancelBtn = UTKButton.Create(cancelText ?? "취소", () => { OnCancel?.Invoke(); Close(); }, UTKButton.Variant.Secondary);
            btnRow.Add(confirmBtn);
            btnRow.Add(cancelBtn);

            UTKWindowBase.ApplyUIToolkitFont(this);
        }

        /// <summary>전체 화면 오버레이+창을 루트에 추가하고 표시.</summary>
        public void ShowToRoot()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            style.display = DisplayStyle.Flex;
            style.position = Position.Absolute;
            style.left = 0; style.right = 0; style.top = 0; style.bottom = 0;
            root.Add(this);
            BringToFront();
        }

        public void Close()
        {
            RemoveFromHierarchy();
        }
    }

    /// <summary>UTK 공통 12색 팔레트 (Theme.uss와 동일 값, 코드 참조용).</summary>
    public static class UTKColor
    {
        public static readonly Color BgPanel      = Hex(0x1C, 0x1C, 0x1C, 0xE0);
        public static readonly Color BgPanelDark  = Hex(0x14, 0x14, 0x14, 0xE8);
        public static readonly Color BorderBronze = Hex(0x8C, 0x6B, 0x3F);
        public static readonly Color BorderGold   = Hex(0xC9, 0xA2, 0x27);
        public static readonly Color IronLine     = Hex(0x3A, 0x3A, 0x3A);
        public static readonly Color TextPrimary  = Hex(0xF5, 0xEF, 0xE0);
        public static readonly Color TextSecondary = Hex(0xB9, 0xB3, 0xA6);
        public static readonly Color AccentMagic  = Hex(0x4A, 0x7B, 0xD0);
        public static readonly Color AccentRare   = Hex(0xE7, 0xB7, 0x3A);
        public static readonly Color HealthRed    = Hex(0xC8, 0x38, 0x38);
        public static readonly Color GuildGreen   = Hex(0x5E, 0x8C, 0x4A);
        public static readonly Color HoverGold    = Hex(0xD9, 0xB4, 0x5B);

        // 등급색 (EquipmentRarityData 통일)
        public static readonly Color RankCommon    = Hex(0x99, 0x99, 0x99);
        public static readonly Color RankUncommon  = Hex(0x33, 0xCC, 0x33);
        public static readonly Color RankRare      = Hex(0x33, 0x66, 0xFF);
        public static readonly Color RankEpic      = Hex(0x99, 0x33, 0xFF);
        public static readonly Color RankLegendary = Hex(0xFF, 0x33, 0x33);
        public static readonly Color RankUnique    = Hex(0xFF, 0xD9, 0x00);

        private static Color Hex(int r, int g, int b, int a = 0xFF)
            => new Color(r / 255f, g / 255f, b / 255f, a / 255f);
    }
}