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
    /// 표시 후 Bridge가 이미 아이템 인벤에 들어가 있으므로 '인벤토리에 놓기'는 닫기(보관됨 안내), '버리기'는 닫기.
    /// </summary>
    public class HarvestResultUTK : UTKWindowBase
    {
        private static HarvestResultUTK _instance;
        private static Updater _updater;

        private const float WinW = 500f;
        private const float WinH = 620f;
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
        private IVisualElementScheduledItem _tickTask;
        private int _lastToken = -1;

        private HarvestResultUTK() : base("🎣 채집·광질 결과", new Vector2(WinW, WinH))
        {
            ApplyGitHubDarkStyle();

            // ── HUDHeader ──
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingTop = 4f; header.style.paddingBottom = 4f;
            header.style.paddingLeft = 8f; header.style.paddingRight = 8f;
            header.style.marginBottom = 10f;
            Content.Add(header);

            _locLabel = new Label("—");
            _locLabel.style.fontSize = 13f;
            _locLabel.style.color = Dark.TextSub;
            _locLabel.style.flexGrow = 1f;
            header.Add(_locLabel);

            _staminaLabel = new Label("");
            _staminaLabel.style.fontSize = 13f;
            _staminaLabel.style.color = Hex(0xFF7300);   // STA 주황
            header.Add(_staminaLabel);

            // ── ResultPanel ──
            var panel = new VisualElement();
            panel.style.backgroundColor = Dark.Panel;
            panel.style.borderTopWidth = panel.style.borderBottomWidth = panel.style.borderLeftWidth = panel.style.borderRightWidth = 1f;
            panel.style.borderTopColor = panel.style.borderBottomColor = panel.style.borderLeftColor = panel.style.borderRightColor = new StyleColor(Dark.Stroke);
            panel.style.borderTopLeftRadius = 8f; panel.style.borderTopRightRadius = 8f;
            panel.style.borderBottomLeftRadius = 8f; panel.style.borderBottomRightRadius = 8f;
            panel.style.paddingTop = 12f; panel.style.paddingBottom = 12f;
            panel.style.paddingLeft = 12f; panel.style.paddingRight = 12f;
            Content.Add(panel);

            // PanelHeader — 서브타이틀 + Badge
            var head = new VisualElement();
            head.style.flexDirection = FlexDirection.Row;
            head.style.alignItems = Align.Center;
            head.style.marginBottom = 6f;
            panel.Add(head);

            _subtitle = new Label("FISHING RESULT");
            _subtitle.style.fontSize = 14f;
            _subtitle.style.color = Dark.TextSub;
            _subtitle.style.flexGrow = 1f;
            head.Add(_subtitle);

            _badge = new Label("SUCCESS");
            _badge.style.fontSize = 13f;
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
            _verb.style.fontSize = 18f;
            _verb.style.color = Dark.Accent;
            _verb.style.marginTop = 6f;
            _verb.style.whiteSpace = WhiteSpace.Normal;
            panel.Add(_verb);

            _itemName = new Label("");
            _itemName.style.fontSize = 15f;
            _itemName.style.color = Dark.Gold;
            _itemName.style.whiteSpace = WhiteSpace.Normal;
            _itemName.style.marginBottom = 8f;
            panel.Add(_itemName);

            // ItemImageSection — TierStrip(상단 4px) + 클래스 + 이미지
            var imageSec = new VisualElement();
            imageSec.style.marginBottom = 8f;
            imageSec.style.paddingTop = 0f; imageSec.style.paddingBottom = 0f;
            imageSec.style.paddingLeft = 0f; imageSec.style.paddingRight = 0f;
            panel.Add(imageSec);

            _tierStrip = new VisualElement();
            _tierStrip.style.height = 4f;
            _tierStrip.style.marginBottom = 4f;
            imageSec.Add(_tierStrip);

            var imageRow = new VisualElement();
            imageRow.style.flexDirection = FlexDirection.Row;
            imageRow.style.alignItems = Align.Center;
            imageRow.style.backgroundColor = Dark.Inset;
            imageRow.style.borderTopLeftRadius = 6f; imageRow.style.borderTopRightRadius = 6f;
            imageRow.style.borderBottomLeftRadius = 6f; imageRow.style.borderBottomRightRadius = 6f;
            imageRow.style.paddingTop = 16f; imageRow.style.paddingBottom = 16f;
            imageRow.style.paddingLeft = 12f; imageRow.style.paddingRight = 12f;
            imageSec.Add(imageRow);

            _tierClass = new Label("LEGENDARY CLASS");
            _tierClass.style.fontSize = 14f;
            _tierClass.style.color = Dark.Gold;
            _tierClass.style.flexGrow = 1f;
            imageRow.Add(_tierClass);

            _itemImage = new VisualElement();
            _itemImage.style.width = 120f;
            _itemImage.style.height = 120f;
            _itemImage.style.alignSelf = Align.Center;
            _itemImage.AddToClassList("utk-slot");
            imageRow.Add(_itemImage);

            // ItemSpecs — 3칸 SpecBox
            var specs = new VisualElement();
            specs.style.flexDirection = FlexDirection.Row;
            specs.style.marginBottom = 8f;
            panel.Add(specs);

            BuildSpecBox(specs, out _spec1Label, out _spec1Val);
            BuildSpecBox(specs, out _spec2Label, out _spec2Val);
            BuildSpecBox(specs, out _spec3Label, out _spec3Val);

            // DescriptionSection
            var descSec = new VisualElement();
            descSec.style.backgroundColor = Dark.Inset;
            descSec.style.borderTopLeftRadius = 6f; descSec.style.borderTopRightRadius = 6f;
            descSec.style.borderBottomLeftRadius = 6f; descSec.style.borderBottomRightRadius = 6f;
            descSec.style.paddingTop = 8f; descSec.style.paddingBottom = 8f;
            descSec.style.paddingLeft = 10f; descSec.style.paddingRight = 10f;
            descSec.style.marginBottom = 10f;
            panel.Add(descSec);

            _descHeader = new Label("물고기 특징");
            _descHeader.style.fontSize = 14f;
            _descHeader.style.color = Dark.Accent;
            _descHeader.style.marginBottom = 4f;
            descSec.Add(_descHeader);

            _descBody = new Label("");
            _descBody.style.fontSize = 13f;
            _descBody.style.color = Dark.TextMain;
            _descBody.style.whiteSpace = WhiteSpace.Normal;
            descSec.Add(_descBody);

            // ActionButtons — 하단 밖(패널 밖) Keep/Discard
            var actions = new VisualElement();
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.marginTop = 4f;
            Content.Add(actions);

            var keepBtn = UTKButton.Create("인벤토리에 놓기", () => Close(), UTKButton.Variant.Primary);
            keepBtn.style.flexGrow = 1f;
            keepBtn.style.marginRight = 6f;
            StyleKeepButton(keepBtn);
            actions.Add(keepBtn);

            var discardBtn = UTKButton.Create("버리기", () => Close(), UTKButton.Variant.Secondary);
            discardBtn.style.flexGrow = 1f;
            StyleDiscardButton(discardBtn);
            actions.Add(discardBtn);

            // SystemTip
            var tip = new VisualElement();
            tip.style.flexDirection = FlexDirection.Row;
            tip.style.alignItems = Align.Center;
            tip.style.marginTop = 8f;
            Content.Add(tip);

            _tipLabel = new Label("");
            _tipLabel.style.fontSize = 12f;
            _tipLabel.style.color = Dark.TextSub;
            _tipLabel.style.whiteSpace = WhiteSpace.Normal;
            tip.Add(_tipLabel);

            style.display = DisplayStyle.None;
            style.left = Length.Percent(62f);
            style.top = 10f;
        }

        private void BuildSpecBox(VisualElement parent, out Label lbl, out Label val)
        {
            var box = new VisualElement();
            box.style.flexGrow = 1f;
            box.style.marginRight = 4f;
            box.style.backgroundColor = Dark.Sub;
            box.style.borderTopLeftRadius = 6f; box.style.borderTopRightRadius = 6f;
            box.style.borderBottomLeftRadius = 6f; box.style.borderBottomRightRadius = 6f;
            box.style.paddingTop = 6f; box.style.paddingBottom = 6f;
            parent.Add(box);

            lbl = new Label("—");
            lbl.style.fontSize = 12f;
            lbl.style.color = Dark.TextSub;
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(lbl);

            val = new Label("—");
            val.style.fontSize = 14f;
            val.style.color = Dark.TextMain;
            val.style.unityTextAlign = TextAnchor.MiddleCenter;
            box.Add(val);
        }

        // ===== 공개 API =====
        public static HarvestResultUTK Instance => _instance;

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
            if (HarvestResultBridge.LatestToken == _lastToken) return;
            _lastToken = HarvestResultBridge.LatestToken;

            // 표시 + 3초 후 자동 닫기
            ApplyLatest();
            Show();
            schedule.Execute(() => { if (IsOpen) Close(); }).ExecuteLater(3000L);
        }

        private void ApplyLatest()
        {
            _subtitle.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "FISHING RESULT",
                HarvestResultBridge.HarvestKind.Gathering => "GATHERING RESULT",
                _ => "MINING RESULT",
            };
            _badge.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "SUCCESS",
                HarvestResultBridge.HarvestKind.Gathering => "채집 성공",
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
                _ => "광석 특징",
            };
            _descBody.text = string.IsNullOrEmpty(HarvestResultBridge.LatestDescription)
                ? "설명이 없습니다."
                : HarvestResultBridge.LatestDescription;

            _locLabel.text = HarvestResultBridge.LatestKind switch
            {
                HarvestResultBridge.HarvestKind.Fishing   => "물가 낚시",
                HarvestResultBridge.HarvestKind.Gathering => "채집",
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
            style.backgroundColor = Dark.Bg;
            style.backgroundImage = new StyleBackground(StyleKeyword.None);
            style.borderTopWidth = style.borderBottomWidth = style.borderLeftWidth = style.borderRightWidth = 1f;
            style.borderTopColor = style.borderBottomColor = style.borderLeftColor = style.borderRightColor = new StyleColor(Dark.Stroke);
            style.borderTopLeftRadius = 8f; style.borderTopRightRadius = 8f;
            style.borderBottomLeftRadius = 8f; style.borderBottomRightRadius = 8f;
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

        private static void StyleDiscardButton(Button btn)
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
