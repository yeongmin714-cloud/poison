using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 티어드 GLB 모델 교체를 관리하는 에디터 도구.
/// UserProvided/ 폴더에서 _tier1~_tier5 접미사가 있는 GLB 파일을 검색하고,
/// 기본 이름별로 그룹화하여 사용 가능/누락 티어를 표시합니다.
/// </summary>
public static class LevelModelSwapper
{
    private const string UserProvidedFolder = "Assets/Resources/Models/UserProvided";

    /// <summary>
    /// 티어드 GLB 파일을 검색하고 결과를 에디터 콘솔에 보고합니다.
    /// 메뉴: Tools > Level Model Swapper > Scan Tiered Models
    /// </summary>
    [MenuItem("Tools/Level Model Swapper/Scan Tiered Models")]
    public static void ScanTieredModels()
    {
        string dataFolder = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
        if (!Directory.Exists(dataFolder))
        {
            Debug.LogWarning($"[LevelModelSwapper] 폴더 없음: {dataFolder}");
            return;
        }

        var glbFiles = Directory.GetFiles(dataFolder, "*.glb");
        if (glbFiles.Length == 0)
        {
            Debug.Log("[LevelModelSwapper] GLB 파일 없음");
            return;
        }

        // 기본 이름별로 티어드 파일 그룹화
        var tierGroups = new Dictionary<string, List<TierFileInfo>>();
        var plainFiles = new List<string>();

        foreach (var glbPath in glbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(glbPath);

            if (ModelMapping.TryParseTierSuffix(fileName, out string baseName, out string suffix))
            {
                string assetPath = GetRelativeAssetPath(glbPath);
                var (placeholderName, _) = ModelMapping.GetMapping(baseName);

                if (!tierGroups.ContainsKey(baseName))
                    tierGroups[baseName] = new List<TierFileInfo>();

                tierGroups[baseName].Add(new TierFileInfo
                {
                    fileName = fileName,
                    tierSuffix = suffix,
                    assetPath = assetPath,
                    placeholderName = placeholderName ?? "(매핑 없음)"
                });
            }
            else
            {
                plainFiles.Add(fileName);
            }
        }

        // 보고서 출력
        Debug.Log($"[LevelModelSwapper] === 티어드 모델 검사 결과 ===");
        Debug.Log($"[LevelModelSwapper] 전체 GLB 파일: {glbFiles.Length}개");
        Debug.Log($"[LevelModelSwapper] 티어드 파일 그룹: {tierGroups.Count}개");
        Debug.Log($"[LevelModelSwapper] 일반(Plain) 파일: {plainFiles.Count}개");

        int totalTierFiles = 0;
        foreach (var kvp in tierGroups)
            totalTierFiles += kvp.Value.Count;
        Debug.Log($"[LevelModelSwapper] 티어드 파일 총계: {totalTierFiles}개");

        foreach (var kvp in tierGroups)
        {
            var (placeholderName, _) = ModelMapping.GetMapping(kvp.Key);
            Debug.Log($"[LevelModelSwapper] ── 기본 이름: '{kvp.Key}' → Placeholder: '{placeholderName}' (파일 {kvp.Value.Count}개)");

            string[] availableTiers = ModelMapping.GetAvailableTiers(kvp.Key);
            var foundTiers = new HashSet<string>();
            foreach (var info in kvp.Value)
                foundTiers.Add(info.tierSuffix);

            foreach (string tier in availableTiers)
            {
                bool hasFile = foundTiers.Contains(tier);
                Debug.Log($"[LevelModelSwapper]   [{(hasFile ? "✓" : " ")}] {tier} {(hasFile ? "" : "(파일 누락)")}");
            }
        }

        Debug.Log($"[LevelModelSwapper] === 검사 완료 ===");
    }

