using UnityEngine;
using UnityEngine.UIElements;
using ProjectName.Core;   // PlayerStats (지갑 단일소스)

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [71차] 공용 골드 밸런스 위젯 (피그마 225:4~11) — 인벤 푸터 WeightRow + 창고 StorageStats 단일소스.
    /// 계획서: docs/2026-10-15_71TH_INVENTORY_GOLD_BALANCE_PLAN.md Phase 2.
    /// 70차 별도 핫바 지갑 배지 폐기의 대체 — 골드 데이터는 PlayerStats 지갑 단일소스 유지
    /// (전리품 골드 수령 → AddItem 인터셉트 → 지갑 증가 → GoldChanged로 라벨 즉시 갱신).
    ///
    /// [기하 진실치] 컨테이너 97.4×26.6:
    ///  - pill (0,0) 46.2×26.6 — bg #FFFFFF/4% · radius 4.8 · 테두리 없음
    ///  - GoldLabel "골드" — pill-local (9.6,4.8) 27×17, 14.4/400 #F0F6FC
    ///  - GoldValue — 컨테이너-로컬 (53.4,3.8) 44×19, 14.4/700 #F0F6FC, "#,##0"
    ///    (컨테이너가 WeightRow-로컬 (0,0)에 놓이므로 컨테이너-로컬 == WeightRow-로컬 진실치).
    /// [picking] 값 라벨/pill 내부 요소는 PickingMode.Ignore(클릭 통과 — 클릭 차단 금지),
    ///   컨테이너 자체는 기본 유지(창 내부 요소이므로 창 조작 영역 보존).
    /// [갱신] Refresh(Label) — PlayerStats.Instance?.Gold → "#,##0" (null="0").
    ///   변화 시만 text 대입(250ms 폴링 경로 GC 억제 선례). 구독은 창별 수명(창 Hide/탈부착 시 해제) —
    ///   위젯은 순수 빌더로 유지해 2창(인벤/창고)이 동일 소스를 공유한다.
    /// </summary>
    public static class UTKGoldBalance
    {
        // ── [피그마 225:4~11 진실치] ──
        public const float ContainerWidth = 97.4f;
        public const float ContainerHeight = 26.6f;
        public const float PillWidth = 46.2f;
        public const float PillHeight = 26.6f;
        public const float PillRadius = 4.8f;
        public const float LabelX = 9.6f;
        public const float LabelY = 4.8f;
        public const float LabelWidth = 27f;
        public const float LabelHeight = 17f;
        public const float ValueX = 53.4f;
        public const float ValueY = 3.8f;
        public const float ValueWidth = 44f;
        public const float ValueHeight = 19f;
        public const float FontSize = 14.4f;

        // pill bg #FFFFFF/4% · 텍스트 #F0F6FC — 위젯 단일소스 상수(UTKTheme.TextMain 동일 톤).
        private static readonly Color PillBgColor = new Color(1f, 1f, 1f, 0.04f);
        private static readonly Color TextColor = new Color(0.941f, 0.963f, 0.988f);   // #F0F6FC

        /// <summary>공용 위젯 빌더 — pill "골드" + 값 라벨. valueLabel은 Refresh 갱신 대상(인벤/창고 창이 보관).</summary>
        public static VisualElement Build(out Label valueLabel, bool includeBadge = true)
        {
            var container = new VisualElement { name = "GoldBalance" };
            container.style.width = ContainerWidth;
            container.style.height = ContainerHeight;

            Label value = null;
            if (includeBadge)
            {
                var pill = new VisualElement { name = "GoldBalancePill" };
                pill.style.position = Position.Absolute;
                pill.style.left = 0f;
                pill.style.top = 0f;
                pill.style.width = PillWidth;
                pill.style.height = PillHeight;
                pill.style.backgroundColor = new StyleColor(PillBgColor);
                pill.style.borderTopLeftRadius = PillRadius;
                pill.style.borderTopRightRadius = PillRadius;
                pill.style.borderBottomLeftRadius = PillRadius;
                pill.style.borderBottomRightRadius = PillRadius;
                pill.pickingMode = PickingMode.Ignore;   // pill 영역 클릭 무시 — 조작 차단 금지
                container.Add(pill);

                var label = new Label("골드") { name = "GoldBalanceLabel" };
                label.style.position = Position.Absolute;
                label.style.left = LabelX;
                label.style.top = LabelY;
                label.style.width = LabelWidth;
                label.style.height = LabelHeight;
                UTKTheme.StyleFigmaText(label, FontSize, 400, TextColor);
                label.pickingMode = PickingMode.Ignore;
                pill.Add(label);

                value = new Label("0") { name = "GoldBalanceValue" };
                value.style.position = Position.Absolute;
                value.style.left = ValueX;
                value.style.top = ValueY;
                value.style.width = ValueWidth;
                value.style.height = ValueHeight;
                UTKTheme.StyleFigmaText(value, FontSize, 700, TextColor, TextAnchor.MiddleRight);
                value.pickingMode = PickingMode.Ignore;   // 값 라벨 클릭 통과
                container.Add(value);
            }

            valueLabel = value;
            return container;
        }

        /// <summary>지갑 단일소스 갱신 — PlayerStats.Instance?.Gold → "#,##0" (null="0"). 변화 시만 DOM 대입.</summary>
        public static void Refresh(Label valueLabel)
        {
            if (valueLabel == null) return;
            int gold = PlayerStats.Instance != null ? PlayerStats.Instance.Gold : 0;
            string text = gold.ToString("#,##0");
            if (valueLabel.text == text) return;   // 값 변화 시만 갱신 — 폴링 경로 GC 억제
            valueLabel.text = text;
        }
    }
}
