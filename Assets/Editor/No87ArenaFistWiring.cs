// [무번호 87-P1 v2] 아레나 맨손 격투 — 병사 신규 FBX 5종 수용 + SoldierFist_AC 생성.
// v1 실측: 신규 FBX=Generic(animationType 2, Root/Hips/Spine1 스켈레톤) vs 병사 본체=Humanoid(3, 믹사모) —
// 경로 바인딩 불일치 21/21로 감사 게이트가 차단. v2: **Humanoid 재임포트 전환**(머슬 공간 리타깃)이 표준 해법 —
// 플레이어 믹사모 클립과 동일 파이프라인. 게이트: humanMotion + avatar 유효성(자동 매핑 실패 시 미기록+보고).
// 실행: Unity -batchmode -executeMethod No87ArenaFistWiring.Run
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class No87ArenaFistWiring
{
    const string SrcCtrlPath = "Assets/Resources/Animation/Controllers/SoldierShield_AC.controller";
    const string DstCtrlPath = "Assets/Resources/Animation/Controllers/SoldierFist_AC.controller";
    const string SoldierFbxPath = "Assets/Resources/Models/UserProvided/fbx/soldier_lv1-20_rigged.fbx";

    static readonly (string path, string stateName)[] ActionFbx =
    {
        ("Assets/Animations/병사 펀치.fbx", "Punch"),
        ("Assets/Animations/병사 돌려차기.fbx", "Kick"),
        ("Assets/Animations/병사 박치기.fbx", "Headbutt"),
        ("Assets/Animations/병사 펀치 후 돌려차기.fbx", "PunchKick"),
        ("Assets/Animations/병사 막기.fbx", "Block"),
    };

    public static void Run()
    {
        Debug.Log("[No87] === 맨손 결투 배치 v2 시작 ===");

        // ── 0) Humanoid 재임포트 전환 (Generic→Humanoid — 머슬 리타깃 경로) ──
        foreach (var (path, stateName) in ActionFbx)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[No87] ❌ 임포터 없음: {path}");
                EditorApplication.Exit(2);
                return;
            }
            if (importer.animationType != (ModelImporterAnimationType)3)   // 3=Humanoid(Unity 6 enum 멤버명 불일치 회피 — meta animationType: 3과 동일)
            {
                importer.animationType = (ModelImporterAnimationType)3;
                importer.SaveAndReimport();
                Debug.Log($"[No87] Humanoid 재임포트: {stateName} ({System.IO.Path.GetFileName(path)}) — Generic({importer.animationType}→3 전환 실행)");
            }
            else Debug.Log($"[No87] 이미 Humanoid: {stateName} — 스킵");
        }

        // ── 1) 클립 수집 + 실측 + humanoid 게이트 ──
        var clips = new Dictionary<string, AnimationClip>();
        var gateFail = new List<string>();
        foreach (var (path, stateName) in ActionFbx)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .Where(c => !c.name.StartsWith("__")).ToArray();
            if (all.Length == 0)
            {
                Debug.LogError($"[No87] ❌ 클립 없음: {path}");
                EditorApplication.Exit(2);
                return;
            }
            var clip = all.OrderByDescending(c => c.length).First();
            clips[stateName] = clip;
            Debug.Log($"[No87] 클립 실측 {stateName} ← '{clip.name}' len={clip.length:F2}s loop={clip.isLooping} humanMotion={clip.humanMotion}");

            var avatar = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();   // Unity 6: ModelImporter.avatar 부재 — FBX 서브에셋에서 획득
            var avOk = avatar != null && avatar.isValid && avatar.isHuman;
            Debug.Log($"[No87] 아바타 게이트 {stateName}: valid={avatar?.isValid} isHuman={avatar?.isHuman}");
            if (!clip.humanMotion || !avOk) gateFail.Add(stateName);
        }
        if (gateFail.Count > 0)
        {
            Debug.LogError($"[No87] ❌ Humanoid 변환/매핑 실패 {gateFail.Count}종({string.Join(",", gateFail)}) — 컨트롤러 생성 중단(미기록). 자동 매핑이 이 스켈레톤을 수용 못함 — 임포트 설정 수동 점검 필요.");
            EditorApplication.Exit(2);
            return;
        }
        Debug.Log("[No87] ✅ Humanoid 게이트 통과(5/5 humanMotion+avatar valid) — 생성 진행");

        // ── 2) 소스 컨트롤러 승계 원본 ──
        var src = AssetDatabase.LoadAssetAtPath<AnimatorController>(SrcCtrlPath);
        if (src == null)
        {
            Debug.LogError($"[No87] ❌ 소스 컨트롤러 미로드: {SrcCtrlPath}");
            EditorApplication.Exit(2);
            return;
        }
        var srcSm = src.layers[0].stateMachine;
        var srcStates = srcSm.states.ToDictionary(s => s.state.name, s => s.state);

        // ── 3) SoldierFist_AC 생성(멱등 재생성) ──
        if (AssetDatabase.LoadAssetAtPath<Object>(DstCtrlPath) != null)
        {
            AssetDatabase.DeleteAsset(DstCtrlPath);
            Debug.Log("[No87] 기존 SoldierFist_AC 삭제(재생성)");
        }
        var dst = AnimatorController.CreateAnimatorControllerAtPath(DstCtrlPath);
        dst.AddParameter("Speed", AnimatorControllerParameterType.Float);   // SoldierShield_AC 파라미터 승계
        dst.AddParameter("MoveRate", AnimatorControllerParameterType.Float);
        dst.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
        dst.AddParameter("Death", AnimatorControllerParameterType.Trigger);
        dst.AddParameter("Block", AnimatorControllerParameterType.Trigger); // [무번호 87] 신규
        var sm = dst.layers[0].stateMachine;

        var states = new Dictionary<string, AnimatorState>();
        foreach (var name in new[] { "Idle", "Move", "Hit", "Death", "Punch", "Kick", "Headbutt", "PunchKick", "Block" })
            states[name] = sm.AddState(name);

        // 모션 승계 — Idle/Move/Hit/Death는 소스 그대로, 공격 5종은 신규(Humanoid) 클립
        states["Idle"].motion = srcStates["Idle"].motion;
        states["Move"].motion = srcStates["Move"].motion;
        states["Hit"].motion = srcStates["Hit"].motion;
        states["Death"].motion = srcStates["Death"].motion;
        foreach (var (path, stateName) in ActionFbx) states[stateName].motion = clips[stateName];
        sm.defaultState = states["Idle"];

        // 보행 전이 — 소스 Idle/Move의 Speed 조건 그대로 복제
        foreach (var pair in new[] { ("Idle", "Move"), ("Move", "Idle") })
        {
            var srcT = srcStates[pair.Item1].transitions
                .FirstOrDefault(t => t.destinationState != null && t.destinationState.name == pair.Item2);
            if (srcT == null) { Debug.LogWarning($"[No87] 소스 전이 미발견: {pair.Item1}→{pair.Item2}"); continue; }
            var t = states[pair.Item1].AddTransition(states[pair.Item2]);
            t.hasExitTime = false;
            t.duration = srcT.duration;
            foreach (var c in srcT.conditions) t.AddCondition(c.mode, c.threshold, c.parameter);
            Debug.Log($"[No87] 보행 전이 {pair.Item1}→{pair.Item2} ({string.Join(",", srcT.conditions.Select(c => $"{c.parameter}{c.mode}{c.threshold:F2}"))})");
        }

        // 트리거 진입 — Idle/Move에서 Hit/Death/Block (소스 진입 패턴: hasExitTime=false, dur 0.1)
        foreach (var from in new[] { "Idle", "Move" })
            foreach (var trig in new[] { "Hit", "Death", "Block" })
            {
                var t = states[from].AddTransition(states[trig]);
                t.hasExitTime = false;
                t.duration = 0.1f;
                t.AddCondition(AnimatorConditionMode.If, 0f, trig);
            }

        // 액션 복귀 — Punch/Kick/Headbutt/PunchKick/Block/Hit → Idle (exit-time 0.92, dur 0.12 — 소스 Attack/Hit 복귀 패턴 승계)
        foreach (var name in new[] { "Punch", "Kick", "Headbutt", "PunchKick", "Block", "Hit" })
        {
            var t = states[name].AddTransition(states["Idle"]);
            t.hasExitTime = true;
            t.exitTime = 0.92f;
            t.duration = 0.12f;
        }
        // Death — 출구 없음(쓰러짐 유지, 패자 관람 계약)

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[No87] ✅ SoldierFist_AC 생성 완료 — 상태 9종(Idle/Move/Hit/Death/Punch/Kick/Headbutt/PunchKick/Block)");
        EditorApplication.Exit(0);
    }
}