    /// <summary>
    /// 모든 티어드 GLB 파일을 목록으로 출력합니다.
    /// 메뉴: Tools > Level Model Swapper > List All Tiered Files
    /// </summary>
    [MenuItem("Tools/Level Model Swapper/List All Tiered Files")]
    public static void ListAllTieredFiles()
    {
        string dataFolder = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
        if (!Directory.Exists(dataFolder))
        {
            Debug.LogWarning($"[LevelModelSwapper] 폴더 없음: {dataFolder}");
            return;
        }

        var glbFiles = Directory.GetFiles(dataFolder, "*.glb");
        Debug.Log($"[LevelModelSwapper] === 모든 티어드 GLB 파일 ===");
        int tieredCount = 0;

        foreach (var glbPath in glbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(glbPath);

            if (ModelMapping.TryParseTierSuffix(fileName, out string baseName, out string suffix))
            {
                var (placeholderName, _) = ModelMapping.GetMapping(baseName);
                Debug.Log($"[LevelModelSwapper] {fileName}.glb → base: '{baseName}', tier: '{suffix}', placeholder: '{placeholderName ?? "(없음)"}'");
                tieredCount++;
            }
        }

        if (tieredCount == 0)
            Debug.Log("[LevelModelSwapper] 티어드 GLB 파일 없음");
        else
            Debug.Log($"[LevelModelSwapper] 총 {tieredCount}개 티어드 파일");
    }

    /// <summary>
    /// 티어가 누락된 기본 이름을 보고합니다.
    /// 메뉴: Tools > Level Model Swapper > Report Missing Tiers
    /// </summary>
    [MenuItem("Tools/Level Model Swapper/Report Missing Tiers")]
    public static void ReportMissingTiers()
    {
        string dataFolder = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");
        if (!Directory.Exists(dataFolder))
        {
            Debug.LogWarning($"[LevelModelSwapper] 폴더 없음: {dataFolder}");
            return;
        }

        var glbFiles = Directory.GetFiles(dataFolder, "*.glb");

        // 티어드 파일 그룹화
        var tierGroups = new Dictionary<string, HashSet<string>>();

        foreach (var glbPath in glbFiles)
        {
            string fileName = Path.GetFileNameWithoutExtension(glbPath);

            if (ModelMapping.TryParseTierSuffix(fileName, out string baseName, out string suffix))
            {
                if (!tierGroups.ContainsKey(baseName))
                    tierGroups[baseName] = new HashSet<string>();
                tierGroups[baseName].Add(suffix);
            }
        }

        Debug.Log($"[LevelModelSwapper] === 티어 누락 보고 ===");
        int missingCount = 0;

        foreach (var kvp in tierGroups)
        {
            string[] allTiers = ModelMapping.GetAvailableTiers(kvp.Key);
            var missingTiers = new List<string>();

            foreach (string tier in allTiers)
            {
                if (!kvp.Value.Contains(tier))
                    missingTiers.Add(tier);
            }

            if (missingTiers.Count > 0)
            {
                var (placeholderName, _) = ModelMapping.GetMapping(kvp.Key);
                Debug.Log($"[LevelModelSwapper] '{kvp.Key}' (→ {placeholderName}): {string.Join(", ", missingTiers)} 누락");
                missingCount++;
            }
        }

        if (missingCount == 0)
            Debug.Log("[LevelModelSwapper] 모든 기본 이름에 대해 전체 티어 존재 ✓");
        else
            Debug.Log($"[LevelModelSwapper] {missingCount}개 기본 이름에서 티어 누락");
    }

    /// <summary>
    /// 절대 경로 → Assets/... 상대 경로 변환
    /// </summary>
    static string GetRelativeAssetPath(string absolutePath)
    {
        // Convert WSL path to Windows path if necessary
        if (absolutePath.StartsWith("/mnt/"))
        {
            var driveLetter = absolutePath[2];
            var rest = absolutePath.Substring(3);
            absolutePath = char.ToUpper(driveLetter) + ":" + rest.Replace('/', '\\');
        }

        absolutePath = absolutePath.Replace('\\', '/');
        string dataPath = Application.dataPath;
        if (absolutePath.StartsWith(dataPath))
        {
            return "Assets" + absolutePath.Substring(dataPath.Length);
        }
        return absolutePath;
    }

    /// <summary>
    /// 티어드 GLB 파일 정보를 저장하는 내부 구조체.
    /// </summary>
    struct TierFileInfo
    {
        /// <summary>파일명 (확장자 제외)</summary>
        public string fileName;

        /// <summary>티어 접미사 (예: "_tier1")</summary>
        public string tierSuffix;

        /// <summary>Assets/... 상대 경로</summary>
        public string assetPath;

        /// <summary>매핑된 Placeholder 이름</summary>
        public string placeholderName;
    }
}