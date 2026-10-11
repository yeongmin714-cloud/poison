using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P22-5 — 원형 게이지 컨트롤 (generateVisualContent 벡터 호).
    /// IMGUI 폴리곤 바를 대체하는 고품질 게이지: 배경 링(디머) + 전경 링(팀색)이
    /// 12시 방향 기준 **시계방향**으로 채워지고, 값 감소 = 시계방향 소모로 표현된다.
    /// 해상도 무관 벡터(96세그먼트) — 폴리곤 계단 없음. 프리미티브 금지 규약(P22-6) 준수.
    ///
    /// [2026-10-10 Figma 23:6 폼 정합 — opt-in 계약] HUD 게이지의 Figma 원본 스펙
    /// (figma-file.json: outer-track/outer-progress-glow arcData innerRadius=0.8 →
    /// 링 두께 = 0.2×r, 외곽 밀착 0.8r..1.0r + 주황 DROP_SHADOW 글로우 + 흰 progress-tip)을
    /// 속성으로 옵트인한다. 기본값은 P22-5 원형(두께 0.28r, 내부 2px 인셋, 디머 배경 링,
    /// 글로우/tip 없음) — BowAimReticleUTK 등 기존 사용처 픽셀 무변경 보장.
    /// </summary>
    public class UTKCircularGauge : VisualElement
    {
        private float _fraction = 1f;
        private Color _fillColor = new Color(0.2f, 0.75f, 0.35f, 1f);
        private int _segments = 96;
        private bool _dirty;

        // [2026-10-10 opt-in 계약] 기본값 = P22-5 원형 — 기존 사용처(BowAimReticleUTK) 무변경.
        private float _thicknessFactor = 0.28f;                  // 링 두께(반지름 배수)
        private float _outerInset = 2f;                          // 링 외곽 → 엘리먼트 가장자리 여백(px)
        private Color _backgroundColor = new Color(0f, 0f, 0f, 0.45f); // 배경(전원) 링 — 디머
        private bool _glowEnabled;                               // 진행 arc 2중 글로우 패스
        private bool _tipEnabled;                                // arc 끝 흰 tip 세그먼트(Figma progress-tip)
        // [2026-10-11 사용자 요구 — 게이지 시계방향 소모] true면 소모 시 공백이 12시부터 시계방향으로
        //   성장한다(채움 시작 에지가 시계방향으로 이동, 끝은 12시 고정). 기본 false — P22-5 원형과
        //   BowAimReticleUTK 파워 링 픽셀 무변경 보호(opt-in 계약).
        private bool _drainClockwise;

        /// <summary>소모 방향 — true: 색이 시계방향으로 빠져나감(공백 12시→시계방향 성장).</summary>
        public bool DrainClockwise
        {
            get => _drainClockwise;
            set { _drainClockwise = value; _dirty = true; }
        }

        /// <summary>호 시작 각도(도) — 순수 헬퍼(EditMode 검증용). 일반: 12시 고정(-90°).
        /// DrainClockwise: -90° + 360°×(1-f) — 소모 경계가 시계방향으로 이동.</summary>
        public static float ArcStartDegrees(float fraction, bool drainClockwise)
        {
            float f = Mathf.Clamp01(fraction);
            return drainClockwise ? -90f + 360f * (1f - f) : -90f;
        }

        public UTKCircularGauge()
        {
            generateVisualContent += OnGenerateVisualContent;
        }

        /// <summary>0~1 게이지 값. 변경 시 다음 프레임 다시 그림.</summary>
        public float Fraction
        {
            get => _fraction;
            set
            {
                var v = Mathf.Clamp01(value);
                if (Mathf.Abs(v - _fraction) < 0.001f) return;
                _fraction = v;
                _dirty = true;
            }
        }

        public Color FillColor
        {
            get => _fillColor;
            set { _fillColor = value; _dirty = true; }
        }

        /// <summary>[Figma 23:6 opt-in] 링 두께 배수(반지름 기준). Figma donut innerRadius 0.8 → 0.2.</summary>
        public float ThicknessFactor
        {
            get => _thicknessFactor;
            set { _thicknessFactor = Mathf.Clamp(value, 0.02f, 0.9f); _dirty = true; }
        }

        /// <summary>[Figma 23:6 opt-in] 링 외곽 여백(px). Figma는 가장자리 밀착 → 0.</summary>
        public float OuterInset
        {
            get => _outerInset;
            set { _outerInset = Mathf.Max(0f, value); _dirty = true; }
        }

        /// <summary>[Figma 23:6 opt-in] 배경(전원) 링 색. Figma outer-track = 백색 저알파.</summary>
        public Color BackgroundRingColor
        {
            get => _backgroundColor;
            set { _backgroundColor = value; _dirty = true; }
        }

        /// <summary>[Figma 23:6 opt-in] 진행 arc 2중 글로우 패스(Figma DROP_SHADOW 모사).</summary>
        public bool ProgressGlowEnabled
        {
            get => _glowEnabled;
            set { _glowEnabled = value; _dirty = true; }
        }

        /// <summary>[Figma 23:6 opt-in] arc 끝 흰 tip 세그먼트(Figma progress-tip 모사).</summary>
        public bool TipEnabled
        {
            get => _tipEnabled;
            set { _tipEnabled = value; _dirty = true; }
        }

        /// <summary>값 변경을 다음 틱에 반영 (외부 폴링 루프에서 호출).</summary>
        public void CommitIfDirty()
        {
            if (_dirty)
            {
                _dirty = false;
                MarkDirtyRepaint();   // 재판정: repaint마다 generateVisualContent 재실행 경로
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mc)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (w <= 0 || h <= 0) return;
            float radius = Mathf.Min(w, h) * 0.5f;
            float thickness = radius * _thicknessFactor;              // 링 두께
            float midR = radius - thickness * 0.5f - _outerInset;

            // 배경 링(전원) — 설정 색(기본 디머 / Figma 계약은 백색 저알파)
            if (_backgroundColor.a > 0.001f)
                WriteRing(mc, midR, thickness, 1f, _backgroundColor);
            // 진행 arc 글로우 패스 — Figma DROP_SHADOW(오렌지 15.9px@0.5 + 5.3px@0.25) 모사
            if (_glowEnabled && _fraction > 0.001f)
            {
                Color c = _fillColor;
                WriteRing(mc, midR, thickness * 2.1f, _fraction, new Color(c.r, c.g, c.b, c.a * 0.16f));
                WriteRing(mc, midR, thickness * 1.45f, _fraction, new Color(c.r, c.g, c.b, c.a * 0.34f));
            }
            // 전경 링 — 시계방향 fill (12시 시작)
            if (_fraction > 0.001f)
                WriteRing(mc, midR, thickness, _fraction, _fillColor);
            // 진행 arc 끝 흰 tip — Figma progress-tip(백색 + 백색 글로우) 모사
            if (_tipEnabled && _fraction > 0.02f)
                WriteTip(mc, midR, thickness);
        }

        /// <summary>호 링 메시 기록 — startAngle=12시(-90°), 시계방향 sweep.</summary>
        private void WriteRing(MeshGenerationContext mc, float midR, float thickness, float fraction, Color color)
        {
            int seg = Mathf.Max(8, Mathf.CeilToInt(_segments * fraction));
            var verts = new Vertex[(seg + 1) * 2];
            var tris = new ushort[seg * 6];

            float w = resolvedStyle.width, h = resolvedStyle.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);

            for (int i = 0; i <= seg; i++)
            {
                float t = (float)i / seg;
                // 12시 기준 시계방향(+) — Unity 패널 y는 하향이므로 +각도가 시계방향.
                // [2026-10-11] DrainClockwise면 시작 에지가 -90°+360°×(1-f)로 이동(소모가 시계방향).
                float ang = ArcStartDegrees(fraction, _drainClockwise) * Mathf.Deg2Rad + t * 360f * Mathf.Deg2Rad * fraction;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                int baseIdx = i * 2;
                verts[baseIdx] = new Vertex
                {
                    position = center + dir * (midR - thickness * 0.5f),
                    tint = color
                };
                verts[baseIdx + 1] = new Vertex
                {
                    position = center + dir * (midR + thickness * 0.5f),
                    tint = color
                };
                if (i < seg)
                {
                    int o = i * 6;
                    int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                    tris[o] = (ushort)a; tris[o + 1] = (ushort)c; tris[o + 2] = (ushort)b;
                    tris[o + 3] = (ushort)b; tris[o + 4] = (ushort)c; tris[o + 5] = (ushort)d;
                }
            }

            var mwd = mc.Allocate(verts.Length, tris.Length);
            mwd.SetAllVertices(verts);
            mwd.SetAllIndices(tris);
        }

        /// <summary>arc 끝(현재 각도)에 짧은 흰 tip 세그먼트 기록 — Figma progress-tip 계약.</summary>
        private void WriteTip(MeshGenerationContext mc, float midR, float thickness)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);
            float sweepDeg = 360f * Mathf.Max(_fraction, 0.02f);
            // [2026-10-11] DrainClockwise면 tip을 소모 경계(호 시작 에지)에 — 색이 빠져나가는 프론티어.
            float endAng = _drainClockwise
                ? ArcStartDegrees(_fraction, true)
                : -90f + sweepDeg;                          // 현재 arc 끝 각도(도)
            float spanDeg = 9f;                             // tip 폭 — Figma tip 시각 폭 근사
            const int seg = 4;
            var verts = new Vertex[(seg + 1) * 2];
            var tris = new ushort[seg * 6];
            var tipColor = new Color(1f, 1f, 1f, 0.95f);

            for (int i = 0; i <= seg; i++)
            {
                float ang = (endAng - spanDeg + spanDeg * 2f * i / seg) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                int baseIdx = i * 2;
                verts[baseIdx] = new Vertex
                {
                    position = center + dir * (midR - thickness * 0.5f),
                    tint = tipColor
                };
                verts[baseIdx + 1] = new Vertex
                {
                    position = center + dir * (midR + thickness * 0.5f),
                    tint = tipColor
                };
                if (i < seg)
                {
                    int o = i * 6;
                    int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                    tris[o] = (ushort)a; tris[o + 1] = (ushort)c; tris[o + 2] = (ushort)b;
                    tris[o + 3] = (ushort)b; tris[o + 4] = (ushort)c; tris[o + 5] = (ushort)d;
                }
            }

            var mwd = mc.Allocate(verts.Length, tris.Length);
            mwd.SetAllVertices(verts);
            mwd.SetAllIndices(tris);
        }
    }
}
