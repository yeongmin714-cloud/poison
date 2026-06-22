#nullable disable
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 UI-01: 장식 테두리 렌더러.
    /// 5종 모서리/테두리 장식(Filigree, Rune, Thorn, Star, Shield)을 IMGUI로 렌더링합니다.
    /// </summary>
    public static class DecorativeBorderRenderer
    {
        private static GUIStyle _borderStyle;

        /// <summary>
        /// 지정된 Rect에 테두리 장식을 그립니다.
        /// </summary>
        /// <param name="rect">창 영역</param>
        /// <param name="type">테두리 종류</param>
        /// <param name="color">테두리 색상</param>
        /// <param name="thickness">테두리 두께</param>
        public static void DrawBorder(Rect rect, UIDesignTheme.BorderType type, Color color, float thickness)
        {
            if (rect.width <= 0 || rect.height <= 0 || thickness <= 0)
                return;

            EnsureStyle(color);

            // 4면 테두리 그리기
            // 상단
            DrawLine(rect.x, rect.y, rect.x + rect.width, rect.y, color, thickness);
            // 하단
            DrawLine(rect.x, rect.y + rect.height, rect.x + rect.width, rect.y + rect.height, color, thickness);
            // 좌측
            DrawLine(rect.x, rect.y, rect.x, rect.y + rect.height, color, thickness);
            // 우측
            DrawLine(rect.x + rect.width, rect.y, rect.x + rect.width, rect.y + rect.height, color, thickness);

            // 4코너 장식
            DrawCornerDecoration(rect, type, color, thickness);
        }

        // ================================================================
        // 내부: 코너 장식
        // ================================================================

        private static void DrawCornerDecoration(Rect rect, UIDesignTheme.BorderType type, Color color, float thickness)
        {
            float cornerSize = Mathf.Min(rect.width, rect.height) * 0.12f;
            cornerSize = Mathf.Clamp(cornerSize, 10f, 50f);

            switch (type)
            {
                case UIDesignTheme.BorderType.Filigree:
                    DrawFiligreeCorners(rect, color, cornerSize, thickness);
                    break;
                case UIDesignTheme.BorderType.Rune:
                    DrawRuneCorners(rect, color, cornerSize, thickness);
                    break;
                case UIDesignTheme.BorderType.Thorn:
                    DrawThornBorder(rect, color, thickness);
                    break;
                case UIDesignTheme.BorderType.Star:
                    DrawStarCorners(rect, color, cornerSize, thickness);
                    break;
                case UIDesignTheme.BorderType.Shield:
                    DrawShieldCorners(rect, color, cornerSize, thickness);
                    break;
            }
        }

        /// <summary>필그리 장식: 각 코너에 곡선 꼬임</summary>
        private static void DrawFiligreeCorners(Rect rect, Color color, float size, float thickness)
        {
            // 좌상단: 곡선
            DrawCurve(rect.x, rect.y, rect.x + size, rect.y, rect.x, rect.y + size, color, thickness);
            // 우상단
            DrawCurve(rect.x + rect.width, rect.y, rect.x + rect.width - size, rect.y, rect.x + rect.width, rect.y + size, color, thickness);
            // 좌하단
            DrawCurve(rect.x, rect.y + rect.height, rect.x + size, rect.y + rect.height, rect.x, rect.y + rect.height - size, color, thickness);
            // 우하단
            DrawCurve(rect.x + rect.width, rect.y + rect.height, rect.x + rect.width - size, rect.y + rect.height, rect.x + rect.width, rect.y + rect.height - size, color, thickness);
        }

        /// <summary>룬 장식: 각 코너에 마법 룬 심볼</summary>
        private static void DrawRuneCorners(Rect rect, Color color, float size, float thickness)
        {
            float cx, cy;
            // 좌상단: 다이아몬드 + 수직선
            cx = rect.x + size * 0.5f;
            cy = rect.y + size * 0.5f;
            DrawDiamond(cx, cy, size * 0.3f, color, thickness);
            DrawLine(cx, rect.y, cx, rect.y + size, color, thickness);

            // 우상단
            cx = rect.x + rect.width - size * 0.5f;
            cy = rect.y + size * 0.5f;
            DrawDiamond(cx, cy, size * 0.3f, color, thickness);
            DrawLine(cx, rect.y, cx, rect.y + size, color, thickness);

            // 좌하단
            cx = rect.x + size * 0.5f;
            cy = rect.y + rect.height - size * 0.5f;
            DrawDiamond(cx, cy, size * 0.3f, color, thickness);
            DrawLine(cx, rect.y + rect.height - size, cx, rect.y + rect.height, color, thickness);

            // 우하단
            cx = rect.x + rect.width - size * 0.5f;
            cy = rect.y + rect.height - size * 0.5f;
            DrawDiamond(cx, cy, size * 0.3f, color, thickness);
            DrawLine(cx, rect.y + rect.height - size, cx, rect.y + rect.height, color, thickness);
        }

        /// <summary>가시/철조망: 테두리 따라 가시</summary>
        private static void DrawThornBorder(Rect rect, Color color, float thickness)
        {
            float spacing = 20f;
            float thornLen = 8f;

            // 상단 가시
            for (float x = rect.x; x <= rect.x + rect.width; x += spacing)
            {
                DrawLine(x, rect.y, x - thornLen, rect.y - thornLen, color, thickness);
                DrawLine(x, rect.y, x + thornLen, rect.y - thornLen, color, thickness);
            }
            // 하단 가시
            for (float x = rect.x; x <= rect.x + rect.width; x += spacing)
            {
                DrawLine(x, rect.y + rect.height, x - thornLen, rect.y + rect.height + thornLen, color, thickness);
                DrawLine(x, rect.y + rect.height, x + thornLen, rect.y + rect.height + thornLen, color, thickness);
            }
            // 좌측 가시
            for (float y = rect.y; y <= rect.y + rect.height; y += spacing)
            {
                DrawLine(rect.x, y, rect.x - thornLen, y - thornLen, color, thickness);
                DrawLine(rect.x, y, rect.x - thornLen, y + thornLen, color, thickness);
            }
            // 우측 가시
            for (float y = rect.y; y <= rect.y + rect.height; y += spacing)
            {
                DrawLine(rect.x + rect.width, y, rect.x + rect.width + thornLen, y - thornLen, color, thickness);
                DrawLine(rect.x + rect.width, y, rect.x + rect.width + thornLen, y + thornLen, color, thickness);
            }
        }

        /// <summary>별 코너: 각 코너에 별 모양</summary>
        private static void DrawStarCorners(Rect rect, Color color, float size, float thickness)
        {
            DrawStar(rect.x + size * 0.5f, rect.y + size * 0.5f, size * 0.4f, color, thickness);               // 좌상
            DrawStar(rect.x + rect.width - size * 0.5f, rect.y + size * 0.5f, size * 0.4f, color, thickness);     // 우상
            DrawStar(rect.x + size * 0.5f, rect.y + rect.height - size * 0.5f, size * 0.4f, color, thickness);   // 좌하
            DrawStar(rect.x + rect.width - size * 0.5f, rect.y + rect.height - size * 0.5f, size * 0.4f, color, thickness); // 우하
        }

        /// <summary>방패 코너: 각 코너에 방패 모양</summary>
        private static void DrawShieldCorners(Rect rect, Color color, float size, float thickness)
        {
            DrawShield(rect.x + size * 0.5f, rect.y + size * 0.5f, size * 0.5f, color, thickness);               // 좌상
            DrawShield(rect.x + rect.width - size * 0.5f, rect.y + size * 0.5f, size * 0.5f, color, thickness);   // 우상
            DrawShield(rect.x + size * 0.5f, rect.y + rect.height - size * 0.5f, size * 0.5f, color, thickness); // 좌하
            DrawShield(rect.x + rect.width - size * 0.5f, rect.y + rect.height - size * 0.5f, size * 0.5f, color, thickness); // 우하
        }

        // ================================================================
        // 기본 도형 드로잉 헬퍼
        // ================================================================

        private static void EnsureStyle(Color color)
        {
            if (_borderStyle == null)
                _borderStyle = new GUIStyle();
            _borderStyle.normal.background = ProceduralTextureGenerator.MakeTexture(1, 1, color);
        }

        private static void DrawLine(float x1, float y1, float x2, float y2, Color color, float thickness)
        {
            if (Mathf.Approximately(x1, x2))
            {
                // 수직선
                float minY = Mathf.Min(y1, y2);
                float maxY = Mathf.Max(y1, y2);
                GUI.DrawTexture(new Rect(x1 - thickness * 0.5f, minY, thickness, maxY - minY),
                    ProceduralTextureGenerator.MakeTexture(1, 1, color));
            }
            else if (Mathf.Approximately(y1, y2))
            {
                // 수평선
                float minX = Mathf.Min(x1, x2);
                float maxX = Mathf.Max(x1, x2);
                GUI.DrawTexture(new Rect(minX, y1 - thickness * 0.5f, maxX - minX, thickness),
                    ProceduralTextureGenerator.MakeTexture(1, 1, color));
            }
            else
            {
                // 대각선: 경사진 Rect 사용
                float minX = Mathf.Min(x1, x2);
                float maxX = Mathf.Max(x1, x2);
                float minY = Mathf.Min(y1, y2);
                float maxY = Mathf.Max(y1, y2);
                float angle = Mathf.Atan2(y2 - y1, x2 - x1) * Mathf.Rad2Deg;
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, new Vector2((x1 + x2) * 0.5f, (y1 + y2) * 0.5f));
                GUI.DrawTexture(new Rect(minX, minY - thickness * 0.5f, maxX - minX, thickness),
                    ProceduralTextureGenerator.MakeTexture(1, 1, color));
                GUI.matrix = matrix;
            }
        }

        private static void DrawCurve(float x1, float y1, float x2, float y2, float x3, float y3, Color color, float thickness)
        {
            // 단순화된 2-세그먼트 곡선: 직선 2개로 근사
            float midX = (x1 + x2 + x3) / 3f;
            float midY = (y1 + y2 + y3) / 3f;
            DrawLine(x1, y1, midX, midY, color, thickness);
            DrawLine(midX, midY, x3, y3, color, thickness);
        }

        private static void DrawDiamond(float cx, float cy, float halfSize, Color color, float thickness)
        {
            DrawLine(cx, cy - halfSize, cx + halfSize, cy, color, thickness);
            DrawLine(cx + halfSize, cy, cx, cy + halfSize, color, thickness);
            DrawLine(cx, cy + halfSize, cx - halfSize, cy, color, thickness);
            DrawLine(cx - halfSize, cy, cx, cy - halfSize, color, thickness);
        }

        private static void DrawStar(float cx, float cy, float radius, Color color, float thickness)
        {
            // 5각 별 (단순화: 5개 선분)
            for (int i = 0; i < 5; i++)
            {
                float angle1 = (i * 2f * Mathf.PI / 5f) - Mathf.PI / 2f;
                float angle2 = ((i + 2) * 2f * Mathf.PI / 5f) - Mathf.PI / 2f;
                float x1 = cx + Mathf.Cos(angle1) * radius;
                float y1 = cy + Mathf.Sin(angle1) * radius;
                float x2 = cx + Mathf.Cos(angle2) * radius;
                float y2 = cy + Mathf.Sin(angle2) * radius;
                DrawLine(x1, y1, x2, y2, color, thickness);
            }
        }

        private static void DrawShield(float cx, float cy, float halfSize, Color color, float thickness)
        {
            // 방패: 상단 직선 + 측면 대각선 + 하단 V
            float top = cy - halfSize;
            float bottom = cy + halfSize;
            float left = cx - halfSize;
            float right = cx + halfSize;
            float midBot = cy + halfSize * 0.6f;

            DrawLine(left, top, right, top, color, thickness);                            // 상단
            DrawLine(right, top, right + halfSize * 0.3f, midBot, color, thickness);      // 우측
            DrawLine(right + halfSize * 0.3f, midBot, cx, bottom, color, thickness);       // 우하단
            DrawLine(cx, bottom, left - halfSize * 0.3f, midBot, color, thickness);        // 좌하단
            DrawLine(left - halfSize * 0.3f, midBot, left, top, color, thickness);         // 좌측
        }
    }
}