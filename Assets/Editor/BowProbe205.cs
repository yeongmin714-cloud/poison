using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// [205차 배치A-3] 활쏘기 애니 에디터 프로브 (batchmode -executeMethod BowProbe205.Probe — QA 서브 소관).
/// 부모 선실측(정적, 신뢰): Player_AC.ArcheryShot 모션 = {guid 772a9018cdb8e5d4b97f98af822798d6, type: 3}
/// = Assets/플레이어 애니메이션/플레이어 활쏘기.fbx ✓ (195차 교정 반영, 병사 467cd2e6 참조 0건).
/// 양 FBX animType=3(Humanoid) ✓ · 애니 데이터 실존(AnimationStack 2/curve 86) ✓ · importAnimation=1·clipAnimations=[](기본 Take) ✓.
/// 잔여 후보 3건 → 본 프로브(에디터 런타임 실측)로만 확정 가능:
///   ① 클립 서브에셋 유효성(길이 0/빈 테이크) ② 아바타 매칭(플레이어 모델 Animator 아바타 vs 클립 humanMotion)
///   ③ 상태 전이 즉시 탈출(ArcheryShot 전출입 전이 설정 실측).
/// 모션 결함 발견 시 자동 수리 분기: state.motion이 null이거나 클립 길이 0 → 플레이어 활쏘기 FBX에서
/// 유효 클립(길이>0.1s, Take 001 우선)을 ArcheryShot에 재배선 + SetDirty + SaveAssets.
/// 정상이면 무수리(유니크 가드 — 수정 파일 0). 모든 결과는 [BowProbe205] 로그로 남겨 QA/부모 판정 근거로 사용.
/// 플레이어 모델 소스: TestPlayerAnimatorBoot가 Resources.Load("Models/UserProvided/fbx/Player_Rigged_Heat") 경유로
/// PlayerModel에 Player_AC를 부착하는 계약 경로(실측 — 프리팹 에셋 전수에 Player_AC 직접 참조 없음 확인).
/// </summary>
public static class BowProbe205
{
    const string Tag = "[BowProbe205]";
    const string PlayerCtrl = "Assets/Resources/Animation/Controllers/Player_AC.controller";
    const string GuardCtrl = "Assets/Resources/Animation/Controllers/SoldierArcher_AC.controller";
    const string PlayerShotFbx = "Assets/플레이어 애니메이션/플레이어 활쏘기.fbx";
    const string GuardShotFbx = "Assets/Animations/병사 활쏘기.fbx";
    const string PlayerModelFbx = "Assets/Resources/Models/UserProvided/fbx/Player_Rigged_Heat.fbx";

    public static void Probe()
    {
        var exitCode = 0;
        try
        {
            // 본체 실측: 플레이어 ArcheryShot (결함 시 자동 수리 분기)
            ProbeStateMotion(PlayerCtrl, "ArcheryShot", PlayerShotFbx, "player", repairOnDefect: true);
            // 대조군: 병사 Archer Attack (결함이어도 무수리 — 네 소유 파일 가드, 실측만)
            ProbeStateMotion(GuardCtrl, "Attack", GuardShotFbx, "guard(대조군)", repairOnDefect: false);
            // 아바타 매칭 실측 (잔여 후보 ②)
            ProbePlayerAvatar();
        }
        catch (Exception e)
        {
            Debug.LogError($"{Tag} ❌ 프로브 예외(미완료 — 판정 보류): {e.GetType().Name}: {e.Message}\n{e.StackTrace}");
            exitCode = 1;
        }
        Debug.Log($"{Tag} ✅ 프로브 완료 — [BowProbe205] 로그 전량을 QA/부모 판정 근거로 사용");
        EditorApplication.Exit(exitCode);
    }

