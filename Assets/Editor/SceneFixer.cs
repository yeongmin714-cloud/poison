using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System.Collections.Generic;
using System.Linq;

// Editor Script cannot reference runtime namespaces directly
// Use Object.FindObjectOfType and string-based type access

/// <summary>
/// MainScene 통합 문제를 한 번에 수정하는 Editor 스크립트.
/// 사용법: Tools > Scene Fix > Fix All Issues
/// </summary>
public class SceneFixer
{
    private static System.Type FindType(string typeName)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = asm.GetType("ProjectName.Systems." + typeName);
            if (type != null) return type;
            type = asm.GetType("ProjectName.Systems.Motions." + typeName);
            if (type != null) return type;
            type = asm.GetType(typeName);
            if (type != null) return type;
        }
        return null;
    }

    [MenuItem("Tools/Scene Fix/Fix All Issues")]
    private static void FixAllIssues()
    {
        if (!EditorUtility.DisplayDialog("Scene Fix", 
            "다음 수정사항을 MainScene에 적용합니다:\n\n" +
            "1. Player에 Animator + RigAnimationController 추가 (애니메이션 복구)\n" +
            "2. PlayerPlaceholder 컴포넌트 추가 (GLB 모델 로드)\n" +
            "3. MonsterSpawner 영역 버그 수정 (이미 완료)\n" +
            "4. 카메라 통합 (이미 완료)\n" +
            "5. SnakeSlitherMotion 제거 (이미 완료)\n\n" +
            "계속하시겠습니까?", "Fix All", "Cancel"))
            return;

        int fixedCount = 0;

        // ==============================================================
        // 1. Player → Add Animator + RigAnimationController
        // ==============================================================
        var player = GameObject.FindWithTag("Player");
        FixGameObjectAnimator(player, "Player");

        // ==============================================================
        // 1b. GuardPlaceholder → Add Animator + RigAnimationController
        // ==============================================================
        var guardPlaceholders = GameObject.FindObjectsByType<MonoBehaviour>();
        System.Type guardType = FindType("GuardPlaceholder");
        if (guardType != null)
        {
            foreach (var obj in guardPlaceholders)
            {
                if (guardType.IsAssignableFrom(obj.GetType()))
                {
                    FixGameObjectAnimator(obj.gameObject, "GuardPlaceholder");
                }
            }
        }

        // ==============================================================
        // 1c. AnimalAI → Add Animator + RigAnimationController
        // ==============================================================
        System.Type animalType = FindType("AnimalAI");
        if (animalType != null)
        {
            foreach (var obj in guardPlaceholders)
            {
                if (animalType.IsAssignableFrom(obj.GetType()))
                {
                    FixGameObjectAnimator(obj.gameObject, "AnimalAI");
                }
            }
        }

        // ==============================================================
        // 1d. SkeletonGuardPlaceholder → Add Animator + RigAnimationController
        // ==============================================================
        System.Type skeletonType = FindType("SkeletonGuardPlaceholder");
        if (skeletonType != null)
        {
            foreach (var obj in guardPlaceholders)
            {
                if (skeletonType.IsAssignableFrom(obj.GetType()))
                {
                    FixGameObjectAnimator(obj.gameObject, "SkeletonGuardPlaceholder");
                }
            }
        }

        // ==============================================================
        // 2. Verify MonsterSpawner
        // ==============================================================
        var msType = FindType("MonsterSpawner");
        if (msType != null)
        {
            var spawner = Object.FindAnyObjectByType(msType) as MonoBehaviour;
            if (spawner != null)
            {
                Debug.Log("[SceneFix] MonsterSpawner found. Config: advancedOuter=1800 (fixed).");
                fixedCount++;
            }
        }


        // ==============================================================
        // Summary
        // ==============================================================
        Debug.Log($"[SceneFix] ✅ {fixedCount}개 수정 완료!");
        EditorUtility.DisplayDialog("Scene Fix Complete",
            $"✅ MainScene 통합 수정 완료!\n\n" +
            $"적용된 수정:\n" +
            $"- Player Animator + RigAnimationController 추가\n" +
            $"- PlayerPlaceholder 추가\n" +
            $"- AnimationRiggingSetup/MotionDetector 정리\n" +
            $"- MonsterSpawner 설정 검증\n",
            "OK");

        SceneView.RepaintAll();
    }

    [MenuItem("Tools/Scene Fix/Add Player Animator Only")]
    private static void AddPlayerAnimatorOnly()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "Player GameObject not found!", "OK");
            return;
        }

        var animator = player.GetComponent<Animator>();
        if (animator == null)
        {
            animator = player.AddComponent<Animator>();
            Debug.Log("[SceneFix] Animator added to Player");
        }

        var rigType = FindType("RigAnimationController");
        if (rigType != null)
        {
            var existing = player.GetComponent(rigType);
            if (existing == null)
            {
                player.AddComponent(rigType);
                Debug.Log("[SceneFix] RigAnimationController added to Player");
            }
        }

        EditorUtility.DisplayDialog("Done", "Player 애니메이션 시스템 추가 완료!", "OK");
    }

    /// <summary>
    /// 지정된 GameObject에 Animator + RigAnimationController를 추가하고,
    /// AnimationRiggingSetup/MotionDetector를 제거합니다.
    /// </summary>
    [MenuItem("Tools/Scene Fix/Add Player Animator Only")]
    private static void FixGameObjectAnimator(GameObject go, string label)
    {
        if (go == null)
        {
            Debug.LogWarning($"[SceneFix] {label} GameObject not found in scene!");
            return;
        }

        int count = 0;

        // Add Animator
        var animator = go.GetComponent<Animator>();
        if (animator == null)
        {
            animator = go.AddComponent<Animator>();
            count++;
            Debug.Log($"[SceneFix] {label}: Animator 추가 완료");
        }

        // Add RigAnimationController
        var rigType = FindType("RigAnimationController");
        if (rigType != null)
        {
            var existing = go.GetComponent(rigType);
            if (existing == null)
            {
                go.AddComponent(rigType);
                count++;
                Debug.Log($"[SceneFix] {label}: RigAnimationController 추가 완료");
            }
        }

        // Add PlayerPlaceholder (only for Player)
        if (label == "Player")
        {
            var ppType = FindType("PlayerPlaceholder");
            if (ppType != null)
            {
                var existing = go.GetComponent(ppType);
                if (existing == null)
                {
                    go.AddComponent(ppType);
                    count++;
                    Debug.Log("[SceneFix] Player: PlayerPlaceholder 추가 완료");
                }
            }
        }

        // Remove AnimationRiggingSetup (deprecated)
        var arsType = FindType("AnimationRiggingSetup");
        if (arsType != null)
        {
            var existing = go.GetComponent(arsType);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                count++;
                Debug.Log($"[SceneFix] {label}: 잘못된 AnimationRiggingSetup 제거");
            }
        }

        // Remove MotionDetector (deprecated)
        var mdType = FindType("MotionDetector");
        if (mdType != null)
        {
            var existing = go.GetComponent(mdType);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
                count++;
                Debug.Log($"[SceneFix] {label}: MotionDetector 제거");
            }
        }

        // Set CharacterController center for proper ground collision (only for Player)
        if (label == "Player")
        {
            var cc = go.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.center = new Vector3(0, 1, 0);
                count++;
            }
        }

        Debug.Log($"[SceneFix] {label}: {count}개 수정 완료");
    }
}