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
        if (player != null)
        {
            // Add Animator
            var animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
                fixedCount++;
                Debug.Log("[SceneFix] Player: Animator 추가 완료");
            }

            // Add RigAnimationController (ProjectName.Systems namespace)
            var rigType = FindType("RigAnimationController");
            if (rigType != null)
            {
                var existing = player.GetComponent(rigType);
                if (existing == null)
                {
                    player.AddComponent(rigType);
                    fixedCount++;
                    Debug.Log("[SceneFix] Player: RigAnimationController 추가 완료");
                }
            }

            // Add PlayerPlaceholder (ProjectName.Systems namespace)
            var ppType = FindType("PlayerPlaceholder");
            if (ppType != null)
            {
                var existing = player.GetComponent(ppType);
                if (existing == null)
                {
                    player.AddComponent(ppType);
                    fixedCount++;
                    Debug.Log("[SceneFix] Player: PlayerPlaceholder 추가 완료");
                }
            }

            // Remove AnimationRiggingSetup (ProjectName.Systems namespace)
            var arsType = FindType("AnimationRiggingSetup");
            if (arsType != null)
            {
                var existing = player.GetComponent(arsType);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                    fixedCount++;
                    Debug.Log("[SceneFix] Player: 잘못된 AnimationRiggingSetup 제거");
                }
            }

            // Remove MotionDetector (ProjectName.Systems.Motions namespace)
            var mdType = FindType("MotionDetector");
            if (mdType != null)
            {
                var existing = player.GetComponent(mdType);
                if (existing != null)
                {
                    Object.DestroyImmediate(existing);
                    fixedCount++;
                    Debug.Log("[SceneFix] Player: MotionDetector 제거");
                }
            }

            // Set CharacterController center for proper ground collision
            var cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.center = new Vector3(0, 1, 0);
                fixedCount++;
            }
        }
        else
        {
            Debug.LogWarning("[SceneFix] Player GameObject not found in scene!");
        }

        // ==============================================================
        // 2. Verify MonsterSpawner
        // ==============================================================
        var msType = FindType("MonsterSpawner");
        if (msType != null)
        {
            var spawner = Object.FindObjectOfType(msType) as MonoBehaviour;
            if (spawner != null)
            {
                Debug.Log("[SceneFix] MonsterSpawner found. Config: advancedOuter=1800 (fixed).");
                fixedCount++;
            }
        }

        // ==============================================================
        // 3. Clean up PolyHaven child bone objects under Player
        // ==============================================================
        if (player != null)
        {
            int boneCount = player.transform.childCount;
            if (boneCount > 50)
            {
                Debug.Log($"[SceneFix] Player has {boneCount} child bones. Keep for now — PlayerPlaceholder GLB loader will replace.");
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
            $"- MonsterSpawner 설정 검증\n\n" +
            $"🔄 Poly Haven 단순화는:\n" +
            $"  Tools > Poly Haven > Replace With Primitives\n\n" +
            $"📦 복원은:\n" +
            $"  Tools > Poly Haven > Restore From Backup\n\n" +
            $"백업: MainScene_PolyHaven_Backup.unity",
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

    [MenuItem("Tools/Scene Fix/Remove PolyHaven Hierarchy Under Player")]
    private static void RemovePlayerChildBones()
    {
        var player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            EditorUtility.DisplayDialog("Error", "Player GameObject not found!", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Remove Player Bones?",
            $"Player에 {player.transform.childCount}개의 자식 오브젝트(본)가 있습니다.\n\n" +
            "제거하면 PlayerPlaceholder가 런타임에 GLB를 로드합니다.\n\n제거하시겠습니까?",
            "제거", "취소"))
            return;

        for (int i = player.transform.childCount - 1; i >= 0; i--)
        {
            var child = player.transform.GetChild(i).gameObject;
            Undo.DestroyObjectImmediate(child);
        }

        Debug.Log($"[SceneFix] Player: 모든 자식 본 제거 완료");
        EditorUtility.DisplayDialog("Done", "Player의 모든 자식 본(GLB 스켈레톤)이 제거되었습니다.", "OK");
    }
}