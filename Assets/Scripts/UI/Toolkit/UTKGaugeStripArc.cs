using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// [2026-10-11 26차→27차 HUD 링 수리] 베이크 스트립 기반 원형 게이지 — HUD 전용.
    /// generateVisualContent 벡터 메시(UTKCircularGauge)는 조상 transform.scale 아래에서
    /// 갱신 재생성이 불안정했다(실측: 링이 일부만 렌더되거나 값이 바뀌어도 재도장 안 됨 —
    /// 테스트 15 영상·픽셀 실측). HUD 링은 <b>65프레임 베이크 스트립(UI/UTKGaugeArcStrip,
    /// 흰 코어 arc + 글로우 + tip)</b> + 배경 이미지 슬라이싱으로 교체 — 갱신 = backgroundPositionX
    /// 스타일 변경이므로 재도장이 엔진 수준에서 보장된다(메시 파이프라인 불요).
    ///
    /// 시계방향 소모(시작 에지 = -90°+360°×(1-f), 스윕 360°×f — 공백이 12시부터 시계방향 성장)는
    /// 스트립 프레임에 굽혀져 있다. DrainClockwise 프로퍼티는 호환용(항상 true 의미).
    /// 색 = unityBackgroundImageTintColor(HP 레드/스타미나 옐로 — HUDUTK에서 대입).
    /// 활 리티클 등 기존 소비처는 UTKCircularGauge(메시)를 그대로 사용 — 무변경.
    /// </summary>
    public class UTKGaugeStripArc : VisualElement
    {
        public const int FrameSize = 128;
        public const int FrameCount = 65;

        private static Texture2D _strip;
        private float _fraction = 1f;
        private bool _dirty = true;

        /// <summary>소모 방향 — 스트립에 굽혀진 의미(항상 시계방향 소모). 호환 유지용.</summary>
        public bool DrainClockwise { get => true; set { /* 스트립 프레임에 굽혀진 의미 — no-op */ } }

        public UTKGaugeStripArc()
        {
            if (_strip == null) _strip = Resources.Load<Texture2D>("UI/UTKGaugeArcStrip");
            if (_strip != null)
            {
                style.backgroundImage = UTKTextureSafe.ToBackground(_strip);
                style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            }
            pickingMode = PickingMode.Ignore;
            Refresh();
        }

        /// <summary>0~1 게이지 값. 변경 시 다음 틱에 슬라이스 갱신.</summary>
        public float Fraction
        {
            get => _fraction;
            set
            {
                var v = Mathf.Clamp01(value);
                if (Mathf.Abs(v - _fraction) < 0.0005f) return;
                _fraction = v;
                _dirty = true;
            }
        }

        /// <summary>변경분 반영 (외부 폴링 루프에서 호출 — HUDUTK 기존 경로 유지).</summary>
        public void CommitIfDirty()
        {
            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
        }

        /// <summary>요소 크기 기준 슬라이싱 재계산 — 값/크기 변경 모두 커버(리사이즈 안전).</summary>
        public void Refresh()
        {
            if (_strip == null) return;
            float w = resolvedStyle.width;
            if (float.IsNaN(w) || w <= 0f) w = style.width.value.value;
            if (float.IsNaN(w) || w <= 0f) return;
            float s = w / FrameSize;
            int frame = Mathf.Clamp(Mathf.RoundToInt(_fraction * (FrameCount - 1)), 0, FrameCount - 1);
            style.backgroundSize = new BackgroundSize(
                new Length(FrameSize * FrameCount * s, LengthUnit.Pixel),
                new Length(FrameSize * s, LengthUnit.Pixel));
            style.backgroundPositionX = new BackgroundPosition(
                BackgroundPositionKeyword.Left, new Length(-frame * FrameSize * s, LengthUnit.Pixel));
            style.backgroundPositionY = new BackgroundPosition(
                BackgroundPositionKeyword.Top, new Length(0f, LengthUnit.Pixel));
        }
    }
}
