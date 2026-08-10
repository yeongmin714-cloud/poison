using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ProjectName.UI.Themes;
using ProjectName.Core.Themes;

namespace ProjectName.UI.Themes
{
    /// <summary>
    /// Phase 33 테마 팩토리.
    /// 각 UI 창 타입별로 사전 정의된 UIDesignTheme 인스턴스를 생성하는 정적 메서드를 제공합니다.
    /// </summary>
    public static class Phase33_Themes
    {
        // ================================================================
        // 공개 팩토리 메서드 (Create* 패턴)
        // ================================================================

        public static UIDesignTheme CreateAlchemyTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Alchemy Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.08f, 0.05f, 0.95f),    // BgColor
                new Color(0.6f, 0.4f, 0.2f, 0.9f),       // BorderColor
                new Color(0.9f, 0.7f, 0.3f, 1f),         // TitleColor
                new Color(0.85f, 0.8f, 0.7f, 1f),        // TextColor
                new Color(0.7f, 0.65f, 0.55f, 1f),       // SubTextColor
                new Color(0.9f, 0.6f, 0.2f, 1f)          // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Glass);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Scale);
            return theme;
        }

        public static UIDesignTheme CreateWarehouseTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Warehouse Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.4f, 0.45f, 0.5f, 0.9f),
                new Color(0.7f, 0.8f, 0.9f, 1f),
                new Color(0.8f, 0.85f, 0.9f, 1f),
                new Color(0.6f, 0.65f, 0.7f, 1f),
                new Color(0.4f, 0.7f, 0.9f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Wood);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Slide);
            return theme;
        }

        public static UIDesignTheme CreateEquipmentTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Equipment Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.1f, 0.95f),
                new Color(0.5f, 0.4f, 0.3f, 0.9f),
                new Color(0.9f, 0.85f, 0.7f, 1f),
                new Color(0.85f, 0.8f, 0.75f, 1f),
                new Color(0.7f, 0.65f, 0.6f, 1f),
                new Color(0.8f, 0.7f, 0.5f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Leather);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateCraftingTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Crafting Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.08f, 0.05f, 0.95f),
                new Color(0.55f, 0.4f, 0.25f, 0.9f),
                new Color(0.9f, 0.75f, 0.4f, 1f),
                new Color(0.85f, 0.8f, 0.7f, 1f),
                new Color(0.7f, 0.65f, 0.55f, 1f),
                new Color(0.9f, 0.6f, 0.2f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Thorn);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Scale);
            return theme;
        }

        public static UIDesignTheme CreateCookingTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Cooking Theme";
            theme.SetColorSet(
                new Color(0.12f, 0.08f, 0.04f, 0.95f),
                new Color(0.6f, 0.4f, 0.2f, 0.9f),
                new Color(0.95f, 0.8f, 0.5f, 1f),
                new Color(0.9f, 0.85f, 0.7f, 1f),
                new Color(0.75f, 0.7f, 0.6f, 1f),
                new Color(0.9f, 0.6f, 0.2f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Fabric);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateRepairTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Repair Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.06f, 0.04f, 0.95f),
                new Color(0.5f, 0.4f, 0.3f, 0.9f),
                new Color(0.85f, 0.75f, 0.55f, 1f),
                new Color(0.8f, 0.75f, 0.65f, 1f),
                new Color(0.65f, 0.6f, 0.5f, 1f),
                new Color(0.8f, 0.6f, 0.3f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Slide);
            return theme;
        }

        public static UIDesignTheme CreateRecipeTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Recipe Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.45f, 0.4f, 0.35f, 0.9f),
                new Color(0.8f, 0.75f, 0.6f, 1f),
                new Color(0.85f, 0.8f, 0.75f, 1f),
                new Color(0.7f, 0.65f, 0.6f, 1f),
                new Color(0.7f, 0.6f, 0.4f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateQuestTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Quest Theme";
            theme.SetColorSet(
                new Color(0.06f, 0.08f, 0.12f, 0.95f),
                new Color(0.4f, 0.5f, 0.6f, 0.9f),
                new Color(0.6f, 0.8f, 1f, 1f),
                new Color(0.75f, 0.85f, 0.95f, 1f),
                new Color(0.55f, 0.7f, 0.8f, 1f),
                new Color(0.4f, 0.7f, 1f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateTooltipTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Tooltip Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.08f, 0.98f),
                new Color(0.5f, 0.5f, 0.6f, 0.9f),
                new Color(0.85f, 0.85f, 0.9f, 1f),
                new Color(0.9f, 0.9f, 0.95f, 1f),
                new Color(0.7f, 0.7f, 0.8f, 1f),
                new Color(0.5f, 0.7f, 0.9f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme CreateInventoryTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Inventory Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.4f, 0.45f, 0.5f, 0.9f),
                new Color(0.7f, 0.8f, 0.9f, 1f),
                new Color(0.8f, 0.85f, 0.9f, 1f),
                new Color(0.6f, 0.65f, 0.7f, 1f),
                new Color(0.4f, 0.7f, 0.9f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Wood);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Slide);
            return theme;
        }

        public static UIDesignTheme CreateStatusTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Status Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.1f, 0.95f),
                new Color(0.5f, 0.4f, 0.3f, 0.9f),
                new Color(0.9f, 0.85f, 0.7f, 1f),
                new Color(0.85f, 0.8f, 0.75f, 1f),
                new Color(0.7f, 0.65f, 0.6f, 1f),
                new Color(0.8f, 0.7f, 0.5f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Leather);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateMedievalMapTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Medieval Map Theme";
            theme.SetColorSet(
                new Color(0.12f, 0.1f, 0.06f, 0.95f),
                new Color(0.55f, 0.45f, 0.3f, 0.9f),
                new Color(0.9f, 0.8f, 0.5f, 1f),
                new Color(0.85f, 0.8f, 0.65f, 1f),
                new Color(0.7f, 0.65f, 0.5f, 1f),
                new Color(0.85f, 0.7f, 0.3f, 1f)
            );
            theme.SetMedievalBackground(true, "Parchment");
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateMedievalShopTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Medieval Shop Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.08f, 0.05f, 0.95f),
                new Color(0.6f, 0.4f, 0.2f, 0.9f),
                new Color(0.95f, 0.8f, 0.5f, 1f),
                new Color(0.9f, 0.85f, 0.7f, 1f),
                new Color(0.75f, 0.7f, 0.6f, 1f),
                new Color(0.9f, 0.6f, 0.2f, 1f)
            );
            theme.SetMedievalBackground(true, "Parchment");
            theme.SetPatternType(UIDesignTheme.PatternType.Fabric);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme CreateMinimapTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Minimap Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.08f, 0.9f),
                new Color(0.4f, 0.4f, 0.5f, 0.8f),
                new Color(0.7f, 0.7f, 0.8f, 1f),
                new Color(0.8f, 0.8f, 0.9f, 1f),
                new Color(0.6f, 0.6f, 0.7f, 1f),
                new Color(0.5f, 0.6f, 0.8f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Stone);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        // ================================================================
        // 별도 네이밍 패턴 (Theme 접미사)
        // ================================================================

        public static UIDesignTheme EnvoyTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Envoy Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.06f, 0.1f, 0.95f),
                new Color(0.5f, 0.4f, 0.6f, 0.9f),
                new Color(0.8f, 0.7f, 0.9f, 1f),
                new Color(0.85f, 0.8f, 0.9f, 1f),
                new Color(0.7f, 0.65f, 0.75f, 1f),
                new Color(0.7f, 0.5f, 0.8f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Fabric);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme ChurchTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Church Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.08f, 0.05f, 0.95f),
                new Color(0.4f, 0.5f, 0.4f, 0.9f),
                new Color(0.7f, 0.9f, 0.7f, 1f),
                new Color(0.8f, 0.95f, 0.8f, 1f),
                new Color(0.65f, 0.8f, 0.65f, 1f),
                new Color(0.5f, 0.7f, 0.5f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Stone);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme MercenaryTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Mercenary Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.06f, 0.04f, 0.95f),
                new Color(0.55f, 0.35f, 0.25f, 0.9f),
                new Color(0.9f, 0.6f, 0.3f, 1f),
                new Color(0.85f, 0.7f, 0.55f, 1f),
                new Color(0.7f, 0.55f, 0.4f, 1f),
                new Color(0.85f, 0.5f, 0.2f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Leather);
            theme.SetBorderType(UIDesignTheme.BorderType.Thorn);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Scale);
            return theme;
        }

        public static UIDesignTheme LordAudienceTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Lord Audience Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.05f, 0.1f, 0.95f),
                new Color(0.5f, 0.35f, 0.55f, 0.9f),
                new Color(0.85f, 0.65f, 0.9f, 1f),
                new Color(0.9f, 0.8f, 0.95f, 1f),
                new Color(0.75f, 0.65f, 0.8f, 1f),
                new Color(0.8f, 0.5f, 0.85f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Marble);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme RevengeTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Revenge Theme";
            theme.SetColorSet(
                new Color(0.12f, 0.02f, 0.02f, 0.95f),
                new Color(0.6f, 0.2f, 0.2f, 0.9f),
                new Color(0.9f, 0.3f, 0.3f, 1f),
                new Color(0.95f, 0.6f, 0.6f, 1f),
                new Color(0.8f, 0.4f, 0.4f, 1f),
                new Color(0.9f, 0.2f, 0.2f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Barbed);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Pulse);
            return theme;
        }

        public static UIDesignTheme SettingsTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Settings Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.08f, 0.1f, 0.95f),
                new Color(0.4f, 0.45f, 0.5f, 0.9f),
                new Color(0.7f, 0.8f, 0.9f, 1f),
                new Color(0.8f, 0.85f, 0.9f, 1f),
                new Color(0.6f, 0.65f, 0.7f, 1f),
                new Color(0.4f, 0.7f, 0.9f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Wood);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme NPCDialogueTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "NPC Dialogue Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.08f, 0.05f, 0.95f),
                new Color(0.55f, 0.4f, 0.25f, 0.9f),
                new Color(0.9f, 0.75f, 0.4f, 1f),
                new Color(0.85f, 0.8f, 0.7f, 1f),
                new Color(0.7f, 0.65f, 0.55f, 1f),
                new Color(0.9f, 0.6f, 0.2f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme SpyTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Spy Theme";
            theme.SetColorSet(
                new Color(0.02f, 0.05f, 0.02f, 0.95f),
                new Color(0.3f, 0.5f, 0.3f, 0.9f),
                new Color(0.5f, 0.8f, 0.5f, 1f),
                new Color(0.7f, 0.9f, 0.7f, 1f),
                new Color(0.55f, 0.75f, 0.55f, 1f),
                new Color(0.4f, 0.7f, 0.4f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Chain);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme FlagRegTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Flag Registration Theme";
            theme.SetColorSet(
                new Color(0.08f, 0.05f, 0.1f, 0.95f),
                new Color(0.5f, 0.35f, 0.55f, 0.9f),
                new Color(0.85f, 0.65f, 0.9f, 1f),
                new Color(0.9f, 0.8f, 0.95f, 1f),
                new Color(0.75f, 0.65f, 0.8f, 1f),
                new Color(0.8f, 0.5f, 0.85f, 1f)
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Marble);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        // ================================================================
        // 추가 테마 팩토리 메서드 (Phase 33 확장)
        // ================================================================

        public static UIDesignTheme DeathScreenTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Death Screen Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.05f, 0.95f),    // BgColor - 잿빛
                new Color(0.2f, 0.2f, 0.2f, 0.9f),         // BorderColor
                new Color(0.6f, 0.1f, 0.1f, 1f),           // TitleColor - 어두운 적색
                new Color(0.5f, 0.5f, 0.5f, 1f),           // TextColor
                new Color(0.35f, 0.35f, 0.35f, 1f),        // SubTextColor
                new Color(0.7f, 0.2f, 0.2f, 1f)            // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Stone);
            theme.SetBorderType(UIDesignTheme.BorderType.None);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme EscMenuTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Esc Menu Theme";
            theme.SetColorSet(
                new Color(0f, 0f, 0f, 0.68f),              // BgColor - Glassmorphism
                new Color(0.3f, 0.3f, 0.35f, 0.5f),        // BorderColor
                new Color(0.9f, 0.9f, 0.95f, 1f),          // TitleColor
                new Color(0.85f, 0.85f, 0.9f, 1f),         // TextColor
                new Color(0.65f, 0.65f, 0.7f, 1f),         // SubTextColor
                new Color(0.5f, 0.7f, 0.9f, 1f)            // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Glass);
            theme.SetBorderType(UIDesignTheme.BorderType.None);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme MainMenuTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Main Menu Theme";
            theme.SetColorSet(
                new Color(0.04f, 0.04f, 0.04f, 0.95f),     // BgColor - 진한 블랙
                new Color(0.6f, 0.5f, 0.2f, 0.9f),         // BorderColor - 골드
                new Color(0.95f, 0.85f, 0.3f, 1f),         // TitleColor - 골드
                new Color(0.9f, 0.85f, 0.7f, 1f),          // TextColor
                new Color(0.75f, 0.7f, 0.55f, 1f),         // SubTextColor
                new Color(1f, 0.85f, 0.2f, 1f)             // AccentColor - 밝은 골드
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Pulse);
            return theme;
        }

        public static UIDesignTheme AchievementTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Achievement Theme";
            theme.SetColorSet(
                new Color(0.5f, 0.4f, 0f, 0.95f),          // BgColor - 골드메달
                new Color(0.7f, 0.6f, 0.2f, 0.9f),         // BorderColor
                new Color(1f, 0.9f, 0.3f, 1f),             // TitleColor - 밝은 골드
                new Color(0.95f, 0.85f, 0.5f, 1f),         // TextColor
                new Color(0.8f, 0.7f, 0.3f, 1f),           // SubTextColor
                new Color(1f, 0.8f, 0f, 1f)                // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Shield);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Scale);
            return theme;
        }

        public static UIDesignTheme TutorialTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Tutorial Theme";
            theme.SetColorSet(
                new Color(1f, 0.99f, 0.9f, 0.95f),         // BgColor - 깔끔한 양피지
                new Color(0.5f, 0.4f, 0.3f, 0.9f),         // BorderColor
                new Color(0.2f, 0.15f, 0.1f, 1f),          // TitleColor - 진한 갈색
                new Color(0.3f, 0.25f, 0.2f, 1f),          // TextColor
                new Color(0.4f, 0.35f, 0.3f, 1f),          // SubTextColor
                new Color(0.6f, 0.45f, 0.25f, 1f)          // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Parchment);
            theme.SetBorderType(UIDesignTheme.BorderType.Filigree);
            theme.SetAnimationType(UIDesignTheme.AnimationType.FadeSlide);
            return theme;
        }

        public static UIDesignTheme GuardInfoTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Guard Info Theme";
            theme.SetColorSet(
                new Color(0.24f, 0.29f, 0.18f, 0.95f),     // BgColor - 카키 군복
                new Color(0.35f, 0.4f, 0.25f, 0.9f),       // BorderColor
                new Color(0.7f, 0.75f, 0.5f, 1f),          // TitleColor
                new Color(0.8f, 0.85f, 0.7f, 1f),          // TextColor
                new Color(0.6f, 0.65f, 0.5f, 1f),          // SubTextColor
                new Color(0.55f, 0.6f, 0.35f, 1f)          // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Fabric);
            theme.SetBorderType(UIDesignTheme.BorderType.Chain);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme LoadingScreenTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Loading Screen Theme";
            theme.SetColorSet(
                new Color(0.05f, 0.05f, 0.1f, 0.95f),      // BgColor - 나침반
                new Color(0.3f, 0.3f, 0.4f, 0.9f),         // BorderColor
                new Color(0.7f, 0.8f, 1f, 1f),             // TitleColor - 푸른빛
                new Color(0.8f, 0.85f, 0.95f, 1f),         // TextColor
                new Color(0.6f, 0.65f, 0.75f, 1f),         // SubTextColor
                new Color(0.4f, 0.6f, 0.9f, 1f)            // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Glass);
            theme.SetBorderType(UIDesignTheme.BorderType.Rune);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme HerbRespawnTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Herb Respawn Theme";
            theme.SetColorSet(
                new Color(0f, 0.2f, 0f, 0.95f),            // BgColor - 녹색원형
                new Color(0.1f, 0.4f, 0.1f, 0.9f),         // BorderColor
                new Color(0.3f, 0.9f, 0.3f, 1f),           // TitleColor - 밝은 녹색
                new Color(0.5f, 0.95f, 0.5f, 1f),          // TextColor
                new Color(0.35f, 0.8f, 0.35f, 1f),         // SubTextColor
                new Color(0.2f, 1f, 0.2f, 1f)              // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Wood);
            theme.SetBorderType(UIDesignTheme.BorderType.None);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Pulse);
            return theme;
        }

        public static UIDesignTheme MonsterLevelTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Monster Level Theme";
            theme.SetColorSet(
                new Color(0.1f, 0.08f, 0.05f, 0.95f),      // BgColor - 티어별
                new Color(0.4f, 0.3f, 0.2f, 0.9f),         // BorderColor
                new Color(0.9f, 0.6f, 0.3f, 1f),           // TitleColor - 주황빛
                new Color(0.85f, 0.75f, 0.6f, 1f),         // TextColor
                new Color(0.7f, 0.6f, 0.5f, 1f),           // SubTextColor
                new Color(0.95f, 0.5f, 0.2f, 1f)           // AccentColor
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Leather);
            theme.SetBorderType(UIDesignTheme.BorderType.Thorn);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }

        public static UIDesignTheme GuardHUDTheme()
        {
            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.name = "Guard HUD Theme";
            theme.SetColorSet(
                new Color(0.15f, 0.2f, 0.22f, 0.95f),      // BgColor - 전술격자
                new Color(0.3f, 0.4f, 0.45f, 0.9f),        // BorderColor
                new Color(0.5f, 0.8f, 0.9f, 1f),           // TitleColor - 시안
                new Color(0.7f, 0.9f, 0.95f, 1f),          // TextColor
                new Color(0.5f, 0.7f, 0.75f, 1f),          // SubTextColor
                new Color(0.3f, 0.9f, 1f, 1f)              // AccentColor - 밝은 시안
            );
            theme.SetPatternType(UIDesignTheme.PatternType.Metal);
            theme.SetBorderType(UIDesignTheme.BorderType.Chain);
            theme.SetAnimationType(UIDesignTheme.AnimationType.Fade);
            return theme;
        }
    }
}