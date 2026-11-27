using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core.Data;       // VillageDrugState/VillageDrugSnapshot/NationType — 등록가 단일 소스
using ProjectName.Core.Utils;      // VillageDrugMath — 권장가 순수 수학(위조 금지)
using ProjectName.Systems;         // ConstructionManager(정적 이벤트 출처)/RestaurantSystem(DefaultFoodBasePrice 폴백)
using ProjectName.UI;              // UIFont 공용 폰트

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [154차 P4] 음식점 초기 등록가 입력 모달 (UTK) — 건설 배치 확정 직후 1회 개방.
    ///
    /// [진입점 — 단방향 계약]
    ///  · ConstructionManager.TryPlace 음식점 분기 성공 → RestaurantPlacementSucceeded(nation, villageIndex, permitFee)
    ///    정적 이벤트(Systems→UI 단방향 — FarmPlot.FarmStatusBroadcast 선례) → 본 모달 Open(nation, villageIndex, permitFee).
    ///  · Bootstrap 자가부트(AfterSceneLoad) + 구독 전 해제 패턴(도메인 리로드/재진입 중복 방지) + EnsureAttached 멱등.
    ///
    /// [UI — 프레임리스 소형 모달, 기존 창 스타일 상수/폰트 재사용(Figma 단일 소스 — 신규 비주얼 언어 금지)]
    ///  · 제목 "음식점 초기 등록가" + 안내 행(허가세 feeG는 자동 지출되었으며 등록가는 언제든 음식점 창에서 변경 가능)
    ///    + 숫자 TextField(가격박스 98×32 #0F141B r6 — RestaurantWindowUTK RowPriceBox 동일 톤) + 확인/나중에 버튼.
    ///  · 기본값 — VillageDrugState.GetSnapshot(nation, villageIndex).registeredPrice가 0보다 크면 그 값,
    ///    아니면 음식점 창 권장가와 동일 진실원: VillageDrugMath.RecommendedPrice(basePrice, addiction).
    ///    배치 시점에는 재고 음식이 없어 basePrice는 RestaurantWindowUTK.ResolveDisplayBasePrice의
    ///    0=무가격 폴백 규약(RestaurantSystem.DefaultFoodBasePrice 10G)을 동일하게 소비한다.
    ///
    /// [커밋 — RestaurantWindowUTK.CommitRegisteredPrice 동일 규칙]
    ///  · 확인: int.TryParse 0 이상만 수용 → VillageDrugState.SetRegisteredPrice + UTKToastService.Show
    ///    "초기 등록가 등록: {price}G" → 창 닫힘. 동일값 재등록은 생략(CommitRegisteredPrice 규약 준용).
    ///  · 나중에/ESC/닫기: 상태 무변경 닫힘(FocusOut 커밋 유입 차단 — Discard 가드).
    ///  · 음식점 창(RestaurantWindowUTK)의 등록가 입력은 이미 동작 — 본 모달은 배치 시점 입력 신설이며 창 측 무변경.
    ///
    /// [키보드 — [151차 P0] 가격 필드 브리지 패턴 이식(RestaurantWindowUTK 선례)]
    ///  · onTextInput 스코프드 부착(FocusIn~FocusOut) + 33ms 틱(백스페이스/삭제/Enter — Enter는 Blur 후
    ///    FocusOut 커밋 단일 경로) + 실 KeyDownEvent 도달 시 자가 은퇴(이중 입력 방지). 창 닫힘 시 Detach 보장.
    /// </summary>
    public class RestaurantInitialPriceModalUTK : UTKWindowBase
    {
        // ═══════════════ [154차 P4] 진실치 — RestaurantWindowUTK Figma 톤 재사용 ═══════════════

        private const float WinW = 360f;          // 소형 모달 360×190
        private const float WinH = 190f;
        private const float RadiusCard = 10f;     // 카드 r10 (RestaurantWindowUTK RadiusCard 동일)
        private const float RadiusBox = 6f;       // 버튼·박스 r6
        private const float PriceBoxW = 98f, PriceBoxH = 32f;   // 가격박스 98×32 — RowPriceBox 동일 스펙
        private const float ButtonW = 150f, ButtonH = 36f;

        // ── 색 토큰 — UTKTheme 공용 + RestaurantWindowUTK 동일 hex(Figma 단일 소스) ──
        private static readonly Color InputCol = new Color32(0x0F, 0x14, 0x1B, 0xFF);   // #0F141B 입력/어두운 표면
        private static readonly Color TextMain = new Color32(0xF0, 0xF6, 0xFC, 0xFF);   // #F0F6FC 본문
        private static readonly Color TextSub = new Color32(0x8B, 0x94, 0x9E, 0xFF);    // #8B949E 보조
        private static readonly Color Accent = new Color32(0x58, 0xA6, 0xFF, 0xFF);     // #58A6FF 액센트
        private const float AccentAlpha = 0.078f;                                       // 액센트 배경 알파
        private static readonly Color AccentBg = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, AccentAlpha);
        private static readonly Color RowStrokeSelected = new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.25f);
        private static readonly Color SurfaceCol = new Color32(0x19, 0x1F, 0x27, 0xFF); // #191F27 표면

        // ═══════════════ 상태 ═══════════════

        private static RestaurantInitialPriceModalUTK _instance;
        public static RestaurantInitialPriceModalUTK Instance => _instance;

        private NationType _nation = NationType.None;
        private int _villageIndex = -1;      // -1 = 미지정 — 커밋 무동작 가드
        private int _permitFee;

        private Label _titleLabel, _infoLabel;
        private TextField _priceField;
        private Button _confirmButton, _laterButton;

        // [151차 P0] 가격 필드 키보드 브리지 상태 — 편집 중인 필드 + 백스페이스/Enter 틱(스코프드 최후수단).
        private TextField _bridgeField;
        private IVisualElementScheduledItem _bridgeTick;
        // 나중에/닫기 경로 — Hide 유발 FocusOut 커밋 차단 가드(상태 무변경 계약).
        private bool _discarding;

        /// <summary>
        /// [154차 P4] 인스턴스 생성자 — public(EditMode 테스트 직접 트리 구성 허용).
        /// 런타임 개방은 static Open 경유(EnsureAttached 멱등 — 기존 인스턴스 재사용).
        /// </summary>
        public RestaurantInitialPriceModalUTK()
            : base("음식점 초기 등록가", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            // Frameless — 창 자체는 투명 루트(ArenaWindowUTK "Frameless + 투명 루트" 선례), 내부 카드가 비주얼 담당.
            style.backgroundColor = new StyleColor(Color.clear);

            var card = new VisualElement { name = "InitialPriceCard" };
            card.style.position = Position.Absolute;
            card.style.left = 0f;
            card.style.top = 0f;
            card.style.width = WinW;
            card.style.height = WinH;
            card.style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            ApplyRadius(card, RadiusCard);
            UTKTheme.ApplyElementBorder(card, UTKTheme.GlassPanelStroke, UTKTheme.GlassStrokeWidth);
            _content.Add(card);

            // 제목 — "음식점 초기 등록가"
            _titleLabel = new Label("음식점 초기 등록가") { name = "ModalTitle" };
            _titleLabel.style.fontSize = 14f;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.color = new StyleColor(TextMain);
            _titleLabel.style.left = 20f;
            _titleLabel.style.top = 16f;
            card.Add(_titleLabel);

            // 안내 행 — 허가세 자동 지출 + 언제든 변경 가능
            _infoLabel = new Label(string.Empty) { name = "ModalInfo" };
            _infoLabel.style.fontSize = 11f;
            _infoLabel.style.color = new StyleColor(TextSub);
            _infoLabel.style.left = 20f;
            _infoLabel.style.top = 42f;
            _infoLabel.style.width = WinW - 40f;
            _infoLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(_infoLabel);

            // 가격박스 98×32 #0F141B r6 — RestaurantWindowUTK RowPriceBox 동일 톤 + 숫자 TextField
            var priceBox = new VisualElement { name = "InitialPriceBox" };
            priceBox.style.position = Position.Absolute;
            priceBox.style.left = 20f;
            priceBox.style.top = 84f;
            priceBox.style.width = PriceBoxW;
            priceBox.style.height = PriceBoxH;
            priceBox.style.backgroundColor = new StyleColor(InputCol);
            ApplyRadius(priceBox, RadiusBox);
            card.Add(priceBox);

            _priceField = new TextField { name = "InitialPriceField", value = "0" };
            _priceField.style.position = Position.Absolute;
            _priceField.style.left = 8f;
            _priceField.style.top = 2f;
            _priceField.style.right = 22f;
            _priceField.style.bottom = 2f;
            _priceField.style.fontSize = 13f;
            _priceField.style.backgroundColor = new StyleColor(Color.clear);
            _priceField.style.borderTopWidth = _priceField.style.borderBottomWidth = 0f;
            _priceField.style.borderLeftWidth = _priceField.style.borderRightWidth = 0f;
            _priceField.style.color = new StyleColor(TextMain);
            // [151차 P0] 브리지 패턴 이식 — FocusIn 부착 / FocusOut 해제 / 실 KeyDownEvent 자가 은퇴.
            _priceField.RegisterCallback<FocusInEvent>(_ => AttachPriceKeyboardBridge(_priceField));
            _priceField.RegisterCallback<FocusOutEvent>(_ => DetachPriceKeyboardBridge());
            _priceField.RegisterCallback<KeyDownEvent>(_ =>
            {
                if (_bridgeField != null)
                    DetachPriceKeyboardBridge();   // 실 전달 확인 — 브리지 자가 은퇴(이중 입력 방지)
            });
            _priceField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    evt.StopPropagation();
                    CommitInitialPrice();   // Enter 즉시 커밋 — RestaurantWindowUTK 등록가 필드 동일 계약
                }
            });
            // FocusOut 커밋 — CommitRegisteredPrice 동일 단일 경로(나중에/닫기는 Discard 가드로 차단).
            _priceField.RegisterCallback<FocusOutEvent>(_ => CommitOnFocusOut());
            priceBox.Add(_priceField);

            var unitLabel = new Label("G") { name = "PriceUnit" };
            unitLabel.style.fontSize = 11f;
            unitLabel.style.color = new StyleColor(TextSub);
            unitLabel.style.position = Position.Absolute;
            unitLabel.style.right = 10f;   // RestaurantWindowUTK [144차 P7-RST #91] 화폐 right 10 동일
            unitLabel.style.top = (PriceBoxH - 14f) * 0.5f;
            unitLabel.pickingMode = PickingMode.Ignore;
            priceBox.Add(unitLabel);

            // 확인 버튼 150×36 — PlaceButton 톤(#58A6FF/0.078 r6 + 액센트 보더)
            _confirmButton = new Button(CommitInitialPrice) { name = "ConfirmButton", text = "" };
            _confirmButton.style.position = Position.Absolute;
            _confirmButton.style.left = 20f;
            _confirmButton.style.top = 136f;
            _confirmButton.style.width = ButtonW;
            _confirmButton.style.height = ButtonH;
            _confirmButton.style.backgroundColor = new StyleColor(AccentBg);
            ApplyRadius(_confirmButton, RadiusBox);
            UTKTheme.ApplyElementBorder(_confirmButton, RowStrokeSelected);
            var confirmLabel = new Label("확인") { name = "ConfirmButtonLabel" };
            ApplyButtonLabel(confirmLabel, Accent);
            _confirmButton.Add(confirmLabel);
            card.Add(_confirmButton);

            // 나중에 버튼 150×36 — 표면 톤(상태 무변경 닫힘)
            _laterButton = new Button(Discard) { name = "LaterButton", text = "" };
            _laterButton.style.position = Position.Absolute;
            _laterButton.style.left = WinW - 20f - ButtonW;
            _laterButton.style.top = 136f;
            _laterButton.style.width = ButtonW;
            _laterButton.style.height = ButtonH;
            _laterButton.style.backgroundColor = new StyleColor(SurfaceCol);
            ApplyRadius(_laterButton, RadiusBox);
            UTKTheme.ApplyElementBorder(_laterButton, UTKTheme.GlassPanelStroke);
            var laterLabel = new Label("나중에") { name = "LaterButtonLabel" };
            ApplyButtonLabel(laterLabel, TextSub);
            _laterButton.Add(laterLabel);
            card.Add(_laterButton);

            ApplyUIToolkitFont(this);
            DisableWindowShadow();   // 프레임리스 소형 모달 — rim 음영 제거(ConstructionWindowUTK 선례)
        }

        private void ApplyButtonLabel(Label label, Color color)
        {
            label.style.position = Position.Absolute;
            label.style.left = 0f;
            label.style.right = 0f;
            label.style.top = 0f;
            label.style.bottom = 0f;
            label.style.fontSize = 13f;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(color);
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
        }

        private static void ApplyRadius(VisualElement ve, float radius)
        {
            ve.style.borderTopLeftRadius = ve.style.borderTopRightRadius = radius;
            ve.style.borderBottomLeftRadius = ve.style.borderBottomRightRadius = radius;
        }

        // ═══════════════ 자가부트 / 개방 API ═══════════════

        /// <summary>[154차 P4] 자가부트 — RestaurantWindowUTK AfterSceneLoad 패턴 준용. 구독 전 해제 패턴.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            EnsureAttached();

            // Systems→UI 단방향 — ConstructionManager 정적 이벤트 구독(FarmPlot.FarmStatusBroadcast 선례).
            ConstructionManager.RestaurantPlacementSucceeded -= OnRestaurantPlacementSucceeded;
            ConstructionManager.RestaurantPlacementSucceeded += OnRestaurantPlacementSucceeded;
        }

        private static void EnsureAttached()
        {
            var root = UIToolkitBootstrap.UIRoot;
            // [117차 R3] 고아 가드 — parent != null이어도 UIRoot 후손이 아니면 비렌더(RestaurantWindowUTK 선례).
            if (_instance != null)
            {
                bool orphaned = _instance.parent == null;
                if (!orphaned)
                {
                    var p = _instance.parent;
                    while (p != null && p != root) p = p.parent;
                    orphaned = p != root;
                }
                if (orphaned)
                {
                    Debug.LogWarning("[RestaurantInitialPriceModalUTK] 고아 인스턴스 감지 — 제거 후 재생성 ([117차 R3])");
                    _instance.RemoveFromHierarchy();
                    _instance = null;
                }
            }
            if (root == null) return;   // 기존 계약 — 루트 부재 시 생성 생략
            if (_instance == null)
                _instance = new RestaurantInitialPriceModalUTK();
            if (_instance.parent == null)
                root.Add(_instance);
        }

        private static void OnRestaurantPlacementSucceeded(NationType nation, int villageIndex, int permitFee)
        {
            Open(nation, villageIndex, permitFee);
        }

        /// <summary>
        /// 음식점 초기 등록가 모달 개방 — 배치 확정 성공 직후 1회. 기존 인스턴스 재사용(멱등).
        /// 무효 키(None/-1)와 UIRoot 부재는 안전 반환(로그만 — EditMode 무해).
        /// </summary>
        public static void Open(NationType nation, int villageIndex, int permitFee)
        {
            if (nation == NationType.None || villageIndex < 0)
            {
                Debug.LogWarning("[RestaurantInitialPriceModalUTK] 무효 마을 키 — 개방 생략");
                return;
            }
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning("[RestaurantInitialPriceModalUTK] UIRoot 미준비 — 모달 개방 생략");
                return;
            }

            EnsureAttached();
            _instance.Bind(nation, villageIndex, permitFee);
            if (_instance.IsOpen)
                _instance.BringToFront();   // 이미 개방 — 대상 마을 재바인딩만(Show 재호출 무효 가드)
            else
                _instance.Show();
            Debug.Log($"[RestaurantInitialPriceModalUTK] 모달 개방: {nation}_v{villageIndex} 허가세 {permitFee}G");
        }

        /// <summary>[154차 P4] 대상 마을 바인딩 + 기본값 산출 — Open의 실질 본체(UIRoot 불필요, 테스트 진입 허용).</summary>
        public void Bind(NationType nation, int villageIndex, int permitFee)
        {
            _nation = nation;
            _villageIndex = villageIndex;
            _permitFee = Mathf.Max(permitFee, 0);
            RefreshContent();
        }

        private void RefreshContent()
        {
            if (_infoLabel != null)
                _infoLabel.text = $"허가세 {_permitFee}G는 자동 지출되었으며 등록가는 언제든 음식점 창에서 변경 가능";
            if (_priceField == null) return;

            // 기본값 — 기등록가 > 0이면 그 값(씬 시딩값 30 규약 포함), 아니면 음식점 창 권장가 동일 진실원.
            // 권장가 = VillageDrugMath.RecommendedPrice(basePrice, addiction) — basePrice는 재고 부재 시
            // RestaurantWindowUTK.ResolveDisplayBasePrice의 0=무가격 폴백(RestaurantSystem.DefaultFoodBasePrice) 규약 준용.
            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(_nation, _villageIndex);
            int recommended = VillageDrugMath.RecommendedPrice(RestaurantSystem.DefaultFoodBasePrice, snapshot.addiction);
            int defaultPrice = snapshot.registeredPrice > 0 ? snapshot.registeredPrice : recommended;
            _priceField.value = Mathf.Max(defaultPrice, 0).ToString();
        }

        // ═══════════════ UTKWindowBase 라이프사이클 ═══════════════

        protected override void OnWindowOpen()
        {
            // 화면 중앙 배치 — 소형 모달 관례(TerritoryInfoPopupUTK 선례).
            var root = UIToolkitBootstrap.UIRoot;
            float rootW = root != null ? root.resolvedStyle.width : 1920f;
            float rootH = root != null ? root.resolvedStyle.height : 1080f;
            style.left = Mathf.Max(4f, rootW * 0.5f - WinW * 0.5f);
            style.top = Mathf.Max(4f, rootH * 0.5f - WinH * 0.5f);
        }

        protected override void OnWindowClosed()
        {
            DetachPriceKeyboardBridge();   // [151차 P0] 창 닫힘 — 편집 브리지 정리(포커스 유실 대비)
        }

        /// <summary>ESC/외부 닫기 — 나중에와 동일(상태 무변경 닫힘 계약).</summary>
        public override void Close() => Discard();

        // ═══════════════ 커밋 — CommitRegisteredPrice 동일 규칙 ═══════════════

        /// <summary>
        /// [154차 P4] 초기 등록가 파싱 규칙 — CommitRegisteredPrice 동일(int.TryParse 0 이상만 수용).
        /// 순수 판정 단일소스 — UI/테스트 공용.
        /// </summary>
        public static bool TryParseInitialPrice(string raw, out int price)
            => int.TryParse(raw?.Trim(), out price) && price >= 0;

        /// <summary>확인 — 유효 값만 SetRegisteredPrice + 토스트 + 닫힘. 무효 값은 거부+상태 무변경(개방 유지).</summary>
        public void CommitInitialPrice()
        {
            if (_nation == NationType.None || _villageIndex < 0) return;
            if (!TryParseInitialPrice(_priceField != null ? _priceField.value : null, out int price))
            {
                UTKToastService.Show("초기 등록가 등록 실패 — 0 이상의 정수를 입력하세요");
                return;
            }

            VillageDrugSnapshot snapshot = VillageDrugState.GetSnapshot(_nation, _villageIndex);
            if (snapshot.registeredPrice != price)
            {
                VillageDrugState.SetRegisteredPrice(_nation, _villageIndex, price);
                UTKToastService.Show($"초기 등록가 등록: {price}G");
            }
            Hide();   // 동일값 재등록 생략은 CommitRegisteredPrice 규약 준용 — 닫힘은 확인 경로 공통
        }

        /// <summary>나중에/닫기 — 상태 무변경 닫힘(FocusOut 커밋 유입 차단 가드).</summary>
        public void Discard()
        {
            _discarding = true;
            Hide();
            _discarding = false;
        }

        private void CommitOnFocusOut()
        {
            if (_discarding) return;
            CommitInitialPrice();
        }

        // ═══════════════ [151차 P0] 가격 필드 키보드 브리지(스코프드 최후수단 — RestaurantWindowUTK 이식) ═══════════════
        // 근거/범위/은퇴 규약 원본 유지 — onTextInput 스코프드 부착 + 33ms 틱 + Enter(Blur → FocusOut 커밋 단일 경로)
        // + 실 KeyDownEvent 도달 즉시 해제(이중 입력 방지) + 포커스 상실 자가 점검 + 창 닫힘 Detach 보장.

        private void AttachPriceKeyboardBridge(TextField field)
        {
            DetachPriceKeyboardBridge();   // 이중 부착 방지(멱등)
            if (UnityEngine.InputSystem.Keyboard.current == null) return;   // Input System 부재 — 기존 경로 의존
            _bridgeField = field;
            UnityEngine.InputSystem.Keyboard.current.onTextInput += OnPriceBridgeTextInput;
            _bridgeTick = schedule.Execute(PriceBridgeTick).Every(33);      // ≒1프레임 — wasPressedThisFrame 폴링
        }

        private void DetachPriceKeyboardBridge()
        {
            _bridgeField = null;
            if (_bridgeTick != null)
            {
                _bridgeTick.Pause();
                _bridgeTick = null;
            }
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
                kb.onTextInput -= OnPriceBridgeTextInput;   // 멱등 해제(구독 전 해제 패턴 준용)
        }

        private void OnPriceBridgeTextInput(char c)
        {
            TextField f = _bridgeField;
            if (f == null || !IsElementOrChildFocused(f))
            {
                DetachPriceKeyboardBridge();   // 포커스 상실 후 도달한 지연 이벤트 방어
                return;
            }
            f.value += c;   // 가격 입력 특성(짧은 정수열) — 말미 추가 규약
        }

        private void PriceBridgeTick()
        {
            TextField f = _bridgeField;
            if (f == null || !IsElementOrChildFocused(f))
            {
                DetachPriceKeyboardBridge();   // 편집 종료 — 틱 자가 소명
                return;
            }
            UnityEngine.InputSystem.Keyboard kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null)
            {
                DetachPriceKeyboardBridge();
                return;
            }
            if (kb.backspaceKey.wasPressedThisFrame)
            {
                if (f.value.Length > 0)
                    f.value = f.value.Substring(0, f.value.Length - 1);
            }
            else if (kb.deleteKey.wasPressedThisFrame)
            {
                f.value = string.Empty;
            }
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                f.Blur();   // FocusOut → CommitOnFocusOut → CommitInitialPrice 단일 커밋 경유
            }
        }

        private static bool IsElementOrChildFocused(VisualElement element)
        {
            if (element == null || element.panel == null || element.panel.focusController == null)
                return false;
            var focused = element.panel.focusController.focusedElement as VisualElement;
            return focused != null && (focused == element || element.Contains(focused));
        }
    }
}
