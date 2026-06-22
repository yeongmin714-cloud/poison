#nullable disable
using UnityEngine;
using UnityEditor;
using ProjectName.UI.Themes;

namespace ProjectName.UI.Editor
{
    /// <summary>
    /// Phase 33 UI-01: UI 테마 에셋 생성 Editor 스크립트.
    /// Tools/Phase 33/Create UI Theme Assets 메뉴에서 모든 테마용 SO 에셋을 생성합니다.
    /// </summary>
    public static class Phase33_CreateThemeAssets
    {
        private const string THEMES_FOLDER = "Assets/Resources/Themes";

        [MenuItem("Tools/Phase 33/Create UI Theme Assets")]
        public static void CreateAllThemeAssets()
        {
            EnsureThemesFolder();

            CreateTheme("Default Dark", "⚔️",
                UIDesignTheme.PatternType.Parchment,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.None,
                UIDesignTheme.AnimationType.FadeSlide);

            CreateTheme("Royal Gold", "👑",
                UIDesignTheme.PatternType.Marble,
                UIDesignTheme.BorderType.Filigree,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Scale);

            CreateTheme("Dark Fantasy", "🗡️",
                UIDesignTheme.PatternType.Leather,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.Skull,
                UIDesignTheme.AnimationType.Shatter);

            CreateTheme("Arcane", "🔮",
                UIDesignTheme.PatternType.Stone,
                UIDesignTheme.BorderType.Rune,
                UIDesignTheme.DecorationType.Seal,
                UIDesignTheme.AnimationType.Flip);

            CreateTheme("Nature", "🌿",
                UIDesignTheme.PatternType.Wood,
                UIDesignTheme.BorderType.Thorn,
                UIDesignTheme.DecorationType.CornerScroll,
                UIDesignTheme.AnimationType.Bounce);

            CreateTheme("Steel Empire", "⚙️",
                UIDesignTheme.PatternType.Metal,
                UIDesignTheme.BorderType.Shield,
                UIDesignTheme.DecorationType.Rivet,
                UIDesignTheme.AnimationType.Reveal);

            CreateTheme("Crystal", "💎",
                UIDesignTheme.PatternType.Glass,
                UIDesignTheme.BorderType.Star,
                UIDesignTheme.DecorationType.Crown,
                UIDesignTheme.AnimationType.Zoom);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[Phase33] {GetThemeCount()} UI 테마 에셋이 생성됨: {THEMES_FOLDER}");
        }

        [MenuItem("Tools/Phase 33/Create UI Theme Assets", true)]
        public static bool ValidateCreateAllThemeAssets()
        {
            return true;
        }

