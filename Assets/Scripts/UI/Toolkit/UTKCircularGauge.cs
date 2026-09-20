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
            float thickness = radius * 0.28f;              // 링 두께
            float midR = radius - thickness * 0.5f - 2f;

            // 배경 링(전원) — 디머
            WriteRing(mc, midR, thickness, 1f, new Color(0f, 0f, 0f, 0.45f));
            // 전경 링 — 시계방향 fill (12시 시작)
            if (_fraction > 0.001f)
                WriteRing(mc, midR, thickness, _fraction, _fillColor);
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
    }
}
