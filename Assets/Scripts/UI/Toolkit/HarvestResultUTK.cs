// F3 (2026-09-24): 낚시/채집/광질 공용 결과 팝업 — Figma fishing/gathering/mining-result-ui 템플릿 정합.
// 구조: HUDHeader(위치) + ResultPanel(PanelHeader·NotificationTitle·ItemImageSection·ItemSpecs·DescriptionSection·ActionButtons) + SystemTip.
// 데이터는 HarvestResultBridge(static, Systems)에서 폴링 소비 — Systems→UI 역참조 없음.
#pragma warning disable 114
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Core.Data;   // EconomyPricing
using ProjectName.Systems;   // HarvestResultBridge

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit — 낚시/채집/광질 결과 공용 팝업.
    /// Figma `fishing/gathering/mining-result-ui` 공용 템플릿(HUDHeader + ResultPanel + SystemTip)을 GitHub-dark로 재현.
    /// 단일 채널 공용 — HarvestResultBridge.Latest* 를 16ms 폴링해 새 결과(LatestToken 변화) 감지 시 표시.
    /// 생산 시스템이 publish 전에 이미 아이템을 적립한다. 확인은 닫기만 한다. 안전한 획득 건 단위 제거 API가 없어 두 번째 액션도 닫기로 표시한다.
    /// </summary>
    public class HarvestResultUTK : UTKWindowBase
    {
        private static HarvestResultUTK _instance;
        private static Updater _updater;

        private const float WinW = 576f;   // Archived 72:* ResultPanel width
        private const float WinH = 859.8f; // Fishing variant; variant bounds applied per kind
        private const long TickMs = 16L;

        // ===== GitHub-dark 팔레트 (F-UI 표준, 이 창 한정) =====
        private static class Dark
        {
            public static readonly Color Bg       = Hex(0x0B0E14);   // 최배경
            public static readonly Color Panel    = Hex(0x161B22);   // 결과 패널
            public static readonly Color Sub      = Hex(0x21262D);   // 보조 패널(SpecBox/버튼)
            public static readonly Color Inset    = Hex(0x1C2128);   // 아이템 이미지/설명 내부
            public static readonly Color Accent   = Hex(0x58A6FF);   // 액센트(파랑)
            public static readonly Color Gold     = Hex(0xE3B341);   // 골드(희귀/아이템명)
            public static readonly Color TextMain = Hex(0xF0F6FC);
            public static readonly Color TextSub  = Hex(0x8B949E);
            public static readonly Color Stroke   = Hex(0x2E343D);
            public static readonly Color Danger   = Hex(0xF85149);
            public static readonly Color WhiteBg  = Hex(0xFFFFFF);   // 버리기 버튼(화이트)

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        }

        private static Color Hex(uint rgb) =>
            new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        // ===== 노드 =====
        private readonly Label _locLabel;      // HUDHeader 좌측 (현재 위치/행동)
        private readonly Label _staminaLabel;  // HUDHeader 우측 (STA/생활)
        private readonly Label _subtitle;      // PanelHeader "FISHING RESULT"
        private readonly Label _badge;         // "SUCCESS" / "채집 성공"
        private readonly Label _verb;          // "대어 획득 성공!"
        private readonly Label _itemName;      // "고대 심해 아로와나을(를) 낚았습니다!"
        private readonly VisualElement _tierStrip;  // 상단 등급 스트립 4px
        private readonly Label _tierClass;     // "LEGENDARY CLASS"
        private readonly VisualElement _itemImage; // 아이템 아이콘 영역
        private readonly Label _spec1Label; private readonly Label _spec1Val;
        private readonly Label _spec2Label; private readonly Label _spec2Val;
        private readonly Label _spec3Label; private readonly Label _spec3Val;
        private readonly Label _descHeader;    // "물고기 특징"
        private readonly Label _descBody;      // 설명
        private readonly Label _tipLabel;      // SystemTip
        private readonly VisualElement _resultPanel;
        private readonly VisualElement _panelShadow;   // [33차] 플루언트 소프트 섀도
        private readonly VisualElement _header;
        private readonly VisualElement _actions;
        private readonly VisualElement _tip;
        private IVisualElementScheduledItem _tickTask;
        private int _lastToken = -1;
        private string _iconAppliedItemId;   // [30차] 아이콘 지연 베이크 재적용 추적

        private HarvestResultUTK() : base("🎣 채집·광질 결과", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);
            ApplyGitHubDarkStyle();

            // ── HUDHeader ──
            var header = new VisualElement();
            _header = header;
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 4f; header.style.paddingBottom = 4f;
            header.style.paddingLeft = 8f; header.style.paddingRight = 8f;
            header.style.marginBottom = 10f;
            Content.Add(header);

            _locLabel = new Label("—");
            _locLabel.style.fontSize = 13.2f;
            _locLabel.style.color = Dark.TextSub;
            _locLabel.style.flexGrow = 1f;
            header.Add(_locLabel);

            _staminaLabel = new Label("");
            _staminaLabel.style.fontSize = 13.2f;
            _staminaLabel.style.color = Hex(0xFF7300);   // STA 주황
            header.Add(_staminaLabel);

            // ── ResultPanel ── [32차 Fluent] 유리 배경(α0.88) — 뒷 배경이 희미하게 비침
            // [33차] .utk-window-shadow(다른 UTK 창과 동일 소프트 섀도) — 플루언트 크롬 일치
            var panelShadow = new VisualElement { name = "ResultPanelShadow" };
            panelShadow.AddToClassList("utk-window-shadow");
            panelShadow.style.position = Position.Absolute;
            panelShadow.pickingMode = PickingMode.Ignore;
            Content.Add(panelShadow);
            _panelShadow = panelShadow;

            var panel = new VisualElement();
            _resultPanel = panel;
            panel.style.backgroundColor = new StyleColor(new Color32(0x16, 0x1B, 0x22, 0xE0));
            panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 2f;   // [33차] .utk-window 2px 계약
            panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(Dark.Stroke);
            panel.style.borderTopLeftRadius = 8f; panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f; panel.style.borderBottomRightRadius = 8f;
            panel.style.paddingTop = 28.8f; panel.style.paddingBottom = 28.8f;   // [Figma 72:80 실측] pad28.8(31차)
            panel.style.paddingLeft = 28.8f; panel.style.paddingRight = 28.8f;
            Content.Add(panel);

            // PanelHeader — 서브타이틀 + Badge
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 20f;   // [Figma 72:17] gap20
            panel.Add(head);

            _subtitle = new Label("FISHING RESULT");
            _subtitle.style.fontSize = 16.8f;                                   // [Figma 72:80] fs16.8 fw700(31차)
            _subtitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _subtitle.style.color = Dark.TextSub;
            _subtitle.style.flexGrow = 1f;
            head.Add(_subtitle);

            _badge = new Label("SUCCESS");
            _badge.style.fontSize = 13.2f;
            _badge.style.unityFontStyleAndWeight = FontStyle.Bold;   // [Figma 72:80] fw700(31차)
            _badge.style.color = Dark.Accent;
            _badge.style.backgroundColor = Dark.Accent;
            _badge.style.borderTopLeftRadius = 4f; _badge.style.borderTopRightRadius = 4f;
            _badge.style.borderBottomLeftRadius = 4f; _badge.style.borderBottomRightRadius = 4f;
            _badge.style.paddingTop = 2f; _badge.style.paddingBottom = 2f;
            _badge.style.paddingLeft = 8f; _badge.style.paddingRight = 8f;
            // 배지 배경은 액센트, 글자는 대비 — 별도 인라인
            _badge.style.color = Dark.Bg;   // 액센트 배경 위 어두운 글자
            head.Add(_badge);

            // NotificationTitle
            _verb = new Label("");
            _verb.style.fontSize = 18f;      // [Figma 72:80] fs18 fw500(31차)
            _verb.style.color = Dark.Accent;
            _verb.style.marginTop = 20f;   // [Figma] gap20
            _verb.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_verb);

            _itemName = new Label("");
            _itemName.style.fontSize = 28.8f;   // [Figma 72:80 실측] fs28.8 fw800 — 메인 타이틀 대형(31차, 기존 15.6)
            _itemName.style.unityFontStyleAndWeight = FontStyle.Bold;
            _itemName.style.color = Dark.Gold;
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _itemName.style.marginBottom = 20f;   // [Figma] gap20
            panel.Add(_itemName);

            // ItemImageSection — TierStrip(상단 4px) + 클래스 + 이미지
            var imageSec = new VisualElement();
            imageSec.style.marginBottom = 20f;   // [Figma] gap20
            imageSec.style.paddingTop = 0f; imageSec.style.paddingBottom = 0f;
            imageSec.style.paddingLeft = 0f; imageSec.style.paddingRight = 0f;
            panel.Add(imageSec);

            _tierStrip = new VisualElement();
            _tierStrip.style.height = 4f;
            _tierStrip.style.marginBottom = 4f;
            imageSec.Add(_tierStrip);

            var imageRow = new VisualElement();
            imageRow.style.height = 312f;         // [Figma 72:80 실측] ItemImage 518.4×312 대형 영역(31차)
            imageRow.style.backgroundColor = Dark.Inset;
            imageRow.style.borderTopLeftRadius = 6f; imageRow.style.borderTopRightRadius = 6f;
            imageRow.style.borderBottomLeftRadius = 6f; imageRow.style.borderBottomRightRadius = 6f;
            imageRow.style.paddingTop = 0f; imageRow.style.paddingBottom = 0f;
            imageRow.style.paddingLeft = 0f; imageRow.style.paddingRight = 0f;
            imageSec.Add(imageRow);

            _tierClass = new Label("LEGENDARY CLASS");
            _tierClass.style.fontSize = 13.2f;    // [Figma 72:80] fs13.2 fw700 — 이미지 좌상단 오버레이
            _tierClass.style.unityFontStyleAndWeight = FontStyle.Bold;
            _tierClass.style.color = Dark.Gold;
            _tierClass.style.position = Position.Absolute;   // [Figma] 이미지 위 오버레이(14.4,14.4)
            _tierClass.style.left = 14.4f;
            _tierClass.style.top = 14.4f;
            imageRow.Add(_tierClass);

            // [32차 수리] 31차의 absolute inset0 + utk-slot 클래스 + BackgroundSize.Contain 조합이
            //   ".utk-slot 64px/불투명 검정" 클래스 충돌로 거대 검은 박스+좌상단 소형 아이콘(.ASS 가림)을
            //   만들었다(벌목 ui.PNG 실측). 검증된 기본 배경 스케일로 회귀 — 정사각 베이크와 1:1인
            //   276×276(=312−36) 중앙 배치, 클래스 미적용.
            _itemImage = new VisualElement();
            _itemImage.style.width = 276f;
            _itemImage.style.height = 276f;
            _itemImage.style.alignSelf = Align.Center;
            _itemImage.style.marginTop = 18f;    // (312−276)/2 — 수직 중앙
            imageRow.Add(_itemImage);

            // ItemSpecs — 3칸 SpecBox
            var specs = new VisualElement();
            specs.style.flexDirection = FlexDirection.Row;
            specs.style.marginBottom = 20f;   // [Figma] gap20
            panel.Add(specs);

            BuildSpecBox(specs, out _spec1Label, out _spec1Val);
            BuildSpecBox(specs, out _spec2Label, out _spec2Val);
            BuildSpecBox(specs, out _spec3Label, out _spec3Val, last: true);

            // DescriptionSection — [Figma 72:80 실측] 518.4×155, pad 19.2, 제목 fs16.8/본문 fs15.6(31차)
            var descSec = new VisualElement();
            descSec.style.backgroundColor = Dark.Inset;
            descSec.style.borderTopLeftRadius = 6f; descSec.style.borderTopRightRadius = 6f;
            descSec.style.borderBottomLeftRadius = 6f; descSec.style.borderBottomRightRadius = 6f;
            descSec.style.paddingTop = 19.2f; descSec.style.paddingBottom = 19.2f;
            descSec.style.paddingLeft = 19.2f; descSec.style.paddingRight = 19.2f;
            descSec.style.minHeight = 155f;
            descSec.style.marginBottom = 20f;   // [Figma] gap20
            panel.Add(descSec);

            _descHeader = new Label("물고기 특징");
            _descHeader.style.fontSize = 16.8f;
            _descHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
            _descHeader.style.color = Dark.Accent;
            _descHeader.style.marginBottom = 9.6f;
            descSec.Add(_descHeader);

            _descBody = new Label("");
            _descBody.style.fontSize = 15.6f;
            _descBody.style.color = Dark.TextMain;
            _descBody.style.whiteSpace = WhiteSpace.Normal;
            descSec.Add(_descBody);

            // ActionButtons — already-acquired item confirmation and honest close-only alternative
            var actions = new VisualElement();
            _actions = actions;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 4f;
            panel.Add(actions);

            // [33차] 버튼 = 실제 동작(피그마 KeepButton/DiscardButton 계약) — 습득 유지 또는
            //   인벤토리에서 제거 후 닫기. 기존 "확인·이미 획득됨/닫기·인벤토리 유지" 무동작 표기 폐지.
            var keepBtn = UTKButton.Create("습득하기", () => Close(), UTKButton.Variant.Primary);
            keepBtn.style.flexGrow = 1f;
            keepBtn.style.marginRight = 9.6f;   // [Figma 72:80] 버튼 간격 9.6(31차)
            keepBtn.style.height = 48.8f;       // [Figma] 48.8
            keepBtn.style.fontSize = 16.8f;
            keepBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            StyleKeepButton(keepBtn);
            actions.Add(keepBtn);

            // 버리기 — 습득분을 인벤토리에서 제거(아이템 id+수량 기준) 후 창 닫기.
            var discardBtn = UTKButton.Create("버리기", () =>
            {
                var item = HarvestResultBridge.LatestItemData;
                if (item != null && PlayerInventory.Instance != null)
                    PlayerInventory.Instance.RemoveItem(item.id, Mathf.Max(1, HarvestResultBridge.LatestCount));
                Close();
            }, UTKButton.Variant.Secondary);
            discardBtn.style.flexGrow = 1f;
            discardBtn.style.height = 48.8f;
            discardBtn.style.fontSize = 16.8f;
            discardBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
            StyleCloseButton(discardBtn);
            actions.Add(discardBtn);

            // SystemTip
            var tip = new VisualElement();
            _tip = tip;
            tip.style.flexDirection = FlexDirection.Row;
            tip.style.alignItems = Align.Center;
            tip.style.marginTop = 8f;
            Content.Add(tip);

            _tipLabel = new Label("");
            _tipLabel.style.fontSize = 14.4f;   // [Figma 72:80] SystemTip fs14.4(31차)
            _tipLabel.style.color = Dark.TextSub;
            _tipLabel.style.whiteSpace = WhiteSpace.Normal;
            tip.Add(_tipLabel);

            style.display = DisplayStyle.None;
            style.width = 1920f; style.height = 1080f;
            Content.style.position = Position.Absolute;
            Content.style.left = 0f; Content.style.top = 0f;
            Content.style.right = 0f; Content.style.bottom = 0f;
            _header.style.position = Position.Absolute;
            _header.style.left = 0f; _header.style.top = 0f;
            _header.style.width = 1920f; _header.style.height = 76.8f;
            _header.style.display = DisplayStyle.None;   // [32차] 좌상단 유령 "벌목" 텍스트 원인 — 캔버스 헤더 스트립은 게임 HUD와 중복, 폐지
            _resultPanel.style.position = Position.Absolute;
            _resultPanel.style.left = 672f; _resultPanel.style.top = 111.2f;
            _resultPanel.style.width = WinW; _resultPanel.style.height = WinH;
            _actions.style.position = Position.Absolute;
            _actions.style.left = 28.8f; _actions.style.right = 28.8f;   // [Figma 72:80] pad28.8(31차)
            _actions.style.bottom = 28.8f;
            _tip.style.position = Position.Absolute;
            _tip.style.left = 675.3f; _tip.style.top = 1028.6f;
            _tip.style.width = 569.4f; _tip.style.height = 17f;
        }

        private void ApplyCanvasBounds()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null) return;
            FigmaCanvasLayout.Apply(this, new Rect(0f, 0f, 1920f, 1080f), root);
            // [32차 수리] sx/sy 독립 스케일 = 게임뷰가 16:9가 아니면 세로 15% 늘어남(벌목 ui.PNG
            //   "세로로 이상해짐"의 실측 원인) — 등배수 계약(k=min) 균일 스케일로 교정.
            float kx = root.resolvedStyle.width / FigmaCanvasLayout.CanvasWidth;
            float ky = root.resolvedStyle.height / FigmaCanvasLayout.CanvasHeight;
            float k = Mathf.Min(kx, ky);
            _header.style.width = 1920f * k; _header.style.height = 76.8f * k;
            _header.style.left = 0f; _header.style.top = 0f;
            float resultHeight = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Gathering => 862.8f,
                HarvestResultBridge.HarvestKind.Mining => 896.8f,
                // [2026-10-11 28차] 벌목 — gathering variant 공유(동일 템플릿 패밀리)
                HarvestResultBridge.HarvestKind.Woodcutting => 862.8f,
                _ => 859.8f,
            };
            float resultTop = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Gathering => 109.7f,
                HarvestResultBridge.HarvestKind.Mining => 92.7f,
                HarvestResultBridge.HarvestKind.Woodcutting => 109.7f,
                _ => 111.2f,
            };
            float tipTop = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Gathering => 1030.1f,
                HarvestResultBridge.HarvestKind.Mining => 1047.1f,
                HarvestResultBridge.HarvestKind.Woodcutting => 1030.1f,
                _ => 1028.6f,
            };
            _resultPanel.style.left = 672f * k; _resultPanel.style.top = resultTop * k;
            _resultPanel.style.width = WinW * k; _resultPanel.style.height = resultHeight * k;
            if (_panelShadow != null)
            {
                _panelShadow.style.left = (672f - 14f) * k;
                _panelShadow.style.top = (resultTop - 14f) * k;
                _panelShadow.style.width = (WinW + 28f) * k;
                _panelShadow.style.height = (resultHeight + 28f) * k;
            }
            _tip.style.left = 675.3f * k; _tip.style.top = tipTop * k;
            _tip.style.width = 569.4f * k; _tip.style.height = 17f * k;
        }

        private void BuildSpecBox(VisualElement parent, out Label lbl, out Label val, bool last = false)
        {
            var box = new VisualElement();
            // [Figma 72:80 실측] SpecBox 166.4×70.6, 간격 9.6 — 32차: flexGrow 균등 분할(패널 테두리
            //   2px 분 때문에 고정폭 3칸은 2px 넘침 — 벌목 ui.PNG "수량 박스 튀어나옴" 실측)
            box.style.flexGrow = 1f;
            box.style.flexShrink = 0f;
            box.style.height = 70.6f;
            box.style.marginRight = last ? 0f : 9.6f;
            box.style.backgroundColor = Dark.Sub;
            box.style.borderTopLeftRadius = 6f; box.style.borderTopRightRadius = 6f;
            box.style.borderBottomLeftRadius = 6f; box.style.borderBottomRightRadius = 6f;
            box.style.paddingTop = 14.4f; box.style.paddingBottom = 6f;
            parent.Add(box);

            lbl = new Label("—");
            lbl.style.fontSize = 13.2f;
            lbl.style.color = Dark.TextSub;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(lbl);

            val = new Label("—");
            val.style.fontSize = 16.8f;
            val.style.unityFontStyleAndWeight = FontStyle.Bold;
            val.style.color = Dark.TextMain;
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(val);
        }

        // ===== 공개 API =====
        public static HarvestResultUTK Instance => _instance;

        /// <summary>[2026-10-11 29차 수리] 전 씬 자가부트 — publish는 이미 호출되고 있었으나(로그 실측:
        /// "🧺 치유초 x1 획득 — 결과 창 표시") Ensure가 어디서도 불리지 않아 Updater(LatestToken 폴링)
        /// 가 부재 → 창이 영구 미오픈이었다. FishingUTK 선례와 동일.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new HarvestResultUTK();
            var go = new GameObject("HarvestResultUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;

            // 폴링을 항상 켠다 (Show/Hide와 무관) — 최초 publish를 놓치지 않도록.
            _instance._lastToken = HarvestResultBridge.LatestToken;   // 스타트 시점 동기화 — 스타트 직전 publish 재표시 방지
            _instance.StartTick();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && _instance.parent == null)
                root.Add(_instance);
            Debug.Log("[HarvestResultUTK] 인스턴스 생성");
        }

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            ApplyCanvasBounds();
        }

        public override void Hide()
        {
            base.Hide();
        }

        private void StartTick()
        {
            if (_tickTask != null) return;
            _tickTask = schedule.Execute(() =>
            {
                PollBridge();
            }).Every(TickMs);
        }

        private void StopTick()
        {
            if (_tickTask != null)
            {
                _tickTask.Pause();
                _tickTask = null;
            }
        }

        // ===== 폴링 — Bridge에서 새 결과 감지 → 표시, consume 후 자동 닫기 =====
        private void PollBridge()
        {
            // [2026-10-11 30차 수리] 아이콘 지연 베이크 재적용 — GLB 아이콘(RT 베이크)은 publish
            //   직후 몇 프레임 뒤에야 완성된다. ApplyLatest는 토큰당 1회라 그 사이를 놓치면
            //   이미지 슬롯이 영구 빈 채로 남았다("glb 사진이 안 뜬다"의 원인). 창이 열려 있는
            //   동안 해결될 때까지 재적용(텍스처가 바뀔 때만 스타일 변경 — 16ms 스팸 없음).
            if (IsOpen && _iconAppliedItemId != HarvestResultBridge.LatestItemData?.id
                && HarvestResultBridge.LatestItemData != null)
            {
                var tex = ItemIconDatabase.GetOrCreateIcon(HarvestResultBridge.LatestItemData);
                if (tex != null)
                {
                    _itemImage.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
                    _iconAppliedItemId = HarvestResultBridge.LatestItemData.id;
                }
            }

            if (HarvestResultBridge.LatestToken == _lastToken) return;
            _lastToken = HarvestResultBridge.LatestToken;

            // 새 결과가 표시되면 이전 만료 타이머를 대체한다.
            ApplyLatest();
            ApplyCanvasBounds();
            Show();
            // [33차] 3초 자동 닫기 폐지 — "창이 자동으로 닫아지는 버그".
            //   습득하기/버리기 클릭(또는 ESC) 시에만 닫힌다.
        }

        // [33차] 3초 자동 닫기 제거 — RestartAutoClose/CancelAutoClose 폐지(자동 닫힘 버그 근원).
        //   창은 습득하기/버리기(또는 ESC/새 결과)로만 닫힌다.

        private void ApplyLatest()
        {
            _subtitle.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "FISHING RESULT",
                HarvestResultBridge.HarvestKind.Gathering => "GATHERING RESULT",
                HarvestResultBridge.HarvestKind.Woodcutting => "WOODCUTTING RESULT",   // [28차]
                _ => "MINING RESULT",
            };
            _badge.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "SUCCESS",
                HarvestResultBridge.HarvestKind.Gathering => "채집 성공",
                HarvestResultBridge.HarvestKind.Woodcutting => "벌목 성공",             // [28차]
                _ => "채굴 성공",
            };
            _verb.text = HarvestResultBridge.LatestResultVerb;
            _itemName.text = HarvestResultBridge.LatestItemName;

            // 등급
            Color rarityColor = EquipmentRarityData.GetRarityColor(HarvestResultBridge.LatestRarity);
            _tierStrip.style.backgroundColor = rarityColor;
            string rarityName = EquipmentRarityData.GetRarityDisplayName(HarvestResultBridge.LatestRarity);
            _tierClass.text = string.IsNullOrEmpty(rarityName) ? "" : $"{rarityName} CLASS";

            // 아이콘
            _itemImage.style.backgroundImage = StyleKeyword.Null;
            _iconAppliedItemId = null;   // [30차] 새 결과 — 재적용 재개
            if (HarvestResultBridge.LatestItemData != null)
            {
                var tex = ItemIconDatabase.GetOrCreateIcon(HarvestResultBridge.LatestItemData);
                if (tex != null)
                    _itemImage.style.backgroundImage = UTKTextureSafe.ToBackground(tex);
            }

            // Spec 3칸
            _spec1Label.text = "희귀도"; _spec1Val.text = rarityName;
            int sell = 0;
            if (HarvestResultBridge.LatestItemData != null)
                sell = EconomyPricing.GetSellPrice(HarvestResultBridge.LatestItemData);
            _spec2Label.text = "판매가"; _spec2Val.text = sell > 0 ? $"{sell}G" : "—";
            _spec3Label.text = "수량";    _spec3Val.text = $"x{HarvestResultBridge.LatestCount}";

            _descHeader.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "물고기 특징",
                HarvestResultBridge.HarvestKind.Gathering => "약초 특징",
                HarvestResultBridge.HarvestKind.Woodcutting => "통나무 특징",           // [28차]
                _ => "광석 특징",
            };
            _descBody.text = string.IsNullOrEmpty(HarvestResultBridge.LatestDescription)
                ? "설명이 없습니다."
                : HarvestResultBridge.LatestDescription;

            _locLabel.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "물가 낚시",
                HarvestResultBridge.HarvestKind.Gathering => "채집",
                HarvestResultBridge.HarvestKind.Woodcutting => "벌목",                // [28차]
                _ => "광맥 채굴",
            };
            _staminaLabel.text = "";
            _tipLabel.text = HarvestResultBridge.LatestTip;

            // consume — 다음 publish 전 중복 방지
            HarvestResultBridge.MarkConsumed();
        }

        // ===== 스타일 (이 창 한정 인라인) =====
        private void ApplyGitHubDarkStyle()
        {
            // [32차 — Fluent 전환] 단독 불투명 풀스크린 창 폐지 — 뒷 배경(월드/HUD)이 희미하게
            //   비치는 플루언트 플로팅 패널로. 창 루트는 완전 투명+클릭 투과, 패널만 유리 배경.
            style.backgroundColor = new StyleColor(Color.clear);
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 0f;
            style.borderTopLeftRadius = 0f; style.borderTopRightRadius = 0f;
            style.borderBottomLeftRadius = 0f; style.borderBottomRightRadius = 0f;
            pickingMode = PickingMode.Ignore;   // [32차] 빈 영역 클릭 통과 — 패널/버튼은 자체 수신(IStyle 아닌 VE 속성)
            style.color = Dark.TextMain;
            style.minWidth = WinW; style.minHeight = WinH;

            var titleBar = this.Q("TitleBar");
            if (titleBar != null)
                titleBar.style.backgroundColor = Dark.Sub;
            if (_titleLabel != null) _titleLabel.style.color = Dark.TextMain;
        }

        private static void StyleKeepButton(Button btn)
        {
            if (btn == null) return;
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = Dark.Accent;
            btn.style.color = Dark.Bg;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(Dark.Accent);
            btn.style.borderTopLeftRadius = 6f; btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f; btn.style.borderBottomRightRadius = 6f;
            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = Hex(0x79C0FF));
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = Dark.Accent);
        }

        private static void StyleCloseButton(Button btn)
        {
            if (btn == null) return;
            btn.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            btn.style.backgroundColor = Dark.WhiteBg;
            btn.style.color = Dark.Danger;
            btn.style.borderTopWidth = btn.style.borderBottomWidth = btn.style.borderLeftWidth = btn.style.borderRightWidth = 1f;
            btn.style.borderTopColor = btn.style.borderBottomColor = btn.style.borderLeftColor = btn.style.borderRightColor = new StyleColor(Dark.WhiteBg);
            btn.style.borderTopLeftRadius = 6f; btn.style.borderTopRightRadius = 6f;
            btn.style.borderBottomLeftRadius = 6f; btn.style.borderBottomRightRadius = 6f;
            btn.RegisterCallback<PointerEnterEvent>(_ => btn.style.backgroundColor = Hex(0xE6E6E6));
            btn.RegisterCallback<PointerLeaveEvent>(_ => btn.style.backgroundColor = Dark.WhiteBg);
        }

        // ===== 입력 Updater — UIRoot 재부착 + 필요한 경우 Ensure =====
        private class Updater : MonoBehaviour
        {
            public HarvestResultUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null && window.IsOpen)
                    root.Add(window);
            }

            private void OnDestroy()
            {
                if (window != null)
                {
                    window.StopTick();
                    window.RemoveFromHierarchy();
                }
            }
        }
    }
}
