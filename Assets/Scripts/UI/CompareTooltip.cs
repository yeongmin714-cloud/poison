using UnityEngine;
using ProjectName.Core;
using ProjectName.Systems;

namespace ProjectName.UI
{
    /// <summary>
    /// 아이템 비교 툴팁 헬퍼 — IMGUI 기반.
    /// 장비 아이템(Weapon/Armor/Tool/Arrow)의 능력치 비교를 담당합니다.
    /// CompareTooltip.DrawComparison()을 호출하면 현재 장착 중인 같은 슬롯의
    /// 아이템과 능력치를 비교하여 툴팁 하단에 렌더링합니다.
    /// </summary>
    public static class CompareTooltip
    {
        // ===== 비교 GUI 상수 =====
        private static readonly Color ColorCompareHeader = new Color(0.50f, 0.45f, 0.40f, 1f);
        private static readonly Color ColorCompareBg = new Color(0.15f, 0.11f, 0.13f, 0.6f);
        private static readonly Color ColorCompareCurrent = new Color(0.70f, 0.65f, 0.60f, 1f);
        private static readonly Color ColorCompareNew = new Color(0.92f, 0.88f, 0.80f, 1f);
        private static readonly Color ColorCompareArrow = new Color(0.60f, 0.55f, 0.50f, 1f);
        private static readonly Color ColorCompareDivider = new Color(0.30f, 0.30f, 0.30f, 0.4f);

        private const float COMPARE_SECTION_PADDING = 4f;
        private const float COMPARE_LINE_HEIGHT = 22f;
        private const float COMPARE_HEADER_HEIGHT = 18f;
        private const float COMPARE_ARROW_HEIGHT = 18f;
        private const float COMPARE_SEPARATOR_HEIGHT = 6f;

        // 캐시된 GUIContent (GC 할당 방지)
        private static readonly GUIContent _gcCompare = new GUIContent();

        // 캐시된 GUIStyle (매 프레임 new 방지)
        private static GUIStyle _styleCompareLabel;
        private static GUIStyle _styleCompareCurrent;
        private static GUIStyle _styleCompareArrow;
        private static GUIStyle _styleCompareNew;
        private static bool _compareStylesInitialized;

        private static void InitCompareStyles()
        {
            if (_compareStylesInitialized) return;

            _styleCompareLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Italic,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorCompareHeader }
            };

