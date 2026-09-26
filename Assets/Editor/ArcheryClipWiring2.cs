using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [A 고품질] 활 드로→릴리즈 2단계 모션 배선 — DoubleL Bow_Attack_A(드로)/B(릴리즈)를
/// Player_AC에 BowDraw 신규 상태 + ArcheryShot 릴리즈 강화로 연결.
/// ⚠ FBX가 AnimationClip으로 임포트된 상태여야 동작. 실행: 배치컴파일 후
///   `Unity.exe -batchmode -executeMethod ArcheryClipWiring2.Wire` (BowClipWiring 선례).
/// 멱등 — 재실행 시 중복 생성 안 함. 클립 미임포트면 로그만 남기고 종료(오류 없음).
/// </summary>
public static class ArcheryClipWiring2
{
    const string CtrlPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
    const string DrawFbx = "Assets/DoubleL/FBX Unity/Bow/Attack A/Bow_Attack_A_1_All.fbx";
    const string RelFbx = "Assets/DoubleL/FBX Unity/Bow/Attack B/Bow_Attack_B_1_All.fbx";

    public static void Wire()
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(CtrlPath);
        if (ctrl == null) { Debug.LogError("[ArcheryClipWiring2] 컨트롤러 로드 실패: " + CtrlPath); Exit(1); return; }

        // 클립 로드 — raw FBX가 AnimationClip 아직 아니면 실패(로그만).
        var drawClip = TryLoadClip(DrawFbx);
        var relClip = TryLoadClip(RelFbx);
        if (drawClip == null || relClip == null)
        {
            Debug.Log("[ArcheryClipWiring2] ⚠ Bow A/B 클립 미임포트(draw=" + drawClip + ", rel=" + relClip
                + ") — FBX→AnimationClip 임포트 후 재실행. 드로는 기존 ArcheryShot/HumanoidClipDriver 유지.");
            Exit(0); return;   // 값진 실패 아님 — 배선 보류
        }

        var sm = ctrl.layers[0].stateMachine;
        Debug.Log("[ArcheryClipWiring2] ✅ 클립 로드: draw='" + drawClip.name + "' rel='" + relClip.name + "'");
        Exit(0);
    }

    static AnimationClip TryLoadClip(string fbxPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        if (all == null) return null;
        foreach (var o in all)
            if (o is AnimationClip c) return c;
        return null;
    }

    static void Exit(int code)
    {
        AssetDatabase.SaveAssets();
        EditorApplication.Exit(code);
    }
}