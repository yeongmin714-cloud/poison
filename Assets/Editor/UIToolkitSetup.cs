#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// UI Toolkit Phase U0 — PanelSettings/Theme.uss 자동 생성 에디터 배치 스크립트.
/// 참조 계획서: docs/UI_TOOLKIT_MIGRATION.md
///
/// 씬 로드 시 (InitializeOnLoadMethod) 1회 실행으로 다음을 보장:
///   (1) Assets/Resources/UI 폴더 생성
///   (2) Theme.uss / UnityDefaultTheme.tss 부재 시 File.WriteAllText 기록 + import
///   (3) PanelSettings.asset 부재 시 ScriptableObject.CreateInstance로 생성
///       (themeStyleSheet=UnityDefaultTheme.tss, ScaleWithScreenSize,
///        referenceResolution 1920x1080, match 0.5)
/// 멱등 — 대상이 이미 존재하면 전부 스킵.
/// Tools/UI Toolkit/Recreate Panel Settings 메뉴로 강제 재생성 가능.
/// </summary>
[InitializeOnLoad]
public static class UIToolkitSetup
{
    public const string UIFolder         = "Assets/Resources/UI";
    public const string ThemeUssPath     = UIFolder + "/Theme.uss";
    public const string DefaultTssPath   = UIFolder + "/UnityDefaultTheme.tss";
    public const string PanelSettingsPath = UIFolder + "/PanelSettings.asset";

    private static bool _ranInDomain;

    static UIToolkitSetup()
    {
        // EditorApplication.delayCall 경유 — 도메인 리로드 직후 최소 1회만, 플레이모드 진입 중 제외.
        EditorApplication.delayCall += () =>
        {
            if (!_ranInDomain && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                _ranInDomain = true;
                EnsureResources();
            }
        };
    }

    [MenuItem("Tools/UI Toolkit/Recreate Panel Settings")]
    public static void Recreate()
    {
        EnsureResources(force: true);
    }

