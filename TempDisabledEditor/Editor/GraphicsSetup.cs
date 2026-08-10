using UnityEditor;
using UnityEngine;
using ProjectName.Systems;

#pragma warning disable 0414

/// <summary>
/// Phase 41-2: 그래픽 설정을 위한 Editor 메뉴.
/// 환경 파티클 컨트롤러 및 특수 효과 컨트롤러를 씬에 생성합니다.
/// </summary>
public class GraphicsSetup
{
    // ================================================================
    // Environment Particle Controller
    // ================================================================

    /// <summary>
    /// Tools/Graphics/Create Environment Particle Controller 메뉴.
    /// EnvironmentParticleController GameObject를 생성하고 자식 파티클 시스템을 구성합니다.
    /// </summary>
    [MenuItem("Tools/Graphics/Create Environment Particle Controller")]
    private static void CreateEnvironmentParticleController()
    {
        // 이미 존재하는지 확인
        var existing = Object.FindAnyObjectByType<EnvironmentParticleController>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog(
                "Already Exists",
                $"EnvironmentParticleController가 이미 씬에 있습니다: {existing.gameObject.name}",
                "OK"
            );
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // GameObject 생성
        var go = new GameObject("EnvironmentParticleController");
        var controller = go.AddComponent<EnvironmentParticleController>();

        // Undo 등록
        Undo.RegisterCreatedObjectUndo(go, "Create Environment Particle Controller");

        // 선택
        Selection.activeGameObject = go;

        Debug.Log("[GraphicsSetup] EnvironmentParticleController 생성 완료.");
    }

    // ================================================================
    // Special Effects Controller
    // ================================================================

    /// <summary>
    /// Tools/Graphics/Create Special Effects Controller 메뉴.
    /// SpecialEffectsController GameObject를 생성합니다.
    /// </summary>
    [MenuItem("Tools/Graphics/Create Special Effects Controller")]
    private static void CreateSpecialEffectsController()
    {
        var existing = Object.FindAnyObjectByType<SpecialEffectsController>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog(
                "Already Exists",
                $"SpecialEffectsController가 이미 씬에 있습니다: {existing.gameObject.name}",
                "OK"
            );
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        var go = new GameObject("SpecialEffectsController");
        var controller = go.AddComponent<SpecialEffectsController>();

        Undo.RegisterCreatedObjectUndo(go, "Create Special Effects Controller");

        Selection.activeGameObject = go;

        Debug.Log("[GraphicsSetup] SpecialEffectsController 생성 완료.");
    }

    // ================================================================
    // Validate (메뉴 활성화 조건)
    // ================================================================

    /// <summary>
    /// Create Environment Particle Controller 메뉴가 활성화되는 조건.
    /// 항상 활성화 (true를 반환).
    /// </summary>
    [MenuItem("Tools/Graphics/Create Environment Particle Controller", true)]
    private static bool ValidateCreateEnvironmentParticleController()
    {
        return true;
    }

    /// <summary>
    /// Create Special Effects Controller 메뉴가 활성화되는 조건.
    /// 항상 활성화 (true를 반환).
    /// </summary>
    [MenuItem("Tools/Graphics/Create Special Effects Controller", true)]
    private static bool ValidateCreateSpecialEffectsController()
    {
        return true;
    }
}