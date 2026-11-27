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

        // ── [144차 P0 V3] 옵셔널 어휘 상태 — 신규 API 호출 시에만 요소 생성/표시(기존 창 렌더 무변화) ──
        private bool _isEmpty;                      // 빈셀 프레임 모드(프레임만 렌더)
        private bool _emptyIconRequested;           // square-dashed 플레이스홀더 옵션
        private Color _emptyIconColor = new Color(0x8B / 255f, 0x94 / 255f, 0x9E / 255f, 1f);
        private float _emptyIconSize = 38.4f;       // loot 70:48 진실치 38.4
        private bool _tierStripVisible;             // 상단 h3.6 스트립 옵션
        private VisualElement _tierStrip;           // 지연 생성
        private VisualElement _emptyIconHost;       // 지연 생성(중앙 정렬 호스트)
        private VisualElement _emptyIcon;

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

        private static bool _iconRatioDiagLogged;

        /// <summary>아이콘 텍스처 설정.</summary>
        public void SetIcon(Texture2D tex)
        {
            // [144차 P0 V3] 실아이콘 배치 = 빈셀 프레임 모드 자동 해제(프레임만 → 실아이콘 전환)
            if (_isEmpty)
                SetEmpty(false);

            // [U8 수리] 소유 복사본 사용 — 외부 캐시 파괴 시 노란 경고 아이콘 방지
            _iconHost.style.backgroundImage = UTKTextureSafe.ToBackground(tex);

            // [P22-7 수리] 배경 스케일 모드 명시 — UITK 기본(scale-and-crop)은 슬롯(정사각)과
            //   아이콘 비율이 다르면 남는 축을 잘라버려 "아이콘이 절반으로 잘려서 보였다".
            //   scale-to-fit(contain)으로 통일 — 잘림 원천 제거, 여백은 슬롯 음각 배경이 채움.
            _iconHost.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;

            // [P22-7 진단] 비정형 아이콘 비율 1회 출력 — 베이크 단계 크롭 여부 분리용
            if (!_iconRatioDiagLogged && tex != null && (tex.width != tex.height))
            {
                _iconRatioDiagLogged = true;
                Debug.Log($"[UTKSlot][P22-7] 비정형 아이콘 감지 — {tex.width}x{tex.height} (scale-to-fit 적용, 잘림 없음)");
            }
        }

        /// <summary>카운트 표시. value &lt;= 0이면 숨김.</summary>
        public void SetCount(int count)
        {
            _countLabel.text = count > 0 ? count.ToString() : "";
            _countLabel.style.display = count > 0 ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>[144차 P0 V3] 카운트 표시(문자열) — Figma 진실치 13.2px Geist Mono 600(UITK Bold 매핑) #8B949E.
        /// 기존 SetCount(int)는 무수정(기존 창 렌더 보존 — cooking/loot/shop 등) — 신규 어휘는 본 오버로드 사용(크래프트 63:198 등).
        /// 폰트 패밀리는 UIFont 폴백 허용(단일 폰트 정책 — LootWindowUTK GetGeistMonoFont 선례).</summary>
        public void SetCount(string text)
        {
            _countLabel.text = text ?? "";
            bool show = !string.IsNullOrEmpty(_countLabel.text);
            _countLabel.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            if (show)
            {
                _countLabel.style.fontSize = 13.2f;
                _countLabel.style.unityFont = GetGeistMonoFont();
                _countLabel.style.unityFontStyleAndWeight = FontStyle.Bold;   // Figma 600 → UITK Bold(StyleFigmaText 규약)
                _countLabel.style.color = new StyleColor(UTKTheme.TextSub);   // #8B949E
            }
        }

        /// <summary>[144차 P0 V3] 빈셀 프레임 모드 — 프레임(셀 배경/스트로크/라운드)만 렌더.
        /// 아이콘/카운트/TierStrip을 숨긴다(프레임은 .utk-slot USS 및 창 측 스타일 유지).
        /// SetIcon 호출 시 자동 해제. SetEmptyIcon(true) 조합 시 square-dashed 플레이스홀더 표시(전리품/음식점),
        /// 상점처럼 프레임만 원하는 곳은 SetEmptyIcon(false) 유지(shop.md — ItemSlotEmpty 자식 0).</summary>
        public void SetEmpty(bool empty)
        {
            _isEmpty = empty;
            if (empty)
            {
                _iconHost.style.display = DisplayStyle.None;
                _countLabel.style.display = DisplayStyle.None;
            }
            else
            {
                _iconHost.style.display = DisplayStyle.Flex;
                // 카운트는 마지막 SetCount 상태로 복원 — 빈 텍스트면 숨김 유지
                _countLabel.style.display = string.IsNullOrEmpty(_countLabel.text)
                    ? DisplayStyle.None
                    : DisplayStyle.Flex;
            }
            RefreshOptionals();
        }

        /// <summary>[144차 P0 V3] 빈셀 square-dashed 플레이스홀더 옵션(전리품/음식점).
        /// 진실치: loot 70:48 38.4×38.4 벡터 스트로크 #8B949E 2.4 / restaurant 25×25 #586473.
        /// UI Toolkit dashed 보더 미지원 → 사각 아웃라인 근사(crafting ◇ 글리프 치환 P2 근사 관행과 동급).</summary>
        public void SetEmptyIcon(bool visible, Color? color = null, float size = 38.4f)
        {
            _emptyIconRequested = visible;
            if (color.HasValue) _emptyIconColor = color.Value;
            _emptyIconSize = size;
            RefreshOptionals();
        }

        /// <summary>[144차 P0 V3] TierStrip 옵션 — 슬롯 상단 h3.6 등급색 가로 스트립.
        /// 진실치: 15:64/70:56 (0,0) 셀폭×3.6(감사: "슬롯 상단 h3.6"). visible=false → 숨김(요소 유지).
        /// 빈셀 프레임 모드에서는 Figma 진실(자식 0)에 따라 스트립도 숨김.</summary>
        public void SetTierStrip(bool visible, Color tierColor)
        {
            _tierStripVisible = visible;
            var strip = EnsureTierStrip();
            strip.style.backgroundColor = new StyleColor(tierColor);
            RefreshOptionals();
        }

        private VisualElement EnsureTierStrip()
        {
            if (_tierStrip != null) return _tierStrip;
            _tierStrip = new VisualElement { name = "TierStrip", pickingMode = PickingMode.Ignore };
            _tierStrip.style.position = Position.Absolute;
            _tierStrip.style.left = 0f;
            _tierStrip.style.right = 0f;
            _tierStrip.style.top = 0f;
            _tierStrip.style.height = 3.6f;
            Add(_tierStrip);
            return _tierStrip;
        }

        private void RefreshOptionals()
        {
            // TierStrip — 빈셀 모드에서는 프레임만(ItemSlotEmpty 자식 0 진실)
            if (_tierStrip != null)
                _tierStrip.style.display = (_tierStripVisible && !_isEmpty)
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;

            // square-dashed 플레이스홀더 — 빈셀 + 옵션 ON일 때만
            bool showPlaceholder = _isEmpty && _emptyIconRequested;
            if (!showPlaceholder)
            {
                if (_emptyIconHost != null) _emptyIconHost.style.display = DisplayStyle.None;
                return;
            }
            if (_emptyIconHost == null)
            {
                _emptyIconHost = new VisualElement { name = "EmptyIconHost", pickingMode = PickingMode.Ignore };
                _emptyIconHost.style.position = Position.Absolute;
                _emptyIconHost.style.left = 0f;
                _emptyIconHost.style.right = 0f;
                _emptyIconHost.style.top = 0f;
                _emptyIconHost.style.bottom = 0f;
                _emptyIconHost.style.alignItems = Align.Center;
                _emptyIconHost.style.justifyContent = Justify.Center;
                _emptyIcon = new VisualElement { name = "EmptyIcon" };
                var side = new StyleColor(_emptyIconColor);
                _emptyIcon.style.borderTopColor = side;
                _emptyIcon.style.borderBottomColor = side;
                _emptyIcon.style.borderLeftColor = side;
                _emptyIcon.style.borderRightColor = side;
                _emptyIcon.style.borderTopWidth = 2.4f;   // loot 70:49 벡터 스트로크 진실치
                _emptyIcon.style.borderBottomWidth = 2.4f;
                _emptyIcon.style.borderLeftWidth = 2.4f;
                _emptyIcon.style.borderRightWidth = 2.4f;
                _emptyIconHost.Add(_emptyIcon);
                Add(_emptyIconHost);
            }
            _emptyIcon.style.width = _emptyIconSize;
            _emptyIcon.style.height = _emptyIconSize;
            _emptyIcon.style.borderTopColor = new StyleColor(_emptyIconColor);
            _emptyIcon.style.borderBottomColor = new StyleColor(_emptyIconColor);
            _emptyIcon.style.borderLeftColor = new StyleColor(_emptyIconColor);
            _emptyIcon.style.borderRightColor = new StyleColor(_emptyIconColor);
            _emptyIconHost.style.display = DisplayStyle.Flex;
        }

        // [144차 P0 V3] Geist Mono 로드(Figma 패밀리 계약) — 에셋 미보유 → UIFont 폴백
        // (ItemDescriptionWindowUTK/WorldMapWindowUTK/LootWindowUTK 선례). Resources/Fonts/GeistMono 추가 시 자동 적용.
        private static Font _geistMono;

        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
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
    // ⑦ UTKCloseButton — [144차 P0 V5] Figma 288:7 진실치
    //    (docs/figma_truth_144/map_explorelist__95_188.json — python 실측).
    //    32×32 · r16(원) · fill #161B22@0.8 · stroke #30363D@0.502 1.2 ·
    //    x-circle 벡터 13.3px #8B949E(스트로크 2.0) — 글리프 "✕" 근사.
    //    DROP_SHADOW(blur 9.6 #000000@0.251)은 UTK 런타임 그림자 경로 부재로 미표현(감사 §보류 준용).
    // ─────────────────────────────────────────────────────────────
    public static class UTKCloseButton
    {
        /// <summary>Figma 진실치 닫기 버튼(기본 32×32 원형) 생성. 우상단 배치(right/top)는 호출부 몫.</summary>
        public static Button CreateCloseButton(Action onClick = null, float size = 32f)
        {
            var btn = new Button(onClick) { text = "✕", name = "CloseButton" };
            btn.style.width = size;
            btn.style.height = size;
            btn.style.fontSize = 18f;   // x-circle 13.3px 시각 근사(AttackFormationUTK 18px 선례)
            btn.style.color = new StyleColor(UTKTheme.TextSub);   // 벡터 스트로크 #8B949E 진실치
            btn.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.8f));
            UTKTheme.ApplyElementBorder(btn, UTKTheme.BorderChrome, UTKTheme.BorderChromeWidth);
            UTKTheme.ApplyRadius(btn, size * 0.5f);   // r16 = 32×32 원(288:7 r=16.0)
            btn.style.unityTextAlign = TextAnchor.MiddleCenter;
            btn.style.flexShrink = 0f;
            return btn;
        }
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

        // [92차 결함1] 토스트 스팸 방어 — 완료 루프 폭발(매 프레임 Show) 시 큐가 수천 개까지 누적되어
        // ToastHost에 Label이 적층 → UIRoot 레이아웃 붕괴(포렌식: 토스트 큐 3,449개 — 건설창 "안뜸" 원인).
        // 스팸 재발 시에도 UI가 생존하도록 Show 선행 검사로 라벨 생성 자체를 차단한다:
        //  ① 동일 문구 0.5s 내 중복 억제  ② 대기 큐 상한(8) 초과 폐기.
        /// <summary>동일 문구 중복 억제 시간 (s) — 이 시간 내 재호출은 폐기. [92차 결함1]</summary>
        private const float DuplicateSuppressSeconds = 0.5f;
        /// <summary>대기 큐 상한 — 초과 신규 토스트는 폐기. [92차 결함1]</summary>
        private const int QueueCap = 8;

        private static string _lastAcceptedText; // 마지막 수용된 문구 — 억제 판정 기준
        private static float _lastAcceptedAtRealtime = float.NegativeInfinity; // Time.realtimeSinceStartup 기준

        /// <summary>토스트 표시. 기본 2.5초 후 페이드아웃.</summary>
        public static void Show(string text, float duration = 2.5f)
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;

            // [92차 결함1] ① 동일 문구 0.5s 내 중복 억제 — 라벨/큐 추가 없이 폐기(적층 원천 차단)
            float now = Time.realtimeSinceStartup;
            if ((text ?? "") == _lastAcceptedText && (now - _lastAcceptedAtRealtime) < DuplicateSuppressSeconds)
                return;

            // [92차 결함1] ② 큐 상한 — 대기 8 초과 시 신규 폐기(스팸 재발 시에도 UI 생존)
            if (_queue.Count >= QueueCap)
                return;

            _lastAcceptedText = text ?? "";
            _lastAcceptedAtRealtime = now;

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
            _messageLabel.style.fontSize = new Length(16.8f, LengthUnit.Pixel);
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
    /// <remarks>
    /// 2026-09-28 전면 통일: 구식 브론즈/우드(BgPanel 0x1C1C1C · BorderBronze 0x8C6B3F · TextPrimary 0xF5EFE0)
    /// → GitHub-dark(Fluent) 팔레트로 리매핑. 기존 자체 제작 창들이 UTKColor를 참조하므로
    /// 이 승격 한 번으로 전 창 분위기가 Figma/GitHub-dark 기준과 통일된다. 값은 UTKTheme와 동일.
    /// </remarks>
    public static class UTKColor
    {
        // ===== GitHub-dark / Fluent dark (F-UI 팔레트 — UTKTheme와 동일 값) =====
        public static readonly Color BgPanel       = Hex(0x16, 0x1B, 0x22, 0xFF); // Panel #161B22
        public static readonly Color BgPanelDark   = Hex(0x0B, 0x0E, 0x14, 0xF2); // BgBase #0B0E14
        public static readonly Color BorderBronze  = Hex(0x2E, 0x34, 0x3D);        // Stroke #2E343D
        public static readonly Color BorderGold    = Hex(0xE3, 0xB3, 0x41);        // Gold #E3B341
        public static readonly Color IronLine      = Hex(0x2E, 0x34, 0x3D);        // Stroke #2E343D
        public static readonly Color TextPrimary   = Hex(0xF0, 0xF6, 0xFC);        // TextMain #F0F6FC
        public static readonly Color TextSecondary = Hex(0x8B, 0x94, 0x9E);        // TextSub #8B949E
        public static readonly Color AccentMagic   = Hex(0x58, 0xA6, 0xFF);        // Accent #58A6FF
        public static readonly Color AccentRare    = Hex(0xE3, 0xB3, 0x41);        // Gold #E3B341
        public static readonly Color HealthRed     = Hex(0xF8, 0x51, 0x49);        // Danger #F85149
        public static readonly Color GuildGreen    = Hex(0x3F, 0xB9, 0x50);        // Success #3FB950
        public static readonly Color HoverGold     = Hex(0xD2, 0x99, 0x22);        // Warn #D29922

        // 등급색 (EquipmentRarityData 통일 — 시맨틱 등급, 유지)
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