            _styleCompareCurrent = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorCompareCurrent }
            };

            _styleCompareArrow = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorCompareArrow }
            };

            _styleCompareNew = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                normal = { textColor = ColorCompareNew }
            };

            _compareStylesInitialized = true;
        }

        /// <summary>
        /// 장비 카테고리인지 확인 (Weapon / Armor / Tool / Arrow)
        /// </summary>
        public static bool IsEquipmentCategory(PlayerInventory.ItemCategory category)
        {
            return category == PlayerInventory.ItemCategory.Weapon
                || category == PlayerInventory.ItemCategory.Armor
                || category == PlayerInventory.ItemCategory.Tool
                || category == PlayerInventory.ItemCategory.Arrow;
        }

        /// <summary>
        /// 아이템 카테고리에 대응하는 EquipmentManager.EquipmentSlot 반환.
        /// 매핑이 불가능하면 null 반환.
        /// </summary>
        public static EquipmentManager.EquipmentSlot? GetEquipmentSlot(PlayerInventory.ItemCategory category)
        {
            return category switch
            {
                PlayerInventory.ItemCategory.Weapon => EquipmentManager.EquipmentSlot.Weapon,
                PlayerInventory.ItemCategory.Armor  => EquipmentManager.EquipmentSlot.Armor,
                PlayerInventory.ItemCategory.Tool    => EquipmentManager.EquipmentSlot.Back,
                PlayerInventory.ItemCategory.Arrow   => EquipmentManager.EquipmentSlot.Weapon,
                _ => null
            };
        }

        /// <summary>
        /// 현재 장착 중인 같은 슬롯의 아이템을 찾아 ItemTooltipData로 반환.
        /// 장비가 없거나 EquipmentManager가 없으면 null 반환.
        /// </summary>
        public static ItemTooltipData? GetEquippedCompareData(PlayerInventory.ItemCategory category)
        {
            var slot = GetEquipmentSlot(category);
            if (slot == null) return null;

            var em = EquipmentManager.Instance;
            if (em == null) return null;

            var slotData = em.GetSlotData(slot.Value);
            if (slotData == null || slotData.itemData == null || string.IsNullOrEmpty(slotData.itemId))
                return null;

            return new ItemTooltipData
            {
                itemName = slotData.itemData.displayName,
                description = slotData.itemData.description,
                effects = slotData.itemData.effects,
                rarity = slotData.itemData.rarity,
                category = slotData.itemData.category,
                maxDurability = slotData.itemData.maxDurability,
                currentDurability = slotData.currentDurability,
                count = 1
            };
        }

        /// <summary>
        /// 효과 문자열에서 숫자 수치값을 추출합니다.
        /// 예: "공격력+10" → 10, "방어력-5" → -5
        /// </summary>
        public static int ExtractStatValue(string effects)
        {
            if (string.IsNullOrEmpty(effects)) return 0;
            string digits = "";
            bool foundSign = false;
            foreach (char c in effects)
            {
                if (c == '-' || c == '+')
                {
                    if (!foundSign)
                    {
                        digits += c;
                        foundSign = true;
                    }
                }
                else if (char.IsDigit(c))
                {
                    digits += c;
                }
            }
            if (string.IsNullOrEmpty(digits)) return 0;
            if (int.TryParse(digits, out int val))
                return val;
            return 0;
        }

        /// <summary>
        /// 효과 문자열에서 스탯 이름을 추출합니다.
        /// 예: "공격력+10" → "공격력"
        /// </summary>
        public static string ExtractStatName(string effects)
        {
            if (string.IsNullOrEmpty(effects)) return "";
            string name = "";
            foreach (char c in effects)
            {
                if (!char.IsDigit(c) && c != '+' && c != '-')
                    name += c;
                else if (c == '+' || c == '-')
                    break;
            }
            return name.Trim();
        }

        /// <summary>
        /// 신규 값과 기존 값의 비교 아이콘 반환:
        /// 🟢 좋음, 🔴 나쁨, ⚪ 같음
        /// </summary>
        public static string GetComparisonIcon(int newValue, int oldValue)
        {
            if (newValue > oldValue) return "🟢";
            if (newValue < oldValue) return "🔴";
            return "⚪";
        }

        /// <summary>
        /// 비교 섹션의 총 높이를 계산합니다.
        /// </summary>
        public static float CalculateCompareHeight(float width)
        {
            // 헤더(현재 장비) + 이름줄 + 화살표 + 헤더(새 장비) + 이름줄 + 구분선
            return COMPARE_HEADER_HEIGHT + COMPARE_LINE_HEIGHT
                 + COMPARE_ARROW_HEIGHT
                 + COMPARE_HEADER_HEIGHT + COMPARE_LINE_HEIGHT
                 + COMPARE_SEPARATOR_HEIGHT;
        }

        /// <summary>
        /// 툴팁 하단에 아이템 비교 섹션을 그립니다.
        /// </summary>
        /// <param name="cx">시작 X (툴팁 내부 패딩 적용된 좌표)</param>
        /// <param name="cy">시작 Y (현재 컨텐츠가 끝난 위치)</param>
        /// <param name="cw">사용 가능한 너비</param>
        /// <param name="newItem">호버 중인 새 아이템 데이터</param>
        /// <param name="equippedItem">장착 중인 기존 아이템 데이터</param>
        /// <param name="texWhite">1×1 흰색 텍스처 (렌더링용)</param>
        /// <returns>다음 Y 위치 (비교 섹션 이후)</returns>
        public static float DrawComparison(float cx, float cy, float cw,
            ItemTooltipData newItem, ItemTooltipData equippedItem, Texture2D texWhite)
        {
            if (texWhite == null) return cy;
            InitCompareStyles();

            float y = cy;

            // ──── 구분선 ────
            Rect dividerRect = new Rect(cx, y, cw, 1);
            DrawColoredRect(dividerRect, ColorCompareDivider, texWhite);
            y += COMPARE_SEPARATOR_HEIGHT;

            // ──── 현재 장비 헤더 ────
            _gcCompare.text = "──────── 현재 장비 ────────";
            float headerHeight = _styleCompareLabel.CalcHeight(_gcCompare, cw);
            GUI.Label(new Rect(cx, y, cw, headerHeight), _gcCompare, _styleCompareLabel);
            y += headerHeight + 2;

            // ──── 현재 장비 이름 + 효과 ────
            string currentText = string.IsNullOrEmpty(equippedItem.itemName) ? "(없음)" : equippedItem.itemName;
            if (!string.IsNullOrEmpty(equippedItem.effects))
                currentText += $" ({equippedItem.effects})";

            _gcCompare.text = currentText;
            float currentHeight = _styleCompareCurrent.CalcHeight(_gcCompare, cw);
            GUI.Label(new Rect(cx, y, cw, currentHeight), _gcCompare, _styleCompareCurrent);
            y += currentHeight + 2;

            // ──── 화살표 (↓) ────
            _gcCompare.text = "     ↓";
            float arrowHeight = _styleCompareArrow.CalcHeight(_gcCompare, cw);
            GUI.Label(new Rect(cx, y, cw, arrowHeight), _gcCompare, _styleCompareArrow);
            y += arrowHeight;

            // ──── 새 장비 헤더 ────
            _gcCompare.text = "──────── 새 장비 ─────────";
            GUI.Label(new Rect(cx, y, cw, headerHeight), _gcCompare, _styleCompareLabel);
            y += headerHeight + 2;

            // ──── 새 장비 이름 + 효과 + 비교 아이콘 ────
            int newVal = ExtractStatValue(newItem.effects);
            int oldVal = ExtractStatValue(equippedItem.effects);
            string icon = GetComparisonIcon(newVal, oldVal);
            int diff = newVal - oldVal;

            string newText = newItem.itemName;
            if (!string.IsNullOrEmpty(newItem.effects))
                newText += $" ({newItem.effects})";

            // 비교 표시: 🟢 +5, 🔴 -5, ⚪ ±0
            string compareSuffix = "";
            if (diff > 0) compareSuffix = $"  {icon} +{diff}";
            else if (diff < 0) compareSuffix = $"  {icon} {diff}";
            else compareSuffix = $"  {icon} ±0";

            string fullNewText = newText + compareSuffix;

            _gcCompare.text = fullNewText;
            float newHeight = _styleCompareNew.CalcHeight(_gcCompare, cw);
            GUI.Label(new Rect(cx, y, cw, newHeight), _gcCompare, _styleCompareNew);
            y += newHeight + COMPARE_SEPARATOR_HEIGHT;

            return y;
        }

        /// <summary>1×1 사각형 그리기</summary>
        private static void DrawColoredRect(Rect rect, Color color, Texture2D texWhite)
        {
            var oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, texWhite);
            GUI.color = oldColor;
        }
    }
}