using System.Collections.Generic;
using System.Linq;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.UI
{
    /// <summary>
    /// Phase 38.4 보조: 미니맵/지도에 축제 중인 영지 아이콘 표시.
    /// M 키 지도(MapWindow)에서 현재 축제가 진행 중인 영지를
    /// 빛나는 효과로 강조하여 표시합니다.
    /// </summary>
    public static class FestivalMapIndicator
    {
        // ===== 상수 =====
        private const float ICON_SIZE = 20f;
        private const float PULSE_SPEED = 2.5f; // 초당 깜빡임 속도
        private const float GLOW_ALPHA_MIN = 0.4f;
        private const float GLOW_ALPHA_MAX = 1.0f;

        // ===== 스타일 =====
        private static GUIStyle _iconStyle;
        private static GUIStyle _labelStyle;
        private static Texture2D _glowTexture;

        // ===== 상태 =====
        private static FestivalData _lastHoveredFestival;
        private static bool _showTooltip;

        // ================================================================
        // 공개 메서드
        // ================================================================

        /// <summary>
        /// 지도 위에 축제 영지 아이콘들을 그립니다.
        /// MapWindow.OnGUI 등에서 호출합니다.
        /// </summary>
        /// <param name="territoryScreenPositions">영지 ID → 화면 좌표 매핑</param>
        public static void OnFestivalMapIcons(Dictionary<string, Vector2> territoryScreenPositions)
        {
            var mgr = FestivalManager.Instance;
            if (mgr == null || !mgr.HasAnyActiveFestival || territoryScreenPositions == null)
                return;

            InitializeStyles();

            foreach (var festival in mgr.ActiveFestivals)
            {
                if (festival == null) continue;

                string territoryKey = festival.territoryId.ToString();

                if (territoryScreenPositions.TryGetValue(territoryKey, out var screenPos))
                {
                    DrawFestivalIcon(screenPos, festival);
                }
            }
        }

        /// <summary>
        /// 특정 영지가 축제 중인지 확인하고 해당 축제 데이터를 반환합니다.
        /// (MapWindow에서 영지 선택 시 툴팁 표시용)
        /// </summary>
        public static FestivalData GetFestivalAtTerritory(string territoryIdStr)
        {
            var mgr = FestivalManager.Instance;
            if (mgr == null) return null;
            return mgr.GetActiveFestivalAtTerritory(territoryIdStr);
        }

        /// <summary>
        /// 마우스 호버 시 축제 툴팁을 그립니다.
        /// </summary>
        /// <param name="mousePos">현재 마우스 위치</param>
        /// <param name="territoryScreenPositions">영지 ID → 화면 좌표 매핑</param>
        public static void OnFestivalTooltip(Vector2 mousePos, Dictionary<string, Vector2> territoryScreenPositions)
        {
            var mgr = FestivalManager.Instance;
            if (mgr == null || !mgr.HasAnyActiveFestival || territoryScreenPositions == null)
                return;

            InitializeStyles();

            foreach (var festival in mgr.ActiveFestivals)
            {
                if (festival == null) continue;

                string territoryKey = festival.territoryId.ToString();
                if (!territoryScreenPositions.TryGetValue(territoryKey, out var screenPos))
                    continue;

                // 마우스가 아이콘 범위 내에 있는지 확인
                float dist = Vector2.Distance(mousePos, screenPos);
                if (dist <= ICON_SIZE)
                {
                    DrawTooltip(screenPos, festival);
                    _lastHoveredFestival = festival;
                    _showTooltip = true;
                    return;
                }
            }

            // 호버 중이 아니면 툴팁 숨김
            if (_showTooltip)
            {
                _showTooltip = false;
                _lastHoveredFestival = null;
            }
        }

        // ================================================================
        // 내부 드로잉
        // ================================================================

        private static void DrawFestivalIcon(Vector2 screenPos, FestivalData festival)
        {
            float pulse = Mathf.Lerp(GLOW_ALPHA_MIN, GLOW_ALPHA_MAX,
                (Mathf.Sin(Time.time * PULSE_SPEED) + 1f) * 0.5f);

            Color iconColor = festival.festivalColor;
            iconColor.a = pulse;

            // 글로우 텍스처 (원형 빛)
            if (_glowTexture != null)
            {
                Color original = GUI.color;
                GUI.color = iconColor;

                float glowSize = ICON_SIZE * 2.5f;
                GUI.DrawTexture(
                    new Rect(screenPos.x - glowSize / 2f, screenPos.y - glowSize / 2f,
                        glowSize, glowSize),
                    _glowTexture,
                    ScaleMode.ScaleToFit);

                GUI.color = original;
            }

            // 이모지 아이콘
            string icon = festival.emoji;
            float iconSize = ICON_SIZE;
            GUI.Label(
                new Rect(screenPos.x - iconSize / 2f, screenPos.y - iconSize / 2f,
                    iconSize, iconSize),
                icon, _iconStyle);
        }

        private static void DrawTooltip(Vector2 screenPos, FestivalData festival)
        {
            float tooltipWidth = 220f;
            float tooltipHeight = 120f;

            float tx = screenPos.x + ICON_SIZE + 8f;
            float ty = screenPos.y - tooltipHeight / 2f;

            // 화면 밖으로 나가지 않도록 조정
            if (tx + tooltipWidth > Screen.width)
                tx = screenPos.x - tooltipWidth - ICON_SIZE - 8f;
            if (ty < 5f) ty = 5f;
            if (ty + tooltipHeight > Screen.height - 5f)
                ty = Screen.height - 5f - tooltipHeight;

            // 배경
            Color original = GUI.backgroundColor;
            GUI.backgroundColor = festival.festivalColor;
            GUI.Box(new Rect(tx - 1f, ty - 1f, tooltipWidth + 2f, tooltipHeight + 2f), "");
            GUI.backgroundColor = new Color(0.08f, 0.08f, 0.12f, 0.95f);
            GUI.Box(new Rect(tx, ty, tooltipWidth, tooltipHeight), "");
            GUI.backgroundColor = original;

            float cx = tx + 8f;
            float cy = ty + 6f;
            float cw = tooltipWidth - 16f;

            // 제목
            GUI.Label(new Rect(cx, cy, cw, 22f),
                $"{festival.emoji} {festival.festivalName}", _labelStyle);
            cy += 24f;

            // 영지
            GUI.Label(new Rect(cx, cy, cw, 18f),
                $"📍 {festival.territoryId}", _labelStyle);
            cy += 20f;

            // 기간
            GUI.Label(new Rect(cx, cy, cw, 18f),
                $"📅 Day {festival.startDay} ~ {festival.endDay}", _labelStyle);
            cy += 20f;

            // 효과 요약
            string effectSummary = festival.GetEffect().GetSummary();
            if (effectSummary.Length > 50)
                effectSummary = effectSummary.Substring(0, 47) + "...";
            GUI.Label(new Rect(cx, cy, cw, 28f),
                $"✨ {effectSummary}", _labelStyle);
        }

        // ================================================================
        // 스타일 초기화
        // ================================================================

        private static void InitializeStyles()
        {
            if (_iconStyle == null)
            {
                _iconStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(ICON_SIZE * 0.8f),
                    alignment = TextAnchor.MiddleCenter,
                    normal = new GUIStyleState { textColor = Color.white }
                };
            }

            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(Screen.height * 0.015f),
                    wordWrap = true,
                    alignment = TextAnchor.UpperLeft,
                    normal = new GUIStyleState { textColor = new Color(0.9f, 0.9f, 0.95f) }
                };
            }

            // 글로우 텍스처 생성 (원형 그라데이션)
            if (_glowTexture == null)
            {
                _glowTexture = CreateCircleTexture(64, new Color(1f, 1f, 1f, 0.3f));
            }
        }

        private static Texture2D CreateCircleTexture(int size, Color centerColor)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float half = size / 2f;
            float radius = half;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - half;
                    float dy = y - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - (dist / radius));
                    alpha = Mathf.Pow(alpha, 0.5f); // 부드러운 페이드
                    tex.SetPixel(x, y, new Color(centerColor.r, centerColor.g, centerColor.b, alpha * centerColor.a));
                }
            }

            tex.Apply();
            return tex;
        }

        /// <summary>모든 상태 초기화 (테스트용)</summary>
        public static void ResetAll()
        {
            _lastHoveredFestival = null;
            _showTooltip = false;
        }
    }
}