    static void ProbeStateMotion(string ctrlPath, string stateName, string expectedFbx, string label, bool repairOnDefect)
    {
        var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (ctrl == null) { Debug.LogError($"{Tag} ❌ 컨트롤러 로드 실패: {ctrlPath}"); return; }

        var allStates = new List<AnimatorState>();
        CollectStates(ctrl.layers[0].stateMachine, allStates);
        var st = allStates.FirstOrDefault(s => s != null && s.name == stateName);
        if (st == null) { Debug.LogError($"{Tag} ❌ 상태 미발견: {stateName} @ {ctrlPath}"); return; }

        // 컨트롤러 트리거 파라미터 존재 여부(HumanoidClipDriver _anim.SetTrigger 경로 전제)
        bool trigExists = ctrl.parameters.Any(p => p.name == stateName && p.type == AnimatorControllerParameterType.Trigger);
        Debug.Log($"{Tag} 컨트롤러: {ctrlPath} — 트리거 '{stateName}' 존재={trigExists} (파라미터 {ctrl.parameters.Length}개)");

        var motion = st.motion;
        if (motion == null)
        {
            Debug.LogError($"{Tag} ❌ {label}.{stateName} 모션 NULL — 결함(후보①)" + (repairOnDefect ? " — 자동 수리 분기 진입" : " — 실측만(대조군 무수리)"));
            if (repairOnDefect) RepairMotion(ctrl, st, stateName, expectedFbx, label);
            return;
        }
        var clip = motion as AnimationClip;
        if (clip == null)
        {
            Debug.LogError($"{Tag} ⚠️ {label}.{stateName} 모션이 AnimationClip 아님: type={motion.GetType().Name} name={motion.name} — 자동 수리 불가(수동 확인)");
            return;
        }

        var srcPath = AssetDatabase.GetAssetPath(clip);
        var srcGuid = AssetDatabase.AssetPathToGUID(srcPath);
        var expectedGuid = AssetDatabase.AssetPathToGUID(expectedFbx);
        Debug.Log($"{Tag} {label}.{stateName} → clip=\"{clip.name}\" length={clip.length:F3}s frames≈{Mathf.RoundToInt(clip.length * clip.frameRate)}(frameRate={clip.frameRate:F0}) isLooping={clip.isLooping} legacy={clip.legacy} humanMotion={clip.humanMotion} src=\"{srcPath}\" guid={srcGuid} expectedGuid={expectedGuid}");

        if (clip.length <= 0.0001f)
        {
            Debug.LogError($"{Tag} ❌ {label}.{stateName} 클립 길이 0(빈 테이크) — 결함(후보①)" + (repairOnDefect ? " — 자동 수리 분기 진입" : " — 실측만(대조군 무수리)"));
            if (repairOnDefect) RepairMotion(ctrl, st, stateName, expectedFbx, label);
            return;
        }
        if (!string.IsNullOrEmpty(expectedGuid) && srcGuid != expectedGuid)
            Debug.LogWarning($"{Tag} ⚠️ {label}.{stateName} 소스가 기대 FBX와 불일치: {srcGuid}({srcPath}) ≠ {expectedGuid} — 교차 배선 정밀 확인 필요");

        // 상태 전이 설정 실측 (잔여 후보 ③ — 즉시 탈출/도달 불가 판정 근거)
        LogTransitions(allStates, st, label, stateName);
    }

    static void LogTransitions(List<AnimatorState> allStates, AnimatorState st, string label, string stateName)
    {
        var outs = st.transitions ?? Array.Empty<AnimatorStateTransition>();
        Debug.Log($"{Tag} 전이[{label}.{stateName} →] 전출 {outs.Length}건");
        foreach (var t in outs)
        {
            if (t == null) { Debug.LogWarning($"{Tag} 전이[{label}.{stateName} →] NULL 전이 1건"); continue; }
            var dest = t.destinationState != null ? t.destinationState.name
                     : t.destinationStateMachine != null ? t.destinationStateMachine.name : "(null)";
            var conds = string.Join(", ", (t.conditions ?? Array.Empty<AnimatorCondition>())
                          .Select(c => $"[{c.mode}] {c.parameter} {c.threshold}"));
            Debug.Log($"{Tag} 전이[{label}.{stateName} →] dest={dest} hasExitTime={t.hasExitTime} exitTime={t.exitTime:F3} duration={t.duration:F3} offset={t.offset:F3} interruptionSource={t.interruptionSource} conditions={(conds.Length > 0 ? conds : "(없음 — 무조건 전이)")}");
        }
        // 전입 전이 스캔 — 도달 경로 존재 판정 근거
        var ins = allStates.SelectMany(s => (s != null ? s.transitions : Array.Empty<AnimatorStateTransition>()).OfType<AnimatorStateTransition>())
                           .Where(t => t != null && t.destinationState == st).ToList();
        Debug.Log($"{Tag} 전이[→ {label}.{stateName}] 전입 {ins.Count}건");
        foreach (var t in ins)
        {
            var srcName = allStates.FirstOrDefault(s => s != null && s.transitions != null && s.transitions.Contains(t))?.name ?? "(?)";
            var conds = string.Join(", ", (t.conditions ?? Array.Empty<AnimatorCondition>())
                          .Select(c => $"[{c.mode}] {c.parameter} {c.threshold}"));
            Debug.Log($"{Tag} 전이[→ {label}.{stateName}] src={srcName} hasExitTime={t.hasExitTime} exitTime={t.exitTime:F3} duration={t.duration:F3} conditions={(conds.Length > 0 ? conds : "(없음)")}");
        }
    }

