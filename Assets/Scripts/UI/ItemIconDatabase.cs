using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core;

namespace ProjectName.UI
{
    /// <summary>
    /// G3-06: 아이템 아이콘 데이터베이스 — 캐싱 + 생성 래퍼.
    /// ProceduralIconGenerator (Core)에서 생성한 32×32 아이콘을 64×64로 업스케일하여 제공합니다.
    /// </summary>
    public static class ItemIconDatabase
    {
        private const int ICON_SIZE = 64;

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

            // ProceduralIconGenerator 32×32 생성 → 64×64 업스케일
            var generated = ProceduralIconGenerator.GenerateIcon(item.id, item.category, item.maxDurability);
            if (generated != null)
            {
                var scaled = ScaleUpTexture(generated, ICON_SIZE, ICON_SIZE);
                _iconCache[item.id] = scaled;
                return scaled;
            }

            return GetFallbackIcon();
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

            if (_fallbackIcon != null)
            {
                Object.Destroy(_fallbackIcon);
                _fallbackIcon = null;
            }
        }

        // ================================================================
        // Fallback 아이콘
        // ================================================================

        private static Texture2D _fallbackIcon;

        private static Texture2D GetFallbackIcon()
        {
            if (_fallbackIcon != null) return _fallbackIcon;

            _fallbackIcon = new Texture2D(ICON_SIZE, ICON_SIZE, TextureFormat.RGBA32, false);
            _fallbackIcon.hideFlags = HideFlags.HideAndDontSave;
            Color bg = new Color(0.2f, 0.2f, 0.2f);
            Color fg = new Color(0.5f, 0.5f, 0.5f);
            for (int y = 0; y < ICON_SIZE; y++)
            {
                for (int x = 0; x < ICON_SIZE; x++)
                {
                    bool border = x < 2 || x >= ICON_SIZE - 2 || y < 2 || y >= ICON_SIZE - 2;
                    bool cross = Mathf.Abs(x - ICON_SIZE / 2) + Mathf.Abs(y - ICON_SIZE / 2) < 8;
                    _fallbackIcon.SetPixel(x, y, border ? fg : cross ? fg : bg);
                }
            }
            _fallbackIcon.Apply();
            return _fallbackIcon;
        }

        // ================================================================
        // 업스케일: 32×32 → 64×64 (Nearest-Neighbor)
        // ================================================================

        private static Texture2D ScaleUpTexture(Texture2D source, int targetWidth, int targetHeight)
        {
            if (source == null || source.width <= 0 || source.height <= 0)
                return GetFallbackIcon();

            var dest = new Texture2D(targetWidth, targetHeight, source.format, false);
            float sx = (float)source.width / targetWidth;
            float sy = (float)source.height / targetHeight;
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                {
                    dest.SetPixel(x, y, source.GetPixel(Mathf.FloorToInt(x * sx), Mathf.FloorToInt(y * sy)));
                }
            }
            dest.Apply();
            return dest;
        }
    }
}