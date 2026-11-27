// [156차 P3] 이계 3택1 카드/증강 UI — UTKWindowBase 파생 자가부트(FishingUTK 선례 패턴 복제).
// 156_AFTERLIFE_CARD_UI_PLAN.md §1(계약):
//   - 구독(Ensure 시 1회): AfterlifeAugmentSystem.OnOfferCreated / AfterlifeCardSystem.OnOfferCreated
//     → pending 플래그 + 자동 개방. 양쪽 OnOfferResolved → 패널 닫기 + 플래그 해제.
//     UI→Systems 방향 이벤트 구독만 허용(어셈블리 역참조 없음).
//   - 표시: 3택 카드 3장 세로 배치 버튼 — 이름/설명/등급색 테두리.
//     등급색 고정: Common #8B949E · Rare #58A6FF · Epic #BC8CFF · Legendary #F0B72F.
//     증강 3택은 기존 파란색(#58A6FF) 단일 테두리.
//   - 패널 헤더 "선택하라" + 20초 카운트다운 바(VisualElement width 퍼센트 — 벡터메시 금지 계약 준수).
//     카운트다운은 Updater에서 AfterlifeAugmentSystem.PendingTimeoutSec 경과 기반 표시만
//     담당(만료 판정/자동 선택은 시스템 몫 — UI는 Select(index) 라우팅만 호출).
//   - 클릭 라우팅: 증강 pending → AfterlifeAugmentSystem.Select(index) /
//     카드 pending → AfterlifeCardSystem.Select(index). 증강 우선 표시
//     (둘 다 pending인 경우는 정상 상태에서 발생 안 함 — 증강 resolved 후 카드 발화).
//   - Systems 소비처 호출 금지(Select만) — 게임플레이 로직 직접 구현 금지.
#pragma warning disable 114
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Systems;   // AfterlifeAugmentSystem / AfterlifeCardSystem / CardDef

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [156차 P3] UI Toolkit — 이계 3택1 (증강/카드 공용) 선택 창 (UTKWindowBase 파생).
    /// 상태/판정은 모두 AfterlifeAugmentSystem·AfterlifeCardSystem가 소유.
    /// 본 창은 오퍼 이벤트 구독 + 표시 + Select 라우팅만 담당한다(게임플레이 로직 없음).
    /// </summary>
    public class AfterlifeCardUIUTK : UTKWindowBase
    {
        private static AfterlifeCardUIUTK _instance;
        private static Updater _updater;
        private static bool _wired;                            // 구독 1회 가드(Ensure 시 1회)
        private static bool _augmentPending;                   // 증강 3택 대기 중
        private static bool _cardPending;                      // 카드 3택 대기 중
        private static float _pendingStartedAtUnscaled = -1f;  // 오퍼 생성 시각 — 카운트다운 표시 기준

        private const float WinW = 420f;
        private const float WinH = 470f;

        private readonly VisualElement _cardsHost;      // 3택 세로 버튼 컨테이너
        private readonly VisualElement _countdownTrack; // 카운트다운 바 트랙
        private readonly VisualElement _countdownFill;  // width 퍼센트로 줄어드는 채움
        private readonly Label _countdownLabel;         // 잔여 초 라벨

        public static AfterlifeCardUIUTK Instance => _instance;

        private AfterlifeCardUIUTK() : base("🃏 선택하라", new Vector2(WinW, WinH))
        {
            SetChrome(UTKWindowChrome.Frameless);
            style.backgroundColor = new StyleColor(UTKTheme.GlassPanelFill);
            UTKTheme.ApplyElementBorder(this);
            UTKTheme.ApplyRadius(this, UTKTheme.GlassRadius);
            _content.style.flexGrow = 1f;
            _content.style.flexDirection = FlexDirection.Column;
            _content.style.paddingLeft = 20f; _content.style.paddingRight = 20f;
            _content.style.paddingTop = 20f; _content.style.paddingBottom = 20f;

            // ── 헤더: 🃏 선택하라 ── [156차 P3]
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            var headerIcon = new Label("🃏");
            headerIcon.style.fontSize = 16.8f;
            headerRow.Add(headerIcon);
            var headerLbl = new Label("선택하라");
            headerLbl.style.marginLeft = 7.2f;
            headerLbl.style.fontSize = 15.6f;
            headerLbl.style.unityFontStyleAndWeight = FontStyle.Bold;
            headerLbl.style.color = new StyleColor(GradePalette.White);
            headerLbl.style.flexGrow = 1f;
            headerRow.Add(headerLbl);
            _content.Add(headerRow);

            // ── 20초 카운트다운 바 (VisualElement width 퍼센트 — 벡터메시 금지 계약 준수) ── [156차 P3]
            var countdownRow = new VisualElement();
            countdownRow.style.flexDirection = FlexDirection.Row;
            countdownRow.style.alignItems = Align.Center;
            countdownRow.style.marginTop = 10f;
            _countdownLabel = new Label("⏳ 잔여 20초");
            _countdownLabel.style.fontSize = 12f;
            _countdownLabel.style.color = new StyleColor(GradePalette.TextBody);
            _countdownLabel.style.flexGrow = 1f;
            countdownRow.Add(_countdownLabel);
            _content.Add(countdownRow);

            _countdownTrack = new VisualElement();
            _countdownTrack.style.height = 6f;
            _countdownTrack.style.marginTop = 6f;
            _countdownTrack.style.backgroundColor = new StyleColor(GradePalette.BarTrack);
            _content.Add(_countdownTrack);

            _countdownFill = new VisualElement();
            _countdownFill.style.height = 6f;
            _countdownFill.style.width = Length.Percent(100f);
            _countdownFill.style.backgroundColor = new StyleColor(GradePalette.Accent);
            _countdownTrack.Add(_countdownFill);

            // ── 3택 카드 컨테이너 (세로 배치) ── [156차 P3]
            _cardsHost = new VisualElement();
            _cardsHost.style.flexDirection = FlexDirection.Column;
            _cardsHost.style.marginTop = 14f;
            _cardsHost.style.flexGrow = 1f;
            _content.Add(_cardsHost);
        }

        // ===== 등급색 팔레트 ([156차 P3] — 계획서 §1 등급색 고정) =====

        private static class GradePalette
        {
            public static readonly Color Common    = Hex(0x8B949E);   // Common
            public static readonly Color Rare      = Hex(0x58A6FF);   // Rare
            public static readonly Color Epic      = Hex(0xBC8CFF);   // Epic
            public static readonly Color Legendary = Hex(0xF0B72F);   // Legendary
            public static readonly Color Accent    = Hex(0x58A6FF);   // 증강 3택 단일 파란색
            public static readonly Color CardFill  = Hex(0x161B22);   // 카드 버튼 배경
            public static readonly Color BarTrack  = Hex(0x30363D);   // 카운트다운 트랙
            public static readonly Color White     = Hex(0xF0F6FC);   // 헤더 텍스트
            public static readonly Color TextBody  = Hex(0xC9D1D9);   // 본문 텍스트

            private static Color Hex(uint rgb) =>
                new Color32((byte)((rgb >> 16) & 0xFF), (byte)((rgb >> 8) & 0xFF), (byte)(rgb & 0xFF), 0xFF);

            /// <summary>카드 등급 → 테두리색 (증강은 호출자가 Accent 고정).</summary>
            public static Color Of(CardRarity rarity)
            {
                switch (rarity)
                {
                    case CardRarity.Epic: return Epic;
                    case CardRarity.Legendary: return Legendary;
                    case CardRarity.Rare: return Rare;
                    default: return Common;
                }
            }
        }

        // ===== 공개 API =====

        /// <summary>[156차 P3] 전 씬 자가부트 — FishingUTK 선례(RuntimeInitializeOnLoadMethod 계열) 동일.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap() => Ensure();

        /// <summary>싱글톤 + 이벤트 구독 보장 (멱등 — Systems 이벤트 구독은 Ensure 시 1회).</summary>
        public static void Ensure()
        {
            WireEvents();
            if (_instance != null) return;
            _instance = new AfterlifeCardUIUTK();
            var go = new GameObject("AfterlifeCardUIUTK_UPD");
            Object.DontDestroyOnLoad(go);
            _updater = go.AddComponent<Updater>();
            _updater.window = _instance;
            Debug.Log("[AfterlifeCardUI] 인스턴스 생성");
        }

        /// <summary>[156차 P3] Systems 이벤트 구독 — UI→Systems 방향만(어셈블리 역참조 없음, 1회 가드).</summary>
        private static void WireEvents()
        {
            if (_wired) return;
            _wired = true;

            AfterlifeAugmentSystem.OnOfferCreated += HandleAugmentOfferCreated;
            AfterlifeCardSystem.OnOfferCreated += HandleCardOfferCreated;
            AfterlifeAugmentSystem.OnOfferResolved += HandleAugmentOfferResolved;
            AfterlifeCardSystem.OnOfferResolved += HandleCardOfferResolved;
        }

        // ===== Systems 이벤트 핸들러 ([156차 P3]) =====

        private static void HandleAugmentOfferCreated(List<AfterlifeAugmentSystem.AugmentDef> offer)
        {
            _augmentPending = true;
            MarkPendingStarted();
            Open();                          // OnOfferCreated 핸들러에서 자동 개방
            _instance?.RebuildCards();
        }

        private static void HandleCardOfferCreated(List<CardDef> offer)
        {
            _cardPending = true;
            MarkPendingStarted();
            Open();                          // OnOfferCreated 핸들러에서 자동 개방
            _instance?.RebuildCards();
        }

        private static void HandleAugmentOfferResolved()
        {
            _augmentPending = false;
            OnAnyOfferResolved();
        }

        private static void HandleCardOfferResolved()
        {
            _cardPending = false;
            OnAnyOfferResolved();
        }

        /// <summary>양쪽 Resolved 공통 — 남은 pending 없으면 패널 닫기, 있으면 다음 표시로 교체.</summary>
        private static void OnAnyOfferResolved()
        {
            if (!_augmentPending && !_cardPending)
            {
                CloseUI();
            }
            else
            {
                _instance?.RebuildCards();   // 증강 resolved 직후 카드 오퍼 발화 등
            }
        }

        /// <summary>오퍼 생성 시각 기록 — 카운트다운 표시 기준(만료 판정은 시스템 몫).</summary>
        private static void MarkPendingStarted()
        {
            _pendingStartedAtUnscaled = Time.unscaledTime;
        }

        /// <summary>창 오픈.</summary>
        public static void Open()
        {
            Ensure();
            _instance?.Show();
        }

        /// <summary>창 닫기.</summary>
        public static void CloseUI() => _instance?.Hide();

        /// <summary>토글.</summary>
        public static void Toggle()
        {
            if (_instance != null && _instance.IsOpen) { _instance.Hide(); return; }
            Open();
        }

        // ===== 생명주기 =====

        public override void Show()
        {
            base.Show();
            var root = UIToolkitBootstrap.UIRoot;
            if (root != null && parent == null)
                root.Add(this);
            RebuildCards();
        }

        // ===== 화면 구성 ([156차 P3]) =====

        /// <summary>3택 카드 재구성 — 증강 우선 표시(둘 다 pending은 정상 상태에서 발생 안 함).</summary>
        public void RebuildCards()
        {
            if (_cardsHost == null) return;
            _cardsHost.Clear();

            if (_augmentPending)
            {
                var choices = AfterlifeAugmentSystem.PendingChoices;
                for (int i = 0; i < choices.Count; i++)
                {
                    var def = choices[i];   // 클로저 캡처 방지 — 로컬 복사
                    int index = i;
                    _cardsHost.Add(BuildChoiceCard(def.Name, def.Desc, GradePalette.Accent,
                        () => SelectChoice(index)));
                }
                return;
            }

            if (_cardPending)
            {
                var choices = AfterlifeCardSystem.PendingChoices;
                for (int i = 0; i < choices.Count; i++)
                {
                    var def = choices[i];
                    int index = i;
                    _cardsHost.Add(BuildChoiceCard(def.Name, def.Desc, GradePalette.Of(def.Rarity),
                        () => SelectChoice(index)));
                }
                return;
            }

            // pending 없음 — 플레이스홀더(수동 Open/Toggle 경로)
            var empty = new Label("제시 대기 중…");
            empty.style.fontSize = 13.2f;
            empty.style.color = new StyleColor(GradePalette.TextBody);
            empty.style.unityTextAlign = TextAnchor.MiddleCenter;
            empty.style.flexGrow = 1f;
            _cardsHost.Add(empty);
        }

        /// <summary>선택 카드 버튼 1장 — 이름/설명 + 등급색 테두리(세로 배치).</summary>
        private static VisualElement BuildChoiceCard(string name, string desc, Color border, System.Action onClick)
        {
            var card = new Button(onClick);
            card.style.marginTop = 8f;
            card.style.marginBottom = 8f;
            card.style.paddingLeft = 14f; card.style.paddingRight = 14f;
            card.style.paddingTop = 10f; card.style.paddingBottom = 10f;
            card.style.backgroundColor = new StyleColor(GradePalette.CardFill);
            card.style.borderTopWidth = 1.6f; card.style.borderBottomWidth = 1.6f;
            card.style.borderLeftWidth = 1.6f; card.style.borderRightWidth = 1.6f;
            card.style.borderTopColor = border; card.style.borderBottomColor = border;
            card.style.borderLeftColor = border; card.style.borderRightColor = border;

            var nameLabel = new Label(name);
            nameLabel.style.fontSize = 15.6f;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.color = new StyleColor(border);   // 이름 = 등급색
            card.Add(nameLabel);

            var descLabel = new Label(desc);
            descLabel.style.fontSize = 12.6f;
            descLabel.style.whiteSpace = WhiteSpace.Normal;
            descLabel.style.color = new StyleColor(GradePalette.TextBody);
            card.Add(descLabel);
            return card;
        }

        // ===== 클릭 라우팅 ([156차 P3]) =====

        /// <summary>
        /// 선택 라우팅 — 증강 pending이면 AfterlifeAugmentSystem.Select, 카드 pending이면
        /// AfterlifeCardSystem.Select. Systems 소비처 호출은 Select만(게임플레이 로직 직접 구현 금지).
        /// 선택 후 Resolved 이벤트가 핸들러에서 패널을 닫는다.
        /// </summary>
        private static void SelectChoice(int index)
        {
            if (_augmentPending) { AfterlifeAugmentSystem.Select(index); return; }
            if (_cardPending) { AfterlifeCardSystem.Select(index); return; }
        }

        // ===== 카운트다운 ([156차 P3] — 표시만, 만료 판정/자동선택은 시스템 몫) =====

        /// <summary>Updater 프레임 호출 — PendingTimeoutSec 경과 기반 잔여율 표시(width 퍼센트).</summary>
        public void TickCountdown()
        {
            if (_countdownFill == null) return;
            if (!_augmentPending && !_cardPending) return;

            float elapsed = Mathf.Max(0f, Time.unscaledTime - _pendingStartedAtUnscaled);
            float timeout = AfterlifeAugmentSystem.PendingTimeoutSec;   // 양쪽 시스템 동일 20초
            float remainRatio = Mathf.Clamp01(1f - elapsed / timeout);
            _countdownFill.style.width = Length.Percent(remainRatio * 100f);
            if (_countdownLabel != null)
                _countdownLabel.text = $"⏳ 잔여 {Mathf.CeilToInt(remainRatio * timeout)}초";
        }

        // ===== 카운트다운/재부착 Updater (FishingUTK 선례 복제 — [156차 P3]) =====

        private class Updater : MonoBehaviour
        {
            public AfterlifeCardUIUTK window;

            private void Update()
            {
                var root = UIToolkitBootstrap.UIRoot;
                if (root != null && window != null && window.parent == null)
                    root.Add(window);   // 씬 전환/루트 재생성 대비 재부착(FishingUTK 동일)

                if (window != null && window.IsOpen)
                    window.TickCountdown();   // [156차 P3] 카운트다운 표시 갱신(판정은 시스템 몫)
            }

            private void OnDestroy()
            {
                if (window != null)
                    window.RemoveFromHierarchy();
            }
        }
    }
}