    /// <summary>폴더/USS/TSS/PanelSettings 보장 (force=true면 PanelSettings 존재해도 재생성).</summary>
    public static void EnsureResources(bool force = false)
    {
        // (a) 폴더 보장
        if (!AssetDatabase.IsValidFolder(UIFolder))
        {
            Directory.CreateDirectory(UIFolder);
            AssetDatabase.Refresh();
        }

        // (b) USS / TSS 기록
        string themeUss  = BuildThemeUss();
        string defaultTss = "@import url(\"unity-theme://default\");\n";
        WriteAssetFile(ThemeUssPath, themeUss, "Theme.uss");
        WriteAssetFile(DefaultTssPath, defaultTss, "UnityDefaultTheme.tss");

        // (c) PanelSettings
        if (force || AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) == null)
        {
            var themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(DefaultTssPath);
            if (themeStyleSheet == null)
                Debug.LogWarning("[UIToolkitSetup] UnityDefaultTheme.tss 미로드 — PanelSettings에 기본 테마 미할당");

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = themeStyleSheet;
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            // dynamicAtlasSettings = 기본값 유지
            AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UIToolkitSetup] PanelSettings 생성됨 → {PanelSettingsPath}");
        }

        // Theme.uss 로드 검증 로그 (코드 경로 정합성 확인용)
        var themeLoaded = AssetDatabase.LoadAssetAtPath<StyleSheet>(ThemeUssPath);
        if (themeLoaded != null)
            Debug.Log($"[UIToolkitSetup] Theme.uss 로드 성공 → {ThemeUssPath}");
        else
            Debug.LogWarning($"[UIToolkitSetup] Theme.uss 로드 실패 → {ThemeUssPath}");
    }

    private static void WriteAssetFile(string assetPath, string content, string label)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);
        bool exists = File.Exists(full);
        if (!exists)
        {
            File.WriteAllText(full, content);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"[UIToolkitSetup] {label} 생성됨 → {assetPath}");
        }
    }

    /// <summary>Theme.uss 본문 (팔레트 62차 확정 + 등급색 + 폰트 5단 + 공통 클래스).</summary>
    private static string BuildThemeUss()
    {
        return @"/* Theme.uss — Phase U0 인프라 (팔레트 + 폰트 + 공통 컴포넌트) */
/* 참조: docs/UI_TOOLKIT_MIGRATION.md, docs/UI_DESIGN_GUIDELINES.md */
/* 희귀도 등급색: EquipmentRarityData.GetRarityColor() 통일 */
/* 폰트: Assets/Resources/Fonts/NotoSansKR-VF.ttf (코드에서 할당) */

:root {
    --c-bg-panel:       rgba(28, 28, 28, 0.878);
    --c-bg-panel-dark:  rgba(20, 20, 20, 0.910);
    --c-border-bronze:  #8C6B3F;
    --c-border-gold:    #C9A227;
    --c-iron-line:      #3A3A3A;
    --c-text-primary:   #F5EFE0;
    --c-text-secondary: #B9B3A6;
    --c-accent-magic:   #4A7BD0;
    --c-accent-rare:    #E7B73A;
    --c-health-red:     #C83838;
    --c-guild-green:    #5E8C4A;
    --c-hover-gold:     #D9B45B;

    --c-rank-common:    #999999;
    --c-rank-uncommon:  #33CC33;
    --c-rank-rare:      #3366FF;
    --c-rank-epic:      #9933FF;
    --c-rank-legendary: #FF3333;
    --c-rank-unique:    #FFD900;

    --fs-xl: 60px;
    --fs-lg: 38px;
    --fs-md: 24px;
    --fs-sm: 17px;
    --fs-xs: 13px;
}

.utk-window {
    position: absolute;
    background-color: var(--c-bg-panel);
    border-width: 2px;
    border-color: var(--c-border-bronze);
    border-style: solid;
    border-radius: 4px;
    color: var(--c-text-primary);
    font-size: var(--fs-sm);
    overflow: hidden;
}

.utk-title-bar {
    flex-direction: row;
    align-items: center;
    background-color: var(--c-bg-panel-dark);
    border-bottom-width: 1px;
    border-bottom-color: var(--c-iron-line);
    border-bottom-style: solid;
    padding: 6px 8px;
    flex-shrink: 0;
}

.utk-title-label {
    flex-grow: 1;
    font-size: var(--fs-lg);
    color: var(--c-text-primary);
    -unity-font-style: bold;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.utk-close-btn {
    width: 28px;
    height: 28px;
    font-size: var(--fs-md);
    color: var(--c-text-primary);
    background-color: var(--c-bg-panel-dark);
    border-width: 1px;
    border-color: var(--c-iron-line);
    border-style: solid;
    border-radius: 2px;
    flex-shrink: 0;
}

.utk-close-btn:hover {
    background-color: var(--c-health-red);
    color: white;
}

.utk-content {
    flex-grow: 1;
    padding: 10px;
    overflow: visible;
}

.utk-btn {
    font-size: var(--fs-sm);
    color: var(--c-text-primary);
    background-color: var(--c-bg-panel-dark);
    border-width: 1px;
    border-color: var(--c-border-bronze);
    border-style: solid;
    border-radius: 3px;
    padding: 4px 14px;
    align-items: center;
    justify-content: center;
}

.utk-btn:hover { background-color: var(--c-hover-gold); color: #201A10; }
.utk-btn:active { border-color: var(--c-border-gold); }

.utk-btn--primary {
    background-color: var(--c-accent-magic);
    color: white;
    border-color: var(--c-accent-magic);
}

.utk-btn--secondary { background-color: var(--c-bg-panel-dark); }

.utk-btn--danger {
    background-color: var(--c-health-red);
    color: white;
    border-color: var(--c-health-red);
}

.utk-btn:disabled { opacity: 0.4; color: var(--c-text-secondary); }

.utk-slot {
    background-color: var(--c-bg-panel-dark);
    border-width: 1px;
    border-color: var(--c-iron-line);
    border-style: solid;
    border-radius: 3px;
    align-items: center;
    justify-content: center;
    overflow: hidden;
}

.utk-slot--hover {
    background-color: rgba(217, 180, 91, 0.15);
    border-color: var(--c-hover-gold);
}

.utk-slot__count {
    position: absolute;
    right: 3px;
    bottom: 2px;
    font-size: var(--fs-xs);
    color: var(--c-text-primary);
    -unity-font-style: bold;
}

.utk-rank--common    { border-color: var(--c-rank-common); }
.utk-rank--uncommon  { border-color: var(--c-rank-uncommon); }
.utk-rank--rare      { border-color: var(--c-rank-rare); }
.utk-rank--epic      { border-color: var(--c-rank-epic); }
.utk-rank--legendary { border-color: var(--c-rank-legendary); }
.utk-rank--unique    { border-color: var(--c-rank-unique); }

.utk-tooltip {
    position: absolute;
    background-color: var(--c-bg-panel-dark);
    border-width: 1px;
    border-color: var(--c-border-bronze);
    border-style: solid;
    border-radius: 3px;
    padding: 6px 10px;
    font-size: var(--fs-xs);
    color: var(--c-text-primary);
    max-width: 300px;
    pointer-events: none;
}

.utk-modal {
    position: absolute;
    background-color: var(--c-bg-panel);
    border-width: 2px;
    border-color: var(--c-border-gold);
    border-style: solid;
    border-radius: 4px;
    padding: 16px;
    min-width: 260px;
}

.utk-modal__overlay {
    position: absolute;
    background-color: rgba(0, 0, 0, 0.5);
}

.utk-toast {
    position: absolute;
    background-color: var(--c-bg-panel-dark);
    border-width: 1px;
    border-color: var(--c-border-bronze);
    border-style: solid;
    border-radius: 4px;
    padding: 8px 16px;
    font-size: var(--fs-sm);
    color: var(--c-text-primary);
}

.utk-toast--fadeout {
    opacity: 0;
    transition-property: opacity;
    transition-duration: 0.4s;
}
";
    }
}
#endif