// 2026-10-25 (125차 후속30): 투기장 승/패 결과 창 — 피그마 '투기장 승리 결과'(280:163) / '투기장 패배 결과'(280:381) 정합.
// ArenaWindowUTK 동일 골격: Frameless + 투명 루트 + raw 1600×1040 + ApplyCanvasFit 등배수(좌상단 원점 계약).
// 개방: ArenaBettingSystem.OnResultReady(로비 복귀 완료 후 발화 — K6) 구독. 승리=파랑 Accent / 패배=빨강 Accent.
// [125차 후속30 재실측] 사용자 Figma 수정 — "투기장으로 돌아가기" 버튼 → 텍스트 "확인"(레이어명 잔존, 244×44 accent 유지).
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI.Toolkit
{
    /// <summary>투기장 경기 결과 창 — 승리/패배 2변형(레이아웃 공유+Accent/문구 분기). 로비 복귀 직후 자동 개방.</summary>
    public class ArenaResultWindowUTK : UTKWindowBase
    {
        public const float WinW = 1600f;
        public const float WinH = 1040f;

        // ── 피그마 색 계약 (280:163/280:381 실측 fill 값) ──
        private static readonly Color RootBg = new Color(0.04f, 0.05f, 0.08f, 1f);      // 풀스크린 오버레이
        private static readonly Color PanelBg = new Color(0.07f, 0.09f, 0.12f, 1f);     // 좌우 판
        private static readonly Color CardBg = new Color(0.10f, 0.12f, 0.15f, 1f);      // 카드/행
        private static readonly Color WinAccent = new Color(0.35f, 0.65f, 1.00f, 1f);   // 승리 액센트
        private static readonly Color LoseAccent = new Color(0.94f, 0.47f, 0.47f, 1f);  // 패배 액센트
        private static readonly Color GoldColor = new Color(0.91f, 0.75f, 0.44f, 1f);   // 골드 문장/최종 보유
        private static readonly Color WinGoldBox = new Color(0.10f, 0.16f, 0.22f, 1f);  // 획득 골드 박스(승)
        private static readonly Color LoseGoldBox = new Color(0.16f, 0.11f, 0.14f, 1f); // 수령 골드 박스(패)
        private static readonly Color WinBadge = new Color(0.10f, 0.16f, 0.22f, 1f);    // 승리 표시 배지
        private static readonly Color LoseBadge = new Color(0.20f, 0.13f, 0.16f, 1f);   // 패배 표시 배지
        private static readonly Color DarkInk = new Color(0.04f, 0.05f, 0.08f, 1f);     // accent 위 잉크
        private static readonly Color TextMain = new Color(0.94f, 0.96f, 0.99f, 1f);
        private static readonly Color TextSub = new Color(0.55f, 0.58f, 0.62f, 1f);
        private static readonly Color Stroke = new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 0.502f);   // [144차 P6-RES P1] #30363D@0.502 (판/최종행/구분선)
        private static readonly Color FinalGoldColor = new Color(0xF0 / 255f, 0xF6 / 255f, 0xFC / 255f, 1f);   // [144차 P6-RES P1] 최종 보유량 #F0F6FC
        private static Color Alpha(Color c, float a) => new Color(c.r, c.g, c.b, a);   // [144차 P6-RES P1] 액센트 투명 변형(배지/마크/코인 fill·테두리)

        private static ArenaResultWindowUTK _instance;
        private static bool _hooked;

        // ── UI 핸들 ──
        private Label _fighterName, _fighterOrigin, _fighterRecord, _fighterGuide, _fighterLevel;
        private VisualElement _photoCard, _winBadge, _recordState, _goldStateBadge, _stateBadge, _markBox, _goldBox, _sepLineA, _sepLineB, _oppThumb;
        private Label _oppName, _oppRecord, _oppResult;
        private Label _mainTitle, _mainSub, _resultMsgTitle, _resultMsgSub;
        private Label _goldValue, _goldSub, _goldLabel, _netLine;
        private Label _calcA, _calcB, _calcC, _capA, _capB, _capC, _rowCCap, _sepA, _sepB;
        private Label _rowA, _rowB, _rowC, _finalGold;
        private Label _summaryLabel, _summarySub, _footNote, _helpLeft, _markGlyph;
        private Button _closeButton, _confirmButton;

        private ArenaResultPayload _payload;
        private bool _won;

        private ArenaResultWindowUTK() : base("투기장 결과", new Vector2(WinW, WinH), UTKWindowChrome.Frameless)
        {
            style.backgroundColor = new StyleColor(Color.clear);
            style.overflow = Overflow.Hidden;

            var root = new VisualElement { name = "ArenaResultRoot" };
            root.style.position = Position.Absolute;
            root.style.left = 0f; root.style.top = 0f;
            root.style.width = WinW; root.style.height = WinH;
            root.style.backgroundColor = new StyleColor(RootBg);   // 피그마 루트 fill — 결과 모달은 불투명(게임 화면 가림)
            _content.style.width = WinW;
            _content.style.height = WinH;
            _content.style.position = Position.Relative;
            _content.Add(root);

            BuildLeftPanel(root);
            BuildRightPanel(root);
            BuildHelpBar(root);

            RegisterCallback<AttachToPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.RegisterCallback<KeyDownEvent>(OnRootKeyDown));
            RegisterCallback<DetachFromPanelEvent>(_ => UIToolkitBootstrap.UIRoot?.UnregisterCallback<KeyDownEvent>(OnRootKeyDown));
            ApplyUIToolkitFont(this);
            DisableWindowShadow();   // 풀캔버스 결과창 — 창 외곽 섀도우 없음(Figma 프레임 drop shadow 없음 계약)
        }

        // ═══════════════ 자가부트 / 개방 API ═══════════════

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => EnsureAttached();

        private static void EnsureAttached()
        {
            var root = UIToolkitBootstrap.UIRoot;
            if (_instance != null && _instance.parent == null)
            {
                _instance = null;   // 고아 인스턴스 폐기 — 재구축(ArenaWindowUTK EnsureAttached 선례)
            }
            if (root == null) return;
            if (_instance == null) _instance = new ArenaResultWindowUTK();
            if (_instance.parent == null) root.Add(_instance);
            if (!_hooked)
            {
                ArenaBettingSystem.OnResultReady += HandleResultReady;
                _hooked = true;
            }
        }

        private static void HandleResultReady(ArenaResultPayload result) => Open(result);

        /// <summary>결과 창 개방 — K6 로비 복귀 완료 신호(ArenaBettingSystem.OnResultReady)에서 호출.
        /// [125차 후속31 M3] 전역 try-catch — UI 결함이 게임 코루틴을 죽이는 구조 폐기(후속30 NRE 실측 교훈).
        ///   실패 시 예외 흡수+로그만 남기고 ArenaBettingSystem 폴백 감시(12s 강제 복귀)에 위임.</summary>
        public static void Open(ArenaResultPayload result)
        {
            if (result == null) return;
            var root = UIToolkitBootstrap.UIRoot;
            if (root == null)
            {
                Debug.LogWarning($"[ArenaResultUTK] 결과 창 개방 실패 — UIRoot null (IsReady={UIToolkitBootstrap.IsReady}) — 12s 폴백 복귀 대기 [125차 후속31 M2]");
                UTKToastService.Show("결과를 표시할 수 없습니다 — 잠시 후 로비로 돌아갑니다.", 2.5f);
                return;
            }
            EnsureAttached();
            try
            {
                _instance.Bind(result);
                if (_instance.IsOpen) _instance.RefreshAll();
                else _instance.Show();
                ArenaBettingSystem.MarkResultShown();   // [125차 후속31 M2] 폴백 감시 해제 표식
                Debug.Log($"[ArenaResultUTK] 결과 창 개방 성공 — {(_instance._won ? "승리" : "패배")} / 승자 {_instance._payload.winnerName} / 정산 {_instance._payload.payout:N0} G / 잔액 {_instance._payload.finalGold:N0} G");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[ArenaResultUTK] 결과 창 개방 중 예외 흡수 — 폴백 복귀 대기: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                UTKToastService.Show("결과 표시에 문제가 발생했습니다 — 잠시 후 로비로 돌아갑니다.", 2.5f);
            }
        }

        // ═══════════════ 바인딩/갱신 ═══════════════

        private void Bind(ArenaResultPayload result)
        {
            _payload = result;
            _won = result.playerWon;
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (_payload == null) return;
            Color accent = _won ? WinAccent : LoseAccent;
            bool winnerIsWest = _payload.winnerIsWest;

            // 좌판 — 승/패 선수(승자 카드 기준, 상대=패자). 패널 제목은 BuildPanelTitle 고정 문구
            var tex = ArenaWindowUTK.GetFighterPortrait(winnerIsWest);
            if (tex != null) _photoCard.style.backgroundImage = new StyleBackground(tex);
            _photoCard.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            _photoCard.style.borderTopColor = _photoCard.style.borderBottomColor =
                _photoCard.style.borderLeftColor = _photoCard.style.borderRightColor = new StyleColor(accent);   // [144차 P6-RES P1] 사진테두리 액센트 1.5
            SetBadge(_winBadge, _won ? "승리" : "패배", _won ? WinBadge : LoseBadge, accent);
            _fighterName.text = _payload.winnerName;
            _fighterGuide.style.color = new StyleColor(accent);   // [144차 P6-RES #1]
            _fighterLevel.text = "Lv.—";   // [144차 P6-RES #2] ArenaResultPayload/ArenaBetFighter에 레벨 필드 없음 — 피그마 더미 Lv.26 사용 금지
            _fighterOrigin.text = _payload.winner != null ? _payload.winner.origin : "";
            _fighterRecord.text = _payload.winner != null ? _payload.winner.RecordText : "";
            SetBadge(_recordState, _won ? "승리 +1" : "패배 +1", Alpha(accent, 0.078f), accent);   // [144차 P6-RES P1] 배지 bg 액센트@0.078·문구 "+1"·문구색 액센트
            _oppName.text = _payload.loserName;
            _oppRecord.text = _payload.loser != null ? _payload.loser.RecordText : "";
            _oppResult.text = _won ? "패배" : "승리";   // [144차 P6-RES #5] 상대 카드 결과 — 플레이어 승/패 반전

            // 우판 — 전투 결과+정산
            _mainTitle.text = "전투 결과";
            _mainSub.text = _won ? "2인 난투 · 승리 베팅 정산" : "2인 난투 · 패배 베팅 정산";
            _resultMsgTitle.text = _won ? "전투에서 승리했습니다!" : "전투에서 패배했습니다";   // [144차 P6-RES P1] JSON 원문
            _resultMsgSub.text = _won ? $"배당률 ×{_payload.odds:0.00} 적중으로 정산이 완료되었습니다." : "베팅 원금이 정산되었습니다.";
            _goldValue.text = _won ? $"+{_payload.payout:N0} G" : "+0 G";
            _goldValue.style.color = new StyleColor(_won ? WinAccent : LoseAccent);   // [144차 P6-RES P1] fs56 mono — 승 #58A6FF / 패 #F07878
            _goldSub.text = _won ? $"배당 ×{_payload.odds:0.00} 적중 — 보유 골드 자동 지급" : "베팅 원금 반환 없음";
            _goldLabel.text = _won ? "획득 골드 · 원금 포함" : "최종 수령 골드";   // [144차 P6-RES #6] 패 문구 = lose JSON 118행
            _goldLabel.style.color = new StyleColor(accent);
            SetBadge(_stateBadge, _won ? "베팅 성공" : "베팅 실패", Alpha(accent, 0.078f), accent);   // [144차 P6-RES P1] 상태배지 (861,39) — 문구/α RefreshAll 갱신
            SetBadge(_goldStateBadge, _won ? "지급 완료" : "정산 확인", accent, DarkInk);
            if (_markBox != null)
            {
                _markBox.style.backgroundColor = new StyleColor(Alpha(accent, 0.078f));   // [144차 P6-RES P1] 마크 α0.078 + stroke@0.251
                _markBox.style.borderTopColor = _markBox.style.borderBottomColor =
                    _markBox.style.borderLeftColor = _markBox.style.borderRightColor = new StyleColor(Alpha(accent, 0.251f));
                if (_markGlyph != null) _markGlyph.style.color = new StyleColor(accent);
            }
            if (_goldBox != null)
            {
                _goldBox.style.backgroundColor = new StyleColor(_won ? WinGoldBox : LoseGoldBox);   // [144차 P6-RES P1] 골드박스 배경 판본 분기 + stroke 액센트@0.251
                _goldBox.style.borderTopColor = _goldBox.style.borderBottomColor =
                    _goldBox.style.borderLeftColor = _goldBox.style.borderRightColor = new StyleColor(Alpha(accent, 0.251f));
            }
            if (_markGlyph != null) _markGlyph.text = _won ? "✓" : "✕";

            // 배당금 계산(승: 베팅×배당=수령 / 패: 베팅·배당·수령 0)
            _calcA.text = $"{_payload.betAmount:N0} G";
            _calcB.text = $"×{_payload.odds:0.00}";
            _calcC.text = _won ? $"{_payload.payout:N0} G" : "0 G";
            bool winStyle = _won;
            _capA.text = "베팅 금액";
            _capB.text = _won ? "적용 배율" : "베팅 배율";   // [144차 P6-RES P1] 칩 캡션 승패 분기(JSON)
            _capC.text = _won ? "최종 수령액 · 원금 포함" : "최종 수령액 · 패배로 지급 없음";
            _rowCCap.text = _won ? "승리 배당금 지급 · 원금 포함" : "패배 정산 · 반환 및 배당금 없음";   // [144차 P6-RES P1] 내행 캡션 승패 분기(JSON)
            _sepA.text = winStyle ? "×" : "";
            _sepB.text = winStyle ? "=" : "";
            var dsp = winStyle ? DisplayStyle.None : DisplayStyle.Flex;   // [144차 P6-RES P1] 패배판 구분선 1×40 ×2(승리판은 ×/= 텍스트 유지)
            _sepLineA.style.display = dsp;
            _sepLineB.style.display = dsp;

            // 골드 정산 내역 — 실제 PlayerStats 정산 흐름 1:1(베팅 차감 → 배당 정산 → 순 변동 → 최종)
            _rowA.text = $"{_payload.betAmount:N0} G";
            _rowB.text = _won ? $"+{_payload.payout:N0} G" : "+0 G";
            int net = _payload.payout - _payload.betAmount;
            _rowC.text = $"{(net >= 0 ? "+" : "")}{net:N0} G";
            _netLine.text = _won ? $"순이익 +{net:N0} G" : $"순손실 −{System.Math.Abs(net):N0} G";   // [144차 P6-RES #7] payload 실데이터(payout − betAmount)
            _finalGold.text = $"{_payload.finalGold:N0} G";

            _summaryLabel.text = _won ? "승리 보상이 지급되었습니다" : $"베팅 금액 {_payload.betAmount:N0} G를 잃었습니다";   // [144차 P6-RES #8] win/lose JSON 원문
            _summarySub.text = _won ? "획득한 골드는 즉시 사용할 수 있습니다." : "반환되는 원금과 지급되는 배당금이 없습니다.";   // [144차 P6-RES #8]
            _footNote.text = _won
                ? "배당금에는 베팅 원금이 포함됩니다. 추가 수령 절차 없이 정산이 완료되었습니다."
                : "패배 시 베팅 원금은 반환되지 않습니다. 추가 차감 없이 정산이 완료되었습니다.";
            _helpLeft.text = _won
                ? "승리 배당금이 보유 골드에 자동으로 지급되었습니다."
                : "베팅에 실패하여 지급되는 골드가 없습니다. 정산이 완료되었습니다.";

            // Accent 변형 — 확인 버튼/문장 배지/상태 배지 (사용자 Figma 수정: 버튼 문구 "확인")
            _confirmButton.style.backgroundColor = new StyleColor(accent);
            _resultMsgTitle.style.color = new StyleColor(accent);
        }

        private static void SetBadge(VisualElement badge, string text, Color bg, Color fg)
        {
            if (badge == null) return;
            badge.style.backgroundColor = new StyleColor(bg);
            var label = badge.Q<Label>();
            if (label != null) { label.text = text; label.style.color = new StyleColor(fg); }
        }

        // ═══════════════ 레이아웃 빌드 (피그마 rel 좌표) ═══════════════

        private Label MakeLabel(VisualElement parent, string text, float size, Color color,
            float x = 0f, float y = 0f, float w = 0f, TextAnchor align = TextAnchor.UpperLeft)
        {
            var l = new Label(text);
            l.style.fontSize = size;
            l.style.color = new StyleColor(color);
            if (w > 0f) l.style.width = w;
            if (x > 0f || y > 0f)
            {
                l.style.position = Position.Absolute;
                l.style.left = x; l.style.top = y;
            }
            l.style.unityTextAlign = align;
            l.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(l);
            return l;
        }

        private VisualElement MakePanel(VisualElement parent, string name, float x, float y, float w, float h, Color bg)
        {
            var p = new VisualElement { name = name };
            p.style.position = Position.Absolute;
            p.style.left = x; p.style.top = y;
            p.style.width = w; p.style.height = h;
            p.style.backgroundColor = new StyleColor(bg);
            p.style.borderTopWidth = p.style.borderBottomWidth = p.style.borderLeftWidth = p.style.borderRightWidth = 1f;   // [144차 P6-RES P1] 판 테두리 폭 1.0 (#30363D@0.502)
            p.style.borderTopColor = p.style.borderBottomColor = p.style.borderLeftColor = p.style.borderRightColor = new StyleColor(Alpha(Stroke, 0.502f));   // [144차 P6-RES P1] 판 테두리 #30363D@0.502 폭1.0
            p.style.borderTopLeftRadius = p.style.borderTopRightRadius = p.style.borderBottomLeftRadius = p.style.borderBottomRightRadius = 14f;
            parent.Add(p);
            return p;
        }

        private void BuildPanelTitle(VisualElement panel, string main, string sub, float w, bool withClose)
        {
            // 제목 아이콘 — 액센트 바(피그마 벡터 아이콘 자리)
            var icon = new VisualElement { name = "TitleIcon" };
            icon.style.position = Position.Absolute;
            icon.style.left = 24f; icon.style.top = 30f;
            icon.style.width = 4f; icon.style.height = 24f;
            icon.style.backgroundColor = new StyleColor(WinAccent);
            icon.style.borderTopLeftRadius = icon.style.borderBottomLeftRadius = 2f;
            icon.style.borderTopRightRadius = icon.style.borderBottomRightRadius = 2f;
            panel.Add(icon);
            // [125차 후속31 M3] 제목 라벨 참조 저장 — BuildPanelTitle이 라벨을 폐기해 RefreshAll NRE(후속30 실측)였던 근본 수리.
            //   우판 보조 제목은 승/패 문구 분기("승리 베팅 정산"/"패배 베팅 정산")라 참조 필수. 좌판은 고정 문구(마지막 호출=우판이 필드 점유).
            _mainTitle = MakeLabel(panel, main, 22f, TextMain, 59f, 24f);   // [144차 P6-RES P1] 제목 fs22 y24
            _mainTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _mainSub = MakeLabel(panel, sub, 11f, TextSub, 157f, 24f, 299f);   // [144차 P6-RES P1] 보조제목 fs11 y24 w299
            if (withClose)
            {
                _closeButton = new Button(Hide) { name = "CloseBtn", text = "✕" };
                _closeButton.style.position = Position.Absolute;
                _closeButton.style.left = w - 54f; _closeButton.style.top = 24f;
                _closeButton.style.width = 30f; _closeButton.style.height = 30f;
                _closeButton.style.backgroundColor = new StyleColor(CardBg);
                _closeButton.style.color = new StyleColor(TextMain);
                _closeButton.style.fontSize = 12f;
                panel.Add(_closeButton);
            }
        }

        private void BuildLeftPanel(VisualElement root)
        {
            var panel = MakePanel(root, "ResultFighterPanel", 64f, 36f, 480f, 882f, PanelBg);
            BuildPanelTitle(panel, "경기 결과", "2인 난투 · 경기 종료", 480f, withClose: false);

            // 선수 사진 432×514 @(24,90) — 병사 전신 초상화(ArenaWindowUTK 렌더 재사용) + 음영
            _photoCard = new VisualElement { name = "FighterPhoto" };
            _photoCard.style.position = Position.Absolute;
            _photoCard.style.left = 24f; _photoCard.style.top = 90f;
            _photoCard.style.width = 432f; _photoCard.style.height = 514f;
            _photoCard.style.borderTopWidth = _photoCard.style.borderBottomWidth =
                _photoCard.style.borderLeftWidth = _photoCard.style.borderRightWidth = 1.5f;   // [144차 P6-RES P1] 사진테두리 액센트 1.5
            _photoCard.style.borderTopColor = _photoCard.style.borderBottomColor =
                _photoCard.style.borderLeftColor = _photoCard.style.borderRightColor = new StyleColor(Stroke);
            _photoCard.style.borderTopLeftRadius = _photoCard.style.borderTopRightRadius =
                _photoCard.style.borderBottomLeftRadius = _photoCard.style.borderBottomRightRadius = 10f;
            _photoCard.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            panel.Add(_photoCard);
            var shade = new VisualElement { name = "PhotoShade" };
            shade.style.position = Position.Absolute;
            shade.style.left = 0f; shade.style.top = 0f;
            shade.style.width = 432f; shade.style.height = 514f;
            shade.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.30f));
            _photoCard.Add(shade);

            // 승/패 표시 배지 69×29 — 사진 좌상단(피그마 rel (16,16) — 사진 카드 기준)
            _winBadge = MakeBadge(_photoCard, "", 16f, 16f, 69f, 29f);
            // 선수 신원 — 사진 하단: 가이드(20,415) → 이름(20,437) + 레벨칩(우측) → 출신/특성 슬롯(20,480) [144차 P6-RES #1#2#3]
            _fighterGuide = MakeLabel(_photoCard, "베팅한 선수", 11f, WinAccent, 20f, 415f, 392f);   // [144차 P6-RES #1] fs11 액센트 — RefreshAll에서 승/패 액센트 재적용
            _fighterName = MakeLabel(_photoCard, "", 18f, TextMain, 20f, 437f, 392f);   // 가이드 신설로 y 415→437
            _fighterLevel = MakeLabel(_photoCard, "", 12f, TextSub, 148f, 444f, 120f);   // [144차 P6-RES #2] fs12 Geist Mono — 이름 행 우측(피그마 x148); 전역 단일 폰트 정책(UIFont) 적용 대상
            _fighterOrigin = MakeLabel(_photoCard, "", 11f, TextSub, 20f, 480f, 392f);   // [144차 P6-RES #3] 전투 특성 슬롯(20,480) — ArenaBetFighter에 특성 필드 부재 → 기존 출신 표기 유지

            // 승/패 기록 432×88 @(24,622)
            var rec = MakePanel(panel, "FighterRecord", 24f, 622f, 432f, 88f, CardBg);
            rec.style.borderTopLeftRadius = rec.style.borderTopRightRadius = rec.style.borderBottomLeftRadius = rec.style.borderBottomRightRadius = 10f;
            MakeLabel(rec, "통산 전적", 11f, TextSub, 16f, 23f);   // [144차 P6-RES P1] 캡션 (16,23)
            _fighterRecord = new Label("");
            _fighterRecord.style.fontSize = 18f;   // [144차 P6-RES P1] 전적 값 fs18
            _fighterRecord.style.color = new StyleColor(TextMain);
            _fighterRecord.style.position = Position.Absolute;
            _fighterRecord.style.left = 16f; _fighterRecord.style.top = 43f;   // [144차 P6-RES P1] (16,43)
            rec.Add(_fighterRecord);
            _recordState = MakeBadge(rec, "", 364f, 33f, 52f, 21f);
            var rsLbl = _recordState.Q<Label>();
            if (rsLbl != null) rsLbl.style.fontSize = 11f;   // [144차 P6-RES P1] 상태배지 fs11

            // 상대 선수 432×118 @(24,728)
            var oppCap = MakeLabel(panel, "상대 선수", 12f, TextMain, 24f, 728f);   // [144차 P6-RES P1] fs12 700
            oppCap.style.unityFontStyleAndWeight = FontStyle.Bold;
            var opp = MakePanel(panel, "OpponentCard", 24f, 752f, 432f, 94f, CardBg);
            opp.style.borderTopLeftRadius = opp.style.borderTopRightRadius = opp.style.borderBottomLeftRadius = opp.style.borderBottomRightRadius = 10f;
            _oppName = MakeLabel(opp, "", 14f, TextMain, 76f, 29f, 300f);   // 썸네일 회피 — 피그마 (76,29)
            _oppRecord = MakeLabel(opp, "", 11f, TextSub, 76f, 51f, 300f);
            // 상대 썸네일 프레임 52×68 r6 — 렌더 데이터(패자 초상) 없음 → 빈프레임 #161B22@0.4 [144차 P6-RES #4]
            _oppThumb = new VisualElement { name = "OppThumb" };
            _oppThumb.style.position = Position.Absolute;
            _oppThumb.style.left = 12f; _oppThumb.style.top = 13f;
            _oppThumb.style.width = 52f; _oppThumb.style.height = 68f;
            _oppThumb.style.backgroundColor = new StyleColor(new Color(0x16 / 255f, 0x1B / 255f, 0x22 / 255f, 0.4f));
            _oppThumb.style.borderTopLeftRadius = _oppThumb.style.borderTopRightRadius =
                _oppThumb.style.borderBottomLeftRadius = _oppThumb.style.borderBottomRightRadius = 6f;
            opp.Add(_oppThumb);
            _oppResult = MakeLabel(opp, "", 12f, TextSub, 397f, 40f, 60f);   // [144차 P6-RES #5] 상대 결과 텍스트 fs12
        }

        private VisualElement MakeBadge(VisualElement parent, string text, float x, float y, float w, float h)
        {
            var b = new VisualElement { name = "Badge_" + text };
            b.style.position = Position.Absolute;
            b.style.left = x; b.style.top = y;
            b.style.width = w; b.style.height = h;
            b.style.borderTopLeftRadius = b.style.borderTopRightRadius = b.style.borderBottomLeftRadius = b.style.borderBottomRightRadius = 6f;
            var l = new Label(text);
            l.style.fontSize = Mathf.Max(10f, h * 0.42f);
            l.style.color = new StyleColor(TextMain);
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.width = w; l.style.height = h;
            b.Add(l);
            parent.Add(b);
            return b;
        }

        private void BuildRightPanel(VisualElement root)
        {
            var panel = MakePanel(root, "ResultSettlePanel", 568f, 36f, 968f, 882f, PanelBg);
            BuildPanelTitle(panel, "전투 결과", "2인 난투 · 승리 베팅 정산", 968f, withClose: true);

            // 승/패 안내 920×100 @(24,86)
            var banner = new VisualElement { name = "ResultBanner" };
            banner.style.position = Position.Absolute;
            banner.style.left = 24f; banner.style.top = 86f;
            banner.style.width = 920f; banner.style.height = 100f;
            panel.Add(banner);
            var mark = new VisualElement { name = "ResultMark" };
            mark.style.position = Position.Absolute;
            mark.style.left = 0f; mark.style.top = 12f;
            mark.style.width = 76f; mark.style.height = 76f;
            mark.style.backgroundColor = new StyleColor(Alpha(WinAccent, 0.078f));   // [144차 P6-RES P1] 마크 fill 액센트@0.078
            mark.style.borderTopWidth = mark.style.borderBottomWidth = mark.style.borderLeftWidth = mark.style.borderRightWidth = 1f;
            mark.style.borderTopColor = mark.style.borderBottomColor = mark.style.borderLeftColor = mark.style.borderRightColor = new StyleColor(Alpha(WinAccent, 0.251f));   // [144차 P6-RES P1] stroke@0.251
            mark.style.borderTopLeftRadius = mark.style.borderTopRightRadius = mark.style.borderBottomLeftRadius = mark.style.borderBottomRightRadius = 10f;   // [144차 P6-RES P1] r10
            banner.Add(mark);
            var markGlyph = new Label("✓");
            markGlyph.style.fontSize = 34f;
            markGlyph.style.color = new StyleColor(WinAccent);   // [144차 P6-RES P1] α배경 위 액센트 글리프
            markGlyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            markGlyph.style.width = 76f; markGlyph.style.height = 76f;
            mark.Add(markGlyph);
            _markGlyph = markGlyph;
            _markBox = mark;   // [144차 P6-RES P1] 마크 색상 갱신 참조
            _resultMsgTitle = MakeLabel(banner, "", 34f, WinAccent, 96f, 17f, 745f);   // [144차 P6-RES P1] 메시지 제목 fs34
            _resultMsgTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _resultMsgSub = MakeLabel(banner, "", 13f, TextSub, 96f, 67f, 745f);   // [144차 P6-RES P1] 설명 fs13 y67
            _stateBadge = MakeBadge(banner, "", 861f, 39f, 59f, 21f);   // [144차 P6-RES P1] 상태배지 (861,39) 59×21

            // 획득(수령) 골드 920×158 @(24,200)
            var goldBox = MakePanel(panel, "GoldBox", 24f, 200f, 920f, 158f, WinGoldBox);
            _goldBox = goldBox;   // [144차 P6-RES P1] 판본 배경/액센트 테두리 갱신 참조
            goldBox.style.borderTopLeftRadius = goldBox.style.borderTopRightRadius = goldBox.style.borderBottomLeftRadius = goldBox.style.borderBottomRightRadius = 10f;   // [144차 P6-RES P1] r10
            var coin = new VisualElement { name = "GoldCoin" };
            coin.style.position = Position.Absolute;
            coin.style.left = 24f; coin.style.top = 43f;
            coin.style.width = 72f; coin.style.height = 72f;
            coin.style.backgroundColor = new StyleColor(Alpha(GoldColor, 0.078f));   // [144차 P6-RES P1] 코인 fill α0.078
            coin.style.borderTopLeftRadius = coin.style.borderTopRightRadius = coin.style.borderBottomLeftRadius = coin.style.borderBottomRightRadius = 36f;
            goldBox.Add(coin);
            var coinGlyph = new Label("G");
            coinGlyph.style.fontSize = 30f;
            coinGlyph.style.color = new StyleColor(GoldColor);   // [144차 P6-RES P1] α배경 위 골드 글리프
            coinGlyph.style.unityTextAlign = TextAnchor.MiddleCenter;
            coinGlyph.style.width = 72f; coinGlyph.style.height = 72f;
            coin.Add(coinGlyph);
            _goldLabel = MakeLabel(goldBox, "", 13f, WinAccent, 116f, 37f, 200f);   // [144차 P6-RES #6] 항목 라벨 fs13 액센트 — RefreshAll에서 승/패 액센트 재적용
            _goldValue = MakeLabel(goldBox, "", 56f, WinAccent, 116f, 57f, 640f);   // [144차 P6-RES P1] 금액 fs56 mono (116,57)
            _goldSub = MakeLabel(goldBox, "", 11f, TextSub, 116f, 100f, 640f);   // 금액(fs30) 회피 — y 84→100
            _goldStateBadge = MakeBadge(goldBox, "", 826f, 57f, 94f, 21f);   // [144차 P6-RES #7] x826 재배치 — 피그마 지급상태 블록(94×43)을 '지급 완료' 필(상단) + 순이익 라인(하단) 분리 구현
            var gsBadgeLbl = _goldStateBadge.Q<Label>();
            if (gsBadgeLbl != null) gsBadgeLbl.style.fontSize = 12f;
            _netLine = MakeLabel(goldBox, "", 12f, TextSub, 826f, 80f, 120f);   // [144차 P6-RES #7] 순이익/순손실 라인 fs12 (826,80)

            // 배당금 계산 920×92 @(24,372) — 승: A×B=C / 패: A·B·C(구분자 ·)
            var calc = MakePanel(panel, "OddsCalc", 24f, 372f, 920f, 92f, CardBg);
            calc.style.borderTopLeftRadius = calc.style.borderTopRightRadius = calc.style.borderBottomLeftRadius = calc.style.borderBottomRightRadius = 10f;
            _calcA = MakeCalcChip(calc, "베팅 금액", 16f, 21f, out _capA);   // [144차 P6-RES P1] 정산칩 x16/333/649 무배경
            _sepA = MakeLabel(calc, "×", 22f, TextSub, 295f, 31f, 30f);   // [144차 P6-RES P1] 구분자 fs22 x295
            _calcB = MakeCalcChip(calc, "적용 배율", 333f, 21f, out _capB);
            _sepB = MakeLabel(calc, "=", 22f, TextSub, 611f, 31f, 30f);   // [144차 P6-RES P1] 구분자 fs22 x611
            _calcC = MakeCalcChip(calc, "최종 수령액 · 원금 포함", 649f, 21f, out _capC);
            _sepLineA = MakeLoseDivider(calc, 295f);   // [144차 P6-RES P1] 패배판 구분선 1×40 ×2
            _sepLineB = MakeLoseDivider(calc, 611f);

            // 골드 정산 내역 920×175 @(24,478)
            MakeLabel(panel, "골드 정산 내역", 15f, TextMain, 24f, 485f);   // [144차 P6-RES P1] 내역제목 fs15
            _rowA = MakeSettleRow(panel, "베팅 차감", 515f, out _);   // [144차 P6-RES P1] 내행 캡션 fs12(정적 캡션 — 승패 분기는 rowC만)
            _rowB = MakeSettleRow(panel, "배당 정산", 545f, out _);
            _rowC = MakeSettleRow(panel, "순 변동", 575f, out _rowCCap);   // [144차 P6-RES P1] rowC 캡션 승패 분기 참조
            var finalRow = new VisualElement { name = "FinalGoldRow" };
            finalRow.style.position = Position.Absolute;
            finalRow.style.left = 24f; finalRow.style.top = 605f;
            finalRow.style.width = 920f; finalRow.style.height = 48f;
            // [144차 P6-RES P1] 최종행 무필 — stroke #30363D@0.502
            finalRow.style.borderTopWidth = finalRow.style.borderBottomWidth = finalRow.style.borderLeftWidth = finalRow.style.borderRightWidth = 1f;
            finalRow.style.borderTopColor = finalRow.style.borderBottomColor = finalRow.style.borderLeftColor = finalRow.style.borderRightColor = new StyleColor(Stroke);
            finalRow.style.borderTopLeftRadius = finalRow.style.borderTopRightRadius = finalRow.style.borderBottomLeftRadius = finalRow.style.borderBottomRightRadius = 10f;
            panel.Add(finalRow);
            var finalCap = MakeLabel(finalRow, "현재 보유 골드", 13f, TextSub, 0f, 16f);   // [144차 P6-RES P1] fs13 700 (0,16) — JSON 원문
            finalCap.style.unityFontStyleAndWeight = FontStyle.Bold;
            _finalGold = MakeLabel(finalRow, "", 25f, FinalGoldColor, 680f, 8f, 240f, TextAnchor.UpperRight);   // [144차 P6-RES P1] fs25 #F0F6FC right=920

            // 결과 확인 920×76 @(24,750) — [125차 후속30 재실측] 버튼 문구 "확인"(사용자 Figma 수정)
            var confirm = MakePanel(panel, "ResultConfirm", 24f, 750f, 920f, 76f, CardBg);
            confirm.style.borderTopLeftRadius = confirm.style.borderTopRightRadius = confirm.style.borderBottomLeftRadius = confirm.style.borderBottomRightRadius = 10f;   // [144차 P6-RES P1] r10
            _summaryLabel = MakeLabel(confirm, "", 13f, TextMain, 16f, 21f, 628f);   // [144차 P6-RES #8] 정산 요약 1행 fs13 700
            _summaryLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _summarySub = MakeLabel(confirm, "", 11f, TextSub, 16f, 43f, 628f);   // [144차 P6-RES #8] 정산 요약 2행 fs11
            _confirmButton = new Button(ConfirmResult) { name = "ConfirmBtn", text = "확인" };   // [125차 후속31 M2] 확인=창 닫기+로비 복귀
            _confirmButton.style.position = Position.Absolute;
            _confirmButton.style.left = 660f; _confirmButton.style.top = 16f;   // [144차 P6-RES P1] 확인버튼 x660
            _confirmButton.style.width = 244f; _confirmButton.style.height = 44f;
            _confirmButton.style.color = new StyleColor(DarkInk);
            _confirmButton.style.fontSize = 13f;   // [144차 P6-RES P1] fs13 700
            _confirmButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            confirm.Add(_confirmButton);

            // 지급/정산 안내 920×18 @(24,840) — 피그마 원문
            _footNote = MakeLabel(panel, "", 11f, TextSub, 24f, 840f, 920f);
        }

        private Label MakeCalcChip(VisualElement parent, string caption, float x, float y, out Label captionLabel)
        {
            var chip = new VisualElement { name = "CalcChip_" + caption };
            chip.style.position = Position.Absolute;
            chip.style.left = x; chip.style.top = y;
            chip.style.width = 254f; chip.style.height = 50f;
            // [144차 P6-RES P1] 정산칩 무배경 — 피그마 계산행 필 없음
            chip.style.borderTopLeftRadius = chip.style.borderTopRightRadius = chip.style.borderBottomLeftRadius = chip.style.borderBottomRightRadius = 8f;
            parent.Add(chip);
            captionLabel = MakeLabel(chip, caption, 11f, TextSub, 12f, 7f, 230f);   // [144차 P6-RES P1] 칩 캡션 fs11
            var val = MakeLabel(chip, "", 22f, TextMain, 12f, 24f, 230f);   // [144차 P6-RES P1] 칩 값 fs22
            return val;
        }

        private VisualElement MakeLoseDivider(VisualElement parent, float x)
        {
            var d = new VisualElement { name = "LoseDivider" };
            d.style.position = Position.Absolute;
            d.style.left = x; d.style.top = 26f;
            d.style.width = 1f; d.style.height = 40f;
            d.style.backgroundColor = new StyleColor(Alpha(Stroke, 0.502f));   // [144차 P6-RES P1] 패배판 구분선 1×40 #30363D@0.502
            parent.Add(d);
            return d;
        }

        private Label MakeSettleRow(VisualElement parent, string caption, float y, out Label captionLabel)
        {
            var row = new VisualElement { name = "SettleRow_" + caption };
            row.style.position = Position.Absolute;
            row.style.left = 24f; row.style.top = y;
            row.style.width = 920f; row.style.height = 18f;
            parent.Add(row);
            captionLabel = MakeLabel(row, caption, 12f, TextSub, 0f, 0f, 400f);   // [144차 P6-RES P1] 내행 캡션 fs12
            var val = MakeLabel(row, "", 14f, TextMain, 720f, 0f, 184f, TextAnchor.UpperRight);   // [144차 P6-RES P1] 내행 금액 fs14 mono right=920(단일 폰트 정책 대상)
            return val;
        }

        private void BuildHelpBar(VisualElement root)
        {
            var help = new VisualElement { name = "HelpBar" };
            help.style.position = Position.Absolute;
            help.style.left = 64f; help.style.top = 942f;
            help.style.width = 1472f; help.style.height = 20f;
            // [144차 P6-RES P1] 도움말바 무배경 — 피그마 필 없음
            root.Add(help);
            _helpLeft = MakeLabel(help, "", 12f, TextSub, 24f, 3f, 400f);   // [144차 P6-RES P1] 도움말 fs12
            MakeLabel(help, "Enter  확인     ·     Esc  닫기", 12f, TextSub, 1334f, 3f, 138f, TextAnchor.UpperRight);   // [144차 P6-RES P1] 우측 x1334 w138
        }

        // ═══════════════ 입력/피팅 ═══════════════

        /// <summary>[125차 후속31 M2] 결과 확인 — 창 닫기+로비 복귀(ArenaBettingSystem.ReturnToLobbyAfterResult).
        ///   확인 버튼/Enter/Esc 공용 경로 — 확인 입력 전에는 투기장에 머문다(자동 복귀 폐기 계약).</summary>
        private void ConfirmResult()
        {
            Hide();
            ArenaBettingSystem.ReturnToLobbyAfterResult();
        }

        private void OnRootKeyDown(KeyDownEvent evt)
        {
            if (!IsOpen) return;
            if (evt.keyCode == KeyCode.Escape) { ConfirmResult(); evt.StopPropagation(); }
            else if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) { ConfirmResult(); evt.StopPropagation(); }
        }

        protected override void OnWindowOpen()
        {
            RefreshAll();
            schedule.Execute(ApplyCanvasFit).StartingIn(16);
            schedule.Execute(ApplyCanvasFit).StartingIn(120);
            schedule.Execute(ApplyCanvasFit).StartingIn(400);
        }

        /// <summary>ApplyCanvasFit — ArenaWindowUTK 동일 등배수 계약(raw 1600×1040 × k, 좌상단 원점 고정).</summary>
        private void ApplyCanvasFit()
        {
            var rootEl = UIToolkitBootstrap.UIRoot;
            if (rootEl == null) return;
            float rootW = rootEl.resolvedStyle.width;
            float rootH = rootEl.resolvedStyle.height;
            if (rootW < 1f || rootH < 1f) return;
            float k = Mathf.Min(rootW / WinW, rootH / WinH);
            style.left = (rootW - WinW * k) * 0.5f;
            style.top = (rootH - WinH * k) * 0.5f;
            style.width = WinW * k;
            style.height = WinH * k;
            _content.style.transformOrigin = new TransformOrigin(0f, 0f, 0f);
            _content.style.scale = new StyleScale(new Scale(new Vector2(k, k)));
        }
    }
}
