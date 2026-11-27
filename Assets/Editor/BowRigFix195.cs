using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [195차 P3] 활쏘기 애니 리그·배선 수리 (원샷 에디터 스크립트 — batchmode -executeMethod BowRigFix195.Run195로 실행).
/// 실측 근원 3건 (런타임 트리거 11회 발화에도 시각 변화 0 — 사용자 리포트 "활쏘기 애니 교체 안 됨"):
/// ① Player_AC.ArcheryShot 모션이 병사 활쏘기.fbx(guid 467cd2e6)를 가리킴 — 플레이어/병사 클립 교차 배선.
///    (실측: 플레이어 활쏘기.fbx.meta guid=772a9018, 병사 활쏘기.fbx.meta guid=467cd2e6 — 168차 기록 뱅크의 guid 귀속이 뒤바뀐 것)
/// ② 플레이어 활쏘기.fbx·병사 활쏘기.fbx·활 대기/달리기 withSkin FBX가 animationType=2(Generic)로 임포트됨 —
///    제네릭 클립은 휴머노이드 아바타 리그에 리타깃 불가(horse168 동류 — 상태는 재생되는데 몸이 안 움직임).
/// ③ SoldierArcher_AC.Attack 모션이 플레이어 활쏘기.fbx(772a9018)로 교차 — 병사 아처도 동일 증상.
/// 수리: ①Generic→Humanoid 전환+재임포트(멱등) ②모션 원본 교정(플레이어=플레이어 활쏘기, 병사=병사 활쏘기) ③전수 로그.
/// Draw_and_Shoot_from_Back_1.fbx(MeshyUser)는 이미 Humanoid(실측 animationType: 3) — 무수정.
/// </summary>
public static class BowRigFix195
{
    const string PlayerShotFbx = "Assets/플레이어 애니메이션/플레이어 활쏘기.fbx";
    const string GuardShotFbx = "Assets/Animations/병사 활쏘기.fbx";
    const string BowIdleFbx = "Assets/플레이어 애니메이션/Meshy_AI_Cartoon_Villager_Boy_biped_Animation_Idle_Holding_Bow_withSkin.fbx";
    const string BowRunFbx = "Assets/플레이어 애니메이션/Meshy_AI_Cartoon_Villager_Boy_biped_Animation_Run_Forward_with_Bow_withSkin.fbx";
    const string PlayerCtrl = "Assets/Resources/Animation/Controllers/Player_AC.controller";
    const string GuardCtrl = "Assets/Resources/Animation/Controllers/SoldierArcher_AC.controller";

    public static void Run195()
    {
        int fixedRigs = 0, fixedMotions = 0;

        // ① 리그 Humanoid 전환 (멱등 — 이미 Humanoid면 스킵)
        fixedRigs += FixRig(PlayerShotFbx);
        fixedRigs += FixRig(GuardShotFbx);
        fixedRigs += FixRig(BowIdleFbx);
        fixedRigs += FixRig(BowRunFbx);

        // ② 플레이어 ArcheryShot 모션 교정 — 플레이어 활쏘기.fbx Take 001
        var shotClip = FindTake(PlayerShotFbx);
        fixedMotions += RewireMotion(PlayerCtrl, "ArcheryShot", shotClip, "player");
        // ③ 병사 Attack 모션 교정 — 병사 활쏘기.fbx Take 001
        var guardClip = FindTake(GuardShotFbx);
        fixedMotions += RewireMotion(GuardCtrl, "Attack", guardClip, "guard");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[BowRigFix195] ✅ 리그 전환 {fixedRigs}건 / 모션 교정 {fixedMotions}건 — 활쏘기 리타깃 경로 정상화");
        EditorApplication.Exit(0);
    }

    static int FixRig(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) { Debug.LogError($"[BowRigFix195] 임포터 없음: {path}"); return 0; }
        // [195차 P3] Unity 6(6000.x) 열거형 — Humanoid 멤버가 Human으로 노출(원시값 3 동일). 정수 캐스팅으로 버전 무관 고정.
        var humanoidType = (ModelImporterAnimationType)3;
        if (importer.animationType == humanoidType)
        {
            Debug.Log($"[BowRigFix195] 이미 Humanoid — 스킵(멱등): {path}");
            return 0;
        }
        importer.animationType = humanoidType;
        importer.SaveAndReimport();
        // Unity 6 — ModelImporter.avatar 프로퍼티 부재 → 임포트 산출 에셋에서 Avatar 취득.
        var av = AssetDatabase.LoadAllAssetsAtPath(path)?.OfType<Avatar>().FirstOrDefault();
        Debug.Log($"[BowRigFix195] 리그 전환 Generic→Humanoid: {path} avatar={(av != null ? av.name + " isHuman=" + av.isHuman : "NULL(수동 확인 필요)")}");
        return 1;
    }

    static AnimationClip FindTake(string fbxPath)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var take = all?.OfType<AnimationClip>().FirstOrDefault(c => c.name == "Take 001")
                   ?? all?.OfType<AnimationClip>().FirstOrDefault();
        if (take == null) Debug.LogError($"[BowRigFix195] 클립 미발견: {fbxPath}");
        return take;
    }

    static int RewireMotion(string ctrlPath, string stateName, AnimationClip clip, string label)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogError($"[BowRigFix195] 컨트롤러 로드 실패: {ctrlPath}"); return 0; }
        if (clip == null) { Debug.LogError($"[BowRigFix195] 클립 null — {ctrlPath}.{stateName} 교정 보류"); return 0; }
        var sm = ctrl.layers[0].stateMachine;
        var st = sm.states.FirstOrDefault(s => s.state != null && s.state.name == stateName).state;
        if (st == null) { Debug.LogError($"[BowRigFix195] 상태 미발견: {stateName} @ {ctrlPath}"); return 0; }
        if (st.motion == clip)
        {
            Debug.Log($"[BowRigFix195] {label}.{stateName} 모션 이미 올바름 — 스킵(멱등)");
            return 0;
        }
        var oldName = st.motion != null ? st.motion.name : "NULL";
        st.motion = clip;
        EditorUtility.SetDirty(ctrl);
        Debug.Log($"[BowRigFix195] {label}.{stateName} 모션 교정: {oldName} → {clip.name} ({clip.GetType().Name})");
        return 1;
    }
}