        private static void EnsureThemesFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(THEMES_FOLDER))
                AssetDatabase.CreateFolder("Assets/Resources", "Themes");
        }

        private static void CreateTheme(string name, string iconPrefix,
            UIDesignTheme.PatternType pattern,
            UIDesignTheme.BorderType border,
            UIDesignTheme.DecorationType decoration,
            UIDesignTheme.AnimationType animation)
        {
            string path = $"{THEMES_FOLDER}/{name.Replace(" ", "")}.asset";

            // 중복 방지
            var existing = AssetDatabase.LoadAssetAtPath<UIDesignTheme>(path);
            if (existing != null)
            {
                Debug.Log($"[Phase33] 이미 존재: {path}");
                return;
            }

            var theme = ScriptableObject.CreateInstance<UIDesignTheme>();
            theme.SetColorSet(
                GetThemeBg(name),
                GetThemeBorder(name),
                GetThemeTitle(name),
                Color.white,
                new Color(0.75f, 0.75f, 0.75f, 1f),
                GetThemeAccent(name)
            );

            // Set via reflection because fields are private
            SetPrivateField(theme, "_themeName", name);
            SetPrivateField(theme, "_iconPrefix", iconPrefix);
            SetPrivateField(theme, "_patternType", pattern);
            SetPrivateField(theme, "_borderType", border);
            SetPrivateField(theme, "_decorationType", decoration);
            SetPrivateField(theme, "_animationType", animation);
            SetPrivateField(theme, "_windowWidth", 600f);
            SetPrivateField(theme, "_windowHeight", 400f);

            AssetDatabase.CreateAsset(theme, path);
            Debug.Log($"[Phase33] 생성됨: {path}");
        }

        private static void SetPrivateField(UIDesignTheme theme, string fieldName, object value)
        {
            var field = typeof(UIDesignTheme).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            if (field != null)
                field.SetValue(theme, value);
        }

        private static int GetThemeCount()
        {
            string[] guids = AssetDatabase.FindAssets("t:UIDesignTheme", new[] { THEMES_FOLDER });
            return guids.Length;
        }

        private static Color GetThemeBg(string name)
        {
            return name switch
            {
                "Default Dark" => new Color(0f, 0f, 0f, 0.88f),
                "Royal Gold" => new Color(0.15f, 0.10f, 0.05f, 0.9f),
                "Dark Fantasy" => new Color(0.08f, 0.02f, 0.05f, 0.9f),
                "Arcane" => new Color(0.05f, 0.02f, 0.15f, 0.9f),
                "Nature" => new Color(0.02f, 0.10f, 0.05f, 0.9f),
                "Steel Empire" => new Color(0.12f, 0.12f, 0.14f, 0.9f),
                "Crystal" => new Color(0.05f, 0.08f, 0.12f, 0.85f),
                _ => new Color(0f, 0f, 0f, 0.88f)
            };
        }

        private static Color GetThemeBorder(string name)
        {
            return name switch
            {
                "Default Dark" => new Color(0.85f, 0.65f, 0.15f, 0.8f),
                "Royal Gold" => new Color(0.95f, 0.75f, 0.25f, 0.9f),
                "Dark Fantasy" => new Color(0.6f, 0.1f, 0.1f, 0.85f),
                "Arcane" => new Color(0.5f, 0.3f, 0.9f, 0.8f),
                "Nature" => new Color(0.2f, 0.7f, 0.2f, 0.8f),
                "Steel Empire" => new Color(0.6f, 0.6f, 0.65f, 0.85f),
                "Crystal" => new Color(0.3f, 0.7f, 0.9f, 0.8f),
                _ => new Color(0.85f, 0.65f, 0.15f, 0.8f)
            };
        }

        private static Color GetThemeTitle(string name)
        {
            return name switch
            {
                "Default Dark" => new Color(0.9f, 0.7f, 0.3f, 1f),
                "Royal Gold" => new Color(1f, 0.8f, 0.2f, 1f),
                "Dark Fantasy" => new Color(0.8f, 0.2f, 0.15f, 1f),
                "Arcane" => new Color(0.6f, 0.4f, 1f, 1f),
                "Nature" => new Color(0.3f, 0.85f, 0.3f, 1f),
                "Steel Empire" => new Color(0.75f, 0.75f, 0.8f, 1f),
                "Crystal" => new Color(0.4f, 0.85f, 1f, 1f),
                _ => new Color(0.9f, 0.7f, 0.3f, 1f)
            };
        }

        private static Color GetThemeAccent(string name)
        {
            return name switch
            {
                "Default Dark" => new Color(0.3f, 0.5f, 0.7f, 1f),
                "Royal Gold" => new Color(0.9f, 0.7f, 0.2f, 1f),
                "Dark Fantasy" => new Color(0.7f, 0.15f, 0.15f, 1f),
                "Arcane" => new Color(0.5f, 0.3f, 0.8f, 1f),
                "Nature" => new Color(0.2f, 0.6f, 0.2f, 1f),
                "Steel Empire" => new Color(0.5f, 0.5f, 0.6f, 1f),
                "Crystal" => new Color(0.2f, 0.6f, 0.85f, 1f),
                _ => new Color(0.3f, 0.5f, 0.7f, 1f)
            };
        }
    }
}