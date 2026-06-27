using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// 절차적 아이콘 생성기 — C8-35.
    /// 카테고리별 형태와 색상으로 32x32 RGBA32 텍스처를 생성.
    /// Core 네임스페이스에 위치하여 모든 asmdef에서 참조 가능.
    /// </summary>
    public static class ProceduralIconGenerator
    {
        private const int ICON_SIZE = 32;
        private static readonly Dictionary<string, Texture2D> _textureCache = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>();

        // ===== 카테고리별 기본 색상 =====
        private static readonly Color ColorHerb = new Color(0.20f, 0.70f, 0.20f);      // 초록
        private static readonly Color ColorMeat = new Color(0.65f, 0.30f, 0.10f);      // 갈색
        private static readonly Color ColorFood = new Color(0.90f, 0.60f, 0.10f);      // 주황
        private static readonly Color ColorPotion = new Color(0.70f, 0.25f, 0.75f);    // 보라 (기본)
        private static readonly Color ColorDrug = new Color(0.60f, 0.15f, 0.60f);      // 보라 (마약)
        private static readonly Color ColorMaterial = new Color(0.50f, 0.50f, 0.50f);  // 회색
        private static readonly Color ColorWeapon = new Color(0.80f, 0.20f, 0.20f);    // 빨강
        private static readonly Color ColorArmor = new Color(0.20f, 0.30f, 0.70f);     // 파랑
        private static readonly Color ColorTool = new Color(0.60f, 0.40f, 0.20f);      // 갈색
        private static readonly Color ColorQuest = new Color(0.80f, 0.75f, 0.15f);     // 노랑
        private static readonly Color ColorArrow = new Color(0.55f, 0.35f, 0.20f);     // 화살 (짙은 갈색)

        // ===== 포션 세부 색상 =====
        private static readonly Color PotionRed = new Color(0.85f, 0.15f, 0.15f);
        private static readonly Color PotionPurple = new Color(0.65f, 0.20f, 0.70f);
        private static readonly Color PotionYellow = new Color(0.85f, 0.80f, 0.15f);
        private static readonly Color PotionSilver = new Color(0.75f, 0.75f, 0.80f);
        private static readonly Color PotionGreen = new Color(0.20f, 0.75f, 0.20f);

        // ===== 테두리 색상 =====
        private static readonly Color BorderBright = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color BorderDim = new Color(1f, 1f, 1f, 0.25f);
        private static readonly Color BorderRare = new Color(1f, 0.85f, 0.20f, 0.95f); // 희귀 테두리 (황금)

        /// <summary>
        /// 아이템 ID와 카테고리로 아이콘 Texture2D 생성 (캐싱 있음).
        /// </summary>
        public static Texture2D GenerateIcon(string itemId, PlayerInventory.ItemCategory category, int maxDurability = 0)
        {
            string cacheKey = $"{itemId}_{maxDurability}";
            if (_textureCache.TryGetValue(cacheKey, out var cached))
                return cached;

            var tex = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
            tex.hideFlags = HideFlags.HideAndDontSave;
            Color fillColor = GetCategoryColor(category, itemId);
            Color borderColor = maxDurability > 0 ? BorderBright : BorderDim;

            // 투명 배경
            Color[] clear = new Color[ICON_SIZE * ICON_SIZE];
            for (int i = 0; i < clear.Length; i++)
                clear[i] = Color.clear;
            tex.SetPixels(clear);

            // 형태 그리기
            DrawShape(tex, category, fillColor);

            // 테두리 그리기
            DrawBorder(tex, borderColor, maxDurability > 0);

            tex.Apply();
            _textureCache[cacheKey] = tex;
            return tex;
        }

        /// <summary>
        /// ItemData에서 Sprite를 가져오거나 생성 (캐싱 있음).
        /// ItemData.icon에 할당 가능.
        /// </summary>
        public static Sprite GetOrCreateIcon(PlayerInventory.ItemData item)
        {
            if (item == null) return null;
            if (item.icon != null) return item.icon;

            string cacheKey = $"{item.id}_{item.maxDurability}";
            if (_spriteCache.TryGetValue(cacheKey, out var cached))
                return cached;

            Texture2D tex = GenerateIcon(item.id, item.category, item.maxDurability);
            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, ICON_SIZE, ICON_SIZE), new Vector2(0.5f, 0.5f), 32f);
            sprite.name = $"icon_{item.id}";
            _spriteCache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>
        /// 모든 정적 ItemData의 아이콘을 한 번에 생성 (GameManager.Start 등에서 호출).
        /// </summary>
        public static void GenerateAllStaticIcons()
        {
            var fields = typeof(PlayerInventory).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var field in fields)
            {
                try
                {
                    if (field.FieldType == typeof(PlayerInventory.ItemData))
                    {
                        var item = field.GetValue(null) as PlayerInventory.ItemData;
                        if (item != null && item.icon == null)
                        {
                            item.icon = GetOrCreateIcon(item);
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"[ProceduralIconGenerator] 아이콘 생성 중 오류 (field={field.Name}): {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 캐시 초기화 (테스트용).
        /// </summary>
        public static void ClearCache()
        {
            foreach (var kv in _textureCache)
            {
                if (kv.Value != null)
                    UnityEngine.Object.DestroyImmediate(kv.Value);
            }
            foreach (var kv in _spriteCache)
            {
                if (kv.Value != null)
                    UnityEngine.Object.DestroyImmediate(kv.Value);
            }
            _textureCache.Clear();
            _spriteCache.Clear();
        }

        // ===================================================================
        // 색상 결정
        // ===================================================================
        private static Color GetCategoryColor(PlayerInventory.ItemCategory category, string itemId)
        {
            if (category == PlayerInventory.ItemCategory.Potion)
            {
                return GetPotionColor(itemId);
            }
            return category switch
            {
                PlayerInventory.ItemCategory.Herb => ColorHerb,
                PlayerInventory.ItemCategory.Meat => ColorMeat,
                PlayerInventory.ItemCategory.Food => ColorFood,
                PlayerInventory.ItemCategory.Drug => ColorDrug,
                PlayerInventory.ItemCategory.Material => ColorMaterial,
                PlayerInventory.ItemCategory.Weapon => ColorWeapon,
                PlayerInventory.ItemCategory.Armor => ColorArmor,
                PlayerInventory.ItemCategory.Tool => ColorTool,
                PlayerInventory.ItemCategory.Quest => ColorQuest,
                PlayerInventory.ItemCategory.Arrow => ColorArrow,
                _ => Color.gray,
            };
        }

        private static Color GetPotionColor(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return ColorPotion;
            string id = itemId.ToLowerInvariant();
            if (id.Contains("red")) return PotionRed;
            if (id.Contains("purple")) return PotionPurple;
            if (id.Contains("yellow")) return PotionYellow;
            if (id.Contains("silver")) return PotionSilver;
            if (id.Contains("green")) return PotionGreen;
            return ColorPotion; // 기본 보라
        }

        // ===================================================================
        // 형태 그리기
        // ===================================================================
        private static void DrawShape(Texture2D tex, PlayerInventory.ItemCategory category, Color color)
        {
            switch (category)
            {
                case PlayerInventory.ItemCategory.Herb: DrawLeaf(tex, color); break;
                case PlayerInventory.ItemCategory.Potion: DrawBottle(tex, color); break;
                case PlayerInventory.ItemCategory.Drug: DrawSyringe(tex, color); break;
                case PlayerInventory.ItemCategory.Meat: DrawMeat(tex, color); break;
                case PlayerInventory.ItemCategory.Food: DrawPlate(tex, color); break;
                case PlayerInventory.ItemCategory.Material: DrawIngot(tex, color); break;
                case PlayerInventory.ItemCategory.Weapon: DrawSword(tex, color); break;
                case PlayerInventory.ItemCategory.Armor: DrawShield(tex, color); break;
                case PlayerInventory.ItemCategory.Tool: DrawPickaxe(tex, color); break;
                case PlayerInventory.ItemCategory.Quest: DrawDocument(tex, color); break;
                case PlayerInventory.ItemCategory.Arrow: DrawArrow(tex, color); break;
                default: DrawCircle(tex, 16, 16, 10, color); break;
            }
        }

        // ===================================================================
        // 개별 형태 드로잉 (32x32 기준)
        // ===================================================================

        /// <summary>잎사귀 모양</summary>
        private static void DrawLeaf(Texture2D tex, Color color)
        {
            // 잎사귀: 중앙 세로로 긴 타원형
            for (int y = 6; y <= 26; y++)
            {
                float t = (y - 6f) / 20f; // 0~1
                int halfWidth = Mathf.RoundToInt(6f * Mathf.Sin(t * Mathf.PI));
                for (int x = 16 - halfWidth; x <= 16 + halfWidth; x++)
                {
                    tex.SetPixel(x, y, color);
                }
            }
            // 줄기
            for (int y = 24; y <= 28; y++)
                tex.SetPixel(16, y, new Color(0.3f, 0.5f, 0.15f));
        }

        /// <summary>병 모양</summary>
        private static void DrawBottle(Texture2D tex, Color color)
        {
            // 목 (좁은 부분)
            for (int y = 6; y <= 12; y++)
                for (int x = 13; x <= 19; x++)
                    tex.SetPixel(x, y, color);
            // 몸통 (넓은 부분)
            for (int y = 12; y <= 26; y++)
            {
                int halfW = (y < 14) ? 5 : 8;
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 마개
            for (int y = 4; y <= 6; y++)
                for (int x = 14; x <= 18; x++)
                    tex.SetPixel(x, y, new Color(0.5f, 0.3f, 0.1f));
        }

        /// <summary>주사기 모양</summary>
        private static void DrawSyringe(Texture2D tex, Color color)
        {
            // 바늘
            for (int y = 4; y <= 10; y++)
                tex.SetPixel(16, y, new Color(0.8f, 0.8f, 0.8f));
            // 몸통 (원통)
            for (int y = 10; y <= 22; y++)
                for (int x = 12; x <= 20; x++)
                    tex.SetPixel(x, y, color);
            // 플런저
            for (int y = 22; y <= 26; y++)
                for (int x = 13; x <= 19; x++)
                    tex.SetPixel(x, y, new Color(0.6f, 0.6f, 0.6f));
            // 십자 표시
            tex.SetPixel(14, 16, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(15, 16, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(16, 14, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(16, 15, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(16, 16, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(16, 17, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(16, 18, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(17, 16, new Color(1f, 1f, 1f, 0.6f));
            tex.SetPixel(18, 16, new Color(1f, 1f, 1f, 0.6f));
        }

        /// <summary>고기 덩어리</summary>
        private static void DrawMeat(Texture2D tex, Color color)
        {
            // 불규칙한 덩어리
            for (int y = 8; y <= 24; y++)
            {
                float t = (y - 8f) / 16f;
                int offset = Mathf.RoundToInt(2f * Mathf.Sin(t * Mathf.PI * 2f));
                int halfW = 7 + offset;
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 지방 줄 (밝은 부분)
            for (int x = 10; x <= 22; x++)
                tex.SetPixel(x, 14, new Color(0.8f, 0.6f, 0.4f));
            for (int x = 12; x <= 20; x++)
                tex.SetPixel(x, 18, new Color(0.8f, 0.6f, 0.4f));
        }

        /// <summary>접시 모양</summary>
        private static void DrawPlate(Texture2D tex, Color color)
        {
            // 접시 외곽
            DrawCircle(tex, 16, 16, 12, color);
            // 접시 내부 (약간 어둡게)
            DrawCircle(tex, 16, 16, 8, new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, 1f));
            // 음식 (중앙)
            DrawCircle(tex, 16, 16, 4, new Color(0.9f, 0.7f, 0.3f));
        }

        /// <summary>주괴 모양</summary>
        private static void DrawIngot(Texture2D tex, Color color)
        {
            // 위쪽 (좁음)
            for (int y = 8; y <= 12; y++)
            {
                int halfW = 5 + (y - 8);
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 아래쪽 (넓음)
            for (int y = 12; y <= 24; y++)
            {
                int halfW = 9;
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 광택
            for (int x = 10; x <= 14; x++)
                tex.SetPixel(x, 14, new Color(0.7f, 0.7f, 0.7f));
        }

        /// <summary>검 모양</summary>
        private static void DrawSword(Texture2D tex, Color color)
        {
            // 칼날
            for (int y = 4; y <= 20; y++)
            {
                int halfW = (y < 16) ? 2 : 3;
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 칼날 끝 (뾰족)
            tex.SetPixel(16, 3, color);
            tex.SetPixel(15, 4, color);
            tex.SetPixel(17, 4, color);
            // 가드
            for (int x = 10; x <= 22; x++)
                tex.SetPixel(x, 20, new Color(0.6f, 0.5f, 0.2f));
            // 손잡이
            for (int y = 21; y <= 26; y++)
                for (int x = 14; x <= 18; x++)
                    tex.SetPixel(x, y, new Color(0.4f, 0.25f, 0.1f));
            // 손잡이 끝
            for (int x = 13; x <= 19; x++)
                tex.SetPixel(x, 27, new Color(0.6f, 0.5f, 0.2f));
        }

        /// <summary>방패 모양</summary>
        private static void DrawShield(Texture2D tex, Color color)
        {
            // 방패 본체
            for (int y = 6; y <= 24; y++)
            {
                float t = (y - 6f) / 18f;
                int halfW;
                if (t < 0.5f)
                    halfW = Mathf.RoundToInt(6f + 4f * (t * 2f));
                else
                    halfW = Mathf.RoundToInt(10f - 4f * ((t - 0.5f) * 2f));
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 방패 중앙 장식
            DrawCircle(tex, 16, 15, 4, new Color(0.8f, 0.7f, 0.2f));
            // 방패 테두리 강조
            for (int x = 7; x <= 25; x++)
            {
                tex.SetPixel(x, 6, new Color(0.7f, 0.7f, 0.7f, 0.5f));
                tex.SetPixel(x, 24, new Color(0.7f, 0.7f, 0.7f, 0.5f));
            }
        }

        /// <summary>곡괭이 모양</summary>
        private static void DrawPickaxe(Texture2D tex, Color color)
        {
            // 곡괭이 머리 (갈고리)
            for (int x = 6; x <= 14; x++)
            {
                int y = 10 + Mathf.Abs(x - 10);
                tex.SetPixel(x, y, color);
                tex.SetPixel(x, y + 1, color);
            }
            // 곡괭이 머리 오른쪽
            for (int x = 14; x <= 20; x++)
            {
                int y = 10 + (x - 14);
                tex.SetPixel(x, y, color);
                tex.SetPixel(x, y + 1, color);
            }
            // 자루
            for (int y = 14; y <= 26; y++)
                for (int x = 15; x <= 17; x++)
                    tex.SetPixel(x, y, new Color(0.5f, 0.3f, 0.1f));
        }

        /// <summary>문서 모양</summary>
        private static void DrawDocument(Texture2D tex, Color color)
        {
            // 문서 배경
            for (int y = 6; y <= 26; y++)
                for (int x = 8; x <= 24; x++)
                    tex.SetPixel(x, y, color);
            // 글자 줄
            for (int row = 0; row < 4; row++)
            {
                int yy = 10 + row * 5;
                int lineLen = 10 - row;
                for (int x = 11; x <= 11 + lineLen; x++)
                    tex.SetPixel(x, yy, new Color(0.2f, 0.2f, 0.2f, 0.6f));
            }
            // 봉인 도장
            DrawCircle(tex, 20, 20, 3, new Color(0.8f, 0.2f, 0.2f));
        }

        /// <summary>화살 모양</summary>
        private static void DrawArrow(Texture2D tex, Color color)
        {
            // 화살촉 (삼각형)
            for (int y = 6; y <= 14; y++)
            {
                int halfW = (y - 6) / 2;
                for (int x = 16 - halfW; x <= 16 + halfW; x++)
                    tex.SetPixel(x, y, color);
            }
            // 화살대
            for (int y = 14; y <= 26; y++)
                for (int x = 15; x <= 17; x++)
                    tex.SetPixel(x, y, new Color(0.5f, 0.3f, 0.1f));
            // 깃털
            for (int y = 24; y <= 26; y++)
            {
                tex.SetPixel(13, y, new Color(0.7f, 0.2f, 0.2f));
                tex.SetPixel(14, y, new Color(0.7f, 0.2f, 0.2f));
                tex.SetPixel(18, y, new Color(0.7f, 0.2f, 0.2f));
                tex.SetPixel(19, y, new Color(0.7f, 0.2f, 0.2f));
            }
        }

        // ===================================================================
        // 기본 도형
        // ===================================================================
        private static void DrawCircle(Texture2D tex, int cx, int cy, int r, Color color)
        {
            for (int y = cy - r; y <= cy + r; y++)
            {
                for (int x = cx - r; x <= cx + r; x++)
                {
                    if (x < 0 || x >= ICON_SIZE || y < 0 || y >= ICON_SIZE) continue;
                    int dx = x - cx;
                    int dy = y - cy;
                    if (dx * dx + dy * dy <= r * r)
                        tex.SetPixel(x, y, color);
                }
            }
        }

        // ===================================================================
        // 테두리
        // ===================================================================
        private static void DrawBorder(Texture2D tex, Color borderColor, bool sharp)
        {
            int thickness = sharp ? 2 : 1;
            float alpha = sharp ? borderColor.a : borderColor.a * 0.3f;
            Color col = new Color(borderColor.r, borderColor.g, borderColor.b, alpha);

            for (int t = 0; t < thickness; t++)
            {
                int b = t;
                int e = ICON_SIZE - 1 - t;
                // 상
                for (int x = b; x <= e; x++)
                    BlendPixel(tex, x, b, col);
                // 하
                for (int x = b; x <= e; x++)
                    BlendPixel(tex, x, e, col);
                // 좌
                for (int y = b; y <= e; y++)
                    BlendPixel(tex, b, y, col);
                // 우
                for (int y = b; y <= e; y++)
                    BlendPixel(tex, e, y, col);
            }

            // 희귀 아이템 (maxDurability > 0) 모서리 강조
            if (sharp)
            {
                Color cornerCol = BorderRare;
                tex.SetPixel(1, 1, cornerCol);
                tex.SetPixel(2, 1, cornerCol);
                tex.SetPixel(1, 2, cornerCol);
                tex.SetPixel(ICON_SIZE - 2, 1, cornerCol);
                tex.SetPixel(ICON_SIZE - 3, 1, cornerCol);
                tex.SetPixel(ICON_SIZE - 2, 2, cornerCol);
                tex.SetPixel(1, ICON_SIZE - 2, cornerCol);
                tex.SetPixel(2, ICON_SIZE - 2, cornerCol);
                tex.SetPixel(1, ICON_SIZE - 3, cornerCol);
                tex.SetPixel(ICON_SIZE - 2, ICON_SIZE - 2, cornerCol);
                tex.SetPixel(ICON_SIZE - 3, ICON_SIZE - 2, cornerCol);
                tex.SetPixel(ICON_SIZE - 2, ICON_SIZE - 3, cornerCol);
            }
        }

        private static void BlendPixel(Texture2D tex, int x, int y, Color color)
        {
            if (x < 0 || x >= ICON_SIZE || y < 0 || y >= ICON_SIZE) return;
            Color existing = tex.GetPixel(x, y);
            float a = color.a;
            tex.SetPixel(x, y, Color.Lerp(existing, color, a));
        }
    }
}