    /// <summary>자동 수리: 기대 FBX에서 유효 클립(길이>0.1s, Take 001 우선)을 찾아 state에 재배선. 유니크 가드 — 결함이 아니면 호출되지 않음.</summary>
    static void RepairMotion(AnimatorController ctrl, AnimatorState st, string stateName, string fbxPath, string label)
    {
        var all = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
        var valid = all?.OfType<AnimationClip>()
            .Where(c => c != null && c.length > 0.1f)
            .OrderByDescending(c => c.name == "Take 001")
            .ThenByDescending(c => c.length)
            .FirstOrDefault();
        if (valid == null)
        {
            Debug.LogError($"{Tag} ❌ 자동 수리 불가 — {fbxPath}에서 유효 클립(길이>0.1s) 미발견. FBX 임포트/테이크 수동 확인 필요");
            return;
        }
        var oldName = st.motion != null ? st.motion.name : "NULL";
        st.motion = valid;
        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log($"{Tag} 🔧 자동 수리: {label}.{stateName} 모션 {oldName} → \"{valid.name}\" (length={valid.length:F3}s humanMotion={valid.humanMotion} src={fbxPath}) — SetDirty+SaveAssets 완료. 재프로브 권장");
    }

    /// <summary>잔여 후보 ② — 플레이어 모델(계약 경로: Player_Rigged_Heat.fbx) Animator 아바타 실측. 경로 부재 시 Resources 전수 프리팹 검색 폴백.</summary>
    static void ProbePlayerAvatar()
    {
        var path = PlayerModelFbx;
        if (!File.Exists(path)) path = FindPlayerModelInResources();
        if (path == null)
        {
            Debug.LogError($"{Tag} ❌ 플레이어 모델 미발견(계약 경로 부재 + Resources 전수 폴백 실패) — 아바타 실측 보류");
            return;
        }
        Debug.Log($"{Tag} 플레이어 모델 경로: {path}");

        var all = AssetDatabase.LoadAllAssetsAtPath(path);
        var root = all?.OfType<GameObject>().FirstOrDefault(g => g.transform.parent == null);
        if (root == null)
        {
            Debug.LogError($"{Tag} ❌ 플레이어 모델 루트 GameObject 미발견: {path}");
            return;
        }
        var anims = root.GetComponentsInChildren<Animator>(true);
        Debug.Log($"{Tag} 플레이어 모델 Animator {anims.Length}개 (root={root.name})");
        foreach (var a in anims)
        {
            var av = a.avatar;
            var ctrlName = a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "NULL";
            if (av == null)
            {
                Debug.LogError($"{Tag} ❌ 플레이어 Animator 아바타 NULL (path={a.transform.name} controller={ctrlName}) — 휴머노이드 리타깃 불가(결함 후보② 확정급)");
                continue;
            }
            int bones = 0;
            try { bones = av.humanDescription.human.Length; } catch { /* 일부 버전 예외 무시 */ }
            Debug.Log($"{Tag} 플레이어 Animator(path={a.transform.name} controller={ctrlName}) avatar=\"{av.name}\" isHuman={av.isHuman} humanBones={bones}");
        }
        // FBX 서브에셋 Avatar 전체 열거(Animator에 안 붙은 경우 대비)
        var avatars = all.OfType<Avatar>().Where(x => x != null).ToList();
        foreach (var av in avatars)
        {
            int bones = 0;
            try { bones = av.humanDescription.human.Length; } catch { /* 일부 버전 예외 무시 */ }
            Debug.Log($"{Tag} 서브에셋 Avatar: \"{av.name}\" isHuman={av.isHuman} humanBones={bones}");
        }
        Debug.Log($"{Tag} 판정 기준: 클립 humanMotion=true && 플레이어 아바타 isHuman=true → 휴머노이드 리타깃 경로 성립(후보② 해소). humanMotion=false → 클립이 제네릭 산출(후보② 결함).");
    }

    static string FindPlayerModelInResources()
    {
        var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources" });
        string hit = null;
        foreach (var g in guids)
        {
            var p = AssetDatabase.GUIDToAssetPath(g);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go == null) continue;
            // PlayerMovement 소유 프리팹 탐색(부모 힌트) — MonoBehaviour 스크립트명 문자열 매칭
            var hasPlayerMovement = go.GetComponentsInChildren<MonoBehaviour>(true)
                .Any(mb => mb != null && mb.GetType().Name == "PlayerMovement");
            var hasPlayerName = go.name.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0;
            if (hasPlayerMovement || hasPlayerName)
            {
                Debug.Log($"{Tag} 폴백 플레이어 프리팹 후보: {p} (playerMovement={hasPlayerMovement} nameMatch={hasPlayerName})");
                if (hit == null) hit = p;
            }
        }
        return hit;
    }

    static void CollectStates(AnimatorStateMachine sm, List<AnimatorState> acc)
    {
        if (sm == null) return;
        foreach (var cs in sm.states) if (cs.state != null) acc.Add(cs.state);
        foreach (var child in sm.stateMachines)
        {
            if (child.stateMachine == null) continue;
            CollectStates(child.stateMachine, acc);
        }
    }
}
