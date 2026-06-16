using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// C10-21: 모델 교체 현황 보고 에디터 도구.
/// 씬 내 모든 Placeholder 오브젝트를 스캔하여 GLB 모델로 교체된 현황을 보고합니다.
/// 메뉴: Tools/Finalize Models
/// </summary>
public static class ModelFinalizer
{
    private const string UserProvidedFolder = "Assets/Resources/Models/UserProvided";
    private const string ReportTitle = "=== 모델 교체 최종 보고서 ===";

    /// <summary>
    /// 씬 내 모든 Placeholder를 스캔하고 교체 현황 보고서를 출력합니다.
    /// 메뉴: Tools/Finalize Models
    /// </summary>
    [MenuItem("Tools/Finalize Models")]
    public static void FinalizeModels()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine(ReportTitle);
        report.AppendLine($"검사 시간: {System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();

        // 1단계: 모든 Placeholder GameObject 찾기
        var allPlaceholders = FindAllPlaceholders();
        report.AppendLine($"[검색] 씬에서 {allPlaceholders.Count}개의 Placeholder 오브젝트 발견");
        report.AppendLine();

        // 2단계: UserProvided 폴더의 GLB 파일 스캔
        var availableGlbFiles = ScanUserProvidedFolder();
        report.AppendLine($"[검색] UserProvided 폴더: {availableGlbFiles.Count}개의 GLB 파일 발견");
        report.AppendLine();

        // 3단계: 각 Placeholder의 교체 상태 확인
        int replacedCount = 0;
        int placeholderCount = 0;
        var missingGlbFiles = new List<string>();

        foreach (var placeholder in allPlaceholders)
        {
            string status = GetPlaceholderStatus(placeholder, availableGlbFiles, out bool isReplaced, out string expectedGlb);
            report.AppendLine($"  {placeholder.name}: {status}");

            if (isReplaced)
                replacedCount++;
            else
                placeholderCount++;

            if (!string.IsNullOrEmpty(expectedGlb) && !availableGlbFiles.Contains(expectedGlb))
            {
                missingGlbFiles.Add(expectedGlb);
            }
        }

        report.AppendLine();

        // 4단계: 요약
        int total = replacedCount + placeholderCount;
        report.AppendLine($"[결과] {replacedCount}/{total} Placeholder 교체 완료 ({(total > 0 ? (replacedCount * 100 / total) : 0)}%)");

        if (replacedCount == total && total > 0)
        {
            report.AppendLine("[결과] ✅ 모든 Placeholder가 GLB 모델로 교체되었습니다!");
        }
        else
        {
            report.AppendLine($"[결과] ⚠️ {placeholderCount}개의 Placeholder가 아직 교체되지 않았습니다.");
        }

        report.AppendLine();

        // 5단계: 누락 GLB 파일 목록
        if (missingGlbFiles.Count > 0)
        {
            report.AppendLine($"[누락 GLB] 총 {missingGlbFiles.Count}개의 GLB 파일이 필요합니다:");
            foreach (var glb in missingGlbFiles)
            {
                report.AppendLine($"  - {glb}.glb → 필요한 위치: {UserProvidedFolder}/");
            }
        }
        else
        {
            report.AppendLine("[누락 GLB] 모든 예상 GLB 파일이 존재합니다.");
        }

        // 콘솔에 보고서 출력
        Debug.Log(report.ToString());

        // 선택적으로 보고서를 파일로 저장
        string reportPath = Path.Combine(Application.dataPath, "../ModelFinalizeReport.txt");
        File.WriteAllText(reportPath, report.ToString());
        Debug.Log($"[ModelFinalizer] 보고서 저장 완료: {reportPath}");
    }

    /// <summary>
    /// 씬에서 모든 Placeholder 오브젝트를 찾습니다.
    /// "Placeholder_" 접두사가 있는 GameObject를 검색합니다.
    /// </summary>
    private static List<GameObject> FindAllPlaceholders()
    {
        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        var placeholders = new List<GameObject>();

        foreach (var go in allObjects)
        {
            // 씬 오브젝트만 처리 (프리팹/에셋 제외)
            if (!EditorUtility.IsPersistent(go) && go.name.StartsWith("Placeholder_"))
            {
                placeholders.Add(go);
            }
        }

        return placeholders;
    }

    /// <summary>
    /// UserProvided 폴더를 스캔하여 사용 가능한 GLB 파일 목록을 반환합니다.
    /// </summary>
    private static HashSet<string> ScanUserProvidedFolder()
    {
        var glbFiles = new HashSet<string>();
        string folderPath = Path.Combine(Application.dataPath, "Resources/Models/UserProvided");

        if (!Directory.Exists(folderPath))
        {
            Debug.LogWarning($"[ModelFinalizer] 폴더 없음: {folderPath}");
            return glbFiles;
        }

        foreach (var filePath in Directory.GetFiles(folderPath, "*.glb"))
        {
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            glbFiles.Add(fileName.ToLowerInvariant());
        }

        return glbFiles;
    }

