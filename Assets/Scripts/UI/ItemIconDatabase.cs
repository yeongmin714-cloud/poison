using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-06: 아이템 아이콘 데이터베이스 — 캐싱 + 생성 래퍼.
    /// 기존 ProceduralIconGenerator (Core)를 활용하여 64×64 아이콘을 제공합니다.
    /// </summary>
    public static class ItemIconDatabase
    {
        private static Dictionary<string, Texture2D> _iconCache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// ItemData로 아이콘을 가져옵니다.
        /// </summary>
        public static Texture2D GetOrCreateIcon(PlayerInventory.ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.id))
                return GetFallbackIcon();

            if (_iconCache.TryGetValue(item.id, out var cached))
                return cached;

            // ProceduralIconGenerator 호출 (Category enum 필요)
            var generated = ProceduralIconGenerator.GenerateIcon(item.id, item.category, item.maxDurability);
            if (generated != null)
            {
                _iconCache[item.id] = generated;
                return generated;
            }

            return GetFallbackIcon();
        }

        /// <summary>
        /// ItemSlot에서 아이콘을 가져옵니다.
        /// </summary>
        public static Texture2D GetIconFromSlot(PlayerInventory.ItemSlot slot)
        {
            if (slot == null || slot.item == null)
                return GetFallbackIcon();
            return GetOrCreateIcon(slot.item);
        }

        /// <summary>
        /// 캐시를 모두 비웁니다.
        /// </summary>
        public static void ClearCache()
        {
            foreach (var tex in _iconCache.Values)
            {
                if (tex != null)
                    Object.Destroy(tex);
            }
            _iconCache.Clear();
        }

        // ================================================================
        // Fallback 아이콘
        // ================================================================

        private static Texture2D _fallbackIcon;

        private static Texture2D GetFallbackIcon()
        {
            if (_fallbackIcon != null) return _fallbackIcon;

            _fallbackIcon = new Texture2D(64, 64);
            Color bg = new Color(0.2f, 0.2f, 0.2f);
            Color fg = new Color(0.5f, 0.5f, 0.5f);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    bool border = x < 2 || x >= 62 || y < 2 || y >= 62;
                    bool cross = Mathf.Abs(x - 32) + Mathf.Abs(y - 32) < 8;
                    _fallbackIcon.SetPixel(x, y, border ? fg : cross ? fg : bg);
                }
            }
            _fallbackIcon.Apply();
            return _fallbackIcon;
        }
    }
}