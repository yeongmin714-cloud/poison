using UnityEngine;
using UnityEngine.UIElements;

namespace ProjectName.UI.Toolkit
{
    /// <summary>
    /// P22-5 — 원형 게이지 컨트롤 (generateVisualContent 벡터 호).
    /// IMGUI 폴리곤 바를 대체하는 고품질 게이지: 배경 링(디머) + 전경 링(팀색)이
    /// 12시 방향 기준 **시계방향**으로 채워지고, 값 감소 = 시계방향 소모로 표현된다.
    /// 해상도 무관 벡터(96세그먼트) — 폴리곤 계단 없음. 프리미티브 금지 규약(P22-6) 준수.
    /// </summary>
    public class UTKCircularGauge : VisualElement
    {
        private float _fraction = 1f;
        private Color _fillColor = new Color(0.2f, 0.75f, 0.35f, 1f);
        private int _segments = 96;
        private bool _dirty;
        private Color _trackColor = new Color(0.10f, 0.11f, 0.13f, 0.92f);  // 배경 링(트랙) — 다크
        private bool _showTicks;
        private Color _tickColor = new Color(0.03f, 0.04f, 0.05f, 0.6f);    // 세그먼트 마디 색
        private float _thicknessRatio = 0.20f;                              // 반지름 대비 링 두께 비율(0.15~0.22)
        private const int TickCount = 12;                                   // 세그먼트 마디 개수

        public UTKCircularGauge()
        {
            pickingMode = PickingMode.Ignore;   // [P23-3] 게이지 루트 클릭 흡수 방지
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

        /// <summary>배경 링(트랙) 색 — 기본 다크 rgba(0.10,0.11,0.13,0.92).</summary>
        public Color TrackColor
        {
            get => _trackColor;
            set { _trackColor = value; _dirty = true; }
        }

        /// <summary>세그먼트 마디 표시 — 링 두께를 가로지르는 방향 눈금 12개(12시부터 30° 간격).</summary>
        public void ShowSegmentTicks(bool show)
        {
            if (_showTicks == show) return;
            _showTicks = show;
            _dirty = true;
        }

        /// <summary>세그먼트 눈금 색.</summary>
        public Color SegmentTickColor
        {
            get => _tickColor;
            set { _tickColor = value; _dirty = true; }
        }

        /// <summary>링 두께 비율(반지름 대비) — 0.15~0.22 클램프.</summary>
        public float ThicknessRatio
        {
            get => _thicknessRatio;
            set { _thicknessRatio = Mathf.Clamp(value, 0.15f, 0.22f); _dirty = true; }
        }

        /// <summary>값 변경을 다음 틱에 반영 (외부 폴링 루프에서 호출).</summary>
        public void CommitIfDirty()
        {
            if (_dirty)
            {
                _dirty = false;
                MarkDirtyRepaint();
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext mc)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            if (w <= 0 || h <= 0) return;
            float radius = Mathf.Min(w, h) * 0.5f;
            float thickness = radius * _thicknessRatio;    // 링 두께(비율 프라퍼티)
            float midR = radius - thickness * 0.5f - 2f;

            // 배경 링(전원) — 트랙색
            WriteRing(mc, midR, thickness, 1f, _trackColor);
            // 전경 링 — 시계방향 fill (12시 시작)
            if (_fraction > 0.001f)
                WriteRing(mc, midR, thickness, _fraction, _fillColor);
            // 세그먼트 마디 — 링 위 방향 눈금
            if (_showTicks)
                WriteTicks(mc, midR, thickness, _tickColor);
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
                // 12시 시작(-90°) + 시계방향(+) — Unity 패널 y는 하향이므로 +각도가 시계방향
                float ang = -90f * Mathf.Deg2Rad + t * 360f * Mathf.Deg2Rad * fraction;
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

        /// <summary>세그먼트 마디 — 링 두께를 가로지르는 짧은 방향 스파이크 12개(정점 4/눈금).</summary>
        private void WriteTicks(MeshGenerationContext mc, float midR, float thickness, Color color)
        {
            float w = resolvedStyle.width, h = resolvedStyle.height;
            var center = new Vector2(w * 0.5f, h * 0.5f);

            float halfLen = thickness * 0.62f;   // 링 두께를 살짝 넘게 가로지름
            float halfW = 1.1f;                  // 눈금 선두께(px)
            var verts = new Vertex[TickCount * 4];
            var tris = new ushort[TickCount * 6];

            for (int i = 0; i < TickCount; i++)
            {
                float ang = (-90f + i * 360f / TickCount) * Mathf.Deg2Rad;   // 12시 시작 시계방향
                var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                var perp = new Vector2(-dir.y, dir.x);
                int b = i * 4;
                verts[b] = new Vertex { position = center + dir * (midR - halfLen) - perp * halfW, tint = color };
                verts[b + 1] = new Vertex { position = center + dir * (midR + halfLen) - perp * halfW, tint = color };
                verts[b + 2] = new Vertex { position = center + dir * (midR + halfLen) + perp * halfW, tint = color };
                verts[b + 3] = new Vertex { position = center + dir * (midR - halfLen) + perp * halfW, tint = color };
                int o = i * 6;
                tris[o] = (ushort)b; tris[o + 1] = (ushort)(b + 2); tris[o + 2] = (ushort)(b + 1);
                tris[o + 3] = (ushort)(b + 1); tris[o + 4] = (ushort)(b + 2); tris[o + 5] = (ushort)(b + 3);
            }

            var mwd = mc.Allocate(verts.Length, tris.Length);
            mwd.SetAllVertices(verts);
            mwd.SetAllIndices(tris);
        }
    }
}
