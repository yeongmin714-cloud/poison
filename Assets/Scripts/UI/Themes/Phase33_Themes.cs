#nullable disable
using System.Reflection;
using UnityEngine;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 Batch 3: 팩토리 메서드로 모든 UI 테마 인스턴스를 생성합니다.
    /// 각 메서드는 UIDesignTheme ScriptableObject를 생성하고 리플렉션으로 private 필드를 설정합니다.
    /// </summary>
    public static class Phase33_Themes
    {
        private static FieldInfo _patternField;
        private static FieldInfo _borderField;
        private static FieldInfo _decoField;
        private static FieldInfo _animField;
        private static FieldInfo _nameField;
        private static FieldInfo _iconField;

        private static void EnsureReflection()
        {
            if (_patternField != null) return;
            var t = typeof(UIDesignTheme);
            _patternField = t.GetField("_patternType", BindingFlags.NonPublic | BindingFlags.Instance);
            _borderField = t.GetField("_borderType", BindingFlags.NonPublic | BindingFlags.Instance);
            _decoField = t.GetField("_decorationType", BindingFlags.NonPublic | BindingFlags.Instance);
            _animField = t.GetField("_animationType", BindingFlags.NonPublic | BindingFlags.Instance);
            _nameField = t.GetField("_themeName", BindingFlags.NonPublic | BindingFlags.Instance);
            _iconField = t.GetField("_iconPrefix", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        private static UIDesignTheme Create(string name, string icon,
            Color bg, Color border, Color title, Color text, Color subText, Color accent,
            UIDesignTheme.PatternType pattern, UIDesignTheme.BorderType borderType,
            UIDesignTheme.DecorationType deco, UIDesignTheme.AnimationType anim)
        {
            EnsureReflection();
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            _nameField?.SetValue(theme, name);
            _iconField?.SetValue(theme, icon);
            theme.SetColorSet(bg, border, title, text, subText, accent);
            _patternField?.SetValue(theme, pattern);
            _borderField?.SetValue(theme, borderType);
            _decoField?.SetValue(theme, deco);
            _animField?.SetValue(theme, anim);
            return theme;
        }

        // ================================================================
        // UI-09: Shop / Loot / Mercenary
        // ================================================================

        /// <summary>상점 — 녹색 펠트, 필그리 테두리, 인장 장식, FadeSlide</summary>
        public static UIDesignTheme ShopTheme()
        {
            return Create("Shop", "🏪",
                new Color(0.08f, 0.15f, 0.06f, 0.9f),   // 녹색펠트 bg
                new Color(0.85f, 0.7f, 0.2f, 0.85f),    // 금실 border
                new Color(0.9f, 0.75f, 0.25f, 1f),      // Title (골드)
                Color.white,                              // Text
                new Color(0.75f, 0.75f, 0.75f, 1f),      // SubText
                new Color(0.3f, 0.8f, 0.3f, 1f),         // Accent (초록)
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Seal,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>전리품 — 나무 느낌, 가시(쇠테) 테두리, 리벳 장식</summary>
        public static UIDesignTheme LootTheme()
        {
            return Create("Loot", "🎁",
                new Color(0.25f, 0.18f, 0.1f, 0.9f),    // 나무 bg
                new Color(0.4f, 0.35f, 0.3f, 0.85f),    // 쇠테 border
                new Color(0.9f, 0.8f, 0.3f, 1f),        // Title
                Color.white,
                new Color(0.75f, 0.7f, 0.6f, 1f),
                new Color(0.6f, 0.4f, 0.2f, 1f),
                UIDesignTheme.PatternType.Wood,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>용병 — 가죽, 인장 장식, 바운스 애니메이션</summary>
        public static UIDesignTheme MercenaryTheme()
        {
            return Create("Mercenary", "🍺",
                new Color(0.3f, 0.22f, 0.12f, 0.9f),    // 기름때 양피지 bg
                new Color(0.6f, 0.4f, 0.2f, 0.85f),     // 갈색 border
                new Color(0.9f, 0.7f, 0.3f, 1f),
                Color.white,
                new Color(0.75f, 0.65f, 0.5f, 1f),
                new Color(0.5f, 0.3f, 0.1f, 1f),
                UIDesignTheme.PatternType.Leather,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Seal,
                UIDesignTheme.AnimationType.Bounce);
        }

        // ================================================================
        // UI-10: Church / Envoy / Spy
        // ================================================================

        /// <summary>성당 — 대리석, 필그리, 왕관, 스케일</summary>
        public static UIDesignTheme ChurchTheme()
        {
            return Create("Church", "⛪",
                new Color(0.1f, 0.05f, 0.15f, 0.9f),    // 스테인드글라스 bg
                new Color(0.85f, 0.7f, 0.2f, 0.85f),    // 금 border
                new Color(0.9f, 0.8f, 0.3f, 1f),
                Color.white,
                new Color(0.75f, 0.7f, 0.8f, 1f),
                new Color(0.6f, 0.3f, 0.8f, 1f),
                UIDesignTheme.PatternType.Marble,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Scale);
        }

        /// <summary>특사 — 돌, 방패, 인장</summary>
        public static UIDesignTheme EnvoyTheme()
        {
            return Create("Envoy", "📜",
                new Color(0.05f, 0.05f, 0.15f, 0.92f),  // 다크블루 bg
                new Color(0.5f, 0.5f, 0.6f, 0.85f),     // 은회색 border
                new Color(0.8f, 0.8f, 0.9f, 1f),
                Color.white,
                new Color(0.6f, 0.6f, 0.7f, 1f),
                new Color(0.3f, 0.4f, 0.7f, 1f),
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Seal,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>정보원 — 금속, 룬, 해골</summary>
        public static UIDesignTheme SpyTheme()
        {
            return Create("Spy", "🕵️",
                new Color(0.08f, 0.08f, 0.1f, 0.92f),   // 암호격자 bg
                new Color(0.3f, 0.8f, 0.3f, 0.85f),     // 녹색룬 border
                new Color(0.2f, 0.9f, 0.2f, 1f),
                Color.white,
                new Color(0.6f, 0.6f, 0.6f, 1f),
                new Color(0.2f, 0.7f, 0.2f, 1f),
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Rune,
                UIDesignTheme.DecorationType.Skull,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // UI-11: Revenge / Death / LordAudience
        // ================================================================

        /// <summary>복수명부 — 돌(피얼룩), 가시철사, 해골, Shatter</summary>
        public static UIDesignTheme RevengeTheme()
        {
            return Create("Revenge", "🗡️",
                new Color(0.15f, 0.02f, 0.02f, 0.92f),  // 피 bg
                new Color(0.8f, 0.1f, 0.1f, 0.85f),     // 붉은 border
                new Color(0.9f, 0.3f, 0.2f, 1f),
                Color.white,
                new Color(0.7f, 0.3f, 0.3f, 1f),
                new Color(0.9f, 0.1f, 0.1f, 1f),
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Skull,
                UIDesignTheme.AnimationType.Shatter);
        }

        /// <summary>사망화면 — 돌, 가시, 해골</summary>
        public static UIDesignTheme DeathTheme()
        {
            return Create("Death", "💀",
                new Color(0.08f, 0.08f, 0.08f, 0.95f),  // 잿빛 bg
                new Color(0.3f, 0.3f, 0.3f, 0.85f),     // 회색 border
                new Color(0.9f, 0.2f, 0.2f, 1f),
                new Color(0.8f, 0.8f, 0.8f, 1f),
                new Color(0.5f, 0.5f, 0.5f, 1f),
                new Color(0.8f, 0.1f, 0.1f, 1f),
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Skull,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>영주대면 — 대리석, 필그리, 왕관, 스케일</summary>
        public static UIDesignTheme LordAudienceTheme()
        {
            return Create("LordAudience", "👑",
                new Color(0.2f, 0.18f, 0.15f, 0.9f),    // 대리석 bg
                new Color(0.85f, 0.75f, 0.25f, 0.85f),  // 황금 border
                new Color(0.9f, 0.8f, 0.3f, 1f),
                Color.white,
                new Color(0.7f, 0.65f, 0.6f, 1f),
                new Color(0.6f, 0.5f, 0.2f, 1f),
                UIDesignTheme.PatternType.Marble,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Scale);
        }

        // ================================================================
        // UI-12: EscMenu / Settings
        // ================================================================

        /// <summary>ESC 메뉴 — 유리(Glassmorphism), 별, 왕관, Reveal</summary>
        public static UIDesignTheme EscMenuTheme()
        {
            return Create("EscMenu", "⏸",
                new Color(0f, 0f, 0f, 0.7f),            // 반투명 bg
                new Color(0.5f, 0.5f, 0.6f, 0.5f),      // 얇은라인 border
                new Color(0.9f, 0.9f, 0.9f, 1f),
                Color.white,
                new Color(0.7f, 0.7f, 0.7f, 1f),
                new Color(0.4f, 0.6f, 0.8f, 1f),
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Star,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Reveal);
        }

        /// <summary>설정 — 금속(모던블랙), 방패, 왕관</summary>
        public static UIDesignTheme SettingsTheme()
        {
            return Create("Settings", "⚙",
                new Color(0.05f, 0.05f, 0.06f, 0.95f),  // 모던블랙 bg
                new Color(0.7f, 0.7f, 0.75f, 0.85f),    // silver border
                new Color(0.9f, 0.9f, 0.9f, 1f),
                Color.white,
                new Color(0.7f, 0.7f, 0.7f, 1f),
                new Color(0.5f, 0.7f, 0.9f, 1f),
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // UI-13: Achievement / GuardWorldHUD / NPCDialogue
        // ================================================================

        /// <summary>업적 — 대리석(메달), 별, 왕관, 바운스</summary>
        public static UIDesignTheme AchievementTheme()
        {
            return Create("Achievement", "🏆",
                new Color(0.15f, 0.1f, 0.02f, 0.9f),    // 골드 bg
                new Color(0.85f, 0.75f, 0.2f, 0.85f),   // gold border
                new Color(1f, 0.9f, 0.3f, 1f),
                Color.white,
                new Color(0.8f, 0.7f, 0.4f, 1f),
                new Color(0.9f, 0.8f, 0.2f, 1f),
                UIDesignTheme.PatternType.Marble,
                UIDesignTheme.BorderType.Star,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Bounce);
        }

        /// <summary>병사 HUD — 금속(전술격자), 방패</summary>
        public static UIDesignTheme GuardHUDTheme()
        {
            return Create("GuardHUD", "⚔️",
                new Color(0.2f, 0.18f, 0.1f, 0.9f),     // 카키 bg
                new Color(0.5f, 0.5f, 0.3f, 0.85f),     // 카키 border
                new Color(0.9f, 0.9f, 0.8f, 1f),
                Color.white,
                new Color(0.7f, 0.7f, 0.6f, 1f),
                new Color(0.4f, 0.5f, 0.2f, 1f),
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>NPC 대화 — 양피지(말풍선), 필그리</summary>
        public static UIDesignTheme NPCDialogueTheme()
        {
            return Create("NPCDialogue", "💬",
                new Color(0.25f, 0.2f, 0.15f, 0.92f),   // 밝은 bg
                new Color(0.5f, 0.4f, 0.3f, 0.85f),     // 갈색 border
                new Color(1f, 0.95f, 0.85f, 1f),
                new Color(0.95f, 0.9f, 0.85f, 1f),
                new Color(0.7f, 0.65f, 0.6f, 1f),
                new Color(0.6f, 0.5f, 0.35f, 1f),
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // UI-14: FlagRegistration / GuardInfo
        // ================================================================

        /// <summary>국기등록 — 돌(방패), 방패, 왕관</summary>
        public static UIDesignTheme FlagRegTheme()
        {
            return Create("FlagRegistration", "🏳️",
                new Color(0.1f, 0.08f, 0.15f, 0.9f),    // 방패 bg
                new Color(0.6f, 0.5f, 0.7f, 0.85f),     // 보라 border
                new Color(0.9f, 0.85f, 0.4f, 1f),
                Color.white,
                new Color(0.7f, 0.65f, 0.7f, 1f),
                new Color(0.5f, 0.3f, 0.7f, 1f),
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>병사정보 — 가죽(군복), 방패</summary>
        public static UIDesignTheme GuardInfoTheme()
        {
            return Create("GuardInfo", "ℹ️",
                new Color(0.22f, 0.18f, 0.08f, 0.9f),   // 카키군복 bg
                new Color(0.5f, 0.45f, 0.3f, 0.85f),    // 카키 border
                new Color(0.9f, 0.9f, 0.8f, 1f),
                Color.white,
                new Color(0.7f, 0.7f, 0.6f, 1f),
                new Color(0.4f, 0.5f, 0.2f, 1f),
                UIDesignTheme.PatternType.Leather,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // UI-15: HerbRespawn / MonsterLevel
        // ================================================================

        /// <summary>약초리스폰 — 유리(녹색원형), 테두리 없음</summary>
        public static UIDesignTheme HerbRespawnTheme()
        {
            return Create("HerbRespawn", "🌿",
                new Color(0.02f, 0.12f, 0.05f, 0.85f),  // 녹색자연 bg
                new Color(0.2f, 0.6f, 0.2f, 0.5f),      // 연초록 border (있으나 없으나)
                new Color(0.3f, 1f, 0.3f, 1f),
                Color.white,
                new Color(0.6f, 0.8f, 0.6f, 1f),
                new Color(0.2f, 0.9f, 0.2f, 1f),
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // UI-17: 이 배치 1의 window-specific themes (sibling subagent와 충돌 방지 명시적 메서드)
        // ================================================================

        /// <summary>MapWindow: Parchment, Filigree, CornerScroll, FadeSlide — 세피아 양피지</summary>
        public static UIDesignTheme CreateMapTheme()
        {
            return Create("Map Theme", "🗺️",
                new Color(0.2f, 0.15f, 0.1f, 0.9f),    // 세피아 양피지 bg
                new Color(0.85f, 0.65f, 0.15f, 0.8f),  // 골드 border
                new Color(0.9f, 0.7f, 0.2f, 1f),       // 앰버 title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.85f, 0.65f, 0.15f, 1f),    // Accent
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.CornerScroll,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>MinimapUI: Glass, Shield, Crown, Scale — 황동 원형느낌</summary>
        public static UIDesignTheme CreateMinimapTheme()
        {
            return Create("Minimap Theme", "🧭",
                new Color(0.25f, 0.18f, 0.08f, 0.9f),  // 다크 브래스 bg
                new Color(0.85f, 0.65f, 0.15f, 0.85f), // 골드 border
                new Color(1f, 0.8f, 0.3f, 1f),         // 밝은 골드 title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.9f, 0.7f, 0.2f, 1f),
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Scale);
        }

        /// <summary>InventoryWindow: Leather, Star, Rivet, FadeSlide — 가죽결</summary>
        public static UIDesignTheme CreateInventoryTheme()
        {
            return Create("Inventory Theme", "📦",
                new Color(0.35f, 0.2f, 0.1f, 0.9f),   // 암갈색 bg
                new Color(0.72f, 0.45f, 0.2f, 0.85f), // 구리 border
                new Color(0.9f, 0.7f, 0.3f, 1f),      // 황금빛 title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.72f, 0.45f, 0.2f, 1f),
                UIDesignTheme.PatternType.Leather,
                UIDesignTheme.BorderType.Star,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>EquipmentWindow: Metal, Shield, Rivet, Reveal — 금속브러시드</summary>
        public static UIDesignTheme CreateEquipmentTheme()
        {
            return Create("Equipment Theme", "🛡️",
                new Color(0.12f, 0.15f, 0.18f, 0.92f),// 철청 bg
                new Color(0.6f, 0.6f, 0.65f, 0.85f),  // 스틸 border
                new Color(0.75f, 0.75f, 0.8f, 1f),    // 밝은 스틸 title
                Color.white,
                new Color(0.7f, 0.7f, 0.72f, 1f),
                new Color(0.6f, 0.6f, 0.65f, 1f),
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.Reveal);
        }

        /// <summary>WarehouseUI: Wood, Thorn, Rivet, Bounce — 나무결</summary>
        public static UIDesignTheme CreateWarehouseTheme()
        {
            return Create("Warehouse Theme", "📦",
                new Color(0.3f, 0.2f, 0.1f, 0.9f),    // 나무 bg
                new Color(0.55f, 0.35f, 0.15f, 0.85f),// 짙은 나무색 border
                new Color(0.85f, 0.65f, 0.25f, 1f),   // 황금빛 title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.55f, 0.35f, 0.15f, 1f),
                UIDesignTheme.PatternType.Wood,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.Bounce);
        }

        /// <summary>몬스터레벨 — 기본값 (작은 라벨용)</summary>
        public static UIDesignTheme MonsterLevelTheme()
        {
            return Create("MonsterLevel", "👾",
                new Color(0.05f, 0.05f, 0.08f, 0.8f),   // 어두운 bg
                new Color(0.3f, 0.3f, 0.4f, 0.7f),      // 회색 border
                Color.white,
                Color.white,
                new Color(0.7f, 0.7f, 0.7f, 1f),
                new Color(0.5f, 0.5f, 0.7f, 1f),
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        // ================================================================
        // Batch 2: UI-05 ~ UI-08 window-specific themes
        // ================================================================

        /// <summary>PlayerStatusWindow: Parchment, Filigree, Crown, FadeSlide — 아이보리 양피지</summary>
        public static UIDesignTheme CreateStatusTheme()
        {
            return Create("Status Theme", "⚔️",
                new Color(0.3f, 0.25f, 0.18f, 0.88f),  // 아이보리 bg
                new Color(0.85f, 0.65f, 0.15f, 0.8f),   // 골드 border
                new Color(0.9f, 0.7f, 0.3f, 1f),        // Title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.3f, 0.5f, 0.7f, 1f),        // Accent
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>QuestWindow: Parchment, Filigree, CornerScroll, FadeSlide — 세피아 양피지</summary>
        public static UIDesignTheme CreateQuestTheme()
        {
            return Create("Quest Theme", "📋",
                new Color(0.25f, 0.2f, 0.12f, 0.9f),   // 세피아 bg
                new Color(0.85f, 0.65f, 0.15f, 0.8f),   // 골드 border
                new Color(0.9f, 0.7f, 0.3f, 1f),
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.3f, 0.5f, 0.7f, 1f),
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.CornerScroll,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>RecipeWindow: Stone, Rune, Seal, Flip — 보라 마법진</summary>
        public static UIDesignTheme CreateRecipeTheme()
        {
            return Create("Recipe Theme", "📖",
                new Color(0.08f, 0.03f, 0.2f, 0.92f),  // 보라 bg
                new Color(0.7f, 0.2f, 0.8f, 0.8f),      // magic purple border
                new Color(0.9f, 0.5f, 1f, 1f),          // light purple title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.4f, 0.2f, 0.8f, 1f),        // deep purple accent
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Rune,
                UIDesignTheme.DecorationType.Seal,
                UIDesignTheme.AnimationType.Flip);
        }

        /// <summary>TooltipWindow: Parchment, simple border, no decoration — 밝은 양피지</summary>
        public static UIDesignTheme CreateTooltipTheme()
        {
            return Create("Tooltip Theme", "💡",
                new Color(0.38f, 0.32f, 0.22f, 0.95f), // 밝은 양피지 bg
                new Color(0.6f, 0.5f, 0.3f, 0.6f),      // muted brown border
                new Color(0.95f, 0.9f, 0.8f, 1f),       // warm white title
                Color.white,
                new Color(0.8f, 0.75f, 0.65f, 1f),
                new Color(0.6f, 0.4f, 0.2f, 1f),        // brown accent
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>CraftingUI: Wood, Thorn, Rivet, FadeSlide — 나무+숯</summary>
        public static UIDesignTheme CreateCraftingTheme()
        {
            return Create("Crafting Theme", "🔨",
                new Color(0.22f, 0.14f, 0.08f, 0.9f),  // 나무 bg
                new Color(0.72f, 0.45f, 0.2f, 0.85f),   // copper border
                new Color(0.9f, 0.7f, 0.3f, 1f),        // gold title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.8f, 0.5f, 0.2f, 1f),        // warm copper accent
                UIDesignTheme.PatternType.Wood,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>CookingUI: Glass(tile feel), Star, CornerScroll, FadeSlide — 벽돌</summary>
        public static UIDesignTheme CreateCookingTheme()
        {
            return Create("Cooking Theme", "🍳",
                new Color(0.3f, 0.18f, 0.12f, 0.9f),   // 벽돌 bg
                new Color(0.85f, 0.6f, 0.2f, 0.85f),    // warm gold border
                new Color(0.9f, 0.7f, 0.3f, 1f),
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.8f, 0.4f, 0.2f, 1f),        // warm red accent
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Star,
                UIDesignTheme.DecorationType.CornerScroll,
                UIDesignTheme.AnimationType.FadeSlide);
        }

        /// <summary>AlchemyUI: Glass, Rune, Skull, Spin — 어두운보라 + 네온초록</summary>
        public static UIDesignTheme CreateAlchemyTheme()
        {
            return Create("Alchemy Theme", "⚗️",
                new Color(0.06f, 0.02f, 0.12f, 0.92f), // 어두운보라 bg
                new Color(0.5f, 0.1f, 0.6f, 0.85f),     // dark purple border
                new Color(0.7f, 0.3f, 0.8f, 1f),        // light purple title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.0f, 1.0f, 0.3f, 1f),        // 네온초록 accent
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Rune,
                UIDesignTheme.DecorationType.Skull,
                UIDesignTheme.AnimationType.Spin);
        }

        /// <summary>RepairStationUI: Metal, Shield, Rivet, FadeSlide — 금속</summary>
        public static UIDesignTheme CreateRepairTheme()
        {
            return Create("Repair Theme", "🔧",
                new Color(0.15f, 0.15f, 0.17f, 0.9f),  // 금속 bg
                new Color(0.6f, 0.6f, 0.65f, 0.85f),    // steel border
                new Color(0.85f, 0.85f, 0.9f, 1f),      // silver title
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                new Color(0.4f, 0.6f, 0.8f, 1f),        // blue steel accent
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.FadeSlide);
        }
    }
}
