using System.Collections.Generic;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Core
{
    /// <summary>
    /// Tracks which recipes the player has discovered.
    /// Recipes are identified by their display name.
    /// Data is persisted via PlayerPrefs using pipe-delimited encoding.
    /// </summary>
    public static class RecipeDiscoverySystem
    {
        private static HashSet<string> _discovered = new HashSet<string>();
        private static bool _initialized = false;

        /// <summary>
        /// Separator used for serializing recipe names.
        /// Pipe character chosen over comma to allow commas in display names.
        /// </summary>
        private const char SEPARATOR = '|';
        private const string PREFS_KEY = "DiscoveredRecipes";

        /// <summary>
        /// [2026-10-11 A안 확정] 게임 시작 시 기본 공개 레시피 — 생활 사이클의 진입 레시피.
        /// 목재티어 무기 4종 + 공구 3종(도끼/곡괭이/삽) + 낚싯대. 제작소(WeaponForgeUTK)의
        /// "?" 블라인드(RecipeDiscoverySystem.IsDiscovered)가 이것들을 가리지 않게 한다.
        /// 판정 키 = 결과명(DisplayName)이므로 동일 문자열로 시드. 석재·철·크리스탈 티어와
        /// 방어구는 기존 발견(성공 제작 시 MarkDiscovered) 시스템 유지.
        /// </summary>
        private static readonly string[] DefaultDiscoveredResultIds =
        {
            "weapon_dagger_wood", "weapon_sword_wood", "weapon_spear_wood", "weapon_bow_wood",
            "tool_axe", "tool_pickaxe", "tool_shovel",
            "fishing_rod",
        };

        /// <summary>
        /// Load discovered recipes from PlayerPrefs.
        /// </summary>
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            string saved = PlayerPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(saved))
            {
                string[] names = saved.Split(SEPARATOR);
                foreach (string name in names)
                {
                    string trimmed = name.Trim();
                    if (!string.IsNullOrEmpty(trimmed))
                        _discovered.Add(trimmed);
                }
            }

            // [2026-10-11 A안] 기본 공개 시드 — 저장 유무와 무관하게 항상 보장(누락 시 보완 저장).
            int seeded = 0;
            foreach (string id in DefaultDiscoveredResultIds)
            {
                string displayName = WeaponCraftDatabase.DisplayName(id);
                if (_discovered.Add(displayName))
                {
                    seeded++;
                }
            }
            if (seeded > 0) Save();

            Debug.Log($"[RecipeDiscoverySystem] Loaded {_discovered.Count} discovered recipes (기본 공개 {DefaultDiscoveredResultIds.Length}종 포함).");
        }

        /// <summary>
        /// Mark a recipe as discovered and save.
        /// Null or empty names are silently ignored.
        /// </summary>
        public static void MarkDiscovered(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName))
            {
                Debug.LogWarning("[RecipeDiscoverySystem] MarkDiscovered called with null/empty name — ignored.");
                return;
            }

            if (!_initialized) Initialize();

            if (_discovered.Add(recipeName))
            {
                Save();
                Debug.Log($"[RecipeDiscoverySystem] 📖 New recipe discovered: {recipeName}");
            }
        }

        /// <summary>
        /// Check if a recipe has been discovered.
        /// Returns false for null or empty names.
        /// </summary>
        public static bool IsDiscovered(string recipeName)
        {
            if (string.IsNullOrEmpty(recipeName)) return false;
            if (!_initialized) Initialize();
            return _discovered.Contains(recipeName);
        }

        /// <summary>
        /// Get all discovered recipe names (defensive copy).
        /// </summary>
        public static IReadOnlyCollection<string> GetAllDiscovered()
        {
            if (!_initialized) Initialize();
            return new HashSet<string>(_discovered);
        }

        /// <summary>
        /// Get total count of discovered recipes.
        /// </summary>
        public static int DiscoveredCount
        {
            get
            {
                if (!_initialized) Initialize();
                return _discovered.Count;
            }
        }

        /// <summary>
        /// Reset all discovered recipes (for testing).
        /// </summary>
        public static void Reset()
        {
            _discovered.Clear();
            Save();
            Debug.Log("[RecipeDiscoverySystem] All discovered recipes reset.");
        }

        private static void Save()
        {
            string joined = string.Join(SEPARATOR.ToString(), _discovered);
            PlayerPrefs.SetString(PREFS_KEY, joined);
            PlayerPrefs.Save();
        }
    }
}