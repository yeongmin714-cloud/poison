// U9-W2 (2026-09-19): 콘솔 경고 소음 정리 — Phase 46 애니메이션 마이그레이션 잔여 경고 억제(실수리는 ROADMAP_NEURAL_ANIMATION). 신규 경고는 억제되지 않는다.
#pragma warning disable 114
using ProjectName.Systems;
using ProjectName.UI;   // UIFont 공용 폰트 — Geist Mono 폴백 계약
using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// UI Toolkit Phase U6 Round D1 — 동적 이벤트 팝업 (알림/선택지) UTK 윈도우.
    /// 원본: Assets/Scripts/UI/DynamicEventUI.cs (425줄 IMGUI static) — 원본은 절대 수정하지 않는다.
    ///
    /// [구현]
    ///   ① 이벤트 알림 — WorldEventManager.OnEventStarted 구독 → 팝업 표시.
    ///   ② 이벤트 정보 — ActiveEvent 실측 필드(type/territory/description/RemainingTime) 직접 사용,
    ///      WorldEventManager.GetEventEmoji/GetEventDisplayName 호출.
    ///   ③ 선택지 — [이동하기] AcceptEvent / [무시] IgnoreEvent 실측 호출.
    ///   자동 종료 — 이벤트 비활성(phase != Active)이거나 15초 초과 시 Dismiss (400ms 폴링).
    ///   각 경로에 [DynEventUTK] UnityEngine.Debug 로그.
    /// [진입점] static Ensure() / ShowEvent(evt) / Open() / Toggle() / Close(). 순수 VisualElement 트리.
    ///
    /// [144차 P8-EV] 피그마 정합(진실 JSON docs/figma_truth_144/event_video__246_141.json — 246:141 312×396):
    ///   P0 #1 코너 데칼 4개(246:142-153) / #2 이벤트 유형 배지(246:186) / #3 발생 시각(246:188),
    ///   영상 플레이어 체계 신설 #4-#8(246:160-205 — 프레임/장면/재생 버튼/탐색 막대/볼륨·최대화),
    ///   P1 #9 패널 312×396 / #10 카드 크롬 #13181F + #30363D@0.502 / #11 장면 내부 위치 표기 / #12 제목 fs16.
    ///   ⚠ 영상 소스 자체는 목업 — 실데이터(영상 파일) 부재 → 재생/탐색/볼륨은 정적 표시(위조 금지, 클릭 무기능).
    /// </summary>
    public class DynamicEventUTK : UTKWindowBase
    {
        // ===== 싱글턴 / 팩토리 =====
        private static DynamicEventUTK _instance;
        public static DynamicEventUTK Instance => _instance;

        public static void Ensure()
        {
            if (_instance != null) return;
            _instance = new DynamicEventUTK();
        }

        /// <summary>현재 표시 중인 이벤트 팝업 열기 (원본 DrawEvent 대응).</summary>
        public static void Open()
        {
            Ensure();
            _instance.Show();
        }

        /// <summary>이벤트 알림 팝업 표시 (원본 ShowEvent 대응, WorldEventManager가 호출).</summary>
        public static void ShowEvent(WorldEventManager.ActiveEvent evt)
        {
            if (evt == null)
            {
                Debug.LogWarning("[DynEventUTK] ShowEvent: evt가 null");
                return;
            }
            Ensure();
            _instance._current = evt;
            _instance._showTime = Time.time;
            Debug.Log("[DynEventUTK] 🌍 이벤트 팝업 표시: " + WorldEventManager.GetEventEmoji(evt.type) + " " + WorldEventManager.GetEventDisplayName(evt.type));
            _instance.Show();
        }

        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        public static void Close()
        {
            if (_instance != null)
                _instance.Hide();
        }

        /// <summary>이벤트 구독 (원본 Initialize 대응).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            WorldEventManager.OnEventStarted += ShowEvent;
        }

        // ===== 설정 =====
        // [144차 P8-EV #9] 패널 크기 480×360 → 312×396 (Figma 246:141).
        //   판단: 게임 전용 요소(남은 시간 카드·자동 종료 카운트다운·[이동하기]/[무시] 버튼)를
        //   396 높이에 모두 수용하면 세로 넘침이 발생한다 → 지시대로 396 초과 수용(overflow 넘침 기록).
        private const float WinW = 312f;
        private const float WinH = 396f;
        private const long RefreshMs = 400L;
        private const float AutoDismissSeconds = 15f;

        // ===== 상태 =====
        private WorldEventManager.ActiveEvent _current;
        private float _showTime;

        // ===== 레퍼런스 =====
        private readonly VisualElement _body;
        private IVisualElementScheduledItem _refreshTask;

        private DynamicEventUTK() : base("🌍 월드 이벤트", new Vector2(WinW, WinH))
        {
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingTop = 6f;
            _content.style.paddingBottom = 6f;

            _body = new VisualElement();
            _body.name = "EventBody";
            _body.style.flexGrow = 1f;
            _body.style.flexDirection = FlexDirection.Column;
            _content.Add(_body);

            // [144차 P8-EV #1] 모서리 장식 4개(246:142-153) — 8×8 L자(1×8/8×1 #58A6FF, 굵기 1.0) ×4.
            //   UTKTheme.AddCornerDecals 공용 헬퍼. 진실은 코너 정렬(인셋 0)이나 공용 어휘는
            //   outside(코너 걸침)/inside(+4) 2모드뿐 — 진실이 패널 안쪽이므로 inside 모드 선택(세부 좌표 미조정 계약).
            UTKTheme.AddCornerDecals(this, 8f, 1.0f, "#58A6FF", outsideCorner: false);

            ApplyUIToolkitFont(this);

            style.display = DisplayStyle.None;
            style.left = 720f;
            style.top = 260f;
        }

        // ===== 생명주기 =====
        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            style.left = 720f;
            style.top = 260f;
            StartRefreshLoop();
            Refresh();
        }

        public override void Hide()
        {
            base.Hide();
            StopRefreshLoop();
        }

        private void StartRefreshLoop()
        {
            if (_refreshTask != null) return;
            _refreshTask = schedule.Execute(() =>
            {
                if (!IsOpen) return;
                if (_current == null)
                {
                    HideNow();
                    return;
                }
                // 이벤트가 더 이상 활성이면 종료
                if (_current.phase != WorldEventManager.EventPhase.Active)
                {
                    Debug.Log("[DynEventUTK] 이벤트 상태 변경으로 팝업 종료");
                    HideNow();
                    return;
                }
                // 자동 종료 (15초)
                if (Time.time - _showTime >= AutoDismissSeconds)
                {
                    if (WorldEventManager.Instance != null)
                        WorldEventManager.Instance.IgnoreEvent(_current);
                    Debug.Log("[DynEventUTK] 시간 초과로 이벤트 무시 후 종료");
                    HideNow();
                    return;
                }
                Refresh();
            }).Every(RefreshMs);
        }

        private void StopRefreshLoop()
        {
            if (_refreshTask != null)
            {
                _refreshTask.Pause();
                _refreshTask = null;
            }
        }

        private void HideNow()
        {
            _current = null;
            Close();
        }

        // ===== 렌더링 =====
        private void Refresh()
        {
            _body.Clear();
            if (_current == null) return;

            var evt = _current;

            // 이벤트 제목은 독립된 GitHub-dark 헤더 카드로 강조한다(게임 전용 유지 — 이모지+이름 통합).
            var headerCard = CreateCard();
            headerCard.style.flexDirection = FlexDirection.Row;
            headerCard.style.alignItems = Align.Center;
            headerCard.style.marginBottom = 8f;
            headerCard.style.paddingTop = 10f;
            headerCard.style.paddingBottom = 10f;
            var title = MakeLabel(WorldEventManager.GetEventEmoji(evt.type) + "  " + WorldEventManager.GetEventDisplayName(evt.type), UTKColor.TextPrimary, true);
            title.style.fontSize = 16f;   // [144차 P8-EV #12] fs16.8 → 16 (Figma 246:189 Roboto 700 16 근사)
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerCard.Add(title);
            _body.Add(headerCard);

            // [144차 P8-EV #4~#8] 이벤트 발생 영상 플레이어(246:160-205) 신설.
            //   ⚠ 영상 소스는 목업 — 실데이터(영상 파일) 부재 → 재생/탐색/볼륨 정적 표시(위조 금지).
            _body.Add(BuildEventVideoPlayer(evt));

            // 설명과 이벤트 세부정보는 별도의 보조 패널 카드에 묶는다.
            // [144차 P8-EV #10] 카드 크롬 — fill #13181F + stroke #30363D@0.502 1.0 (CreateCard 참조).
            var detailsCard = CreateCard();
            detailsCard.style.flexGrow = 1f;
            detailsCard.style.marginBottom = 8f;
            detailsCard.style.paddingTop = 12f;
            detailsCard.style.paddingBottom = 12f;
            detailsCard.style.paddingLeft = 12f;
            detailsCard.style.paddingRight = 12f;
            detailsCard.style.flexDirection = FlexDirection.Column;

            // [144차 P8-EV #2/#3] 이벤트 분류 행(246:185 SPACE_BETWEEN) — 유형 배지 + 발생 시각.
            //   유형명은 타이틀(이모지+이름 통합)에서 분리해 실제 이벤트 데이터(evt.type)로 매핑.
            //   발생 시각은 실제 evt.startTime 경과 표기 — 아래 자동 종료 카운트다운(⏰)과 병행 유지.
            var classRow = new VisualElement { name = "EventClassRow" };
            classRow.style.flexDirection = FlexDirection.Row;
            classRow.style.alignItems = Align.Center;
            classRow.style.justifyContent = Justify.SpaceBetween;
            classRow.style.marginBottom = 10f;
            classRow.Add(CreateEventTypeBadge(GetEventTypeBadgeText(evt.type)));
            var occurred = MakeLabel(FormatOccurredTime(evt), UTKColor.TextSecondary, false);
            occurred.style.fontSize = 11f;   // 246:188 Roboto 400 11 #8B949E
            classRow.Add(occurred);
            detailsCard.Add(classRow);

            if (!string.IsNullOrEmpty(evt.description))
            {
                var description = MakeLabel(evt.description, UTKColor.TextSecondary, true);
                description.style.marginBottom = 10f;
                detailsCard.Add(description);
            }

            // [144차 P8-EV #11] 구 '📍 영지: …' 상세 카드 라벨 폐기 → 장면 내부 '📍 {영지명}' 11 #F0F6FC
            //   (BuildEventVideoPlayer 내 246:169-171 — 영지/장소 문자열 실데이터 재사용, territoryId 표기 동반 폐기).

            float remaining = evt.RemainingTime;
            Color timerColor = remaining < 60f ? UTKColor.HealthRed : (remaining < 180f ? UTKColor.BorderGold : Color.white);
            var timerCard = CreateCard();
            timerCard.style.flexDirection = FlexDirection.Row;
            timerCard.style.alignItems = Align.Center;
            timerCard.style.paddingTop = 7f;
            timerCard.style.paddingBottom = 7f;
            timerCard.style.paddingLeft = 9f;
            timerCard.style.paddingRight = 9f;
            timerCard.style.marginBottom = 8f;
            timerCard.style.borderLeftWidth = 2f;
            timerCard.style.borderLeftColor = new StyleColor(timerColor);
            timerCard.Add(MakeLabel("⏱️ " + FormatRemainingTime(remaining), timerColor, false));
            detailsCard.Add(timerCard);

            float autoRemaining = AutoDismissSeconds - (Time.time - _showTime);
            if (autoRemaining > 0f)
                detailsCard.Add(MakeLabel("⏰ " + Mathf.CeilToInt(autoRemaining) + "초 후 자동 종료", UTKColor.TextSecondary, false));

            _body.Add(detailsCard);

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.marginTop = 2f;
            _body.Add(row);

            row.Add(UTKButton.Create("⚔️ 이동하기", OnMoveToEvent, UTKButton.Variant.Primary));
            row.Add(UTKButton.Create("❌ 무시", OnIgnoreEvent, UTKButton.Variant.Danger));
        }

        // [144차 P8-EV #10] 패널 카드 크롬 — fill #13181F(구 #161B22/#21262D 통일) + stroke #30363D@0.502 1.0
        //   (Figma 246:141 프레임 크롬 — UTKTheme.BorderChrome 재사용). r6은 기존 게임 카드 크롬 유지
        //   (진실 프레임 r10은 UTKWindowBase 창 크롬 몫 — 판단 기록).
        private static VisualElement CreateCard()
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0x13 / 255f, 0x18 / 255f, 0x1F / 255f, 1f));   // #13181F
            UTKTheme.ApplyElementBorder(card, UTKTheme.BorderChrome, 1.0f);   // #30363D@0.502 · 1.0 (Figma 246:141)
            card.style.borderTopLeftRadius = 6f;
            card.style.borderTopRightRadius = 6f;
            card.style.borderBottomLeftRadius = 6f;
            card.style.borderBottomRightRadius = 6f;
            card.style.paddingLeft = 12f;
            card.style.paddingRight = 12f;
            return card;
        }

        // ===== 선택지 처리 =====
        private void OnMoveToEvent()
        {
            if (_current == null || WorldEventManager.Instance == null)
            {
                HideNow();
                return;
            }
            Debug.Log("[DynEventUTK] 🚀 플레이어가 " + _current.territoryName + " 이벤트로 이동 (수락)");
            WorldEventManager.Instance.AcceptEvent(_current);
            HideNow();
        }

        private void OnIgnoreEvent()
        {
            if (_current == null || WorldEventManager.Instance == null)
            {
                HideNow();
                return;
            }
            Debug.Log("[DynEventUTK] ❌ 플레이어가 " + _current.territoryName + " 이벤트를 무시");
            WorldEventManager.Instance.IgnoreEvent(_current);
            HideNow();
        }

        // ===== 헬퍼 =====
        private static string FormatRemainingTime(float seconds)
        {
            if (seconds <= 0f)
                return "시간 초과";
            int totalSec = Mathf.CeilToInt(seconds);
            int minutes = totalSec / 60;
            int secs = totalSec % 60;
            return minutes > 0 ? "남은 시간: " + minutes + "분 " + secs.ToString("D2") + "초"
                               : "남은 시간: " + secs + "초";
        }

        private static Label MakeLabel(string text, Color color, bool wrap)
        {
            var l = new Label(text);
            l.style.fontSize = 13f;   // [144차 P8-EV P2] 설명 fs13.2 → 13 근사(기록) — 246:190 Roboto 400 13
            l.style.color = new StyleColor(color);
            if (wrap)
                l.style.whiteSpace = WhiteSpace.Normal;
            return l;
        }

        // ===== [144차 P8-EV] 이벤트 영상 플레이어 체계(246:160-205) 신설 =====

        /// <summary>
        /// [144차 P8-EV #4~#8] 이벤트 발생 영상 플레이어(246:160) — 280×256 r7.2, fill #0B0E14,
        /// stroke #58A6FF@0.251. 장면(246:161 280×214) + 재생 컨트롤(246:172) 구성.
        /// ⚠ 영상 소스는 목업 — 실데이터(영상 파일) 부재 → 재생 버튼/탐색 막대/시간/볼륨 모두 정적 표시
        ///   (위조 금지: 실재생·실시간 연동 없음, pickingMode Ignore로 클릭 무기능).
        /// </summary>
        private static VisualElement BuildEventVideoPlayer(WorldEventManager.ActiveEvent evt)
        {
            // [144차 P8-EV #4] 플레이어 프레임 — 280×256 r7.2, fill #0B0E14, stroke #58A6FF@0.251.
            var player = new VisualElement { name = "EventVideoPlayer" };
            player.style.width = 280f;
            player.style.height = 256f;
            player.style.backgroundColor = new StyleColor(UTKTheme.BgBase);   // #0B0E14
            UTKTheme.ApplyElementBorder(player, new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.251f), 1f);
            UTKTheme.ApplyRadius(player, 7.2f);
            player.style.flexDirection = FlexDirection.Column;
            player.style.alignSelf = Align.Center;   // 창 내폭 중앙 — 진실 pl/pr 16 근사
            player.style.overflow = Overflow.Hidden; // r7.2 코너 클립
            player.style.marginBottom = 12f;         // 진실 프레임 gap 12
            player.pickingMode = PickingMode.Ignore;

            // ── 장면(246:161 280×214) ──
            var scene = new VisualElement { name = "EventScene" };
            scene.style.width = 280f;
            scene.style.height = 214f;
            scene.style.backgroundColor = new StyleColor(UTKTheme.BgBase);   // #0B0E14
            scene.style.overflow = Overflow.Hidden;
            player.Add(scene);

            // [144차 P8-EV #5] 장면 IMAGE(246:162) — 실데이터(영상 파일) 부재 → placeholder fill #0B0E14(위조 금지 기록).
            //   장면 그라데이션(246:163) — LINEAR #0B0E14 0.125@0.0 → 0.0@0.58 → 0.8@1.0.
            //   USS gradient 금지 → 1×64 텍스처 근사(HUDUTK.GetPlateGradientTexture 텍스처 선례).
            var sceneGradient = new VisualElement { name = "SceneGradient" };
            sceneGradient.style.position = Position.Absolute;
            sceneGradient.style.left = 0f;
            sceneGradient.style.top = 0f;
            sceneGradient.style.right = 0f;
            sceneGradient.style.bottom = 0f;
            // [144차 P8-EV #5 보정2] Unity 6000.x: StyleBackground 2인자(texture,scaleMode) 오버로드 부재 —
            //   1인자 Background{texture} 사용. Background 기본 scaleMode=ScaleAndStretch로 전면 커버 근사.
            sceneGradient.style.backgroundImage = new StyleBackground(new Background { texture = GetSceneGradientTexture() });
            sceneGradient.pickingMode = PickingMode.Ignore;
            scene.Add(sceneGradient);

            // 기록 칩(246:164) — 72×20 r4 fill #0B0E14@0.6 · 점 4×4 #58A6FF(246:165) · '이벤트 기록' 10 Medium #F0F6FC(246:166).
            var recChip = new VisualElement { name = "EventRecordChip" };
            recChip.style.position = Position.Absolute;
            recChip.style.left = 10f;
            recChip.style.top = 10f;
            recChip.style.width = 72f;
            recChip.style.height = 20f;
            recChip.style.backgroundColor = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 0.6f));
            UTKTheme.ApplyRadius(recChip, 4f);
            recChip.pickingMode = PickingMode.Ignore;
            var recDot = new VisualElement { name = "RecordDot" };
            recDot.style.width = 4f;
            recDot.style.height = 4f;
            recDot.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // #58A6FF
            UTKTheme.ApplyRadius(recDot, 2f);
            recDot.style.position = Position.Absolute;
            recDot.style.left = 7f;
            recDot.style.top = 8f;
            recDot.pickingMode = PickingMode.Ignore;
            recChip.Add(recDot);
            var recText = new Label("이벤트 기록");
            UTKTheme.StyleFigmaText(recText, 10f, 500, UTKTheme.TextMain);   // Roboto 500 Medium → Normal(<600 매핑 선례)
            recText.style.position = Position.Absolute;
            recText.style.left = 16f;
            recText.style.top = 4f;
            recText.pickingMode = PickingMode.Ignore;
            recChip.Add(recText);
            scene.Add(recChip);

            // [144차 P8-EV #6] 재생 버튼(246:167) — 44×44 r22 장면 중앙, fill #0B0E14@0.6 + stroke #58A6FF.
            //   정적(클릭 무기능 — 영상 소스 부재, 위조 금지).
            var playButton = new VisualElement { name = "EventPlayButton" };
            playButton.style.position = Position.Absolute;
            playButton.style.width = 44f;
            playButton.style.height = 44f;
            playButton.style.left = 118f;   // (280−44)/2
            playButton.style.top = 85f;     // (214−44)/2
            playButton.style.backgroundColor = new StyleColor(new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, 0.6f));
            UTKTheme.ApplyElementBorder(playButton, UTKTheme.Accent, 1f);
            UTKTheme.ApplyRadius(playButton, 22f);
            playButton.pickingMode = PickingMode.Ignore;
            var playGlyph = BuildPlayGlyph(16f, UTKTheme.TextMain);   // 246:168 재생 16×16 #F0F6FC
            playGlyph.style.position = Position.Absolute;
            playGlyph.style.left = 14f;
            playGlyph.style.top = 14f;
            playButton.Add(playGlyph);
            scene.Add(playButton);

            // [144차 P8-EV #11] 장면 위치(246:169-171) — 장면 내부 좌하단 '📍 {영지명}' 11 #F0F6FC.
            //   실제 이벤트 데이터(evt.territoryName) 재사용. JSON 원문은 map-pin 벡터(246:198 12×12
            //   stroke #58A6FF 1.5) — 지시대로 📍 텍스트 프리픽스 근사(기록).
            var scenePlace = new Label("📍 " + evt.territoryName);
            scenePlace.style.fontSize = 11f;   // 246:171 Roboto 400 11
            scenePlace.style.color = new StyleColor(UTKColor.TextPrimary);   // #F0F6FC
            scenePlace.style.position = Position.Absolute;
            scenePlace.style.left = 12f;
            scenePlace.style.bottom = 16f;   // 장면 하단 16px(246:169 ay204, 장면 하단 233)
            scenePlace.pickingMode = PickingMode.Ignore;
            scene.Add(scenePlace);

            // ── 재생 컨트롤(246:172 280×42, pad 10/4/10/8, gap 8) ──
            // [144차 P8-EV #7] JSON 원문 확인: 탐색 막대(246:173)는 246:161 장면 내부가 아니라
            //   246:172 '재생 컨트롤'(플레이어 하단 행) 내부 — JSON 원문 우선 배치(지시 '246:161 내부' 정정 기록).
            var controls = new VisualElement { name = "EventPlaybackControls" };
            controls.style.flexGrow = 1f;
            controls.style.paddingLeft = 10f;
            controls.style.paddingRight = 10f;
            controls.style.paddingTop = 4f;
            controls.style.paddingBottom = 8f;
            player.Add(controls);

            // 탐색 막대(246:173-176) — 260×3 트랙 #30363D r2 · 재생 구간 65×3 #58A6FF r2 · 위치 손잡이 6×6.
            //   정적 표시(65px = '00:08 / 00:32' 진실치 — 실제 탐색 연동 없음, 위조 금지).
            var seekBar = new VisualElement { name = "EventSeekBar" };
            seekBar.style.width = 260f;
            seekBar.style.height = 3f;
            seekBar.style.backgroundColor = new StyleColor(new Color(0x30 / 255f, 0x36 / 255f, 0x3D / 255f, 1f));   // #30363D
            UTKTheme.ApplyRadius(seekBar, 2f);
            seekBar.pickingMode = PickingMode.Ignore;
            var seekPlayed = new VisualElement { name = "SeekPlayed" };
            seekPlayed.style.position = Position.Absolute;
            seekPlayed.style.left = 0f;
            seekPlayed.style.top = 0f;
            seekPlayed.style.width = 65f;
            seekPlayed.style.height = 3f;
            seekPlayed.style.backgroundColor = new StyleColor(UTKTheme.Accent);   // #58A6FF (65px)
            UTKTheme.ApplyRadius(seekPlayed, 2f);
            seekPlayed.pickingMode = PickingMode.Ignore;
            seekBar.Add(seekPlayed);
            var seekKnob = new VisualElement { name = "SeekKnob" };
            seekKnob.style.position = Position.Absolute;
            seekKnob.style.width = 6f;
            seekKnob.style.height = 6f;
            seekKnob.style.left = 62f;    // 재생 구간 끝(65) 중심
            seekKnob.style.top = -1.5f;
            seekKnob.style.backgroundColor = new StyleColor(UTKTheme.Accent);
            UTKTheme.ApplyRadius(seekKnob, 3f);
            seekKnob.pickingMode = PickingMode.Ignore;
            seekBar.Add(seekKnob);
            controls.Add(seekBar);

            // 재생 도구(246:177) — 260×16 SPACE_BETWEEN: 좌(재생 10×10 + '00:08 / 00:32') · 우(volume/maximize).
            var transport = new VisualElement { name = "EventPlaybackTransport" };
            transport.style.flexDirection = FlexDirection.Row;
            transport.style.alignItems = Align.Center;
            transport.style.justifyContent = Justify.SpaceBetween;
            transport.style.width = 260f;
            transport.style.marginTop = 8f;
            transport.pickingMode = PickingMode.Ignore;

            var timeGroup = new VisualElement { name = "PlayTimeGroup" };
            timeGroup.style.flexDirection = FlexDirection.Row;
            timeGroup.style.alignItems = Align.Center;
            var smallPlay = BuildPlayGlyph(10f, UTKTheme.TextMain);   // 246:179 재생 10×10 #F0F6FC
            smallPlay.style.marginRight = 8f;
            timeGroup.Add(smallPlay);
            // [144차 P8-EV #7] '00:08 / 00:32' — Geist Mono 10 #8B949E(246:180). 정적 표기 — 실제 영상 시간 아님(기록).
            var timeLabel = new Label("00:08 / 00:32");
            timeLabel.style.fontSize = 10f;
            timeLabel.style.color = new StyleColor(UTKColor.TextSecondary);   // #8B949E
            timeLabel.style.unityFont = GetGeistMonoFont();   // Geist Mono 패밀리 계약(에셋 부재 시 폴백 — 선례)
            timeGroup.Add(timeLabel);
            transport.Add(timeGroup);

            var settingsGroup = new VisualElement { name = "VideoSettings" };
            settingsGroup.style.flexDirection = FlexDirection.Row;
            settingsGroup.style.alignItems = Align.Center;
            var volumeGlyph = BuildVolumeGlyph(14f, UTKColor.TextSecondary);      // 246:201-202 (원 stroke #8B949E 1.5 → 채움 근사)
            volumeGlyph.style.marginRight = 12f;   // 진실 gap 12
            settingsGroup.Add(volumeGlyph);
            settingsGroup.Add(BuildMaximizeGlyph(14f, UTKColor.TextSecondary));   // 246:204-205
            transport.Add(settingsGroup);
            controls.Add(transport);

            return player;
        }

        private static Texture2D _sceneGradientTex;

        /// <summary>[144차 P8-EV #5] 장면 그라데이션(246:163) — LINEAR #0B0E14 0.125@0.0 → 0.0@0.58 → 0.8@1.0
        ///   텍스처 근사(1×64, 위→아래). USS gradient 금지 → HUDUTK.GetPlateGradientTexture 텍스처 선례.</summary>
        private static Texture2D GetSceneGradientTexture()
        {
            if (_sceneGradientTex != null) return _sceneGradientTex;
            var tex = new Texture2D(1, 64, TextureFormat.RGBA32, false) { name = "UTK_EventSceneGradient" };
            var pixels = new Color[64];
            for (int y = 0; y < 64; y++)
            {
                float t = 1f - (float)y / 63f;   // SetPixels 행 0=하단 — t는 상단 기준 진행률
                float a = t < 0.58f ? Mathf.Lerp(0.125f, 0f, t / 0.58f) : Mathf.Lerp(0f, 0.8f, (t - 0.58f) / 0.42f);
                pixels[y] = new Color(0x0B / 255f, 0x0E / 255f, 0x14 / 255f, a);   // #0B0E14
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            _sceneGradientTex = tex;
            return tex;
        }

        /// <summary>[144차 P8-EV #2] 이벤트 유형 배지(246:186-187) — 58×19 r4, fill #58A6FF@0.078 +
        ///   stroke #58A6FF@0.251 1.0, 텍스트 11 Bold #58A6FF(pad 7/3). 유형명은 타이틀(이모지+이름 통합)에서
        ///   분리 — GetEventTypeBadgeText 실데이터 매핑.</summary>
        private static VisualElement CreateEventTypeBadge(string text)
        {
            var badge = new VisualElement { name = "EventTypeBadge" };
            badge.style.backgroundColor = new StyleColor(new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.078f));
            UTKTheme.ApplyElementBorder(badge, new Color(0x58 / 255f, 0xA6 / 255f, 0xFF / 255f, 0.251f), 1f);
            UTKTheme.ApplyRadius(badge, 4f);
            badge.style.paddingLeft = badge.style.paddingRight = 7f;
            badge.style.paddingTop = badge.style.paddingBottom = 3f;
            var label = new Label(text ?? "");
            label.style.fontSize = 11f;   // 246:187 Roboto 700 11
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new StyleColor(UTKTheme.Accent);   // #58A6FF
            badge.Add(label);
            return badge;
        }

        /// <summary>[144차 P8-EV #2] 실제 이벤트 데이터(evt.type) → 배지 유형명 매핑(공격/영지/성장).
        ///   Figma 원문 표기 '영지 방어'(246:187)는 영지 카테고리 사례치.</summary>
        private static string GetEventTypeBadgeText(WorldEventManager.EventType type)
        {
            switch (type)
            {
                case WorldEventManager.EventType.MonsterRaid:           // 몬스터 습격
                case WorldEventManager.EventType.AssassinationContract: // 암살 의뢰
                    return "공격";
                case WorldEventManager.EventType.Fire:                  // 화재 발생
                case WorldEventManager.EventType.Plague:                // 역병 발생
                case WorldEventManager.EventType.Storm:                 // 악천후
                    return "영지";   // 영지 피해·수복 계열
                case WorldEventManager.EventType.TravelingMerchant:     // 방랑 상인 행렬
                case WorldEventManager.EventType.FireFestival:          // 풍년 축제
                case WorldEventManager.EventType.RoyalEnvoy:            // 왕실 사절
                    return "성장";
                default:
                    return "이벤트";
            }
        }

        /// <summary>[144차 P8-EV #3] 이벤트 발생 시각(246:188 '방금 발생' 11 #8B949E) — 실제 evt.startTime
        ///   경과 표기(60초 미만 '방금 발생' → 'N분 전 발생'). 자동 종료 카운트다운(⏰)과 병행 유지.</summary>
        private static string FormatOccurredTime(WorldEventManager.ActiveEvent evt)
        {
            float elapsed = Mathf.Max(0f, Time.time - evt.startTime);
            if (elapsed < 60f)
                return "방금 발생";
            return Mathf.FloorToInt(elapsed / 60f) + "분 전 발생";
        }

        // ── [144차 P8-EV #6/#7/#8] 플레이어 글리프 근사 — UTKTheme 글리프 선례(ConstructionWindowUTK
        //    Build*Glyph 사각/바 조합) 준용. 영상 소스 부재 → 전부 정적(pickingMode Ignore).

        /// <summary>재생(▶) 글리프 근사 — 우측 향한 픽셀 삼각형(세로 바 4본). 246:168 16×16 / 246:179 10×10.</summary>
        private static VisualElement BuildPlayGlyph(float size, Color color)
        {
            var host = new VisualElement { name = "PlayGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            float barWidth = size * 0.14f;
            float[] heights = { 0.92f, 0.66f, 0.4f, 0.16f };   // 좌→우 높이 감소(우측 향한 삼각형)
            for (int i = 0; i < heights.Length; i++)
            {
                var bar = new VisualElement();
                bar.style.width = barWidth;
                bar.style.height = size * heights[i];
                bar.style.backgroundColor = new StyleColor(color);
                bar.style.position = Position.Absolute;
                bar.style.left = i * barWidth * 1.45f;
                bar.style.top = size * (1f - heights[i]) * 0.5f;
                bar.pickingMode = PickingMode.Ignore;
                host.Add(bar);
            }
            return host;
        }

        /// <summary>volume-2 글리프 근사(246:201-202 14×14, 원 stroke #8B949E 1.5 → 채움 근사) —
        ///   스피커 몸체 + 2음파 바.</summary>
        private static VisualElement BuildVolumeGlyph(float size, Color color)
        {
            var host = new VisualElement { name = "VolumeGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            var body = new VisualElement();
            body.style.width = size * 0.28f;
            body.style.height = size * 0.5f;
            body.style.backgroundColor = new StyleColor(color);
            body.style.position = Position.Absolute;
            body.style.left = size * 0.07f;
            body.style.top = size * 0.25f;
            body.pickingMode = PickingMode.Ignore;
            host.Add(body);
            float[] waveHeights = { 0.36f, 0.68f };
            for (int i = 0; i < waveHeights.Length; i++)
            {
                var wave = new VisualElement();
                wave.style.width = size * 0.09f;
                wave.style.height = size * waveHeights[i];
                wave.style.backgroundColor = new StyleColor(color);
                wave.style.position = Position.Absolute;
                wave.style.left = size * (0.55f + i * 0.24f);
                wave.style.top = size * (1f - waveHeights[i]) * 0.5f;
                wave.pickingMode = PickingMode.Ignore;
                host.Add(wave);
            }
            return host;
        }

        /// <summary>maximize 글리프 근사(246:204-205 14×14, 원 stroke #8B949E 1.5) — 4코너 L자 브래킷
        ///   (AddCornerDecals 동일 2면 보더 트릭, 박스 중앙 10.5×10.5).</summary>
        private static VisualElement BuildMaximizeGlyph(float size, Color color)
        {
            var host = new VisualElement { name = "MaximizeGlyph" };
            host.style.width = size;
            host.style.height = size;
            host.pickingMode = PickingMode.Ignore;
            float box = size * 0.75f;        // 246:205 벡터 10.5×10.5 근사
            float inset = (size - box) * 0.5f;
            float weight = size * 0.107f;    // 1.5/14 근사
            void Bracket(string cornerName, bool top, bool left)
            {
                var bracket = new VisualElement { name = cornerName };
                bracket.style.position = Position.Absolute;
                bracket.style.width = box * 0.42f;
                bracket.style.height = box * 0.42f;
                var side = new StyleColor(color);
                bracket.style.borderTopColor = side;
                bracket.style.borderBottomColor = side;
                bracket.style.borderLeftColor = side;
                bracket.style.borderRightColor = side;
                bracket.style.borderTopWidth = top ? weight : 0f;
                bracket.style.borderBottomWidth = top ? 0f : weight;
                bracket.style.borderLeftWidth = left ? weight : 0f;
                bracket.style.borderRightWidth = left ? 0f : weight;
                if (left) bracket.style.left = inset; else bracket.style.right = inset;
                if (top) bracket.style.top = inset; else bracket.style.bottom = inset;
                bracket.pickingMode = PickingMode.Ignore;
                host.Add(bracket);
            }
            Bracket("MaximizeTL", true, true);
            Bracket("MaximizeTR", true, false);
            Bracket("MaximizeBL", false, true);
            Bracket("MaximizeBR", false, false);
            return host;
        }

        private static Font _geistMono;

        /// <summary>[144차 P8-EV #7] Geist Mono 패밀리 계약(246:180) — WorldMapWindowUTK/ArenaWindowUTK
        ///   동일 선례. 프로젝트에 Geist Mono 에셋 미보유 실측(2026-10-24) → 부재 시 UIFont.Load() 폴백
        ///   (폰트 스왑만 기록, 에셋 추가(Resources/Fonts/GeistMono) 시 자동 적용).</summary>
        private static Font GetGeistMonoFont()
        {
            if (_geistMono == null)
                _geistMono = Resources.Load<Font>("Fonts/GeistMono");
            return _geistMono != null ? _geistMono : UIFont.Load();
        }
    }
}