using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// ui 예시 2 스펙 HP바(P24 신규) — 베이크 프레임 + 레드 fill + 팁 글로우 + 수치 라벨.
    /// 프레임(GaugeHPFrame 440×160, 좌측 라운드 캡 + 우측 슬롯 웰)의 슬롯 rect 안에
    /// GaugeHPFill 스트립을 폭 비례로 채우고, fill 끝단에 GaugeTipGlow(세로 타원 글로우),
    /// 슬롯 중앙에 "현재 / 최대" 수치를 표시한다.
    /// 종횡비 고정(72×198 = 원본 440/160) — 프레임/게이지 왜곡 방지.
    /// [P23-3] 모든 자식 pickingMode=Ignore — 오버레이 자식 클릭 흡수 방지(필수).
    /// </summary>
    public class HPBarUTK : VisualElement
    {
        // [베이크 실측] 프레임 슬롯 rect(정규화) — GaugeHPFrame.png 440×160 기준
        private const float SlotU0 = 0.3841f;
        private const float SlotV0 = 0.0688f;
        private const float SlotU1 = 0.9886f;
        private const float SlotV1 = 0.9313f;

        /// <summary>표시 높이(px). 폭 = 높이 × 원본 종횡비(2.75)로 고정.</summary>
        public const float BarHeight = 72f;
        public const float BarWidth = BarHeight * (440f / 160f);   // 198

        // 슬롯 지오메트리(정적 계산) — fill/tip/라벨 배치 기준
        private static readonly float SlotW = (SlotU1 - SlotU0) * BarWidth;   // ≈119.69
        private static readonly float SlotH = (SlotV1 - SlotV0) * BarHeight;  // ≈62.10
        private static readonly float FillLeft = SlotU0 * BarWidth + 2f;      // 슬롯 좌측 + 2px 여백
        private static readonly float FillTop = SlotV0 * BarHeight + 3f;      // 슬롯 상단 + 3px 여백
        private static readonly float FillHeight = SlotH - 6f;                // 상하 3px 여백
        private static readonly float TipW = SlotH * 0.5f;   // GaugeTipGlow 96×192 종횡비(0.5)와 일치 — ScaleToFit 무왜곡
        private static readonly float TipH = SlotH;

        private readonly VisualElement _fill;
        private readonly VisualElement _tipGlow;
        private readonly Label _label;

        private float _lastFrac = -1f;
        private string _lastText;

        public HPBarUTK()
        {
            pickingMode = PickingMode.Ignore;
            style.width = BarWidth;
            style.height = BarHeight;

            // 프레임(베이크 PNG) — 전체 크기, 요소 종횡비 = 원본 종횡비라 scale-to-fit 무왜곡
            var frame = new VisualElement { pickingMode = PickingMode.Ignore };
            frame.style.position = Position.Absolute;
            frame.style.left = 0f;
            frame.style.top = 0f;
            frame.style.width = BarWidth;
            frame.style.height = BarHeight;
            frame.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeHPFrame"));
            frame.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            Add(frame);

            // fill — 슬롯 rect 내부, 폭 = slotW × fraction(최소폭 0, fraction 0이면 숨김)
            _fill = new VisualElement { pickingMode = PickingMode.Ignore };
            _fill.style.position = Position.Absolute;
            _fill.style.left = FillLeft;
            _fill.style.top = FillTop;
            _fill.style.height = FillHeight;
            _fill.style.width = 0f;
            _fill.style.display = DisplayStyle.None;
            _fill.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeHPFill"));
            // 9-슬라이스 — 좌우/상단 경계 고정 + 중앙 자동 신장(ScaleMode.Stretch 미존재, GUI용과 혼동 금지)
            _fill.style.unitySliceLeft = 6;
            _fill.style.unitySliceRight = 6;
            _fill.style.unitySliceTop = 8;
            _fill.style.unitySliceBottom = 8;
            Add(_fill);

            // 팁 글로우 — fill 끝단 세로 타원 빛(96×96 → 세로로 늘린 타원, 점이 아닌 수직 바 형태)
            _tipGlow = new VisualElement { pickingMode = PickingMode.Ignore };
            _tipGlow.style.position = Position.Absolute;
            _tipGlow.style.width = TipW;
            _tipGlow.style.height = TipH;
            _tipGlow.style.top = FillTop + (FillHeight - TipH) * 0.5f;
            _tipGlow.style.display = DisplayStyle.None;
            _tipGlow.style.backgroundImage = UTKTextureSafe.ToBackground(Resources.Load<Texture2D>("UI/GaugeTipGlow"));
            _tipGlow.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;   // 96×192 세로 타원 — 요소 31×62와 동일 종횡비, 무왜곡
            _tipGlow.style.opacity = 0.85f;
            Add(_tipGlow);

            // 수치 라벨 — 슬롯 중앙 "현재 / 최대"
            _label = new Label(string.Empty) { pickingMode = PickingMode.Ignore };
            _label.style.position = Position.Absolute;
            _label.style.left = SlotU0 * BarWidth;
            _label.style.top = SlotV0 * BarHeight;
            _label.style.width = SlotW;
            _label.style.height = SlotH;
            _label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _label.style.color = Color.white;
            _label.style.fontSize = 15;
            _label.style.textShadow = new TextShadow
            {
                offset = new Vector2(0f, -1f),
                blurRadius = 2f,
                color = new Color(0f, 0f, 0f, 0.6f)
            };
            UTKWindowBase.ApplyUIToolkitFont(_label);
            Add(_label);
        }

        /// <summary>
        /// 게이지 값 설정 — 값 변화 시에만 스타일 갱신(더티 체크, 250ms 폴링 루프 대응).
        /// fraction은 지오메트리, current/max는 라벨 텍스트에 반영된다.
        /// </summary>
        public void Set(float fraction, float current, float max)
        {
            float frac = Mathf.Clamp01(fraction);
            string text = FormatValueText(current, max);
            bool geoDirty = Mathf.Abs(frac - _lastFrac) > 0.0005f;
            bool textDirty = !string.Equals(text, _lastText, System.StringComparison.Ordinal);
            if (!geoDirty && !textDirty) return;

            if (geoDirty)
            {
                _lastFrac = frac;
                float fillW = Mathf.Max(0f, SlotW * frac);
                _fill.style.width = fillW;
                bool showFill = frac > 0.0005f;
                _fill.style.display = showFill ? DisplayStyle.Flex : DisplayStyle.None;

                // 팁 글로우 — fill 끝 중앙(fraction < 0.06 은 숨김: 프레임 밖 넘침 방지)
                bool showTip = showFill && frac >= 0.06f;
                _tipGlow.style.display = showTip ? DisplayStyle.Flex : DisplayStyle.None;
                if (showTip)
                    _tipGlow.style.left = FillLeft + fillW - TipW * 0.5f;
            }

            if (textDirty)
            {
                _lastText = text;
                _label.text = text;
            }
            // 최대치 미확정(로딩 직전 등)일 때는 수치 대신 빈 슬롯
            _label.style.display = max > 0f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>"현재 / 최대" — 현재는 F1(예: 36.6), 최대는 정수면 축약(예: 40).</summary>
        private static string FormatValueText(float current, float max)
        {
            return current.ToString("F1") + " / " + max.ToString("0.#");
        }
    }
}