    /// <summary>
    /// Placeholder GameObject의 교체 상태를 평가합니다.
    /// </summary>
    /// <param name="placeholder">대상 Placeholder</param>
    /// <param name="availableGlbFiles">사용 가능한 GLB 파일 목록</param>
    /// <param name="isReplaced">교체 완료 여부</param>
    /// <param name="expectedGlb">예상되는 GLB 파일명 (소문자)</param>
    /// <returns>상태 설명 문자열</returns>
    private static string GetPlaceholderStatus(GameObject placeholder, HashSet<string> availableGlbFiles, out bool isReplaced, out string expectedGlb)
    {
        isReplaced = false;
        expectedGlb = null;

        // Placeholder 이름에서 GLB 기본 이름 추출
        string baseName = ExtractGlbBaseName(placeholder.name);
        if (string.IsNullOrEmpty(baseName))
        {
            return "⚠️ 알 수 없는 Placeholder 형식";
        }

        // 예상 GLB 파일명
        expectedGlb = baseName.ToLowerInvariant();

        // Placeholder의 자식들을 검사하여 실제 모델(MeshFilter가 있는)이 있는지 확인
        bool hasMeshChildren = HasMeshChildren(placeholder);

        // PlayerPlaceholder 특수 처리
        var playerPlaceholder = placeholder.GetComponent<PlayerPlaceholder>();
        if (playerPlaceholder != null)
        {
            // PlayerPlaceholder 스크립트가 아직 있으면 교체 전
            if (playerPlaceholder.enabled)
            {
                isReplaced = false;
            }
            else
            {
                // PlayerPlaceholder가 비활성화되었거나 제거되었으면 GLB가 추가된 것
                isReplaced = HasMeshChildren(placeholder);
            }
        }
        else if (hasMeshChildren)
        {
            // Mesh를 가진 자식이 있으면 교체 완료로 간주
            isReplaced = true;
        }
        else
        {
            // Mesh가 없으면 Placeholder 그대로
            isReplaced = false;
        }

        // 확인된 GLB 파일 존재 여부
        bool glbExists = availableGlbFiles.Contains(expectedGlb);

        if (isReplaced)
        {
            return $"✅ 교체 완료 (GLB: {expectedGlb}.glb)";
        }
        else if (glbExists)
        {
            return $"🔄 GLB 파일 있음 — 교체 필요 (Tools/Swap Models from UserProvided 실행)";
        }
        else
        {
            return $"❌ GLB 파일 없음 — {expectedGlb}.glb 필요";
        }
    }

    /// <summary>
    /// Placeholder 이름에서 GLB 기본 이름을 추출합니다.
    /// "Placeholder_Castle_Blue" → "blue_castle"
    /// "Placeholder_Soldier" → "soldier"
    /// </summary>
    private static string ExtractGlbBaseName(string placeholderName)
    {
        // "Placeholder_" 접두사 제거
        string stripped = placeholderName;
        if (stripped.StartsWith("Placeholder_"))
        {
            stripped = stripped.Substring("Placeholder_".Length);
        }
        else
        {
            return null;
        }

        // CamelCase → snake_case 변환
        // 예: "CastleBlue" → "castle_blue"
        return ConvertToSnakeCase(stripped).ToLowerInvariant();
    }

    /// <summary>
    /// CamelCase 문자열을 snake_case로 변환합니다.
    /// "CastleBlue" → "castle_blue"
    /// </summary>
    private static string ConvertToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var result = new StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];

            if (char.IsUpper(c))
            {
                if (i > 0 && !char.IsUpper(input[i - 1]))
                {
                    result.Append('_');
                }
                result.Append(char.ToLower(c));
            }
            else if (c == ' ')
            {
                result.Append('_');
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// GameObject의 계층 구조에 MeshFilter 또는 SkinnedMeshRenderer를 가진 자식이 있는지 확인합니다.
    /// </summary>
    private static bool HasMeshChildren(GameObject parent)
    {
        // 자신이 메시를 가지고 있는지 확인
        if (parent.GetComponent<MeshFilter>() != null || parent.GetComponent<SkinnedMeshRenderer>() != null)
            return true;

        // 자식들 확인
        foreach (Transform child in parent.transform)
        {
            if (child.GetComponent<MeshFilter>() != null || child.GetComponent<SkinnedMeshRenderer>() != null)
                return true;

            // 재귀적으로 깊은 자식도 확인
            if (child.childCount > 0 && HasMeshChildren(child.gameObject))
                return true;
        }

        return false;
    }
}
